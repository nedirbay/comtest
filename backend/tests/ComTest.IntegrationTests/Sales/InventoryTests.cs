using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.IntegrationTests.Catalog;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;
using Npgsql;

namespace ComTest.IntegrationTests.Sales;

[Collection(ApiCollection.Name)]
public sealed class InventoryTests(ApiFixture api)
{
    [Fact]
    public async Task Receipt_AddsStock_AndRecordsWhoDidIt()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 7);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        Assert.Equal((7, 0), await api.StockOfAsync(product.VariantId));

        var movements = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/inventory/movements?variantId={product.VariantId}");
        var movement = Assert.Single(movements.GetProperty("items").EnumerateArray());

        Assert.Equal("receipt", movement.GetProperty("type").GetString());
        Assert.Equal(7, movement.GetProperty("onHandDelta").GetInt32());
        Assert.Equal(7, movement.GetProperty("onHandAfter").GetInt32());
        Assert.Equal(store.Store.Owner.UserId, movement.GetProperty("by").GetGuid());
    }

    [Fact]
    public async Task Adjustment_CannotTakeStockBelowZero_OrBelowWhatIsPromised()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 5);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        Assert.Equal(ErrorCodes.Inventory.InsufficientStock, await (await AdjustAsync(admin, store, product, -6)).ReadCode());

        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId, 3);
        await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));

        Assert.Equal((5, 3), await api.StockOfAsync(product.VariantId));
        Assert.Equal(HttpStatusCode.Conflict, (await AdjustAsync(admin, store, product, -3)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AdjustAsync(admin, store, product, -2)).StatusCode);
        Assert.Equal((3, 3), await api.StockOfAsync(product.VariantId));
    }

    [Theory]
    [InlineData("sale", 1)]
    [InlineData("reservation", 1)]
    [InlineData("receipt", -1)]
    [InlineData("adjustment", 0)]
    public async Task StaffCannotFakeSalesOrMakeBadAdjustments(string type, int delta)
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 5);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/inventory/stock/adjustments", new
        {
            warehouseId = store.WarehouseId,
            variantId = product.VariantId,
            delta,
            type,
            reason = "x",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdjustmentNeedsAReason()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 5);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await admin.PostAsJsonAsync("/api/v1/admin/inventory/stock/adjustments", new
        {
            warehouseId = store.WarehouseId,
            variantId = product.VariantId,
            delta = 1,
            type = "adjustment",
            reason = string.Empty,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ImpossibleQuantities_AreRefusedByTheDatabaseItself()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 5);

        await using var connection = api.OpenApplicationConnection();
        await using var setTenant = new NpgsqlCommand($"SELECT set_config('app.tenant_id', '{store.Store.TenantId}', false)", connection);
        await setTenant.ExecuteNonQueryAsync();

        foreach (var statement in new[] { "reserved = on_hand + 1", "on_hand = -1", "reserved = -1" })
        {
            await using var command = new NpgsqlCommand($"UPDATE stock_items SET {statement} WHERE product_variant_id = '{product.VariantId}'", connection);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("23514", exception.SqlState);
        }
    }

    [Fact]
    public async Task MovementHistory_CannotBeRewritten()
    {
        await using var connection = api.OpenApplicationConnection();

        foreach (var table in new[] { "stock_movements", "order_status_changes", "payment_events" })
        {
            await using var command = new NpgsqlCommand($"DELETE FROM {table}", connection);

            var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("42501", exception.SqlState);
        }
    }

    [Fact]
    public async Task LowStock_IsListed_AndRaisesOneAlertPerDip()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 10);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var stock = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/inventory/stock?variantId={product.VariantId}");
        var stockId = stock.GetProperty("items")[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/admin/inventory/stock/{stockId}/threshold", new { threshold = 5 })).StatusCode);
        Assert.Equal(0, (await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/inventory/stock?lowOnly=true")).GetProperty("totalCount").GetInt32());

        await AdjustAsync(admin, store, product, -6);
        await AdjustAsync(admin, store, product, -1);

        var low = await admin.GetFromJsonAsync<JsonElement>("/api/v1/admin/inventory/stock?lowOnly=true");

        Assert.Equal(1, low.GetProperty("totalCount").GetInt32());
        Assert.True(low.GetProperty("items")[0].GetProperty("isLow").GetBoolean());
        Assert.Equal(1L, await CountAlertsAsync(stockId));

        await AdjustAsync(admin, store, product, 50);
        await AdjustAsync(admin, store, product, -52);

        Assert.Equal(2L, await CountAlertsAsync(stockId));
    }

    [Fact]
    public async Task WarehouseWithStock_CannotBeDeleted()
    {
        var store = await api.CreateSellingStoreAsync();
        await api.CreateSellableProductAsync(store, stock: 1);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await admin.DeleteAsync($"/api/v1/admin/inventory/warehouses/{store.WarehouseId}");

        Assert.Equal(ErrorCodes.Inventory.WarehouseNotEmpty, await response.ReadCode());
    }

    [Fact]
    public async Task OnlyOneWarehouse_IsTheDefault()
    {
        var store = await api.CreateSellingStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/inventory/warehouses", new { name = "Täze", code = "NEW", isDefault = true }));

        var warehouses = await admin.GetFromJsonAsync<JsonElement[]>("/api/v1/admin/inventory/warehouses");

        Assert.Equal("NEW", Assert.Single(warehouses!, item => item.GetProperty("isDefault").GetBoolean()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reservation_UsesAllWarehouses_DefaultFirst()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 2);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);

        var second = await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/inventory/warehouses", new { name = "Ikinji", code = "SECOND" }));
        await SalesHelpers.ReadAsync(await admin.PostAsJsonAsync("/api/v1/admin/inventory/stock/adjustments", new
        {
            warehouseId = second.GetProperty("id").GetGuid(),
            variantId = product.VariantId,
            delta = 3,
            type = "receipt",
            reason = "x",
        }));

        using var shopper = new Shopper(api, store.Slug);
        await shopper.AddAsync(product.VariantId, 4);
        await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store));

        Assert.Equal((5, 4), await api.StockOfAsync(product.VariantId));
        Assert.Equal(2, await api.QueryAsOwnerAsync<int>(
            "SELECT reserved FROM stock_items WHERE product_variant_id = @variant AND warehouse_id = @warehouse",
            ("variant", product.VariantId),
            ("warehouse", store.WarehouseId)));
    }

    [Fact]
    public async Task Permissions_AndIsolation_AreEnforced()
    {
        var store = await api.CreateSellingStoreAsync();
        var other = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 5);
        var customer = await api.LoginAsync(await api.CreateConfirmedAccountAsync());

        using var shopper = api.CreateStoreClient(store.Slug, customer.AccessToken);
        using var anonymous = api.CreateStoreClient(store.Slug);
        using var attacker = api.CreateStoreClient(other.Slug, other.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, (await shopper.GetAsync("/api/v1/admin/inventory/stock")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/admin/inventory/stock")).StatusCode);

        var foreignVariant = await attacker.PostAsJsonAsync("/api/v1/admin/inventory/stock/adjustments", new
        {
            warehouseId = other.WarehouseId,
            variantId = product.VariantId,
            delta = 1,
            type = "receipt",
            reason = "x",
        });

        var foreignWarehouse = await attacker.PostAsJsonAsync("/api/v1/admin/inventory/stock/adjustments", new
        {
            warehouseId = store.WarehouseId,
            variantId = product.VariantId,
            delta = 1,
            type = "receipt",
            reason = "x",
        });

        Assert.Equal(HttpStatusCode.NotFound, foreignVariant.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreignWarehouse.StatusCode);
        Assert.Equal(0, (await attacker.GetFromJsonAsync<JsonElement>("/api/v1/admin/inventory/stock")).GetProperty("totalCount").GetInt32());
        Assert.Equal((5, 0), await api.StockOfAsync(product.VariantId));
    }

    [Fact]
    public async Task Storefront_ShowsOnlyWhetherItCanBeBought_NotHowManyAreLeft()
    {
        var store = await api.CreateSellingStoreAsync();
        var inStock = await api.CreateSellableProductAsync(store, stock: 1234);
        var soldOut = await api.CreateSellableProductAsync(store, stock: 0);
        using var visitor = api.CreateStoreClient(store.Slug);

        var detail = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{inStock.Slug}");
        var empty = await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{soldOut.Slug}");
        var listing = await (await visitor.GetAsync("/api/v1/catalog/products")).Content.ReadAsStringAsync();

        Assert.True(detail.GetProperty("variants")[0].GetProperty("inStock").GetBoolean());
        Assert.False(empty.GetProperty("variants")[0].GetProperty("inStock").GetBoolean());

        // Numbers only: a slug is random hex, and now and then it contains these digits.
        Assert.False(HasNumber(detail, 1234));
        Assert.False(HasNumber(JsonDocument.Parse(listing).RootElement, 1234));
        Assert.DoesNotContain("onHand", detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasNumber(JsonElement element, decimal number) => element.ValueKind switch
    {
        JsonValueKind.Number => element.GetDecimal() == number,
        JsonValueKind.Object => element.EnumerateObject().Any(property => HasNumber(property.Value, number)),
        JsonValueKind.Array => element.EnumerateArray().Any(item => HasNumber(item, number)),
        _ => false,
    };

    [Fact]
    public async Task StockFlag_IsFresh_EvenWhenTheProductIsCached()
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: 1);
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        using var visitor = api.CreateStoreClient(store.Slug);

        Assert.True((await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{product.Slug}")).GetProperty("variants")[0].GetProperty("inStock").GetBoolean());

        await AdjustAsync(admin, store, product, -1);

        Assert.False((await visitor.GetFromJsonAsync<JsonElement>($"/api/v1/catalog/products/{product.Slug}")).GetProperty("variants")[0].GetProperty("inStock").GetBoolean());
    }

    [Fact]
    public async Task SimultaneousOrders_NeverSellMoreThanThereIs()
    {
        const int Stock = 5;
        const int Shoppers = 25;

        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, stock: Stock);

        var shoppers = Enumerable.Range(0, Shoppers).Select(_ => new Shopper(api, store.Slug)).ToList();

        try
        {
            foreach (var shopper in shoppers)
            {
                await SalesHelpers.ReadAsync(await shopper.AddAsync(product.VariantId));
            }

            var responses = await Task.WhenAll(shoppers.Select((shopper, index) =>
                shopper.PlaceCourierOrderAsync(store, phone: "+9936500" + (10000 + index).ToString(System.Globalization.CultureInfo.InvariantCulture))));

            Assert.Equal(Stock, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
            Assert.All(
                responses.Where(response => response.StatusCode != HttpStatusCode.OK),
                response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));

            Assert.Equal((Stock, Stock), await api.StockOfAsync(product.VariantId));
            Assert.Equal((long)Stock, await api.QueryAsOwnerAsync<long>("SELECT count(*) FROM orders WHERE tenant_id = @id", ("id", store.Store.TenantId)));
        }
        finally
        {
            shoppers.ForEach(shopper => shopper.Dispose());
        }
    }

    private static Task<HttpResponseMessage> AdjustAsync(HttpClient admin, SellingStore store, SellableProduct product, int delta) =>
        admin.PostAsJsonAsync("/api/v1/admin/inventory/stock/adjustments", new
        {
            warehouseId = store.WarehouseId,
            variantId = product.VariantId,
            delta,
            type = "adjustment",
            reason = "test",
        });

    private Task<long> CountAlertsAsync(Guid stockItemId) =>
        api.QueryAsOwnerAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE type LIKE '%LowStockDetected' AND payload->>'StockItemId' = @id",
            ("id", stockItemId.ToString()));
}
