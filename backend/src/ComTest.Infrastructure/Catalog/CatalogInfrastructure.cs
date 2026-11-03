using ComTest.Application.Abstractions;
using Ganss.Xss;
using Microsoft.Extensions.DependencyInjection;

namespace ComTest.Infrastructure.Catalog;

/// <summary>Allows the formatting tags a description needs and strips everything else, including scripts and event handlers.</summary>
public sealed class HtmlCleaner : IHtmlCleaner
{
    private static readonly HtmlSanitizer Sanitizer = CreateSanitizer();

    public string Clean(string html) => Sanitizer.Sanitize(html);

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "h2", "h3", "h4", "blockquote",
                "table", "thead", "tbody", "tr", "th", "td", "a", "img", "span",
            },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src", "alt", "title", "colspan", "rowspan" },
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "https", "http" },
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "href", "src" },
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        });

        return sanitizer;
    }
}

public static class CatalogServiceRegistration
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IHtmlCleaner, HtmlCleaner>();
        services.AddHybridCache();

        return services;
    }
}
