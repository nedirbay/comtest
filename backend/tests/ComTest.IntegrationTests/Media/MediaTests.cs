using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Media;
using ComTest.Infrastructure.Persistence.Outbox;
using ComTest.IntegrationTests.Catalog;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ComTest.IntegrationTests.Media;

[Collection(ApiCollection.Name)]
public sealed class MediaUploadTests(ApiFixture api)
{
    private static readonly HttpClient Direct = new();

    [Fact]
    public async Task Upload_IsReencoded_IntoThreeSizes_AndTheOriginalIsDeleted()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var asset = await UploadAsync(admin, TestImages.Png(2000, 1000), "image/png");

        Assert.Equal("ready", asset.GetProperty("status").GetString());
        Assert.Equal(2000, asset.GetProperty("width").GetInt32());

        foreach (var (field, width) in new[] { ("thumbnail", 320), ("medium", 800), ("large", 1600) })
        {
            var response = await Direct.GetAsync(asset.GetProperty(field).GetString());
            var bytes = await response.Content.ReadAsByteArrayAsync();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(TestImages.IsWebp(bytes), field);
            Assert.Equal("image/webp", response.Content.Headers.ContentType!.MediaType);
            Assert.Contains("immutable", response.Headers.CacheControl!.ToString(), StringComparison.Ordinal);

            using var decoded = TestImages.Decode(bytes);
            Assert.Equal(width, decoded.Width);
        }

        var incoming = await api.QueryAsOwnerAsync<string>("SELECT incoming_key FROM media_assets WHERE id = @id", ("id", asset.GetProperty("id").GetGuid()));
        var storage = api.Factory.Services.GetRequiredService<S3FileStorage>();

