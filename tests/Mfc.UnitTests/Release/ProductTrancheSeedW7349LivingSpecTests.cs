using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-349: known-limitations / queue seed locked DESK-GRPC-DEADLINE-01 (W7-350) after PLAN-51 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7349LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedDeskGrpcDeadline01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan51 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-51-desktop-grpc-unary-deadline.md"));
        string desktopOptions = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Configuration/DesktopOptions.cs"));
        string servicesDir = Path.Combine(root, "src/Mfc.Desktop/Services");

        Assert.Contains("Intentional residual (W7-349 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-GRPC-DEADLINE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-350", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-351", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-350**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-349 | [#1104](https://github.com/sesquicadaver/MTDirector/issues/1104) | Seed first PLAN-51 atomic row after inventory → DESK-GRPC-DEADLINE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-350 | [#1106](https://github.com/sesquicadaver/MTDirector/issues/1106) | DESK-GRPC-DEADLINE-01 — Desktop unary gRPC CallOptions deadline policy | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-351 | [#1107](https://github.com/sesquicadaver/MTDirector/issues/1107) | Seed next after DESK-GRPC-DEADLINE-01 (PLAN-51 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-349", plan, StringComparison.Ordinal);
        Assert.Contains("W7-350", plan, StringComparison.Ordinal);
        Assert.Contains("W7-351", plan, StringComparison.Ordinal);
        Assert.Contains("DESK-GRPC-DEADLINE-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-349 (#1104) DONE", plan51, StringComparison.Ordinal);
        Assert.Contains("DESK-GRPC-DEADLINE-01", plan51, StringComparison.Ordinal);
        Assert.Contains("W7-350", plan51, StringComparison.Ordinal);
        Assert.Contains("W7-351", plan51, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-381 (#1168)", plan51, StringComparison.Ordinal);

        // DEADLINE-01 shipped after this seed.
        Assert.Contains("UnaryCallTimeoutSeconds", desktopOptions, StringComparison.Ordinal);
        Assert.Contains("DesktopGrpcUnaryCall", File.ReadAllText(Path.Combine(servicesDir, "DesktopGrpcUnaryCall.cs")), StringComparison.Ordinal);
        foreach (string clientPath in Directory.EnumerateFiles(servicesDir, "Grpc*Client.cs"))
        {
            Assert.Contains("DesktopGrpcUnaryCall.For", File.ReadAllText(clientPath), StringComparison.Ordinal);
        }

        Assert.Contains(
            "CancelAfter",
            File.ReadAllText(Path.Combine(servicesDir, "ControllerConnectionService.cs")),
            StringComparison.Ordinal);
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
