using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-301: known-limitations / queue seed locked OPS-HOST-DOC-01 (W7-302) after PLAN-39 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7301LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedOpsHostDoc01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan39 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-39-controller-host-operator-doc-packaging.md"));
        string packageController = File.ReadAllText(Path.Combine(root, "scripts/release/package-controller.sh"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));

        Assert.Contains("Intentional residual (W7-301 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-302", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-303", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-302**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-301 | [#1008](https://github.com/sesquicadaver/MTDirector/issues/1008) | Seed first PLAN-39 atomic row after inventory → OPS-HOST-DOC-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-302 | [#1010](https://github.com/sesquicadaver/MTDirector/issues/1010) | OPS-HOST-DOC-01 — author packaging/doc/mfc/README.md + package-controller bundle | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-303 | [#1012](https://github.com/sesquicadaver/MTDirector/issues/1012) | Seed next after OPS-HOST-DOC-01 (PLAN-39 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-301", plan, StringComparison.Ordinal);
        Assert.Contains("W7-302", plan, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-301 (#1008) DONE", plan39, StringComparison.Ordinal);
        Assert.Contains("OPS-HOST-DOC-01", plan39, StringComparison.Ordinal);
        Assert.Contains("W7-302", plan39, StringComparison.Ordinal);
        Assert.Contains("W7-303", plan39, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", plan39, StringComparison.Ordinal);

        Assert.Contains("DEST=\"$OUT_DIR/controller\"", packageController, StringComparison.Ordinal);
        Assert.Contains("mfc-controller.service", packageController, StringComparison.Ordinal);
        Assert.Contains("Documentation=file:///usr/share/doc/mfc/README.md", unit, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "packaging/doc/mfc/README.md")));
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
