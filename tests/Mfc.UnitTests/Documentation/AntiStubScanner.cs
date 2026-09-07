using System.Text.RegularExpressions;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// QG-ANTISTUB-01: scans production and test C# sources for stub / NotImplemented / skipped-test markers.
/// </summary>
internal static class AntiStubScanner
{
    private static readonly Regex NotImplemented =
        new(@"\bNotImplementedException\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SkippedXunit =
        new(@"\[(?:Fact|Theory)\s*\(\s*Skip\s*=", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex StubTodo =
        new(@"//\s*(TODO|FIXME)\s*:?\s*.*\bstub\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal readonly record struct Finding(string RelativePath, int LineNumber, string Line, string Rule);

    private static readonly HashSet<string> ExcludedRelativePaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "tests/Mfc.UnitTests/Documentation/AntiStubScanner.cs",
        "tests/Mfc.UnitTests/Documentation/QgAntistub01AntiStubLivingSpecTests.cs",
    };

    /// <summary>Scans repository <c>src/**/*.cs</c> and <c>tests/**/*.cs</c> (excluding bin/obj and this gate's own sources).</summary>
    internal static IReadOnlyList<Finding> ScanRepository(string repoRoot)
    {
        List<Finding> findings = [];
        foreach ((string folder, bool isTests) in new[] { ("src", false), ("tests", true) })
        {
            string directory = Path.Combine(repoRoot, folder);
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                {
                    continue;
                }

                string relative = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
                if (ExcludedRelativePaths.Contains(relative))
                {
                    continue;
                }

                ScanText(relative, File.ReadAllText(path), isTests, findings);
            }
        }

        return findings;
    }

    /// <summary>Scans an in-memory C# snippet (synthetic detector proof).</summary>
    internal static IReadOnlyList<Finding> ScanSourceText(string relativePath, string source, bool isTests)
    {
        List<Finding> findings = [];
        ScanText(relativePath, source, isTests, findings);
        return findings;
    }

    private static void ScanText(string relativePath, string source, bool isTests, List<Finding> findings)
    {
        string[] lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            string trimmed = line.Trim();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                if (StubTodo.IsMatch(trimmed))
                {
                    findings.Add(new Finding(relativePath, i + 1, trimmed, "stub-todo"));
                }

                continue;
            }

            if (NotImplemented.IsMatch(line))
            {
                findings.Add(new Finding(relativePath, i + 1, trimmed, "NotImplementedException"));
            }

            if (isTests && SkippedXunit.IsMatch(line))
            {
                findings.Add(new Finding(relativePath, i + 1, trimmed, "xunit-Skip"));
            }
        }
    }
}
