using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-397: AUDIT-SBOM-01 — real-mode SBOM generation is fail-closed
/// (SDK required, non-empty components, GPG key without gpg fails).
/// </summary>
public sealed class AuditSbom01FailClosedW7397LivingSpecTests
{
    [Fact]
    public void Ac1FailClosedSbomScriptAndQueueLock()
    {
        string root = RepoRoot();
        string script = File.ReadAllText(Path.Combine(root, "scripts/release/generate-sbom-and-checksums.sh"));
        string packaging = File.ReadAllText(Path.Combine(root, "docs/release/packaging.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.Contains("AUDIT-SBOM-01", script, StringComparison.Ordinal);
        Assert.Contains("require_dotnet", script, StringComparison.Ordinal);
        Assert.Contains("fail_if_empty_components_real", script, StringComparison.Ordinal);
        Assert.Contains("sbom_component_count", script, StringComparison.Ordinal);
        Assert.Contains("dotnet --list-sdks", script, StringComparison.Ordinal);
        Assert.Contains("MFC_RELEASE_GPG_KEY_ID set but gpg is not available", script, StringComparison.Ordinal);
        Assert.Contains("Not a cryptographic signature", script, StringComparison.Ordinal);
        Assert.Contains("empty components allowed", script, StringComparison.Ordinal);
        Assert.DoesNotContain("|| true", script, StringComparison.Ordinal);

        Assert.Contains("AUDIT-SBOM-01", packaging, StringComparison.Ordinal);
        Assert.Contains("non-empty SBOM", packaging, StringComparison.Ordinal);
        Assert.Contains("attestation, not a cryptographic signature", packaging, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-397 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-SBOM-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("AuditSbom01FailClosedW7397LivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-398", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-OWN-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-397 | [#1200](https://github.com/sesquicadaver/MTDirector/issues/1200) | AUDIT-SBOM-01 — SBOM/signing fail-closed (no empty components / missing SDK) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-398 | [#1202](https://github.com/sesquicadaver/MTDirector/issues/1202) | Seed next after AUDIT-SBOM-01 → AUDIT-OWN-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-399 | [#1203](https://github.com/sesquicadaver/MTDirector/issues/1203) | AUDIT-OWN-01 — Onboarding durable writer lease vs recovery | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-423 (#1242)", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-SBOM-01 W7-397 (#1200) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-398 (#1202) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-423 (#1242)", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-OWN-01", plan62, StringComparison.Ordinal);

        Assert.Contains("AUDIT-SBOM-01 W7-397 (#1200) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-423 (#1242)", continuous, StringComparison.Ordinal);
        Assert.Contains("AUDIT-OWN-01", continuous, StringComparison.Ordinal);

        Assert.Contains("AuditSbom01FailClosedW7397", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7398", testing, StringComparison.Ordinal);

        Assert.Contains("| `W7-397` | #1200 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-398` | #1202 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-399` | #1203 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-423 (#1242)", issues, StringComparison.Ordinal);
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
