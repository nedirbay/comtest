using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;

namespace ComTest.IntegrationTests.Catalog;

public static class CatalogRequests
{
    public static object Product(string slug, string sku, decimal price = 100m, decimal? cost = 60m, string name = "Telefon", string? description = null, Guid? categoryId = null, Guid? brandId = null, uint? version = null, Guid? variantId = null, object? attributes = null) => new
    {
        slug,
        translations = new[] { new { language = "tk", name, description } },
        variants = new[] { new { id = variantId, sku, price, currency = "TMT", costAmount = cost, compareAtAmount = (decimal?)null, options = new Dictionary<string, string> { ["color"] = "gara" } } },
        categoryId,
        brandId,
        attributes,
        version,
    };

    public static async Task<JsonElement> CreateProductAsync(this HttpClient client, object product)
    {
        var response = await client.PostAsJsonAsync("/api/v1/admin/catalog/products", product);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static async Task<JsonElement> PublishAsync(this HttpClient client, Guid id)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/admin/catalog/products/{id}/status", new { status = "published" });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static string NewSlug(string prefix = "item") => $"{prefix}-{Guid.NewGuid():N}"[..24];

    public static async Task<string?> ReadCode(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

}

[Collection(ApiCollection.Name)]
public sealed class ProductTests(ApiFixture api)
{
    [Fact]
    public async Task DraftProduct_IsInvisible_UntilPublished_AndArchivingHidesItAgain()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var visitor = api.CreateStoreClient(store.Slug);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug));
        var id = created.GetProperty("id").GetGuid();

