using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Api.Hosting;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Infrastructure.Persistence;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Domain;
using ComTest.Shared.Results;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Npgsql;

namespace ComTest.IntegrationTests.Tenancy;

[Collection(ApiCollection.Name)]
public sealed class TenantRegistrationTests(ApiFixture api)
{
    [Fact]
    public async Task CreatingAStore_MakesTheCallerItsOwner_AndSwitchesTheSession()
    {
        var owner = await api.CreateConfirmedAccountAsync();
        var signedIn = await api.LoginAsync(owner);
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);
        var slug = StoreHelpers.NewSlug();

        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "Ak Bazar", slug });
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var token = new JsonWebToken(body.GetProperty("accessToken").GetString()!);

        Assert.Equal(body.GetProperty("tenant").GetProperty("id").GetString(), token.GetClaim("tid").Value);
        Assert.Contains(token.Claims, claim => claim.Type == "role" && claim.Value == Roles.StoreOwner);
        Assert.Contains(token.Claims, claim => claim.Type == "permission" && claim.Value == Permissions.SettingsManage);
    }

    [Theory]
    [InlineData("api")]
    [InlineData("admin")]
    [InlineData("www")]
    public async Task ReservedSlugs_AreRefused(string slug)
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "X", slug });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Tenant.SlugReserved, await ReadCode(response));
    }

    [Theory]
    [InlineData("UPPER-case")]
    [InlineData("has space")]
    [InlineData("a")]
    [InlineData("-dash-first")]
    [InlineData("dots.not.allowed")]
    public async Task InvalidSlugs_AreRefused(string slug)
    {
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "X", slug });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TakenSlug_IsRefused()
    {
        var store = await api.CreateStoreAsync();
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "X", slug = store.Slug });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ErrorCodes.Tenant.SlugTaken, await ReadCode(response));
    }

    [Fact]
    public async Task Creating_RequiresSignIn()
    {
        using var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = "X", slug = StoreHelpers.NewSlug() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<string?> ReadCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}

