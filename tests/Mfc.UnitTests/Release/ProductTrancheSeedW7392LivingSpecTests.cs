using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-392: freeze closed. The linear queue has no next planned row.
/// No plan-62 markdown and no invented successor.
/// </summary>
public sealed class ProductTrancheSeedW7392LivingSpecTests
{
    [Fact]
    public void Ac1FreezeClosedAndQueueExhausted()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan61 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-61-desktop-connection-disconnected-fault-text.md"));

        Assert.Contains("Intentional residual (W7-392 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-392 (#1191) DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", limitations, StringComparison.Ordinal);
        Assert.Contains("No `plan-62-*.md`", limitations, StringComparison.Ordinal);
        Assert.Contains("stops", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-392 | [#1191](https://github.com/sesquicadaver/MTDirector/issues/1191) | Freeze — no further correlation-id / fault-text plans without a pre-existing TOR | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **0** |", roadmap, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", roadmap, StringComparison.Ordinal);
        Assert.DoesNotContain("§3.C NEXT = W7-392 (#1191)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-392 (#1191) DONE", plan61, StringComparison.Ordinal);
        Assert.Contains("no `docs/planning/plan-62-*.md`", plan61, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", plan61, StringComparison.Ordinal);
        Assert.DoesNotContain("PLAN-62 —", plan61, StringComparison.Ordinal);

        Assert.Contains("Freeze **W7-392 (#1191) DONE**", plan, StringComparison.Ordinal);
        Assert.Contains("Queue exhausted", plan, StringComparison.Ordinal);
        Assert.Contains("No `plan-62-*.md`", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = none (queue exhausted; W7-392 #1191 DONE)", plan, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(root, "docs/planning/plan-62-desktop-correlation-id.md")));
        Assert.Empty(Directory.GetFiles(Path.Combine(root, "docs/planning"), "plan-62-*.md"));
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
