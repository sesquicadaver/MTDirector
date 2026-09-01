using Google.Protobuf;
using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller;
using Mfc.Controller.Grpc;
using Mfc.Domain.Policy.Primitives;
using Mfc.Infrastructure.Persistence;
using Mfc.IntegrationTests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DomainPolicy = Mfc.Domain.Policy;

namespace Mfc.IntegrationTests.Controller;

[Collection(PostgresSharedFixtureDefinition.Name)]
public sealed class PolicyGrpcHostTests
{
    private readonly PostgresFixture _postgres;

    public PolicyGrpcHostTests(PostgresFixture postgres)
    {
        _postgres = postgres;
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    [Fact]
    public async Task C2CreateDraftAddListUpdateDeleteRulesWithContentHashCas()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient client = new(channel);
            Metadata headers = ActorHeaders("tester");

            PolicyDraft draft = await client.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            Assert.NotNull(draft.RevisionId);
            Assert.Equal(32, draft.ContentHash.Value.Length);

            PolicyRuleMutation added = await client.AddRuleAsync(
                new AddRuleRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    ExpectedContentHash = draft.ContentHash,
                    Family = IpAddressFamily.Ipv4,
                    Chain = PolicyFilterChain.Forward,
                    Stage = PolicyPipelineStage.CompanyAllow,
                    Enabled = true,
                    Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
                    Description = "allow",
                },
                headers,
                deadline: Deadline());
            Assert.NotNull(added.Rule);
            Assert.Equal(0u, added.Rule.Ordinal);
            Assert.Equal(32, added.ContentHash.Value.Length);

            ListRulesResponse listed = await client.ListRulesAsync(
                new ListRulesRequest { RevisionId = draft.RevisionId },
                headers,
                deadline: Deadline());
            Assert.Single(listed.Rules);
            Assert.Equal(added.ContentHash, listed.ContentHash);

            RpcException cas = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await client.AddRuleAsync(
                    new AddRuleRequest
                    {
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                        RevisionId = draft.RevisionId,
                        ExpectedContentHash = draft.ContentHash,
                        Family = IpAddressFamily.Ipv4,
                        Chain = PolicyFilterChain.Forward,
                        Stage = PolicyPipelineStage.CompanyAllow,
                        Enabled = true,
                        Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
                    },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.Aborted, cas.StatusCode);
            Assert.Contains("content_hash", cas.Status.Detail, StringComparison.Ordinal);

