using System.Globalization;

namespace ComTest.Shared.Domain;

/// <summary>
/// A non-negative amount in one currency, rounded to 2 decimals. A negative total or a
/// mixed-currency sum throws, so such bugs stop the operation instead of reaching a payment.
/// </summary>
public sealed record Money
{
    private const int Scale = 2;

    private static readonly HashSet<string> SupportedCurrencies = new(StringComparer.Ordinal) { "TMT", "USD" };

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public decimal Amount { get; private init; }

    public string Currency { get; private init; }

    public bool IsZero => Amount == 0m;

    public static bool IsSupportedCurrency(string? currency) =>
        currency is not null && SupportedCurrencies.Contains(currency);

    public static Money Of(decimal amount, string currency)
    {
        if (!IsSupportedCurrency(currency))
        {
            throw new ArgumentException($"Unsupported currency '{currency}'.", nameof(currency));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        return new Money(Round(amount), currency);
    }

    public static Money Zero(string currency) => Of(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);

        var result = Amount - other.Amount;

        return result < 0m
            ? throw new InvalidOperationException("Subtraction would produce a negative amount.")
            : new Money(result, Currency);
    }

    /// <summary>Subtracts as much as possible and stops at zero, as a discount should.</summary>
    public Money SubtractUpToZero(Money other)
    {
        EnsureSameCurrency(other);

        return new Money(Math.Max(Amount - other.Amount, 0m), Currency);
    }

    public Money Multiply(int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);

        return new Money(Amount * quantity, Currency);
    }

    public Money Percent(decimal percent)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(percent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(percent, 100m);

        return new Money(Round(Amount * percent / 100m), Currency);
    }

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Amount:0.00} {Currency}");

    private static decimal Round(decimal amount) => Math.Round(amount, Scale, MidpointRounding.AwayFromZero);

    private void EnsureSameCurrency(Money other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Cannot combine {Currency} with {other.Currency}.");
        }
    }
}
