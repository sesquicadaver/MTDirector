using System.Reflection;
using Mfc.Application.Deployment;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Deployment;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-230: AUDIT-DEP-03 — VRRP reachability/traffic facts must be observed, not fabricated.</summary>
public sealed class AuditDep03VrrpLiveFactsW7230LivingSpecTests
{
    [Fact]
    public void Ac1Plan26AndRuntimeGateLockAuditDep03()
    {
        string root = RepoRoot();
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string runtime = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs"));
        string session = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Deployment/RouterOsDeploymentDeviceSession.cs"));
        string observer = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Deployment/VrrpMemberLiveFactsObserver.cs"));
        string registry = File.ReadAllText(Path.Combine(root, "src/Mfc.RouterOs/Commands/RosReadCommandRegistry.cs"));

        Assert.Contains("AUDIT-DEP-03", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-230", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-230 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("VrrpMemberLiveFactsObserver", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-230", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-DEP-03", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-230", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditDep03VrrpLiveFactsW7230", testing, StringComparison.Ordinal);

        Assert.DoesNotContain("Task.FromResult(true)", runtime, StringComparison.Ordinal);
        Assert.Contains("ProbeReachableAsync", runtime, StringComparison.Ordinal);
        Assert.Contains("ProbeReachableAsync", session, StringComparison.Ordinal);
        Assert.Contains("ObserveIndependentRoutedTrafficAsync", session, StringComparison.Ordinal);
        Assert.Contains("independentTraffic", session, StringComparison.Ordinal);
        Assert.Contains("SystemIdentity", observer, StringComparison.Ordinal);
        Assert.Contains("HasProvenIndependentRoutedTraffic", observer, StringComparison.Ordinal);
        Assert.Contains("rx-byte", registry, StringComparison.Ordinal);

        Assert.NotNull(typeof(VrrpMemberLiveFactsObserver).GetMethod(
            nameof(VrrpMemberLiveFactsObserver.ProbeReachableAsync),
            BindingFlags.Public | BindingFlags.Static));
        Assert.NotNull(typeof(IVrrpMemberDeploymentRuntime).GetMethod(
            nameof(IVrrpMemberDeploymentRuntime.IsReachableAsync)));
        Assert.True(RosReadCommandRegistry.Get(RosReadCommandId.Interfaces).PropertyProfile.TryGet("rx-byte", out _));
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
