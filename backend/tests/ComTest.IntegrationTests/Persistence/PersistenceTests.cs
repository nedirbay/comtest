using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using ComTest.Application.Abstractions;
using ComTest.Domain.Modules.Catalog;
using ComTest.Infrastructure;
using ComTest.Infrastructure.Persistence;
using ComTest.Infrastructure.Persistence.Outbox;
using ComTest.IntegrationTests.Catalog;
using ComTest.IntegrationTests.Support;
using ComTest.Shared.Domain;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ComTest.IntegrationTests.Persistence;

[Collection(ApiCollection.Name)]
public sealed class AuditAndSoftDeleteTests(ApiFixture api)
{
    [Fact]
    public async Task AuditFields_RecordWhoAndWhen()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug));
        var id = created.GetProperty("id").GetGuid();
        var variantId = created.GetProperty("variants")[0].GetProperty("id").GetGuid();

        Assert.Equal(store.Owner.UserId, await api.QueryAsOwnerAsync<Guid>("SELECT created_by FROM products WHERE id = @id", ("id", id)));
        Assert.Null(await api.QueryAsOwnerAsync<DateTime?>("SELECT updated_at FROM products WHERE id = @id", ("id", id)));

        await admin.PutAsJsonAsync($"/api/v1/admin/catalog/products/{id}", CatalogRequests.Product(slug, "SKU-" + slug, name: "Täze", variantId: variantId));

        Assert.Equal(store.Owner.UserId, await api.QueryAsOwnerAsync<Guid>("SELECT updated_by FROM products WHERE id = @id", ("id", id)));
        Assert.NotNull(await api.QueryAsOwnerAsync<DateTime?>("SELECT updated_at FROM products WHERE id = @id", ("id", id)));
    }

    [Fact]
    public async Task Delete_HidesTheRow_KeepsItInTheDatabase_AndFreesTheSlug()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug));
        var id = created.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/admin/catalog/products/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/admin/catalog/products/{id}")).StatusCode);
        Assert.True(await api.QueryAsOwnerAsync<bool>("SELECT is_deleted FROM products WHERE id = @id", ("id", id)));
        Assert.Equal(store.Owner.UserId, await api.QueryAsOwnerAsync<Guid>("SELECT deleted_by FROM products WHERE id = @id", ("id", id)));

        var again = await admin.PostAsJsonAsync("/api/v1/admin/catalog/products", CatalogRequests.Product(slug, "OTHER-" + slug));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task IdsAreVersion7()
    {
        var store = await api.CreateStoreAsync();

        Assert.Equal(7, store.TenantId.Version);
        Assert.Equal(7, store.Owner.UserId.Version);
    }
}

[Collection(ApiCollection.Name)]
public sealed class ConcurrencyTests(ApiFixture api)
{
    [Fact]
    public async Task ConflictingWrites_FailInsteadOfOverwriting()
    {
        var store = await api.CreateStoreAsync();

        await using var scope1 = NewTenantScope(store.TenantId);
        await using var scope2 = NewTenantScope(store.TenantId);

        var db1 = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();

        var brand = Brand.Create("X", CatalogRequests.NewSlug("brand"), null);
        db1.Brands.Add(brand);
        await db1.SaveChangesAsync();

        var first = await db1.Brands.FirstAsync(item => item.Id == brand.Id);
        var second = await db2.Brands.FirstAsync(item => item.Id == brand.Id);

        first.Update("Birinji", first.Slug, null);
        second.Update("Ikinji", second.Slug, null);

        await db1.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db2.SaveChangesAsync());
    }

    [Fact]
    public async Task VersionColumn_IsTheSystemXmin()
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var property = db.Model.FindEntityType(typeof(Product))!.FindProperty(nameof(IVersioned.Version))!;

        Assert.Equal("xmin", property.GetColumnName());
        Assert.True(property.IsConcurrencyToken);
    }

    private AsyncServiceScope NewTenantScope(Guid tenantId)
    {
        var scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(tenantId);

        return scope;
    }
}

[Collection(ApiCollection.Name)]
public sealed class MoneyMappingTests(ApiFixture api)
{
    [Theory]
    [InlineData("product_variants", "price_amount")]
    [InlineData("product_variants", "compare_at_amount")]
    [InlineData("product_variants", "cost_amount")]
    [InlineData("price_changes", "old_price_amount")]
    [InlineData("price_changes", "new_price_amount")]
    public async Task AmountColumns_AreNumeric18Scale2(string table, string column)
    {
        var type = await api.QueryAsOwnerAsync<string>(
            """
            SELECT format_type(a.atttypid, a.atttypmod) FROM pg_attribute a
            WHERE a.attrelid = to_regclass(@table) AND a.attname = @column
            """,
            ("table", table),
            ("column", column));

        Assert.Equal("numeric(18,2)", type);
    }

