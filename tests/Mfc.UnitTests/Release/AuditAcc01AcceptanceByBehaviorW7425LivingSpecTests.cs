using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-425: AUDIT-ACC-01 — acceptance by behavior not file presence (audit F14).</summary>
public sealed class AuditAcc01AcceptanceByBehaviorW7425LivingSpecTests
{
    [Fact]
    public void Ac1ThreeLayerAcceptanceAndBehavioralChrGates()
    {
        string root = RepoRoot();
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string acceptance = File.ReadAllText(Path.Combine(root, "docs/release/mvp-acceptance.md"));
        string gates = File.ReadAllText(Path.Combine(root, "docs/release/release-gates.md"));
        string mvp = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/Release/MvpReleaseAcceptanceLivingSpecTests.cs"));
        string workflow = File.ReadAllText(Path.Combine(root, ".github/workflows/routeros-integration.yml"));

        Assert.Contains("AUDIT-ACC-01", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-425 (#1245) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-426 (#1247) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-427 (#1248) OPEN (NEXT)", plan62, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-425 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-ACC-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-425", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-ACC-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-427 (#1248)", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-425", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditAcc01AcceptanceByBehaviorW7425", testing, StringComparison.Ordinal);

        Assert.Contains("Acceptance layers (AUDIT-ACC-01 / F14)", acceptance, StringComparison.Ordinal);
        Assert.Contains("**A — Implementation / issue-queue**", acceptance, StringComparison.Ordinal);
        Assert.Contains("**B — Unit / integration behavior**", acceptance, StringComparison.Ordinal);
        Assert.Contains("**C — Live acceptance**", acceptance, StringComparison.Ordinal);
        Assert.Contains("NOT SATISFIED", acceptance, StringComparison.Ordinal);
        Assert.Contains("file-existence alone", acceptance, StringComparison.Ordinal);
        Assert.Contains("CHR skeleton contracts", acceptance, StringComparison.Ordinal);

        Assert.Contains("Layer B behavior, not Layer C", gates, StringComparison.Ordinal);
        Assert.Contains("AuditAcc01AcceptanceByBehaviorW7425LivingSpecTests", gates, StringComparison.Ordinal);
        Assert.Contains("absence is NOT a PASS", gates, StringComparison.Ordinal);

        Assert.Contains("Ac3ChrUnitIntegrationIsBehavioralAndLiveRemainsNotSatisfied", mvp, StringComparison.Ordinal);
        Assert.Contains("Ac4PhysicalCrsUnitIntegrationIsBehavioralNotHardwareProof", mvp, StringComparison.Ordinal);
        Assert.Contains("ExecuteStandaloneDeploymentUseCase.ExecuteAsync", mvp, StringComparison.Ordinal);
        Assert.DoesNotContain("Ac3ChrMatrixSubstitutedByE2ELivingSpecs", mvp, StringComparison.Ordinal);
        Assert.DoesNotContain("Ac4PhysicalCrsSubstitutedByScriptedFixture", mvp, StringComparison.Ordinal);

        Assert.Contains("not live acceptance", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("chr-skeleton-contracts", workflow, StringComparison.Ordinal);
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
