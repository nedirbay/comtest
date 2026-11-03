using ComTest.Application;
using ComTest.Shared.Domain;
using ComTest.Shared.Messaging;
using ComTest.Shared.Querying;
using ComTest.Shared.Results;
using Microsoft.Extensions.DependencyInjection;

namespace ComTest.UnitTests.Domain;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(10.005, 10.01)]
    [InlineData(10.004, 10.00)]
    [InlineData(0, 0)]
    public void Of_RoundsToTwoDecimals(decimal input, decimal expected) =>
        Assert.Equal(expected, Money.Of(input, "TMT").Amount);

    [Fact]
    public void Of_RejectsNegativeAmountAndUnknownCurrency()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Of(-0.01m, "TMT"));
        Assert.Throws<ArgumentException>(() => Money.Of(1m, "EUR"));
        Assert.Throws<ArgumentException>(() => Money.Of(1m, "tmt"));
    }

    [Fact]
    public void Arithmetic_RefusesToMixCurrencies()
    {
        var tmt = Money.Of(10m, "TMT");
        var usd = Money.Of(10m, "USD");

        Assert.Throws<InvalidOperationException>(() => tmt.Add(usd));
        Assert.Throws<InvalidOperationException>(() => tmt.Subtract(usd));
        Assert.Throws<InvalidOperationException>(() => tmt.SubtractUpToZero(usd));
    }

    [Fact]
    public void Subtract_NeverProducesANegativeAmount()
    {
        var price = Money.Of(10m, "TMT");
        var discount = Money.Of(15m, "TMT");

        Assert.Throws<InvalidOperationException>(() => price.Subtract(discount));
        Assert.True(price.SubtractUpToZero(discount).IsZero);
    }

    [Fact]
    public void Multiply_AndPercent_ValidateTheirInput()
    {
        var price = Money.Of(19.99m, "TMT");

        Assert.Equal(59.97m, price.Multiply(3).Amount);
        Assert.Equal(2.00m, price.Percent(10m).Amount);
        Assert.Throws<ArgumentOutOfRangeException>(() => price.Multiply(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => price.Percent(101m));
        Assert.Throws<ArgumentOutOfRangeException>(() => price.Percent(-1m));
    }

    [Fact]
    public void Equality_ComparesAmountAndCurrency()
    {
        Assert.Equal(Money.Of(1.50m, "TMT"), Money.Of(1.5m, "TMT"));
        Assert.NotEqual(Money.Of(1.5m, "TMT"), Money.Of(1.5m, "USD"));
    }
}

public sealed class ResultTests
{
    [Fact]
    public void Success_ExposesValue()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Failure_ThrowsWhenValueIsRead()
    {
        Result<int> result = Error.NotFound("test.missing");

        Assert.True(result.IsFailure);
        Assert.Equal("test.missing", result.Error!.Code);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }
}

public sealed class EntityTests
{
    private sealed class SampleEntity : Entity;

    [Fact]
    public void NewEntities_GetTimeOrderedVersion7Ids()
    {
        var first = new SampleEntity();

        Assert.Equal(7, first.Id.Version);
        Assert.NotEqual(first.Id, new SampleEntity().Id);
    }
}

public sealed class SortMapTests
{
    private sealed record Row(string Name, int Price);

    private static readonly IQueryable<Row> Rows = new[] { new Row("b", 1), new Row("a", 2), new Row("c", 3) }.AsQueryable();

    private static readonly SortMap<Row> Map = new SortMap<Row>().Add("name", row => row.Name).Add("price", row => row.Price);

    [Fact]
    public void Apply_SortsByAllowedFields()
    {
        Assert.Equal(["a", "b", "c"], Map.Apply(Rows, "name", row => row.Name).Value.Select(row => row.Name));
        Assert.Equal(["c", "a", "b"], Map.Apply(Rows, "-price", row => row.Name).Value.Select(row => row.Name));
    }

    [Theory]
    [InlineData("password")]
    [InlineData("name;drop table users")]
    [InlineData("-unknown")]
    public void Apply_RejectsFieldsOutsideTheAllowlist(string sort)
    {
        var result = Map.Apply(Rows, sort, row => row.Name);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCodes.Common.InvalidSort, result.Error!.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Apply_UsesFallbackWithoutSort(string? sort) =>
        Assert.Equal(["a", "b", "c"], Map.Apply(Rows, sort, row => row.Name).Value.Select(row => row.Name));
}

public sealed class PagedResultTests
{
    [Theory]
    [InlineData(45, 20, 1, 3, true)]
    [InlineData(40, 20, 2, 2, false)]
    [InlineData(0, 20, 1, 0, false)]
    public void Pages_AreCalculated(long total, int size, int page, int expectedPages, bool expectedHasNext)
    {
        var result = new PagedResult<int>([], page, size, total);

        Assert.Equal(expectedPages, result.TotalPages);
        Assert.Equal(expectedHasNext, result.HasNext);
    }

    [Fact]
    public void Skip_FollowsPageAndSize() =>
        Assert.Equal(40, new PageRequest { Page = 3, PageSize = 20 }.Skip);
}

public sealed record PingQuery : IQuery<string>;

public sealed record PingCommand : ICommand<string>;

public sealed class PingHandler : IQueryHandler<PingQuery, string>, ICommandHandler<PingCommand, string>
{
    public Task<Result<string>> HandleAsync(PingQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success("query"));

    public Task<Result<string>> HandleAsync(PingCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success("command"));
}

public sealed class MessageHandlerRegistrationTests
{
    [Fact]
    public async Task AddMessageHandlers_RegistersCommandAndQueryHandlers()
    {
        await using var provider = new ServiceCollection()
            .AddMessageHandlers(typeof(PingHandler).Assembly)
            .BuildServiceProvider();

        await using var scope = provider.CreateAsyncScope();

        var query = scope.ServiceProvider.GetRequiredService<IQueryHandler<PingQuery, string>>();
        var command = scope.ServiceProvider.GetRequiredService<ICommandHandler<PingCommand, string>>();

        Assert.Equal("query", (await query.HandleAsync(new PingQuery(), CancellationToken.None)).Value);
        Assert.Equal("command", (await command.HandleAsync(new PingCommand(), CancellationToken.None)).Value);
    }
}
