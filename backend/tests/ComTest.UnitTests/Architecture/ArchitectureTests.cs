using System.Reflection;
using ComTest.Api.Hosting;
using ComTest.Infrastructure.Persistence;
using NetArchTest.Rules;

namespace ComTest.UnitTests.Architecture;

/// <summary>Layer and module boundaries, so that a forbidden dependency fails the build's tests.</summary>
public sealed class ArchitectureTests
{
    private const string Shared = "ComTest.Shared";
    private const string Domain = "ComTest.Domain";
    private const string Application = "ComTest.Application";
    private const string Infrastructure = "ComTest.Infrastructure";
    private const string Api = "ComTest.Api";

    private static readonly Assembly SharedAssembly = typeof(ComTest.Shared.Results.Result).Assembly;
    private static readonly Assembly DomainAssembly = typeof(ComTest.Domain.AssemblyMarker).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(ComTest.Application.AssemblyMarker).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(AppDbContext).Assembly;
    private static readonly Assembly ApiAssembly = typeof(ApiServiceExtensions).Assembly;

    [Fact]
    public void Shared_DependsOnNoOtherLayer() =>
        AssertNoDependency(SharedAssembly, Domain, Application, Infrastructure, Api);

    [Fact]
    public void Domain_DependsOnlyOnShared() =>
        AssertNoDependency(DomainAssembly, Application, Infrastructure, Api, "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore");

    [Fact]
    public void Application_DoesNotDependOnInfrastructureOrApi() =>
        AssertNoDependency(ApplicationAssembly, Infrastructure, Api, "Microsoft.AspNetCore", "Npgsql");

    [Fact]
    public void Infrastructure_DoesNotDependOnApi() =>
        AssertNoDependency(InfrastructureAssembly, Api);

    [Theory]
    [InlineData("ComTest.Domain.Modules")]
    [InlineData("ComTest.Application.Modules")]
    public void Modules_UseEachOtherOnlyThroughContracts(string modulesNamespace)
    {
        var assembly = modulesNamespace.StartsWith(Domain, StringComparison.Ordinal) ? DomainAssembly : ApplicationAssembly;

        var violations = FindModuleViolations(assembly, modulesNamespace);

        Assert.True(violations.Count == 0, "Module boundary broken: " + string.Join(", ", violations));
    }

    [Fact]
    public void ModuleRule_DetectsAViolationAndAllowsContracts()
    {
        var violations = FindModuleViolations(typeof(ArchitectureTests).Assembly, "ComTest.UnitTests.Architecture.Sample");

        var violation = Assert.Single(violations);
        Assert.Contains(nameof(Sample.Orders.BadOrderService), violation, StringComparison.Ordinal);
    }

    [Fact]
    public void CommandAndQueryHandlers_AreNotPublicApiOfTheApiLayer()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That().ResideInNamespace("ComTest.Api.Controllers")
            .ShouldNot().HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        Assert.True(result.IsSuccessful, "Controllers must not use EF Core directly: " + string.Join(", ", result.FailingTypeNames ?? []));
    }

    /// <summary>A module may reach another one only through that module's Contracts namespace.</summary>
    private static List<string> FindModuleViolations(Assembly assembly, string modulesNamespace)
    {
        var prefix = modulesNamespace + ".";

        var moduleTypes = assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(prefix, StringComparison.Ordinal) == true && type.FullName is not null)
            .ToList();

        var modules = moduleTypes
            .Select(type => type.Namespace![prefix.Length..].Split('.')[0])
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var violations = new List<string>();

        foreach (var module in modules)
        {
            var forbidden = moduleTypes
                .Where(type => !IsInModule(type, prefix + module) && !IsContract(type, prefix))
                .Select(type => type.FullName!)
                .ToArray();

            if (forbidden.Length == 0)
            {
                continue;
            }

            var result = Types.InAssembly(assembly)
                .That().ResideInNamespace(prefix + module)
                .ShouldNot().HaveDependencyOnAny(forbidden)
                .GetResult();

            violations.AddRange(result.FailingTypeNames ?? []);
        }

        return violations;
    }

    private static bool IsInModule(Type type, string moduleNamespace) =>
        type.Namespace == moduleNamespace
        || type.Namespace!.StartsWith(moduleNamespace + ".", StringComparison.Ordinal);

    private static bool IsContract(Type type, string prefix)
    {
        var parts = type.Namespace![prefix.Length..].Split('.');

        return parts.Length > 1 && parts[1] == "Contracts";
    }

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();

        Assert.True(result.IsSuccessful, "Forbidden dependency in: " + string.Join(", ", result.FailingTypeNames ?? []));
    }
}
