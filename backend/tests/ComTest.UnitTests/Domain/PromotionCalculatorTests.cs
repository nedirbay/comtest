using ComTest.Domain.Modules.Promotions;

namespace ComTest.UnitTests.Domain;

public sealed class PromotionCalculatorTests
{
    private static readonly Guid Phones = Guid.NewGuid();
    private static readonly Guid Laptops = Guid.NewGuid();

    private static Promotion Make(PromotionType type, decimal value, string? code = null, Guid? category = null, bool combines = false) =>
        Promotion.Create(new PromotionTerms(code, "Aksiýa", type, value, null, null, null, category, null, null, combines, true), "TMT");

    private static List<PromotionLine> Lines(params (decimal Total, Guid Category)[] lines) =>
        lines.Select(line => new PromotionLine(line.Total, new HashSet<Guid> { line.Category })).ToList();

    [Fact]
    public void Percentage_IsTakenOffTheEligibleGoodsAndRounded()
    {
        var lines = Lines((33.33m, Phones), (100m, Laptops));

        var outcome = PromotionCalculator.Best(Make(PromotionType.Percentage, 10m, "KOD123", Phones), [], lines, 0m);

        Assert.Equal(3.33m, outcome.GoodsDiscount);
    }

    [Fact]
    public void FixedAmount_NeverGoesBelowZero()
    {
        var outcome = PromotionCalculator.Best(Make(PromotionType.FixedAmount, 500m, "KOD123"), [], Lines((100m, Phones)), 20m);

        Assert.Equal(100m, outcome.GoodsDiscount);
        Assert.Equal(0m, outcome.ShippingDiscount);
    }

    [Fact]
    public void FreeShipping_WaivesOnlyTheShipping()
    {
        var outcome = PromotionCalculator.Best(Make(PromotionType.FreeShipping, 0m, "KOD123"), [], Lines((100m, Phones)), 20m);

        Assert.Equal(0m, outcome.GoodsDiscount);
        Assert.Equal(20m, outcome.ShippingDiscount);
    }

    [Fact]
    public void Promotions_ThatDoNotCombine_GiveTheBetterOfTheTwo()
    {
        var code = Make(PromotionType.Percentage, 5m, "KOD123");
        var automatic = Make(PromotionType.Percentage, 20m);

        var outcome = PromotionCalculator.Best(code, [automatic], Lines((100m, Phones)), 0m);

        Assert.Equal(20m, outcome.GoodsDiscount);
        Assert.Same(automatic, Assert.Single(outcome.Shares).Promotion);
    }

    [Fact]
    public void Promotions_ThatCombine_AddUp()
    {
        var code = Make(PromotionType.Percentage, 10m, "KOD123", combines: true);
        var automatic = Make(PromotionType.FixedAmount, 5m, combines: true);
        var shipping = Make(PromotionType.FreeShipping, 0m, combines: true);

        var outcome = PromotionCalculator.Best(code, [automatic, shipping], Lines((100m, Phones)), 20m);

        Assert.Equal(15m, outcome.GoodsDiscount);
        Assert.Equal(20m, outcome.ShippingDiscount);
        Assert.Equal(3, outcome.Shares.Count);
    }

    [Fact]
    public void AnAutomaticPromotion_ThatDoesNotCombine_IsNotAddedToACodeThatDoes()
    {
        var code = Make(PromotionType.Percentage, 10m, "KOD123", combines: true);
        var automatic = Make(PromotionType.Percentage, 5m);

        var outcome = PromotionCalculator.Best(code, [automatic], Lines((100m, Phones)), 0m);

        Assert.Equal(10m, outcome.GoodsDiscount);
        Assert.Same(code, Assert.Single(outcome.Shares).Promotion);
    }

    [Fact]
    public void CombinedDiscounts_NeverTakeTheGoodsBelowZero()
    {
        var first = Make(PromotionType.Percentage, 100m, "KOD123", combines: true);
        var second = Make(PromotionType.FixedAmount, 50m, combines: true);
        var shipping = Make(PromotionType.FreeShipping, 0m, combines: true);

        var outcome = PromotionCalculator.Best(first, [second, shipping], Lines((100m, Phones)), 20m);

        Assert.Equal(100m, outcome.GoodsDiscount);
        Assert.Equal(20m, outcome.ShippingDiscount);
        Assert.Equal(100m, outcome.Shares.Where(share => share.Promotion.Type != PromotionType.FreeShipping).Sum(share => share.Amount));
    }

    [Fact]
    public void NoPromotions_MeanNoDiscount()
    {
        var outcome = PromotionCalculator.Best(null, [], Lines((100m, Phones)), 20m);

        Assert.Equal(0m, outcome.Value);
        Assert.Empty(outcome.Shares);
    }

    [Theory]
    [InlineData(-1, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(-2, -1, false)]
    public void IsRunning_ChecksThePeriod(int startHours, int endHours, bool expected)
    {
        var now = DateTimeOffset.UtcNow;
        var promotion = Promotion.Create(new PromotionTerms(null, "A", PromotionType.Percentage, 1m, now.AddHours(startHours), now.AddHours(endHours), null, null, null, null, false, true), "TMT");

        Assert.Equal(expected, promotion.IsRunning(now));
    }

    [Fact]
    public void GeneratedCodes_AreLongAndReadable()
    {
        var code = PromotionCode.Generate();

        Assert.Equal(PromotionCode.GeneratedLength, code.Length);
        Assert.DoesNotContain(code, letter => "01OIL".Contains(letter, StringComparison.Ordinal));
    }
}
