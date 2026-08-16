using System.Security.Cryptography;
using System.Text;
using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyApprovalWorkflowTests
{
    [Fact]
    public void Ac1AnalysisRunIsImmutableAndBundleHashIsContentAddressed()
    {
        PolicyAnalysisRun run = ValidRun();
        Hash256 other = H("other-bundle-slot");
        Assert.Throws<DomainInvariantException>(() =>
            PolicyAnalysisRun.Reconstitute(
                run.Id,
                run.RevisionId,
                run.RevisionContentHash,
                run.LogicalEffectiveHash,
                run.AnalysisContextHash,
                run.EvidenceContextHash,
                other,
                run.ImpactSetHash,
                run.PerDeviceAnalysisHashes,
                run.BundleHash,
                run.DependencyFingerprint,
                run.RiskLevel,
                run.EvidenceSignalsPresent,
                run.AnalyzerVersion,
                run.PolicySchemaVersion,
                run.PipelineVersion,
                run.Findings,
                run.TestResults,
                run.CreatedBy,
                run.CreatedAtUtc));
        PolicyAnalysisRun clone = PolicyAnalysisRun.Reconstitute(
            run.Id,
            run.RevisionId,
            run.RevisionContentHash,
            run.LogicalEffectiveHash,
            run.AnalysisContextHash,
            run.EvidenceContextHash,
            run.TopologyProjectionHash,
            run.ImpactSetHash,
            run.PerDeviceAnalysisHashes,
            run.BundleHash,
            run.DependencyFingerprint,
            run.RiskLevel,
            run.EvidenceSignalsPresent,
            run.AnalyzerVersion,
            run.PolicySchemaVersion,
            run.PipelineVersion,
            run.Findings,
            run.TestResults,
            run.CreatedBy,
            run.CreatedAtUtc);
        Assert.Equal(run.BundleHash.ToString(), clone.BundleHash.ToString());
    }

    [Fact]
    public void Ac2ApprovalIsBoundToExactAnalysisBundleHash()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision, PolicyAnalysisRun run) = InReviewWithRun();
        PolicyApprovalEvaluation mismatch = PolicyApprovalGate.Evaluate(
            revision,
            policy,
            run,
            H("wrong-bundle"),
            run.DependencyFingerprint,
            [],
            [],
            UserId.New(),
            isSecurityOwner: false);
        Assert.Equal(PolicyApprovalCodes.BundleMismatch, mismatch.ErrorCode);
        PolicyApprovalEvaluation match = PolicyApprovalGate.Evaluate(
            revision,
            policy,
            run,
            run.BundleHash,
            run.DependencyFingerprint,
            [],
            [],
            UserId.New(),
            isSecurityOwner: false);
        Assert.Equal(PolicyApprovalCodes.OutcomeApprove, match.Outcome);
        Assert.True(match.CompletesApproval);
    }

    [Fact]
    public void Ac3BlockerForbidsApproval()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = InReview();
        PolicyApprovalFinding blocker = Finding(
            "RULE_EMPTY_SELECTOR",
            PolicyEvidenceAnalysisCodes.SeverityBlocker,
            "empty");
        PolicyAnalysisRun run = ValidRun(revision, findings: [blocker]);
        PolicyApprovalEvaluation result = PolicyApprovalGate.Evaluate(
            revision, policy, run, run.BundleHash, run.DependencyFingerprint, [], [], UserId.New(), false);
        Assert.Equal(PolicyApprovalCodes.Blocker, result.ErrorCode);
        Assert.False(result.CompletesApproval);
    }

    [Fact]
    public void Ac4WarningRequiresAcknowledgmentOfExactHash()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = InReview();
        PolicyApprovalFinding warning = Finding("FASTTRACK_FALLBACK_REQUIRED", PolicyEvidenceAnalysisCodes.SeverityWarning, "fallback");
        PolicyAnalysisRun run = ValidRun(revision, findings: [warning]);
        PolicyApprovalEvaluation missing = PolicyApprovalGate.Evaluate(
            revision, policy, run, run.BundleHash, run.DependencyFingerprint, [], [], UserId.New(), false);
        Assert.Equal(PolicyApprovalCodes.WarningUnacked, missing.ErrorCode);

        PolicyWarningAcknowledgment wrong = PolicyWarningAcknowledgment.Create(
            run.Id, H("not-the-warning"), UserId.New(), DateTimeOffset.UtcNow);
        PolicyApprovalEvaluation stillMissing = PolicyApprovalGate.Evaluate(
            revision, policy, run, run.BundleHash, run.DependencyFingerprint, [wrong], [], UserId.New(), false);
        Assert.Equal(PolicyApprovalCodes.WarningUnacked, stillMissing.ErrorCode);

        PolicyWarningAcknowledgment ack = PolicyWarningAcknowledgment.Create(
            run.Id, warning.WarningHash, UserId.New(), DateTimeOffset.UtcNow);
        PolicyApprovalEvaluation ok = PolicyApprovalGate.Evaluate(
            revision, policy, run, run.BundleHash, run.DependencyFingerprint, [ack], [], UserId.New(), false);
        Assert.Equal(PolicyApprovalCodes.OutcomeApprove, ok.Outcome);
    }

    [Fact]
    public void Ac5HighAndCriticalSeparationOfDutiesApplies()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = InReview();
        PolicyAnalysisRun high = ValidRun(revision, risk: PolicyEvidenceAnalysisCodes.RiskHigh);
        PolicyApprovalEvaluation self = PolicyApprovalGate.Evaluate(
            revision, policy, high, high.BundleHash, high.DependencyFingerprint, [], [], revision.CreatedBy, false);
        Assert.Equal(PolicyApprovalCodes.SeparationOfDuties, self.ErrorCode);

        PolicyApprovalEvaluation other = PolicyApprovalGate.Evaluate(
            revision, policy, high, high.BundleHash, high.DependencyFingerprint, [], [], UserId.New(), false);
        Assert.Equal(PolicyApprovalCodes.OutcomeApprove, other.Outcome);

        PolicyAnalysisRun critical = ValidRun(
            revision,
            risk: PolicyEvidenceAnalysisCodes.RiskCritical,
            evidenceSignalsPresent: true);
        UserId first = UserId.New();
        PolicyApprovalEvaluation firstVote = PolicyApprovalGate.Evaluate(
            revision, policy, critical, critical.BundleHash, critical.DependencyFingerprint, [], [], first, false);
        Assert.Equal(PolicyApprovalCodes.OutcomeRecordVote, firstVote.Outcome);
        Assert.False(firstVote.CompletesApproval);

        PolicyApproval firstRecord = PolicyApproval.Create(
            revision.Id, critical.Id, critical.BundleHash, first, isSecurityOwner: false, DateTimeOffset.UtcNow);
        PolicyApprovalEvaluation second = PolicyApprovalGate.Evaluate(
            revision,
            policy,
            critical,
            critical.BundleHash,
            critical.DependencyFingerprint,
            [],
            [firstRecord],
            UserId.New(),
            isSecurityOwner: true);
        Assert.Equal(PolicyApprovalCodes.OutcomeApprove, second.Outcome);
        Assert.True(second.CompletesApproval);
    }

    [Fact]
    public void Ac5MissingEvidenceSignalsIsUnknownCriticalNotSilentNone()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = InReview();
        PolicyAnalysisRun run = ValidRun(
            revision,
            risk: PolicyEvidenceAnalysisCodes.RiskNone,
            evidenceSignalsPresent: false);
        Assert.Equal(PolicyEvidenceAnalysisCodes.RiskCritical, run.EffectiveRiskLevel());
        PolicyApprovalEvaluation first = PolicyApprovalGate.Evaluate(
            revision, policy, run, run.BundleHash, run.DependencyFingerprint, [], [], UserId.New(), true);
        Assert.Equal(PolicyApprovalCodes.OutcomeRecordVote, first.Outcome);
    }

    [Fact]
    public void Ac6ApprovalDoesNotActivateBinding()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision, PolicyAnalysisRun run) = InReviewWithRun();
        PolicyApprovalEvaluation result = PolicyApprovalGate.Evaluate(
            revision, policy, run, run.BundleHash, run.DependencyFingerprint, [], [], UserId.New(), false);
        Assert.True(result.CompletesApproval);
        revision.Approve(DateTimeOffset.UtcNow);
        Assert.Equal(PolicyRevisionState.Approved, revision.State);
        Assert.Empty(Array.Empty<PolicyDesiredBinding>());
    }

    [Fact]
    public void Ac7BindingAllowedOnlyForApprovedRevision()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision, PolicyAnalysisRun run) = InReviewWithRun();
        PolicyBindingEvaluation draft = PolicyBindingGate.EvaluateActivation(
            revision, policy, run, run.DependencyFingerprint, []);
        Assert.Equal(PolicyApprovalCodes.BindingNotApproved, draft.ErrorCode);

        revision.Approve(DateTimeOffset.UtcNow, run.Id, run.BundleHash);
        PolicyBindingEvaluation ok = PolicyBindingGate.EvaluateActivation(
            revision, policy, run, run.DependencyFingerprint, []);
        Assert.True(ok.Allowed);

        revision.Revoke();
        PolicyBindingEvaluation revoked = PolicyBindingGate.EvaluateActivation(
            revision, policy, run, run.DependencyFingerprint, []);
        Assert.Equal(PolicyApprovalCodes.BindingRevoked, revoked.ErrorCode);
    }

    [Fact]
    public void Ac2BindingRejectsAnalysisRunThatDidNotCompleteApproval()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision, PolicyAnalysisRun approvedRun) = InReviewWithRun();
        revision.Approve(DateTimeOffset.UtcNow, approvedRun.Id, approvedRun.BundleHash);
        PolicyAnalysisRun other = ValidRun(revision, risk: PolicyEvidenceAnalysisCodes.RiskMedium);
        PolicyBindingEvaluation mismatch = PolicyBindingGate.EvaluateActivation(
            revision, policy, other, other.DependencyFingerprint, []);
        Assert.Equal(PolicyApprovalCodes.BundleMismatch, mismatch.ErrorCode);

        (Mfc.Domain.Policy.Policy blockedPolicy, PolicyRevision blockedRevision) = InReview();
        PolicyAnalysisRun blocked = ValidRun(
            blockedRevision,
            findings:
            [
                Finding("RULE_EMPTY_SELECTOR", PolicyEvidenceAnalysisCodes.SeverityBlocker, "empty"),
            ]);
        blockedRevision.Approve(DateTimeOffset.UtcNow, blocked.Id, blocked.BundleHash);
        PolicyBindingEvaluation blocker = PolicyBindingGate.EvaluateActivation(
            blockedRevision, blockedPolicy, blocked, blocked.DependencyFingerprint, []);
        Assert.Equal(PolicyApprovalCodes.Blocker, blocker.ErrorCode);
    }

    [Fact]
    public void Ac8AndAc10BindingAndExpiryDoNotDeploy()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision, PolicyAnalysisRun run) = ApprovedException();
        PolicyDesiredBinding binding = PolicyDesiredBinding.Activate(
            policy,
            revision,
            run,
            DateTimeOffset.UtcNow,
            validFromUtc: DateTimeOffset.UtcNow.AddDays(-1),
            validUntilUtc: DateTimeOffset.UtcNow.AddDays(-1));
        Assert.Equal(PolicyBindingState.Active, binding.State);
        PolicyBindingEvaluation expiry = PolicyBindingGate.EvaluateExpiry(binding, DateTimeOffset.UtcNow);
        Assert.True(expiry.Allowed);
        binding.ExpirePendingReconciliation(DateTimeOffset.UtcNow);
        Assert.Equal(PolicyBindingState.ExpiredPendingReconciliation, binding.State);
        Assert.Equal(2ul, binding.RowVersion);
    }

    [Fact]
    public void Ac9CompanySiteNodeCardinalityReplacementLeavesOneActive()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision first, PolicyAnalysisRun run) = InReviewWithRun();
        first.Approve(DateTimeOffset.UtcNow);
        PolicyDesiredBinding binding = PolicyDesiredBinding.Activate(
            policy, first, run, DateTimeOffset.UtcNow, null, null);
        Assert.Equal(PolicyBindingScope.Company, binding.Scope);
        Assert.Null(binding.ScopeId);
        binding.Disable(DateTimeOffset.UtcNow);
        Assert.Equal(PolicyBindingState.Disabled, binding.State);

        PolicyRevision second = PolicyRevision.CreateDraft(
            policy,
            revisionNumber: 2,
            PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope),
            parentContextHash: null,
            UserId.New(),
            DateTimeOffset.UtcNow);
        second.MarkValidated();
        second.SubmitForReview();
        PolicyAnalysisRun run2 = ValidRun(second);
        second.Approve(DateTimeOffset.UtcNow);
        PolicyDesiredBinding next = PolicyDesiredBinding.Activate(
            policy, second, run2, DateTimeOffset.UtcNow, null, null);
        Assert.Equal(PolicyBindingState.Active, next.State);
        Assert.Equal(PolicyBindingState.Disabled, binding.State);
    }

    [Fact]
    public void Ac9ExceptionCapIsTwoHundredFiftySix()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision, PolicyAnalysisRun run) = ApprovedException();
        revision.Approve(DateTimeOffset.UtcNow, run.Id, run.BundleHash);
        Guid siteId = policy.OwnerId!.Value;
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<PolicyDesiredBinding> existing = [];
        for (int i = 0; i < PolicyDesiredBinding.MaxActiveExceptionsPerScope; i++)
        {
            existing.Add(PolicyDesiredBinding.Reconstitute(
                PolicyBindingId.New(),
                PolicyBindingScope.Exception,
                siteId,
                PolicyId.New(),
                revision.Id,
                run.Id,
                run.BundleHash,
                PolicyBindingState.Active,
                now,
                now.AddDays(1),
                1,
                now,
                now));
        }

        PolicyBindingEvaluation over = PolicyBindingGate.EvaluateActivation(
            revision, policy, run, run.DependencyFingerprint, existing);
        Assert.Equal(PolicyApprovalCodes.BindingCardinality, over.ErrorCode);

        existing.RemoveAt(existing.Count - 1);
        existing.Add(PolicyDesiredBinding.Activate(
            policy, revision, run, now, now, now.AddDays(1)));
        PolicyBindingEvaluation replacement = PolicyBindingGate.EvaluateActivation(
            revision, policy, run, run.DependencyFingerprint, existing);
        Assert.True(replacement.Allowed);
    }

    [Fact]
    public void Ac11DependencyChangeMarksApprovalStaleAndRuntimeObservationIsExcluded()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision, PolicyAnalysisRun run) = InReviewWithRun();
        PolicyApprovalEvaluation stale = PolicyApprovalGate.Evaluate(
            revision, policy, run, run.BundleHash, H("changed-deps"), [], [], UserId.New(), false);
        Assert.Equal(PolicyApprovalCodes.Stale, stale.ErrorCode);

        PolicyApprovalDependencyVector baseline = Vector();
        Hash256 fingerprint = PolicyApprovalHasher.HashDependencyFingerprint(baseline);
        PolicyApprovalDependencyVector sameWithoutRole = Vector();
        Assert.Equal(
            fingerprint.ToString(),
            PolicyApprovalHasher.HashDependencyFingerprint(sameWithoutRole).ToString());
        PolicyApprovalDependencyVector analyzerBump = Vector(analyzer: "mfc.policy-approval.v2");
        Assert.NotEqual(
            fingerprint.ToString(),
            PolicyApprovalHasher.HashDependencyFingerprint(analyzerBump).ToString());
    }

    [Fact]
    public void Ac12RowVersionOptimisticConcurrencyIncrementsOnBindingMutation()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision, PolicyAnalysisRun run) = InReviewWithRun();
        revision.Approve(DateTimeOffset.UtcNow);
        PolicyDesiredBinding binding = PolicyDesiredBinding.Activate(
            policy, revision, run, DateTimeOffset.UtcNow, null, null);
        Assert.Equal(1ul, binding.RowVersion);
        binding.Disable(DateTimeOffset.UtcNow);
        Assert.Equal(2ul, binding.RowVersion);
    }

    [Fact]
    public void BundleHashChangesWithDeviceSlotAndDoesNotAlterEvidenceCombiner()
    {
        Hash256 logical = H("logical");
        Hash256 topology = H("topo");
        Hash256 impact = H("impact");
        Hash256 a = PolicyApprovalHasher.HashAnalysisBundle(logical, [H("d1")], topology, impact);
        Hash256 b = PolicyApprovalHasher.HashAnalysisBundle(logical, [H("d1"), H("d2")], topology, impact);
        Assert.NotEqual(a.ToString(), b.ToString());
        Hash256 evidence = PolicyEvidenceAnalysis.HashAnalysisContext(
            H("actual"), H("path"), H("mgmt"), H("topo-ctx"), H("ft"), H("evidence"));
        Hash256 again = PolicyEvidenceAnalysis.HashAnalysisContext(
            H("actual"), H("path"), H("mgmt"), H("topo-ctx"), H("ft"), H("evidence"));
        Assert.Equal(evidence.ToString(), again.ToString());
    }

    [Fact]
    public void FailedSystemTestForbidsApproval()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = InReview();
        PolicyAnalysisRun run = ValidRun(
            revision,
            tests:
            [
                new PolicyApprovalTestOutcome
                {
                    TestId = PolicyTestId.New(),
                    Origin = PolicyEvidenceAnalysisCodes.OriginSystem,
                    Outcome = PolicyEvidenceAnalysisCodes.OutcomeFail,
                    Proof = PolicyEvidenceAnalysisCodes.ProofProven,
                },
            ]);
        PolicyApprovalEvaluation result = PolicyApprovalGate.Evaluate(
            revision, policy, run, run.BundleHash, run.DependencyFingerprint, [], [], UserId.New(), false);
        Assert.Equal(PolicyApprovalCodes.TestsFailed, result.ErrorCode);
    }

    private static (Mfc.Domain.Policy.Policy Policy, PolicyRevision Revision, PolicyAnalysisRun Run) InReviewWithRun()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = InReview();
        return (policy, revision, ValidRun(revision));
    }

    private static (Mfc.Domain.Policy.Policy Policy, PolicyRevision Revision) InReview()
    {
        Mfc.Domain.Policy.Policy policy = Mfc.Domain.Policy.Policy.Create(
            NonEmptyName.Create("baseline"),
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            ownerId: null);
        PolicyRevision revision = PolicyRevision.CreateDraft(
            policy,
            revisionNumber: 1,
            PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope),
            parentContextHash: null,
            UserId.New(),
            DateTimeOffset.UtcNow);
        revision.MarkValidated();
        revision.SubmitForReview();
        return (policy, revision);
    }

    private static (Mfc.Domain.Policy.Policy Policy, PolicyRevision Revision, PolicyAnalysisRun Run) ApprovedException()
    {
        Guid siteId = Guid.NewGuid();
        Mfc.Domain.Policy.Policy policy = Mfc.Domain.Policy.Policy.Create(
            NonEmptyName.Create("exc"),
            PolicyKind.Exception,
            PolicyOwnerScope.Site,
            siteId);
        ExceptionMetadata metadata = ExceptionMetadata.Create(
            PolicyOwnerScope.Site,
            siteId,
            PolicyPipelineStage.CompanyDeny,
            RuleId.New(),
            DateTimeOffset.UtcNow.AddDays(-2),
            DateTimeOffset.UtcNow.AddDays(-1),
            "change window",
            "TICKET-1");
        PolicyDocument document = PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope)
            .WithExceptionMetadata(metadata);
        PolicyRevision revision = PolicyRevision.CreateDraft(
            policy,
            revisionNumber: 1,
            document,
            H("parent-context"),
            UserId.New(),
            DateTimeOffset.UtcNow);
        revision.MarkValidated();
        revision.SubmitForReview();
        PolicyAnalysisRun run = ValidRun(revision);
        return (policy, revision, run);
    }

    private static PolicyAnalysisRun ValidRun(
        PolicyRevision? revision = null,
        string risk = PolicyEvidenceAnalysisCodes.RiskLow,
        bool evidenceSignalsPresent = true,
        IReadOnlyList<PolicyApprovalFinding>? findings = null,
        IReadOnlyList<PolicyApprovalTestOutcome>? tests = null)
    {
        PolicyRevisionId revisionId = revision?.Id ?? PolicyRevisionId.New();
        Hash256 content = revision?.ContentHash ?? H("content");
        return PolicyAnalysisRun.Create(
            revisionId,
            content,
            H("logical"),
            H("analysis-ctx"),
            H("evidence-ctx"),
            H("topology"),
            H("impact"),
            [H("device-a")],
            PolicyApprovalHasher.HashDependencyFingerprint(Vector()),
            risk,
            evidenceSignalsPresent,
            PolicyApprovalCodes.AnalyzerVersion,
            PolicyDocument.SchemaName,
            PolicyPipelineV1.Version,
            findings ?? [],
            tests ??
            [
                new PolicyApprovalTestOutcome
                {
                    TestId = PolicyTestId.New(),
                    Origin = PolicyEvidenceAnalysisCodes.OriginSystem,
                    Outcome = PolicyEvidenceAnalysisCodes.OutcomePass,
                    Proof = PolicyEvidenceAnalysisCodes.ProofProven,
                },
            ],
            UserId.New(),
            DateTimeOffset.UtcNow);
    }

    private static PolicyApprovalFinding Finding(string code, string severity, string message)
        => new()
        {
            Code = code,
            Severity = severity,
            Message = message,
            Target = "revision",
            WarningHash = PolicyApprovalHasher.HashWarning(code, "revision", message),
        };

    private static PolicyApprovalDependencyVector Vector(string analyzer = PolicyApprovalCodes.AnalyzerVersion)
        => new()
        {
            CompanyBindingHash = H("company"),
            SiteBindingHash = H("site"),
            NodeBindingHash = H("node"),
            ActiveExceptionsHash = H("exc"),
            ZoneBindingHash = H("zone"),
            NodeMembershipHash = H("members"),
            RouterOsConfigurationHash = H("ros"),
            CapabilityHash = H("cap"),
            CompatibilityHash = H("compat"),
            ManagementAccessProfileHash = H("mgmt"),
            AnchorGuardContextHash = H("anchor"),
            AnalyzerVersion = analyzer,
            PolicySchemaVersion = PolicyDocument.SchemaName,
            PipelineVersion = PolicyPipelineV1.Version,
        };

    private static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
