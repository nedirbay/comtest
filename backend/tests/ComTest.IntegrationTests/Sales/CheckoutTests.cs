using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Api.Hosting;
using ComTest.IntegrationTests.Catalog;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ComTest.IntegrationTests.Sales;

[Collection(ApiCollection.Name)]
public sealed class CheckoutQuoteTests(ApiFixture api)
{
    [Fact]
    public async Task Quote_OffersWhatTheRegionCanHave_AndAddsItUp()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId, 2);

        var served = await SalesHelpers.ReadAsync(await shopper.QuoteAsync(SalesHelpers.Region, store.CourierId));
        var unserved = await SalesHelpers.ReadAsync(await shopper.QuoteAsync("mary"));
        var noRegion = await SalesHelpers.ReadAsync(await shopper.QuoteAsync());

        Assert.Equal(2, served.GetProperty("shippingOptions").GetArrayLength());
        Assert.Equal(200m, served.GetProperty("totals").GetProperty("subtotal").GetDecimal());
        Assert.Equal(SalesHelpers.CourierPrice, served.GetProperty("totals").GetProperty("shipping").GetDecimal());
        Assert.Equal(220m, served.GetProperty("totals").GetProperty("total").GetDecimal());
        Assert.Equal("Pickup", Assert.Single(unserved.GetProperty("shippingOptions").EnumerateArray()).GetProperty("type").GetString());
        Assert.Single(noRegion.GetProperty("shippingOptions").EnumerateArray());
        Assert.Equal("cashOnDelivery", Assert.Single(served.GetProperty("paymentOptions").EnumerateArray()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delivery_BecomesFree_AboveTheThreshold()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 600m);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId, 2);

        var quote = await SalesHelpers.ReadAsync(await shopper.QuoteAsync(SalesHelpers.Region, store.CourierId));

        Assert.Equal(0m, quote.GetProperty("totals").GetProperty("shipping").GetDecimal());
    }

    [Fact]
    public async Task AMethodThatCannotServeTheRegion_IsRefused()
    {
        var store = await api.CreateSellingStoreAsync();
        using var shopper = new Shopper(api, store.Slug);

        var response = await shopper.QuoteAsync("mary", store.CourierId);

        Assert.Equal(ErrorCodes.Shipping.MethodUnavailable, await response.ReadCode());
    }
}

