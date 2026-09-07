using System.Reflection;

namespace Mfc.UnitTests.Architecture;

/// <summary>
/// Builds a directed import graph among production <c>Mfc.*</c> assemblies and detects cycles.
/// Edges are <see cref="Assembly.GetReferencedAssemblies"/> filtered to the production set
/// (beyond pairwise NetArch boundary asserts in <see cref="ArchitectureBoundaryTests"/>).
/// </summary>
internal static class ProductionImportGraph
{
    internal static readonly string[] ProductionAssemblyNames =
    [
        "Mfc.Domain",
        "Mfc.Application",
        "Mfc.Infrastructure",
        "Mfc.RouterOs",
        "Mfc.Contracts",
        "Mfc.Controller",
        "Mfc.Desktop",
    ];

    /// <summary>Maps assembly name → referenced production Mfc.* assembly names.</summary>
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> Build(IReadOnlyDictionary<string, Assembly> byName)
    {
        Dictionary<string, IReadOnlyList<string>> graph = new(StringComparer.Ordinal);
        foreach (string name in ProductionAssemblyNames)
        {
            if (!byName.TryGetValue(name, out Assembly? assembly))
            {
                throw new InvalidOperationException($"Production assembly '{name}' was not loaded for import-graph analysis.");
            }

            List<string> edges = assembly.GetReferencedAssemblies()
                .Select(static a => a.Name ?? string.Empty)
                .Where(static n => ProductionAssemblyNames.Contains(n, StringComparer.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static n => n, StringComparer.Ordinal)
                .ToList();
            graph[name] = edges;
        }

        return graph;
    }

    /// <summary>
    /// Returns simple cycles as node paths (start node repeated at end), or empty when DAG.
    /// Uses DFS coloring; reports each distinct cycle once (canonicalized by lexicographically minimal rotation).
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<string>> FindCycles(IReadOnlyDictionary<string, IReadOnlyList<string>> graph)
    {
        Dictionary<string, int> color = graph.Keys.ToDictionary(static k => k, static _ => 0, StringComparer.Ordinal);
        List<string> stack = [];
        HashSet<string> seenCanonical = new(StringComparer.Ordinal);
        List<IReadOnlyList<string>> cycles = [];

        foreach (string start in graph.Keys.OrderBy(static k => k, StringComparer.Ordinal))
        {
            if (color[start] == 0)
            {
                Dfs(start);
            }
        }

        return cycles;

        void Dfs(string node)
        {
            color[node] = 1;
            stack.Add(node);

            foreach (string next in graph[node])
            {
                if (color[next] == 0)
                {
                    Dfs(next);
                }
                else if (color[next] == 1)
                {
                    int idx = stack.IndexOf(next);
                    if (idx < 0)
                    {
                        continue;
                    }

                    List<string> cycle = stack.Skip(idx).Append(next).ToList();
                    string canonical = Canonicalize(cycle);
                    if (seenCanonical.Add(canonical))
                    {
                        cycles.Add(cycle);
                    }
                }
            }

            stack.RemoveAt(stack.Count - 1);
            color[node] = 2;
        }
    }

    private static string Canonicalize(List<string> cycleWithRepeat)
    {
        // Drop closing repeat for rotation search.
        string[] nodes = cycleWithRepeat.Take(cycleWithRepeat.Count - 1).ToArray();
        if (nodes.Length == 0)
        {
            return string.Empty;
        }

        string best = string.Join("→", nodes);
        for (int i = 1; i < nodes.Length; i++)
        {
            string rotated = string.Join("→", nodes.Skip(i).Concat(nodes.Take(i)));
            if (string.CompareOrdinal(rotated, best) < 0)
            {
                best = rotated;
            }
        }

        return best;
    }
}
