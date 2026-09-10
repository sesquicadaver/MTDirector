using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>CT wrapper: DESK-A11Y-POLICY-ACK-02 / W7-192 ack/record a11y regression; PLAN-22 COMPLETE.</summary>
public sealed class CtDeskA11yPolicyAck02DesktopPoliciesAckRecordA11yRegressionLivingSpecTests
{
    [Fact]
    public void Ac1DesktopPoliciesAckRecordA11yRegressionLivingSpecAndPlan22CompleteExist()
    {
        string root = RepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopPoliciesAckRecordA11yRegressionLivingSpecTests.cs")));
        string plan22 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-22-desktop-policies-ack-record-automation.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        Assert.Contains("PLAN-22 COMPLETE", plan22, StringComparison.Ordinal);
        Assert.Contains("W7-192 DONE", plan22, StringComparison.Ordinal);
        Assert.Contains("DesktopPoliciesAckRecordA11yRegressionLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("CtDeskA11yPolicyAck02", testing, StringComparison.Ordinal);
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
