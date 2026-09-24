using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-413: AUDIT-CAP-04 — capture attempt identity (audit F09).</summary>
public sealed class AuditCap04CaptureAttemptIdentityW7413LivingSpecTests
{
    [Fact]
    public void Ac1DeviceScopedIdempotencyPersistAttemptAndNodeTimeSet()
    {
        string root = RepoRoot();
        string capture = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Snapshots/CaptureSnapshotUseCase.cs"));
        string node = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Snapshots/CaptureNodeSnapshotsUseCase.cs"));
        string storeIface = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Abstractions/Persistence/InventorySnapshotStores.cs"));
        string efStore = File.ReadAllText(Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Snapshots/EfSnapshotStore.cs"));
        string grpc = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/SnapshotGrpcService.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.Contains("AUDIT-CAP-04", capture, StringComparison.Ordinal);
        Assert.Contains("IdempotencyKeyBoundToOtherDeviceAsync", capture, StringComparison.Ordinal);
        Assert.Contains("contentDeduplicated", capture, StringComparison.Ordinal);
        Assert.Contains("PersistCompletedAsync", capture, StringComparison.Ordinal);
        Assert.Contains("HashRequest", capture, StringComparison.Ordinal);

        Assert.Contains("deviceId", storeIface, StringComparison.Ordinal);
        Assert.Contains("IdempotencyKeyBoundToOtherDeviceAsync", storeIface, StringComparison.Ordinal);
        Assert.Contains("o.TargetId == deviceId.Value", efStore, StringComparison.Ordinal);

        Assert.Contains("TimeSetFit", node, StringComparison.Ordinal);
        Assert.Contains("MaxMemberCaptureSkew", node, StringComparison.Ordinal);
        Assert.Contains("EvaluateTimeSetFit", node, StringComparison.Ordinal);
        Assert.Contains("AllMembersSucceeded", node, StringComparison.Ordinal);

        Assert.Contains("TimeSetFit", grpc, StringComparison.Ordinal);
        Assert.Contains("AllMembersSucceeded", grpc, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-413 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-CAP-04 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-414", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-AN-03", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-413 | [#1226](https://github.com/sesquicadaver/MTDirector/issues/1226) | AUDIT-CAP-04 — Capture attempt identity | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-414 | [#1228](https://github.com/sesquicadaver/MTDirector/issues/1228) | Seed next after AUDIT-CAP-04 → AUDIT-AN-03 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-415 | [#1229](https://github.com/sesquicadaver/MTDirector/issues/1229) | AUDIT-AN-03 — Server-owned analysis | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-CAP-04 W7-413 (#1226) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-414 (#1228) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-415 (#1229) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", plan62, StringComparison.Ordinal);

        Assert.Contains("AUDIT-CAP-04 W7-413 (#1226) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", continuous, StringComparison.Ordinal);

        Assert.Contains("AuditCap04CaptureAttemptIdentityW7413", testing, StringComparison.Ordinal);
        Assert.Contains("| `W7-413` | #1226 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-414` | #1228 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-415` | #1229 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-417 (#1233)", issues, StringComparison.Ordinal);
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
