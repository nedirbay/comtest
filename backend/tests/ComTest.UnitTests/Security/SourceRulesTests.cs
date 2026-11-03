using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace ComTest.UnitTests.Security;

/// <summary>Rules about the source code itself, which the compiler cannot enforce.</summary>
public sealed partial class SourceRulesTests
{
    [Fact]
    public void Source_DoesNotUseRawSqlStringApis()
    {
        var offenders = SourceFiles().Where(file => RawSqlPattern().IsMatch(File.ReadAllText(file))).ToList();

        Assert.True(offenders.Count == 0, "Use the parameterized FromSql/ExecuteSql instead: " + string.Join(", ", offenders));
    }

    [Fact]
    public void Source_DoesNotEnableSensitiveDataLogging()
    {
        var offenders = SourceFiles().Where(file => File.ReadAllText(file).Contains("EnableSensitiveDataLogging", StringComparison.Ordinal)).ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void IgnoreQueryFilters_IsNeverUsedWithoutNamingTheFilter()
    {
        // The parameterless form would also drop the tenant filter.
        var offenders = SourceFiles().Where(file => File.ReadAllText(file).Contains("IgnoreQueryFilters()", StringComparison.Ordinal)).ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheTenantFilter_IsNeverIgnored()
    {
        var offenders = SourceFiles().Where(file => TenantFilterIgnored().IsMatch(File.ReadAllText(file))).ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void PlatformAccess_IsTurnedOnOnlyInItsOneSanctionedPlace()
    {
        string[] allowed = ["PlatformDataAccess.cs", "TenantContext.cs"];

        var offenders = SourceFiles()
            .Where(file => File.ReadAllText(file).Contains("UsePlatformAccess", StringComparison.Ordinal))
            .Where(file => !allowed.Contains(Path.GetFileName(file)))
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void AppSettings_ContainNoSecrets()
    {
        var offenders = Directory
            .GetFiles(Path.Combine(BackendRoot(), "src"), "appsettings*.json", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Where(file => SecretLikePattern().IsMatch(File.ReadAllText(file)))
            .ToList();

        Assert.Empty(offenders);
    }

    private static IEnumerable<string> SourceFiles() =>
        Directory.GetFiles(Path.Combine(BackendRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file));

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string BackendRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", ".."));

    [GeneratedRegex(@"IgnoreQueryFilters\(\[[^\]]*QueryFilters\.Tenant")]
    private static partial Regex TenantFilterIgnored();

    [GeneratedRegex(@"\b(FromSqlRaw|ExecuteSqlRaw|ExecuteSqlRawAsync|SqlQueryRaw)\b")]
    private static partial Regex RawSqlPattern();

    [GeneratedRegex("(?i)(password\\s*=|\"password\"\\s*:|BEGIN (EC )?PRIVATE KEY|\"secret\"\\s*:)")]
    private static partial Regex SecretLikePattern();
}
