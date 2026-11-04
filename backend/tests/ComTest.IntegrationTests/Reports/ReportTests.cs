using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Results;

namespace ComTest.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class ReportTests(ApiFixture api)
{

    private static Uri Path(string path) => new(path, UriKind.Relative);

    /// <summary>A window around now, as orders are placed at the moment the test runs.</summary>
    private static string Around()
    {
        var now = DateTimeOffset.UtcNow;

        return $"from={Uri.EscapeDataString(now.AddHours(-1).ToString("O"))}&to={Uri.EscapeDataString(now.AddHours(1).ToString("O"))}";
    }

    private async Task<(SellingStore Store, SellableProduct Product, HttpClient Owner)> ArrangeAsync(decimal price = 100m)
    {
        var store = await api.CreateSellingStoreAsync();
        var product = await api.CreateSellableProductAsync(store, price: price, stock: 50, name: "Telefon");

        return (store, product, api.CreateStoreClient(store.Slug, store.AccessToken));
    }

    private async Task<JsonElement> PlaceAsync(SellingStore store, Guid variantId, int quantity = 1, string phone = "+99365111111")
    {
        using var shopper = new Shopper(api, store.Slug);

        (await shopper.AddAsync(variantId, quantity)).EnsureSuccessStatusCode();

        var placed = await SalesHelpers.ReadAsync(await shopper.PlaceCourierOrderAsync(store, phone: phone));

        return placed.GetProperty("order");
    }

    [Fact]
    public async Task Sales_CountWhatWasSoldAndLeaveOutCancelledOrders()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            await PlaceAsync(store, product.VariantId, quantity: 2);
            await PlaceAsync(store, product.VariantId);

            var cancelled = await PlaceAsync(store, product.VariantId, quantity: 5);

            (await owner.PutAsJsonAsync($"/api/v1/admin/orders/{cancelled.GetProperty("id").GetGuid()}/status", new { status = "cancelled" })).EnsureSuccessStatusCode();

            var report = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/reports/sales?" + Around())));
            var figures = report.GetProperty("figures");

            Assert.Equal("TMT", report.GetProperty("currency").GetString());
            Assert.Equal(2, figures.GetProperty("orders").GetInt32());
            Assert.Equal(3, figures.GetProperty("units").GetInt64());

            // Goods plus delivery for each of the two orders.
            var expected = 300m + (2 * SalesHelpers.CourierPrice);

            Assert.Equal(expected, figures.GetProperty("revenue").GetDecimal());
            Assert.Equal(expected / 2, figures.GetProperty("averageOrderValue").GetDecimal());
            Assert.Equal(2 * SalesHelpers.CourierPrice, figures.GetProperty("shipping").GetDecimal());
            Assert.Equal(0, report.GetProperty("previous").GetProperty("orders").GetInt32());

            var day = report.GetProperty("days").EnumerateArray().Single(item => item.GetProperty("orders").GetInt32() > 0);

