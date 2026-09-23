using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-307: PLAN-40 COMPLETE; known-limitations / queue seed locked PLAN-41 inventory (W7-308)
/// and follow-up seed W7-309 after OPS-HOST-LOG-01.
/// </summary>
public sealed class ProductTrancheSeedW7307LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan41AfterPlan40Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan40 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-40-controller-host-journald-syslog-identity.md"));
        string plan41 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-41-release-signing-crypto-gpg-sigstore.md"));
        string unit = File.ReadAllText(Path.Combine(root, "packaging/systemd/mfc-controller.service"));
        string signing = File.ReadAllText(Path.Combine(root, "docs/release/RELEASE_SIGNING.md"));

        Assert.Contains("Intentional residual (W7-307 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-40 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-41", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-308", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-309", limitations, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-02", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-308**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-307 | [#1020](https://github.com/sesquicadaver/MTDirector/issues/1020) | Seed next after OPS-HOST-LOG-01 (PLAN-40 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-308 | [#1023](https://github.com/sesquicadaver/MTDirector/issues/1023) | PLAN-41 — Inventory release signing crypto (GPG/Sigstore beyond QG-SIGN-01) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-309 | [#1024](https://github.com/sesquicadaver/MTDirector/issues/1024) | Seed first PLAN-41 atomic row after inventory → QG-SIGN-02 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-40 COMPLETE", plan40, StringComparison.Ordinal);
        Assert.Contains("W7-307 (#1020) DONE", plan40, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", plan40, StringComparison.Ordinal);
        Assert.Contains("plan-41-release-signing-crypto-gpg-sigstore.md", plan40, StringComparison.Ordinal);

        Assert.Contains("PLAN-41", plan, StringComparison.Ordinal);
        Assert.Contains("W7-308", plan, StringComparison.Ordinal);
        Assert.Contains("W7-307 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-41-release-signing-crypto-gpg-sigstore.md", plan, StringComparison.Ordinal);

        Assert.Contains("QG-SIGN-02", plan41, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan41, StringComparison.Ordinal);
        Assert.Contains("W7-308", plan41, StringComparison.Ordinal);
        Assert.Contains("W7-309", plan41, StringComparison.Ordinal);
        Assert.Contains("W7-310", plan41, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-399 (#1203)", plan41, StringComparison.Ordinal);
        Assert.Contains("Sigstore", plan41, StringComparison.Ordinal);
        Assert.Contains("190980c0", plan41, StringComparison.Ordinal);

        Assert.Contains("SyslogIdentifier=mfc-controller", unit, StringComparison.Ordinal);
        Assert.Contains("future", signing, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(root, "docs/release/RELEASE_SIGNING.md")));
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
