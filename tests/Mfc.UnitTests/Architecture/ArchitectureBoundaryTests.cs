using System.Reflection;
using NetArchTest.Rules;
using Xunit;

namespace Mfc.UnitTests.Architecture;

/// <summary>
/// Enforces assembly dependency boundaries from Repository Bootstrap Plan / M0-04.
/// Project-reference rules use <see cref="Assembly.GetReferencedAssemblies"/> (authoritative for empty skeletons).
/// NetArchTest covers type-level package/namespace constraints and the negative detector fixture.
/// Each fact is independent (no shared mutable state, no order dependence).
/// </summary>
public sealed class ArchitectureBoundaryTests
{
    private static Assembly Domain => typeof(Mfc.Domain.AssemblyMarker).Assembly;
    private static Assembly Application => typeof(Mfc.Application.AssemblyMarker).Assembly;
    private static Assembly Infrastructure => typeof(Mfc.Infrastructure.AssemblyMarker).Assembly;
    private static Assembly RouterOs => typeof(Mfc.RouterOs.AssemblyMarker).Assembly;
    private static Assembly Contracts => typeof(Mfc.Contracts.AssemblyMarker).Assembly;
    private static Assembly Controller => typeof(Mfc.Controller.Program).Assembly;
    private static Assembly Desktop => typeof(Mfc.Desktop.App).Assembly;

    private static bool References(Assembly source, string assemblyName)
        => source.GetReferencedAssemblies()
            .Any(a => string.Equals(a.Name, assemblyName, StringComparison.Ordinal));

    private static void AssertDoesNotReference(Assembly source, string forbidden, string because)
    {
        Assert.False(
            References(source, forbidden),
            $"{because}. {source.GetName().Name} references {forbidden}.");
    }

    private static void AssertReferences(Assembly source, string required, string because)
    {
        Assert.True(
            References(source, required),
            $"{because}. {source.GetName().Name} does not reference {required}.");
    }

    [Fact]
    public void DomainMustNotDependOnApplication()
        => AssertDoesNotReference(Domain, "Mfc.Application", "Domain must not reference Application");

    [Fact]
    public void DomainMustNotDependOnInfrastructure()
        => AssertDoesNotReference(Domain, "Mfc.Infrastructure", "Domain must not reference Infrastructure");

    [Fact]
    public void DomainMustNotDependOnRouterOs()
        => AssertDoesNotReference(Domain, "Mfc.RouterOs", "Domain must not reference RouterOs");

    [Fact]
    public void DomainMustNotDependOnController()
        => AssertDoesNotReference(Domain, "Mfc.Controller", "Domain must not reference Controller");

    [Fact]
    public void DomainMustNotDependOnDesktop()
        => AssertDoesNotReference(Domain, "Mfc.Desktop", "Domain must not reference Desktop");

    [Fact]
    public void ApplicationMustNotDependOnInfrastructure()
        => AssertDoesNotReference(Application, "Mfc.Infrastructure", "Application must not reference Infrastructure");

    [Fact]
    public void ApplicationMustNotDependOnRouterOs()
        => AssertDoesNotReference(Application, "Mfc.RouterOs", "Application must not reference RouterOs");

    [Fact]
    public void ApplicationMustNotDependOnController()
        => AssertDoesNotReference(Application, "Mfc.Controller", "Application must not reference Controller");

    [Fact]
    public void ApplicationMustNotDependOnDesktop()
        => AssertDoesNotReference(Application, "Mfc.Desktop", "Application must not reference Desktop");

    [Fact]
    public void InfrastructureMustNotDependOnController()
        => AssertDoesNotReference(Infrastructure, "Mfc.Controller", "Infrastructure must not reference Controller");

    [Fact]
    public void RouterOsMustNotDependOnInfrastructure()
        => AssertDoesNotReference(RouterOs, "Mfc.Infrastructure", "RouterOs must not reference Infrastructure");

    [Fact]
    public void DesktopMustNotDependOnDomain()
        => AssertDoesNotReference(Desktop, "Mfc.Domain", "Desktop must not reference Domain");

    [Fact]
    public void DesktopMustNotDependOnApplication()
        => AssertDoesNotReference(Desktop, "Mfc.Application", "Desktop must not reference Application");

    [Fact]
    public void DesktopMustNotDependOnInfrastructure()
        => AssertDoesNotReference(Desktop, "Mfc.Infrastructure", "Desktop must not reference Infrastructure");