            Assert.Equal(2, day.GetProperty("orders").GetInt32());
            Assert.Equal(expected, day.GetProperty("revenue").GetDecimal());
        }
    }

    [Fact]
    public async Task Sales_ListEveryDayOfThePeriodInTheCallersOwnDay()
    {
        var (store, product, owner) = await ArrangeAsync();

        using (owner)
        {
            await PlaceAsync(store, product.VariantId);

            var now = DateTimeOffset.UtcNow;
            var from = Uri.EscapeDataString(now.AddDays(-2).ToString("O"));
            var to = Uri.EscapeDataString(now.AddDays(1).ToString("O"));
            const int offset = 14 * 60;

            var report = await SalesHelpers.ReadAsync(await owner.GetAsync(Path($"/api/v1/admin/reports/sales?from={from}&to={to}&utcOffsetMinutes={offset}")));
            var days = report.GetProperty("days").EnumerateArray().ToList();

            Assert.True(days.Count is 3 or 4);

            var sold = Assert.Single(days, day => day.GetProperty("orders").GetInt32() == 1);
            var expected = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromMinutes(offset)).DateTime);

            Assert.Equal(expected.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), sold.GetProperty("date").GetString());
        }
    }

    [Theory]
    [InlineData("from=2024-01-02T00:00:00Z&to=2024-01-01T00:00:00Z")]
    [InlineData("from=2024-01-01T00:00:00Z&to=2024-01-01T00:00:00Z")]
    [InlineData("from=2020-01-01T00:00:00Z&to=2024-01-01T00:00:00Z")]
    [InlineData("from=2024-01-01T00:00:00Z&to=2024-01-02T00:00:00Z&utcOffsetMinutes=100000")]
    public async Task Sales_RefuseAPeriodThatMakesNoSense(string query)
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await owner.GetAsync(Path("/api/v1/admin/reports/sales?" + query));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.Report.InvalidPeriod, await Catalog.CatalogRequests.ReadCode(response));
    }

    [Fact]
    public async Task Refunds_AreShownOnTheirOwn()
    {
        var (store, product, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            var order = await PlaceAsync(store, product.VariantId);
            var orderId = order.GetProperty("id").GetGuid();

            (await owner.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/confirm", new { })).EnsureSuccessStatusCode();
            (await owner.PostAsJsonAsync($"/api/v1/admin/orders/{orderId}/payment/refunds", new { amount = 30m, reason = "Zaýa" })).EnsureSuccessStatusCode();

            var report = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/reports/sales?" + Around())));

            Assert.Equal(30m, report.GetProperty("refunded").GetDecimal());
            Assert.Equal(100m + SalesHelpers.CourierPrice, report.GetProperty("figures").GetProperty("revenue").GetDecimal());
        }
    }

    [Fact]
    public async Task Products_AreRankedAndProfitIsForOwnersOnly()
    {
        var (store, cheap, owner) = await ArrangeAsync(price: 100m);

        using (owner)
        {
            var dear = await api.CreateSellableProductAsync(store, price: 500m, stock: 20, name: "Noutbuk");

            await PlaceAsync(store, cheap.VariantId, quantity: 3);
            await PlaceAsync(store, dear.VariantId);

            var byRevenue = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/reports/products?" + Around())));
            var top = byRevenue.GetProperty("items")[0];

            Assert.Equal(2, byRevenue.GetProperty("totalCount").GetInt64());
            Assert.Equal(dear.ProductId, top.GetProperty("productId").GetGuid());
            Assert.Equal("Noutbuk", top.GetProperty("name").GetString());

            // The helpers give every variant a cost of 60, and only an owner holds reports.financial.
            Assert.Equal(440m, top.GetProperty("estimatedProfit").GetDecimal());

            var byUnits = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/reports/products?sort=units&" + Around())));

            Assert.Equal(cheap.ProductId, byUnits.GetProperty("items")[0].GetProperty("productId").GetGuid());

            var (_, managerToken, _) = await api.AddStaffAsync(store.Store, Roles.Manager);
            using var manager = api.CreateStoreClient(store.Slug, managerToken);

            var managerView = await SalesHelpers.ReadAsync(await manager.GetAsync(Path("/api/v1/admin/reports/products?" + Around())));

            Assert.Equal(JsonValueKind.Null, managerView.GetProperty("items")[0].GetProperty("estimatedProfit").ValueKind);

            var stock = await SalesHelpers.ReadAsync(await manager.GetAsync(Path("/api/v1/admin/reports/stock")));

            Assert.Equal(JsonValueKind.Null, stock.GetProperty("stockValue").ValueKind);

            var file = await manager.GetAsync(Path("/api/v1/admin/reports/export?kind=products&format=csv&" + Around()));

            file.EnsureSuccessStatusCode();
            Assert.DoesNotContain("440", await file.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("cost")]
    [InlineData("id;drop table orders")]
    public async Task Products_RefuseAnyOtherSort(string sort)
    {
        var store = await api.CreateStoreAsync();
        using var owner = api.CreateStoreClient(store.Slug, store.AccessToken);

        var response = await owner.GetAsync(Path($"/api/v1/admin/reports/products?sort={Uri.EscapeDataString(sort)}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Customers_TellNewFromReturningAndMaskContactsWithoutThePermission()
    {
        var (store, product, owner) = await ArrangeAsync();

        using (owner)
        {
            await PlaceAsync(store, product.VariantId, phone: "+99365000001");

            var boundary = DateTimeOffset.UtcNow;

            await Task.Delay(50);

            await PlaceAsync(store, product.VariantId, phone: "+99365000001");
            await PlaceAsync(store, product.VariantId, phone: "+99365000002");

            var from = Uri.EscapeDataString(boundary.ToString("O"));
            var to = Uri.EscapeDataString(boundary.AddHours(1).ToString("O"));

            var report = await SalesHelpers.ReadAsync(await owner.GetAsync(Path($"/api/v1/admin/reports/customers?from={from}&to={to}")));

            Assert.Equal(2, report.GetProperty("buyers").GetInt32());
            Assert.Equal(1, report.GetProperty("newBuyers").GetInt32());
            Assert.Equal(1, report.GetProperty("returningBuyers").GetInt32());
            Assert.Contains(report.GetProperty("top").EnumerateArray(), buyer => buyer.GetProperty("phone").GetString() == "+99365000001");
        }
    }

    [Fact]
    public async Task Customers_AreMaskedForThoseWhoCannotReadCustomers()
    {
        var (store, product, owner) = await ArrangeAsync();

        using (owner)
        {
            await PlaceAsync(store, product.VariantId, phone: "+99365222333");

            await api.ExecuteAsOwnerAsync(
                "DELETE FROM role_claims WHERE claim_value = @permission AND role_id = (SELECT id FROM roles WHERE name = @role)",
                ("permission", Permissions.CustomersRead),
                ("role", Roles.Manager));

            try
            {
                var (_, token, _) = await api.AddStaffAsync(store.Store, Roles.Manager);
                using var manager = api.CreateStoreClient(store.Slug, token);

                var report = await SalesHelpers.ReadAsync(await manager.GetAsync(Path("/api/v1/admin/reports/customers?" + Around())));
                var buyer = report.GetProperty("top")[0];

                Assert.Equal("***33", buyer.GetProperty("phone").GetString());
                Assert.EndsWith("***", buyer.GetProperty("name").GetString(), StringComparison.Ordinal);

                var file = await manager.GetAsync(Path("/api/v1/admin/reports/export?kind=customers&format=csv&" + Around()));
                var text = await file.Content.ReadAsStringAsync();

                Assert.DoesNotContain("+99365222333", text, StringComparison.Ordinal);
            }
            finally
            {
                await api.ExecuteAsOwnerAsync(
                    "INSERT INTO role_claims (role_id, claim_type, claim_value) SELECT id, 'permission', @permission FROM roles WHERE name = @role",
                    ("permission", Permissions.CustomersRead),
                    ("role", Roles.Manager));
            }
        }
    }

    [Fact]
    public async Task Stock_AddsUpEveryVariantForTheStore()
    {
        var (store, product, owner) = await ArrangeAsync();

        using (owner)
        {
            await PlaceAsync(store, product.VariantId, quantity: 2);

            var report = await SalesHelpers.ReadAsync(await owner.GetAsync(Path("/api/v1/admin/reports/stock")));

            Assert.Equal(1, report.GetProperty("variants").GetInt32());
            Assert.Equal(50, report.GetProperty("onHand").GetInt64());
            Assert.Equal(2, report.GetProperty("reserved").GetInt64());
            Assert.Equal(50 * 60m, report.GetProperty("stockValue").GetDecimal());
        }
    }

    [Fact]
    public async Task Reports_RefuseVisitorsAndStaffWithoutThePermission()
    {
        var store = await api.CreateStoreAsync();
        using var visitor = api.CreateStoreClient(store.Slug);

        Assert.Equal(HttpStatusCode.Unauthorized, (await visitor.GetAsync(Path("/api/v1/admin/reports/sales"))).StatusCode);

        var (_, token, _) = await api.AddStaffAsync(store, Roles.Staff);
        using var staff = api.CreateStoreClient(store.Slug, token);

        foreach (var path in new[] { "sales", "products", "customers", "stock", "export?kind=sales&format=csv" })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(Path("/api/v1/admin/reports/" + path))).StatusCode);
        }
    }

    [Fact]
    public async Task Reports_NeverIncludeAnotherStore()
    {
        var (store, product, owner) = await ArrangeAsync();

        using (owner)
        {
            await PlaceAsync(store, product.VariantId);

            var other = await api.CreateStoreAsync();
            using var otherOwner = api.CreateStoreClient(other.Slug, other.AccessToken);

            var report = await SalesHelpers.ReadAsync(await otherOwner.GetAsync(Path("/api/v1/admin/reports/sales?" + Around())));

            Assert.Equal(0, report.GetProperty("figures").GetProperty("orders").GetInt32());
            Assert.Equal(0, (await SalesHelpers.ReadAsync(await otherOwner.GetAsync(Path("/api/v1/admin/reports/stock")))).GetProperty("variants").GetInt32());
        }
    }

    [Fact]
    public async Task Export_DefusesFormulasFromCustomers()
    {
        var (store, product, owner) = await ArrangeAsync();

        using (owner)
        {
            using var shopper = new Shopper(api, store.Slug);

            (await shopper.AddAsync(product.VariantId)).EnsureSuccessStatusCode();

            var placed = await shopper.CheckoutAsync(new
            {
                shippingMethodId = store.CourierId,
                paymentMethod = "cashOnDelivery",
                contact = new { name = "=HYPERLINK(\"http://evil\",\"x\")", phone = "+99365111111" },
                address = SalesHelpers.Address(),
            });

            placed.EnsureSuccessStatusCode();

            var csv = await owner.GetAsync(Path("/api/v1/admin/reports/export?kind=customers&format=csv&" + Around()));

            csv.EnsureSuccessStatusCode();
            Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
            Assert.Contains(".csv", csv.Content.Headers.ContentDisposition?.FileName, StringComparison.Ordinal);

            var bytes = await csv.Content.ReadAsByteArrayAsync();

            Assert.Equal(Encoding.UTF8.GetPreamble(), bytes[..3]);

            var text = Encoding.UTF8.GetString(bytes[3..]);

            Assert.Contains("\"'=HYPERLINK(\"\"http://evil\"\",\"\"x\"\")\"", text, StringComparison.Ordinal);
            Assert.DoesNotContain("\n=HYPERLINK", text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("sales")]
    [InlineData("products")]
    [InlineData("customers")]
    [InlineData("stock")]
    public async Task Export_WritesARealSpreadsheet(string kind)
    {
        var (store, product, owner) = await ArrangeAsync();

        using (owner)
        {
            await PlaceAsync(store, product.VariantId);

            var response = await owner.GetAsync(Path($"/api/v1/admin/reports/export?kind={kind}&format=xlsx&" + Around()));

            response.EnsureSuccessStatusCode();
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType?.MediaType);

            var bytes = await response.Content.ReadAsByteArrayAsync();

            // Every .xlsx is a zip file, and that is how such a file begins.
            Assert.Equal("PK"u8.ToArray(), bytes[..2]);
        }
    }
}
