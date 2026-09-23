using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-308: PLAN-41 inventory documents sole QG-SIGN-02 rank (opt-in GPG/Sigstore crypto gate)
/// and opens SIGN-02 implement after seed.
/// </summary>
public sealed class Plan41ReleaseSigningCryptoGpgSigstoreW7308LivingSpecTests
{
    [Fact]
    public void Ac1Plan41InventoryDocumentsSoleQgSign02RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan41 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-41-release-signing-crypto-gpg-sigstore.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string signing = File.ReadAllText(Path.Combine(root, "docs/release/RELEASE_SIGNING.md"));
        string signingGate = File.ReadAllText(Path.Combine(root, "docs/development/signing-gate.md"));
        string script = File.ReadAllText(Path.Combine(root, "scripts/release/generate-sbom-and-checksums.sh"));
        string ci = File.ReadAllText(Path.Combine(root, ".github/workflows/ci.yml"));
        string qgSign01 = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Documentation/QgSign01ReleaseSigningLivingSpecTests.cs"));

        Assert.Contains("PLAN-41 — Release signing crypto (GPG/Sigstore beyond QG-SIGN-01)", plan41, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan41, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-02", plan41, StringComparison.Ordinal);
        Assert.Contains("190980c0", plan41, StringComparison.Ordinal);
        Assert.Contains("W7-310", plan41, StringComparison.Ordinal);
        Assert.Contains("W7-309", plan41, StringComparison.Ordinal);
        Assert.Contains("W7-308", plan41, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan41, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("opt-in", plan41, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("org secrets on every PR", plan41, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no-org-secrets-on-every-PR", plan41, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-401 (#1206)", plan41, StringComparison.Ordinal);
        Assert.Contains("generate-sbom-and-checksums.sh", plan41, StringComparison.Ordinal);
        Assert.Contains("QgSign01", plan41, StringComparison.Ordinal);
        Assert.Contains("ci.yml", plan41, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-308 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-310", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-309", limitations, StringComparison.Ordinal);
        Assert.Contains("190980c0", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-308 | [#1023](https://github.com/sesquicadaver/MTDirector/issues/1023) | PLAN-41 — Inventory release signing crypto (GPG/Sigstore beyond QG-SIGN-01) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
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
        Assert.Contains("§3.C NEXT = W7-401 (#1206)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-309", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-310", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-41", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-41-release-signing-crypto-gpg-sigstore.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-41-release-signing-crypto-gpg-sigstore.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan41ReleaseSigningCryptoGpgSigstoreW7308", testing, StringComparison.Ordinal);

        // Evidence surfaces remain present (inventory does not implement crypto yet).
        Assert.Contains("CI signing gate", signing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("future", signing, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GPG or Sigstore", signing, StringComparison.Ordinal);
        Assert.Contains("QG-SIGN-01", signingGate, StringComparison.Ordinal);
        Assert.Contains("MFC_RELEASE_GPG_KEY_ID", script, StringComparison.Ordinal);
        Assert.Contains("SHA256SUMS", script, StringComparison.Ordinal);
        Assert.DoesNotContain("cosign", ci, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sigstore", ci, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QgSign01ReleaseSigningLivingSpecTests", qgSign01, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(root, "scripts/release/generate-sbom-and-checksums.sh")));
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
