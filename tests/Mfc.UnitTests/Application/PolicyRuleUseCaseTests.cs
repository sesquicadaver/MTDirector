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

    [Fact]
    public async Task GetRevisionAndGetRuleRoundTrip()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);
        ApplicationResult<PolicyRuleMutationView> added = await add.ExecuteAsync(CreateAcceptRule(
            draft.Value.RevisionId, hash));
        Assert.True(added.IsSuccess);

        GetPolicyRevisionUseCase getRevision = new(auth, policies);
        ApplicationResult<PolicyRevisionView> revision = await getRevision.ExecuteAsync(new GetPolicyRevisionQuery
        {
            Actor = "admin",
            RevisionId = draft.Value.RevisionId,
        });
        Assert.True(revision.IsSuccess);
        Assert.Single(revision.Value!.Rules);
        Assert.Equal(added.Value!.Rule!.Id, revision.Value.Rules[0].Id);

        GetRuleUseCase getRule = new(auth, policies);
        ApplicationResult<PolicyRuleView> rule = await getRule.ExecuteAsync(new GetRuleQuery
        {
            Actor = "admin",
            RevisionId = draft.Value.RevisionId,
            RuleId = added.Value.Rule.Id,
        });
        Assert.True(rule.IsSuccess);
        Assert.Equal(added.Value.Rule.Id, rule.Value!.Id);

        ApplicationResult<PolicyRuleView> missing = await getRule.ExecuteAsync(new GetRuleQuery
        {
            Actor = "admin",
            RevisionId = draft.Value.RevisionId,
            RuleId = Guid.NewGuid(),
        });
        Assert.True(missing.IsFailure);
        Assert.Equal("not_found", missing.Error!.Code);

        ApplicationResult<PolicyRevisionView> missingRevision = await getRevision.ExecuteAsync(
            new GetPolicyRevisionQuery { Actor = "admin", RevisionId = Guid.NewGuid() });
        Assert.True(missingRevision.IsFailure);
        Assert.Equal("not_found", missingRevision.Error!.Code);
    }

    [Fact]
    public async Task UpdateDeleteReorderAndIdempotentReplay()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        Guid revisionId = draft.Value!.RevisionId;
        byte[] hash = Convert.FromHexString(draft.Value.ContentHashHex);
        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);

        ApplicationResult<PolicyRuleMutationView> first = await add.ExecuteAsync(CreateAcceptRule(revisionId, hash));
        Assert.True(first.IsSuccess);
        hash = Convert.FromHexString(first.Value!.ContentHashHex);
        Guid ruleA = first.Value.Rule!.Id;

        ApplicationResult<PolicyRuleMutationView> second = await add.ExecuteAsync(CreateAcceptRule(revisionId, hash));
        Assert.True(second.IsSuccess);
        hash = Convert.FromHexString(second.Value!.ContentHashHex);
        Guid ruleB = second.Value.Rule!.Id;

        Guid updateKey = Guid.NewGuid();
        UpdateRuleUseCase update = new(auth, policies, zones, idempotency, audit);
        UpdateRuleCommand updateCommand = new()
        {
            Actor = "admin",
            IdempotencyKey = updateKey,
            RevisionId = revisionId,
            RuleId = ruleA,
            ExpectedContentHash = hash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Enabled = false,
            Description = "updated",
            Logging = new LogSpecificationInput { Enabled = true, Prefix = "mfc" },
            Predicate = new TrafficPredicateInput
            {
                ConnectionStates = [ConnectionState.New],
            },
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
        };
        ApplicationResult<PolicyRuleMutationView> updated = await update.ExecuteAsync(updateCommand);
        Assert.True(updated.IsSuccess);
        Assert.False(updated.Value!.Rule!.Enabled);
        Assert.Equal("updated", updated.Value.Rule.Description);
        hash = Convert.FromHexString(updated.Value.ContentHashHex);

        ApplicationResult<PolicyRuleMutationView> replayUpdate = await update.ExecuteAsync(updateCommand);
        Assert.True(replayUpdate.IsSuccess);
        Assert.Equal(updated.Value.Rule.Id, replayUpdate.Value!.Rule!.Id);

        ReorderRulesUseCase reorder = new(auth, policies, zones, idempotency, audit);
        ApplicationResult<PolicyRuleMutationView> reordered = await reorder.ExecuteAsync(new ReorderRulesCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = revisionId,
            ExpectedContentHash = hash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            OrderedRuleIds = [ruleB, ruleA],
        });
        Assert.True(reordered.IsSuccess);
        Assert.Equal(0u, reordered.Value!.Rules.Single(r => r.Id == ruleB).Ordinal);
        Assert.Equal(1u, reordered.Value.Rules.Single(r => r.Id == ruleA).Ordinal);
        hash = Convert.FromHexString(reordered.Value.ContentHashHex);

        DeleteRuleUseCase delete = new(auth, policies, zones, idempotency, audit);
        Guid deleteKey = Guid.NewGuid();
        DeleteRuleCommand deleteCommand = new()
        {
            Actor = "admin",
            IdempotencyKey = deleteKey,
            RevisionId = revisionId,
            RuleId = ruleA,
            ExpectedContentHash = hash,
        };
        ApplicationResult<PolicyRuleMutationView> deleted = await delete.ExecuteAsync(deleteCommand);
        Assert.True(deleted.IsSuccess);
        Assert.Single(deleted.Value!.Rules);
        Assert.Equal(ruleB, deleted.Value.Rules[0].Id);
        hash = Convert.FromHexString(deleted.Value.ContentHashHex);

        ApplicationResult<PolicyRuleMutationView> replayDelete = await delete.ExecuteAsync(deleteCommand);
        Assert.True(replayDelete.IsSuccess);
        Assert.Single(replayDelete.Value!.Rules);

        ApplicationResult<PolicyRuleMutationView> missingDelete = await delete.ExecuteAsync(new DeleteRuleCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = revisionId,
            RuleId = Guid.NewGuid(),
            ExpectedContentHash = hash,
        });
        Assert.True(missingDelete.IsFailure);

        auth.DeniedPermissions.Add(ApplicationPermissions.PolicyWrite);
        ApplicationResult<PolicyRuleMutationView> denied = await add.ExecuteAsync(CreateAcceptRule(revisionId, hash));
        Assert.True(denied.IsFailure);
        Assert.Equal("forbidden", denied.Error!.Code);
    }

    [Fact]
    public async Task InvalidIdempotencyKeyAndUnknownRevisionFail()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);
        ApplicationResult<PolicyRuleMutationView> badKey = await add.ExecuteAsync(new AddRuleCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.Empty,
            RevisionId = Guid.NewGuid(),
            ExpectedContentHash = new byte[32],
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
        });
        Assert.True(badKey.IsFailure);

        ApplicationResult<PolicyRuleMutationView> missing = await add.ExecuteAsync(CreateAcceptRule(
            Guid.NewGuid(), new byte[32]));
        Assert.True(missing.IsFailure);
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task DraftReplayCatalogBranchesAndBadHashLength()
    {
        (FakeAuthorizationBoundary auth,
            FakePolicyStore policies,
            FakeZoneDefinitionStore zones,
            FakeIdempotencyStore idempotency,
            FakeAuditEventWriter audit) = CreateDeps();

        Guid draftKey = Guid.NewGuid();
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit);
        CreateDraftPolicyCommand draftCommand = new()
        {
            Actor = "admin",
            IdempotencyKey = draftKey,
            Name = "baseline",
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
        };
        ApplicationResult<PolicyDraftView> draft = await create.ExecuteAsync(draftCommand);
        Assert.True(draft.IsSuccess);
        ApplicationResult<PolicyDraftView> replayDraft = await create.ExecuteAsync(draftCommand);
        Assert.True(replayDraft.IsSuccess);
        Assert.Equal(draft.Value!.PolicyId, replayDraft.Value!.PolicyId);

        ZoneDefinition zone = ZoneDefinition.Create(
            PolicyOwnerScope.Company,
            null,
            NonEmptyName.Create("wan"),
            NonEmptyName.Create("WAN"));
        await zones.AddAsync(zone);

        Guid addressInclude = Guid.NewGuid();
        Guid addressExclude = Guid.NewGuid();
        Guid destInclude = Guid.NewGuid();
        Guid destExclude = Guid.NewGuid();
        Guid serviceId = Guid.NewGuid();
        PolicyRevision? revision = await policies.GetRevisionAsync(new PolicyRevisionId(draft.Value.RevisionId));
        Assert.NotNull(revision);
        using JsonDocument addressA = JsonDocument.Parse($$"""{"id":"{{addressInclude:D}}"}""");
        using JsonDocument addressB = JsonDocument.Parse($$"""{"id":"{{addressExclude:D}}"}""");
        using JsonDocument addressC = JsonDocument.Parse($$"""{"id":"{{destInclude:D}}"}""");
        using JsonDocument addressD = JsonDocument.Parse($$"""{"id":"{{destExclude:D}}"}""");
        using JsonDocument service = JsonDocument.Parse($$"""{"id":"{{serviceId:D}}"}""");
        using JsonDocument skipped = JsonDocument.Parse("\"not-object\"");
        using JsonDocument badId = JsonDocument.Parse("""{"id":123}""");
        PolicyDocument empty = PolicyDocument.CreateEmpty(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company);
        PolicyDocument seeded = new(
            empty.Kind,
            empty.OwnerScope,
            empty.SchemaVersion,
            empty.ChainContracts,
            empty.ZoneDefinitions,
            [addressA.RootElement.Clone(), addressB.RootElement.Clone(), addressC.RootElement.Clone(),
             addressD.RootElement.Clone(), skipped.RootElement.Clone(), badId.RootElement.Clone()],
            [service.RootElement.Clone()],
            empty.Rules,
            empty.Tests,
            empty.ExceptionMetadata);
        revision!.ReplaceDocument(seeded, null);
        await policies.SaveRevisionAsync(revision);
        byte[] hash = revision.ContentHash.Bytes.ToArray();

        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);
        Guid addKey = Guid.NewGuid();
        AddRuleCommand addCommand = new()
        {
            Actor = "admin",
            IdempotencyKey = addKey,
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
            Predicate = new TrafficPredicateInput
            {
                SourceAddresses = new AddressSelectorInput
                {
                    Include = [addressInclude],
                    Exclude = [addressExclude],
                },
                DestinationAddresses = new AddressSelectorInput
                {
                    Include = [destInclude],
                    Exclude = [destExclude],
                },
                IngressZones = new ZoneSelectorInput { Include = [zone.Id.Value], Exclude = [zone.Id.Value] },
                EgressZones = new ZoneSelectorInput { Include = [zone.Id.Value], Exclude = [zone.Id.Value] },
                Services = new ServiceSelectorInput { Include = [serviceId] },
                TcpFlags = new TcpFlagConstraintInput
                {
                    RequiredPresent = [TcpHeaderBit.Syn],
                    RequiredAbsent = [TcpHeaderBit.Ack],
                },
                IpsecPolicy = new IpsecPolicyPredicateInput
                {
                    Direction = IpsecDirection.Out,
                    Policy = IpsecPolicyKind.None,
                },
            },
        };
        ApplicationResult<PolicyRuleMutationView> added = await add.ExecuteAsync(addCommand);
        Assert.True(added.IsSuccess);
        ApplicationResult<PolicyRuleMutationView> replayAdd = await add.ExecuteAsync(addCommand);
        Assert.True(replayAdd.IsSuccess);
        hash = Convert.FromHexString(added.Value!.ContentHashHex);

        ReorderRulesUseCase reorder = new(auth, policies, zones, idempotency, audit);
        Guid reorderKey = Guid.NewGuid();
        ReorderRulesCommand reorderCommand = new()
        {
            Actor = "admin",
            IdempotencyKey = reorderKey,
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            OrderedRuleIds = [added.Value.Rule!.Id],
        };
        ApplicationResult<PolicyRuleMutationView> reordered = await reorder.ExecuteAsync(reorderCommand);
        Assert.True(reordered.IsSuccess);
        ApplicationResult<PolicyRuleMutationView> replayReorder = await reorder.ExecuteAsync(reorderCommand);
        Assert.True(replayReorder.IsSuccess);

        ApplicationResult<PolicyRuleMutationView> shortHash = await add.ExecuteAsync(CreateAcceptRule(
            draft.Value.RevisionId, [1, 2, 3]));
        Assert.True(shortHash.IsFailure);
        Assert.Equal("validation", shortHash.Error!.Code);

        ApplicationResult<PolicyRuleMutationView> missingService = await add.ExecuteAsync(new AddRuleCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = Convert.FromHexString(reordered.Value!.ContentHashHex),
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
            Predicate = new TrafficPredicateInput
            {
                Services = new ServiceSelectorInput { Include = [Guid.NewGuid()] },
            },
        });
        Assert.True(missingService.IsFailure);
        Assert.Equal("validation", missingService.Error!.Code);
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
