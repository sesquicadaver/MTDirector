using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Policy tests, semantic revision diff, and risk classification (Policy Model §54–§61 / M2-16).
/// Does not approve, bind, or deploy. Does not write RouterOS.
/// </summary>
public static class PolicyEvidenceAnalysis
{
    public const string AnalyzerVersion = "mfc.policy-evidence.v1";

    public const string EvidenceContextPrefix = "mfc.policy.evidence_context.v1";

    public const string AnalysisContextPrefix = "mfc.policy.analysis_context.v1";

    public static PolicyEvidenceAnalysisResult Analyze(
        IReadOnlyList<PolicyRule> afterRules,
        IReadOnlyList<PolicyTestCase> tests,
        ChainContractSet contracts,
        IReadOnlyDictionary<AddressObjectId, AddressObject> afterAddresses,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> afterServices,
        IReadOnlyList<PolicyRule>? beforeRules = null,
        IReadOnlyDictionary<AddressObjectId, AddressObject>? beforeAddresses = null,
        IReadOnlyDictionary<ServiceObjectId, ServiceObject>? beforeServices = null,
        IReadOnlySet<Guid>? beforeZoneIds = null,
        IReadOnlySet<Guid>? afterZoneIds = null,
        IReadOnlyList<ActualFilterRule>? actualFilter = null,
        PolicyEvidenceSignals? signals = null)
    {
        ArgumentNullException.ThrowIfNull(afterRules);
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(afterAddresses);
        ArgumentNullException.ThrowIfNull(afterServices);
        IReadOnlyList<PolicyRule> before = beforeRules ?? afterRules;
        IReadOnlyDictionary<AddressObjectId, AddressObject> beforeAddr = beforeAddresses ?? afterAddresses;
        IReadOnlyDictionary<ServiceObjectId, ServiceObject> beforeSvc = beforeServices ?? afterServices;
        IReadOnlySet<Guid> zonesBefore = beforeZoneIds ?? new HashSet<Guid>();
        IReadOnlySet<Guid> zonesAfter = afterZoneIds ?? zonesBefore;
        PolicyEvidenceSignals flags = signals ?? PolicyEvidenceSignals.None;

        IReadOnlyList<PolicyTestResult> testResults = PolicyTestEvaluator.Evaluate(
            tests,
            afterRules,
            contracts,
            afterAddresses,
            afterServices,
            actualFilter);
        PolicyRevisionDiffResult diff = PolicyRevisionDiffer.Diff(
            before,
            afterRules,
            beforeAddr,
            afterAddresses,
            beforeSvc,
            afterServices,
            zonesBefore,
            zonesAfter);
        List<PolicyEvidenceFinding> findings = [];
        foreach (PolicyTestResult result in testResults)
        {
            PolicyTestCase? test = tests.FirstOrDefault(t => t.Id == result.TestId);
            if (result.FailureCode == PolicyEvidenceAnalysisCodes.SystemTestDisabled)
            {
                findings.Add(Finding(
                    PolicyEvidenceAnalysisCodes.SystemTestDisabled,
                    $"SYSTEM test {result.TestId} cannot be disabled.",
                    result.TestId.Value));
                continue;
            }

            bool safety = test?.Origin == PolicyTestOrigin.System;
            if (result.Outcome == PolicyEvidenceAnalysisCodes.OutcomeFail && safety)
            {
                findings.Add(Finding(
                    result.FailureCode ?? PolicyEvidenceAnalysisCodes.SafetyTestFailed,
                    $"Safety test {result.TestId} failed with proof {result.Proof}.",
                    result.TestId.Value));
            }
        }

        IReadOnlyList<PolicyEvidenceFinding> orderedFindings = findings
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.TargetId)
            .ToArray();
        PolicyRiskResult risk = PolicyRiskClassifier.Classify(diff, orderedFindings, flags, before, afterRules);
        return new PolicyEvidenceAnalysisResult
        {
            TestResults = testResults,
            Diff = diff,
            Risk = risk,
            Findings = orderedFindings,
            EvidenceContextHash = HashEvidenceContext(testResults, diff, risk),
        };
    }

    public static Hash256 HashEvidenceContext(
        IReadOnlyList<PolicyTestResult> tests,
        PolicyRevisionDiffResult diff,
        PolicyRiskResult risk)
    {
        ArgumentNullException.ThrowIfNull(tests);
        ArgumentNullException.ThrowIfNull(diff);
        ArgumentNullException.ThrowIfNull(risk);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, EvidenceContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        foreach (PolicyTestResult test in tests.OrderBy(static t => t.TestId.ToString(), StringComparer.Ordinal))
        {
            AppendUtf8(hasher, test.TestId.ToString());
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, test.Outcome);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, test.Proof);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, test.FailureCode ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, test.MatchedRuleId?.ToString() ?? string.Empty);
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(int)test.FinalDisposition]);
            hasher.AppendData([(byte)1]);
        }

        hasher.AppendData([(byte)2]);
        foreach (PolicyRuleDiffEntry entry in diff.RuleChanges.OrderBy(static e => e.RuleId.Value))
        {
            AppendUtf8(hasher, entry.RuleId.ToString());
            hasher.AppendData([(byte)0]);
            foreach (string change in entry.Changes.OrderBy(static c => c, StringComparer.Ordinal))
            {
                AppendUtf8(hasher, change);
                hasher.AppendData([(byte)0]);
            }

            hasher.AppendData([(byte)1]);
        }

        hasher.AppendData([(byte)3]);
        foreach (string packet in diff.PacketSpaceClasses.OrderBy(static s => s, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, packet);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)4]);
        foreach (string semantic in diff.SemanticClasses.OrderBy(static s => s, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, semantic);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)5]);
        foreach (PolicyObjectImpact impact in diff.ObjectImpacts.OrderBy(static i => i.ObjectId))
        {
            AppendUtf8(hasher, impact.ObjectId.ToString("D"));
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, impact.ObjectKind);
            hasher.AppendData([(byte)0]);
            foreach (RuleId id in impact.DependentRuleIds.OrderBy(static r => r.Value))
            {
                AppendUtf8(hasher, id.ToString());
                hasher.AppendData([(byte)0]);
            }

            hasher.AppendData([(byte)1]);
        }

        hasher.AppendData([(byte)6]);
        AppendUtf8(hasher, risk.Level);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// analysis_context_hash including M2-12…M2-15 slots plus this evidence slot.
    /// Does not change the one- through five-argument combiners.
    /// </summary>
    public static Hash256 HashAnalysisContext(
        Hash256 actualFilterContextHash,
        Hash256 packetPathContextHash,
        Hash256 managementPathContextHash,
        Hash256 topologyDependencyContextHash,
        Hash256 fastTrackContextHash,
        Hash256 evidenceContextHash)
    {
        ArgumentNullException.ThrowIfNull(actualFilterContextHash);
        ArgumentNullException.ThrowIfNull(packetPathContextHash);
        ArgumentNullException.ThrowIfNull(managementPathContextHash);
        ArgumentNullException.ThrowIfNull(topologyDependencyContextHash);
        ArgumentNullException.ThrowIfNull(fastTrackContextHash);
        ArgumentNullException.ThrowIfNull(evidenceContextHash);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, AnalysisContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, ActualFilterAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(actualFilterContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, PacketPathAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(packetPathContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, ManagementPathAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(managementPathContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, TopologyDependencyAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(topologyDependencyContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, FastTrackAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(fastTrackContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(evidenceContextHash.Bytes);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static PolicyEvidenceFinding Finding(string code, string message, Guid? target)
        => new()
        {
            Code = code,
            Severity = PolicyEvidenceAnalysisCodes.SeverityBlocker,
            Message = message,
            TargetId = target,
        };

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));
}
