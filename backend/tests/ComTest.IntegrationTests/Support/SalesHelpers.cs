using System.Net.Http.Json;
using System.Text.Json;
using ComTest.IntegrationTests.Catalog;

namespace ComTest.IntegrationTests.Support;

/// <summary>A store that can actually sell: a warehouse, delivery options and products with stock.</summary>
public sealed record SellingStore(TestStore Store, Guid WarehouseId, Guid CourierId, Guid PickupId)
{
    public string Slug => Store.Slug;

    public string AccessToken => Store.AccessToken;
}

public sealed record SellableProduct(Guid ProductId, Guid VariantId, string Slug);

public static class SalesHelpers
{
    public const string Region = "ashgabat";
    public const decimal CourierPrice = 20m;
    public const decimal FreeShippingOver = 1000m;

    public static async Task<SellingStore> CreateSellingStoreAsync(this ApiFixture api)
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var warehouse = await ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/inventory/warehouses", new { name = "Esasy", code = "MAIN" }));
        var zone = await ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/shipping/zones", new { name = "Şäher", regions = new[] { Region } }));

        var courier = await ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/shipping/methods", new
        {
            type = "courier",
            names = new Dictionary<string, string> { ["tk"] = "Kurýer", ["ru"] = "Курьер" },
            price = CourierPrice,
            currency = "TMT",
            zoneId = zone.GetProperty("id").GetGuid(),
            freeOverAmount = FreeShippingOver,
        }));

        var pickup = await ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/shipping/methods", new
        {
            type = "pickup",
            names = new Dictionary<string, string> { ["tk"] = "Dükandan almak" },
            price = 0m,
            currency = "TMT",
            pickupAddress = "Aşgabat, Köçe 1",
        }));

        return new SellingStore(store, warehouse.GetProperty("id").GetGuid(), courier.GetProperty("id").GetGuid(), pickup.GetProperty("id").GetGuid());
    }

    public static async Task<SellableProduct> CreateSellableProductAsync(this ApiFixture api, SellingStore store, decimal price = 100m, int stock = 10, string name = "Telefon")
    {
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug, price: price, name: name));
        var productId = created.GetProperty("id").GetGuid();
        var variantId = created.GetProperty("variants")[0].GetProperty("id").GetGuid();

        await admin.PublishAsync(productId);

        if (stock > 0)
        {
            var adjusted = await admin.PostAsJsonAsync("/api/v1/admin/inventory/stock/adjustments", new
            {
                warehouseId = store.WarehouseId,
                variantId,
                delta = stock,
                type = "receipt",
                reason = "test",
            });

            Assert.True(adjusted.IsSuccessStatusCode, await adjusted.Content.ReadAsStringAsync());
        }

        return new SellableProduct(productId, variantId, slug);
    }

    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static object Address(string region = Region, string phone = "+99365111111") => new
    {
        recipient = "Aýna Berdiýewa",
        phone,
        region,
        line1 = "Köçe 1, jaý 2",
    };

    public static async Task<(int OnHand, int Reserved)> StockOfAsync(this ApiFixture api, Guid variantId)
    {
        var onHand = await api.QueryAsOwnerAsync<int>("SELECT coalesce(sum(on_hand), 0)::int FROM stock_items WHERE product_variant_id = @id", ("id", variantId));
        var reserved = await api.QueryAsOwnerAsync<int>("SELECT coalesce(sum(reserved), 0)::int FROM stock_items WHERE product_variant_id = @id", ("id", variantId));

        return (onHand, reserved);
    }
}

/// <summary>A shopper's browser: remembers the guest cart cookie and sends what real clients send.</summary>
public sealed class Shopper : IDisposable
{
    private const string CookieName = "__Secure-comtest-cart";

    private readonly HttpClient _client;

    public Shopper(ApiFixture api, string slug, string? accessToken = null)
    {
        _client = api.CreateStoreClient(slug, accessToken);
        _client.DefaultRequestHeaders.Add("X-Requested-With", "ComTest");
    }

    public string? GuestCookie { get; set; }

    public string? LastSetCookie { get; private set; }

    public Task<HttpResponseMessage> AddAsync(Guid variantId, int quantity = 1) =>
        SendAsync(HttpMethod.Post, "/api/v1/cart/items", new { variantId, quantity });

    public Task<HttpResponseMessage> GetCartAsync() => SendAsync(HttpMethod.Get, "/api/v1/cart");

    public Task<HttpResponseMessage> QuoteAsync(string? region = null, Guid? shippingMethodId = null) =>
        SendAsync(HttpMethod.Post, "/api/v1/checkout/quote", new { region, shippingMethodId });

    public Task<HttpResponseMessage> CheckoutAsync(object request, string? key = null) =>
        SendAsync(HttpMethod.Post, "/api/v1/checkout", request, key ?? Guid.NewGuid().ToString("N"));

    /// <summary>A guest order to the given store, sent to the default address by courier.</summary>
    public Task<HttpResponseMessage> PlaceCourierOrderAsync(SellingStore store, string phone = "+99365111111", string? key = null, decimal? expectedTotal = null) =>
        CheckoutAsync(
            new
            {
                shippingMethodId = store.CourierId,
                paymentMethod = "cashOnDelivery",
                contact = new { name = "Aýna", phone },
                address = SalesHelpers.Address(phone: phone),
                expectedTotal,
            },
            key);

    public async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(method, path);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        if (GuestCookie is not null)
        {
            request.Headers.Add("Cookie", $"{CookieName}={GuestCookie}");
        }

        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        var response = await _client.SendAsync(request);

        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            var cookie = cookies.FirstOrDefault(value => value.StartsWith(CookieName + "=", StringComparison.Ordinal));

            if (cookie is not null)
            {
                LastSetCookie = cookie;

                var value = cookie[(CookieName.Length + 1)..].Split(';')[0];

                GuestCookie = value.Length == 0 ? null : value;
            }
        }

        return response;
    }

    public void Dispose() => _client.Dispose();
}
