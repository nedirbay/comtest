using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.IntegrationTests.Catalog;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;

namespace ComTest.IntegrationTests.Sales;

[Collection(ApiCollection.Name)]
public sealed class PaymentTests(ApiFixture api)
{
    [Fact]
    public async Task ConfirmingCash_MarksTheOrderPaid_OnlyOnce_AndIsJournaled()
    {
        var (store, orderId) = await PlaceAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var first = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { note = "Kurýer aldy" }));
        var second = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { }));

        Assert.Equal("Paid", first.GetProperty("status").GetString());
        Assert.Equal("Paid", second.GetProperty("status").GetString());

        var events = second.GetProperty("events").EnumerateArray().Select(item => item.GetProperty("type").GetString()).ToList();

        Assert.Equal(["Created", "Confirmed"], events);
        Assert.Equal(store.Store.Owner.UserId, second.GetProperty("events")[1].GetProperty("by").GetGuid());

        var order = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/orders/{orderId}");
        Assert.Equal("Paid", order.GetProperty("order").GetProperty("paymentState").GetString());

        Assert.Equal(1L, await api.QueryAsOwnerAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE type LIKE '%OrderPaid' AND payload->>'OrderId' = @id",
            ("id", orderId.ToString())));
    }

    [Fact]
    public async Task ABrowserCannotMarkAnOrderPaid()
    {
        var (store, orderId) = await PlaceAsync();
        var customer = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        var (_, staffToken, _) = await api.AddStaffAsync(store.Store, Roles.Staff);

        using var shopper = api.CreateStoreClient(store.Slug, customer.AccessToken);
        using var anonymous = api.CreateStoreClient(store.Slug);
        using var staff = api.CreateStoreClient(store.Slug, staffToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await shopper.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { })).StatusCode);

        Assert.Equal("Unpaid", await api.QueryAsOwnerAsync<string>("SELECT payment_state FROM orders WHERE id = @id", ("id", orderId)));
    }

    [Fact]
    public async Task ACancelledOrder_CannotBePaid()
    {
        var (store, orderId) = await PlaceAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "cancelled" }));

        var response = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { });

        Assert.Equal(ErrorCodes.Payment.InvalidState, await response.ReadCode());
    }

    [Fact]
    public async Task CancellingAPaidOrder_KeepsThePaymentForARefund()
    {
        var (store, orderId) = await PlaceAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { }));
        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "cancelled" }));

        var payment = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/orders/{orderId}/payment");

        Assert.Equal("Paid", payment.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Refunds_AddUp_AndCannotExceedThePayment()
    {
        var (store, orderId) = await PlaceAsync(price: 100m);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var unpaid = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount = 10m, reason = "x" });
        Assert.Equal(ErrorCodes.Payment.InvalidState, await unpaid.ReadCode());

        await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { }));

        var partial = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount = 70m, reason = "Bölek" }));
        Assert.Equal("PartiallyRefunded", partial.GetProperty("status").GetString());
        Assert.Equal("PartiallyRefunded", await api.QueryAsOwnerAsync<string>("SELECT payment_state FROM orders WHERE id = @id", ("id", orderId)));

        // The order total is 120: the item plus 20 for delivery.
        var tooMuch = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount = 50.01m, reason = "x" });
        Assert.Equal(ErrorCodes.Payment.RefundExceedsPaid, await tooMuch.ReadCode());

        var rest = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount = 50m, reason = "Galany" }));
        Assert.Equal("Refunded", rest.GetProperty("status").GetString());
        Assert.Equal("Refunded", await api.QueryAsOwnerAsync<string>("SELECT payment_state FROM orders WHERE id = @id", ("id", orderId)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ARefundMustBePositive(int amount)
    {
        var (store, orderId) = await PlaceAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount, reason = "x" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task OnlyTheOwner_CanRefund()
    {
        var (store, orderId) = await PlaceAsync();
        var (_, managerToken, _) = await api.AddStaffAsync(store.Store, Roles.Manager);
        using var manager = api.CreateStoreClient(store.Slug, managerToken);

        var response = await manager.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount = 1m, reason = "x" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task LargeRefunds_NeedASecondPerson()
    {
        var (store, orderId) = await PlaceAsync(price: 1000m);
        var (_, managerToken, _) = await api.AddStaffAsync(store.Store, Roles.Manager);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var manager = api.CreateStoreClient(store.Slug, managerToken);

        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync("/api/v1/admin/store/payment-settings", new
        {
            acceptsCashOnDelivery = true,
            acceptsBankTransfer = false,
            refundApprovalThreshold = 500m,
        }).ContinueWith(task => EnsureNoContent(task.Result)));

        await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { }));

        var small = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount = 100m, reason = "Az" }));
        Assert.Equal(100m, small.GetProperty("refundedAmount").GetDecimal());

        var large = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount = 600m, reason = "Köp" }));

        Assert.Equal(100m, large.GetProperty("refundedAmount").GetDecimal());
        Assert.Equal(600m, large.GetProperty("pendingRefundAmount").GetDecimal());

        var refundId = large.GetProperty("refunds")[1].GetProperty("id").GetGuid();

        var self = await admin.PostAsync($"/api/v1/admin/orders/{orderId}/payment/refunds/{refundId}/approve", null);
        Assert.Equal(ErrorCodes.Payment.SameApprover, await self.ReadCode());

        var approved = await SalesHelpers.ReadAsync(await manager.PostAsync($"/api/v1/admin/orders/{orderId}/payment/refunds/{refundId}/approve", null));

        Assert.Equal(700m, approved.GetProperty("refundedAmount").GetDecimal());
        Assert.Equal(0m, approved.GetProperty("pendingRefundAmount").GetDecimal());
        Assert.Equal("Completed", approved.GetProperty("refunds")[1].GetProperty("status").GetString());

        var again = await manager.PostAsync($"/api/v1/admin/orders/{orderId}/payment/refunds/{refundId}/approve", null);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
    }

    [Fact]
    public async Task SimultaneousRefunds_CannotOverdraw()
    {
        var (store, orderId) = await PlaceAsync(price: 100m);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { }));

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount = 50m, reason = "x" })));

        Assert.DoesNotContain(responses, response => response.StatusCode == HttpStatusCode.InternalServerError);

        var refunded = await api.QueryAsOwnerAsync<decimal>("SELECT refunded_amount FROM payments WHERE order_id = @id", ("id", orderId));

        Assert.True(refunded <= 120m, $"Refunded {refunded} of 120.");
    }

    [Fact]
    public async Task PaymentAmountsCannotBreakTheirLimits_EvenInTheDatabase()
    {
        var (store, orderId) = await PlaceAsync(price: 100m);

        await using var connection = api.OpenApplicationConnection();
        await using var setTenant = new Npgsql.NpgsqlCommand($"SELECT set_config('app.tenant_id', '{store.Store.TenantId}', false)", connection);
        await setTenant.ExecuteNonQueryAsync();

        await using var command = new Npgsql.NpgsqlCommand($"UPDATE payments SET refunded_amount = amount_amount + 1 WHERE order_id = '{orderId}'", connection);

        var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal("23514", exception.SqlState);
    }

    [Fact]
    public async Task EveryOrder_HasExactlyOnePayment()
    {
        var (store, orderId) = await PlaceAsync();

        await using var connection = api.OpenApplicationConnection();
        await using var setTenant = new Npgsql.NpgsqlCommand($"SELECT set_config('app.tenant_id', '{store.Store.TenantId}', false)", connection);
        await setTenant.ExecuteNonQueryAsync();

        await using var command = new Npgsql.NpgsqlCommand(
            $"""
            INSERT INTO payments (id, tenant_id, order_id, method, status, amount_amount, amount_currency, refunded_amount, pending_refund_amount, created_at)
            VALUES (gen_random_uuid(), '{store.Store.TenantId}', '{orderId}', 'CashOnDelivery', 'Pending', 1, 'TMT', 0, 0, now())
            """,
            connection);

        var exception = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal("23505", exception.SqlState);
    }

    private static HttpResponseMessage EnsureNoContent(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { }) };
    }

    private async Task<(SellingStore Store, Guid OrderId)> PlaceAsync(decimal price = 100m)
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: price);
        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId);

        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));

        return (store, placed.GetProperty("order").GetProperty("id").GetGuid());
    }
}