[Collection(ApiCollection.Name)]
public sealed class TenantResolutionTests(ApiFixture api)
{
    [Fact]
    public async Task Subdomain_SelectsTheStore()
    {
        var store = await api.CreateStoreAsync();
        using var client = api.CreateStoreClient(store.Slug);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/store");

        Assert.Equal(store.Slug, body.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task PublicStore_DoesNotExposeInternalFields()
    {
        var store = await api.CreateStoreAsync();
        using var client = api.CreateStoreClient(store.Slug);

        var body = await (await client.GetAsync("/api/v1/store")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("maxStaff", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("plan", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnknownSubdomain_IsNotFound()
    {
        using var client = api.CreateStoreClient("no-such-store");

        var response = await client.GetAsync("/api/v1/store");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ErrorCodes.Tenant.NotFound, await ReadCode(response));
    }

    [Fact]
    public async Task PlatformHost_HasNoTenant()
    {
        using var client = api.CreatePlatformClient();

        var response = await client.GetAsync("/api/v1/store");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.Tenant.Required, await ReadCode(response));
    }

    [Fact]
    public async Task OwnDomain_WorksOnlyAfterVerification()
    {
        var store = await api.CreateStoreAsync();
        var hostname = $"shop{Guid.NewGuid():N}.example.test";

        await api.ExecuteAsOwnerAsync(
            """
            INSERT INTO tenant_domains (id, owner_tenant_id, hostname, created_at, verified_at)
            VALUES (gen_random_uuid(), @tenant, @host, now(), NULL)
            """,
            ("tenant", store.TenantId),
            ("host", hostname));

        using var client = api.CreateClient(new() { BaseAddress = new Uri($"https://{hostname}"), AllowAutoRedirect = false });

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/store")).StatusCode);

        await api.ExecuteAsOwnerAsync("UPDATE tenant_domains SET verified_at = now() WHERE hostname = @host", ("host", hostname));

        var verifiedHost = $"verified{Guid.NewGuid():N}.example.test";
        await api.ExecuteAsOwnerAsync(
            "INSERT INTO tenant_domains (id, owner_tenant_id, hostname, created_at, verified_at) VALUES (gen_random_uuid(), @tenant, @host, now(), now())",
            ("tenant", store.TenantId),
            ("host", verifiedHost));

        using var verified = api.CreateClient(new() { BaseAddress = new Uri($"https://{verifiedHost}"), AllowAutoRedirect = false });
        var body = await verified.GetFromJsonAsync<JsonElement>("/api/v1/store");

        Assert.Equal(store.Slug, body.GetProperty("slug").GetString());
    }

    [Fact]
    public async Task TenantHeader_IsIgnored_WhenNotAllowed()
    {
        var store = await api.CreateStoreAsync();
        using var client = api.CreatePlatformClient();
        client.DefaultRequestHeaders.Add(TenantResolutionMiddleware.TenantHeader, store.Slug);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/store")).StatusCode);
    }

    [Fact]
    public async Task TokenForOneStore_IsRefusedOnAnotherStoresAddress()
    {
        var first = await api.CreateStoreAsync();
        var second = await api.CreateStoreAsync();
        using var client = api.CreateStoreClient(second.Slug, first.AccessToken);

        var response = await client.GetAsync("/api/v1/admin/store");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.Tenant.Mismatch, await ReadCode(response));
    }

    [Fact]
    public async Task SuspendedStore_IsClosed_AndReopens()
    {
        var store = await api.CreateStoreAsync();
        var admin = await api.CreatePlatformAdminAsync();
        using var platform = api.CreatePlatformClient(admin);
        using var client = api.CreateStoreClient(store.Slug);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/store")).StatusCode);

        var suspended = await platform.PutAsJsonAsync($"/api/v1/platform/tenants/{store.TenantId}/status", new { status = "suspended" });
        Assert.Equal(HttpStatusCode.OK, suspended.StatusCode);

        var closed = await client.GetAsync("/api/v1/store");
        Assert.Equal(HttpStatusCode.Forbidden, closed.StatusCode);
        Assert.Equal(ErrorCodes.Tenant.Suspended, await ReadCode(closed));

        await platform.PutAsJsonAsync($"/api/v1/platform/tenants/{store.TenantId}/status", new { status = "active" });

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/store")).StatusCode);
    }

    [Fact]
    public async Task PlatformEndpoints_AreClosedToStoreOwners()
    {
        var store = await api.CreateStoreAsync();
        using var client = api.CreatePlatformClient(store.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/platform/tenants")).StatusCode);
    }

    [Fact]
    public async Task PlatformListing_IsPagedAndClamped()
    {
        await api.CreateStoreAsync();
        var admin = await api.CreatePlatformAdminAsync();
        using var client = api.CreatePlatformClient(admin);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/v1/platform/tenants?page=1&pageSize=100000");

        Assert.Equal(100, body.GetProperty("pageSize").GetInt32());
        Assert.True(body.GetProperty("totalCount").GetInt64() >= 1);
    }

    [Fact]
    public async Task TenantRateLimit_IsSharedInAStore_AndDoesNotAffectOthers()
    {
        var busy = await api.CreateStoreAsync();
        var quiet = await api.CreateStoreAsync();

        using var factory = api.Factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<RateLimitSettings>(settings => settings.TenantPerMinute = 2)));

        var options = ApiFixture.DefaultClientOptions();
        options.BaseAddress = new Uri($"https://{busy.Slug}.{ApiFixture.BaseDomain}");
        using var busyClient = factory.CreateClient(options);

        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 3; attempt++)
        {
            statuses.Add((await busyClient.GetAsync("/api/v1/store")).StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);

        options.BaseAddress = new Uri($"https://{quiet.Slug}.{ApiFixture.BaseDomain}");
        using var quietClient = factory.CreateClient(options);

        Assert.Equal(HttpStatusCode.OK, (await quietClient.GetAsync("/api/v1/store")).StatusCode);
    }

    private static async Task<string?> ReadCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}

[Collection(ApiCollection.Name)]
public sealed class StoreSettingsTests(ApiFixture api)
{
    [Fact]
    public async Task Owner_CanReadAndUpdateSettings_WithVersionCheck()
    {
        var store = await api.CreateStoreAsync();
        using var client = api.CreateStoreClient(store.Slug, store.AccessToken);

        var current = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/store");
        var version = current.GetProperty("version").GetUInt32();

        var updated = await client.PutAsJsonAsync("/api/v1/admin/store", Settings("Täze At", version));
        var stale = await client.PutAsJsonAsync("/api/v1/admin/store", Settings("Köne wersiýa", version));

        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var publicStore = await api.CreateStoreClient(store.Slug).GetFromJsonAsync<JsonElement>("/api/v1/store");

        Assert.Equal("Täze At", publicStore.GetProperty("name").GetString());
        Assert.Equal("USD", publicStore.GetProperty("defaultCurrency").GetString());
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,x")]
    [InlineData("not a url")]
    public async Task LogoUrl_MustBeHttp(string logoUrl)
    {
        var store = await api.CreateStoreAsync();
        using var client = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await client.PutAsJsonAsync("/api/v1/admin/store", new
        {
            name = "X",
            defaultCurrency = "TMT",
            defaultLanguage = "tk",
            logoUrl,
            version = 0,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UnsupportedCurrency_IsRefused()
    {
        var store = await api.CreateStoreAsync();
        using var client = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await client.PutAsJsonAsync("/api/v1/admin/store", new { name = "X", defaultCurrency = "EUR", defaultLanguage = "tk", version = 0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Staff_CannotChangeSettings()
    {
        var store = await api.CreateStoreAsync();
        var (_, staffToken, _) = await api.AddStaffAsync(store, Roles.Staff);
        using var client = api.CreateStoreClient(store.Slug, staffToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/store")).StatusCode);
    }

    [Fact]
    public async Task Customer_CannotChangeSettings()
    {
        var store = await api.CreateStoreAsync();
        var customer = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateStoreClient(store.Slug, customer.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/admin/store")).StatusCode);
    }

    private static object Settings(string name, uint version) => new
    {
        name,
        defaultCurrency = "USD",
        defaultLanguage = "ru",
        contactEmail = "shop@example.test",
        version,
    };
}

[Collection(ApiCollection.Name)]
public sealed class StaffTests(ApiFixture api)
{
    [Fact]
    public async Task InvitedPerson_JoinsWithTheInvitedRole()
    {
        var store = await api.CreateStoreAsync();
        var (account, staffToken, _) = await api.AddStaffAsync(store, Roles.Manager);

        var token = new JsonWebToken(staffToken);

        Assert.Equal(store.TenantId.ToString(), token.GetClaim("tid").Value);
        Assert.Contains(token.Claims, claim => claim.Type == "role" && claim.Value == Roles.Manager);

        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        var staff = await owner.GetFromJsonAsync<JsonElement[]>("/api/v1/admin/staff");

        Assert.Contains(staff!, member => member.GetProperty("email").GetString() == account.Email);
    }

    [Theory]
    [InlineData(Roles.StoreOwner)]
    [InlineData(Roles.PlatformAdmin)]
    [InlineData(Roles.Customer)]
    [InlineData("Nonsense")]
    public async Task OnlyStaffRoles_CanBeInvited(string role)
    {
        var store = await api.CreateStoreAsync();
        using var client = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = "x@example.test", role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invitation_WorksOnlyForTheInvitedMailbox()
    {
        var store = await api.CreateStoreAsync();
        var invited = await api.CreateConfirmedAccountAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = invited.Email, role = Roles.Staff });

        var stranger = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(stranger.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/tenants/invitations/accept", new { token = api.ReadInvitationToken(invited.Email) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Invitation_WorksOnlyOnce_AndIsStoredHashed()
    {
        var store = await api.CreateStoreAsync();
        var invited = await api.CreateConfirmedAccountAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);
        await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = invited.Email, role = Roles.Staff });

        var rawToken = api.ReadInvitationToken(invited.Email);

        Assert.Equal(0L, await api.QueryAsOwnerAsync<long>("SELECT count(*) FROM staff_invitations WHERE token_hash = @raw", ("raw", rawToken)));

        var signedIn = await api.LoginAsync(invited);
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/tenants/invitations/accept", new { token = rawToken })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/tenants/invitations/accept", new { token = rawToken })).StatusCode);
    }

    [Fact]
    public async Task ExpiredOrRevokedInvitation_IsRefused()
    {
        var store = await api.CreateStoreAsync();
        var expired = await api.CreateConfirmedAccountAsync();
        var revoked = await api.CreateConfirmedAccountAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = expired.Email, role = Roles.Staff });
        await api.ExecuteAsOwnerAsync("UPDATE staff_invitations SET expires_at = now() - interval '1 minute' WHERE tenant_id = @id", ("id", store.TenantId));

        var invitation = await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = revoked.Email, role = Roles.Staff });
        var invitationId = (await invitation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/admin/staff/invitations/{invitationId}")).StatusCode);

        foreach (var account in new[] { expired, revoked })
        {
            var signedIn = await api.LoginAsync(account);
            using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/tenants/invitations/accept", new { token = api.ReadInvitationToken(account.Email) })).StatusCode);
        }
    }

    [Fact]
    public async Task ForgedInvitationToken_IsRefused()
    {
        var store = await api.CreateStoreAsync();
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        foreach (var token in new[] { "garbage", $"{store.TenantId:N}.forged-random-part", "not-a-guid.part" })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/tenants/invitations/accept", new { token })).StatusCode);
        }
    }

    [Fact]
    public async Task StaffLimit_CountsPendingInvitations()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        // The free plan allows 3, and the owner already counts as one.
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = "a@example.test", role = Roles.Staff })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = "b@example.test", role = Roles.Staff })).StatusCode);

        var overLimit = await owner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = "c@example.test", role = Roles.Staff });

        Assert.Equal(HttpStatusCode.Conflict, overLimit.StatusCode);
    }

    [Fact]
    public async Task ChangingARole_TakesEffectAtOnce()
    {
        var store = await api.CreateStoreAsync();
        var (account, staffToken, refreshToken) = await api.AddStaffAsync(store, Roles.Manager);
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.PutAsJsonAsync($"/api/v1/admin/staff/{account.UserId}/role", new { role = Roles.Staff })).StatusCode);

        using var staff = api.CreateStoreClient(store.Slug, staffToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/v1/account/me")).StatusCode);

        using var anonymous = api.CreateClient();
        var refreshed = await SignedIn.FromResponseAsync(await anonymous.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", refreshToken)));
        var token = new JsonWebToken(refreshed.AccessToken);

        Assert.Contains(token.Claims, claim => claim.Type == "role" && claim.Value == Roles.Staff);
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "role" && claim.Value == Roles.Manager);
    }

    [Fact]
    public async Task RemovingStaff_EndsTheirAccessAtOnce()
    {
        var store = await api.CreateStoreAsync();
        var (account, staffToken, refreshToken) = await api.AddStaffAsync(store, Roles.Manager);
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/api/v1/admin/staff/{account.UserId}")).StatusCode);

        using var staff = api.CreateStoreClient(store.Slug, staffToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staff.GetAsync("/api/v1/account/me")).StatusCode);

        using var anonymous = api.CreateClient();
        var refreshed = await SignedIn.FromResponseAsync(await anonymous.SendAsync(ApiFixture.CookieRequest("/api/v1/auth/refresh", refreshToken)));
        var token = new JsonWebToken(refreshed.AccessToken);

        Assert.False(token.TryGetClaim("tid", out _));
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "permission");
    }

    [Fact]
    public async Task OwnerAndSelf_AreProtected()
    {
        var store = await api.CreateStoreAsync();
        var (_, managerToken, _) = await api.AddStaffAsync(store, Roles.Manager);
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        // The manager lacks staff.manage entirely, so give the check its own assertion.
        using var manager = api.CreateStoreClient(store.Slug, managerToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.DeleteAsync($"/api/v1/admin/staff/{store.Owner.UserId}")).StatusCode);

        var selfDelete = await owner.DeleteAsync($"/api/v1/admin/staff/{store.Owner.UserId}");
        var selfRole = await owner.PutAsJsonAsync($"/api/v1/admin/staff/{store.Owner.UserId}/role", new { role = Roles.Staff });

        Assert.Equal(HttpStatusCode.Forbidden, selfDelete.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, selfRole.StatusCode);
    }

    [Fact]
    public async Task SwitchingToAStoreYouDoNotBelongTo_IsRefused()
    {
        var store = await api.CreateStoreAsync();
        var signedIn = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateAuthorizedClient(signedIn.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/account/tenant", new { tenantId = store.TenantId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SwitchingStores_InvalidatesTheOldToken_AndListsMemberships()
    {
        var store = await api.CreateStoreAsync();
        using var ownerClient = api.CreateAuthorizedClient(store.AccessToken);

        var second = await ownerClient.PostAsJsonAsync("/api/v1/tenants", new { name = "Second", slug = StoreHelpers.NewSlug() });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var body = await second.Content.ReadFromJsonAsync<JsonElement>();
        var secondToken = body.GetProperty("accessToken").GetString()!;

        Assert.Equal(HttpStatusCode.Unauthorized, (await ownerClient.GetAsync("/api/v1/account/me")).StatusCode);

        using var client = api.CreateAuthorizedClient(secondToken);
        var memberships = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/account/tenants");

        Assert.Equal(2, memberships!.Length);
    }
}

[Collection(ApiCollection.Name)]
public sealed class TenantIsolationTests(ApiFixture api)
{
    [Fact]
    public async Task StaffListing_ShowsOnlyThisStoresPeople()
    {
        var first = await api.CreateStoreAsync();
        var second = await api.CreateStoreAsync();
        using var client = api.CreateStoreClient(first.Slug, first.AccessToken);

        var staff = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/admin/staff");

        Assert.Contains(staff!, member => member.GetProperty("email").GetString() == first.Owner.Email);
        Assert.DoesNotContain(staff!, member => member.GetProperty("email").GetString() == second.Owner.Email);
    }

    [Fact]
    public async Task AnotherStoresPeopleAndInvitations_LookLikeTheyDoNotExist()
    {
        var first = await api.CreateStoreAsync();
        var second = await api.CreateStoreAsync();
        var (victim, _, _) = await api.AddStaffAsync(second, Roles.Staff);

        using var secondOwner = api.CreateStoreClient(second.Slug, second.AccessToken);
        var invitation = await secondOwner.PostAsJsonAsync("/api/v1/admin/staff/invitations", new { email = "pending@example.test", role = Roles.Staff });
        var invitationId = (await invitation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var attacker = api.CreateStoreClient(first.Slug, first.AccessToken);

        Assert.Equal(HttpStatusCode.NotFound, (await attacker.DeleteAsync($"/api/v1/admin/staff/{victim.UserId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.PutAsJsonAsync($"/api/v1/admin/staff/{victim.UserId}/role", new { role = Roles.Manager })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.DeleteAsync($"/api/v1/admin/staff/invitations/{invitationId}")).StatusCode);

        Assert.Equal(1L, await api.QueryAsOwnerAsync<long>(
            "SELECT count(*) FROM tenant_members WHERE user_id = @user AND tenant_id = @tenant AND role = 'Staff'",
            ("user", victim.UserId),
            ("tenant", second.TenantId)));
    }

    [Fact]
    public async Task EveryTableWithATenantColumn_HasForcedRowLevelSecurity()
    {
        await using var connection = api.OpenApplicationConnection();
        await using var command = new NpgsqlCommand(
            """
            SELECT c.relname, c.relrowsecurity, c.relforcerowsecurity,
                   EXISTS (SELECT 1 FROM pg_policy p WHERE p.polrelid = c.oid)
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            JOIN pg_attribute a ON a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped
            WHERE n.nspname = 'public' AND c.relkind = 'r' AND c.relname <> 'outbox_messages'
            """,
            connection);

        await using var reader = await command.ExecuteReaderAsync();
        var tables = 0;

        while (await reader.ReadAsync())
        {
            tables++;
            var name = reader.GetString(0);

            Assert.True(reader.GetBoolean(1), $"{name}: row-level security is off.");
            Assert.True(reader.GetBoolean(2), $"{name}: row-level security is not forced.");
            Assert.True(reader.GetBoolean(3), $"{name}: no policy.");
        }

        Assert.True(tables >= 2);
    }

    [Fact]
    public async Task ApplicationRole_WithoutATenant_SeesNoTenantRows()
    {
        await api.CreateStoreAsync();
        await using var connection = api.OpenApplicationConnection();

        Assert.Equal(0L, await CountAsync(connection, "SELECT count(*) FROM tenant_members"));
    }

    [Fact]
    public async Task ApplicationRole_SeesOnlyTheCurrentTenant_EvenWithoutAWhereClause()
    {
        var first = await api.CreateStoreAsync();
        await api.CreateStoreAsync();
        await using var connection = api.OpenApplicationConnection();

        await SetTenantAsync(connection, first.TenantId);

        Assert.Equal(1L, await CountAsync(connection, "SELECT count(*) FROM tenant_members"));
        Assert.Equal(1L, await CountAsync(connection, $"SELECT count(*) FROM tenant_members WHERE tenant_id = '{first.TenantId}'"));
    }

    [Fact]
    public async Task ApplicationRole_CannotWriteARowForAnotherTenant()
    {
        var first = await api.CreateStoreAsync();
        var second = await api.CreateStoreAsync();
        await using var connection = api.OpenApplicationConnection();

        await SetTenantAsync(connection, first.TenantId);

        await using var insert = new NpgsqlCommand(
            $"""
            INSERT INTO tenant_members (id, tenant_id, user_id, role, created_at)
            VALUES (gen_random_uuid(), '{second.TenantId}', '{first.Owner.UserId}', 'StoreOwner', now())
            """,
            connection);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());

        Assert.Equal("42501", exception.SqlState);
    }

    [Fact]
    public async Task ApplicationRole_CannotMoveARowToAnotherTenant()
    {
        var first = await api.CreateStoreAsync();
        var second = await api.CreateStoreAsync();
        await using var connection = api.OpenApplicationConnection();

        await SetTenantAsync(connection, first.TenantId);

        await using var update = new NpgsqlCommand($"UPDATE tenant_members SET tenant_id = '{second.TenantId}'", connection);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());

        Assert.Equal("42501", exception.SqlState);
    }

    [Fact]
    public async Task PooledConnections_DoNotKeepThePreviousTenant()
    {
        var first = await api.CreateStoreAsync();
        var second = await api.CreateStoreAsync();

        await using var scope = api.Factory.Services.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<TenantContext>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        tenantContext.Use(first.TenantId);
        Assert.Equal([first.TenantId], await db.TenantMembers.Select(member => member.TenantId).Distinct().ToListAsync());

        await using var other = api.Factory.Services.CreateAsyncScope();
        other.ServiceProvider.GetRequiredService<TenantContext>().Use(second.TenantId);
        var otherDb = other.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal([second.TenantId], await otherDb.TenantMembers.Select(member => member.TenantId).Distinct().ToListAsync());

        await using var none = api.Factory.Services.CreateAsyncScope();
        var noTenantDb = none.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Empty(await noTenantDb.TenantMembers.ToListAsync());
    }

    [Fact]
    public async Task EvenWithFiltersIgnored_RowLevelSecurityStillHolds()
    {
        var first = await api.CreateStoreAsync();
        await api.CreateStoreAsync();

        await using var scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(first.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var tenants = await db.TenantMembers.IgnoreQueryFilters([QueryFilters.Tenant]).Select(member => member.TenantId).Distinct().ToListAsync();

        Assert.Equal([first.TenantId], tenants);
    }

    [Fact]
    public void EveryEntity_HasItsConventionsApplied()
    {
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var entity in db.Model.GetEntityTypes().Where(type => type.BaseType is null))
        {
            var clrType = entity.ClrType;
            var filters = entity.GetDeclaredQueryFilters().Select(filter => filter.Key).ToList();

            if (typeof(ITenantOwned).IsAssignableFrom(clrType))
            {
                Assert.Contains(QueryFilters.Tenant, filters);
            }

            if (typeof(ISoftDeletable).IsAssignableFrom(clrType))
            {
                Assert.Contains(QueryFilters.SoftDelete, filters);
            }

            if (typeof(IVersioned).IsAssignableFrom(clrType))
            {
                Assert.True(entity.FindProperty(nameof(IVersioned.Version))!.IsConcurrencyToken, clrType.Name);
            }
        }
    }

    private static async Task SetTenantAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using var command = new NpgsqlCommand($"SELECT set_config('app.tenant_id', '{tenantId}', false)", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);

        return (long)(await command.ExecuteScalarAsync())!;
    }
}
