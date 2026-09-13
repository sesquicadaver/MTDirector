using Mfc.Application.Common;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Application.Policies;

/// <summary>
/// Server-side structural analysis and document-test coverage helpers (AUDIT-AN-01 / Policy Model §63).
/// </summary>
internal static class PolicyRevisionStructuralAnalysis
{
    public static ApplicationError? EnsureStructuralAnalysisPasses(
        Policy policy,
        PolicyRevision revision,
        PolicyDocument document)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(revision);
        ArgumentNullException.ThrowIfNull(document);

        ApplicationError? catalogs = TryBuildCatalogs(
            policy,
            revision,
            document,
            out Dictionary<AddressObjectId, AddressObject> addresses,
            out Dictionary<ServiceObjectId, ServiceObject> services,
            out HashSet<Guid> zones);
        if (catalogs is not null)
        {
            return catalogs;
        }

        PolicyAnalysisResult analysis = PolicyAnalysisEngine.Analyze(
            document.Rules,
            addresses,
            services,
            zones);
        PolicyAnalysisFinding? blocker = analysis.FirstBlocker;
        if (blocker is not null)
        {
            return ApplicationError.Validation(
                $"Structural analysis blocker {blocker.Code}: {blocker.Message}");
        }

        return null;
    }

    public static ApplicationError? TryBuildCatalogs(
        Policy policy,
        PolicyRevision revision,
        PolicyDocument document,
        out Dictionary<AddressObjectId, AddressObject> addresses,
        out Dictionary<ServiceObjectId, ServiceObject> services,
        out HashSet<Guid> zones)
    {
        addresses = [];
        services = [];
        zones = [];
        PolicyObjectIdentity owner = PolicyCatalogViewMapper.DeriveObjectIdentity(policy, revision);
        if (!PolicyCatalogViewMapper.TryParseTypedAddresses(document, owner, out addresses, out string? error)
            || !PolicyCatalogViewMapper.TryParseTypedServices(document, owner, out services, out error))
        {
            return ApplicationError.Validation(error ?? "Policy catalog parse failed.");
        }

        zones = PolicyCatalogViewMapper.ExtractZoneIds(document);
        return null;
    }

    public static IReadOnlyList<PolicyApprovalFinding> CollectStructuralApprovalFindings(
        Policy policy,
        PolicyRevision revision,
        PolicyDocument document)
    {
        ApplicationError? catalogs = TryBuildCatalogs(
            policy,
            revision,
            document,
            out Dictionary<AddressObjectId, AddressObject> addresses,
            out Dictionary<ServiceObjectId, ServiceObject> services,
            out HashSet<Guid> zones);
        if (catalogs is not null)
        {
            return
            [
                new PolicyApprovalFinding
                {
                    Code = PolicyApprovalCodes.Blocker,
                    Severity = PolicyEvidenceAnalysisCodes.SeverityBlocker,
                    Message = catalogs.Message,
                    Target = "catalog",
                    WarningHash = PolicyApprovalHasher.HashWarning(
                        PolicyApprovalCodes.Blocker,
                        "catalog",
                        catalogs.Message),
                },
            ];
        }

        PolicyAnalysisResult analysis = PolicyAnalysisEngine.Analyze(
            document.Rules,
            addresses,
            services,
            zones);
        List<PolicyApprovalFinding> findings = [];
        foreach (PolicyAnalysisFinding finding in analysis.Findings)
        {
            string severity = finding.Severity == PolicyAnalysisCodes.SeverityBlocker
                ? PolicyEvidenceAnalysisCodes.SeverityBlocker
                : PolicyEvidenceAnalysisCodes.SeverityWarning;
            string target = finding.RuleId.ToString("D");
            findings.Add(new PolicyApprovalFinding
            {
                Code = finding.Code,
                Severity = severity,
                Message = finding.Message,
                Target = target,
                WarningHash = PolicyApprovalHasher.HashWarning(finding.Code, target, finding.Message),
            });
        }

        return findings;
    }

    public static ApplicationError? EnsureTestCoverage(
        PolicyDocument document,
        IReadOnlyList<PolicyApprovalTestInput> testResults)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(testResults);
        HashSet<Guid> required = PolicyCatalogViewMapper.ExtractTestIds(document.Tests);
        if (required.Count == 0)
        {
            return null;
        }

        HashSet<Guid> recorded = testResults.Select(static t => t.TestId).ToHashSet();
        foreach (Guid testId in required.OrderBy(static g => g))
        {
            if (!recorded.Contains(testId))
            {
                return new ApplicationError(
                    PolicyApprovalCodes.TestsIncomplete,
                    $"Mandatory test {testId:D} is missing from the analysis run.");
            }
        }

        return null;
    }
}