        Assert.Null(await storage.StatAsync(incoming!, CancellationToken.None));
    }

    [Fact]
    public async Task SmallImages_AreNotBlownUp()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var asset = await UploadAsync(admin, TestImages.Png(100, 50), "image/png");

        var bytes = await Direct.GetByteArrayAsync(asset.GetProperty("large").GetString());
        using var decoded = TestImages.Decode(bytes);

        Assert.Equal(100, decoded.Width);
    }

    [Fact]
    public async Task HiddenMetadata_DoesNotSurvive()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var original = TestImages.JpegWithExif();

        Assert.Contains(TestImages.HiddenMetadata, Encoding.ASCII.GetString(original), StringComparison.Ordinal);

        var asset = await UploadAsync(admin, original, "image/jpeg");

        foreach (var field in new[] { "thumbnail", "medium", "large" })
        {
            var bytes = await Direct.GetByteArrayAsync(asset.GetProperty(field).GetString());

            Assert.DoesNotContain(TestImages.HiddenMetadata, Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
            Assert.DoesNotContain("Exif", Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task PhotoOrientation_IsAppliedToThePixels()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        // Orientation 6: a photo taken with the phone upright, stored sideways.
        var asset = await UploadAsync(admin, TestImages.JpegWithExif(orientation: 6, width: 400, height: 200), "image/jpeg");

        Assert.Equal(200, asset.GetProperty("width").GetInt32());
        Assert.Equal(400, asset.GetProperty("height").GetInt32());

        using var decoded = TestImages.Decode(await Direct.GetByteArrayAsync(asset.GetProperty("large").GetString()));

        Assert.Equal(200, decoded.Width);
        Assert.Equal(400, decoded.Height);
    }

    [Fact]
    public async Task AScriptAppendedToAnImage_IsLeftBehind()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var asset = await UploadAsync(admin, TestImages.PngWithAppendedScript(), "image/png");

        Assert.Equal("ready", asset.GetProperty("status").GetString());

        var bytes = await Direct.GetByteArrayAsync(asset.GetProperty("large").GetString());

        Assert.DoesNotContain("<script>", Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>", "image/jpeg")]
    [InlineData("MZ this is really a program", "image/png")]
    [InlineData("<svg xmlns='http://www.w3.org/2000/svg' onload='alert(1)'/>", "image/webp")]
    public async Task AFileLyingAboutItsType_IsRejected_AndDeleted(string content, string claimedType)
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var asset = await UploadAsync(admin, Encoding.UTF8.GetBytes(content), claimedType);

        Assert.Equal("rejected", asset.GetProperty("status").GetString());
        Assert.Equal(ErrorCodes.Media.UnsupportedType, asset.GetProperty("rejectionCode").GetString());
        Assert.Equal(JsonValueKind.Null, asset.GetProperty("large").ValueKind);

        var incoming = await api.QueryAsOwnerAsync<string>("SELECT incoming_key FROM media_assets WHERE id = @id", ("id", asset.GetProperty("id").GetGuid()));

        Assert.Null(await api.Factory.Services.GetRequiredService<S3FileStorage>().StatAsync(incoming!, CancellationToken.None));
    }

    [Fact]
    public async Task ACorruptImage_IsRejected()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var png = TestImages.Png();

        // A genuine signature followed by garbage.
        byte[] corrupt = [.. png.AsSpan(0, 16), .. Encoding.ASCII.GetBytes(new string('x', 200))];

        var asset = await UploadAsync(admin, corrupt, "image/png");

        Assert.Equal("rejected", asset.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ADecompressionBomb_IsRejected_WithoutBeingDecoded()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var asset = await UploadAsync(admin, TestImages.DecompressionBomb(), "image/png");

        Assert.Equal("rejected", asset.GetProperty("status").GetString());
        Assert.Equal(ErrorCodes.Media.TooLarge, asset.GetProperty("rejectionCode").GetString());
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("application/x-msdownload")]
    [InlineData("image/gif")]
    public async Task OtherFileTypes_AreRefusedAtTheStart(string contentType)
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "x", contentType, sizeBytes = 100 });

        Assert.Equal(ErrorCodes.Media.UnsupportedType, await response.ReadCode());
    }

    [Fact]
    public async Task TooLargeFiles_AreRefused_EvenWhenTheClientUndersellsThem()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var declared = await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "x.png", contentType = "image/png", sizeBytes = 11L * 1024 * 1024 });
        Assert.Equal(ErrorCodes.Media.TooLarge, await declared.ReadCode());

        // Claims 10 bytes, sends far more.
        var png = TestImages.Png();
        var ticket = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "x.png", contentType = "image/png", sizeBytes = 10 }));

        await PutAsync(ticket, png, "image/png");

        var id = ticket.GetProperty("id").GetGuid();
        await SalesHelpers.ReadAsync(await admin.PostAsync($"/api/v1/admin/media/{id}/complete", null));
        await DrainAsync();

        var asset = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/media/{id}");

        Assert.Equal("rejected", asset.GetProperty("status").GetString());
        Assert.Equal(ErrorCodes.Media.TooLarge, asset.GetProperty("rejectionCode").GetString());
    }

    [Fact]
    public async Task Quota_CountsStoredFiles_AndUploadsInProgress()
    {
        var store = await api.CreateStoreAsync();
        await api.ExecuteAsOwnerAsync("UPDATE tenants SET storage_quota_megabytes = 1 WHERE id = @id", ("id", store.TenantId));
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var first = await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "a.png", contentType = "image/png", sizeBytes = 700_000 });
        var second = await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "b.png", contentType = "image/png", sizeBytes = 700_000 });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(ErrorCodes.Media.QuotaExceeded, await second.ReadCode());
    }

    [Theory]
    [InlineData("../../../etc/passwd.png", "passwd.png")]
    [InlineData("..\\..\\windows\\system32\\evil.png", "evil.png")]
    [InlineData("normal name.png", "normal name.png")]
    public async Task FileNames_CannotReachTheStoragePath(string fileName, string expectedDisplayName)
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var ticket = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName, contentType = "image/png", sizeBytes = 100 }));
        var id = ticket.GetProperty("id").GetGuid();

        var key = await api.QueryAsOwnerAsync<string>("SELECT incoming_key FROM media_assets WHERE id = @id", ("id", id));
        var display = await api.QueryAsOwnerAsync<string>("SELECT display_name FROM media_assets WHERE id = @id", ("id", id));

        Assert.Equal($"tenants/{store.TenantId:N}/incoming/{id:N}", key);
        Assert.Equal(expectedDisplayName, display);
        Assert.DoesNotContain("..", ticket.GetProperty("uploadUrl").GetString()!.Split('?')[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheBucket_IsNotOpenToTheWorld()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var asset = await UploadAsync(admin, TestImages.Png(), "image/png");
        var signed = asset.GetProperty("large").GetString()!;

        Assert.Equal(HttpStatusCode.OK, (await Direct.GetAsync(signed)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Direct.GetAsync(signed.Split('?')[0])).StatusCode);

        var list = await Direct.GetAsync($"{api.StorageUrl}/comtest-test/");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
    }

    [Fact]
    public async Task AnUploadUrl_OnlyWorksForItsOwnFileAndType()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var ticket = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "x.png", contentType = "image/png", sizeBytes = 1000 }));
        var url = ticket.GetProperty("uploadUrl").GetString()!;

        using var wrongType = new ByteArrayContent(TestImages.Png());
        wrongType.Headers.ContentType = new MediaTypeHeaderValue("text/html");

        Assert.Equal(HttpStatusCode.Forbidden, (await Direct.PutAsync(url, wrongType)).StatusCode);

        var otherKey = new Uri(url);
        var redirected = $"{otherKey.Scheme}://{otherKey.Authority}/comtest-test/tenants/someone-else/evil.png{otherKey.Query}";

        using var content = new ByteArrayContent(TestImages.Png());
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");

        Assert.Equal(HttpStatusCode.Forbidden, (await Direct.PutAsync(redirected, content)).StatusCode);
    }

    [Fact]
    public async Task CompletingBeforeUploading_IsRefused()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var ticket = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "x.png", contentType = "image/png", sizeBytes = 1000 }));

        var response = await admin.PostAsync($"/api/v1/admin/media/{ticket.GetProperty("id").GetGuid()}/complete", null);

        Assert.Equal(ErrorCodes.Media.UploadMissing, await response.ReadCode());
    }

    [Fact]
    public async Task Permissions_AndIsolation_AreEnforced()
    {
        var store = await api.CreateStoreAsync();
        var other = await api.CreateStoreAsync();
        var (_, staffToken, _) = await api.AddStaffAsync(store, Roles.Staff);
        var customer = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var staff = api.CreateStoreClient(store.Slug, staffToken);
        using var shopper = api.CreateStoreClient(store.Slug, customer.AccessToken);
        using var anonymous = api.CreateStoreClient(store.Slug);
        using var attacker = api.CreateStoreClient(other.Slug, other.AccessToken);

        var body = new { fileName = "x.png", contentType = "image/png", sizeBytes = 1000 };

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/admin/media/uploads", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await shopper.PostAsJsonAsync("/api/v1/admin/media/uploads", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/v1/admin/media/uploads", body)).StatusCode);

        var asset = await UploadAsync(admin, TestImages.Png(), "image/png");
        var id = asset.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await attacker.GetAsync($"/api/v1/admin/media/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.DeleteAsync($"/api/v1/admin/media/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.PostAsync($"/api/v1/admin/media/{id}/complete", null)).StatusCode);
        Assert.Equal(0, (await attacker.GetFromJsonAsync<JsonElement>("/api/v1/admin/media")).GetProperty("totalCount").GetInt32());

        // Another store cannot put this store's image on its own product either.
        var slug = CatalogRequests.NewSlug();
        var stolen = await attacker.PostAsJsonAsync("/api/v1/admin/catalog/products", new
        {
            slug,
            translations = new[] { new { language = "tk", name = "X" } },
            variants = new[] { new { sku = "S-" + slug, price = 1m, currency = "TMT" } },
            images = new[] { new { mediaAssetId = id } },
        });

        Assert.Equal(ErrorCodes.Media.NotReady, await stolen.ReadCode());
    }

    [Fact]
    public async Task ProductGallery_ShowsInTheStorefront_AndAnImageInUseCannotBeDeleted()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var visitor = api.CreateStoreClient(store.Slug);

        var first = await UploadAsync(admin, TestImages.Png(), "image/png");
        var second = await UploadAsync(admin, TestImages.Jpeg(), "image/jpeg");
        var firstId = first.GetProperty("id").GetGuid();
        var secondId = second.GetProperty("id").GetGuid();
        var slug = CatalogRequests.NewSlug();

        var created = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/catalog/products", new
        {
            slug,
            translations = new[] { new { language = "tk", name = "Suratly" } },
            variants = new[] { new { sku = "S-" + slug, price = 1m, currency = "TMT" } },
            images = new object[] { new { mediaAssetId = secondId, altText = "Öňden" }, new { mediaAssetId = firstId } },
        }));

        var productId = created.GetProperty("id").GetGuid();
        var variantId = created.GetProperty("variants")[0].GetProperty("id").GetGuid();
        await admin.PublishAsync(productId);

        var detail = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{slug}");
        var images = detail.GetProperty("images");

        Assert.Equal(2, images.GetArrayLength());
        Assert.Equal(secondId, images[0].GetProperty("mediaAssetId").GetGuid());
        Assert.Equal("Öňden", images[0].GetProperty("altText").GetString());
        Assert.Equal(HttpStatusCode.OK, (await Direct.GetAsync(images[0].GetProperty("large").GetString())).StatusCode);

        var listing = await visitor.GetFromJsonAsync<JsonElement>("/api/v1/catalog/products");
        Assert.False(string.IsNullOrEmpty(listing.GetProperty("items")[0].GetProperty("imageUrl").GetString()));

        var inUse = await admin.DeleteAsync($"/api/v1/admin/media/{firstId}");
        Assert.Equal(ErrorCodes.Media.InUse, await inUse.ReadCode());

        // Leaving images out keeps them; an empty list removes them.
        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{productId}", new
        {
            slug,
            translations = new[] { new { language = "tk", name = "Täze at" } },
            variants = new[] { new { id = variantId, sku = "S-" + slug, price = 1m, currency = "TMT" } },
        }));

        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/catalog/products/{productId}")).GetProperty("images").GetArrayLength());

        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{productId}", new
        {
            slug,
            translations = new[] { new { language = "tk", name = "Täze at" } },
            variants = new[] { new { id = variantId, sku = "S-" + slug, price = 1m, currency = "TMT" } },
            images = Array.Empty<object>(),
        }));

        var baseKey = await api.QueryAsOwnerAsync<string>("SELECT base_key FROM media_assets WHERE id = @id", ("id", firstId));

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/media/{firstId}")).StatusCode);
        Assert.Null(await api.Factory.Services.GetRequiredService<S3FileStorage>().StatAsync(baseKey + "/large.webp", CancellationToken.None));
    }

    [Fact]
    public async Task AnImageNotYetProcessed_CannotBeUsed()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var ticket = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "x.png", contentType = "image/png", sizeBytes = 1000 }));
        var slug = CatalogRequests.NewSlug();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/catalog/products", new
        {
            slug,
            translations = new[] { new { language = "tk", name = "X" } },
            variants = new[] { new { sku = "S-" + slug, price = 1m, currency = "TMT" } },
            images = new[] { new { mediaAssetId = ticket.GetProperty("id").GetGuid() } },
        });

        Assert.Equal(ErrorCodes.Media.NotReady, await response.ReadCode());
    }

    [Fact]
    public async Task WhenStorageIsDown_TheApiStillStarts_AndOnlyUploadsFail()
    {
        var store = await api.CreateStoreAsync();

        // Nothing listens on this port, and the bucket check runs at start-up.
        using var factory = api.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<StorageOptions>(options =>
                {
                    options.ServiceUrl = "http://127.0.0.1:1";
                    options.CreateBucketIfMissing = true;
                })));

        var clientOptions = ApiFixture.DefaultClientOptions();
        clientOptions.BaseAddress = new Uri($"https://{store.Slug}.{ApiFixture.BaseDomain}");

        var started = System.Diagnostics.Stopwatch.StartNew();
        using var client = factory.CreateClient(clientOptions);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/catalog/products")).StatusCode);
        Assert.True(started.Elapsed < TimeSpan.FromSeconds(20), $"Start-up took {started.Elapsed}.");
    }

    [Fact]
    public async Task AbandonedUploads_AreCleanedUp_ButNotFinishedOnes()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var ready = await UploadAsync(admin, TestImages.Png(), "image/png");
        var abandoned = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "x.png", contentType = "image/png", sizeBytes = 1000 }));
        var abandonedId = abandoned.GetProperty("id").GetGuid();

        await api.ExecuteAsOwnerAsync("UPDATE media_assets SET upload_expires_at = now() - interval '1 hour' WHERE id = @id", ("id", abandonedId));

        await api.Factory.Services.GetRequiredService<MediaHousekeeping>().RemoveAbandonedAsync(CancellationToken.None);

        Assert.Equal(0L, await api.QueryAsOwnerAsync<long>("SELECT count(*) FROM media_assets WHERE id = @id", ("id", abandonedId)));
        Assert.Equal(1L, await api.QueryAsOwnerAsync<long>("SELECT count(*) FROM media_assets WHERE id = @id", ("id", ready.GetProperty("id").GetGuid())));
    }

    private async Task<JsonElement> UploadAsync(HttpClient admin, byte[] content, string contentType)
    {
        var ticket = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/media/uploads", new { fileName = "photo.bin", contentType, sizeBytes = content.Length }));

        await PutAsync(ticket, content, contentType);

        var id = ticket.GetProperty("id").GetGuid();
        var completed = await SalesHelpers.ReadAsync(await admin.PostAsync($"/api/v1/admin/media/{id}/complete", null));

        Assert.Equal("processing", completed.GetProperty("status").GetString());

        await DrainAsync();

        return await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/media/{id}");
    }

    private static async Task PutAsync(JsonElement ticket, byte[] content, string contentType)
    {
        using var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var response = await Direct.PutAsync(ticket.GetProperty("uploadUrl").GetString(), body);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task DrainAsync()
    {
        var processor = api.Factory.Services.GetRequiredService<OutboxProcessor>();

        while (await processor.ProcessBatchAsync(CancellationToken.None) > 0)
        {
        }
    }
}
