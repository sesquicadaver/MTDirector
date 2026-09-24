using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-395: AUDIT-STATUS-01 — issue-queue CLOSED must not be read as production-safe write-path proof.
/// </summary>
public sealed class AuditStatus01HonestStatusesW7395LivingSpecTests
{
    [Fact]
    public void Ac1HonestStatusLayersAndQueueRules()
    {
        string root = RepoRoot();
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        string acceptance = File.ReadAllText(Path.Combine(root, "docs/release/mvp-acceptance.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string slash = File.ReadAllText(Path.Combine(root, ".cursor/rules/slash-autopilot.mdc"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));

        Assert.Contains("issue-queue", readme, StringComparison.Ordinal);
        Assert.Contains("production-safe write path NOT PROVEN", readme, StringComparison.Ordinal);
        Assert.Contains("NOT SATISFIED", readme, StringComparison.Ordinal);
        Assert.Contains("AUDIT-STATUS-01", readme, StringComparison.Ordinal);
        Assert.Contains("plan-62-audit-remediation-acd0759.md", readme, StringComparison.Ordinal);

        Assert.Contains("MVP CLOSED:** **yes**", acceptance, StringComparison.Ordinal);
        Assert.Contains("(issue-queue)", acceptance, StringComparison.Ordinal);
        Assert.Contains("NOT PROVEN", acceptance, StringComparison.Ordinal);
        Assert.Contains("issue-queue DoD substitute", acceptance, StringComparison.Ordinal);
        Assert.Contains("does **not** satisfy a production-safe write-path claim", acceptance, StringComparison.Ordinal);
        Assert.Contains("optional", acceptance, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Live CHR OFF", acceptance, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Intentional residual (W7-395 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-STATUS-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-396", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-SBOM-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-395 | [#1197](https://github.com/sesquicadaver/MTDirector/issues/1197) | AUDIT-STATUS-01 — Honest MVP/M7/write-path status vs audit acd0759 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-396 | [#1199](https://github.com/sesquicadaver/MTDirector/issues/1199) | Seed next after AUDIT-STATUS-01 → AUDIT-SBOM-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-397 | [#1200](https://github.com/sesquicadaver/MTDirector/issues/1200) | AUDIT-SBOM-01 — SBOM/signing fail-closed (no empty components / missing SDK) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", roadmap, StringComparison.Ordinal);
        Assert.Contains("TOR/audit-backed", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-STATUS-01 W7-395 (#1197) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan62, StringComparison.Ordinal);

        Assert.Contains("Засів нового траншу", slash, StringComparison.Ordinal);
        Assert.Contains("не вигадує", slash, StringComparison.Ordinal);
        Assert.Contains("явним наказом оператора", slash, StringComparison.Ordinal);
        Assert.Contains("ROADMAP §6", slash, StringComparison.Ordinal);

        Assert.Contains("AuditStatus01HonestStatusesW7395", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7396", testing, StringComparison.Ordinal);
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