[Collection(ApiCollection.Name)]
public sealed class PlaceOrderTests(ApiFixture api)
{
    [Fact]
    public async Task GuestOrder_IsPriced_ByTheServer_Reserves_AndEmptiesTheCart()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m, stock: 10);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId, 3);

        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));
        var order = placed.GetProperty("order");
        var totals = order.GetProperty("totals");

        Assert.Equal("Pending", order.GetProperty("status").GetString());
        Assert.Equal("Unpaid", order.GetProperty("paymentState").GetString());
        Assert.Equal(300m, totals.GetProperty("subtotal").GetDecimal());
        Assert.Equal(SalesHelpers.CourierPrice, totals.GetProperty("shipping").GetDecimal());
        Assert.Equal(320m, totals.GetProperty("total").GetDecimal());
        Assert.False(string.IsNullOrEmpty(placed.GetProperty("trackingToken").GetString()));

        Assert.Equal((10, 3), await api.StockOfAsync(product.VariantId));
        Assert.Equal(0, (await SalesHelpers.ReadAsync(await shopper.GetCartAsync())).GetProperty("itemCount").GetInt32());
    }

    [Theory]
    [InlineData("total", 0.01)]
    [InlineData("unitPrice", 0.01)]
    [InlineData("discount", 1000)]
    public async Task AClientCannotSendItsOwnAmounts(string field, double value)
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        var body = new Dictionary<string, object?>
        {
            ["shippingMethodId"] = store.PickupId,
            ["paymentMethod"] = "cashOnDelivery",
            ["contact"] = new { name = "A", phone = "+99365111111" },
            [field] = value,
        };

        Assert.Equal(HttpStatusCode.BadRequest, (await shopper.CheckoutAsync(body)).StatusCode);
    }

    [Fact]
    public async Task SendingTheSameRequestTwice_MakesOneOrder()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 10);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId, 2);

        var key = Guid.NewGuid().ToString("N");

        var first = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store, key: key));
        var second = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store, key: key));

        Assert.Equal(first.GetProperty("order").GetProperty("id").GetGuid(), second.GetProperty("order").GetProperty("id").GetGuid());
        Assert.Equal((10, 2), await api.StockOfAsync(product.VariantId));
        Assert.Equal(1L, await CountOrdersAsync(store));
    }

    [Fact]
    public async Task DoubleClicks_AtTheSameMoment_MakeOneOrder()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 10);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId, 2);

        var key = Guid.NewGuid().ToString("N");

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => shopper.PlaceCourierOrderAsync(store, key: key)));

        Assert.All(responses, response => Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Conflict, response.StatusCode.ToString()));
        Assert.Contains(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal(1L, await CountOrdersAsync(store));
        Assert.Equal((10, 2), await api.StockOfAsync(product.VariantId));
    }

    [Fact]
    public async Task OneShoppersKey_CannotReachAnotherShoppersOrder()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 10);
        var key = Guid.NewGuid().ToString("N");

        using var first = new Shopper(api, store.Slug);
        using var second = new Shopper(api, store.Slug);
        await first.AddAsync(product.VariantId);
        await second.AddAsync(product.VariantId);

        var one = await SalesHelpers.ReadAsync(await first.PlaceCourierOrderAsync(store, key: key));
        var two = await SalesHelpers.ReadAsync(await second.PlaceCourierOrderAsync(store, key: key));

        Assert.NotEqual(one.GetProperty("order").GetProperty("id").GetGuid(), two.GetProperty("order").GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short")]
    [InlineData("has spaces in it")]
    public async Task IdempotencyKey_IsRequired(string? key)
    {
        var store = await api.CreateSellingStoreAsync();
        using var shopper = new Shopper(api, store.Slug);

        var response = await shopper.SendAsync(
            HttpMethod.Post,
            "/api/v1/checkout",
            new { shippingMethodId = store.PickupId, paymentMethod = "cashOnDelivery" },
            key);

        Assert.Equal(ErrorCodes.Checkout.IdempotencyKeyRequired, await response.ReadCode());
    }

    [Fact]
    public async Task ExpectedTotal_StopsTheOrderWhenThePriceHasMoved()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        await admin.PutAsJsonAsync(
            $"/api/v1/admin/catalog/products/{product.ProductId}",
            CatalogRequests.Product(product.Slug, "SKU-" + product.Slug, price: 150m, variantId: product.VariantId));

        var response = await shopper.PlaceCourierOrderAsync(store, expectedTotal: 120m);

        Assert.Equal(ErrorCodes.Checkout.TotalChanged, await response.ReadCode());
        Assert.Equal(0L, await CountOrdersAsync(store));
        Assert.Equal(1, (await SalesHelpers.ReadAsync(await shopper.GetCartAsync())).GetProperty("itemCount").GetInt32());

        var accepted = await shopper.PlaceCourierOrderAsync(store, expectedTotal: 170m);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task OutOfStock_LeavesNothingBehind_EvenWhenAnotherLineFitted()
    {
        var store = await api.CreateSellingStoreAsync();
        var plenty = await api.CreateSellableProductAsync(store, stock: 10);
        var scarce = await api.CreateSellableProductAsync(store, stock: 1);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(plenty.VariantId, 2);
        await shopper.AddAsync(scarce.VariantId, 2);

        var response = await shopper.PlaceCourierOrderAsync(store);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Inventory.InsufficientStock, await response.ReadCode());
        Assert.Equal(0L, await CountOrdersAsync(store));
        Assert.Equal(0L, await api.QueryAsOwnerAsync<long>("SELECT count(*) FROM payments WHERE tenant_id = @id", ("id", store.Store.TenantId)));
        Assert.Equal(0L, await api.QueryAsOwnerAsync<long>("SELECT count(*) FROM stock_reservations WHERE tenant_id = @id", ("id", store.Store.TenantId)));
        Assert.Equal((10, 0), await api.StockOfAsync(plenty.VariantId));
        Assert.Equal((1, 0), await api.StockOfAsync(scarce.VariantId));
        Assert.Equal(4, (await SalesHelpers.ReadAsync(await shopper.GetCartAsync())).GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task EmptyCart_CannotBeOrdered()
    {
        var store = await api.CreateSellingStoreAsync();
        using var shopper = new Shopper(api, store.Slug);

        Assert.Equal(ErrorCodes.Cart.Empty, await (await shopper.PlaceCourierOrderAsync(store)).ReadCode());
    }

    [Fact]
    public async Task AProductWithdrawnAfterAdding_CannotBeOrdered()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{product.ProductId}/status", new { status = "archived" });

        Assert.Equal(ErrorCodes.Cart.ItemUnavailable, await (await shopper.PlaceCourierOrderAsync(store)).ReadCode());
    }

    [Fact]
    public async Task Courier_NeedsAnAddress_InAServedRegion()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        var noAddress = await shopper.CheckoutAsync(new { shippingMethodId = store.CourierId, paymentMethod = "cashOnDelivery", contact = new { name = "A", phone = "+99365111111" } });
        var wrongRegion = await shopper.CheckoutAsync(new { shippingMethodId = store.CourierId, paymentMethod = "cashOnDelivery", address = SalesHelpers.Address(region: "mary") });

        Assert.Equal(HttpStatusCode.BadRequest, noAddress.StatusCode);
        Assert.Equal(ErrorCodes.Shipping.MethodUnavailable, await wrongRegion.ReadCode());
    }

    [Fact]
    public async Task Pickup_NeedsNoAddress_ButNeedsAContact()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        var noContact = await shopper.CheckoutAsync(new { shippingMethodId = store.PickupId, paymentMethod = "cashOnDelivery" });
        Assert.Equal(ErrorCodes.Checkout.ContactRequired, await noContact.ReadCode());

        var placed = await SalesHelpers.ReadAsync(await shopper.CheckoutAsync(new { shippingMethodId = store.PickupId, paymentMethod = "cashOnDelivery", contact = new { name = "Aýna", phone = "+99365111111" } }));

        Assert.Equal(0m, placed.GetProperty("order").GetProperty("totals").GetProperty("shipping").GetDecimal());
        Assert.Equal("Pickup", placed.GetProperty("order").GetProperty("shippingType").GetString());
    }

    [Theory]
    [InlineData("bankTransfer")]
    [InlineData("creditCard")]
    public async Task UnavailablePaymentMethods_AreRefused_AndRollBack(string method)
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        var response = await shopper.CheckoutAsync(new { shippingMethodId = store.PickupId, paymentMethod = method, contact = new { name = "A", phone = "+99365111111" } });

        Assert.Equal(ErrorCodes.Payment.MethodUnavailable, await response.ReadCode());
        Assert.Equal(0L, await CountOrdersAsync(store));
    }

    [Fact]
    public async Task BankTransfer_ReturnsTheStoresInstructions()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync("/api/v1/admin/store/payment-settings", new
        {
            acceptsCashOnDelivery = false,
            acceptsBankTransfer = true,
            bankTransferInstructions = "Hasap: 123456",
        })).StatusCode);

        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        var quote = await SalesHelpers.ReadAsync(await shopper.QuoteAsync());
        Assert.Equal("bankTransfer", Assert.Single(quote.GetProperty("paymentOptions").EnumerateArray()).GetProperty("code").GetString());

        var cash = await shopper.CheckoutAsync(new { shippingMethodId = store.PickupId, paymentMethod = "cashOnDelivery", contact = new { name = "A", phone = "+99365111111" } });
        Assert.Equal(ErrorCodes.Payment.MethodUnavailable, await cash.ReadCode());

        var placed = await SalesHelpers.ReadAsync(await shopper.CheckoutAsync(new { shippingMethodId = store.PickupId, paymentMethod = "bankTransfer", contact = new { name = "A", phone = "+99365111111" } }));
        Assert.Equal("Hasap: 123456", placed.GetProperty("paymentInstructions").GetString());
    }

    [Fact]
    public async Task SignedInCustomer_CanUseTheirOwnAddress_ButNotAnothers()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        var owner = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        var attacker = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var ownerClient = api.CreateStoreClient(store.Slug, owner.AccessToken);
        var address = await SalesHelpers.ReadAsync(await ownerClient.PostAsJsonAsync("/api/v1/account/addresses", new { address = SalesHelpers.Address() }));
        var addressId = address.GetProperty("id").GetGuid();

        using var attackerShopper = new Shopper(api, store.Slug, attacker.AccessToken);
        await attackerShopper.AddAsync(product.VariantId);

        var stolen = await attackerShopper.CheckoutAsync(new { shippingMethodId = store.CourierId, paymentMethod = "cashOnDelivery", addressId });

        Assert.Equal(HttpStatusCode.NotFound, stolen.StatusCode);
        Assert.Equal(ErrorCodes.Customer.AddressNotFound, await stolen.ReadCode());

        using var ownerShopper = new Shopper(api, store.Slug, owner.AccessToken);
        await ownerShopper.AddAsync(product.VariantId);

        var placed = await SalesHelpers.ReadAsync(await ownerShopper.CheckoutAsync(new { shippingMethodId = store.CourierId, paymentMethod = "cashOnDelivery", addressId }));

        Assert.Equal("Aýna Berdiýewa", placed.GetProperty("order").GetProperty("shipTo").GetProperty("recipient").GetString());
    }

    [Fact]
    public async Task OpenGuestOrders_AreCappedPerPhone()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 50);
        const string Phone = "+99365222222";

        for (var index = 0; index < 3; index++)
        {
            using var shopper = new Shopper(api, store.Slug);
            await shopper.AddAsync(product.VariantId);
            await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store, Phone));
        }

        using var spammer = new Shopper(api, store.Slug);
        await spammer.AddAsync(product.VariantId);

        var response = await spammer.PlaceCourierOrderAsync(store, Phone);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(ErrorCodes.Checkout.TooManyOpenOrders, await response.ReadCode());
    }

    [Fact]
    public async Task CheckoutRateLimit_Applies()
    {
        var store = await api.CreateSellingStoreAsync();

        using var factory = api.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<RateLimitSettings>(settings => settings.CheckoutPerMinute = 2)));

        var options = ApiFixture.DefaultClientOptions();
        options.BaseAddress = new Uri($"https://{store.Slug}.{ApiFixture.BaseDomain}");
        using var client = factory.CreateClient(options);
        client.DefaultRequestHeaders.Add("X-Requested-With", "ComTest");

        HttpResponseMessage response = null!;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/checkout")
            {
                Content = JsonContent.Create(new { shippingMethodId = store.PickupId, paymentMethod = "cashOnDelivery" }),
            };
            request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            response = await client.SendAsync(request);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task AnOrder_KeepsTheNameAndPriceItWasBoughtAt()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m, name: "Köne at");
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));
        var orderId = placed.GetProperty("order").GetProperty("id").GetGuid();

        await admin.PutAsJsonAsync(
            $"/api/v1/admin/catalog/products/{product.ProductId}",
            CatalogRequests.Product(product.Slug, "SKU-" + product.Slug, price: 999m, name: "Täze at", variantId: product.VariantId));

        var order = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/orders/{orderId}");
        var item = order.GetProperty("order").GetProperty("items")[0];

        Assert.Equal("Köne at", item.GetProperty("productName").GetString());
        Assert.Equal(100m, item.GetProperty("unitPrice").GetDecimal());
    }

    private Task<long> CountOrdersAsync(SellingStore store) =>
        api.QueryAsOwnerAsync<long>("SELECT count(*) FROM orders WHERE tenant_id = @id", ("id", store.Store.TenantId));
}
