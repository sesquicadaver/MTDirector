using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-313: known-limitations / queue seed locked CTRL-HTTP-HEALTH-01 (W7-314) after PLAN-42 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7313LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedCtrlHttpHealth01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan42 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-42-controller-http-health-probes.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("Intentional residual (W7-313 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-HEALTH-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-314", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-315", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-314**", limitations, StringComparison.Ordinal);

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
        Assert.Contains("§3.C NEXT = W7-354 (#1114)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-313", plan, StringComparison.Ordinal);
        Assert.Contains("W7-314", plan, StringComparison.Ordinal);
        Assert.Contains("W7-315", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-HEALTH-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-354 (#1114)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-313 (#1032) DONE", plan42, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-HEALTH-01", plan42, StringComparison.Ordinal);
        Assert.Contains("W7-314", plan42, StringComparison.Ordinal);
        Assert.Contains("W7-315", plan42, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-354 (#1114)", plan42, StringComparison.Ordinal);

        Assert.Contains("MapGrpcHealthChecksService", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
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
