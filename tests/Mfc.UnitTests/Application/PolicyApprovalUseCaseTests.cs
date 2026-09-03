using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class PolicyApprovalUseCaseTests
{
    [Fact]
    public async Task Ac6AndAc13ApproveDoesNotBindAndIsAudited()
    {
        Fixture fx = await SeedInReviewAsync();
        ApplicationResult<PolicyApprovalVoteView> vote = await fx.Approve.ExecuteAsync(ApproveCommand(fx, "reviewer"));
        Assert.True(vote.IsSuccess);
        Assert.True(vote.Value!.CompletesApproval);
        Assert.Equal(PolicyRevisionState.Approved, vote.Value.RevisionState);
        Assert.Empty(vote.Value.BindingIds);
        Assert.Contains(fx.Audit.Events, e => e.Action == ApproveRevisionUseCase.Operation);
        Assert.Contains(fx.Audit.Events, e => e.PayloadJson.Contains("\"binding_activated\":false", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac3BlockerForbidsApprovalUseCase()
    {
        Fixture fx = await SeedInReviewAsync(findings:
        [
            new PolicyApprovalFindingInput
            {
                Code = "RULE_EMPTY_SELECTOR",
                Severity = PolicyEvidenceAnalysisCodes.SeverityBlocker,
                Message = "empty",
            },
        ]);
        ApplicationResult<PolicyApprovalVoteView> vote = await fx.Approve.ExecuteAsync(ApproveCommand(fx, "reviewer"));
        Assert.True(vote.IsFailure);
        Assert.Equal(PolicyApprovalCodes.Blocker, vote.Error!.Code);
    }

    [Fact]
    public async Task Ac4WarningAckAndAc11StaleFingerprint()
    {
        PolicyApprovalFindingInput warning = new()
        {
            Code = "FASTTRACK_FALLBACK_REQUIRED",
            Severity = PolicyEvidenceAnalysisCodes.SeverityWarning,
            Message = "fallback",
        };
        Fixture fx = await SeedInReviewAsync(findings: [warning]);
        ApplicationResult<PolicyApprovalVoteView> unacked = await fx.Approve.ExecuteAsync(ApproveCommand(fx, "reviewer"));
        Assert.Equal(PolicyApprovalCodes.WarningUnacked, unacked.Error!.Code);

        Hash256 warningHash = PolicyApprovalHasher.HashWarning(warning.Code, warning.Target, warning.Message);
        ApplicationResult<PolicyAnalysisRunView> ack = await fx.Ack.ExecuteAsync(new AcknowledgeWarningCommand
        {
            Actor = "reviewer",
            IdempotencyKey = Guid.NewGuid(),
            AnalysisRunId = fx.RunId,
            WarningHash = warningHash.Bytes.ToArray(),
        });
        Assert.True(ack.IsSuccess);

        ApplicationResult<PolicyApprovalVoteView> stale = await fx.Approve.ExecuteAsync(ApproveCommand(
            fx, "reviewer", fingerprint: H("changed").Bytes.ToArray()));
        Assert.Equal(PolicyApprovalCodes.Stale, stale.Error!.Code);

        ApplicationResult<PolicyApprovalVoteView> ok = await fx.Approve.ExecuteAsync(ApproveCommand(fx, "reviewer"));
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public async Task Ac5AuthorCannotApproveHighAndMissingSignalsIsCritical()
    {
        Fixture high = await SeedInReviewAsync(risk: PolicyEvidenceAnalysisCodes.RiskHigh, author: "author");
        ApplicationResult<PolicyApprovalVoteView> self = await high.Approve.ExecuteAsync(ApproveCommand(high, "author"));
        Assert.Equal(PolicyApprovalCodes.SeparationOfDuties, self.Error!.Code);

        Fixture missing = await SeedInReviewAsync(
            risk: PolicyEvidenceAnalysisCodes.RiskNone,
            evidenceSignalsPresent: false);
        ApplicationResult<PolicyApprovalVoteView> first = await missing.Approve.ExecuteAsync(
            ApproveCommand(missing, "reviewer"));
        Assert.True(first.IsSuccess);
        Assert.False(first.Value!.CompletesApproval);
        Assert.Equal(PolicyRevisionState.InReview, first.Value.RevisionState);
    }

    [Fact]
    public async Task Ac7Ac8Ac9Ac10BindingSeparateFromApprovalAndExpiryDoesNotDeploy()
    {
        Fixture fx = await SeedInReviewAsync();
        ApplicationResult<PolicyBindingView> tooEarly = await fx.Bind.ExecuteAsync(BindCommand(fx, "binder"));
        Assert.Equal(PolicyApprovalCodes.BindingNotApproved, tooEarly.Error!.Code);

        ApplicationResult<PolicyApprovalVoteView> vote = await fx.Approve.ExecuteAsync(ApproveCommand(fx, "reviewer"));
        Assert.True(vote.IsSuccess);
        ApplicationResult<PolicyBindingView> bound = await fx.Bind.ExecuteAsync(BindCommand(fx, "binder"));
        Assert.True(bound.IsSuccess);
        Assert.False(bound.Value!.DeploymentStarted);
        Assert.Equal(PolicyBindingState.Active, bound.Value.State);
        Assert.Equal(1ul, bound.Value.RowVersion);

        Fixture second = await SeedInReviewAsync(policies: fx.Policies, approvals: fx.Approvals, name: "baseline-2");
        ApplicationResult<PolicyApprovalVoteView> vote2 = await second.Approve.ExecuteAsync(ApproveCommand(second, "reviewer"));
        Assert.True(vote2.IsSuccess);
        ApplicationResult<PolicyBindingView> replaced = await second.Bind.ExecuteAsync(BindCommand(second, "binder"));
        Assert.True(replaced.IsSuccess);
        PolicyDesiredBinding? first = await fx.Approvals.GetBindingAsync(new PolicyBindingId(bound.Value.Id));
        Assert.Equal(PolicyBindingState.Disabled, first!.State);

        ApplicationResult<PolicyBindingView> notException = await fx.Expire.ExecuteAsync(new ExpireExceptionBindingCommand
        {
            Actor = "binder",
            IdempotencyKey = Guid.NewGuid(),
            BindingId = replaced.Value!.Id,
            ExpectedRowVersion = replaced.Value.RowVersion,
        });
        Assert.Equal(PolicyApprovalCodes.BindingNotException, notException.Error!.Code);
    }

    [Fact]
    public async Task Ac12IdempotencyReplayAndCasConflict()
    {
        Fixture fx = await SeedInReviewAsync();
        Guid key = Guid.NewGuid();
        ApproveRevisionCommand command = ApproveCommand(fx, "reviewer", key);
        ApplicationResult<PolicyApprovalVoteView> first = await fx.Approve.ExecuteAsync(command);
        ApplicationResult<PolicyApprovalVoteView> replay = await fx.Approve.ExecuteAsync(command);
        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value!.ApprovalId, replay.Value!.ApprovalId);

        ApplicationResult<PolicyApprovalVoteView> cas = await fx.Approve.ExecuteAsync(ApproveCommand(
            fx, "other", fingerprint: fx.Fingerprint, content: H("wrong-content").Bytes.ToArray()));
        Assert.Equal("conflict", cas.Error!.Code);
    }

    [Fact]
    public async Task ForbiddenApproveAndBindAreDenied()
    {
        Fixture fx = await SeedInReviewAsync();
        fx.Auth.DeniedPermissions.Add(ApplicationPermissions.PolicyApprove);
        ApplicationResult<PolicyApprovalVoteView> deniedApprove = await fx.Approve.ExecuteAsync(
            ApproveCommand(fx, "reviewer"));
        Assert.Equal("forbidden", deniedApprove.Error!.Code);

        fx.Auth.DeniedPermissions.Clear();
        fx.Auth.DeniedPermissions.Add(ApplicationPermissions.PolicyBind);
        await fx.Approve.ExecuteAsync(ApproveCommand(fx, "reviewer"));
        ApplicationResult<PolicyBindingView> deniedBind = await fx.Bind.ExecuteAsync(BindCommand(fx, "binder"));
        Assert.Equal("forbidden", deniedBind.Error!.Code);
    }

    [Fact]
    public async Task MutationFailurePathsCoverAuthCasValidationAndMissingResources()
    {
        Fixture fx = await SeedInReviewAsync();
        ApplicationResult<PolicyAnalysisRunView> emptyKey = await fx.Record.ExecuteAsync(RecordCommand(fx, Guid.Empty));
        Assert.Equal("validation", emptyKey.Error!.Code);

        fx.Auth.DeniedPermissions.Add(ApplicationPermissions.PolicyWrite);
        ApplicationResult<PolicyAnalysisRunView> deniedRecord = await fx.Record.ExecuteAsync(RecordCommand(fx));
        Assert.Equal("forbidden", deniedRecord.Error!.Code);
        fx.Auth.DeniedPermissions.Clear();

        RecordAnalysisRunCommand badHash = RecordCommand(fx);
        badHash = new RecordAnalysisRunCommand
        {
            Actor = badHash.Actor,
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = badHash.RevisionId,
            ExpectedContentHash = badHash.ExpectedContentHash,
            LogicalEffectiveHash = new byte[31],
            AnalysisContextHash = badHash.AnalysisContextHash,
            EvidenceContextHash = badHash.EvidenceContextHash,
            TopologyProjectionHash = badHash.TopologyProjectionHash,
            ImpactSetHash = badHash.ImpactSetHash,
            PerDeviceAnalysisHashes = badHash.PerDeviceAnalysisHashes,
            DependencyFingerprint = badHash.DependencyFingerprint,
            RiskLevel = badHash.RiskLevel,
            EvidenceSignalsPresent = badHash.EvidenceSignalsPresent,
            AnalyzerVersion = badHash.AnalyzerVersion,
            PolicySchemaVersion = badHash.PolicySchemaVersion,
            PipelineVersion = badHash.PipelineVersion,
            Findings = badHash.Findings,
            TestResults = badHash.TestResults,
        };
        ApplicationResult<PolicyAnalysisRunView> hashFail = await fx.Record.ExecuteAsync(badHash);
        Assert.Equal("validation", hashFail.Error!.Code);

        ApplicationResult<PolicyRevisionView> resubmit = await fx.Submit.ExecuteAsync(new SubmitRevisionForReviewCommand
        {
            Actor = "author",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = fx.RevisionId,
            ExpectedContentHash = fx.ContentHash,
        });
        Assert.Equal(PolicyApprovalCodes.NotInReview, resubmit.Error!.Code);

        ApplicationResult<PolicyApprovalVoteView> missingRun = await fx.Approve.ExecuteAsync(new ApproveRevisionCommand
        {
            Actor = "reviewer",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = fx.RevisionId,
            AnalysisRunId = Guid.NewGuid(),
            ExpectedContentHash = fx.ContentHash,
            ExpectedBundleHash = fx.BundleHash,
            CurrentDependencyFingerprint = fx.Fingerprint,
        });
        Assert.Equal(PolicyApprovalCodes.MissingRun, missingRun.Error!.Code);

        ApplicationResult<PolicyAnalysisRunView> missingAck = await fx.Ack.ExecuteAsync(new AcknowledgeWarningCommand
        {
            Actor = "reviewer",
            IdempotencyKey = Guid.NewGuid(),
            AnalysisRunId = Guid.NewGuid(),
            WarningHash = fx.BundleHash,
        });
        Assert.Equal("not_found", missingAck.Error!.Code);

        await fx.Approve.ExecuteAsync(ApproveCommand(fx, "reviewer"));
        ApplicationResult<PolicyBindingView> staleBind = await fx.Bind.ExecuteAsync(new ActivateDesiredBindingCommand
        {
            Actor = "binder",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = fx.RevisionId,
            AnalysisRunId = fx.RunId,
            ExpectedContentHash = fx.ContentHash,
            CurrentDependencyFingerprint = H("stale").Bytes.ToArray(),
        });
        Assert.Equal(PolicyApprovalCodes.BindingStale, staleBind.Error!.Code);

        ApplicationResult<PolicyBindingView> missingExpire = await fx.Expire.ExecuteAsync(new ExpireExceptionBindingCommand
        {
            Actor = "binder",
            IdempotencyKey = Guid.NewGuid(),
            BindingId = Guid.NewGuid(),
            ExpectedRowVersion = 1,
        });
        Assert.Equal("not_found", missingExpire.Error!.Code);

        ApplicationResult<PolicyBindingView> bound = await fx.Bind.ExecuteAsync(BindCommand(fx, "binder"));
        Assert.True(bound.IsSuccess);
        ApplicationResult<PolicyBindingView> casExpire = await fx.Expire.ExecuteAsync(new ExpireExceptionBindingCommand
        {
            Actor = "binder",
            IdempotencyKey = Guid.NewGuid(),
            BindingId = bound.Value!.Id,
            ExpectedRowVersion = 99,
        });
        Assert.Equal("conflict", casExpire.Error!.Code);
        ApplicationResult<PolicyBindingView> emptyExpireKey = await fx.Expire.ExecuteAsync(new ExpireExceptionBindingCommand
        {
            Actor = "binder",
            IdempotencyKey = Guid.Empty,
            BindingId = bound.Value.Id,
            ExpectedRowVersion = bound.Value.RowVersion,
        });
        Assert.Equal("validation", emptyExpireKey.Error!.Code);

        fx.Auth.DeniedPermissions.Add(ApplicationPermissions.PolicyBind);
        ApplicationResult<PolicyBindingView> deniedExpire = await fx.Expire.ExecuteAsync(new ExpireExceptionBindingCommand
        {
            Actor = "binder",
            IdempotencyKey = Guid.NewGuid(),
            BindingId = bound.Value.Id,
            ExpectedRowVersion = bound.Value.RowVersion,
        });
        Assert.Equal("forbidden", deniedExpire.Error!.Code);
        fx.Auth.DeniedPermissions.Clear();

        ApplicationResult<PolicyApprovalVoteView> shortBundle = await fx.Approve.ExecuteAsync(new ApproveRevisionCommand
        {
            Actor = "reviewer",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = fx.RevisionId,
            AnalysisRunId = fx.RunId,
            ExpectedContentHash = fx.ContentHash,
            ExpectedBundleHash = new byte[31],
            CurrentDependencyFingerprint = fx.Fingerprint,
        });
        Assert.Equal("validation", shortBundle.Error!.Code);
    }

    [Fact]
    public async Task IdempotencyReplayCoversRecordBindAckAndUnknownWarningHash()
    {
        PolicyApprovalFindingInput warning = new()
        {
            Code = "FASTTRACK_FALLBACK_REQUIRED",
            Severity = PolicyEvidenceAnalysisCodes.SeverityWarning,
            Message = "fallback",
        };
        Fixture fx = await SeedInReviewAsync(findings: [warning]);
        Guid recordKey = Guid.NewGuid();
        RecordAnalysisRunCommand record = RecordCommand(fx, recordKey);
        ApplicationResult<PolicyAnalysisRunView> first = await fx.Record.ExecuteAsync(record);
        ApplicationResult<PolicyAnalysisRunView> replay = await fx.Record.ExecuteAsync(record);
        Assert.True(first.IsSuccess);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value!.Id, replay.Value!.Id);

        Hash256 warningHash = PolicyApprovalHasher.HashWarning(warning.Code, warning.Target, warning.Message);
        Guid ackKey = Guid.NewGuid();
        AcknowledgeWarningCommand ack = new()
        {
            Actor = "reviewer",
            IdempotencyKey = ackKey,
            AnalysisRunId = fx.RunId,
            WarningHash = warningHash.Bytes.ToArray(),
        };
        Assert.True((await fx.Ack.ExecuteAsync(ack)).IsSuccess);
        Assert.True((await fx.Ack.ExecuteAsync(ack)).IsSuccess);
        ApplicationResult<PolicyAnalysisRunView> unknownWarning = await fx.Ack.ExecuteAsync(new AcknowledgeWarningCommand
        {
            Actor = "reviewer",
            IdempotencyKey = Guid.NewGuid(),
            AnalysisRunId = fx.RunId,
            WarningHash = H("missing-warning").Bytes.ToArray(),
        });
        Assert.Equal(PolicyApprovalCodes.WarningUnacked, unknownWarning.Error!.Code);

        Assert.True((await fx.Approve.ExecuteAsync(ApproveCommand(fx, "reviewer"))).IsSuccess);
        Guid bindKey = Guid.NewGuid();
        ActivateDesiredBindingCommand bind = new()
        {
            Actor = "binder",
            IdempotencyKey = bindKey,
            RevisionId = fx.RevisionId,
            AnalysisRunId = fx.RunId,
            ExpectedContentHash = fx.ContentHash,
            CurrentDependencyFingerprint = fx.Fingerprint,
        };
        ApplicationResult<PolicyBindingView> bound = await fx.Bind.ExecuteAsync(bind);
        ApplicationResult<PolicyBindingView> bindReplay = await fx.Bind.ExecuteAsync(bind);
        Assert.True(bound.IsSuccess);
        Assert.True(bindReplay.IsSuccess);
        Assert.Equal(bound.Value!.Id, bindReplay.Value!.Id);
    }

    [Fact]
    public async Task CompletingVoteRecoversWhenRevisionStillInReview()
    {
        Fixture fx = await SeedInReviewAsync();
        PolicyApproval orphan = PolicyApproval.Create(
            new PolicyRevisionId(fx.RevisionId),
            new PolicyAnalysisRunId(fx.RunId),
            Hash256.Create(fx.BundleHash),
            new UserId(ActorKey.FromActor("reviewer")),
            isSecurityOwner: false,
            DateTimeOffset.UtcNow);
        await fx.Approvals.AddApprovalAsync(orphan);
        ApplicationResult<PolicyApprovalVoteView> recovered = await fx.Approve.ExecuteAsync(
            ApproveCommand(fx, "reviewer"));
        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.True(recovered.Value!.CompletesApproval);
        Assert.Equal(PolicyRevisionState.Approved, recovered.Value.RevisionState);
        Assert.Equal(orphan.Id.Value, recovered.Value.ApprovalId);
        Assert.Contains(fx.Audit.Events, e => e.Action == ApproveRevisionUseCase.Operation);
        Assert.Contains(fx.Audit.Events, e => e.PayloadJson.Contains("\"binding_activated\":false", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BindingRejectsAnalysisRunThatDidNotCompleteApproval()
    {
        Fixture fx = await SeedInReviewAsync();
        Assert.True((await fx.Approve.ExecuteAsync(ApproveCommand(fx, "reviewer"))).IsSuccess);
        RecordAnalysisRunCommand other = RecordCommand(fx);
        other = new RecordAnalysisRunCommand
        {
            Actor = other.Actor,
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = other.RevisionId,
            ExpectedContentHash = other.ExpectedContentHash,
            LogicalEffectiveHash = H("logical-b").Bytes.ToArray(),
            AnalysisContextHash = other.AnalysisContextHash,
            EvidenceContextHash = other.EvidenceContextHash,
            TopologyProjectionHash = other.TopologyProjectionHash,
            ImpactSetHash = other.ImpactSetHash,
            PerDeviceAnalysisHashes = other.PerDeviceAnalysisHashes,
            DependencyFingerprint = other.DependencyFingerprint,
            RiskLevel = other.RiskLevel,
            EvidenceSignalsPresent = other.EvidenceSignalsPresent,
            AnalyzerVersion = other.AnalyzerVersion,
            PolicySchemaVersion = other.PolicySchemaVersion,
            PipelineVersion = other.PipelineVersion,
            Findings = other.Findings,
            TestResults = other.TestResults,
        };
        ApplicationResult<PolicyAnalysisRunView> later = await fx.Record.ExecuteAsync(other);
        Assert.True(later.IsSuccess, later.Error?.Message);
        ApplicationResult<PolicyBindingView> mismatch = await fx.Bind.ExecuteAsync(new ActivateDesiredBindingCommand
        {
            Actor = "binder",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = fx.RevisionId,
            AnalysisRunId = later.Value!.Id,
            ExpectedContentHash = fx.ContentHash,
            CurrentDependencyFingerprint = fx.Fingerprint,
        });
        Assert.Equal(PolicyApprovalCodes.BundleMismatch, mismatch.Error!.Code);
    }

    [Fact]
    public async Task RecordAnalysisRunIdempotencyConflictsWhenTestsDiffer()
    {
        Fixture fx = await SeedInReviewAsync();
        Guid key = Guid.NewGuid();
        RecordAnalysisRunCommand first = RecordCommand(fx, key);
        Assert.True((await fx.Record.ExecuteAsync(first)).IsSuccess);
        RecordAnalysisRunCommand changed = RecordCommand(fx, key);
        changed = new RecordAnalysisRunCommand
        {
            Actor = changed.Actor,
            IdempotencyKey = key,
            RevisionId = changed.RevisionId,
            ExpectedContentHash = changed.ExpectedContentHash,
            LogicalEffectiveHash = changed.LogicalEffectiveHash,
            AnalysisContextHash = changed.AnalysisContextHash,
            EvidenceContextHash = changed.EvidenceContextHash,
            TopologyProjectionHash = changed.TopologyProjectionHash,
            ImpactSetHash = changed.ImpactSetHash,
            PerDeviceAnalysisHashes = changed.PerDeviceAnalysisHashes,
            DependencyFingerprint = changed.DependencyFingerprint,
            RiskLevel = changed.RiskLevel,
            EvidenceSignalsPresent = changed.EvidenceSignalsPresent,
            AnalyzerVersion = changed.AnalyzerVersion,
            PolicySchemaVersion = changed.PolicySchemaVersion,
            PipelineVersion = changed.PipelineVersion,
            Findings = changed.Findings,
            TestResults =
            [
                new PolicyApprovalTestInput
                {
                    TestId = Guid.NewGuid(),
                    Origin = PolicyEvidenceAnalysisCodes.OriginSystem,
                    Outcome = PolicyEvidenceAnalysisCodes.OutcomePass,
                    Proof = PolicyEvidenceAnalysisCodes.ProofProven,
                },
            ],
        };
        ApplicationResult<PolicyAnalysisRunView> conflict = await fx.Record.ExecuteAsync(changed);
        Assert.Equal("conflict", conflict.Error!.Code);
    }

    [Theory]
    [InlineData("analysis")]
    [InlineData("evidence")]
    [InlineData("topology")]
    [InlineData("impact")]
    [InlineData("fingerprint")]
    [InlineData("device")]
    public async Task RecordAnalysisRunRejectsShortComponentHashes(string which)
    {
        Fixture fx = await SeedInReviewAsync();
        RecordAnalysisRunCommand valid = RecordCommand(fx);
        byte[] shortHash = new byte[31];
        RecordAnalysisRunCommand command = new()
        {
            Actor = valid.Actor,
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = valid.RevisionId,
            ExpectedContentHash = valid.ExpectedContentHash,
            LogicalEffectiveHash = valid.LogicalEffectiveHash,
            AnalysisContextHash = which == "analysis" ? shortHash : valid.AnalysisContextHash,
            EvidenceContextHash = which == "evidence" ? shortHash : valid.EvidenceContextHash,
            TopologyProjectionHash = which == "topology" ? shortHash : valid.TopologyProjectionHash,
            ImpactSetHash = which == "impact" ? shortHash : valid.ImpactSetHash,
            PerDeviceAnalysisHashes = which == "device" ? [shortHash] : valid.PerDeviceAnalysisHashes,
            DependencyFingerprint = which == "fingerprint" ? shortHash : valid.DependencyFingerprint,
            RiskLevel = valid.RiskLevel,
            EvidenceSignalsPresent = valid.EvidenceSignalsPresent,
            AnalyzerVersion = valid.AnalyzerVersion,
            PolicySchemaVersion = valid.PolicySchemaVersion,
            PipelineVersion = valid.PipelineVersion,
            Findings = valid.Findings,
            TestResults = valid.TestResults,
        };
        ApplicationResult<PolicyAnalysisRunView> result = await fx.Record.ExecuteAsync(command);
        Assert.Equal("validation", result.Error!.Code);
    }

    private static RecordAnalysisRunCommand RecordCommand(Fixture fx, Guid? key = null)
        => new()
        {
            Actor = "author",
            IdempotencyKey = key ?? Guid.NewGuid(),
            RevisionId = fx.RevisionId,
            ExpectedContentHash = fx.ContentHash,
            LogicalEffectiveHash = H("logical").Bytes.ToArray(),
            AnalysisContextHash = H("analysis").Bytes.ToArray(),
            EvidenceContextHash = H("evidence").Bytes.ToArray(),
            TopologyProjectionHash = H("topology").Bytes.ToArray(),
            ImpactSetHash = H("impact").Bytes.ToArray(),
            PerDeviceAnalysisHashes = [H("device").Bytes.ToArray()],
            DependencyFingerprint = fx.Fingerprint,
            RiskLevel = PolicyEvidenceAnalysisCodes.RiskLow,
            EvidenceSignalsPresent = true,
            AnalyzerVersion = PolicyApprovalCodes.AnalyzerVersion,
            PolicySchemaVersion = PolicyDocument.SchemaName,
            PipelineVersion = PolicyPipelineV1.Version,
            Findings = [],
            TestResults =
            [
                new PolicyApprovalTestInput
                {
                    TestId = Guid.NewGuid(),
                    Origin = PolicyEvidenceAnalysisCodes.OriginSystem,
                    Outcome = PolicyEvidenceAnalysisCodes.OutcomePass,
                    Proof = PolicyEvidenceAnalysisCodes.ProofProven,
                },
            ],
        };

    private static async Task<Fixture> SeedInReviewAsync(
        string risk = PolicyEvidenceAnalysisCodes.RiskLow,
        bool evidenceSignalsPresent = true,
        IReadOnlyList<PolicyApprovalFindingInput>? findings = null,
        string author = "author",
        FakePolicyStore? policies = null,
        FakePolicyApprovalStore? approvals = null,
        string name = "baseline")
    {
        FakeAuthorizationBoundary auth = new();
        policies ??= new FakePolicyStore();
        approvals ??= new FakePolicyApprovalStore();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        FakeClock clock = new();
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<PolicyDraftView> draft = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = author,
            IdempotencyKey = Guid.NewGuid(),
            Name = name,
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
        });
        Assert.True(draft.IsSuccess);
        PolicyRevision? revision = await policies.GetRevisionAsync(new PolicyRevisionId(draft.Value!.RevisionId));
        Assert.NotNull(revision);
        revision!.MarkValidated();
        await policies.SaveRevisionAsync(revision);

        SubmitRevisionForReviewUseCase submit = new(auth, policies, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<PolicyRevisionView> submitted = await submit.ExecuteAsync(new SubmitRevisionForReviewCommand
        {
            Actor = author,
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = revision.Id.Value,
            ExpectedContentHash = revision.ContentHash.Bytes.ToArray(),
        });
        Assert.True(submitted.IsSuccess);

        byte[] fingerprint = PolicyApprovalHasher.HashDependencyFingerprint(Vector()).Bytes.ToArray();
        RecordAnalysisRunUseCase record = new(auth, policies, approvals, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<PolicyAnalysisRunView> run = await record.ExecuteAsync(new RecordAnalysisRunCommand
        {
            Actor = author,
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = revision.Id.Value,
            ExpectedContentHash = revision.ContentHash.Bytes.ToArray(),
            LogicalEffectiveHash = H("logical").Bytes.ToArray(),
            AnalysisContextHash = H("analysis").Bytes.ToArray(),
            EvidenceContextHash = H("evidence").Bytes.ToArray(),
            TopologyProjectionHash = H("topology").Bytes.ToArray(),
            ImpactSetHash = H("impact").Bytes.ToArray(),
            PerDeviceAnalysisHashes = [H("device").Bytes.ToArray()],
            DependencyFingerprint = fingerprint,
            RiskLevel = risk,
            EvidenceSignalsPresent = evidenceSignalsPresent,
            AnalyzerVersion = PolicyApprovalCodes.AnalyzerVersion,
            PolicySchemaVersion = PolicyDocument.SchemaName,
            PipelineVersion = PolicyPipelineV1.Version,
            Findings = findings ?? [],
            TestResults =
            [
                new PolicyApprovalTestInput
                {
                    TestId = Guid.NewGuid(),
                    Origin = PolicyEvidenceAnalysisCodes.OriginSystem,
                    Outcome = PolicyEvidenceAnalysisCodes.OutcomePass,
                    Proof = PolicyEvidenceAnalysisCodes.ProofProven,
                },
            ],
        });
        Assert.True(run.IsSuccess, run.Error?.Message);

        return new Fixture
        {
            Auth = auth,
            Policies = policies,
            Approvals = approvals,
            Audit = audit,
            Record = record,
            Submit = submit,
            Approve = new ApproveRevisionUseCase(auth, policies, approvals, idempotency, audit, new FakeUnitOfWork()),
            Ack = new AcknowledgeWarningUseCase(auth, approvals, idempotency, audit, new FakeUnitOfWork()),
            Bind = new ActivateDesiredBindingUseCase(auth, policies, approvals, idempotency, audit, clock, new FakeUnitOfWork()),
            Expire = new ExpireExceptionBindingUseCase(auth, approvals, idempotency, audit, clock, new FakeUnitOfWork()),
            RevisionId = revision.Id.Value,
            RunId = run.Value!.Id,
            ContentHash = revision.ContentHash.Bytes.ToArray(),
            BundleHash = Convert.FromHexString(run.Value.BundleHashHex),
            Fingerprint = fingerprint,
        };
    }

    private static ApproveRevisionCommand ApproveCommand(
        Fixture fx,
        string actor,
        Guid? key = null,
        byte[]? fingerprint = null,
        byte[]? content = null)
        => new()
        {
            Actor = actor,
            IdempotencyKey = key ?? Guid.NewGuid(),
            RevisionId = fx.RevisionId,
            AnalysisRunId = fx.RunId,
            ExpectedContentHash = content ?? fx.ContentHash,
            ExpectedBundleHash = fx.BundleHash,
            CurrentDependencyFingerprint = fingerprint ?? fx.Fingerprint,
        };

    private static ActivateDesiredBindingCommand BindCommand(Fixture fx, string actor)
        => new()
        {
            Actor = actor,
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = fx.RevisionId,
            AnalysisRunId = fx.RunId,
            ExpectedContentHash = fx.ContentHash,
            CurrentDependencyFingerprint = fx.Fingerprint,
        };

    private static PolicyApprovalDependencyVector Vector()
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
            AnalyzerVersion = PolicyApprovalCodes.AnalyzerVersion,
            PolicySchemaVersion = PolicyDocument.SchemaName,
            PipelineVersion = PolicyPipelineV1.Version,
        };

    private static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class Fixture
    {
        public required FakeAuthorizationBoundary Auth { get; init; }

        public required FakePolicyStore Policies { get; init; }

        public required FakePolicyApprovalStore Approvals { get; init; }

        public required FakeAuditEventWriter Audit { get; init; }

        public required RecordAnalysisRunUseCase Record { get; init; }

        public required SubmitRevisionForReviewUseCase Submit { get; init; }

        public required ApproveRevisionUseCase Approve { get; init; }

        public required AcknowledgeWarningUseCase Ack { get; init; }

        public required ActivateDesiredBindingUseCase Bind { get; init; }

        public required ExpireExceptionBindingUseCase Expire { get; init; }

        public required Guid RevisionId { get; init; }

        public required Guid RunId { get; init; }

        public required byte[] ContentHash { get; init; }

        public required byte[] BundleHash { get; init; }

        public required byte[] Fingerprint { get; init; }
    }
}
