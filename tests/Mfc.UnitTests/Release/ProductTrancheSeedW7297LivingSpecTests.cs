using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-297: known-limitations / queue seed locked OPS-HOST-SYSUSERS-01 (W7-298) after PLAN-38 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7297LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedOpsHostSysusers01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan38 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-38-controller-host-sysusers-tmpfiles-packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));

        Assert.Contains("Intentional residual (W7-297 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-298", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-299", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-298**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-297 | [#1000](https://github.com/sesquicadaver/MTDirector/issues/1000) | Seed first PLAN-38 atomic row after inventory → OPS-HOST-SYSUSERS-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-298 | [#1002](https://github.com/sesquicadaver/MTDirector/issues/1002) | OPS-HOST-SYSUSERS-01 — author sysusers.d/tmpfiles.d + docs + package-controller bundle | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-299 | [#1004](https://github.com/sesquicadaver/MTDirector/issues/1004) | Seed next after OPS-HOST-SYSUSERS-01 (PLAN-38 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-312 (#1031)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-297 (#1000) DONE", plan, StringComparison.Ordinal);
        Assert.Contains("W7-298", plan, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-312 (#1031)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-297 (#1000) DONE", plan38, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-SYSUSERS-01", plan38, StringComparison.Ordinal);
        Assert.Contains("W7-298", plan38, StringComparison.Ordinal);
        Assert.Contains("W7-299", plan38, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-312 (#1031)", plan38, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.env.example", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.sysusers", packageController, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.env.example")));
        Assert.True(File.Exists(Path.Combine(root, "packaging/systemd/mfc-controller.sysusers")));
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
