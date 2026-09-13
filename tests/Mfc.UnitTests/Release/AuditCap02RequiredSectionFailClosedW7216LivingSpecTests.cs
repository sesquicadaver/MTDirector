using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-216: AUDIT-CAP-02 required-section read failure must not complete snapshot.</summary>
public sealed class AuditCap02RequiredSectionFailClosedW7216LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndGateLockAuditCap02()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string gate = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Snapshot/RequiredSectionCaptureGate.cs"));
        string builder = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Snapshot/SnapshotCaptureResultBuilder.cs"));
        string port = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Ports/RouterOsSnapshotCapturePort.cs"));
        string useCase = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Snapshots/CaptureSnapshotUseCase.cs"));

        Assert.Contains("AUDIT-CAP-02", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-216", plan26, StringComparison.Ordinal);
        Assert.Contains("**DONE**", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-216 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-02", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-216", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-02", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-216", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditCap02RequiredSectionFailClosedW7216", testing, StringComparison.Ordinal);
        Assert.Contains("EnsureRequiredSucceeded", gate, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT_REQUIRED_SECTION_FAILED", gate, StringComparison.Ordinal);
        Assert.Contains("RequiredSectionCaptureGate.EnsureRequiredSucceeded", builder, StringComparison.Ordinal);
        Assert.Contains("RequiredSectionCaptureException", port, StringComparison.Ordinal);
        Assert.Contains("SNAPSHOT_REQUIRED_SECTION_FAILED", useCase, StringComparison.Ordinal);
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
