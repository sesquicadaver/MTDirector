using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-436: CAP-IDEM-01 — capture idempotency unique includes TargetId (F09 residual).</summary>
public sealed class CapIdem01CaptureIdempotencyTargetIdUniqueW7436LivingSpecTests
{
    [Fact]
    public void Ac1UniqueIndexIncludesTargetIdAndQueueAdvances()
    {
        string root = RepoRoot();
        string config = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Configurations/CaptureOperationConfiguration.cs"));
        string migration = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Migrations/20261008214925_CaptureIdempotencyTargetIdUniqueW7436.cs"));
        string snapshot = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Migrations/MfcDbContextModelSnapshot.cs"));
        string schemaTests = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.IntegrationTests/Persistence/InventorySnapshotSchemaTests.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan63 = File.ReadAllText(
            Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string reaudit = File.ReadAllText(
            Path.Combine(root, "docs/audits/MTDirector-reaudit-post-plan62-20261008.md"));
        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));

        Assert.Contains("e.RequestedBy, e.IdempotencyKey, e.TargetId", config, StringComparison.Ordinal);
        Assert.Contains("uq_capture_operation_idempotency", config, StringComparison.Ordinal);
        Assert.Contains("CAP-IDEM-01", config, StringComparison.Ordinal);
        Assert.Contains("RequestedBy\", \"IdempotencyKey\", \"TargetId\"", migration, StringComparison.Ordinal);
        Assert.Contains("uq_capture_operation_idempotency", migration, StringComparison.Ordinal);
        Assert.Contains(
            "b.HasIndex(\"RequestedBy\", \"IdempotencyKey\", \"TargetId\")",
            snapshot,
            StringComparison.Ordinal);
        Assert.Contains("CaptureOperationIdempotencyIsUniquePerTarget", schemaTests, StringComparison.Ordinal);
        Assert.Contains("CAP-IDEM-01 / F09", schemaTests, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-436 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CAP-IDEM-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-436 | [#1261](https://github.com/sesquicadaver/MTDirector/issues/1261) | CAP-IDEM-01 — Capture idempotency unique includes TargetId (F09 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-437 | [#1263](https://github.com/sesquicadaver/MTDirector/issues/1263) | Seed next after CAP-IDEM-01 → PLAN63-DONE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-438 (#1264)", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **1** |", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-436 (#1261) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("CAP-IDEM-01", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-438 (#1264)", plan63, StringComparison.Ordinal);
        Assert.Contains("CapIdem01CaptureIdempotencyTargetIdUniqueW7436", testing, StringComparison.Ordinal);
        Assert.Contains("CLOSED** CAP-IDEM-01", reaudit, StringComparison.Ordinal);
        Assert.Contains("W7-436", changelog, StringComparison.Ordinal);
        Assert.Contains("CAP-IDEM-01", changelog, StringComparison.Ordinal);
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
