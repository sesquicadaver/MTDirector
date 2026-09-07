using System.Globalization;
using System.Text;
using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// QG-ANTISTUB-01 Living Spec: production/test C# must not contain NotImplementedException,
/// stub TODOs, or xUnit Skip attributes (anti-stub DoD).
/// </summary>
public sealed class QgAntistub01AntiStubLivingSpecTests
{
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

    [Fact]
    public void Ac1RepositoryHasNoAntiStubFindings()
    {
        IReadOnlyList<AntiStubScanner.Finding> findings = AntiStubScanner.ScanRepository(RepoRoot());
        if (findings.Count == 0)
        {
            return;
        }

        StringBuilder sb = new();
        sb.AppendLine("QG-ANTISTUB-01: anti-stub findings:");
        foreach (AntiStubScanner.Finding finding in findings.Take(50))
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  [{finding.Rule}] {finding.RelativePath}:{finding.LineNumber}: {finding.Line}");
        }

        Assert.Fail(sb.ToString());
    }

    [Fact]
    public void Ac2ScannerDetectsNotImplementedException()
    {
        const string source = """
            namespace Demo;
            public sealed class X
            {
                public void M() => throw new NotImplementedException();
            }
            """;

        IReadOnlyList<AntiStubScanner.Finding> findings =
            AntiStubScanner.ScanSourceText("src/Demo/X.cs", source, isTests: false);

        Assert.Contains(findings, static f => f.Rule == "NotImplementedException");
    }

    [Fact]
    public void Ac3ScannerDetectsSkippedXunitTests()
    {
        const string source = """
            using Xunit;
            public sealed class T
            {
                [Fact(Skip = "temporary")]
                public void Disabled() { }
            }
            """;

        IReadOnlyList<AntiStubScanner.Finding> findings =
            AntiStubScanner.ScanSourceText("tests/Demo/T.cs", source, isTests: true);

        Assert.Contains(findings, static f => f.Rule == "xunit-Skip");
    }

    [Fact]
    public void Ac4ScannerDetectsStubTodoComments()
    {
        const string source = """
            namespace Demo;
            public sealed class X
            {
                // TODO: stub until wired
                public void M() { }
            }
            """;

        IReadOnlyList<AntiStubScanner.Finding> findings =
            AntiStubScanner.ScanSourceText("src/Demo/X.cs", source, isTests: false);

        Assert.Contains(findings, static f => f.Rule == "stub-todo");
    }

    [Fact]
    public void Ac5DocsMatrixDocumentsQgAntistub01LivingSpec()
    {
        string root = RepoRoot();
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string plan03 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-03-quality-gates.md"));

        Assert.Contains("QG-ANTISTUB-01", testing, StringComparison.Ordinal);
        Assert.Contains("QgAntistub01AntiStubLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("W7-54 DONE", plan03, StringComparison.Ordinal);
    }
}