    [Fact]
    public async Task Money_SurvivesASaveAndLoad_RoundedToTwoDecimals()
    {
        var store = await api.CreateStoreAsync();
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();

        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug, price: 10.005m));
        var id = created.GetProperty("id").GetGuid();

        var loaded = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/admin/catalog/products/{id}");
        var variant = loaded.GetProperty("variants")[0];

        Assert.Equal(10.01m, variant.GetProperty("price").GetDecimal());
        Assert.Equal("TMT", variant.GetProperty("currency").GetString());
    }
}

[Collection(ApiCollection.Name)]
public sealed class OutboxTests(ApiFixture api)
{
    [Fact]
    public async Task PublishedEvent_IsDelivered_WithTheRightTenant_AndOnlyOnce()
    {
        var store = await api.CreateStoreAsync();
        var productId = await PublishProductAsync(store);

        var stored = await api.QueryAsOwnerAsync<Guid>(
            "SELECT tenant_id FROM outbox_messages WHERE payload->>'ProductId' = @id AND type LIKE '%ProductPublished'",
            ("id", productId.ToString()));

        Assert.Equal(store.TenantId, stored);

        await DrainAsync();
        await DrainAsync();

        var delivered = api.DomainEvents.Delivered.Where(item => item.ProductId == productId).ToList();

        Assert.Single(delivered);
        Assert.Equal(store.TenantId, delivered[0].TenantId);
    }

    [Fact]
    public async Task FailedDelivery_IsRetriedLater_AndFinallyDeadLettered()
    {
        var store = await api.CreateStoreAsync();
        var productId = await PublishProductAsync(store);
        api.DomainEvents.FailFor(productId);

        await DrainAsync();

        const string Where = "payload->>'ProductId' = @id AND type LIKE '%ProductPublished'";

        Assert.Equal(1, await api.QueryAsOwnerAsync<int>($"SELECT attempts FROM outbox_messages WHERE {Where}", ("id", productId.ToString())));
        Assert.Null(await api.QueryAsOwnerAsync<DateTime?>($"SELECT processed_at FROM outbox_messages WHERE {Where}", ("id", productId.ToString())));
        Assert.True(await api.QueryAsOwnerAsync<bool>($"SELECT next_attempt_at > now() FROM outbox_messages WHERE {Where}", ("id", productId.ToString())));

        // Only the exception type is stored, as its message could hold personal data.
        Assert.Equal("System.InvalidOperationException", await LastErrorAsync(productId));

        await api.ExecuteAsOwnerAsync($"UPDATE outbox_messages SET attempts = 9, next_attempt_at = now() WHERE {Where}", ("id", productId.ToString()));
        await DrainAsync();

        Assert.NotNull(await api.QueryAsOwnerAsync<DateTime?>($"SELECT dead_lettered_at FROM outbox_messages WHERE {Where}", ("id", productId.ToString())));
    }

    [Fact]
    public async Task UnknownEventType_IsNotInstantiated_AndDoesNotStopTheBatch()
    {
        var id = Guid.NewGuid();

        await api.ExecuteAsOwnerAsync(
            """
            INSERT INTO outbox_messages (id, type, payload, occurred_at, next_attempt_at, attempts)
            VALUES (@id, 'System.Diagnostics.Process, System', '{"StartInfo":{"FileName":"/bin/sh"}}', now() - interval '1 hour', now() - interval '1 hour', 0)
            """,
            ("id", id));

        var store = await api.CreateStoreAsync();
        var productId = await PublishProductAsync(store);
        await DrainAsync();

        Assert.Equal("System.InvalidOperationException", await api.QueryAsOwnerAsync<string>("SELECT last_error FROM outbox_messages WHERE id = @id", ("id", id)));
        Assert.Contains(api.DomainEvents.Delivered, item => item.ProductId == productId);
    }

    [Fact]
    public async Task EventAndChange_AreSavedTogether_OrNotAtAll()
    {
        var store = await api.CreateStoreAsync();
        var slug = CatalogRequests.NewSlug();

        await using var seed = api.Factory.Services.CreateAsyncScope();
        seed.ServiceProvider.GetRequiredService<TenantContext>().Use(store.TenantId);
        var seedDb = seed.ServiceProvider.GetRequiredService<AppDbContext>();
        seedDb.Products.Add(Product.Create(slug, null, null, null));
        await seedDb.SaveChangesAsync();

        await using var scope = api.Factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().Use(store.TenantId);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The same slug breaks the unique index, so the whole save fails.
        var duplicate = Product.Create(slug, null, null, null);
        duplicate.SetTranslations([new ProductTranslationInput("tk", "X")]);
        duplicate.SetVariants([new VariantInput(null, "V-" + slug, null, Money.Of(1m, "TMT"), null, null, new Dictionary<string, string>(), true)], DateTimeOffset.UtcNow);
        duplicate.Publish(DateTimeOffset.UtcNow);
        db.Products.Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal(0L, await api.QueryAsOwnerAsync<long>(
            "SELECT count(*) FROM outbox_messages WHERE payload->>'ProductId' = @id",
            ("id", duplicate.Id.ToString())));
    }

