using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-191: known-limitations / queue seed locks next PLAN-22 row (W7-192 DESK-A11Y-POLICY-ACK-02).</summary>
public sealed class ProductTrancheSeedW7191LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskA11yPolicyAck02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan22 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-22-desktop-policies-ack-record-automation.md"));

        Assert.Contains("Intentional residual (W7-191 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-ACK-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-192", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-192", roadmap, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-ACK-02 — Policies ack/record Names + authoring residual + lifecycle + shell/Incident Names regression Living Spec", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-192", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-ACK-02", plan, StringComparison.Ordinal);
        Assert.Contains("W7-191 DONE", plan22, StringComparison.Ordinal);
        Assert.Contains("DESK-A11Y-POLICY-ACK-02", plan22, StringComparison.Ordinal);
        Assert.Contains("W7-192", plan22, StringComparison.Ordinal);
        Assert.Contains("W7-191", roadmap, StringComparison.Ordinal);
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
