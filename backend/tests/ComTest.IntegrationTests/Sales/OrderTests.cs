using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Ordering;
using ComTest.IntegrationTests.Catalog;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace ComTest.IntegrationTests.Sales;

[Collection(ApiCollection.Name)]
public sealed class OrderWorkflowTests(ApiFixture api)
{
    [Fact]
    public async Task Workflow_MovesStockAtTheRightMoments_AndKeepsHistory()
    {
        var (store, product, orderId) = await PlaceOrderAsync(quantity: 2, stock: 10);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        Assert.Equal((10, 2), await api.StockOfAsync(product.VariantId));

        await ChangeAsync(admin, orderId, "confirmed");
        await ChangeAsync(admin, orderId, "packed");
        Assert.Equal((10, 2), await api.StockOfAsync(product.VariantId));

        var shipped = await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new
        {
            status = "shipped",
            carrier = "Kurýer gullugy",
            trackingNumber = "TRK-1",
            trackingUrl = "https://track.example.test/TRK-1",
        }));

        Assert.Equal((8, 0), await api.StockOfAsync(product.VariantId));
        Assert.Equal("TRK-1", shipped.GetProperty("order").GetProperty("tracking").GetProperty("number").GetString());

        var delivered = await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "delivered", note = "Gowşuryldy" }));
        var history = delivered.GetProperty("history").EnumerateArray().ToList();

        Assert.Equal(["Pending", "Confirmed", "Packed", "Shipped", "Delivered"], history.Select(item => item.GetProperty("to").GetString()));
        Assert.Equal(store.Store.Owner.UserId, history[^1].GetProperty("by").GetGuid());
        Assert.Equal("Gowşuryldy", history[^1].GetProperty("note").GetString());

        foreach (var eventName in new[] { "OrderCreated", "OrderShipped", "OrderDelivered" })
        {
            Assert.Equal(1L, await api.QueryAsOwnerAsync<long>(
                "SELECT count(*) FROM outbox_messages WHERE type LIKE @type AND payload->>'OrderId' = @id",
                ("type", "%" + eventName),
                ("id", orderId.ToString())));
        }
    }

    [Theory]
    [InlineData("shipped")]
    [InlineData("delivered")]
    [InlineData("returned")]
    [InlineData("pending")]
    public async Task SkippingSteps_IsRefused(string status)
    {
        var (store, _, orderId) = await PlaceOrderAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Order.InvalidTransition, await response.ReadCode());
    }

    [Fact]
    public async Task Cancelling_ReleasesTheStock_AndTheExpectedPayment()
    {
        var (store, product, orderId) = await PlaceOrderAsync(quantity: 3, stock: 10);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        await ChangeAsync(admin, orderId, "cancelled");

        Assert.Equal((10, 0), await api.StockOfAsync(product.VariantId));

        var payment = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/orders/{orderId}/payment");

        Assert.Equal("Cancelled", payment.GetProperty("status").GetString());

        var again = await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "confirmed" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Returning_RestocksOnce()
    {
        var (store, product, orderId) = await PlaceOrderAsync(quantity: 2, stock: 10);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        foreach (var status in new[] { "confirmed", "packed", "shipped", "delivered" })
        {
            await ChangeAsync(admin, orderId, status);
        }

        Assert.Equal((8, 0), await api.StockOfAsync(product.VariantId));

        await ChangeAsync(admin, orderId, "returned");

        Assert.Equal((10, 0), await api.StockOfAsync(product.VariantId));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "returned" })).StatusCode);
    }

    [Fact]
    public async Task StaffCanMoveOrdersForward_ButOnlyManagersCanUndoThem()
    {
        var (store, _, orderId) = await PlaceOrderAsync();
        var (_, staffToken, _) = await api.AddStaffAsync(store.Store, Roles.Staff);
        var (_, managerToken, _) = await api.AddStaffAsync(store.Store, Roles.Manager);

        using var staff = api.CreateStoreClient(store.Slug, staffToken);
        using var manager = api.CreateStoreClient(store.Slug, managerToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "cancelled" })).StatusCode);

        await ChangeAsync(staff, orderId, "confirmed");
        await ChangeAsync(manager, orderId, "cancelled");
    }

    [Fact]
    public async Task ATrackingLink_MustBeHttp()
    {
        var (store, _, orderId) = await PlaceOrderAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        await ChangeAsync(admin, orderId, "confirmed");
        await ChangeAsync(admin, orderId, "packed");

        var response = await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "shipped", trackingUrl = "javascript:alert(1)" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SimultaneousStatusChanges_CannotBothWin()
    {
        var (store, product, orderId) = await PlaceOrderAsync(quantity: 2, stock: 10);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        await ChangeAsync(admin, orderId, "confirmed");
        await ChangeAsync(admin, orderId, "packed");

        var responses = await Task.WhenAll(
            admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "shipped" }),
            admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "shipped" }),
            admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "shipped" }));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Equal((8, 0), await api.StockOfAsync(product.VariantId));
    }

    private async Task<(SellingStore Store, SellableProduct Product, Guid OrderId)> PlaceOrderAsync(int quantity = 1, int stock = 10)
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: stock);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId, quantity);

        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));

        return (store, product, placed.GetProperty("order").GetProperty("id").GetGuid());
    }

    private static async Task ChangeAsync(HttpClient admin, Guid orderId, string status) =>
        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status }));
}

