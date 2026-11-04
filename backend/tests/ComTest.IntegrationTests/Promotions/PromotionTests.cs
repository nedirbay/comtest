using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.IntegrationTests.Catalog;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;

namespace ComTest.IntegrationTests.Promotions;

[Collection(ApiCollection.Name)]
public sealed class PromotionTests(ApiFixture api)
{
    private static Uri Path(string path) => new(path, UriKind.Relative);

    private static object Terms(
        string? code = "BAYRAM2026",
        string type = "percentage",
        decimal value = 10m,
        decimal? minSubtotal = null,
        Guid? categoryId = null,
        int? usageLimit = null,
        int? perCustomerLimit = null,
        bool combines = false,
        bool isActive = true,
        DateTimeOffset? startsAt = null,
        DateTimeOffset? endsAt = null) => new
        {
            code,
            name = "Baýram",
            type,
            value,
            minSubtotal,
            categoryId,
            usageLimit,
            perCustomerLimit,
            combinesWithOthers = combines,
            isActive,
            startsAt,
            endsAt,
        };

    private async Task<(SellingStore Store, SellableProduct Product, HttpClient Owner)> ArrangeAsync(decimal price = 100m)
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: price, stock: 50);

        return (store, product, api.CreateStoreClient(store.Slug, store.AccessToken));
    }

    private static async Task<JsonElement> CreateAsync(HttpClient owner, object terms) =>
        await SalesHelpers.ReadAsync(await owner.PostAsJsonAsync("/api/v1/admin/promotions", terms));

    private static Task<HttpResponseMessage> QuoteAsync(Shopper shopper, SellingStore store, string? code = null) =>
        shopper.SendAsync(
            HttpMethod.Post,
            "/api/v1/checkout/quote" + (code is null ? string.Empty : "?promoCode=" + Uri.EscapeDataString(code)),
            new { region = SalesHelpers.Region, shippingMethodId = store.CourierId });

    private static Task<HttpResponseMessage> PlaceAsync(Shopper shopper, SellingStore store, string? code, string phone = "+99365111111") =>
        shopper.CheckoutAsync(new
        {
            shippingMethodId = store.CourierId,
            paymentMethod = "cashOnDelivery",
            contact = new { name = "Aýna", phone },
            address = SalesHelpers.Address(phone: phone),
            promoCode = code,
        });

    private static JsonElement Totals(JsonElement body) => body.GetProperty("totals");

    [Fact]
    public async Task APercentageCode_LowersTheQuoteAndTheOrderAlike()
    {
        var (store, product, owner) = await ArrangeAsync(price: 200m);

        using (owner)
        {
            await CreateAsync(owner, Terms());

            using var shopper = new Shopper(api, store.Slug);

            (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

            var quote = await SalesHelpers.ReadAsync(await QuoteAsync(shopper, store, "bayram2026"));

            Assert.Equal(200m, Totals(quote).GetProperty("subtotal").GetDecimal());
            Assert.Equal(20m, Totals(quote).GetProperty("discount").GetDecimal());
            Assert.Equal(200m - 20m + SalesHelpers.CourierPrice, Totals(quote).GetProperty("total").GetDecimal());
            Assert.Equal("Baýram", quote.GetProperty("promotions")[0].GetProperty("name").GetString());

            var placed = await SalesHelpers.ReadAsync(await PlaceAsync(shopper, store, "BAYRAM2026"));
            var order = placed.GetProperty("order");

            Assert.Equal(20m, Totals(order).GetProperty("discount").GetDecimal());
            Assert.Equal(200m - 20m + SalesHelpers.CourierPrice, Totals(order).GetProperty("total").GetDecimal());

            var list = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/promotions?page=1&pageSize=10")));

            Assert.Equal(1, list.GetProperty("items")[0].GetProperty("usageCount").GetInt32());
        }
    }

    [Fact]
    public async Task AFixedAmountAndFreeShipping_CanBringATotalToZero()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            await CreateAsync(owner, Terms(code: "HEMMESI", type: "fixedAmount", value: 500m, combines: true));
            await CreateAsync(owner, Terms(code: null, type: "freeShipping", value: 0m, combines: true));

            using var shopper = new Shopper(api, store.Slug);

            (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

            var placed = await SalesHelpers.ReadAsync(await PlaceAsync(shopper, store, "HEMMESI"));
            var totals = Totals(placed.GetProperty("order"));

            Assert.Equal(100m, totals.GetProperty("discount").GetDecimal());
            Assert.Equal(0m, totals.GetProperty("shipping").GetDecimal());
            Assert.Equal(0m, totals.GetProperty("total").GetDecimal());
        }
    }

    [Fact]
    public async Task AnAutomaticPromotion_NeedsNoCode()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            await CreateAsync(owner, Terms(code: null, value: 50m));

            using var shopper = new Shopper(api, store.Slug);

            (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

            var quote = await SalesHelpers.ReadAsync(await QuoteAsync(shopper, store));

            Assert.Equal(50m, Totals(quote).GetProperty("discount").GetDecimal());
            Assert.Equal(JsonValueKind.Null, quote.GetProperty("promotions")[0].GetProperty("code").ValueKind);
        }
    }

    [Fact]
    public async Task ABetterOffer_TakesThePlaceOfACodeAndSaysSo()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            await CreateAsync(owner, Terms(code: "AZAJYK", value: 5m));
            await CreateAsync(owner, Terms(code: null, value: 30m));

            using var shopper = new Shopper(api, store.Slug);

            (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

            var quote = await SalesHelpers.ReadAsync(await QuoteAsync(shopper, store, "AZAJYK"));

            Assert.Equal(30m, Totals(quote).GetProperty("discount").GetDecimal());
            Assert.True(quote.GetProperty("promoCodeIgnored").GetBoolean());
        }
    }

    [Fact]
    public async Task ACategoryRule_CoversTheCategoryItsChildrenAndNothingElse()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            var parent = await SalesHelpers.ReadAsync(await owner.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { slug = "elektronika", sortOrder = 0, translations = new[] { new { language = "tk", name = "Elektronika" } } }));
            var child = await SalesHelpers.ReadAsync(await owner.PostAsJsonAsync("/api/v1/admin/catalog/categories", new { slug = "telefonlar", parentId = parent.GetProperty("id").GetGuid(), sortOrder = 0, translations = new[] { new { language = "tk", name = "Telefonlar" } } }));

            var inside = await owner.CreateProductAsync(CatalogRequests.Product("icinde-" + CatalogRequests.NewSlug()[..8], "I-" + CatalogRequests.NewSlug(), price: 300m, categoryId: child.GetProperty("id").GetGuid()));

            await owner.PublishAsync(inside.GetProperty("id").GetGuid());

            var insideVariant = inside.GetProperty("variants")[0].GetProperty("id").GetGuid();

            (await owner.PostAsJsonAsync("/api/v1/admin/inventory/stock/adjustments", new { warehouseId = store.WarehouseId, variantId = insideVariant, delta = 5, type = "receipt", reason = "test" })).EnsureSuccessStatusCode();

            await CreateAsync(owner, Terms(code: "ELEKTRONIKA", categoryId: parent.GetProperty("id").GetGuid()));

            using (var outsider = new Shopper(api, store.Slug))
            {
                (await outsider.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

                var refused = await QuoteAsync(outsider, store, "ELEKTRONIKA");

                Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
                Assert.Equal(ErrorCodes.Promotion.NotApplicable, await refused.ReadCode());
            }

            using var shopper = new Shopper(api, store.Slug);

            (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();
            (await shopper.AddAsync(insideVariant)).EnsureSuccessStatusCode();

            var quote = await SalesHelpers.ReadAsync(await QuoteAsync(shopper, store, "ELEKTRONIKA"));

            // Ten percent of the phone only, not of the whole cart.
            Assert.Equal(30m, Totals(quote).GetProperty("discount").GetDecimal());
        }
    }

    [Fact]
    public async Task Conditions_AreChecked()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            var now = DateTimeOffset.UtcNow;

            await CreateAsync(owner, Terms(code: "GYMMAT", minSubtotal: 1000m));
            await CreateAsync(owner, Terms(code: "GECENKI", startsAt: now.AddDays(-3), endsAt: now.AddDays(-1)));
            await CreateAsync(owner, Terms(code: "GELJEK", startsAt: now.AddDays(1)));
            await CreateAsync(owner, Terms(code: "OCURIK", isActive: false));

            using var shopper = new Shopper(api, store.Slug);

            (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

            var low = await QuoteAsync(shopper, store, "GYMMAT");

            Assert.Equal(ErrorCodes.Promotion.MinSubtotalNotMet, await low.ReadCode());

            foreach (var code in new[] { "GECENKI", "GELJEK", "OCURIK", "HIC-BOLMADYK" })
            {
                var response = await QuoteAsync(shopper, store, code);

                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal(ErrorCodes.Promotion.CodeNotValid, await response.ReadCode());
            }

            var placed = await PlaceAsync(shopper, store, "HIC-BOLMADYK");

            Assert.Equal(ErrorCodes.Promotion.CodeNotValid, await placed.ReadCode());
        }
    }

    [Fact]
    public async Task TheLastUse_GoesToExactlyOneBuyerWhenManyRace()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            await CreateAsync(owner, Terms(code: "SONKYSY", usageLimit: 1));

            var shoppers = Enumerable.Range(0, 8).Select(_ => new Shopper(api, store.Slug)).ToList();

            try
            {
                foreach (var shopper in shoppers)
                {
                    (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();
                }

                var answers = await Task.WhenAll(shoppers.Select((shopper, index) => PlaceAsync(shopper, store, "SONKYSY", phone: $"+9936500000{index}")));

                Assert.Single(answers, answer => answer.IsSuccessStatusCode);

                var list = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/promotions?page=1&pageSize=10")));

                Assert.Equal(1, list.GetProperty("items")[0].GetProperty("usageCount").GetInt32());
            }
            finally
            {
                shoppers.ForEach(shopper => shopper.Dispose());
            }
        }
    }

    [Fact]
    public async Task APerCustomerLimit_HoldsAcrossCarts()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            await CreateAsync(owner, Terms(code: "BIRGEZEK", perCustomerLimit: 1));

            using var first = new Shopper(api, store.Slug);

            (await first.AddAsync(product.VariantId)).EnsureSuccessStatusCode();
            (await PlaceAsync(first, store, "BIRGEZEK", phone: "+99365777777")).EnsureSuccessStatusCode();

            using var again = new Shopper(api, store.Slug);

            (await again.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

            var refused = await PlaceAsync(again, store, "BIRGEZEK", phone: "+99365777777");

            Assert.Equal(ErrorCodes.Promotion.CodeNotValid, await refused.ReadCode());

            (await PlaceAsync(again, store, "BIRGEZEK", phone: "+99365888888")).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task CancellingAnOrder_GivesItsUseBack()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            await CreateAsync(owner, Terms(code: "GAYTARYLYAR", usageLimit: 1, perCustomerLimit: 1));

            using var first = new Shopper(api, store.Slug);

            (await first.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

            var placed = await SalesHelpers.ReadAsync(await PlaceAsync(first, store, "GAYTARYLYAR"));
            var orderId = placed.GetProperty("order").GetProperty("id").GetGuid();

            (await owner.PutAsJsonAsync($"/api/v1/admin/orders/{orderId}/status", new { status = "cancelled" })).EnsureSuccessStatusCode();

            using var second = new Shopper(api, store.Slug);

            (await second.AddAsync(product.VariantId)).EnsureSuccessStatusCode();
            (await PlaceAsync(second, store, "GAYTARYLYAR")).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task AFailedOrder_KeepsItsUse()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            await CreateAsync(owner, Terms(code: "SAKLANYAR", usageLimit: 1));

            using var shopper = new Shopper(api, store.Slug);

            // More than is in stock, so that the order fails after the promotion was taken.
            (await shopper.AddAsync(product.VariantId, 50)).EnsureSuccessStatusCode();
            await SalesHelpers.ReadAsync(await owner.PostAsJsonAsync("/api/v1/admin/inventory/stock/adjustments", new { warehouseId = store.WarehouseId, variantId = product.VariantId, delta = -49, type = "adjustment", reason = "test" }));

            var failed = await PlaceAsync(shopper, store, "SAKLANYAR");

            Assert.False(failed.IsSuccessStatusCode);

            var list = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/promotions?page=1&pageSize=10")));

            Assert.Equal(0, list.GetProperty("items")[0].GetProperty("usageCount").GetInt32());
        }
    }

    [Fact]
    public async Task Staff_CanNotManagePromotionsAndAnotherStoreCanNotUseThem()
    {
        var (store, product, owner) = await ArrangeAsync();

        using (owner)
        {
            var created = await CreateAsync(owner, Terms(code: "OZUMIZIN"));
            var (_, token, _) = await api.AddStaffAsync(store.Store, Roles.Staff);
            using var staff = api.CreateStoreClient(store.Slug, token);
            using var visitor = api.CreateStoreClient(store.Slug);

            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(Path("/api/v1/admin/promotions"))).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync(Path("/api/v1/admin/promotions"))).StatusCode);

            var other = await api.CreateSellingStoreAsync();
            var otherProduct = await api.CreateSellableProductAsync(other);
            using var otherOwner = api.CreateStoreClient(other.Slug, other.AccessToken);

            Assert.Equal(HttpStatusCode.NotFound, (await otherOwner.GetAsync(Path($"/api/v1/admin/promotions/{created.GetProperty("id").GetGuid()}"))).StatusCode);

            using var shopper = new Shopper(api, other.Slug);

            (await shopper.AddAsync(otherProduct.VariantId)).EnsureSuccessStatusCode();

            Assert.Equal(ErrorCodes.Promotion.CodeNotValid, await (await QuoteAsync(shopper, other, "OZUMIZIN")).ReadCode());
        }
    }

    [Theory]
    [InlineData("KISA", "percentage", 10)]
    [InlineData("bad code!", "percentage", 10)]
    [InlineData("DOGRYKOD", "percentage", 0)]
    [InlineData("DOGRYKOD", "percentage", 101)]
    [InlineData("DOGRYKOD", "fixedAmount", -5)]
    public async Task BadTerms_AreRefused(string code, string type, int value)
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await owner.PostAsJsonAsync("/api/v1/admin/promotions", Terms(code: code, type: type, value: value));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ACode_CanBeUsedOnceInAStoreAndAgainOnceDeleted()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        var first = await CreateAsync(owner, Terms(code: "GAYTALANYAR"));
        var clash = await owner.PostAsJsonAsync("/api/v1/admin/promotions", Terms(code: "gaytalanyar"));

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
        Assert.Equal(ErrorCodes.Promotion.CodeTaken, await clash.ReadCode());

        (await owner.DeleteAsync(Path($"/api/v1/admin/promotions/{first.GetProperty("id").GetGuid()}"))).EnsureSuccessStatusCode();

        await CreateAsync(owner, Terms(code: "GAYTALANYAR"));
    }

    [Fact]
    public async Task NewCodes_AreDifferentEachTime()
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        var first = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/promotions/new-code")));
        var second = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/promotions/new-code")));

        Assert.NotEqual(first.GetProperty("code").GetString(), second.GetProperty("code").GetString());
    }
}