            PolicyRuleMutation updated = await client.UpdateRuleAsync(
                new UpdateRuleRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    RuleId = added.Rule.Id,
                    ExpectedContentHash = added.ContentHash,
                    Family = IpAddressFamily.Ipv4,
                    Chain = PolicyFilterChain.Forward,
                    Stage = PolicyPipelineStage.CompanyAllow,
                    Enabled = false,
                    Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
                    Description = "disabled",
                },
                headers,
                deadline: Deadline());
            Assert.False(updated.Rule!.Enabled);

            PolicyRuleMutation deleted = await client.DeleteRuleAsync(
                new DeleteRuleRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    RuleId = added.Rule.Id,
                    ExpectedContentHash = updated.ContentHash,
                },
                headers,
                deadline: Deadline());
            Assert.Empty(deleted.Rules);

            global::Mfc.Contracts.Mfc.V1.PolicyRevision revision = await client.GetPolicyRevisionAsync(
                new GetPolicyRevisionRequest { RevisionId = draft.RevisionId },
                headers,
                deadline: Deadline());
            Assert.Empty(revision.Rules);
            Assert.Equal(PolicyRevisionState.Draft, revision.State);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task ListPoliciesReturnsCreatedDraftsWithLatestRevision()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient client = new(channel);
            Metadata headers = ActorHeaders("tester");

            PolicyDraft first = await client.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "alpha-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            PolicyDraft second = await client.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "beta-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());

            ListPoliciesResponse listed = await client.ListPoliciesAsync(
                new ListPoliciesRequest(),
                headers,
                deadline: Deadline());
            Assert.Equal(2, listed.Policies.Count);
            Assert.Equal("alpha-baseline", listed.Policies[0].Name);
            Assert.Equal(first.PolicyId, listed.Policies[0].PolicyId);
            Assert.Equal(first.RevisionId, listed.Policies[0].LatestRevisionId);
            Assert.Equal(1u, listed.Policies[0].LatestRevisionNumber);
            Assert.Equal(PolicyRevisionState.Draft, listed.Policies[0].LatestRevisionState);
            Assert.Equal(first.ContentHash, listed.Policies[0].ContentHash);
            Assert.Equal("beta-baseline", listed.Policies[1].Name);
            Assert.Equal(second.RevisionId, listed.Policies[1].LatestRevisionId);

            ListPoliciesResponse filtered = await client.ListPoliciesAsync(
                new ListPoliciesRequest { Kind = PolicyKind.SiteOverlay },
                headers,
                deadline: Deadline());
            Assert.Empty(filtered.Policies);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task GetDevicePolicySafetyAnalysisUnknownDeviceReturnsNotFound()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient client = new(channel);
            Metadata headers = ActorHeaders("tester");

            RpcException missing = await Assert.ThrowsAsync<RpcException>(async () =>
                await client.GetDevicePolicySafetyAnalysisAsync(
                    new GetDevicePolicySafetyAnalysisRequest
                    {
                        DeviceId = ProtoUuid.FromGuid(Guid.NewGuid()),
                        ControllerSourcePrefixes = { "192.0.2.0/24" },
                    },
                    headers,
                    deadline: Deadline()));
            Assert.Equal(StatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task C2ComposeEffectivePolicyReturnsRulesRefsAndHash()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient policy = new(channel);
            InventoryService.InventoryServiceClient inventory = new(channel);
            Metadata headers = ActorHeaders("tester");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = "C2",
                    Name = "Compose C2",
                },
                headers,
                deadline: Deadline());
            Node node = await inventory.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = "edge",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                },
                headers,
                deadline: Deadline());

            PolicyDraft draft = await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            PolicyRuleMutation added = await policy.AddRuleAsync(
                new AddRuleRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    ExpectedContentHash = draft.ContentHash,
                    Family = IpAddressFamily.Ipv4,
                    Chain = PolicyFilterChain.Forward,
                    Stage = PolicyPipelineStage.CompanyAllow,
                    Enabled = true,
                    Effect = new RuleEffect { Kind = PolicyRuleEffect.Accept },
                    Description = "allow",
                },
                headers,
                deadline: Deadline());
            await ApproveRevisionAsync(app, ProtoUuid.ToGuid(draft.RevisionId));

            EffectivePolicy composed = await policy.ComposeEffectivePolicyAsync(
                new ComposeEffectivePolicyRequest { NodeId = node.Id },
                headers,
                deadline: Deadline());
            Assert.Equal(node.Id, composed.NodeId);
            Assert.Equal(32, composed.LogicalEffectiveHash.Value.Length);
            Assert.NotNull(composed.Company);
            Assert.Equal(draft.PolicyId, composed.Company.PolicyId);
            Assert.Equal(draft.RevisionId, composed.Company.RevisionId);
            Assert.Equal(1u, composed.Company.RevisionNumber);
            Assert.Equal(32, composed.Company.ContentHash.Value.Length);
            Assert.Null(composed.Site);
            Assert.Null(composed.Node);
            Assert.Single(composed.Rules);
            Assert.Equal(added.Rule!.Id, composed.Rules[0].Id);
            Assert.Equal("allow", composed.Rules[0].Description);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task C2ComposeEffectivePolicyIncludesExemptionAndHashDiffers()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient policy = new(channel);
            InventoryService.InventoryServiceClient inventory = new(channel);
            Metadata headers = ActorHeaders("tester");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = "C2E",
                    Name = "Compose C2 exception",
                },
                headers,
                deadline: Deadline());
            Node node = await inventory.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = "edge",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                },
                headers,
                deadline: Deadline());

            PolicyDraft companyDraft = await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            Guid addr = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            Guid denyId = await ReplaceCompanyWithDenyAsync(app, ProtoUuid.ToGuid(companyDraft.RevisionId), addr);
            await ApproveRevisionAsync(app, ProtoUuid.ToGuid(companyDraft.RevisionId));

            PolicyDraft exDraft = await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "site-exception",
                    Kind = PolicyKind.Exception,
                    OwnerScope = PolicyOwnerScope.Site,
                    OwnerId = site.Id,
                    ParentContextHash = new Sha256 { Value = ByteString.CopyFrom(new byte[32]) },
                },
                headers,
                deadline: Deadline());
            global::Mfc.Contracts.Mfc.V1.PolicyRevision withMeta = await policy.UpdateExceptionMetadataAsync(
                new UpdateExceptionMetadataRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = exDraft.RevisionId,
                    ExpectedContentHash = exDraft.ContentHash,
                    Metadata = new ExceptionMetadata
                    {
                        TargetScope = PolicyOwnerScope.Site,
                        TargetScopeId = site.Id,
                        TargetStage = PolicyPipelineStage.CompanyDeny,
                        WaivedRuleId = ProtoUuid.FromGuid(denyId),
                        ValidFrom = DomainPolicy.ExceptionMetadata.FormatTimestamp(
                            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                        ValidUntil = DomainPolicy.ExceptionMetadata.FormatTimestamp(
                            new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                        Reason = "change window",
                        TicketReference = "TICKET-C2",
                    },
                },
                headers,
                deadline: Deadline());
            PolicyRuleMutation exempt = await policy.AddRuleAsync(
                new AddRuleRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = exDraft.RevisionId,
                    ExpectedContentHash = withMeta.ContentHash,
                    Family = IpAddressFamily.Ipv4,
                    Chain = PolicyFilterChain.Forward,
                    Stage = PolicyPipelineStage.CompanyDenyExemptions,
                    Enabled = true,
                    Effect = new RuleEffect { Kind = PolicyRuleEffect.ExemptDenyStage },
                    Predicate = new TrafficPredicate
                    {
                        SourceAddresses = new AddressSelector
                        {
                            Include = { ProtoUuid.FromGuid(addr) },
                        },
                    },
                },
                headers,
                deadline: Deadline());
            await ApproveRevisionAsync(app, ProtoUuid.ToGuid(exDraft.RevisionId));

            EffectivePolicy withException = await policy.ComposeEffectivePolicyAsync(
                new ComposeEffectivePolicyRequest { NodeId = node.Id },
                headers,
                deadline: Deadline());
            Assert.Equal(2, withException.Rules.Count);
            Assert.Equal(PolicyPipelineStage.CompanyDenyExemptions, withException.Rules[0].Stage);
            Assert.Equal(exempt.Rule!.Id, withException.Rules[0].Id);
            Assert.Equal(denyId, ProtoUuid.ToGuid(withException.Rules[1].Id));

            await ArchivePolicyAsync(app, ProtoUuid.ToGuid(exDraft.PolicyId));
            EffectivePolicy withoutException = await policy.ComposeEffectivePolicyAsync(
                new ComposeEffectivePolicyRequest { NodeId = node.Id },
                headers,
                deadline: Deadline());
            Assert.Single(withoutException.Rules);
            Assert.NotEqual(withException.LogicalEffectiveHash, withoutException.LogicalEffectiveHash);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task CMetaUpdateExceptionMetadataCasDraftOnlyAndGetRevision()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient policy = new(channel);
            InventoryService.InventoryServiceClient inventory = new(channel);
            Metadata headers = ActorHeaders("tester");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = "CM",
                    Name = "C_META",
                },
                headers,
                deadline: Deadline());
            PolicyDraft exDraft = await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "site-exception",
                    Kind = PolicyKind.Exception,
                    OwnerScope = PolicyOwnerScope.Site,
                    OwnerId = site.Id,
                    ParentContextHash = new Sha256 { Value = ByteString.CopyFrom(new byte[32]) },
                },
                headers,
                deadline: Deadline());
            ExceptionMetadata metadata = new()
            {
                TargetScope = PolicyOwnerScope.Site,
                TargetScopeId = site.Id,
                TargetStage = PolicyPipelineStage.CompanyDeny,
                WaivedRuleId = ProtoUuid.FromGuid(Guid.NewGuid()),
                ValidFrom = DomainPolicy.ExceptionMetadata.FormatTimestamp(
                    new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                ValidUntil = DomainPolicy.ExceptionMetadata.FormatTimestamp(
                    new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)),
                Reason = "change window",
                TicketReference = "TICKET-META",
            };
            global::Mfc.Contracts.Mfc.V1.PolicyRevision updated = await policy.UpdateExceptionMetadataAsync(
                new UpdateExceptionMetadataRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = exDraft.RevisionId,
                    ExpectedContentHash = exDraft.ContentHash,
                    Metadata = metadata,
                },
                headers,
                deadline: Deadline());
            Assert.NotNull(updated.ExceptionMetadata);
            Assert.Equal("TICKET-META", updated.ExceptionMetadata.TicketReference);

            global::Mfc.Contracts.Mfc.V1.PolicyRevision fetched = await policy.GetPolicyRevisionAsync(
                new GetPolicyRevisionRequest { RevisionId = exDraft.RevisionId },
                headers,
                deadline: Deadline());
            Assert.NotNull(fetched.ExceptionMetadata);
            Assert.Equal(updated.ExceptionMetadata.TicketReference, fetched.ExceptionMetadata.TicketReference);

            RpcException cas = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await policy.UpdateExceptionMetadataAsync(
                    new UpdateExceptionMetadataRequest
                    {
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                        RevisionId = exDraft.RevisionId,
                        ExpectedContentHash = exDraft.ContentHash,
                        Metadata = metadata,
                    },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.Aborted, cas.StatusCode);

            await ApproveRevisionAsync(app, ProtoUuid.ToGuid(exDraft.RevisionId));
            RpcException draftOnly = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await policy.UpdateExceptionMetadataAsync(
                    new UpdateExceptionMetadataRequest
                    {
                        IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                        RevisionId = exDraft.RevisionId,
                        ExpectedContentHash = updated.ContentHash,
                        Metadata = metadata,
                    },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.InvalidArgument, draftOnly.StatusCode);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task O1TwoActiveCompaniesDecodeComposeTrailer()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient policy = new(channel);
            InventoryService.InventoryServiceClient inventory = new(channel);
            Metadata headers = ActorHeaders("tester");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = "O1",
                    Name = "Compose O1",
                },
                headers,
                deadline: Deadline());
            Node node = await inventory.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = "edge",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                },
                headers,
                deadline: Deadline());

            await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-a",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-b",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());

            RpcException ex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await policy.ComposeEffectivePolicyAsync(
                    new ComposeEffectivePolicyRequest { NodeId = node.Id },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
            byte[]? trailer = ex.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey);
            Assert.NotNull(trailer);
            ErrorDetail detail = ErrorDetail.Parser.ParseFrom(trailer);
            Assert.StartsWith("POLICY_COMPOSE_", detail.Code, StringComparison.Ordinal);
            Assert.NotEqual("failed", detail.Code, StringComparer.Ordinal);
            Assert.NotEqual("conflict", detail.Code, StringComparer.Ordinal);
            Assert.Equal(DomainPolicy.PolicyComposeCodes.PolicyNotUnique, detail.Code);
            Assert.False(detail.Retryable);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task O1EmptyExceptionMetadataDecodesExceptionTrailer()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient policy = new(channel);
            InventoryService.InventoryServiceClient inventory = new(channel);
            Metadata headers = ActorHeaders("tester");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = "O1E",
                    Name = "Exception O1",
                },
                headers,
                deadline: Deadline());
            Node node = await inventory.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = "edge",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                },
                headers,
                deadline: Deadline());
            PolicyDraft company = await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            await ApproveRevisionAsync(app, ProtoUuid.ToGuid(company.RevisionId));
            PolicyDraft exception = await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "empty-exception",
                    Kind = PolicyKind.Exception,
                    OwnerScope = PolicyOwnerScope.Site,
                    OwnerId = site.Id,
                    ParentContextHash = new Sha256 { Value = ByteString.CopyFrom(new byte[32]) },
                },
                headers,
                deadline: Deadline());
            await ApproveRevisionAsync(app, ProtoUuid.ToGuid(exception.RevisionId));

            RpcException ex = await Assert.ThrowsAsync<RpcException>(async () =>
            {
                await policy.ComposeEffectivePolicyAsync(
                    new ComposeEffectivePolicyRequest { NodeId = node.Id },
                    headers,
                    deadline: Deadline());
            });
            Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
            byte[]? trailer = ex.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey);
            Assert.NotNull(trailer);
            ErrorDetail detail = ErrorDetail.Parser.ParseFrom(trailer);
            Assert.StartsWith("POLICY_EXCEPTION_", detail.Code, StringComparison.Ordinal);
            Assert.NotEqual("failed", detail.Code, StringComparer.Ordinal);
            Assert.NotEqual("conflict", detail.Code, StringComparer.Ordinal);
            Assert.Equal(DomainPolicy.PolicyExceptionCodes.MetadataInvalid, detail.Code);
            Assert.False(detail.Retryable);
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    [Fact]
    public async Task O3UnusedPolicyObjectFindingOnSuccess()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";

        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();

        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient policy = new(channel);
            InventoryService.InventoryServiceClient inventory = new(channel);
            Metadata headers = ActorHeaders("tester");

            Site site = await inventory.CreateSiteAsync(
                new CreateSiteRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Code = "O3",
                    Name = "Compose O3",
                },
                headers,
                deadline: Deadline());
            Node node = await inventory.CreateNodeAsync(
                new CreateNodeRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    SiteId = site.Id,
                    Name = "edge",
                    DeclaredKind = NodeKind.Router,
                    DeclaredUplinkMode = DeclaredUplinkMode.One,
                },
                headers,
                deadline: Deadline());

            PolicyDraft draft = await policy.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            Guid unused = Guid.NewGuid();
            await ReplaceDraftWithUnusedObjectAsync(
                app,
                ProtoUuid.ToGuid(draft.RevisionId),
                unused);
            await ApproveRevisionAsync(app, ProtoUuid.ToGuid(draft.RevisionId));

            EffectivePolicy composed = await policy.ComposeEffectivePolicyAsync(
                new ComposeEffectivePolicyRequest { NodeId = node.Id },
                headers,
                deadline: Deadline());
            Assert.Contains(composed.Findings, f => f.Code == DomainPolicy.PolicyComposeCodes.UnusedPolicyObject);
            Assert.Contains(composed.Findings, f => f.Subject == unused.ToString("D"));
        }
        finally
        {
            using CancellationTokenSource stopCts = new(TimeSpan.FromSeconds(5));
            await app.StopAsync(stopCts.Token);
        }
    }

    private static async Task ArchivePolicyAsync(WebApplication app, Guid policyId)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        DomainPolicy.Policy? policy = await store.GetPolicyAsync(new PolicyId(policyId));
        Assert.NotNull(policy);
        policy!.Archive();
        await store.UpdatePolicyAsync(policy);
    }

    private static async Task<Guid> ReplaceCompanyWithDenyAsync(WebApplication app, Guid revisionId, Guid addressId)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        DomainPolicy.PolicyRevision? revision = await store.GetRevisionAsync(new PolicyRevisionId(revisionId));
        Assert.NotNull(revision);
        DomainPolicy.PolicyRule deny = DomainPolicy.PolicyRule.Create(
            Mfc.Domain.Inventory.IpAddressFamily.IPv4,
            DomainPolicy.PolicyFilterChain.Forward,
            DomainPolicy.PolicyPipelineStage.CompanyDeny,
            ordinal: 0,
            DomainPolicy.TrafficPredicate.Create(
                sourceAddresses: DomainPolicy.AddressSelector.Create(
                    [new Mfc.Domain.Policy.Primitives.AddressObjectId(addressId)])),
            DomainPolicy.RuleEffectSpec.Create(DomainPolicy.PolicyRuleEffect.Drop),
            exceptionEligible: true);
        DomainPolicy.PolicyDocument document = new(
            DomainPolicy.PolicyKind.CompanyBaseline,
            DomainPolicy.PolicyOwnerScope.Company,
            addressObjects:
            [
                System.Text.Json.JsonDocument.Parse(
                    "{\"id\":\"" + addressId +
                    "\",\"name\":\"deny-src\",\"family\":\"IPv4\",\"entries\":[{\"kind\":\"PREFIX\",\"address\":\"10.0.0.0\",\"prefix_length\":24}]}")
                    .RootElement.Clone(),
            ],
            rules: [deny]);
        revision!.ReplaceDocument(document, parentContextHash: null);
        await store.SaveRevisionAsync(revision);
        return deny.Id.Value;
    }

    [Fact]
    public async Task ApprovalAndDesiredBindingAreSeparateAndDoNotDeploy()
    {
        string connectionString = await _postgres.CreateFreshDatabaseAsync();
        string url = $"http://127.0.0.1:{GetFreeTcpPort()}";
        await using var app = Program.BuildHost(DevArgs(url, connectionString));
        await app.Services.MigrateAsync();
        await app.StartAsync();
        try
        {
            await WaitForPortAsync(url, TimeSpan.FromSeconds(10));
            using GrpcChannel channel = GrpcChannel.ForAddress(url);
            PolicyService.PolicyServiceClient client = new(channel);
            Metadata headers = ActorHeaders("tester");
            PolicyDraft draft = await client.CreateDraftPolicyAsync(
                new CreateDraftPolicyRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    Name = "company-baseline",
                    Kind = PolicyKind.CompanyBaseline,
                    OwnerScope = PolicyOwnerScope.Company,
                },
                headers,
                deadline: Deadline());
            await MarkValidatedAsync(app, ProtoUuid.ToGuid(draft.RevisionId));

            Sha256 fingerprint = Utf8Sha256("deps");
            PolicyAnalysisRun run = await client.RecordAnalysisRunAsync(
                new RecordAnalysisRunRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    ExpectedContentHash = draft.ContentHash,
                    LogicalEffectiveHash = Utf8Sha256("logical"),
                    AnalysisContextHash = Utf8Sha256("analysis"),
                    EvidenceContextHash = Utf8Sha256("evidence"),
                    TopologyProjectionHash = Utf8Sha256("topology"),
                    ImpactSetHash = Utf8Sha256("impact"),
                    PerDeviceAnalysisHashes = { Utf8Sha256("device") },
                    DependencyFingerprint = fingerprint,
                    RiskLevel = "LOW",
                    EvidenceSignalsPresent = true,
                    AnalyzerVersion = "mfc.policy-approval.v1",
                    PolicySchemaVersion = "mfc.policy.v1",
                    PipelineVersion = "v1",
                    TestResults =
                    {
                        new PolicyAnalysisTestResult
                        {
                            TestId = ProtoUuid.FromGuid(Guid.NewGuid()),
                            Origin = "SYSTEM",
                            Outcome = "PASS",
                            Proof = "PROVEN",
                        },
                    },
                },
                headers,
                deadline: Deadline());
            Assert.Equal(32, run.BundleHash.Value.Length);

            global::Mfc.Contracts.Mfc.V1.PolicyRevision reviewed = await client.SubmitRevisionForReviewAsync(
                new SubmitRevisionForReviewRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    ExpectedContentHash = draft.ContentHash,
                },
                headers,
                deadline: Deadline());
            Assert.Equal(PolicyRevisionState.InReview, reviewed.State);

            PolicyApprovalVote vote = await client.ApproveRevisionAsync(
                new ApproveRevisionRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    AnalysisRunId = run.Id,
                    ExpectedContentHash = draft.ContentHash,
                    ExpectedBundleHash = run.BundleHash,
                    CurrentDependencyFingerprint = fingerprint,
                },
                headers,
                deadline: Deadline());
            Assert.True(vote.CompletesApproval);
            Assert.Equal(PolicyRevisionState.Approved, vote.RevisionState);
            Assert.Empty(vote.BindingIds);

            global::Mfc.Contracts.Mfc.V1.PolicyBinding bound = await client.ActivateDesiredBindingAsync(
                new ActivateDesiredBindingRequest
                {
                    IdempotencyKey = ProtoUuid.FromGuid(Guid.NewGuid()),
                    RevisionId = draft.RevisionId,
                    AnalysisRunId = run.Id,
                    ExpectedContentHash = draft.ContentHash,
                    CurrentDependencyFingerprint = fingerprint,
                },
                headers,
                deadline: Deadline());
            Assert.False(bound.DeploymentStarted);
            Assert.Equal(PolicyBindingState.Active, bound.State);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    private static async Task MarkValidatedAsync(WebApplication app, Guid revisionId)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        DomainPolicy.PolicyRevision? revision = await store.GetRevisionAsync(new PolicyRevisionId(revisionId));
        Assert.NotNull(revision);
        revision!.MarkValidated();
        await store.SaveRevisionAsync(revision);
    }

    private static Sha256 Utf8Sha256(string value)
        => new()
        {
            Value = ByteString.CopyFrom(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))),
        };

    private static async Task ApproveRevisionAsync(WebApplication app, Guid revisionId)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        DomainPolicy.PolicyRevision? revision = await store.GetRevisionAsync(new PolicyRevisionId(revisionId));
        Assert.NotNull(revision);
        revision!.MarkValidated();
        revision.SubmitForReview();
        revision.Approve(DateTimeOffset.UtcNow);
        await store.SaveRevisionAsync(revision);
    }

    private static async Task ReplaceDraftWithUnusedObjectAsync(WebApplication app, Guid revisionId, Guid unusedObjectId)
    {
        await using AsyncServiceScope scope = app.Services.CreateAsyncScope();
        IPolicyStore store = scope.ServiceProvider.GetRequiredService<IPolicyStore>();
        DomainPolicy.PolicyRevision? revision = await store.GetRevisionAsync(new PolicyRevisionId(revisionId));
        Assert.NotNull(revision);
        DomainPolicy.PolicyDocument document = new(
            DomainPolicy.PolicyKind.CompanyBaseline,
            DomainPolicy.PolicyOwnerScope.Company,
            addressObjects:
            [
                System.Text.Json.JsonDocument.Parse("{\"id\":\"" + unusedObjectId + "\"}").RootElement.Clone(),
            ],
            rules:
            [
                DomainPolicy.PolicyRule.Create(
                    Mfc.Domain.Inventory.IpAddressFamily.IPv4,
                    DomainPolicy.PolicyFilterChain.Forward,
                    DomainPolicy.PolicyPipelineStage.CompanyAllow,
                    ordinal: 0,
                    DomainPolicy.TrafficPredicate.Create(),
                    DomainPolicy.RuleEffectSpec.Create(DomainPolicy.PolicyRuleEffect.Accept)),
            ]);
        revision!.ReplaceDocument(document, parentContextHash: null);
        await store.SaveRevisionAsync(revision);
    }

    private static string[] DevArgs(string url, string connectionString)
        =>
        [
            "--environment", "Development",
            $"--Mfc:Grpc:ListenAddress={url}",
            "--Mfc:Grpc:AllowInsecureLoopback=true",
            "--Mfc:Grpc:ShutdownTimeoutSeconds=5",
            "--Mfc:Security:RequireTls=true",
            "--Mfc:Security:MasterKeyProvider=Development",
            "--Mfc:Authentication:AllowDevelopmentAuthentication=true",
                "--Mfc:OperationalJobs:Enabled=false",
            $"--Mfc:Database:ConnectionString={connectionString}",
        ];

    private static Metadata ActorHeaders(string actor) => new()
    {
        { PolicyGrpcService.ActorMetadataKey, actor },
    };

    private static DateTime Deadline() => DateTime.UtcNow.AddSeconds(30);

    private static int GetFreeTcpPort()
    {
        System.Net.Sockets.TcpListener listener = new(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task WaitForPortAsync(string url, TimeSpan timeout)
    {
        Uri uri = new(url);
        using CancellationTokenSource delay = new(timeout);
        while (!delay.IsCancellationRequested)
        {
            try
            {
                using System.Net.Sockets.TcpClient client = new();
                await client.ConnectAsync(uri.Host, uri.Port, delay.Token);
                return;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                await Task.Delay(50, delay.Token);
            }
        }

        throw new TimeoutException($"Timed out waiting for {url}");
    }
}