[Collection(ApiCollection.Name)]
public sealed class CustomerOrderTests(ApiFixture api)
{
    [Fact]
    public async Task Customers_SeeOnlyTheirOwnOrders()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        var owner = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        var other = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var shopper = new Shopper(api, store.Slug, owner.AccessToken);
        await shopper.AddAsync(product.VariantId);
        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));
        var orderId = placed.GetProperty("order").GetProperty("id").GetGuid();

        using var ownerClient = api.CreateStoreClient(store.Slug, owner.AccessToken);
        using var otherClient = api.CreateStoreClient(store.Slug, other.AccessToken);

        Assert.Equal(1, (await ownerClient.GetFromJsonAsync<JsonElement>("/api/v1/account/orders")).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await ownerClient.GetAsync($"/api/v1/account/orders/{orderId}")).StatusCode);

        Assert.Equal(0, (await otherClient.GetFromJsonAsync<JsonElement>("/api/v1/account/orders")).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.GetAsync($"/api/v1/account/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.PostAsync($"/api/v1/account/orders/{orderId}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.GetAsync($"/api/v1/admin/orders/{orderId}")).StatusCode);

        using var anonymous = api.CreateStoreClient(store.Slug);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/account/orders/{orderId}")).StatusCode);
    }

    [Fact]
    public async Task Customers_CanCancelOnlyUntilTheShopStarts()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 10);
        var account = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var shopper = new Shopper(api, store.Slug, account.AccessToken);
        using var client = api.CreateStoreClient(store.Slug, account.AccessToken);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        await shopper.AddAsync(product.VariantId, 2);
        var first = (await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store))).GetProperty("order").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/v1/account/orders/{first}/cancel", null)).StatusCode);
        Assert.Equal((10, 0), await api.StockOfAsync(product.VariantId));

        await shopper.AddAsync(product.VariantId, 1);
        var second = (await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store))).GetProperty("order").GetProperty("id").GetGuid();
        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{second}/status", new { status = "confirmed" }));

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/v1/account/orders/{second}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task Customers_CannotCancelAnOrderThatIsAlreadyPaid()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 10);
        var account = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var shopper = new Shopper(api, store.Slug, account.AccessToken);
        using var client = api.CreateStoreClient(store.Slug, account.AccessToken);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        await shopper.AddAsync(product.VariantId);
        var orderId = (await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store))).GetProperty("order").GetProperty("id").GetGuid();

        await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { }));

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/api/v1/account/orders/{orderId}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task StaffNotes_AreNeverShownToCustomers()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        var account = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var shopper = new Shopper(api, store.Slug, account.AccessToken);
        await shopper.AddAsync(product.VariantId);
        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));
        var orderId = placed.GetProperty("order").GetProperty("id").GetGuid();

        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var withNote = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/notes", new { text = "Içerki gizlin bellik" }));

        Assert.Equal("Içerki gizlin bellik", withNote.GetProperty("notes")[0].GetProperty("text").GetString());

        using var client = api.CreateStoreClient(store.Slug, account.AccessToken);
        var view = await (await client.GetAsync($"/api/v1/account/orders/{orderId}")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("gizlin", view, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GuestTracking_ShowsStatus_WithoutPersonalDetails()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));
        var token = placed.GetProperty("trackingToken").GetString();

        using var anonymous = api.CreateStoreClient(store.Slug);
        var response = await anonymous.PostAsJsonAsync("/api/v1/orders/track", new { token });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"status\":\"Pending\"", body, StringComparison.Ordinal);

        foreach (var secret in new[] { "+99365111111", "Köçe", "Aýna", "phone", "line1", "\"sku\"" })
        {
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ForgedOrForeignTrackingTokens_AreRefused()
    {
        var store = await api.CreateSellingStoreAsync();
        var other = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));
        var token = placed.GetProperty("trackingToken").GetString()!;

        using var here = api.CreateStoreClient(store.Slug);
        using var elsewhere = api.CreateStoreClient(other.Slug);

        Assert.Equal(HttpStatusCode.NotFound, (await elsewhere.PostAsJsonAsync("/api/v1/orders/track", new { token })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await here.PostAsJsonAsync("/api/v1/orders/track", new { token = "forged" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await here.PostAsJsonAsync("/api/v1/orders/track", new { token = token[..^4] + "AAAA" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await here.PostAsJsonAsync("/api/v1/orders/track", new { token = placed.GetProperty("order").GetProperty("number").GetString() })).StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public sealed class AdminOrderListTests(ApiFixture api)
{
    [Fact]
    public async Task Listing_Filters_Sorts_AndIsolatesStores()
    {
        var store = await api.CreateSellingStoreAsync();
        var other = await api.CreateSellingStoreAsync();
        var cheap = await api.CreateSellableProductAsync(store, price: 100m, stock: 10);
        var dear = await api.CreateSellableProductAsync(store, price: 900m, stock: 10);

        using var first = new Shopper(api, store.Slug);
        await first.AddAsync(cheap.VariantId);
        var one = await SalesHelpers.ReadAsync(await first.PlaceCourierOrderAsync(store, "+99365333333"));

        using var second = new Shopper(api, store.Slug);
        await second.AddAsync(dear.VariantId);
        var two = await SalesHelpers.ReadAsync(await second.PlaceCourierOrderAsync(store, "+99365444444"));

        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var twoId = two.GetProperty("order").GetProperty("id").GetGuid();
        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{twoId}/status", new { status = "confirmed" }));

        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/orders")).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/orders?status=confirmed")).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/orders?q=%2B99365333333")).GetProperty("totalCount").GetInt32());
        Assert.Equal(1, (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/orders?q={one.GetProperty("order").GetProperty("number").GetString()}")).GetProperty("totalCount").GetInt32());
        Assert.Equal(0, (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/orders?to=2000-01-01T00:00:00Z")).GetProperty("totalCount").GetInt32());
        Assert.Equal(2, (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/orders?from=2000-01-01T00:00:00Z")).GetProperty("totalCount").GetInt32());

        var byTotal = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/orders?sort=-total");
        Assert.Equal(twoId, byTotal.GetProperty("items")[0].GetProperty("id").GetGuid());

        Assert.Equal(ErrorCodes.Common.InvalidSort, await (await admin.GetAsync("/api/v1/admin/orders?sort=contact.phone")).ReadCode());

        using var attacker = api.CreateStoreClient(other.Slug, other.AccessToken);
        Assert.Equal(0, (await attacker.GetFromJsonAsync<JsonElement>("/api/v1/admin/orders")).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.GetAsync($"/api/v1/admin/orders/{twoId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.PutAsJsonAsync($"/api/v1/admin/orders/{twoId}/status", new { status = "cancelled" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.PostAsJsonAsync($"/api/v1/admin/orders/{twoId}/notes", new { text = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.GetAsync($"/api/v1/admin/orders/{twoId}/payment")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.PostAsJsonAsync($"/api/v1/admin/orders/{twoId}/payment/confirm", new { })).StatusCode);
    }

    [Fact]
    public async Task Customers_AreSummarisedFromOrders()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m, stock: 10);
        const string Phone = "+99365555555";

        for (var index = 0; index < 2; index++)
        {
            using var shopper = new Shopper(api, store.Slug);
            await shopper.AddAsync(product.VariantId);
            await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store, Phone));
        }

        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var customers = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/customers");
        var customer = Assert.Single(customers.GetProperty("items").EnumerateArray());

        Assert.Equal(Phone, customer.GetProperty("phone").GetString());
        Assert.Equal(2, customer.GetProperty("orderCount").GetInt32());
        Assert.Equal(240m, customer.GetProperty("totalSpent").GetDecimal());

        var shopperAccount = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateStoreClient(store.Slug, shopperAccount.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/customers")).StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public sealed class HousekeepingTests(ApiFixture api)
{
    [Fact]
    public async Task UnconfirmedOrders_ExpireInEveryStore_AndFreeTheirStock()
    {
        var first = await api.CreateSellingStoreAsync();
        var second = await api.CreateSellingStoreAsync();
        var firstProduct = await api.CreateSellableProductAsync(first, stock: 10);
        var secondProduct = await api.CreateSellableProductAsync(second, stock: 10);

        var expired1 = await PlaceAsync(first, firstProduct, 2);
        var expired2 = await PlaceAsync(second, secondProduct, 3);
        var fresh = await PlaceAsync(first, firstProduct, 1);
        var confirmed = await PlaceAsync(first, firstProduct, 1);

        using var admin = api.CreateStoreClient(first.Slug, first.AccessToken);
        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{confirmed}/status", new { status = "confirmed" }));

        foreach (var id in new[] { expired1, expired2 })
        {
            await api.ExecuteAsOwnerAsync("UPDATE orders SET expires_at = now() - interval '1 minute' WHERE id = @id", ("id", id));
        }

        var housekeeping = api.Factory.Services.GetRequiredService<SalesHousekeeping>();

        Assert.True(await housekeeping.ExpirePendingOrdersAsync(CancellationToken.None) >= 2);

        Assert.Equal("Cancelled", await StatusOfAsync(expired1));
        Assert.Equal("Cancelled", await StatusOfAsync(expired2));
        Assert.Equal("Pending", await StatusOfAsync(fresh));
        Assert.Equal("Confirmed", await StatusOfAsync(confirmed));

        Assert.Equal((10, 2), await api.StockOfAsync(firstProduct.VariantId));
        Assert.Equal((10, 0), await api.StockOfAsync(secondProduct.VariantId));
        Assert.Equal("expired", await api.QueryAsOwnerAsync<string>(
            "SELECT note FROM order_status_changes WHERE order_id = @id AND to_status = 'Cancelled'",
            ("id", expired1)));
    }

    [Fact]
    public async Task APaidOrder_NeverExpires()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 10);
        var orderId = await PlaceAsync(store, product, 1);

        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { }));

        Assert.Null(await api.QueryAsOwnerAsync<DateTime?>("SELECT expires_at FROM orders WHERE id = @id", ("id", orderId)));

        await api.ExecuteAsOwnerAsync("UPDATE orders SET expires_at = now() - interval '1 minute' WHERE id = @id", ("id", orderId));
        await api.Factory.Services.GetRequiredService<SalesHousekeeping>().ExpirePendingOrdersAsync(CancellationToken.None);

        Assert.Equal("Pending", await StatusOfAsync(orderId));
    }

    [Fact]
    public async Task AbandonedGuestCarts_AreRemoved_ButNotCustomerCarts()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        var account = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var guest = new Shopper(api, store.Slug);
        using var customer = new Shopper(api, store.Slug, account.AccessToken);
        await guest.AddAsync(product.VariantId);
        await customer.AddAsync(product.VariantId);

        await api.ExecuteAsOwnerAsync("UPDATE carts SET last_activity_at = now() - interval '40 days' WHERE tenant_id = @id", ("id", store.Store.TenantId));

        await api.Factory.Services.GetRequiredService<SalesHousekeeping>().DeleteAbandonedGuestCartsAsync(CancellationToken.None);

        Assert.Equal(0, (await SalesHelpers.ReadAsync(await guest.GetCartAsync())).GetProperty("itemCount").GetInt32());
        Assert.Equal(1, (await SalesHelpers.ReadAsync(await customer.GetCartAsync())).GetProperty("itemCount").GetInt32());
    }

    private async Task<Guid> PlaceAsync(SellingStore store, SellableProduct product, int quantity)
    {
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId, quantity);

        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store, "+9936" + Random.Shared.Next(1000000, 9999999)));

        return placed.GetProperty("order").GetProperty("id").GetGuid();
    }

    private Task<string?> StatusOfAsync(Guid orderId) =>
        api.QueryAsOwnerAsync<string>("SELECT status FROM orders WHERE id = @id", ("id", orderId));
}
