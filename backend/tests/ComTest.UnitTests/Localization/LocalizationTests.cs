using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using ComTest.Api.Localization;
using ComTest.Shared.Results;

namespace ComTest.UnitTests.Localization;

public sealed class LocalizationTests
{
    public static TheoryData<string> Languages => new() { string.Empty, ".tk", ".ru" };

    [Theory]
    [MemberData(nameof(Languages))]
    public void EveryErrorCode_HasATranslation(string language)
    {
        var translated = ReadKeys(language);
        var missing = AllErrorCodes().Where(code => !translated.Contains(code)).ToList();

        Assert.True(missing.Count == 0, $"Missing in ErrorMessages{language}.resx: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Translations_HaveNoUnknownCodes(string language)
    {
        var known = AllErrorCodes().ToHashSet(StringComparer.Ordinal);
        var unknown = ReadKeys(language).Where(key => !known.Contains(key)).ToList();

        Assert.True(unknown.Count == 0, $"Unknown in ErrorMessages{language}.resx: {string.Join(", ", unknown)}");
    }

    [Theory]
    [InlineData("tk")]
    [InlineData("ru")]
    public void Translations_AreNotCopiesOfEnglish(string language)
    {
        var english = ReadValues(string.Empty);
        var translated = ReadValues("." + language);
        var untranslated = translated.Where(pair => pair.Value == english[pair.Key]).Select(pair => pair.Key).ToList();

        Assert.True(untranslated.Count == 0, $"Same as English in {language}: {string.Join(", ", untranslated)}");
    }

    [Theory]
    [InlineData("tk", "NotEmptyValidator", "boş")]
    [InlineData("ru", "NotEmptyValidator", "должно")]
    [InlineData("en", "NotEmptyValidator", "must not")]
    public void ValidationMessages_FollowTheLanguage(string language, string validator, string expected)
    {
        var message = new ValidationLanguageManager().GetString(validator, CultureInfo.GetCultureInfo(language));

        Assert.Contains(expected, message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TurkmenCulture_IsAvailableOnThisRuntime() =>
        Assert.Equal("tk", CultureInfo.GetCultureInfo("tk").TwoLetterISOLanguageName);

    private static IEnumerable<string> AllErrorCodes() =>
        typeof(ErrorCodes).GetNestedTypes()
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

    private static HashSet<string> ReadKeys(string language) => ReadValues(language).Keys.ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> ReadValues(string language) =>
        XDocument.Load(ResourcePath(language)).Root!.Elements("data").ToDictionary(
            element => element.Attribute("name")!.Value,
            element => element.Element("value")!.Value,
            StringComparer.Ordinal);

    private static string ResourcePath(string language, [CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(path)!, "..", "..", "..", "src", "ComTest.Api", "Localization", $"ErrorMessages{language}.resx"));
}
