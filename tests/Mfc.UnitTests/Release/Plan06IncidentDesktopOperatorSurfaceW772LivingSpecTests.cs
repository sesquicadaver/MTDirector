using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-72: PLAN-06 Incident Desktop operator-surface inventory documents ranked DESK-INCIDENT rows and seeds DESK-INCIDENT-01.</summary>
public sealed class Plan06IncidentDesktopOperatorSurfaceW772LivingSpecTests
{
    [Fact]
    public void Ac1Plan06InventoryDocumentsRankedIncidentRowsAndSeedsDeskIncident01()
    {
        string root = RepoRoot();
        string plan06 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-06-incident-desktop-operator-surface.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string plan05 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-05-desktop-operator-surface.md"));

        Assert.Contains("PLAN-06 — Incident Desktop operator-surface Living Spec product tranche", plan06, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-01", plan06, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-02", plan06, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-03", plan06, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-04", plan06, StringComparison.Ordinal);
        Assert.Contains("W7-73", plan06, StringComparison.Ordinal);
        Assert.Contains("IncidentViewModel", plan06, StringComparison.Ordinal);
        Assert.Contains("IncidentGrpcHostTests", plan06, StringComparison.Ordinal);
        Assert.Contains("IngestIncidentSignal", plan06, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-72 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-06", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-INCIDENT-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-73", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-06", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-06-incident-desktop-operator-surface.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-06-incident-desktop-operator-surface.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("COMPLETE", plan05, StringComparison.Ordinal);
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
