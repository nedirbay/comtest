using System.Linq.Expressions;
using ComTest.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ComTest.Infrastructure.Persistence;

public static class QueryFilters
{
    /// <summary>Hides soft-deleted rows. Ignore by name only where deleted rows are the point.</summary>
    public const string SoftDelete = nameof(SoftDelete);

    /// <summary>Limits rows to the current tenant. Row-level security enforces the same rule below it.</summary>
    public const string Tenant = nameof(Tenant);
}

public static class EntityBuilderExtensions
{
    /// <summary>Maps <see cref="Money"/> to <c>numeric(18,2)</c> plus a three-letter currency column.</summary>
    public static ComplexPropertyBuilder<Money> MapMoney<T>(
        this EntityTypeBuilder<T> builder,
        Expression<Func<T, Money?>> property)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        var money = builder.ComplexProperty(property);
        money.Property(value => value.Amount).HasPrecision(18, 2);
        money.Property(value => value.Currency).HasMaxLength(3).IsFixedLength();

        return money;
    }
}
