using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-309: known-limitations / queue seed locked QG-SIGN-02 (W7-310) after PLAN-41 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7309LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedQgSign02AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan41 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-41-release-signing-crypto-gpg-sigstore.md"));
        string signing = File.ReadAllText(Path.Combine(root, "docs/release/RELEASE_SIGNING.md"));
        string script = File.ReadAllText(Path.Combine(root, "scripts/release/generate-sbom-and-checksums.sh"));

        Assert.Contains("Intentional residual (W7-309 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-310", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-311", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-310**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-309 | [#1024](https://github.com/sesquicadaver/MTDirector/issues/1024) | Seed first PLAN-41 atomic row after inventory → QG-SIGN-02 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-310 | [#1026](https://github.com/sesquicadaver/MTDirector/issues/1026) | QG-SIGN-02 — Opt-in cryptographic signing gate (GPG/Sigstore) beyond QG-SIGN-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-311 | [#1028](https://github.com/sesquicadaver/MTDirector/issues/1028) | Seed next after QG-SIGN-02 (PLAN-41 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-338 (#1082)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-309", plan, StringComparison.Ordinal);
        Assert.Contains("W7-310", plan, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-02", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-338 (#1082)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-309 (#1024) DONE", plan41, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-02", plan41, StringComparison.Ordinal);
        Assert.Contains("W7-310", plan41, StringComparison.Ordinal);
        Assert.Contains("W7-311", plan41, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-338 (#1082)", plan41, StringComparison.Ordinal);

        Assert.Contains("future", signing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MFC_RELEASE_GPG_KEY_ID", script, StringComparison.Ordinal);
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
