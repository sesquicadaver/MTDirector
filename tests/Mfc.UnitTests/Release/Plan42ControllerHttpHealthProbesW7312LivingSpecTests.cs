using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-312: PLAN-42 inventory documents sole CTRL-HTTP-HEALTH-01 rank
/// (HTTP live/ready beyond gRPC) and opens HEALTH implement after seed.
/// </summary>
public sealed class Plan42ControllerHttpHealthProbesW7312LivingSpecTests
{
    [Fact]
    public void Ac1Plan42InventoryDocumentsSoleCtrlHttpHealth01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan42 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-42-controller-http-health-probes.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packagingDoc = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("PLAN-42 — Controller HTTP liveness/readiness probes (beyond gRPC health)", plan42, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan42, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-HEALTH-01", plan42, StringComparison.Ordinal);
        Assert.Contains("ad3718cb", plan42, StringComparison.Ordinal);
        Assert.Contains("W7-314", plan42, StringComparison.Ordinal);
        Assert.Contains("W7-313", plan42, StringComparison.Ordinal);
        Assert.Contains("W7-312", plan42, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan42, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/health/live", plan42, StringComparison.Ordinal);
        Assert.Contains("/health/ready", plan42, StringComparison.Ordinal);
        Assert.Contains("HttpProtocols.Http2", plan42, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-317 (#1040)", plan42, StringComparison.Ordinal);
        Assert.Contains("MapGrpcHealthChecksService", plan42, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-312 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-HEALTH-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-314", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-313", limitations, StringComparison.Ordinal);
        Assert.Contains("ad3718cb", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-312 | [#1031](https://github.com/sesquicadaver/MTDirector/issues/1031) | PLAN-42 — Inventory Controller HTTP liveness/readiness probes beyond gRPC health | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-313 | [#1032](https://github.com/sesquicadaver/MTDirector/issues/1032) | Seed first PLAN-42 atomic row after inventory → CTRL-HTTP-HEALTH-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-314 | [#1034](https://github.com/sesquicadaver/MTDirector/issues/1034) | CTRL-HTTP-HEALTH-01 — HTTP liveness/readiness probes beyond gRPC health | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-315 | [#1036](https://github.com/sesquicadaver/MTDirector/issues/1036) | Seed next after CTRL-HTTP-HEALTH-01 (PLAN-42 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-317 (#1040)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-313", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-314", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-42", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-42-controller-http-health-probes.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-42-controller-http-health-probes.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan42ControllerHttpHealthProbesW7312", testing, StringComparison.Ordinal);

        // Evidence surfaces remain present (inventory does not implement HTTP health yet).
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);
        Assert.Contains("MapGrpcHealthChecksService", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.Contains("HttpProtocols.Http1AndHttp2", program, StringComparison.Ordinal);
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
