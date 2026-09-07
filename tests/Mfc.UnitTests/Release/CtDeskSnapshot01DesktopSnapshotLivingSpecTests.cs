using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>DESK-SNAPSHOT-01 / W7-85: Desktop Snapshot Living Spec is present and documented.</summary>
public sealed class CtDeskSnapshot01DesktopSnapshotLivingSpecTests
{
    [Fact]
    public void Ac1DesktopSnapshotLivingSpecAndPlan07MatrixExist()
    {
        string root = RepoRoot();
        string deskTests = Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopSnapshotLivingSpecTests.cs");
        string plan07 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-07-core-mvp-desktop-operator-surface.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));

        Assert.True(File.Exists(deskTests), "DesktopSnapshotLivingSpecTests.cs missing.");
        string body = File.ReadAllText(deskTests);
        Assert.Contains("Ac3ReloadLoadsCapturesWhenDeviceSelected", body, StringComparison.Ordinal);
        Assert.Contains("Ac2ViewModelExposesReloadCaptureCopyAndDiffCompareCommands", body, StringComparison.Ordinal);
        Assert.Contains("Ac4CaptureRequiresDeviceSelectionAndConnectedController", body, StringComparison.Ordinal);
        Assert.Contains("Ac6HostContractLivingSpecRemainsPresent", body, StringComparison.Ordinal);
        Assert.Contains("W7-85 DONE", plan07, StringComparison.Ordinal);
        Assert.Contains("DESK-SNAPSHOT-01", testing, StringComparison.Ordinal);
        Assert.Contains("DesktopSnapshotLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-85 Living Spec lock)", limitations, StringComparison.Ordinal);
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
