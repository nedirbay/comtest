using ComTest.Domain.Modules.Catalog;
using ComTest.Domain.Modules.Tenancy;
using ComTest.Infrastructure.Catalog;
using ComTest.Shared.Domain;

namespace ComTest.UnitTests.Domain;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    private static VariantInput Variant(Guid? id, decimal price, bool active = true) =>
        new(id, "SKU", null, Money.Of(price, "TMT"), null, null, new Dictionary<string, string>(), active);

    [Fact]
    public void ChangingAPrice_RecordsItAndRaisesAnEvent()
    {
        var product = Product.Create("telefon", null, null, null);
        product.SetVariants([Variant(null, 100m)], Now);

        var variantId = product.Variants.Single().Id;
        product.ClearDomainEvents();

        product.SetVariants([Variant(variantId, 120m)], Now);

        var change = Assert.Single(product.PriceChanges);
        Assert.Equal(100m, change.OldPrice.Amount);
        Assert.Equal(120m, change.NewPrice.Amount);
        Assert.IsType<VariantPriceChanged>(Assert.Single(product.DomainEvents));
    }

    [Fact]
    public void KeepingThePrice_RecordsNothing()
    {
        var product = Product.Create("telefon", null, null, null);
        product.SetVariants([Variant(null, 100m)], Now);

        product.SetVariants([Variant(product.Variants.Single().Id, 100m)], Now);

        Assert.Empty(product.PriceChanges);
    }

    [Fact]
    public void MissingVariantsInTheInput_AreRemoved()
    {
        var product = Product.Create("telefon", null, null, null);
        product.SetVariants([Variant(null, 100m)], Now);

        product.SetVariants([Variant(null, 50m)], Now);

        Assert.Equal(50m, Assert.Single(product.Variants).Price.Amount);
    }

    [Fact]
    public void Publish_RequiresANameAndAnActiveVariant()
    {
        var product = Product.Create("telefon", null, null, null);

        Assert.False(product.CanPublish());
        Assert.Throws<InvalidOperationException>(() => product.Publish(Now));

        product.SetTranslations([new ProductTranslationInput("tk", "Telefon")]);
        Assert.False(product.CanPublish());

        product.SetVariants([Variant(null, 100m, active: false)], Now);
        Assert.False(product.CanPublish());

        product.SetVariants([Variant(null, 100m)], Now);
        Assert.True(product.CanPublish());

        product.Publish(Now);

        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(Now, product.PublishedAt);
        Assert.Contains(product.DomainEvents, domainEvent => domainEvent is ProductPublished);
    }

    [Fact]
    public void Republishing_KeepsTheFirstPublicationDate()
    {
        var product = Product.Create("telefon", null, null, null);
        product.SetTranslations([new ProductTranslationInput("tk", "Telefon")]);
        product.SetVariants([Variant(null, 100m)], Now);
        product.Publish(Now);
        product.Archive();

        product.Publish(Now.AddDays(5));

        Assert.Equal(Now, product.PublishedAt);
    }

    [Fact]
    public void SetTranslations_UpdatesAddsAndRemoves()
    {
        var product = Product.Create("telefon", null, null, null);
        product.SetTranslations([new ProductTranslationInput("tk", "Telefon"), new ProductTranslationInput("ru", "Телефон")]);

        var turkmen = product.Translations.Single(item => item.Language == "tk");

        product.SetTranslations([new ProductTranslationInput("tk", "Täze"), new ProductTranslationInput("en", "Phone")]);

        Assert.Equal(2, product.Translations.Count);
        Assert.Same(turkmen, product.Translations.Single(item => item.Language == "tk"));
        Assert.Equal("Täze", turkmen.Name);
        Assert.DoesNotContain(product.Translations, item => item.Language == "ru");
    }
}

