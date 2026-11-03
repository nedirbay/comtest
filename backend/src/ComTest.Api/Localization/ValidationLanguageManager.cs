using FluentValidation.Resources;

namespace ComTest.Api.Localization;

/// <summary>Marker for the ErrorMessages resources: English in the base file, plus tk and ru.</summary>
public sealed class ErrorMessages;

/// <summary>FluentValidation ships Russian and English; this adds Turkmen.</summary>
public sealed class ValidationLanguageManager : LanguageManager
{
    private const string Turkmen = "tk";

    public ValidationLanguageManager()
    {
        AddTranslation(Turkmen, "NotNullValidator", "'{PropertyName}' hökmany.");
        AddTranslation(Turkmen, "NotEmptyValidator", "'{PropertyName}' boş bolmaly däl.");
        AddTranslation(Turkmen, "EmailValidator", "'{PropertyName}' dogry e-poçta salgysy däl.");
        AddTranslation(Turkmen, "LengthValidator", "'{PropertyName}' {MinLength} bilen {MaxLength} simwol aralygynda bolmaly.");
        AddTranslation(Turkmen, "MinimumLengthValidator", "'{PropertyName}' azyndan {MinLength} simwol bolmaly.");
        AddTranslation(Turkmen, "MaximumLengthValidator", "'{PropertyName}' köp bolsa {MaxLength} simwol bolmaly.");
        AddTranslation(Turkmen, "RegularExpressionValidator", "'{PropertyName}' dogry görnüşde däl.");
        AddTranslation(Turkmen, "PredicateValidator", "'{PropertyName}' şerte laýyk gelmeýär.");
        AddTranslation(Turkmen, "AsyncPredicateValidator", "'{PropertyName}' şerte laýyk gelmeýär.");
        AddTranslation(Turkmen, "GreaterThanValidator", "'{PropertyName}' {ComparisonValue} bahasyndan uly bolmaly.");
        AddTranslation(Turkmen, "GreaterThanOrEqualValidator", "'{PropertyName}' {ComparisonValue} bahasyndan kiçi bolmaly däl.");
        AddTranslation(Turkmen, "LessThanValidator", "'{PropertyName}' {ComparisonValue} bahasyndan kiçi bolmaly.");
        AddTranslation(Turkmen, "LessThanOrEqualValidator", "'{PropertyName}' {ComparisonValue} bahasyndan uly bolmaly däl.");
        AddTranslation(Turkmen, "InclusiveBetweenValidator", "'{PropertyName}' {From} bilen {To} aralygynda bolmaly.");
        AddTranslation(Turkmen, "EqualValidator", "'{PropertyName}' '{ComparisonValue}' bilen deň bolmaly.");
        AddTranslation(Turkmen, "NotEqualValidator", "'{PropertyName}' '{ComparisonValue}' bilen deň bolmaly däl.");
        AddTranslation(Turkmen, "EnumValidator", "'{PropertyName}' rugsat berlen baha däl.");
    }
}
