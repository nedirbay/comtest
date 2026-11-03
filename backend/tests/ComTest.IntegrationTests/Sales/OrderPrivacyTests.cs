using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.IntegrationTests.Support;

namespace ComTest.IntegrationTests.Sales;

/// <summary>
/// No built-in role lacks `customers.read` today, so these tests take it away from one for a moment.
/// Tests in this collection run one at a time, and permissions are read when a token is issued.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class OrderPrivacyTests(ApiFixture api)
{
    private const string CustomerPhone = "+99365222333";

    private async Task<(SellingStore Store, string StaffToken, Guid OrderId, string Number)> ArrangeAsync()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 5);
        var shopper = new Shopper(api, store.Slug);

        (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store, phone: CustomerPhone));
        var order = placed.GetProperty("order");

        await api.ExecuteAsOwnerAsync(
            "DELETE FROM role_claims WHERE claim_value = @permission AND role_id = (SELECT id FROM roles WHERE name = @role)",
            ("permission", Permissions.CustomersRead),
            ("role", Roles.Staff));

        var (_, token, _) = await api.AddStaffAsync(store.Store, Roles.Staff);

        return (store, token, order.GetProperty("id").GetGuid(), order.GetProperty("number").GetString()!);
    }

    private Task RestoreAsync() => api.ExecuteAsOwnerAsync(
        "INSERT INTO role_claims (role_id, claim_type, claim_value) SELECT id, 'permission', @permission FROM roles WHERE name = @role",
        ("permission", Permissions.CustomersRead),
        ("role", Roles.Staff));

    [Fact]
    public async Task StaffWithoutThePermission_SeeNoContactDetails()
    {
        var (store, token, orderId, _) = await ArrangeAsync();

        try
        {
            using var staff = api.CreateStoreClient(store.Slug, token);

            var list = await SalesHelpers.ReadAsync(await staff.GetAsync(new Uri("/api/v1/admin/orders?page=1&pageSize=10", UriKind.Relative)));
            var row = list.GetProperty("items")[0];

            Assert.Equal("A***", row.GetProperty("contactName").GetString());
            Assert.Equal("***33", row.GetProperty("contactPhone").GetString());

            var detail = await SalesHelpers.ReadAsync(await staff.GetAsync(new Uri($"/api/v1/admin/orders/{orderId}", UriKind.Relative)));
            var text = detail.GetRawText();

            Assert.DoesNotContain(CustomerPhone, text, StringComparison.Ordinal);
            Assert.DoesNotContain("Aýna", text, StringComparison.Ordinal);

            var order = detail.GetProperty("order");

            Assert.Equal(JsonValueKind.Null, order.GetProperty("contact").GetProperty("email").ValueKind);
            Assert.Equal(JsonValueKind.Null, order.GetProperty("shipTo").GetProperty("line1").ValueKind);
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("customerUserId").ValueKind);

            // The region stays, which is what deciding how to send it takes.
            Assert.False(string.IsNullOrEmpty(order.GetProperty("shipTo").GetProperty("region").GetString()));
        }
        finally
        {
            await RestoreAsync();
        }
    }

    [Fact]
    public async Task StaffWithoutThePermission_GetNoDetailsFromAChangeEither()
    {
        var (store, token, orderId, _) = await ArrangeAsync();

        try
        {
            using var staff = api.CreateStoreClient(store.Slug, token);

            var changed = await staff.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "confirmed" });

            Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
            Assert.DoesNotContain(CustomerPhone, await changed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

            var noted = await staff.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/notes", new { text = "Gaplandy" });

            Assert.Equal(HttpStatusCode.OK, noted.StatusCode);
            Assert.DoesNotContain(CustomerPhone, await noted.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
        finally
        {
            await RestoreAsync();
        }
    }

    [Fact]
    public async Task StaffWithoutThePermission_CannotFindWhoANumberBelongsTo()
    {
        var (store, token, _, number) = await ArrangeAsync();

        try
        {
            using var staff = api.CreateStoreClient(store.Slug, token);

            var byPhone = await SalesHelpers.ReadAsync(await staff.GetAsync(new Uri($"/api/v1/admin/orders?page=1&pageSize=10&q={Uri.EscapeDataString(CustomerPhone)}", UriKind.Relative)));
            var byNumber = await SalesHelpers.ReadAsync(await staff.GetAsync(new Uri($"/api/v1/admin/orders?page=1&pageSize=10&q={number}", UriKind.Relative)));

            Assert.Equal(0, byPhone.GetProperty("totalCount").GetInt32());
            Assert.Equal(1, byNumber.GetProperty("totalCount").GetInt32());
        }
        finally
        {
            await RestoreAsync();
        }
    }

    [Fact]
    public async Task StaffWithThePermission_SeeEverything()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 5);
        var shopper = new Shopper(api, store.Slug);

        (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();
        (await shopper.PlaceCourierOrderAsync(store, phone: CustomerPhone)).EnsureSuccessStatusCode();

        var (_, token, _) = await api.AddStaffAsync(store.Store, Roles.Staff);
        using var staff = api.CreateStoreClient(store.Slug, token);

        var list = await SalesHelpers.ReadAsync(await staff.GetAsync(new Uri("/api/v1/admin/orders?page=1&pageSize=10", UriKind.Relative)));
        var row = list.GetProperty("items")[0];

        Assert.Equal("Aýna", row.GetProperty("contactName").GetString());
        Assert.Equal(CustomerPhone, row.GetProperty("contactPhone").GetString());
    }
}
