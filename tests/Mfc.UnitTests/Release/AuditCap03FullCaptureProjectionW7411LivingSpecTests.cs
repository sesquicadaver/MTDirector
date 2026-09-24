using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-411: AUDIT-CAP-03 — full capture projection (audit F08).</summary>
public sealed class AuditCap03FullCaptureProjectionW7411LivingSpecTests
{
    [Fact]
    public void Ac1FullFacilityRoutingDynamicFilterAndStrictMapRecord()
    {
        string root = RepoRoot();
        string projector = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Snapshot/DiscoveryCanonicalProjector.cs"));
        string executor = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Commands/RosReadCommandExecutor.cs"));
        string safety = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/GetDevicePolicySafetyAnalysisUseCase.cs"));
        string routingModels = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Discovery/RoutingDependencyModels.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.Contains("AUDIT-CAP-03", projector, StringComparison.Ordinal);
        Assert.Contains("BuildFacilityConfigurationProperties", projector, StringComparison.Ordinal);
        Assert.Contains("ProjectOrderedFilterObservation", projector, StringComparison.Ordinal);
        Assert.Contains("src-address", projector, StringComparison.Ordinal);
        Assert.Contains("dst-address", projector, StringComparison.Ordinal);

        Assert.Contains("TryMapRecord", executor, StringComparison.Ordinal);
        Assert.Contains("InvalidUtf8ErrorCode", executor, StringComparison.Ordinal);
        Assert.Contains("DuplicateAttributeErrorCode", executor, StringComparison.Ordinal);
        Assert.Contains("TryDecodeUtf8", executor, StringComparison.Ordinal);

        Assert.Contains("PreferEffectiveFilterSequence", safety, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-03", safety, StringComparison.Ordinal);

        Assert.Contains(".src-address", routingModels, StringComparison.Ordinal);
        Assert.Contains(".dst-address", routingModels, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-411 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-03 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-412", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-04", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-411 | [#1221](https://github.com/sesquicadaver/MTDirector/issues/1221) | AUDIT-CAP-03 — Full capture projection | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-412 | [#1224](https://github.com/sesquicadaver/MTDirector/issues/1224) | Seed next after AUDIT-CAP-03 → AUDIT-CAP-04 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-413 | [#1226](https://github.com/sesquicadaver/MTDirector/issues/1226) | AUDIT-CAP-04 — Capture attempt identity | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-414 | [#1228](https://github.com/sesquicadaver/MTDirector/issues/1228) | Seed next after AUDIT-CAP-04 → AUDIT-AN-03 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-415 | [#1229](https://github.com/sesquicadaver/MTDirector/issues/1229) | AUDIT-AN-03 — Server-owned analysis | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-CAP-03 W7-411 (#1221) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-412 (#1224) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-413 (#1226) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-414 (#1228) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-415 (#1229) OPEN (NEXT)", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", plan62, StringComparison.Ordinal);

        Assert.Contains("AUDIT-CAP-03 W7-411 (#1221) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", continuous, StringComparison.Ordinal);

        Assert.Contains("AuditCap03FullCaptureProjectionW7411", testing, StringComparison.Ordinal);
        Assert.Contains("| `W7-411` | #1221 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-412` | #1224 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-415 (#1229)", issues, StringComparison.Ordinal);
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