        Assert.Equal("draft", created.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/v1/catalog/products/{slug}")).StatusCode);

        await admin.PublishAsync(id);

        Assert.Equal(HttpStatusCode.OK, (await visitor.GetAsync($"/api/v1/catalog/products/{slug}")).StatusCode);

        var archived = await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{id}/status", new { status = "archived" });
        Assert.Equal(HttpStatusCode.OK, archived.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await visitor.GetAsync($"/api/v1/catalog/products/{slug}")).StatusCode);
    }

    [Fact]
    public async Task CostPrice_IsVisibleToStaff_ButNeverToVisitors()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var visitor = api.CreateStoreClient(store.Slug);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug, cost: 54321m));
        await admin.PublishAsync(created.GetProperty("id").GetGuid());

        Assert.Equal(54321m, created.GetProperty("variants")[0].GetProperty("costAmount").GetDecimal());

        var detail = await (await visitor.GetAsync($"/api/v1/catalog/products/{slug}")).Content.ReadAsStringAsync();
        var listing = await (await visitor.GetAsync("/api/v1/catalog/products")).Content.ReadAsStringAsync();

        foreach (var body in new[] { detail, listing })
        {
            Assert.DoesNotContain("54321", body, StringComparison.Ordinal);
            Assert.DoesNotContain("costAmount", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("taxGroup", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("<script>alert(1)</script><p>ok</p>", "<script")]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript:")]
    [InlineData("<iframe src=\"https://evil.test\"></iframe>", "<iframe")]
    [InlineData("<p style=\"background:url(javascript:x)\">x</p>", "style=")]
    public async Task Description_IsCleanedOfAnythingThatCouldRun(string html, string forbidden)
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug, description: html));

        var stored = created.GetProperty("translations")[0].GetProperty("description").GetString();

        Assert.DoesNotContain(forbidden, stored, StringComparison.OrdinalIgnoreCase);

        var inDatabase = await api.QueryAsOwnerAsync<string>("SELECT description FROM product_translations WHERE product_id = @id", ("id", created.GetProperty("id").GetGuid()));

        Assert.DoesNotContain(forbidden, inDatabase, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Description_KeepsSafeFormatting()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug, description: "<p><strong>Täze</strong> haryt</p>"));

        Assert.Equal("<p><strong>Täze</strong> haryt</p>", created.GetProperty("translations")[0].GetProperty("description").GetString());
    }

    [Theory]
    [InlineData(-1, "TMT")]
    [InlineData(100, "EUR")]
    [InlineData(100, "tmt")]
    [InlineData(99999999999, "TMT")]
    public async Task InvalidPrices_AreRefused(decimal price, string currency)
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/catalog/products", new
        {
            slug,
            translations = new[] { new { language = "tk", name = "X" } },
            variants = new[] { new { sku = "S-" + slug, price, currency } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PriceChange_IsRecorded_WithWhoAndWhen()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug, price: 100m));
        var id = created.GetProperty("id").GetGuid();
        var variantId = created.GetProperty("variants")[0].GetProperty("id").GetGuid();

        var updated = await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{id}", CatalogRequests.Product(slug, "SKU-" + slug, price: 150m, variantId: variantId));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        var history = await admin.GetFromJsonAsync<JsonElement[]>($"/api/v1/admin/catalog/products/{id}/price-history");

        var change = Assert.Single(history!);
        Assert.Equal(100m, change.GetProperty("oldAmount").GetDecimal());
        Assert.Equal(150m, change.GetProperty("newAmount").GetDecimal());
        Assert.Equal(store.Owner.UserId, change.GetProperty("changedBy").GetGuid());
    }

    [Fact]
    public async Task PriceHistory_CannotBeRewrittenByTheApplicationRole()
    {
        await using var connection = api.OpenApplicationConnection();
        await using var command = new Npgsql.NpgsqlCommand("DELETE FROM price_changes", connection);

        var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal("42501", exception.SqlState);
    }

    [Fact]
    public async Task DuplicateSlugAndSku_AreRefused()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();
        var sku = "SKU-" + slug;

        await admin.CreateProductAsync(CatalogRequests.Product(slug, sku));

        var sameSlug = await admin.PostAsJsonAsync("/api/v1/admin/catalog/products", CatalogRequests.Product(slug, "OTHER-" + slug));
        var sameSku = await admin.PostAsJsonAsync("/api/v1/admin/catalog/products", CatalogRequests.Product(CatalogRequests.NewSlug(), sku));

        Assert.Equal(ErrorCodes.Catalog.SlugTaken, await sameSlug.ReadCode());
        Assert.Equal(ErrorCodes.Catalog.SkuTaken, await sameSku.ReadCode());
    }

    [Fact]
    public async Task Publishing_WithoutAnActiveVariant_IsRefused()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(new
        {
            slug,
            translations = new[] { new { language = "tk", name = "X" } },
            variants = new[] { new { sku = "S-" + slug, price = 10m, currency = "TMT", isActive = false } },
        });

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{created.GetProperty("id").GetGuid()}/status", new { status = "published" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Catalog.NotReadyToPublish, await response.ReadCode());
    }

    [Fact]
    public async Task PlanLimit_IsEnforced()
    {
        var store = await api.CreateStoreAsync();
        await api.ExecuteAsOwnerAsync("UPDATE tenants SET max_products = 1 WHERE id = @id", ("id", store.TenantId));
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var slug = CatalogRequests.NewSlug();
        await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug));

        var second = CatalogRequests.NewSlug();
        var response = await admin.PostAsJsonAsync("/api/v1/admin/catalog/products", CatalogRequests.Product(second, "SKU-" + second));

        Assert.Equal(ErrorCodes.Catalog.ProductLimitReached, await response.ReadCode());
    }

    [Fact]
    public async Task StaleVersion_IsAConflict()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug));
        var id = created.GetProperty("id").GetGuid();
        var variantId = created.GetProperty("variants")[0].GetProperty("id").GetGuid();
        var version = created.GetProperty("version").GetUInt32();

        var fresh = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/catalog/products/{id}");
        version = fresh.GetProperty("version").GetUInt32();

        var first = await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{id}", CatalogRequests.Product(slug, "SKU-" + slug, name: "Birinji", version: version, variantId: variantId));
        var second = await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{id}", CatalogRequests.Product(slug, "SKU-" + slug, name: "Ikinji", version: version, variantId: variantId));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.Common.ConcurrencyConflict, await second.ReadCode());
    }

    [Fact]
    public async Task Permissions_AreEnforced()
    {
        var store = await api.CreateStoreAsync();
        var (_, staffToken, _) = await api.AddStaffAsync(store, Roles.Staff);
        var customer = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        var slug = CatalogRequests.NewSlug();
        var product = CatalogRequests.Product(slug, "SKU-" + slug);

        using var staff = api.CreateStoreClient(store.Slug, staffToken);
        using var shopper = api.CreateStoreClient(store.Slug, customer.AccessToken);
        using var anonymous = api.CreateStoreClient(store.Slug);

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync("/api/v1/admin/catalog/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/admin/catalog/products", product)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await shopper.GetAsync("/api/v1/admin/catalog/products")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/catalog/products")).StatusCode);
    }

    [Fact]
    public async Task UnknownFields_CannotSetTheStatusOrTenant()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var response = await admin.PostAsJsonAsync("/api/v1/admin/catalog/products", new
        {
            slug,
            status = "Published",
            tenantId = Guid.NewGuid(),
            translations = new[] { new { language = "tk", name = "X" } },
            variants = new[] { new { sku = "S-" + slug, price = 10m, currency = "TMT" } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public sealed class StorefrontTests(ApiFixture api)
{
    [Theory]
    [InlineData("tk", "Telefon")]
    [InlineData("ru", "Телефон")]
    [InlineData("en", "Telefon")]
    public async Task Names_FollowTheVisitorsLanguage_WithAFallback(string language, string expected)
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(new
        {
            slug,
            translations = new[] { new { language = "tk", name = "Telefon" }, new { language = "ru", name = "Телефон" } },
            variants = new[] { new { sku = "S-" + slug, price = 10m, currency = "TMT" } },
        });

        await admin.PublishAsync(created.GetProperty("id").GetGuid());

        using var visitor = api.CreateStoreClient(store.Slug);
        visitor.DefaultRequestHeaders.Add("Accept-Language", language);

        var detail = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{slug}");
        var listing = await visitor.GetFromJsonAsync<JsonElement>("/api/v1/catalog/products");

        Assert.Equal(expected, detail.GetProperty("name").GetString());
        Assert.Equal(expected, listing.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Listing_FiltersByCategoryBrandPriceAndSearch()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var categoryResponse = await admin.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { slug = "telefonlar", sortOrder = 0, translations = new[] { new { language = "tk", name = "Telefonlar" } } });
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var brandResponse = await admin.PostAsJsonAsync("/api/v1/admin/catalog/brands", new { name = "Samsung", slug = "samsung" });
        var brandId = (await brandResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var phone = await admin.CreateProductAsync(CatalogRequests.Product("arzan-telefon", "A1", price: 100m, name: "Arzan telefon", categoryId: categoryId, brandId: brandId));
        var laptop = await admin.CreateProductAsync(CatalogRequests.Product("gymmat-noutbuk", "B1", price: 900m, name: "Gymmat noutbuk 100%"));

        await admin.PublishAsync(phone.GetProperty("id").GetGuid());
        await admin.PublishAsync(laptop.GetProperty("id").GetGuid());

        using var visitor = api.CreateStoreClient(store.Slug);

        Assert.Equal(["arzan-telefon"], await SlugsAsync(visitor, $"categoryId={categoryId}"));
        Assert.Equal(["arzan-telefon"], await SlugsAsync(visitor, $"brandId={brandId}"));
        Assert.Equal(["gymmat-noutbuk"], await SlugsAsync(visitor, "minPrice=500"));
        Assert.Equal(["arzan-telefon"], await SlugsAsync(visitor, "maxPrice=500"));
        Assert.Equal(["arzan-telefon"], await SlugsAsync(visitor, "q=TELEFON"));
        Assert.Equal(["arzan-telefon", "gymmat-noutbuk"], await SlugsAsync(visitor, "sort=price"));
        Assert.Equal(["gymmat-noutbuk", "arzan-telefon"], await SlugsAsync(visitor, "sort=-price"));

        // A percent sign is searched for literally: it must not act as a wildcard.
        Assert.Equal(["gymmat-noutbuk"], await SlugsAsync(visitor, "q=100%25"));
        Assert.Empty(await SlugsAsync(visitor, "q=%25%25nothing"));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("cost")]
    [InlineData("id;drop table products")]
    public async Task Sorting_ByAnythingElse_IsRefused(string sort)
    {
        var store = await api.CreateStoreAsync();
        using var visitor = api.CreateStoreClient(store.Slug);

        var response = await visitor.GetAsync($"/api/v1/catalog/products?sort={Uri.EscapeDataString(sort)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.Common.InvalidSort, await response.ReadCode());
    }

    [Fact]
    public async Task PageSize_HasACeiling()
    {
        var store = await api.CreateStoreAsync();
        using var visitor = api.CreateStoreClient(store.Slug);

        var body = await visitor.GetFromJsonAsync<JsonElement>("/api/v1/catalog/products?pageSize=1000000");

        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
    }

    [Fact]
    public async Task CategoryTree_IsNested_AndHidesInactive()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var parent = await (await admin.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { slug = "elektronika", sortOrder = 0, translations = new[] { new { language = "tk", name = "Elektronika" } } })).Content.ReadFromJsonAsync<JsonElement>();
        var parentId = parent.GetProperty("id").GetGuid();

        await admin.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { parentId, slug = "telefonlar", sortOrder = 0, translations = new[] { new { language = "tk", name = "Telefonlar" } } });
        await admin.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { parentId, slug = "gizlin", sortOrder = 1, isActive = false, translations = new[] { new { language = "tk", name = "Gizlin" } } });

        using var visitor = api.CreateStoreClient(store.Slug);
        var tree = await visitor.GetFromJsonAsync<JsonElement[]>("/api/v1/catalog/categories");

        var root = Assert.Single(tree!);
        Assert.Equal("Elektronika", root.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Cache_ShowsChangesAtOnce()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var visitor = api.CreateStoreClient(store.Slug);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug, name: "Köne at"));
        var id = created.GetProperty("id").GetGuid();
        var variantId = created.GetProperty("variants")[0].GetProperty("id").GetGuid();
        await admin.PublishAsync(id);

        Assert.Equal("Köne at", (await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{slug}")).GetProperty("name").GetString());

        await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{id}", CatalogRequests.Product(slug, "SKU-" + slug, name: "Täze at", variantId: variantId));

        Assert.Equal("Täze at", (await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{slug}")).GetProperty("name").GetString());
    }

    [Fact]
    public async Task StorefrontOutsideAStore_IsRefused()
    {
        using var client = api.CreatePlatformClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/catalog/products")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/catalog/categories")).StatusCode);
    }

    private static async Task<List<string?>> SlugsAsync(HttpClient client, string query)
    {
        var body = await client.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products?{query}");

        return body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("slug").GetString()).ToList();
    }
}