/// <summary>The whole story, as one customer and one shop live it.</summary>
[Collection(ApiCollection.Name)]
public sealed class FullPurchaseScenarioTests(ApiFixture api)
{
    [Fact]
    public async Task RegisterShopOrderPayDeliver()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 250m, stock: 5, name: "Täze telefon");

        var browser = new Shopper(api, store.Slug);
        var listing = await SalesHelpers.ReadAsync(await browser.SendAsync(HttpMethod.Get, "/api/v1/catalog/products"));
        Assert.True(listing.GetProperty("items")[0].GetProperty("inStock").GetBoolean());

        await SalesHelpers.ReadAsync(await browser.AddAsync(product.VariantId, 2));
        var guestCookie = browser.GuestCookie;
        browser.Dispose();

        var account = await api.CreateConfirmedAccountAsync();
        var session = await api.LoginAsync(account);

        using var customer = new Shopper(api, store.Slug, session.AccessToken) { GuestCookie = guestCookie };

        var cart = await SalesHelpers.ReadAsync(await customer.GetCartAsync());
        Assert.Equal(500m, cart.GetProperty("subtotal").GetDecimal());

        using var accountClient = api.CreateStoreClient(store.Slug, session.AccessToken);
        var address = await SalesHelpers.ReadAsync(await accountClient.PostAsJsonAsync("/api/v1/account/addresses", new { address = SalesHelpers.Address() }));

        var quote = await SalesHelpers.ReadAsync(await customer.QuoteAsync(SalesHelpers.Region, store.CourierId));
        var total = quote.GetProperty("totals").GetProperty("total").GetDecimal();
        Assert.Equal(520m, total);

        var placed = await SalesHelpers.ReadAsync(await customer.CheckoutAsync(new
        {
            shippingMethodId = store.CourierId,
            paymentMethod = "cashOnDelivery",
            addressId = address.GetProperty("id").GetGuid(),
            customerNote = "Agşam getiriň",
            expectedTotal = total,
        }));

        var orderId = placed.GetProperty("order").GetProperty("id").GetGuid();
        Assert.Equal((5, 2), await api.StockOfAsync(product.VariantId));

        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        foreach (var status in new[] { "confirmed", "packed", "shipped" })
        {
            await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status }));
        }

        Assert.Equal((3, 0), await api.StockOfAsync(product.VariantId));

        var tracked = await SalesHelpers.ReadAsync(await accountClient.GetAsync($"/api/v1/account/orders/{orderId}"));
        Assert.Equal("Shipped", tracked.GetProperty("status").GetString());

        await SalesHelpers.ReadAsync(await admin.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "delivered" }));
        await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { note = "Nagt alyndy" }));

        var finished = await SalesHelpers.ReadAsync(await accountClient.GetAsync($"/api/v1/account/orders/{orderId}"));

        Assert.Equal("Delivered", finished.GetProperty("status").GetString());
        Assert.Equal("Paid", finished.GetProperty("paymentState").GetString());
        Assert.Equal(520m, finished.GetProperty("totals").GetProperty("total").GetDecimal());
    }
}
