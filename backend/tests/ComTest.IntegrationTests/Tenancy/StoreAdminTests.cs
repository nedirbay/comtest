using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.IntegrationTests.Support;

namespace ComTest.IntegrationTests.Tenancy;

[Collection(ApiCollection.Name)]
public sealed class StoreAdminTests(ApiFixture api)
{
    private static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());

        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Uri Path(string path) => new(path, UriKind.Relative);

    [Fact]
    public async Task PaymentSettings_CanBeReadBack()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        var initial = await ReadAsync(await owner.GetAsync(Path("/api/v1/admin/store/payment-settings")));

        Assert.True(initial.GetProperty("acceptsCashOnDelivery").GetBoolean());
        Assert.False(initial.GetProperty("acceptsBankTransfer").GetBoolean());

        var saved = await owner.PutAsJsonAsync("/api/v1/admin/store/payment-settings", new
        {
            acceptsCashOnDelivery = false,
            acceptsBankTransfer = true,
            bankTransferInstructions = "Hasap: 123",
            refundApprovalThreshold = 500m,
        });

        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        var settings = await ReadAsync(await owner.GetAsync(Path("/api/v1/admin/store/payment-settings")));

        Assert.False(settings.GetProperty("acceptsCashOnDelivery").GetBoolean());
        Assert.True(settings.GetProperty("acceptsBankTransfer").GetBoolean());
        Assert.Equal("Hasap: 123", settings.GetProperty("bankTransferInstructions").GetString());
        Assert.Equal(500m, settings.GetProperty("refundApprovalThreshold").GetDecimal());
    }

    [Fact]
    public async Task PaymentSettings_AreForThoseWhoMayChangeThem()
    {
        var store = await api.CreateStoreAsync();
        var (_, staffToken, _) = await api.AddStaffAsync(store, Roles.Staff);
        using var staff = api.CreateStoreClient(store.Slug, staffToken);

        var response = await staff.GetAsync(Path("/api/v1/admin/store/payment-settings"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PaymentSettings_OfAnotherStoreStayHidden()
    {
        var mine = await api.CreateStoreAsync();
        var other = await api.CreateStoreAsync();
        using var client = api.CreateStoreClient(other.Slug, mine.AccessToken);

        var response = await client.GetAsync(Path("/api/v1/admin/store/payment-settings"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Invitations_ListOnlyThoseStillOpen()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        var open = $"Open-{Guid.NewGuid():N}@Example.test";
        var revoked = $"revoked-{Guid.NewGuid():N}@example.test";

        await ReadAsync(await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = open, role = Roles.Manager }));

        var toRevoke = await ReadAsync(await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = revoked, role = Roles.Staff }));
        var removed = await owner.DeleteAsync(Path($"/api/v1/admin/staff/invitations/{toRevoke.GetProperty("id").GetGuid()}"));

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var list = await ReadAsync(await owner.GetAsync(Path("/api/v1/admin/staff/invitations")));
        var only = Assert.Single(list.EnumerateArray());

        Assert.Equal(open.ToLowerInvariant(), only.GetProperty("email").GetString());
        Assert.Equal(Roles.Manager, only.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Invitations_DisappearOnceAccepted()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        await api.AddStaffAsync(store, Roles.Staff);

        var list = await ReadAsync(await owner.GetAsync(Path("/api/v1/admin/staff/invitations")));

        Assert.Empty(list.EnumerateArray());
    }

    [Fact]
    public async Task Invitations_OfAnotherStoreStayHidden()
    {
        var first = await api.CreateStoreAsync();
        var second = await api.CreateStoreAsync();
        using var firstOwner = api.CreateStoreClient(first.Slug, first.AccessToken);
        using var secondOwner = api.CreateStoreClient(second.Slug, second.AccessToken);

        await ReadAsync(await firstOwner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = $"a-{Guid.NewGuid():N}@example.test", role = Roles.Staff }));

        var list = await ReadAsync(await secondOwner.GetAsync(Path("/api/v1/admin/staff/invitations")));

        Assert.Empty(list.EnumerateArray());
    }

    [Fact]
    public async Task Invitations_AreForThoseWhoManageStaff()
    {
        var store = await api.CreateStoreAsync();
        var (_, staffToken, _) = await api.AddStaffAsync(store, Roles.Staff);
        using var staff = api.CreateStoreClient(store.Slug, staffToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(Path("/api/v1/admin/staff/invitations"))).StatusCode);
    }
}