    [Fact]
    public async Task ParallelProcessors_DeliverEachEventOnce()
    {
        var store = await api.CreateStoreAsync();
        var products = new List<Guid>();

        for (var index = 0; index < 5; index++)
        {
            products.Add(await PublishProductAsync(store));
        }

        var processor = api.Factory.Services.GetRequiredService<OutboxProcessor>();

        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => processor.ProcessBatchAsync(CancellationToken.None)));
        await DrainAsync();

        foreach (var productId in products)
        {
            Assert.Single(api.DomainEvents.Delivered, item => item.ProductId == productId);
        }
    }

    /// <summary>Earlier tests leave events waiting, so one batch may not reach this test's event.</summary>
    private async Task DrainAsync()
    {
        var processor = api.Factory.Services.GetRequiredService<OutboxProcessor>();

        while (await processor.ProcessBatchAsync(CancellationToken.None) > 0)
        {
        }
    }

    private Task<string?> LastErrorAsync(Guid productId) =>
        api.QueryAsOwnerAsync<string>(
            "SELECT last_error FROM outbox_messages WHERE payload->>'ProductId' = @id AND type LIKE '%ProductPublished'",
            ("id", productId.ToString()));

    private async Task<Guid> PublishProductAsync(TestStore store)
    {
        using var admin = api.CreateStoreClient(store.Slug, store.AccessToken);
        var slug = CatalogRequests.NewSlug();
        var created = await admin.CreateProductAsync(CatalogRequests.Product(slug, "SKU-" + slug));
        var id = created.GetProperty("id").GetGuid();
        await admin.PublishAsync(id);

        return id;
    }
}

[Collection(ApiCollection.Name)]
public sealed class DataProtectionKeyTests(ApiFixture api)
{
    [Fact]
    public async Task KeysInTheDatabase_AreEncryptedWithTheCertificate()
    {
        var databaseName = "dp_" + Guid.NewGuid().ToString("N");
        await api.ExecuteAsOwnerAsync($"CREATE DATABASE {databaseName}");

        var connection = new NpgsqlConnectionStringBuilder(api.OwnerConnectionString) { Database = databaseName }.ConnectionString;

        await using (var migrator = new AppDbContext(
            (DbContextOptions<AppDbContext>)new DbContextOptionsBuilder<AppDbContext>().UseAppDatabase(connection).Options,
            new TenantContext()))
        {
            await migrator.Database.MigrateAsync();
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:CertificatePfxBase64"] = Convert.ToBase64String(CreateCertificate()),
                ["DataProtection:CertificatePassword"] = "test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddDbContext<AppDbContext>(options => options.UseAppDatabase(connection));
        DependencyInjection.AddDataProtectionKeys(services, configuration, new ProductionEnvironment());

        await using var provider = services.BuildServiceProvider();

        var protector = provider.GetRequiredService<IDataProtectionProvider>().CreateProtector("test");

        Assert.Equal("gizlin", protector.Unprotect(protector.Protect("gizlin")));

        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT xml FROM data_protection_keys LIMIT 1", db);
        var xml = (string)(await command.ExecuteScalarAsync())!;

        Assert.Contains("encryptedSecret", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<masterKey", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_RefusesToStart_WithoutACertificate()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        Assert.Throws<InvalidOperationException>(() =>
            DependencyInjection.AddDataProtectionKeys(services, configuration, new ProductionEnvironment()));
    }

    private static byte[] CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=comtest-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));

        return certificate.Export(X509ContentType.Pfx, "test");
    }

    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

[Collection(ApiCollection.Name)]
public sealed class DevDataSeederTests(ApiFixture api)
{
    [Fact]
    public async Task Seed_CreatesAWorkingStore_AndIsSafeToRunTwice()
    {
        await using var first = api.Factory.Services.CreateAsyncScope();
        var accounts = await ComTest.Api.Hosting.DevDataSeeder.SeedAsync(first.ServiceProvider, CancellationToken.None);

        Assert.Equal(3, accounts.Count);
        Assert.All(accounts, account => Assert.True(account.Password.Length >= 20));

        await using var second = api.Factory.Services.CreateAsyncScope();
        Assert.Empty(await ComTest.Api.Hosting.DevDataSeeder.SeedAsync(second.ServiceProvider, CancellationToken.None));

        using var visitor = api.CreateStoreClient(ComTest.Api.Hosting.DevDataSeeder.DemoSlug);
        var products = await visitor.GetFromJsonAsync<JsonElement>("/api/v1/catalog/products");

        Assert.Equal(2, products.GetProperty("totalCount").GetInt32());

        using var anonymous = api.CreateClient();
        var customer = accounts.Single(account => account.Role == "Customer");
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new { login = customer.Email, password = customer.Password });

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public void SeedAccount_DoesNotPrintItsPassword() =>
        Assert.DoesNotContain("hunter2", new ComTest.Api.Hosting.SeededAccount("a@b.test", "hunter2", "Customer").ToString(), StringComparison.Ordinal);
}
