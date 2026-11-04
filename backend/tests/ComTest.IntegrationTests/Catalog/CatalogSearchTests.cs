using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;

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

    private static async Task<Guid> CreateAttributeAsync(HttpClient owner, string code, string dataType, string[] options, string name)
    {
        var attribute = await ReadAsync(await owner.PostAsJsonAsync(
            "/api/v1/admin/catalog/attributes",
            new { code, dataType, options, translations = new[] { new { language = "tk", name } } }));

        return attribute.GetProperty("id").GetGuid();
    }

    private static async Task<string> PublishWithAttributesAsync(HttpClient owner, Guid? categoryId, params (Guid Id, string Value)[] attributes)
    {
        var slug = CatalogRequests.NewSlug();
        var product = await owner.CreateProductAsync(CatalogRequests.Product(
            slug,
            "S-" + slug,
            categoryId: categoryId,
            attributes: attributes.Select(item => new { attributeId = item.Id, value = item.Value }).ToArray()));

        await owner.PublishAsync(product.GetProperty("id").GetGuid());

        return slug;
    }

    private static async Task<List<string>> ListedSlugsAsync(HttpClient client, string query)
    {
        var page = await ReadAsync(await client.GetAsync(Path("/api/v1/catalog/products?" + query)));

        return page.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("slug").GetString()!).ToList();
    }

    [Fact]
    public async Task AttributeFilter_WidensWithinAnAttributeAndNarrowsAcrossThem()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var visitor = api.CreateStoreClient(store.Slug);

        var color = await CreateAttributeAsync(owner, "color", "select", ["gara", "ak", "gök"], "Reňk");
        var usb = await CreateAttributeAsync(owner, "usb", "boolean", [], "USB");

        var black = await PublishWithAttributesAsync(owner, null, (color, "gara"), (usb, "true"));
        var white = await PublishWithAttributesAsync(owner, null, (color, "ak"), (usb, "true"));
        var blue = await PublishWithAttributesAsync(owner, null, (color, "gök"), (usb, "false"));

        Assert.Equal([black], await ListedSlugsAsync(visitor, "attr=color:gara"));
        Assert.Equal(new[] { black, white }.Order(StringComparer.Ordinal), (await ListedSlugsAsync(visitor, "attr=color:gara&attr=color:ak")).Order(StringComparer.Ordinal));
        Assert.Equal([white], await ListedSlugsAsync(visitor, "attr=color:ak&attr=usb:true"));
        Assert.Empty(await ListedSlugsAsync(visitor, "attr=color:gök&attr=usb:true"));
        Assert.Equal([blue], await ListedSlugsAsync(visitor, "attr=color:" + Uri.EscapeDataString("gök")));

        // A value can hold the separator, and an attribute nobody defined simply matches nothing.
        Assert.Empty(await ListedSlugsAsync(visitor, "attr=color:a:b"));
        Assert.Empty(await ListedSlugsAsync(visitor, "attr=nosuch:x"));
    }

    [Theory]
    [InlineData("attr=color")]
    [InlineData("attr=:gara")]
    [InlineData("attr=color:")]
    public async Task AttributeFilter_RefusesAMalformedPair(string query)
    {
        var store = await api.CreateStoreAsync();
        using var visitor = api.CreateStoreClient(store.Slug);

        var response = await visitor.GetAsync(Path("/api/v1/catalog/products?" + query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.Catalog.InvalidAttributeFilter, await response.ReadCode());
    }

    [Fact]
    public async Task AttributeFilter_RefusesMorePairsThanItAllows()
    {
        var store = await api.CreateStoreAsync();
        using var visitor = api.CreateStoreClient(store.Slug);

        var query = string.Join('&', Enumerable.Range(0, 31).Select(index => $"attr=a:{index}"));
        var response = await visitor.GetAsync(Path("/api/v1/catalog/products?" + query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AttributeFilters_ListOnlyListedTypesAndCountsOfProductsOnSale()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var visitor = api.CreateStoreClient(store.Slug);

        var category = await ReadAsync(await owner.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { slug = "telefonlar", sortOrder = 0, translations = new[] { new { language = "tk", name = "Telefonlar" } } }));
        var categoryId = category.GetProperty("id").GetGuid();

        var size = await CreateAttributeAsync(owner, "size", "select", ["S", "M", "L"], "Ölçeg");
        var note = await CreateAttributeAsync(owner, "note", "text", [], "Bellik");

        await PublishWithAttributesAsync(owner, categoryId, (size, "L"), (note, "erkin tekst"));
        await PublishWithAttributesAsync(owner, categoryId, (size, "S"));
        await PublishWithAttributesAsync(owner, categoryId, (size, "S"));
        await PublishWithAttributesAsync(owner, null, (size, "M"));

        // A draft is not on sale, so it does not count.
        await owner.CreateProductAsync(CatalogRequests.Product(
            "garalama-" + CatalogRequests.NewSlug()[..8],
            "D-" + CatalogRequests.NewSlug(),
            categoryId: categoryId,
            attributes: new[] { new { attributeId = size, value = "L" } }));

        var all = await ReadAsync(await visitor.GetAsync(Path("/api/v1/catalog/attribute-filters")));
        var filter = Assert.Single(all.EnumerateArray());

        Assert.Equal("size", filter.GetProperty("code").GetString());
        Assert.Equal("Ölçeg", filter.GetProperty("name").GetString());
        Assert.Equal("select", filter.GetProperty("dataType").GetString());
        Assert.Equal(["S", "M", "L"], filter.GetProperty("values").EnumerateArray().Select(item => item.GetProperty("value").GetString()));

        var shelf = await ReadAsync(await visitor.GetAsync(Path($"/api/v1/catalog/attribute-filters?categoryId={categoryId}")));
        var values = Assert.Single(shelf.EnumerateArray()).GetProperty("values").EnumerateArray()
            .ToDictionary(item => item.GetProperty("value").GetString()!, item => item.GetProperty("count").GetInt32());

        Assert.Equal(new Dictionary<string, int> { ["S"] = 2, ["L"] = 1 }, values);
    }

    [Fact]
    public async Task AttributeFilters_NeverShowAnotherStore()
    {
        var store = await api.CreateStoreAsync();
        var other = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var otherOwner = api.CreateStoreClient(other.Slug, other.AccessToken);
        using var otherVisitor = api.CreateStoreClient(other.Slug);

        var color = await CreateAttributeAsync(owner, "color", "select", ["gara"], "Reňk");
        var slug = await PublishWithAttributesAsync(owner, null, (color, "gara"));

        var filters = await ReadAsync(await otherVisitor.GetAsync(Path("/api/v1/catalog/attribute-filters")));

        Assert.Empty(filters.EnumerateArray());
        Assert.DoesNotContain(slug, await ListedSlugsAsync(otherVisitor, "attr=color:gara"));
    }
}