[Collection(ApiCollection.Name)]
public sealed class TaxonomyTests(ApiFixture api)
{
    [Fact]
    public async Task CategoryCannotBecomeItsOwnDescendant()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var parent = await CreateCategoryAsync(admin, "ene", null);
        var child = await CreateCategoryAsync(admin, "cagа", parent);

        var cycle = await admin.PutAsJsonAsync($"/api/v1/admin/catalog/categories/{parent}", new { parentId = child, slug = "ene", sortOrder = 0, translations = new[] { new { language = "tk", name = "X" } } });
        var self = await admin.PutAsJsonAsync($"/api/v1/admin/catalog/categories/{parent}", new { parentId = parent, slug = "ene", sortOrder = 0, translations = new[] { new { language = "tk", name = "X" } } });

        Assert.Equal(ErrorCodes.Catalog.CategoryCycle, await cycle.ReadCode());
        Assert.Equal(ErrorCodes.Catalog.CategoryCycle, await self.ReadCode());
    }

    [Fact]
    public async Task CategoryWithChildren_CannotBeDeleted()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var parent = await CreateCategoryAsync(admin, "ene-kategoriya", null);
        await CreateCategoryAsync(admin, "cagaly", parent);

        var response = await admin.DeleteAsync($"/api/v1/admin/catalog/categories/{parent}");

        Assert.Equal(ErrorCodes.Catalog.CategoryHasChildren, await response.ReadCode());
    }

    [Fact]
    public async Task AttributeRules_AreChecked_OnProducts()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var categoryId = await CreateCategoryAsync(admin, "telefon", null);

        var memory = await (await admin.PostAsJsonAsync("/api/v1/admin/catalog/attributes", new { code = "memory", dataType = "number", translations = new[] { new { language = "tk", name = "Ýat" } } })).Content.ReadFromJsonAsync<JsonElement>();
        var color = await (await admin.PostAsJsonAsync("/api/v1/admin/catalog/attributes", new { code = "color", dataType = "select", options = new[] { "gara", "ak" }, translations = new[] { new { language = "tk", name = "Reňk" } } })).Content.ReadFromJsonAsync<JsonElement>();
        var memoryId = memory.GetProperty("id").GetGuid();
        var colorId = color.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/api/v1/admin/catalog/categories/{categoryId}/attributes", new { attributeId = memoryId, isRequired = true })).StatusCode);

        async Task<HttpResponseMessage> SaveAsync(object attributes)
        {
            var slug = CatalogRequests.NewSlug();

            return await admin.PostAsJsonAsync("/api/v1/admin/catalog/products", CatalogRequests.Product(slug, "SKU-" + slug, categoryId: categoryId, attributes: attributes));
        }

        Assert.Equal(ErrorCodes.Catalog.RequiredAttributeMissing, await (await SaveAsync(Array.Empty<object>())).ReadCode());
        Assert.Equal(ErrorCodes.Catalog.InvalidAttributeValue, await (await SaveAsync(new[] { new { attributeId = memoryId, value = "kop" } })).ReadCode());
        Assert.Equal(ErrorCodes.Catalog.InvalidAttributeValue, await (await SaveAsync(new[] { new { attributeId = memoryId, value = "128" }, new { attributeId = colorId, value = "gyzyl" } })).ReadCode());
        Assert.Equal(ErrorCodes.Catalog.AttributeNotFound, await (await SaveAsync(new[] { new { attributeId = Guid.NewGuid(), value = "1" } })).ReadCode());

        var valid = await SaveAsync(new[] { new { attributeId = memoryId, value = "128" }, new { attributeId = colorId, value = "gara" } });

        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
    }

    private static async Task<Guid> CreateCategoryAsync(HttpClient admin, string slug, Guid? parentId)
    {
        var response = await admin.PostAsJsonAsync("/api/v1/admin/catalog/categories", new
        {
            parentId,
            slug = slug.Replace("а", "a", StringComparison.Ordinal),
            sortOrder = 0,
            translations = new[] { new { language = "tk", name = slug } },
        });

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}

