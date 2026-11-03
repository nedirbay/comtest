namespace ComTest.Shared.Results;

public enum ErrorType
{
    Failure,
    Validation,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,
    TooManyRequests,
}

/// <summary>
/// A stable, machine-readable failure. <see cref="Code"/> is also the localization key,
/// so user-facing text is resolved at the API boundary and never comes from internals.
/// </summary>
public sealed record Error(string Code, ErrorType Type, IReadOnlyDictionary<string, string[]>? Details = null)
{
    public static Error Failure(string code) => new(code, ErrorType.Failure);

    public static Error Validation(string code, IReadOnlyDictionary<string, string[]>? details = null) =>
        new(code, ErrorType.Validation, details);

    public static Error Unauthorized(string code) => new(code, ErrorType.Unauthorized);

    public static Error Forbidden(string code) => new(code, ErrorType.Forbidden);

    public static Error NotFound(string code) => new(code, ErrorType.NotFound);

    public static Error Conflict(string code) => new(code, ErrorType.Conflict);

    public static Error TooManyRequests(string code) => new(code, ErrorType.TooManyRequests);
}
