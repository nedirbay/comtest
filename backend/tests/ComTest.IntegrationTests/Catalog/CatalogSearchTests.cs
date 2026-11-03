using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.IntegrationTests.Support;

namespace ComTest.IntegrationTests.Catalog;

[Collection(ApiCollection.Name)]
public sealed class CatalogSearchTests(ApiFixture api)
{
    private static Uri Path(string path) => new(path, UriKind.Relative);

    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> CreateBrandAsync(HttpClient owner, string name)
    {
        var brand = await ReadAsync(await owner.PostAsJsonAsync("/api/v1/admin/catalog/brands", new { name, slug = CatalogRequests.NewSlug(), logoUrl = (string?)null }));

        return brand.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task StoreBrands_ListOnlyBrandsWithSomethingToBuy()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var visitor = api.CreateStoreClient(store.Slug);

        var sold = await CreateBrandAsync(owner, "Satylýan");
        var draftOnly = await CreateBrandAsync(owner, "Garalama");
        await CreateBrandAsync(owner, "Harytsyz");

        var slug = CatalogRequests.NewSlug();
        var product = await owner.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug, brandId: sold));

        await owner.PublishAsync(product.GetProperty("id").GetGuid());

        var draftSlug = CatalogRequests.NewSlug();

        await owner.CreateProductAsync(CatalogRequests.Product(draftSlug, "SKU-" + draftSlug, brandId: draftOnly));

        var brands = await ReadAsync(await visitor.GetAsync(Path("/api/v1/catalog/brands")));
        var only = Assert.Single(brands.EnumerateArray());

        Assert.Equal(sold, only.GetProperty("id").GetGuid());
        Assert.Equal("Satylýan", only.GetProperty("name").GetString());
        Assert.False(only.TryGetProperty("version", out _));
    }

    [Fact]
    public async Task StoreBrands_NeverShowAnotherStore()
    {
        var first = await api.CreateStoreAsync();
        var second = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(first.Slug, first.AccessToken);
        using var visitor = api.CreateStoreClient(second.Slug);

        var brand = await CreateBrandAsync(owner, "Birinji");
        var slug = CatalogRequests.NewSlug();
        var product = await owner.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug, brandId: brand));

        await owner.PublishAsync(product.GetProperty("id").GetGuid());

        var brands = await ReadAsync(await visitor.GetAsync(Path("/api/v1/catalog/brands")));

        Assert.Empty(brands.EnumerateArray());
    }

    private async Task<(HttpClient Owner, string Slug, string Sku)> ProductNamedAsync(string name)
    {
        var store = await api.CreateStoreAsync();
        var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();
        var sku = "SKU-" + slug;

        await owner.CreateProductAsync(CatalogRequests.Product(slug, sku, name: name));
        await owner.CreateProductAsync(CatalogRequests.Product(CatalogRequests.NewSlug(), "OTHER-" + Guid.NewGuid().ToString("N")[..8], name: "Başga zat"));

        return (owner, slug, sku);
    }

    [Theory]
    [InlineData("keteni")]
    [InlineData("KETENI")]
    public async Task AdminProducts_AreFoundByNameInAnyCase(string term)
    {
        var (owner, slug, _) = await ProductNamedAsync("Keteni köýnek");

        using (owner)
        {
            var found = await ReadAsync(await owner.GetAsync(Path($"/api/v1/admin/catalog/products?page=1&pageSize=10&q={term}")));
            var only = Assert.Single(found.GetProperty("items").EnumerateArray());

            Assert.Equal(slug, only.GetProperty("slug").GetString());
        }
    }

    [Fact]
    public async Task AdminProducts_AreFoundBySkuAndSlug_IncludingDrafts()
    {
        var (owner, slug, sku) = await ProductNamedAsync("Garalama haryt");

        using (owner)
        {
            var bySku = await ReadAsync(await owner.GetAsync(Path($"/api/v1/admin/catalog/products?page=1&pageSize=10&q={sku}")));
            var bySlug = await ReadAsync(await owner.GetAsync(Path($"/api/v1/admin/catalog/products?page=1&pageSize=10&q={slug}")));

            Assert.Equal(1, bySku.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, bySlug.GetProperty("totalCount").GetInt32());
        }
    }

    [Fact]
    public async Task AdminProducts_TreatWildcardsAsPlainText()
    {
        var (owner, _, _) = await ProductNamedAsync("Keteni köýnek");

        using (owner)
        {
            var found = await ReadAsync(await owner.GetAsync(Path("/api/v1/admin/catalog/products?page=1&pageSize=10&q=%25")));

            Assert.Equal(0, found.GetProperty("totalCount").GetInt32());
        }
    }

    [Fact]
    public async Task AdminProducts_ShowTheNameInTheViewersLanguage()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        await owner.CreateProductAsync(new
        {
            slug,
            translations = new[]
            {
                new { language = "en", name = "Shirt" },
                new { language = "ru", name = "Рубашка" },
                new { language = "tk", name = "Köýnek" },
            },
            variants = new[] { new { id = (Guid?)null, sku = "SKU-" + slug, price = 10m, currency = "TMT" } },
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, Path("/api/v1/admin/catalog/products?page=1&pageSize=10"));

        request.Headers.AcceptLanguage.ParseAdd("ru");

        var list = await ReadAsync(await owner.SendAsync(request));

        Assert.Equal("Рубашка", list.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task EveryStatusInAnAnswer_IsSpelledLikeTheRequests()
    {
        var store = await api.CreateSellingStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();
        var product = await owner.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug));

        Assert.Equal("draft", product.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/api/v1/admin/catalog/products/{product.GetProperty("id").GetGuid()}/status", new { status = "published" })).StatusCode);

        var settings = await ReadAsync(await owner.GetAsync(Path("/api/v1/admin/store")));

        Assert.Equal("free", settings.GetProperty("plan").GetString());

        var methods = await ReadAsync(await owner.GetAsync(Path("/api/v1/admin/shipping/methods")));

        Assert.All(methods.EnumerateArray(), method =>
            Assert.Contains(method.GetProperty("type").GetString(), new[] { "courier", "pickup" }));
    }
}