[Collection(ApiCollection.Name)]
public sealed class CatalogIsolationTests(ApiFixture api)
{
    [Fact]
    public async Task AnotherStoresCatalog_LooksLikeItDoesNotExist()
    {
        var victim = await api.CreateStoreAsync();
        var attacker = await api.CreateStoreAsync();
        using var victimAdmin = api.CreateStoreClient(victim.Slug, victim.AccessToken);
        using var attackerAdmin = api.CreateStoreClient(attacker.Slug, attacker.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var product = await victimAdmin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug));
        var productId = product.GetProperty("id").GetGuid();
        await victimAdmin.PublishAsync(productId);

        var category = await (await victimAdmin.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { slug = "gizlin-kategoriya", sortOrder = 0, translations = new[] { new { language = "tk", name = "X" } } })).Content.ReadFromJsonAsync<JsonElement>();
        var categoryId = category.GetProperty("id").GetGuid();
        var brand = await (await victimAdmin.PostAsJsonAsync("/api/v1/admin/catalog/brands", new { name = "X", slug = "gizlin-marka" })).Content.ReadFromJsonAsync<JsonElement>();
        var brandId = brand.GetProperty("id").GetGuid();

        var attackSlug = CatalogRequests.NewSlug();

        Assert.Equal(HttpStatusCode.NotFound, (await attackerAdmin.GetAsync($"/api/v1/admin/catalog/products/{productId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attackerAdmin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{productId}", CatalogRequests.Product(attackSlug, "S-" + attackSlug))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attackerAdmin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{productId}/status", new { status = "archived" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attackerAdmin.DeleteAsync($"/api/v1/admin/catalog/products/{productId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attackerAdmin.GetAsync($"/api/v1/admin/catalog/products/{productId}/price-history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attackerAdmin.DeleteAsync($"/api/v1/admin/catalog/categories/{categoryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attackerAdmin.DeleteAsync($"/api/v1/admin/catalog/brands/{brandId}")).StatusCode);

        // Using the other store's category or brand on one's own product must fail too.
        var withForeignCategory = await attackerAdmin.PostAsJsonAsync("/api/v1/admin/catalog/products", CatalogRequests.Product(attackSlug, "S-" + attackSlug, categoryId: categoryId));
        var withForeignBrand = await attackerAdmin.PostAsJsonAsync("/api/v1/admin/catalog/products", CatalogRequests.Product(attackSlug, "S-" + attackSlug, brandId: brandId));

        Assert.Equal(HttpStatusCode.NotFound, withForeignCategory.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, withForeignBrand.StatusCode);

        var list = await (await attackerAdmin.GetAsync("/api/v1/admin/catalog/products")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(slug, list, StringComparison.Ordinal);

        using var attackerStore = api.CreateStoreClient(attacker.Slug);
        Assert.Equal(HttpStatusCode.NotFound, (await attackerStore.GetAsync($"/api/v1/catalog/products/{slug}")).StatusCode);
        Assert.DoesNotContain(slug, await (await attackerStore.GetAsync("/api/v1/catalog/products")).Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var victimStore = api.CreateStoreClient(victim.Slug);
        Assert.Equal(HttpStatusCode.OK, (await victimStore.GetAsync($"/api/v1/catalog/products/{slug}")).StatusCode);
    }

    [Fact]
    public async Task TwoStores_CanUseTheSameSlugAndSku_AndCacheNeverMixesThem()
    {
        var first = await api.CreateStoreAsync();
        var second = await api.CreateStoreAsync();
        using var firstAdmin = api.CreateStoreClient(first.Slug, first.AccessToken);
        using var secondAdmin = api.CreateStoreClient(second.Slug, second.AccessToken);

        const string Slug = "umumy-haryt";
        const string Sku = "UMUMY-1";

        var a = await firstAdmin.CreateProductAsync(CatalogRequests.Product(Slug, Sku, name: "Birinji dükan"));
        var b = await secondAdmin.CreateProductAsync(CatalogRequests.Product(Slug, Sku, name: "Ikinji dükan"));
        await firstAdmin.PublishAsync(a.GetProperty("id").GetGuid());
        await secondAdmin.PublishAsync(b.GetProperty("id").GetGuid());

        using var firstStore = api.CreateStoreClient(first.Slug);
        using var secondStore = api.CreateStoreClient(second.Slug);

        for (var round = 0; round < 2; round++)
        {
            Assert.Equal("Birinji dükan", (await firstStore.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{Slug}")).GetProperty("name").GetString());
            Assert.Equal("Ikinji dükan", (await secondStore.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{Slug}")).GetProperty("name").GetString());
        }
    }
}