public sealed class AttributeDefinitionTests
{
    [Theory]
    [InlineData(AttributeDataType.Number, "128", true)]
    [InlineData(AttributeDataType.Number, "1.5", true)]
    [InlineData(AttributeDataType.Number, "kop", false)]
    [InlineData(AttributeDataType.Boolean, "true", true)]
    [InlineData(AttributeDataType.Boolean, "hawa", false)]
    [InlineData(AttributeDataType.Select, "gara", true)]
    [InlineData(AttributeDataType.Select, "gyzyl", false)]
    [InlineData(AttributeDataType.Text, "islendik", true)]
    [InlineData(AttributeDataType.Text, "", false)]
    public void Accepts_ChecksTheValueAgainstTheType(AttributeDataType type, string value, bool expected)
    {
        var attribute = AttributeDefinition.Create("code", type, ["gara", "ak"], [new TranslationInput("tk", "X")]);

        Assert.Equal(expected, attribute.Accepts(value));
    }
}

public sealed class TenantEntityTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(TenantPlan.Free, 100, 3)]
    [InlineData(TenantPlan.Standard, 5000, 15)]
    [InlineData(TenantPlan.Business, 100000, 100)]
    public void Plan_SetsTheLimits(TenantPlan plan, int maxProducts, int maxStaff)
    {
        var tenant = Tenant.Create("Ak Bazar", "ak-bazar", "TMT", "tk", Now);

        tenant.ChangePlan(plan);

        Assert.Equal(maxProducts, tenant.MaxProducts);
        Assert.Equal(maxStaff, tenant.MaxStaff);
    }

    [Fact]
    public void Create_RaisesAnEvent_AndStartsActiveOnTheFreePlan()
    {
        var tenant = Tenant.Create("Ak Bazar", "ak-bazar", "TMT", "tk", Now);

        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Equal(TenantPlan.Free, tenant.Plan);
        Assert.IsType<TenantCreated>(Assert.Single(tenant.DomainEvents));
    }

    [Fact]
    public void TenantOfAnEntity_CannotBeChanged()
    {
        var member = TenantMember.Create(Guid.NewGuid(), Guid.NewGuid(), "Staff");

        Assert.Throws<InvalidOperationException>(() => member.AssignTenant(Guid.NewGuid()));
    }

    [Fact]
    public void Invitation_IsPendingOnlyWhileUnusedAndUnexpired()
    {
        var invitation = StaffInvitation.Create("A@B.TEST", "Staff", "hash", Now.AddDays(1));

        Assert.True(invitation.IsPending(Now));
        Assert.False(invitation.IsPending(Now.AddDays(2)));

        invitation.Accept(Now);
        Assert.False(invitation.IsPending(Now));

        var revoked = StaffInvitation.Create("A@B.TEST", "Staff", "hash", Now.AddDays(1));
        revoked.Revoke(Now);
        Assert.False(revoked.IsPending(Now));
    }
}

public sealed class HtmlCleanerTests
{
    private readonly HtmlCleaner _cleaner = new();

    [Theory]
    [InlineData("<script>alert(1)</script>", "script")]
    [InlineData("<svg onload=alert(1)>", "onload")]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript")]
    [InlineData("<a href=\"data:text/html,x\">x</a>", "data:")]
    [InlineData("<iframe src=\"https://evil.test\"></iframe>", "iframe")]
    [InlineData("<form action=\"https://evil.test\"><input></form>", "form")]
    [InlineData("<style>body{display:none}</style>", "style")]
    [InlineData("<div style=\"position:fixed\">x</div>", "position")]
    public void Clean_RemovesDangerousContent(string html, string forbidden) =>
        Assert.DoesNotContain(forbidden, _cleaner.Clean(html), StringComparison.OrdinalIgnoreCase);

    [Theory]
    [InlineData("<p><strong>Täze</strong> <em>haryt</em></p>")]
    [InlineData("<ul><li>Bir</li></ul>")]
    [InlineData("<a href=\"https://example.test/\">salgy</a>")]
    public void Clean_KeepsFormatting(string html) => Assert.Equal(html, _cleaner.Clean(html));
}