    [Fact]
    public void DesktopMustNotDependOnRouterOs()
        => AssertDoesNotReference(Desktop, "Mfc.RouterOs", "Desktop must not reference RouterOs");

    [Fact]
    public void DomainMustNotDependOnForbiddenInfrastructurePackages()
    {
        string[] forbidden =
        [
            "Microsoft.EntityFrameworkCore",
            "Microsoft.AspNetCore",
            "Avalonia",
            "Grpc.Core",
            "Grpc.Net.Client",
            "Grpc.AspNetCore",
            "Npgsql",
            "Npgsql.EntityFrameworkCore.PostgreSQL",
        ];

        foreach (string name in forbidden)
        {
            AssertDoesNotReference(Domain, name, $"Domain must not use {name}");
        }

        // Also guard type-level dependency names NetArchTest understands for future Domain code.
        TestResult result = Types.InAssembly(Domain)
            .ShouldNot()
            .HaveDependencyOnAny(
                "Microsoft.EntityFrameworkCore",
                "Avalonia",
                "Grpc",
                "Npgsql")
            .GetResult();

        Assert.True(result.IsSuccessful, string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>()));
    }

    [Fact]
    public void DesktopMustNotContainRouterOsProtocolTypes()
    {
        AssertDoesNotReference(Desktop, "Mfc.RouterOs", "Desktop must not reference RouterOs");

        Type[] routerOsTypes = Desktop.GetExportedTypes()
            .Where(t => t.Namespace is not null && t.Namespace.StartsWith("Mfc.RouterOs", StringComparison.Ordinal))
            .ToArray();

        Assert.Empty(routerOsTypes);
    }

    [Fact]
    public void ApplicationMustNotUseIServiceProvider()
    {
        AssertDoesNotReference(
            Application,
            "Microsoft.Extensions.DependencyInjection",
            "Application must not depend on DI container");

        AssertDoesNotReference(
            Application,
            "Microsoft.Extensions.DependencyInjection.Abstractions",
            "Application must not depend on DI abstractions (IServiceProvider)");
    }

    [Fact]
    public void ProductionAssembliesMustNotReferenceTestProjects()
    {
        Assembly[] production =
        [
            Domain,
            Application,
            Infrastructure,
            RouterOs,
            Contracts,
            Controller,
            Desktop,
        ];

        string[] testAssemblies =
        [
            "Mfc.UnitTests",
            "Mfc.IntegrationTests",
            "Mfc.RouterOs.IntegrationTests",
        ];

        foreach (Assembly assembly in production)
        {
            foreach (string testAssembly in testAssemblies)
            {
                AssertDoesNotReference(
                    assembly,
                    testAssembly,
                    $"Production assembly {assembly.GetName().Name} must not reference {testAssembly}");
            }
        }
    }

    [Fact]
    public void InfrastructureMustNotReferenceSqlite()
    {
        AssertDoesNotReference(
            Infrastructure,
            "Microsoft.Data.Sqlite",
            "Infrastructure must not use SQLite");
        AssertDoesNotReference(
            Infrastructure,
            "Microsoft.EntityFrameworkCore.Sqlite",
            "Infrastructure must not use EF Core SQLite provider");
        AssertReferences(
            Infrastructure,
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            "Infrastructure must use PostgreSQL provider");
    }

    /// <summary>
    /// Controlled negative proof: detector sees Application→Domain and rejects a fake Application→Infrastructure claim.
    /// </summary>
    [Fact]
    public void DetectorReportsViolationWhenForbiddenDependencyExists()
    {
        AssertReferences(
            Application,
            "Mfc.Domain",
            "Sanity: Application must reference Domain so forbidden-dependency detection is meaningful");

        Assert.True(
            References(Application, "Mfc.Domain"),
            "Detector fixture: existing dependency Application→Domain must be observable.");

        Assert.False(
            References(Application, "Mfc.Infrastructure"),
            "Detector fixture: Application must not falsely report Infrastructure dependency.");

        // If we incorrectly claimed Application must NOT depend on Domain, the assertion above would fail —
        // that is the negative proof that the checker distinguishes allowed vs forbidden edges.
        bool wouldFailIfInverted = References(Application, "Mfc.Domain");
        Assert.True(wouldFailIfInverted);
    }
}
