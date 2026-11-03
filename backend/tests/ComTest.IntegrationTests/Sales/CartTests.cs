using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ComTest.IntegrationTests.Catalog;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;

namespace ComTest.IntegrationTests.Sales;

[Collection(ApiCollection.Name)]
public sealed class CartTests(ApiFixture api)
{
    [Fact]
    public async Task Guest_GetsACart_WithAProtectedCookie_AndOnlyItsHashIsStored()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m);
        using var shopper = new Shopper(api, store.Slug);

        var cart = await SalesHelpers.ReadAsync(await shopper.AddAsync(product.VariantId, 2));

        Assert.Equal(200m, cart.GetProperty("subtotal").GetDecimal());
        Assert.Equal(2, cart.GetProperty("itemCount").GetInt32());

        var cookie = shopper.LastSetCookie!.ToLowerInvariant();

        Assert.Contains("httponly", cookie, StringComparison.Ordinal);
        Assert.Contains("secure", cookie, StringComparison.Ordinal);
        Assert.Contains("samesite=lax", cookie, StringComparison.Ordinal);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(shopper.GuestCookie!)));

        Assert.Equal(0L, await api.QueryAsOwnerAsync<long>("SELECT count(*) FROM carts WHERE guest_token_hash = @raw", ("raw", shopper.GuestCookie!)));
        Assert.Equal(1L, await api.QueryAsOwnerAsync<long>("SELECT count(*) FROM carts WHERE guest_token_hash = @hash", ("hash", hash)));
    }

    [Fact]
    public async Task Cart_CanBeChangedAndEmptied()
    {
        var store = await api.CreateSellingStoreAsync();
        var first = await api.CreateSellableProductAsync(store, price: 100m);
        var second = await api.CreateSellableProductAsync(store, price: 50m);
        using var shopper = new Shopper(api, store.Slug);

        await shopper.AddAsync(first.VariantId);
        await shopper.AddAsync(second.VariantId, 2);
        Assert.Equal(200m, (await SalesHelpers.ReadAsync(await shopper.GetCartAsync())).GetProperty("subtotal").GetDecimal());

        var changed = await SalesHelpers.ReadAsync(await shopper.SendAsync(HttpMethod.Put, $"/api/v1/cart/items/{first.VariantId}", new { quantity = 3 }));
        Assert.Equal(400m, changed.GetProperty("subtotal").GetDecimal());

        var removed = await SalesHelpers.ReadAsync(await shopper.SendAsync(HttpMethod.Delete, $"/api/v1/cart/items/{second.VariantId}"));
        Assert.Equal(300m, removed.GetProperty("subtotal").GetDecimal());

        var cleared = await SalesHelpers.ReadAsync(await shopper.SendAsync(HttpMethod.Delete, "/api/v1/cart"));
        Assert.Equal(0, cleared.GetProperty("itemCount").GetInt32());

        var missing = await shopper.SendAsync(HttpMethod.Delete, $"/api/v1/cart/items/{first.VariantId}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    [InlineData(int.MaxValue)]
    public async Task BadQuantities_AreRefused(int quantity)
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        using var shopper = new Shopper(api, store.Slug);

        Assert.Equal(HttpStatusCode.BadRequest, (await shopper.AddAsync(product.VariantId, quantity)).StatusCode);

        await shopper.AddAsync(product.VariantId);

        Assert.Equal(HttpStatusCode.BadRequest, (await shopper.SendAsync(HttpMethod.Put, $"/api/v1/cart/items/{product.VariantId}", new { quantity })).StatusCode);
    }

    [Fact]
    public async Task AClientCannotChooseItsOwnPrice()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m);
        using var shopper = new Shopper(api, store.Slug);

        var response = await shopper.SendAsync(HttpMethod.Post, "/api/v1/cart/items", new { variantId = product.VariantId, quantity = 1, price = 0.01m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Mutations_RequireTheCsrfHeader()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        using var client = api.CreateStoreClient(store.Slug);

        var response = await client.PostAsJsonAsync("/api/v1/cart/items", new { variantId = product.VariantId, quantity = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.Auth.CsrfRejected, await response.ReadCode());
    }

    [Fact]
    public async Task UnpublishedOrUnknownVariants_CannotBeAdded()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var shopper = new Shopper(api, store.Slug);

        await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{product.ProductId}/status", new { status = "draft" });

        Assert.Equal(ErrorCodes.Cart.ItemUnavailable, await (await shopper.AddAsync(product.VariantId)).ReadCode());
        Assert.Equal(HttpStatusCode.NotFound, (await shopper.AddAsync(Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task ItemsInAnotherCurrency_CannotBeAdded()
    {
        var store = await api.CreateSellingStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(new
        {
            slug,
            translations = new[] { new { language = "tk", name = "Dollarly" } },
            variants = new[] { new { sku = "USD-" + slug, price = 10m, currency = "USD" } },
        });

        await admin.PublishAsync(created.GetProperty("id").GetGuid());

        using var shopper = new Shopper(api, store.Slug);

        Assert.Equal(ErrorCodes.Cart.CurrencyMismatch, await (await shopper.AddAsync(created.GetProperty("variants")[0].GetProperty("id").GetGuid())).ReadCode());
    }

    [Fact]
    public async Task Guests_CannotSeeEachOthersCarts_AndACookieMeansNothingAtAnotherStore()
    {
        var store = await api.CreateSellingStoreAsync();
        var other = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store);

        using var first = new Shopper(api, store.Slug);
        await first.AddAsync(product.VariantId);

        using var second = new Shopper(api, store.Slug);
        Assert.Equal(0, (await SalesHelpers.ReadAsync(await second.GetCartAsync())).GetProperty("itemCount").GetInt32());

        using var forged = new Shopper(api, store.Slug) { GuestCookie = "not-a-real-token" };
        Assert.Equal(0, (await SalesHelpers.ReadAsync(await forged.GetCartAsync())).GetProperty("itemCount").GetInt32());

        using var elsewhere = new Shopper(api, other.Slug) { GuestCookie = first.GuestCookie };
        Assert.Equal(0, (await SalesHelpers.ReadAsync(await elsewhere.GetCartAsync())).GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task AnotherStoresVariant_CannotBeAdded()
    {
        var store = await api.CreateSellingStoreAsync();
        var other = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(other);
        using var shopper = new Shopper(api, store.Slug);

        Assert.Equal(HttpStatusCode.NotFound, (await shopper.AddAsync(product.VariantId)).StatusCode);
    }

    [Fact]
    public async Task PriceChanges_AndMissingStock_AreFlagged()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m, stock: 2);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var shopper = new Shopper(api, store.Slug);

        await shopper.AddAsync(product.VariantId, 3);

        var before = (await SalesHelpers.ReadAsync(await shopper.GetCartAsync()));
        Assert.True(before.GetProperty("lines")[0].GetProperty("insufficientStock").GetBoolean());
        Assert.True(before.GetProperty("hasProblems").GetBoolean());
        Assert.Equal(JsonValueKind.Null, before.GetProperty("lines")[0].GetProperty("previousUnitPrice").ValueKind);

        await admin.PutAsJsonAsync(
            $"/api/v1/admin/catalog/products/{product.ProductId}",
            CatalogRequests.Product(product.Slug, "SKU-" + product.Slug, price: 150m, variantId: product.VariantId));

        var line = (await SalesHelpers.ReadAsync(await shopper.GetCartAsync())).GetProperty("lines")[0];

        Assert.Equal(150m, line.GetProperty("unitPrice").GetDecimal());
        Assert.Equal(100m, line.GetProperty("previousUnitPrice").GetDecimal());
        Assert.Equal(450m, line.GetProperty("lineTotal").GetDecimal());
    }

    [Fact]
    public async Task ArchivedProducts_StayInTheCart_ButDoNotCount()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: 100m);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var shopper = new Shopper(api, store.Slug);

        await shopper.AddAsync(product.VariantId);
        await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{product.ProductId}/status", new { status = "archived" });

        var cart = await SalesHelpers.ReadAsync(await shopper.GetCartAsync());

        Assert.False(cart.GetProperty("lines")[0].GetProperty("isAvailable").GetBoolean());
        Assert.Equal(0m, cart.GetProperty("subtotal").GetDecimal());
        Assert.True(cart.GetProperty("hasProblems").GetBoolean());
    }

    [Fact]
    public async Task SigningIn_MergesTheGuestCart_AndRemovesTheCookie()
    {
        var store = await api.CreateSellingStoreAsync();
        var shared = await api.CreateSellableProductAsync(store, price: 100m);
        var guestOnly = await api.CreateSellableProductAsync(store, price: 10m);
        var account = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var signedIn = new Shopper(api, store.Slug, account.AccessToken);
        await SalesHelpers.ReadAsync(await signedIn.AddAsync(shared.VariantId, 1));

        using var browser = new Shopper(api, store.Slug);
        await browser.AddAsync(shared.VariantId, 2);
        await browser.AddAsync(guestOnly.VariantId, 1);
        var guestCookie = browser.GuestCookie;

        using var merged = new Shopper(api, store.Slug, account.AccessToken) { GuestCookie = guestCookie };
        var cart = await SalesHelpers.ReadAsync(await merged.GetCartAsync());

        Assert.Equal(4, cart.GetProperty("itemCount").GetInt32());
        Assert.Equal(310m, cart.GetProperty("subtotal").GetDecimal());
        Assert.Null(merged.GuestCookie);

        // The guest cart is gone, so its cookie cannot be merged a second time.
        using var again = new Shopper(api, store.Slug, account.AccessToken) { GuestCookie = guestCookie };
        Assert.Equal(4, (await SalesHelpers.ReadAsync(await again.GetCartAsync())).GetProperty("itemCount").GetInt32());
    }

    [Fact]
    public async Task CartOutsideAStore_IsRefused()
    {
        using var client = api.CreatePlatformClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/cart")).StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public sealed class AddressBookTests(ApiFixture api)
{
    [Fact]
    public async Task Addresses_CanBeManaged_AndTheFirstBecomesDefault()
    {
        var store = await api.CreateSellingStoreAsync();
        var account = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateStoreClient(store.Slug, account.AccessToken);

        var first = await SalesHelpers.ReadAsync(await client.PostAsJsonAsync("/api/v1/account/addresses", new { address = SalesHelpers.Address(), label = "Öý" }));
        var second = await SalesHelpers.ReadAsync(await client.PostAsJsonAsync("/api/v1/account/addresses", new { address = SalesHelpers.Address(), isDefault = true }));

        Assert.True(first.GetProperty("isDefault").GetBoolean());

        var list = await client.GetFromJsonAsync<JsonElement[]>("/api/v1/account/addresses");

        Assert.Equal(2, list!.Length);
        Assert.Equal(second.GetProperty("id").GetGuid(), Assert.Single(list, item => item.GetProperty("isDefault").GetBoolean()).GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/v1/account/addresses/{first.GetProperty("id").GetGuid()}")).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<JsonElement[]>("/api/v1/account/addresses"))!);
    }

    [Fact]
    public async Task AnotherPersonsAddress_LooksLikeItDoesNotExist()
    {
        var store = await api.CreateSellingStoreAsync();
        var owner = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        var attacker = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var ownerClient = api.CreateStoreClient(store.Slug, owner.AccessToken);
        using var attackerClient = api.CreateStoreClient(store.Slug, attacker.AccessToken);

        var address = await SalesHelpers.ReadAsync(await ownerClient.PostAsJsonAsync("/api/v1/account/addresses", new { address = SalesHelpers.Address() }));
        var id = address.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await attackerClient.PutAsJsonAsync($"/api/v1/account/addresses/{id}", new { address = SalesHelpers.Address(region: "mary") })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attackerClient.DeleteAsync($"/api/v1/account/addresses/{id}")).StatusCode);
        Assert.Empty((await attackerClient.GetFromJsonAsync<JsonElement[]>("/api/v1/account/addresses"))!);
        Assert.Single((await ownerClient.GetFromJsonAsync<JsonElement[]>("/api/v1/account/addresses"))!);
    }

    [Fact]
    public async Task AddressesBelongToOneStore()
    {
        var store = await api.CreateSellingStoreAsync();
        var other = await api.CreateSellingStoreAsync();
        var account = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var here = api.CreateStoreClient(store.Slug, account.AccessToken);
        using var there = api.CreateStoreClient(other.Slug, account.AccessToken);

        await SalesHelpers.ReadAsync(await here.PostAsJsonAsync("/api/v1/account/addresses", new { address = SalesHelpers.Address() }));

        Assert.Empty((await there.GetFromJsonAsync<JsonElement[]>("/api/v1/account/addresses"))!);
    }

    [Theory]
    [InlineData("not-a-phone")]
    [InlineData("65111111")]
    public async Task BadPhoneNumbers_AreRefused(string phone)
    {
        var store = await api.CreateSellingStoreAsync();
        var account = await api.LoginAsync(await api.CreateConfirmedAccountAsync());
        using var client = api.CreateStoreClient(store.Slug, account.AccessToken);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/account/addresses", new { address = SalesHelpers.Address(phone: phone) })).StatusCode);
    }

    [Fact]
    public async Task AddressBook_RequiresSignIn()
    {
        var store = await api.CreateSellingStoreAsync();
        using var client = api.CreateStoreClient(store.Slug);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/account/addresses")).StatusCode);
    }
}

[Collection(ApiCollection.Name)]
public sealed class ShippingAdminTests(ApiFixture api)
{
    [Fact]
    public async Task Regions_ArePublic_AndComeFromActiveZones()
    {
        var store = await api.CreateSellingStoreAsync();
        using var visitor = api.CreateStoreClient(store.Slug);

        Assert.Equal([SalesHelpers.Region], (await visitor.GetFromJsonAsync<string[]>("/api/v1/shipping/regions"))!);
    }

    [Fact]
    public async Task CourierNeedsAZone_AndPickupNeedsAnAddress()
    {
        var store = await api.CreateSellingStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var names = new Dictionary<string, string> { ["tk"] = "X" };

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/shipping/methods", new { type = "courier", names, price = 1m, currency = "TMT" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/shipping/methods", new { type = "pickup", names, price = 0m, currency = "TMT" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/v1/admin/shipping/methods", new { type = "pickup", names, price = -1m, currency = "TMT", pickupAddress = "X" })).StatusCode);
    }

    [Fact]
    public async Task Staff_CannotChangeDelivery_AndStoresAreIsolated()
    {
        var store = await api.CreateSellingStoreAsync();
        var other = await api.CreateSellingStoreAsync();
        var (_, managerToken, _) = await api.AddStaffAsync(store.Store, "Manager");

        using var manager = api.CreateStoreClient(store.Slug, managerToken);
        using var attacker = api.CreateStoreClient(other.Slug, other.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/api/v1/admin/shipping/methods")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await attacker.DeleteAsync($"/api/v1/admin/shipping/methods/{store.CourierId}")).StatusCode);
    }
}
