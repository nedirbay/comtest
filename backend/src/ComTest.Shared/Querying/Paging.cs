using System.Linq.Expressions;
using ComTest.Shared.Results;

namespace ComTest.Shared.Querying;

public sealed record PageRequest
{
    public const int MaxPageSize = 100;

    private const int DefaultPageSize = 20;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = DefaultPageSize;

    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, long TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasNext => Page < TotalPages;
}

/// <summary>
/// Allowlist of sortable fields. Clients send names such as <c>name,-price</c>, so only the
/// columns listed here can reach ORDER BY, and unknown fields are rejected.
/// </summary>
public sealed class SortMap<T>
{
    private readonly Dictionary<string, Expression<Func<T, object?>>> _fields = new(StringComparer.OrdinalIgnoreCase);

    public SortMap<T> Add(string name, Expression<Func<T, object?>> selector)
    {
        _fields.Add(name, selector);

        return this;
    }

    public Result<IQueryable<T>> Apply(IQueryable<T> query, string? sort, Expression<Func<T, object?>> fallback)
    {
        ArgumentNullException.ThrowIfNull(query);

        IOrderedQueryable<T>? ordered = null;

        foreach (var token in (sort ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var descending = token[0] == '-';

            if (!_fields.TryGetValue(descending ? token[1..] : token, out var selector))
            {
                return Error.Validation(ErrorCodes.Common.InvalidSort);
            }

            ordered = (ordered, descending) switch
            {
                (null, false) => query.OrderBy(selector),
                (null, true) => query.OrderByDescending(selector),
                (_, false) => ordered.ThenBy(selector),
                (_, true) => ordered.ThenByDescending(selector),
            };
        }

        return Result.Success<IQueryable<T>>(ordered is null ? query.OrderBy(fallback) : ordered.ThenBy(fallback));
    }
}
