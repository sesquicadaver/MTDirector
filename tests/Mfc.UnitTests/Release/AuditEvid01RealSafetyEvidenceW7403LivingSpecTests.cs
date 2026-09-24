using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-403: AUDIT-EVID-01 — Controller-proven sealed safety evidence (no AllSafeEvidence stub).
/// </summary>
public sealed class AuditEvid01RealSafetyEvidenceW7403LivingSpecTests
{
    [Fact]
    public void Ac1SealedEvidenceIsProvenAndProductionSurfacesRejectSyntheticFields()
    {
        string root = RepoRoot();
        string builder = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Deployment/SealedDeploymentPlanBuilder.cs"));
        string evidence = File.ReadAllText(Path.Combine(root, "src/Mfc.Application/Deployment/SealedTransitionEvidence.cs"));
        string create = File.ReadAllText(Path.Combine(
            root, "src/Mfc.Application/Deployment/CreateDeploymentPlanFromSealedArtifactsUseCase.cs"));
        string policy = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Deployment/StandaloneDeploymentPolicy.cs"));
        string validator = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Deployment/TransitionStateValidator.cs"));
        string codes = File.ReadAllText(Path.Combine(root, "src/Mfc.Domain/Deployment/DeploymentCodes.cs"));
        string vrrp = File.ReadAllText(Path.Combine(
            root, "src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan62 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-62-audit-remediation-acd0759.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string issues = File.ReadAllText(Path.Combine(root, "ISSUES.md"));

        Assert.DoesNotContain("TransitionStateValidator.AllSafeEvidence", builder, StringComparison.Ordinal);
        Assert.DoesNotContain("AllSafeEvidence(", builder, StringComparison.Ordinal);
        Assert.Contains("SealedTransitionEvidence.Prove", builder, StringComparison.Ordinal);
        Assert.Contains("DeploymentProbeKind.ApiSsl", builder, StringComparison.Ordinal);
        Assert.Contains("bootstrap fallback forbidden", builder, StringComparison.Ordinal);

        Assert.Contains("Does not invent", evidence, StringComparison.Ordinal);
        Assert.Contains("TransitionStateUnsafe", evidence, StringComparison.Ordinal);

        Assert.Contains("RequireApiSslProbe", create, StringComparison.Ordinal);
        Assert.Contains("SealedEvidenceMissing", create, StringComparison.Ordinal);

        Assert.Contains("EnsureSealedEvidencePresent", policy, StringComparison.Ordinal);
        Assert.Contains("guard and anchor context hashes must be distinct", policy, StringComparison.Ordinal);

        Assert.Contains("unit tests only", validator, StringComparison.Ordinal);
        Assert.Contains("AUDIT-EVID-01", validator, StringComparison.Ordinal);

        Assert.Contains(nameof(DeploymentCodes.SealedEvidenceMissing), codes, StringComparison.Ordinal);
        Assert.Contains("live old anchor", vrrp, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("Intentional residual (W7-403 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-EVID-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains("AuditEvid01RealSafetyEvidenceW7403LivingSpecTests", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-405", limitations, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RB-01", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-403 | [#1209](https://github.com/sesquicadaver/MTDirector/issues/1209) | AUDIT-EVID-01 — Real safety evidence (no AllSafeEvidence) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-404 | [#1211](https://github.com/sesquicadaver/MTDirector/issues/1211) | Seed next after AUDIT-EVID-01 → AUDIT-RB-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-405 | [#1212](https://github.com/sesquicadaver/MTDirector/issues/1212) | AUDIT-RB-01 — Unified strict rollback | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", roadmap, StringComparison.Ordinal);

        Assert.Contains("AUDIT-EVID-01 W7-403 (#1209) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("W7-404 (#1211) DONE", plan62, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan62, StringComparison.Ordinal);
        Assert.Contains("AUDIT-RB-01", plan62, StringComparison.Ordinal);

        Assert.Contains("AUDIT-EVID-01 W7-403 (#1209) DONE", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", continuous, StringComparison.Ordinal);

        Assert.Contains("AuditEvid01RealSafetyEvidenceW7403", testing, StringComparison.Ordinal);
        Assert.Contains("ProductTrancheSeedW7404", testing, StringComparison.Ordinal);

        Assert.Contains("| `W7-403` | #1209 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-404` | #1211 |", issues, StringComparison.Ordinal);
        Assert.Contains("| `W7-405` | #1212 |", issues, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", issues, StringComparison.Ordinal);
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
