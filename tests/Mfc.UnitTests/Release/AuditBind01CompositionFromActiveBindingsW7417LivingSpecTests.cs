using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-417: AUDIT-BIND-01 — composition from active bindings (audit F07).</summary>
public sealed class AuditBind01CompositionFromActiveBindingsW7417LivingSpecTests
{
    [Fact]
    public void Ac1ComposeAndCompileUseActiveDesiredBindingsNotLatestApproved()
    {
        string root = RepoRoot();
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string loader = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/PolicyBoundLayerLoader.cs"));
        string compose = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/ComposeEffectivePolicyUseCase.cs"));
        string compile = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Policies/CompileNodeFilterArtifactsUseCase.cs"));
        string composeTests = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/Application/ComposeEffectivePolicyUseCaseTests.cs"));

        Assert.Contains("AUDIT-BIND-01", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-417 (#1233) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-417 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-BIND-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-417", roadmap, StringComparison.Ordinal);
        Assert.Contains("AUDIT-BIND-01", roadmap, StringComparison.Ordinal);
        Assert.Contains("**DONE**", roadmap, StringComparison.Ordinal);
        Assert.Contains("W7-417", continuous, StringComparison.Ordinal);
        Assert.Contains("AuditBind01CompositionFromActiveBindingsW7417", testing, StringComparison.Ordinal);
        Assert.Contains("DesiredRevisionId", loader, StringComparison.Ordinal);
        Assert.Contains("PolicyBoundLayerLoader.LoadBoundLayerAsync", compose, StringComparison.Ordinal);
        Assert.Contains("PolicyBoundLayerLoader.LoadBoundLayerAsync", compile, StringComparison.Ordinal);
        Assert.Contains("AppendBoundExceptionLayersAsync", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderByDescending(static r => r.RevisionNumber)", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("OrderByDescending(static r => r.RevisionNumber)", compile, StringComparison.Ordinal);
        Assert.Contains("A1LoadsBoundRevisionNotLatestApproved", composeTests, StringComparison.Ordinal);
        Assert.Contains("CompanyApprovedWithoutBindingIsCompanyRequired", composeTests, StringComparison.Ordinal);
        Assert.Contains("ApprovedExceptionWithoutBindingIsOmitted", composeTests, StringComparison.Ordinal);
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
