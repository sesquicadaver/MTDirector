using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-328: PLAN-46 inventory documents sole CTRL-HTTP-OTEL-RESOURCE-01 rank
/// (OTel ResourceBuilder service.name/service.version after log↔trace correlation)
/// and opens RESOURCE implement after seed.
/// </summary>
public sealed class Plan46ControllerOtelResourceIdentityW7328LivingSpecTests
{
    [Fact]
    public void Ac1Plan46InventoryDocumentsSoleCtrlHttpOtelResource01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan46 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-46-controller-otel-resource-identity.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packagingDoc = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string logger = File.ReadAllText(Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Logging/RedactingJsonConsoleLoggerProvider.cs"));

        Assert.Contains("PLAN-46 — Controller OpenTelemetry resource identity after log↔trace correlation", plan46, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan46, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", plan46, StringComparison.Ordinal);
        Assert.Contains("894cc4b8", plan46, StringComparison.Ordinal);
        Assert.Contains("W7-330", plan46, StringComparison.Ordinal);
        Assert.Contains("W7-329", plan46, StringComparison.Ordinal);
        Assert.Contains("W7-328", plan46, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan46, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ResourceBuilder", plan46, StringComparison.Ordinal);
        Assert.Contains("service.name", plan46, StringComparison.Ordinal);
        Assert.Contains("service.version", plan46, StringComparison.Ordinal);
        Assert.Contains("opt-in", plan46, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("§3.C NEXT = W7-330 (#1066)", plan46, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-328 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-OTEL-RESOURCE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-330", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-329", limitations, StringComparison.Ordinal);
        Assert.Contains("894cc4b8", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-328 | [#1063](https://github.com/sesquicadaver/MTDirector/issues/1063) | PLAN-46 — Inventory Controller OpenTelemetry resource identity after log↔trace correlation | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-329 | [#1064](https://github.com/sesquicadaver/MTDirector/issues/1064) | Seed first PLAN-46 atomic row after inventory → CTRL-HTTP-OTEL-RESOURCE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-330 | [#1066](https://github.com/sesquicadaver/MTDirector/issues/1066) | CTRL-HTTP-OTEL-RESOURCE-01 — Controller OTel ResourceBuilder service.name/service.version | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-331 | [#1068](https://github.com/sesquicadaver/MTDirector/issues/1068) | Seed next after CTRL-HTTP-OTEL-RESOURCE-01 (PLAN-46 COMPLETE) | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-330 (#1066)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-329", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-330", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-46", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-46-controller-otel-resource-identity.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-46-controller-otel-resource-identity.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan46ControllerOtelResourceIdentityW7328", testing, StringComparison.Ordinal);

        // Evidence surfaces remain present (inventory does not implement resource identity yet).
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);
        Assert.Contains("/metrics", installation, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.Contains("/health/live", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("Activity.Current", logger, StringComparison.Ordinal);
        Assert.Contains("traceId", logger, StringComparison.Ordinal);
        Assert.DoesNotContain("ResourceBuilder", program, StringComparison.Ordinal);
        Assert.DoesNotContain("service.name", program, StringComparison.Ordinal);
        Assert.Contains("/health/live", packagingDoc, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "src/Mfc.Controller/Program.cs")));
    }

    private static string RepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
