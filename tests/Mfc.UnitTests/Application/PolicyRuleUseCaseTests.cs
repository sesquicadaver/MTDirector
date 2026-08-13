using System.Text.Json;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class PolicyRuleUseCaseTests
{
    [Fact]
    public async Task A1DraftOnlyMutationsRejectApprovedRevision()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        Assert.True(draft.IsSuccess);

        PolicyRevision? revision = await policies.GetRevisionAsync(new PolicyRevisionId(draft.Value!.RevisionId));
        Assert.NotNull(revision);
        revision!.MarkValidated();
        revision.SubmitForReview();
        revision.Approve(DateTimeOffset.UtcNow);
        await policies.SaveRevisionAsync(revision);

        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);
        ApplicationResult<PolicyRuleMutationView> result = await add.ExecuteAsync(new AddRuleCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = Convert.FromHexString(draft.Value.ContentHashHex),
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
        });
        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
        Assert.Contains("DRAFT", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A2OrdinalRepairOnAdd()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);

        ApplicationResult<PolicyRuleMutationView> first = await add.ExecuteAsync(CreateAcceptRule(
            draft.Value.RevisionId, hash, ordinal: 5));
        Assert.True(first.IsSuccess);
        Assert.Equal(0u, first.Value!.Rule!.Ordinal);
        hash = Convert.FromHexString(first.Value.ContentHashHex);

        ApplicationResult<PolicyRuleMutationView> second = await add.ExecuteAsync(CreateAcceptRule(
            draft.Value.RevisionId, hash, ordinal: 99));
        Assert.True(second.IsSuccess);
        Assert.Equal(2, second.Value!.Rules.Count);
        Assert.Equal(new uint[] { 0, 1 }, second.Value.Rules.Select(r => r.Ordinal).OrderBy(o => o).ToArray());
    }

    [Fact]
    public async Task A3ContentHashCasConflict()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] stale = Convert.FromHexString(draft.Value!.ContentHashHex);
        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);
        ApplicationResult<PolicyRuleMutationView> first = await add.ExecuteAsync(CreateAcceptRule(
            draft.Value.RevisionId, stale));
        Assert.True(first.IsSuccess);

        ApplicationResult<PolicyRuleMutationView> conflict = await add.ExecuteAsync(CreateAcceptRule(
            draft.Value.RevisionId, stale));
        Assert.True(conflict.IsFailure);
        Assert.Equal("conflict", conflict.Error!.Code);
        Assert.Contains("content_hash", conflict.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A4ListIncludesDisabledRules()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);
        AddRuleCommand disabled = CreateAcceptRule(draft.Value.RevisionId, hash);
        disabled = new AddRuleCommand
        {
            Actor = disabled.Actor,
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = disabled.RevisionId,
            ExpectedContentHash = disabled.ExpectedContentHash,
            Family = disabled.Family,
            Chain = disabled.Chain,
            Stage = disabled.Stage,
            Enabled = false,
            Effect = disabled.Effect,
        };
        ApplicationResult<PolicyRuleMutationView> added = await add.ExecuteAsync(disabled);
        Assert.True(added.IsSuccess);
        Assert.False(added.Value!.Rule!.Enabled);

        ListRulesUseCase list = new(auth, policies);
        ApplicationResult<PolicyRuleListView> all = await list.ExecuteAsync(new ListRulesQuery
        {
            Actor = "admin",
            RevisionId = draft.Value.RevisionId,
            ActiveOnly = false,
        });
        Assert.True(all.IsSuccess);
        Assert.Single(all.Value!.Rules);
        Assert.False(all.Value.Rules[0].Enabled);

        ApplicationResult<PolicyRuleListView> active = await list.ExecuteAsync(new ListRulesQuery
        {
            Actor = "admin",
            RevisionId = draft.Value.RevisionId,
            ActiveOnly = true,
        });
        Assert.True(active.IsSuccess);
        Assert.Empty(active.Value!.Rules);
    }

    [Fact]
    public async Task A5ZoneHardAndAddressServiceSoftWarning()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);

        Guid missingZone = Guid.NewGuid();
        ApplicationResult<PolicyRuleMutationView> missing = await add.ExecuteAsync(new AddRuleCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
            Predicate = new TrafficPredicateInput
            {
                IngressZones = new ZoneSelectorInput { Include = [missingZone] },
            },
        });
        Assert.True(missing.IsFailure);
        Assert.Equal("not_found", missing.Error!.Code);

        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Company,
            null,
            NonEmptyName.Create("lan"),
            NonEmptyName.Create("LAN"));
        await zones.AddAsync(zone);

        Guid addressId = Guid.NewGuid();
        Guid serviceId = Guid.NewGuid();
        ApplicationResult<PolicyRuleMutationView> soft = await add.ExecuteAsync(new AddRuleCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
            Predicate = new TrafficPredicateInput
            {
                IngressZones = new ZoneSelectorInput { Include = [zone.Id.Value] },
                SourceAddresses = new AddressSelectorInput { Include = [addressId] },
                Services = new ServiceSelectorInput { Include = [serviceId] },
            },
        });
        Assert.True(soft.IsSuccess);
        Assert.Contains(
            soft.Value!.Warnings,
            w => w.Code == "POLICY_SELECTOR_CATALOG_SOFT");
        Assert.Contains(
            soft.Value.Rule!.Warnings,
            w => w.Code == "POLICY_SELECTOR_CATALOG_SOFT");
    }

    [Fact]
    public async Task A5bNonEmptyAddressCatalogRequiresMembership()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        PolicyRevision? revision = await policies.GetRevisionAsync(new PolicyRevisionId(draft.Value!.RevisionId));
        Assert.NotNull(revision);

        Guid knownAddress = Guid.NewGuid();
        using JsonDocument addressDoc = JsonDocument.Parse(
            $$"""{"id":"{{knownAddress:D}}","name":"net"}""");
        PolicyDocument empty = PolicyDocument.CreateEmpty(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company);
        PolicyDocument seeded = new(
            empty.Kind,
            empty.OwnerScope,
            empty.SchemaVersion,
            empty.ChainContracts,
            empty.ZoneDefinitions,
            [addressDoc.RootElement.Clone()],
            empty.ServiceObjects,
            empty.Rules,
            empty.Tests,
            empty.ExceptionMetadata);
        revision!.ReplaceDocument(seeded, null);
        await policies.SaveRevisionAsync(revision);

        byte[] hash = revision.ContentHash.Bytes.ToArray();
        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);

        ApplicationResult<PolicyRuleMutationView> missing = await add.ExecuteAsync(new AddRuleCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
            Predicate = new TrafficPredicateInput
            {
                SourceAddresses = new AddressSelectorInput { Include = [Guid.NewGuid()] },
            },
        });
        Assert.True(missing.IsFailure);
        Assert.Equal("validation", missing.Error!.Code);

        ApplicationResult<PolicyRuleMutationView> ok = await add.ExecuteAsync(new AddRuleCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
            Predicate = new TrafficPredicateInput
            {
                SourceAddresses = new AddressSelectorInput { Include = [knownAddress] },
            },
        });
        Assert.True(ok.IsSuccess);
        Assert.DoesNotContain(
            ok.Value!.Warnings,
            w => w.Code == "POLICY_SELECTOR_CATALOG_SOFT" && w.Subject == "address");
    }

    private static (
        FakeAuthorizationBoundary Auth,
        FakePolicyStore Policies,
        FakeZoneDefinitionStore Zones,
        FakeIdempotencyStore Idempotency,
        FakeAuditEventWriter Audit) CreateDeps()
        => (new FakeAuthorizationBoundary(), new FakePolicyStore(), new FakeZoneDefinitionStore(),
            new FakeIdempotencyStore(), new FakeAuditEventWriter());

    private static async Task<ApplicationResult<PolicyDraftView>> CreateDraftAsync(
        FakeAuthorizationBoundary auth,
        FakePolicyStore policies,
        FakeIdempotencyStore idempotency,
        FakeAuditEventWriter audit)
    {
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit);
        return await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = "baseline",
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
        });
    }

    private static AddRuleCommand CreateAcceptRule(Guid revisionId, byte[] hash, uint ordinal = 0)
        => new()
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = revisionId,
            ExpectedContentHash = hash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Ordinal = ordinal,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
        };
}
