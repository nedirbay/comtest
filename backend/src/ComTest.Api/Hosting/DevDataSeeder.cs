using System.Security.Cryptography;
using ComTest.Application.Modules.Identity.Contracts;
using ComTest.Application.Modules.Inventory;
using ComTest.Application.Modules.Inventory.Contracts;
using ComTest.Domain.Modules.Catalog;
using ComTest.Domain.Modules.Inventory;
using ComTest.Domain.Modules.Shipping;
using ComTest.Domain.Modules.Tenancy;
using ComTest.Infrastructure.Identity;
using ComTest.Infrastructure.Persistence;
using ComTest.Shared.Domain;
using ComTest.Shared.Messaging;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ComTest.Api.Hosting;

public sealed record SeededAccount(string Email, string Password, string Role)
{
    public override string ToString() => nameof(SeededAccount);
}

/// <summary>
/// Demo data for local work: <c>dotnet run --project src/ComTest.Api -- seed</c>. Passwords are
/// random and shown once, so that no known password can ever exist in a shared database.
/// </summary>
public static class DevDataSeeder
{
    public const string Command = "seed";
    public const string DemoSlug = "demo";

    public static async Task RunAsync(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        if (!app.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("Demo data can only be created in the Development environment.");
        }

        await using var scope = app.Services.CreateAsyncScope();

        var accounts = await SeedAsync(scope.ServiceProvider, CancellationToken.None);

        if (accounts.Count == 0)
        {
            await Console.Out.WriteLineAsync("Demo data already exists: nothing was changed.");

            return;
        }

        await Console.Out.WriteLineAsync($"Demo store '{DemoSlug}' created. These passwords will not be shown again:");

        foreach (var account in accounts)
        {
            await Console.Out.WriteLineAsync($"  {account.Role,-14} {account.Email,-32} {account.Password}");
        }
    }

    public static async Task<IReadOnlyList<SeededAccount>> SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        var db = services.GetRequiredService<AppDbContext>();

        if (await db.Tenants.AnyAsync(tenant => tenant.Slug == DemoSlug, cancellationToken))
        {
            return [];
        }

        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var timeProvider = services.GetRequiredService<TimeProvider>();
        var now = timeProvider.GetUtcNow();

        var accounts = new List<SeededAccount>();
        await CreateUserAsync(users, "admin@comtest.local", "Platform Admin", Roles.PlatformAdmin, accounts, now);
        var owner = await CreateUserAsync(users, "owner@demo.comtest.local", "Demo Owner", Roles.StoreOwner, accounts, now);
        await CreateUserAsync(users, "customer@comtest.local", "Demo Customer", Roles.Customer, accounts, now);

        var tenant = Tenant.Create("Demo Dükan", DemoSlug, "TMT", "tk", now);

        services.GetRequiredService<TenantContext>().Use(tenant.Id);

        db.Tenants.Add(tenant);
        db.TenantMembers.Add(TenantMember.Create(tenant.Id, owner.Id, Roles.StoreOwner));

        var products = AddCatalog(db, now);

        AddShipping(db);

        await db.SaveChangesAsync(cancellationToken);
        await AddStockAsync(db, services, products, cancellationToken);

        return accounts;
    }

    private static async Task AddStockAsync(AppDbContext db, IServiceProvider services, List<Product> products, CancellationToken cancellationToken)
    {
        var warehouse = Warehouse.Create("Esasy ammar", "MAIN", "Aşgabat", isDefault: true);

        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(cancellationToken);

        var adjust = services.GetRequiredService<ICommandHandler<AdjustStockCommand, StockItemDto>>();

        foreach (var variant in products.SelectMany(product => product.Variants))
        {
            await adjust.HandleAsync(
                new AdjustStockCommand(new AdjustStockRequest(warehouse.Id, variant.Id, 25, StockMovementType.Receipt, "Demo")),
                cancellationToken);
        }
    }

    private static void AddShipping(AppDbContext db)
    {
        var city = ShippingZone.Create("Aşgabat", ["ashgabat"]);
        var regions = ShippingZone.Create("Welaýatlar", ["ahal", "balkan", "dashoguz", "lebap", "mary"]);

        db.ShippingZones.AddRange(city, regions);

        db.ShippingMethods.AddRange(
            ShippingMethod.Create(
                ShippingMethodType.Courier,
                city.Id,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["tk"] = "Kurýer", ["ru"] = "Курьер", ["en"] = "Courier" },
                Money.Of(20m, "TMT"),
                1000m,
                0,
                1,
                null,
                0),
            ShippingMethod.Create(
                ShippingMethodType.Courier,
                regions.Id,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["tk"] = "Welaýat kurýeri", ["ru"] = "Доставка в регионы", ["en"] = "Regional courier" },
                Money.Of(50m, "TMT"),
                null,
                2,
                4,
                null,
                1),
            ShippingMethod.Create(
                ShippingMethodType.Pickup,
                null,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["tk"] = "Dükandan almak", ["ru"] = "Самовывоз", ["en"] = "Pick up in store" },
                Money.Of(0m, "TMT"),
                null,
                null,
                null,
                "Aşgabat, Magtymguly şaýoly 1",
                2));
    }

    private static List<Product> AddCatalog(AppDbContext db, DateTimeOffset now)
    {
        var electronics = Category.Create(null, "elektronika", 0, [new TranslationInput("tk", "Elektronika"), new TranslationInput("ru", "Электроника"), new TranslationInput("en", "Electronics")]);
        var phones = Category.Create(electronics.Id, "telefonlar", 0, [new TranslationInput("tk", "Telefonlar"), new TranslationInput("ru", "Телефоны"), new TranslationInput("en", "Phones")]);
        var brand = Brand.Create("Samsung", "samsung", null);

        db.Categories.AddRange(electronics, phones);
        db.Brands.Add(brand);

        List<Product> products =
        [
            CreateProduct("galaxy-a55", "Galaxy A55", "Galaxy A55 телефон", "GAL-A55", 5200m, 4300m, phones.Id, brand.Id, now),
            CreateProduct("galaxy-s25", "Galaxy S25", "Galaxy S25 телефон", "GAL-S25", 12900m, 10800m, phones.Id, brand.Id, now),
        ];

        db.Products.AddRange(products);

        return products;
    }

    private static Product CreateProduct(string slug, string nameTk, string nameRu, string sku, decimal price, decimal cost, Guid categoryId, Guid brandId, DateTimeOffset now)
    {
        var product = Product.Create(slug, categoryId, brandId, null);

        product.SetTranslations([new ProductTranslationInput("tk", nameTk), new ProductTranslationInput("ru", nameRu)]);
        product.SetVariants(
            [new VariantInput(null, sku, null, Money.Of(price, "TMT"), null, cost, new Dictionary<string, string>(StringComparer.Ordinal) { ["color"] = "gara" }, true)],
            now);
        product.Publish(now);

        return product;
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        UserManager<ApplicationUser> users,
        string email,
        string fullName,
        string role,
        List<SeededAccount> accounts,
        DateTimeOffset now)
    {
        var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            CreatedAt = now,
        };

        var created = await users.CreateAsync(user, password);

        if (!created.Succeeded)
        {
            throw new InvalidOperationException($"Could not create {email}: {string.Join(", ", created.Errors.Select(error => error.Code))}");
        }

        // A store owner's role comes from the membership row, not from a global role.
        if (role != Roles.StoreOwner)
        {
            await users.AddToRoleAsync(user, role);
        }

        accounts.Add(new SeededAccount(email, password, role));

        return user;
    }
}
