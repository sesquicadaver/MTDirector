using System.Reflection;
using System.Text;
using Xunit;

namespace Mfc.UnitTests.Architecture;

/// <summary>
/// QG-IMPORT-01 Living Spec: production Mfc.* assembly import graph must be a DAG (no cycles),
/// complementing pairwise <see cref="ArchitectureBoundaryTests"/>.
/// </summary>
public sealed class QgImport01ImportGraphCycleLivingSpecTests
{
    private static Dictionary<string, Assembly> LoadProductionAssemblies()
    {
        // Force-load markers so GetReferencedAssemblies reflects project refs under test host.
        _ = typeof(Mfc.Domain.AssemblyMarker).Assembly;
        _ = Mfc.Application.AssemblyMarker.DomainDependencyAnchor;
        _ = typeof(Mfc.Application.AssemblyMarker).Assembly;
        _ = typeof(Mfc.Infrastructure.AssemblyMarker).Assembly;
        _ = typeof(Mfc.RouterOs.AssemblyMarker).Assembly;
        _ = typeof(Mfc.Contracts.AssemblyMarker).Assembly;
        _ = typeof(Mfc.Controller.Program).Assembly;
        _ = typeof(Mfc.Desktop.App).Assembly;

        Dictionary<string, Assembly> map = new(StringComparer.Ordinal);
        foreach (string name in ProductionImportGraph.ProductionAssemblyNames)
        {
            Assembly? loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.Ordinal));
            Assert.NotNull(loaded);
            map[name] = loaded!;
        }

        return map;
    }

    [Fact]
    public void Ac1ProductionMfcImportGraphHasNoCycles()
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> graph =
            ProductionImportGraph.Build(LoadProductionAssemblies());
        IReadOnlyList<IReadOnlyList<string>> cycles = ProductionImportGraph.FindCycles(graph);

        if (cycles.Count == 0)
        {
            return;
        }

        StringBuilder sb = new();
        sb.AppendLine("QG-IMPORT-01: cyclic Mfc.* assembly dependencies detected:");
        foreach (IReadOnlyList<string> cycle in cycles)
        {
            sb.AppendLine("  " + string.Join(" → ", cycle));
        }

        Assert.Fail(sb.ToString());
    }

    [Fact]
    public void Ac2ImportGraphIncludesExpectedLayerEdges()
    {
        IReadOnlyDictionary<string, IReadOnlyList<string>> graph =
            ProductionImportGraph.Build(LoadProductionAssemblies());

        Assert.Contains("Mfc.Domain", graph["Mfc.Application"], StringComparer.Ordinal);
        Assert.Contains("Mfc.Application", graph["Mfc.Infrastructure"], StringComparer.Ordinal);
        Assert.Contains("Mfc.Contracts", graph["Mfc.Desktop"], StringComparer.Ordinal);
        Assert.DoesNotContain("Mfc.Domain", graph["Mfc.Desktop"], StringComparer.Ordinal);
        Assert.DoesNotContain("Mfc.Infrastructure", graph["Mfc.Application"], StringComparer.Ordinal);
    }

    [Fact]
    public void Ac3DocsMatrixDocumentsQgImport01LivingSpec()
    {
        string root = RepoRoot();
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string plan03 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-03-quality-gates.md"));

        Assert.Contains("QG-IMPORT-01", testing, StringComparison.Ordinal);
        Assert.Contains("QgImport01ImportGraphCycleLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("QG-IMPORT-01", plan03, StringComparison.Ordinal);
        Assert.Contains("W7-52 DONE", plan03, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac4CycleDetectorReportsSyntheticCycle()
    {
        Dictionary<string, IReadOnlyList<string>> synthetic = new(StringComparer.Ordinal)
        {
            ["A"] = ["B"],
            ["B"] = ["C"],
            ["C"] = ["A"],
        };

        IReadOnlyList<IReadOnlyList<string>> cycles = ProductionImportGraph.FindCycles(synthetic);
        Assert.NotEmpty(cycles);
        Assert.Contains(
            cycles,
            static c => c.Count >= 4
                        && c.Contains("A", StringComparer.Ordinal)
                        && c.Contains("B", StringComparer.Ordinal)
                        && c.Contains("C", StringComparer.Ordinal));
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
