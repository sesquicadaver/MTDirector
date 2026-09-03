using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;
using PolicyContainer = Mfc.Domain.Policy.Policy;

namespace Mfc.UnitTests.Application;

public sealed class UpdateExceptionMetadataUseCaseTests
{
    private static readonly DateTimeOffset From = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Until = new(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid Addr = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task A9CreateDraftExceptionWithoutOwnerIdIsValidation()
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyStore policies = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<PolicyDraftView> result = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = "orphan-exception",
            Kind = PolicyKind.Exception,
            OwnerScope = PolicyOwnerScope.Site,
            OwnerId = null,
            ParentContextHash = new byte[32],
        });
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
        Assert.Contains("company-wide", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AMetaCasDraftOnlyAndRewritesParentWhenTargetFound()
    {
        (UpdateExceptionMetadataUseCase useCase, FakePolicyStore policies, Node node, PolicyRevision draft, PolicyRule deny, PolicyRevision companyRev) =
            await SeedDraftExceptionAsync();
        ExceptionMetadataInput input = MetaInput(node.SiteId.Value, deny.Id.Value);
        ApplicationResult<PolicyRevisionView> updated = await useCase.ExecuteAsync(new UpdateExceptionMetadataCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Id.Value,
            ExpectedContentHash = draft.ContentHash.Bytes.ToArray(),
            Metadata = input,
        });
        Assert.True(updated.IsSuccess);
        Assert.NotNull(updated.Value!.ExceptionMetadata);
        Assert.Equal(deny.Id.Value, updated.Value.ExceptionMetadata!.WaivedRuleId);
        Assert.Equal("TICKET-1", updated.Value.ExceptionMetadata.TicketReference);
        Hash256 waived = PolicyHashing.HashContent(PolicyCanonicalWriter.WriteRuleBytes(deny));
        Hash256 expectedParent = PolicyHashing.ComputeParentContextHash(
            PolicyKind.Exception,
            companyRev.ContentHash,
            null,
            null,
            waived)!;
        Assert.Equal(expectedParent.ToString(), updated.Value.ParentContextHashHex);

        ApplicationResult<PolicyRevisionView> stale = await useCase.ExecuteAsync(new UpdateExceptionMetadataCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Id.Value,
            ExpectedContentHash = draft.ContentHash.Bytes.ToArray(),
            Metadata = input,
        });
        Assert.True(stale.IsFailure);
        Assert.Equal("conflict", stale.Error!.Code);

        PolicyRevision loaded = (await policies.GetRevisionAsync(draft.Id))!;
        loaded.MarkValidated();
        loaded.SubmitForReview();
        loaded.Approve(DateTimeOffset.UtcNow);
        await policies.SaveRevisionAsync(loaded);

        ApplicationResult<PolicyRevisionView> approved = await useCase.ExecuteAsync(new UpdateExceptionMetadataCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Id.Value,
            ExpectedContentHash = Convert.FromHexString(updated.Value.ContentHashHex),
            Metadata = input,
        });
        Assert.True(approved.IsFailure);
        Assert.Equal("validation", approved.Error!.Code);
    }

    private static async Task<(
        UpdateExceptionMetadataUseCase UseCase,
        FakePolicyStore Policies,
        Node Node,
        PolicyRevision Draft,
        PolicyRule Deny,
        PolicyRevision CompanyRev)> SeedDraftExceptionAsync()
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyStore policies = new();
        FakeNodeStore nodes = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        Node node = Node.Create(SiteId.New(), NonEmptyName.Create("edge"), NodeKind.Router, DeclaredUplinkMode.One);
        await nodes.AddAsync(node);

        PolicyContainer company = PolicyContainer.Create(
            NonEmptyName.Create("baseline"),
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            null);
        await policies.AddPolicyAsync(company);
        PolicyRule deny = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            0,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(Addr)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);
        PolicyDocument companyDoc = new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects: [System.Text.Json.JsonDocument.Parse("{\"id\":\"" + Addr + "\"}").RootElement.Clone()],
            rules: [deny]);
        PolicyRevision companyRev = PolicyRevision.CreateDraft(
            company, 1, companyDoc, null, UserId.New(), DateTimeOffset.UtcNow);
        companyRev.MarkValidated();
        companyRev.SubmitForReview();
        companyRev.Approve(DateTimeOffset.UtcNow);
        await policies.AddRevisionAsync(companyRev);

        PolicyContainer exception = PolicyContainer.Create(
            NonEmptyName.Create("ex"),
            PolicyKind.Exception,
            PolicyOwnerScope.Site,
            node.SiteId.Value);
        await policies.AddPolicyAsync(exception);
        PolicyRevision draft = PolicyRevision.CreateDraft(
            exception,
            1,
            PolicyDocument.CreateEmpty(exception.Kind, exception.OwnerScope),
            Hash256.Create(new byte[32]),
            UserId.New(),
            DateTimeOffset.UtcNow);
        await policies.AddRevisionAsync(draft);

        UpdateExceptionMetadataUseCase useCase = new(auth, policies, nodes, idempotency, audit, new FakeUnitOfWork());
        return (useCase, policies, node, draft, deny, companyRev);
    }

    private static ExceptionMetadataInput MetaInput(Guid siteId, Guid waivedRuleId)
        => new()
        {
            TargetScope = PolicyOwnerScope.Site,
            TargetScopeId = siteId,
            TargetStage = PolicyPipelineStage.CompanyDeny,
            WaivedRuleId = waivedRuleId,
            ValidFrom = From,
            ValidUntil = Until,
            Reason = "change window",
            TicketReference = "TICKET-1",
        };
}
