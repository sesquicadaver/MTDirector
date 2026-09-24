using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-311: PLAN-41 COMPLETE; known-limitations / queue seed locked PLAN-42 inventory (W7-312)
/// and follow-up seed W7-313 after QG-SIGN-02.
/// </summary>
public sealed class ProductTrancheSeedW7311LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan42AfterPlan41Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan41 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-41-release-signing-crypto-gpg-sigstore.md"));
        string plan42 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-42-controller-http-health-probes.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string cryptoScript = Path.Combine(root, "scripts/release/sign-sha256sums-crypto.sh");

        Assert.Contains("Intentional residual (W7-311 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-41 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-42", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-312", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-313", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-HEALTH-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-312**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-311 | [#1028](https://github.com/sesquicadaver/MTDirector/issues/1028) | Seed next after QG-SIGN-02 (PLAN-41 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-312 | [#1031](https://github.com/sesquicadaver/MTDirector/issues/1031) | PLAN-42 — Inventory Controller HTTP liveness/readiness probes beyond gRPC health | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-313 | [#1032](https://github.com/sesquicadaver/MTDirector/issues/1032) | Seed first PLAN-42 atomic row after inventory → CTRL-HTTP-HEALTH-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-423 (#1242)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-41 COMPLETE", plan41, StringComparison.Ordinal);
        Assert.Contains("W7-311 (#1028) DONE", plan41, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-423 (#1242)", plan41, StringComparison.Ordinal);
        Assert.Contains("plan-42-controller-http-health-probes.md", plan41, StringComparison.Ordinal);

        Assert.Contains("PLAN-42", plan, StringComparison.Ordinal);
        Assert.Contains("W7-312", plan, StringComparison.Ordinal);
        Assert.Contains("W7-311 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-423 (#1242)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-42-controller-http-health-probes.md", plan, StringComparison.Ordinal);

        Assert.Contains("CTRL-HTTP-HEALTH-01", plan42, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan42, StringComparison.Ordinal);
        Assert.Contains("W7-312", plan42, StringComparison.Ordinal);
        Assert.Contains("W7-313", plan42, StringComparison.Ordinal);
        Assert.Contains("W7-314", plan42, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-423 (#1242)", plan42, StringComparison.Ordinal);
        Assert.Contains("MapGrpcHealthChecksService", plan42, StringComparison.Ordinal);
        Assert.Contains("ad3718cb", plan42, StringComparison.Ordinal);

        Assert.Contains("MapGrpcHealthChecksService", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
        Assert.True(File.Exists(cryptoScript));
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
