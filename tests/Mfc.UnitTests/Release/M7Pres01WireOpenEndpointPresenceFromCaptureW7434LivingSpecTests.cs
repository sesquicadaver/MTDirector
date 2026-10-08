using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-434: M7-PRES-01 — wire OpenEndpointPresence from capture (F13 residual).</summary>
public sealed class M7Pres01WireOpenEndpointPresenceFromCaptureW7434LivingSpecTests
{
    [Fact]
    public void Ac1ProductionCaptureWiresOpenEndpointPresenceProjection()
    {
        string root = RepoRoot();
        string port = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Abstractions/Jobs/IEndpointPresenceCaptureProjectionPort.cs"));
        string projector = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Endpoint/EndpointPresenceCaptureProjectionPort.cs"));
        string capture = File.ReadAllText(
            Path.Combine(root, "src/Mfc.Application/Snapshots/CaptureSnapshotUseCase.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string coverage = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/Endpoint/EndpointPresenceCaptureProjectionTests.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan63 = File.ReadAllText(
            Path.Combine(root, "docs/planning/plan-63-reaudit-residuals-wave-a.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string reaudit = File.ReadAllText(
            Path.Combine(root, "docs/audits/MTDirector-reaudit-post-plan62-20261008.md"));

        Assert.Contains("IEndpointPresenceCaptureProjectionPort", port, StringComparison.Ordinal);
        Assert.Contains("NotConfiguredEndpointPresenceCaptureProjectionPort", port, StringComparison.Ordinal);
        Assert.Contains("OpenEndpointPresenceUseCase", projector, StringComparison.Ordinal);
        Assert.Contains("DeterministicEndpointId", projector, StringComparison.Ordinal);
        Assert.Contains("IEndpointPresenceCaptureProjectionPort", capture, StringComparison.Ordinal);
        Assert.Contains("_presenceProjection", capture, StringComparison.Ordinal);
        Assert.Contains("M7-PRES-01", capture, StringComparison.Ordinal);
        Assert.Contains(
            "TryAddSingleton<IEndpointPresenceCaptureProjectionPort, NotConfiguredEndpointPresenceCaptureProjectionPort>",
            program,
            StringComparison.Ordinal);
        Assert.Contains("EndpointPresenceCaptureProjectionPort", program, StringComparison.Ordinal);
        Assert.Contains("ProjectFromCaptureOpensPresenceForManagementIpWithInventoryAnchors", coverage, StringComparison.Ordinal);
        Assert.Contains("CaptureSnapshotSucceedsEvenWhenPresenceProjectionFails", coverage, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-434 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("M7-PRES-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-434 | [#1259](https://github.com/sesquicadaver/MTDirector/issues/1259) | M7-PRES-01 — Wire OpenEndpointPresence from capture (F13 residual) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-436 (#1261)", roadmap, StringComparison.Ordinal);
        Assert.Contains("| **Нереалізовано (§3)** | **3** |", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-434 (#1259) DONE", plan63, StringComparison.Ordinal);
        Assert.Contains("M7-PRES-01", plan63, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-436 (#1261)", plan63, StringComparison.Ordinal);
        Assert.Contains("M7Pres01WireOpenEndpointPresenceFromCaptureW7434", testing, StringComparison.Ordinal);
        Assert.Contains("OpenEndpointPresence", reaudit, StringComparison.Ordinal);
        Assert.Contains("M7-PRES-01", reaudit, StringComparison.Ordinal);
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
