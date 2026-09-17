using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>W7-234: AUDIT-AUTH-01 production allowlist; §3.C NEXT advanced past PLAN-26 COMPLETE / PLAN-27 inventory.</summary>
public sealed class AuditAuth01AllowlistedOperatorsW7234LivingSpecTests
{
    [Fact]
    public void Ac1ProductionCompositionUsesAllowlistNotAllowAll()
    {
        string root = RepoRoot();
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string options = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Configuration/ControllerOptions.cs"));
        string appsettings = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/appsettings.json"));
        string configDoc = File.ReadAllText(Path.Combine(root, "docs/operations/controller-configuration.md"));
        string security = File.ReadAllText(Path.Combine(root, "SECURITY.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string plan26 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-26-code-audit-remediation-11cb746.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));

        Assert.Contains("AllowListedOperatorAuthorizationBoundary", program, StringComparison.Ordinal);
        Assert.Contains("options.Authorization.Operators", program, StringComparison.Ordinal);
        Assert.Contains("SystemActorAuthorizationBoundary", program, StringComparison.Ordinal);
        Assert.Contains("AllowDevelopmentAuthentication", program, StringComparison.Ordinal);
        Assert.Contains("AllowAllAuthorizationBoundary", program, StringComparison.Ordinal);
        Assert.DoesNotContain("new DenyAllAuthorizationBoundary()", program, StringComparison.Ordinal);

        int allowAll = program.IndexOf("new AllowAllAuthorizationBoundary()", StringComparison.Ordinal);
        int allowDev = program.IndexOf("AllowDevelopmentAuthentication", StringComparison.Ordinal);
        Assert.True(allowDev >= 0 && allowAll > allowDev);

        Assert.Contains("AuthorizationHostOptions", options, StringComparison.Ordinal);
        Assert.Contains("OperatorAuthorizationEntry", options, StringComparison.Ordinal);
        Assert.Contains("Mfc:Authorization:Operators", options, StringComparison.Ordinal);
        Assert.Contains("\"Operators\": []", appsettings, StringComparison.Ordinal);

        Assert.Contains("Authorization:Operators", configDoc, StringComparison.Ordinal);
        Assert.Contains("AllowListedOperatorAuthorizationBoundary", configDoc, StringComparison.Ordinal);
        Assert.Contains("W7-234", security, StringComparison.Ordinal);
        Assert.Contains("AllowListedOperatorAuthorizationBoundary", security, StringComparison.Ordinal);
        Assert.Contains("AuditAuth01AllowlistedOperatorsW7234", testing, StringComparison.Ordinal);

        Assert.Contains("AUDIT-AUTH-01", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-234 (#875) DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("W7-235 DONE", plan26, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-314 (#1034)", plan26, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-234 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AllowListedOperatorAuthorizationBoundary", limitations, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-314 (#1034)", roadmap, StringComparison.Ordinal);
        Assert.Contains(
            "W7-234 | [#875](https://github.com/sesquicadaver/MTDirector/issues/875) | AUDIT-AUTH-01 — Production operator authorization DenyAll | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(
            root,
            "src/Mfc.Controller/Authorization/AllowListedOperatorAuthorizationBoundary.cs")));
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
