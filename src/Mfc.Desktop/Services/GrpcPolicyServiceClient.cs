using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>gRPC PolicyService client bound to the current controller channel.</summary>
public sealed class GrpcPolicyServiceClient : IPolicyServiceClient
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcPolicyServiceClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<PolicyDraft> CreateDraftPolicyAsync(
        string name,
        PolicyKind kind,
        PolicyOwnerScope ownerScope,
        Guid? ownerId = null,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        CreateDraftPolicyRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            Name = name,
            Kind = kind,
            OwnerScope = ownerScope,
        };
        if (ownerId is Guid id)
        {
            request.OwnerId = DesktopProtoUuid.FromGuid(id);
        }

        return await client.CreateDraftPolicyAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevision> GetPolicyRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.GetPolicyRevisionAsync(
                new GetPolicyRevisionRequest { RevisionId = DesktopProtoUuid.FromGuid(revisionId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ListRulesResponse> ListRulesAsync(
        Guid revisionId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.ListRulesAsync(
                new ListRulesRequest
                {
                    RevisionId = DesktopProtoUuid.FromGuid(revisionId),
                    ActiveOnly = activeOnly,
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRuleMutation> AddRuleAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        uint ordinal,
        bool enabled,
        TrafficPredicate? predicate,
        RuleEffect effect,
        string description,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        AddRuleRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            RevisionId = DesktopProtoUuid.FromGuid(revisionId),
            ExpectedContentHash = ToSha256(expectedContentHash),
            Family = family,
            Chain = chain,
            Stage = stage,
            Ordinal = ordinal,
            Enabled = enabled,
            Effect = effect,
            Logging = new LogSpecification { Enabled = false },
            ExceptionEligible = false,
            Description = description,
        };
        if (predicate is not null)
        {
            request.Predicate = predicate;
        }

        return await client.AddRuleAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRuleMutation> ReorderRulesAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IpAddressFamily family,
        PolicyFilterChain chain,
        PolicyPipelineStage stage,
        IReadOnlyList<Guid> orderedRuleIds,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        ReorderRulesRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            RevisionId = DesktopProtoUuid.FromGuid(revisionId),
            ExpectedContentHash = ToSha256(expectedContentHash),
            Family = family,
            Chain = chain,
            Stage = stage,
        };
        request.OrderedRuleIds.AddRange(orderedRuleIds.Select(DesktopProtoUuid.FromGuid));
        return await client.ReorderRulesAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevision> ValidateRevisionAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.ValidateRevisionAsync(
                new ValidateRevisionRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = DesktopProtoUuid.FromGuid(revisionId),
                    ExpectedContentHash = ToSha256(expectedContentHash),
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevision> UpsertAddressObjectAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        Guid? objectId,
        string name,
        IpAddressFamily family,
        IReadOnlyList<AddressObjectEntry> entries,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        UpsertAddressObjectRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            RevisionId = DesktopProtoUuid.FromGuid(revisionId),
            ExpectedContentHash = ToSha256(expectedContentHash),
            Name = name,
            Family = family,
        };
        if (objectId is Guid id)
        {
            request.ObjectId = DesktopProtoUuid.FromGuid(id);
        }

        request.Entries.AddRange(entries);
        if (!string.IsNullOrWhiteSpace(description))
        {
            request.Description = description;
        }

        return await client.UpsertAddressObjectAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevision> UpsertServiceObjectAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        Guid? objectId,
        string name,
        IReadOnlyList<ServiceTerm> terms,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        UpsertServiceObjectRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            RevisionId = DesktopProtoUuid.FromGuid(revisionId),
            ExpectedContentHash = ToSha256(expectedContentHash),
            Name = name,
        };
        if (objectId is Guid id)
        {
            request.ObjectId = DesktopProtoUuid.FromGuid(id);
        }

        request.Terms.AddRange(terms);
        if (!string.IsNullOrWhiteSpace(description))
        {
            request.Description = description;
        }

        return await client.UpsertServiceObjectAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevision> ReplaceChainContractsAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        IReadOnlyList<ChainContract> contracts,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        ReplaceChainContractsRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            RevisionId = DesktopProtoUuid.FromGuid(revisionId),
            ExpectedContentHash = ToSha256(expectedContentHash),
        };
        request.Contracts.AddRange(contracts);
        return await client.ReplaceChainContractsAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevision> ReplacePolicyTestsAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        string? testsJson,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        ReplacePolicyTestsRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            RevisionId = DesktopProtoUuid.FromGuid(revisionId),
            ExpectedContentHash = ToSha256(expectedContentHash),
        };
        if (testsJson is not null)
        {
            request.TestsJson = testsJson;
        }

        return await client.ReplacePolicyTestsAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevisionDiff> DiffPolicyRevisionsAsync(
        Guid beforeRevisionId,
        Guid afterRevisionId,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.DiffPolicyRevisionsAsync(
                new DiffPolicyRevisionsRequest
                {
                    BeforeRevisionId = DesktopProtoUuid.FromGuid(beforeRevisionId),
                    AfterRevisionId = DesktopProtoUuid.FromGuid(afterRevisionId),
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<EffectivePolicy> ComposeEffectivePolicyAsync(
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.ComposeEffectivePolicyAsync(
                new ComposeEffectivePolicyRequest { NodeId = DesktopProtoUuid.FromGuid(nodeId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevision> SubmitRevisionForReviewAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.SubmitRevisionForReviewAsync(
                new SubmitRevisionForReviewRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = DesktopProtoUuid.FromGuid(revisionId),
                    ExpectedContentHash = ToSha256(expectedContentHash),
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyAnalysisRun> RecordAnalysisRunAsync(
        Guid revisionId,
        byte[] expectedContentHash,
        byte[] logicalEffectiveHash,
        byte[] analysisContextHash,
        byte[] evidenceContextHash,
        byte[] topologyProjectionHash,
        byte[] impactSetHash,
        IReadOnlyList<byte[]> perDeviceAnalysisHashes,
        byte[] dependencyFingerprint,
        string riskLevel,
        bool evidenceSignalsPresent,
        string analyzerVersion,
        string policySchemaVersion,
        string pipelineVersion,
        IReadOnlyList<PolicyAnalysisFinding>? findings = null,
        IReadOnlyList<PolicyAnalysisTestResult>? testResults = null,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        RecordAnalysisRunRequest request = new()
        {
            IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
            RevisionId = DesktopProtoUuid.FromGuid(revisionId),
            ExpectedContentHash = ToSha256(expectedContentHash),
            LogicalEffectiveHash = ToSha256(logicalEffectiveHash),
            AnalysisContextHash = ToSha256(analysisContextHash),
            EvidenceContextHash = ToSha256(evidenceContextHash),
            TopologyProjectionHash = ToSha256(topologyProjectionHash),
            ImpactSetHash = ToSha256(impactSetHash),
            DependencyFingerprint = ToSha256(dependencyFingerprint),
            RiskLevel = riskLevel,
            EvidenceSignalsPresent = evidenceSignalsPresent,
            AnalyzerVersion = analyzerVersion,
            PolicySchemaVersion = policySchemaVersion,
            PipelineVersion = pipelineVersion,
        };
        request.PerDeviceAnalysisHashes.AddRange(perDeviceAnalysisHashes.Select(ToSha256));
        if (findings is not null)
        {
            request.Findings.AddRange(findings);
        }

        if (testResults is not null)
        {
            request.TestResults.AddRange(testResults);
        }

        return await client.RecordAnalysisRunAsync(request, ActorHeaders(), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyApprovalVote> ApproveRevisionAsync(
        Guid revisionId,
        Guid analysisRunId,
        byte[] expectedContentHash,
        byte[] expectedBundleHash,
        byte[] currentDependencyFingerprint,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.ApproveRevisionAsync(
                new ApproveRevisionRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = DesktopProtoUuid.FromGuid(revisionId),
                    AnalysisRunId = DesktopProtoUuid.FromGuid(analysisRunId),
                    ExpectedContentHash = ToSha256(expectedContentHash),
                    ExpectedBundleHash = ToSha256(expectedBundleHash),
                    CurrentDependencyFingerprint = ToSha256(currentDependencyFingerprint),
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyBinding> ActivateDesiredBindingAsync(
        Guid revisionId,
        Guid analysisRunId,
        byte[] expectedContentHash,
        byte[] currentDependencyFingerprint,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.ActivateDesiredBindingAsync(
                new ActivateDesiredBindingRequest
                {
                    IdempotencyKey = DesktopProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = DesktopProtoUuid.FromGuid(revisionId),
                    AnalysisRunId = DesktopProtoUuid.FromGuid(analysisRunId),
                    ExpectedContentHash = ToSha256(expectedContentHash),
                    CurrentDependencyFingerprint = ToSha256(currentDependencyFingerprint),
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private PolicyService.PolicyServiceClient CreateClient()
    {
        GrpcChannel channel = _connection.Channel
            ?? throw new InvalidOperationException("Controller is not connected.");
        return new PolicyService.PolicyServiceClient(channel);
    }

    private Metadata ActorHeaders()
        => new() { { "x-mfc-actor", string.IsNullOrWhiteSpace(_options.Actor) ? "desktop" : _options.Actor } };

    private static Sha256 ToSha256(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 32)
        {
            throw new ArgumentException("Hash must be exactly 32 bytes.", nameof(value));
        }

        return new Sha256 { Value = ByteString.CopyFrom(value) };
    }
}
