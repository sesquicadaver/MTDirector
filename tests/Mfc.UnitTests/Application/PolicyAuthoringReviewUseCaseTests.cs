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

public sealed class PolicyAuthoringReviewUseCaseTests
{
    [Fact]
    public async Task ValidateRevisionDraftToValidatedWithCasAndIdempotency()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        Assert.True(draft.IsSuccess);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        Guid key = Guid.NewGuid();
        ValidateRevisionUseCase useCase = new(auth, policies, idempotency, audit);
        ValidateRevisionCommand command = new()
        {
            Actor = "admin",
            IdempotencyKey = key,
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
        };
        ApplicationResult<PolicyRevisionView> first = await useCase.ExecuteAsync(command);
        Assert.True(first.IsSuccess);
        Assert.Equal(PolicyRevisionState.Validated, first.Value!.State);

        ApplicationResult<PolicyRevisionView> replay = await useCase.ExecuteAsync(command);
        Assert.True(replay.IsSuccess);
        Assert.Equal(PolicyRevisionState.Validated, replay.Value!.State);

        ApplicationResult<PolicyRevisionView> conflict = await useCase.ExecuteAsync(new ValidateRevisionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = new byte[32],
        });
        Assert.True(conflict.IsFailure);
        Assert.Equal("conflict", conflict.Error!.Code);
        Assert.Contains(audit.Events, e => e.Action == ValidateRevisionUseCase.Operation);
    }

    [Fact]
    public async Task UpsertAddressObjectAddsAndReplacesById()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        UpsertAddressObjectUseCase useCase = new(auth, policies, idempotency, audit);
        ApplicationResult<PolicyRevisionView> created = await useCase.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "corp",
            Family = IpAddressFamily.IPv4,
            Entries =
            [
                new AddressObjectEntryView { Kind = "HOST", Address = "10.0.0.1" },
            ],
        });
        Assert.True(created.IsSuccess);
        Assert.Single(created.Value!.AddressObjects);
        Guid objectId = created.Value.AddressObjects[0].Id;
        hash = Convert.FromHexString(created.Value.ContentHashHex);

        ApplicationResult<PolicyRevisionView> replaced = await useCase.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            ObjectId = objectId,
            Name = "corp2",
            Family = IpAddressFamily.IPv4,
            Entries =
            [
                new AddressObjectEntryView { Kind = "PREFIX", Address = "10.1.0.0", PrefixLength = 24 },
            ],
        });
        Assert.True(replaced.IsSuccess);
        Assert.Single(replaced.Value!.AddressObjects);
        Assert.Equal("corp2", replaced.Value.AddressObjects[0].Name);
        Assert.Equal(objectId, replaced.Value.AddressObjects[0].Id);
    }

    [Fact]
    public async Task UpsertServiceObjectAndReplaceTests()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        UpsertServiceObjectUseCase upsert = new(auth, policies, idempotency, audit);
        ApplicationResult<PolicyRevisionView> service = await upsert.ExecuteAsync(new UpsertServiceObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "http",
            Terms =
            [
                new ServiceTermView
                {
                    Protocol = new IpProtocolView { Any = false, Number = 6, CanonicalName = "tcp" },
                    DestinationPorts = [new PortIntervalView { Start = 80, End = 80 }],
                },
            ],
        });
        Assert.True(service.IsSuccess);
        Assert.Single(service.Value!.ServiceObjects);
        hash = Convert.FromHexString(service.Value.ContentHashHex);

        ReplacePolicyTestsUseCase replaceTests = new(auth, policies, idempotency, audit);
        ApplicationResult<PolicyRevisionView> withTests = await replaceTests.ExecuteAsync(new ReplacePolicyTestsCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            TestsJson = """[{"id":"dddddddd-dddd-dddd-dddd-dddddddddddd","name":"t1"}]""",
        });
        Assert.True(withTests.IsSuccess);
        Assert.Contains("dddddddd-dddd-dddd-dddd-dddddddddddd", withTests.Value!.TestsJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReplaceChainContractsCompanyBaselineOnly()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        ReplaceChainContractsUseCase useCase = new(auth, policies, idempotency, audit);
        ApplicationResult<PolicyRevisionView> ok = await useCase.ExecuteAsync(new ReplaceChainContractsCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Contracts =
            [
                new ChainContractView
                {
                    Family = IpAddressFamily.IPv4,
                    Chain = PolicyFilterChain.Forward,
                    DefaultDisposition = "DROP",
                },
            ],
        });
        Assert.True(ok.IsSuccess);
        Assert.Single(ok.Value!.ChainContracts);
        Assert.Equal("DROP", ok.Value.ChainContracts[0].DefaultDisposition);
    }

    [Fact]
    public async Task DiffPolicyRevisionsReportsRuleChangeAndRisk()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> beforeDraft = await CreateDraftAsync(auth, policies, idempotency, audit);
        ApplicationResult<PolicyDraftView> afterDraft = await CreateDraftAsync(auth, policies, idempotency, audit, "after");
        FakeZoneDefinitionStore zones = new();
        AddRuleUseCase add = new(auth, policies, zones, idempotency, audit);
        byte[] afterHash = Convert.FromHexString(afterDraft.Value!.ContentHashHex);
        ApplicationResult<PolicyRuleMutationView> added = await add.ExecuteAsync(new AddRuleCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = afterDraft.Value.RevisionId,
            ExpectedContentHash = afterHash,
            Family = IpAddressFamily.IPv4,
            Chain = PolicyFilterChain.Forward,
            Stage = PolicyPipelineStage.CompanyAllow,
            Effect = new RuleEffectInput { Kind = PolicyRuleEffect.Accept },
        });
        Assert.True(added.IsSuccess);

        DiffPolicyRevisionsUseCase diff = new(auth, policies);
        ApplicationResult<PolicyRevisionDiffView> result = await diff.ExecuteAsync(new DiffPolicyRevisionsQuery
        {
            Actor = "admin",
            BeforeRevisionId = beforeDraft.Value!.RevisionId,
            AfterRevisionId = afterDraft.Value.RevisionId,
        });
        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value!.RuleChanges);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.RiskLevel));
        Assert.Contains(
            result.Value.RuleChanges,
            line => line.Changes.Contains(PolicyEvidenceAnalysisCodes.ChangeAdded, StringComparer.Ordinal));
    }

    [Fact]
    public async Task ValidateRevisionFailurePaths()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        ValidateRevisionUseCase useCase = new(auth, policies, idempotency, audit);

        auth.DeniedPermissions.Add(ApplicationPermissions.PolicyWrite);
        ApplicationResult<PolicyRevisionView> forbidden = await useCase.ExecuteAsync(new ValidateRevisionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
        });
        Assert.True(forbidden.IsFailure);
        Assert.Equal("forbidden", forbidden.Error!.Code);
        auth.DeniedPermissions.Clear();

        ApplicationResult<PolicyRevisionView> emptyKey = await useCase.ExecuteAsync(new ValidateRevisionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.Empty,
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
        });
        Assert.True(emptyKey.IsFailure);
        Assert.Equal("validation", emptyKey.Error!.Code);

        ApplicationResult<PolicyRevisionView> missing = await useCase.ExecuteAsync(new ValidateRevisionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(),
            ExpectedContentHash = hash,
        });
        Assert.True(missing.IsFailure);
        Assert.Equal("not_found", missing.Error!.Code);

        PolicyRevision? revision = await policies.GetRevisionAsync(new PolicyRevisionId(draft.Value.RevisionId));
        Assert.NotNull(revision);
        revision!.MarkValidated();
        revision.SubmitForReview();
        await policies.SaveRevisionAsync(revision);
        ApplicationResult<PolicyRevisionView> badState = await useCase.ExecuteAsync(new ValidateRevisionCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = Convert.FromHexString(revision.ContentHash.ToString()),
        });
        Assert.True(badState.IsFailure);
        Assert.Equal("validation", badState.Error!.Code);
    }

    [Fact]
    public async Task CatalogMutationFailurePaths()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        UpsertAddressObjectUseCase upsertAddress = new(auth, policies, idempotency, audit);
        ReplaceChainContractsUseCase replaceContracts = new(auth, policies, idempotency, audit);
        ReplacePolicyTestsUseCase replaceTests = new(auth, policies, idempotency, audit);

        auth.DeniedPermissions.Add(ApplicationPermissions.PolicyWrite);
        ApplicationResult<PolicyRevisionView> forbidden = await upsertAddress.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "x",
            Family = IpAddressFamily.IPv4,
            Entries = [new AddressObjectEntryView { Kind = "HOST", Address = "10.0.0.1" }],
        });
        Assert.True(forbidden.IsFailure);
        Assert.Equal("forbidden", forbidden.Error!.Code);
        auth.DeniedPermissions.Clear();

        ApplicationResult<PolicyRevisionView> emptyKey = await upsertAddress.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.Empty,
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "x",
            Family = IpAddressFamily.IPv4,
            Entries = [new AddressObjectEntryView { Kind = "HOST", Address = "10.0.0.1" }],
        });
        Assert.True(emptyKey.IsFailure);
        Assert.Equal("validation", emptyKey.Error!.Code);

        ApplicationResult<PolicyRevisionView> missingRevision = await upsertAddress.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = Guid.NewGuid(),
            ExpectedContentHash = hash,
            Name = "x",
            Family = IpAddressFamily.IPv4,
            Entries = [new AddressObjectEntryView { Kind = "HOST", Address = "10.0.0.1" }],
        });
        Assert.True(missingRevision.IsFailure);
        Assert.Equal("not_found", missingRevision.Error!.Code);

        ApplicationResult<PolicyRevisionView> badEntry = await upsertAddress.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "x",
            Family = IpAddressFamily.IPv4,
            Entries = [new AddressObjectEntryView { Kind = "BOGUS", Address = "10.0.0.1" }],
        });
        Assert.True(badEntry.IsFailure);
        Assert.Equal("validation", badEntry.Error!.Code);

        ApplicationResult<PolicyRevisionView> rejectWithoutMode = await replaceContracts.ExecuteAsync(
            new ReplaceChainContractsCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                RevisionId = draft.Value.RevisionId,
                ExpectedContentHash = hash,
                Contracts =
                [
                    new ChainContractView
                    {
                        Family = IpAddressFamily.IPv4,
                        Chain = PolicyFilterChain.Forward,
                        DefaultDisposition = "REJECT",
                    },
                ],
            });
        Assert.True(rejectWithoutMode.IsFailure);
        Assert.Equal("validation", rejectWithoutMode.Error!.Code);

        ApplicationResult<PolicyRevisionView> rejectOk = await replaceContracts.ExecuteAsync(new ReplaceChainContractsCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Contracts =
            [
                new ChainContractView
                {
                    Family = IpAddressFamily.IPv4,
                    Chain = PolicyFilterChain.Forward,
                    DefaultDisposition = "REJECT",
                    RejectMode = RejectMode.AdminProhibited,
                },
            ],
        });
        Assert.True(rejectOk.IsSuccess);
        Assert.Equal("REJECT", rejectOk.Value!.ChainContracts[0].DefaultDisposition);
        Assert.Equal(RejectMode.AdminProhibited, rejectOk.Value.ChainContracts[0].RejectMode);
        hash = Convert.FromHexString(rejectOk.Value.ContentHashHex);

        ApplicationResult<PolicyRevisionView> unknownDisposition = await replaceContracts.ExecuteAsync(
            new ReplaceChainContractsCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                RevisionId = draft.Value.RevisionId,
                ExpectedContentHash = hash,
                Contracts =
                [
                    new ChainContractView
                    {
                        Family = IpAddressFamily.IPv4,
                        Chain = PolicyFilterChain.Input,
                        DefaultDisposition = "ACCEPT",
                    },
                ],
            });
        Assert.True(unknownDisposition.IsFailure);
        Assert.Equal("validation", unknownDisposition.Error!.Code);

        ApplicationResult<PolicyRevisionView> badTestsObject = await replaceTests.ExecuteAsync(new ReplacePolicyTestsCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            TestsJson = """{"not":"array"}""",
        });
        Assert.True(badTestsObject.IsFailure);
        Assert.Equal("validation", badTestsObject.Error!.Code);

        ApplicationResult<PolicyRevisionView> missingTests = await replaceTests.ExecuteAsync(new ReplacePolicyTestsCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
        });
        Assert.True(missingTests.IsFailure);
        Assert.Equal("validation", missingTests.Error!.Code);

        ApplicationResult<PolicyRevisionView> structuredTests = await replaceTests.ExecuteAsync(new ReplacePolicyTestsCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            TestJsonElements = ["""{"id":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"}"""],
        });
        Assert.True(structuredTests.IsSuccess);
        hash = Convert.FromHexString(structuredTests.Value!.ContentHashHex);

        ApplicationResult<PolicyRevisionView> casConflict = await upsertAddress.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = new byte[32],
            Name = "stale",
            Family = IpAddressFamily.IPv4,
            Entries = [new AddressObjectEntryView { Kind = "HOST", Address = "10.0.0.9" }],
        });
        Assert.True(casConflict.IsFailure);
        Assert.Equal("conflict", casConflict.Error!.Code);

        PolicyRevision? revision = await policies.GetRevisionAsync(new PolicyRevisionId(draft.Value.RevisionId));
        Assert.NotNull(revision);
        revision!.MarkValidated();
        revision.SubmitForReview();
        revision.Approve(DateTimeOffset.UtcNow);
        await policies.SaveRevisionAsync(revision);
        ApplicationResult<PolicyRevisionView> approvedBlocked = await upsertAddress.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = Convert.FromHexString(revision.ContentHash.ToString()),
            Name = "blocked",
            Family = IpAddressFamily.IPv4,
            Entries = [new AddressObjectEntryView { Kind = "HOST", Address = "10.0.0.8" }],
        });
        Assert.True(approvedBlocked.IsFailure);
        Assert.Equal("validation", approvedBlocked.Error!.Code);
    }

    [Fact]
    public async Task OverlayCannotReplaceChainContracts()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit);
        ApplicationResult<PolicyDraftView> overlay = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = "site-overlay",
            Kind = PolicyKind.SiteOverlay,
            OwnerScope = PolicyOwnerScope.Site,
            OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ParentContextHash = new byte[32],
        });
        Assert.True(overlay.IsSuccess);
        ReplaceChainContractsUseCase useCase = new(auth, policies, idempotency, audit);
        ApplicationResult<PolicyRevisionView> blocked = await useCase.ExecuteAsync(new ReplaceChainContractsCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = overlay.Value!.RevisionId,
            ExpectedContentHash = Convert.FromHexString(overlay.Value.ContentHashHex),
            Contracts =
            [
                new ChainContractView
                {
                    Family = IpAddressFamily.IPv4,
                    Chain = PolicyFilterChain.Forward,
                    DefaultDisposition = "DROP",
                },
            ],
        });
        Assert.True(blocked.IsFailure);
        Assert.Equal("validation", blocked.Error!.Code);
        Assert.Contains("COMPANY_BASELINE", blocked.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiffPolicyRevisionsFailurePaths()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        DiffPolicyRevisionsUseCase diff = new(auth, policies);

        auth.DeniedPermissions.Add(ApplicationPermissions.PolicyRead);
        ApplicationResult<PolicyRevisionDiffView> forbidden = await diff.ExecuteAsync(new DiffPolicyRevisionsQuery
        {
            Actor = "admin",
            BeforeRevisionId = draft.Value!.RevisionId,
            AfterRevisionId = draft.Value.RevisionId,
        });
        Assert.True(forbidden.IsFailure);
        Assert.Equal("forbidden", forbidden.Error!.Code);
        auth.DeniedPermissions.Clear();

        ApplicationResult<PolicyRevisionDiffView> missing = await diff.ExecuteAsync(new DiffPolicyRevisionsQuery
        {
            Actor = "admin",
            BeforeRevisionId = Guid.NewGuid(),
            AfterRevisionId = draft.Value.RevisionId,
        });
        Assert.True(missing.IsFailure);
        Assert.Equal("not_found", missing.Error!.Code);
    }

    [Fact]
    public async Task CatalogIdempotencyReplayAndReturnToUnmanaged()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        ReplaceChainContractsUseCase useCase = new(auth, policies, idempotency, audit);
        Guid key = Guid.NewGuid();
        ReplaceChainContractsCommand command = new()
        {
            Actor = "admin",
            IdempotencyKey = key,
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Contracts =
            [
                new ChainContractView
                {
                    Family = IpAddressFamily.IPv4,
                    Chain = PolicyFilterChain.Forward,
                    DefaultDisposition = "RETURN_TO_UNMANAGED",
                },
            ],
        };
        ApplicationResult<PolicyRevisionView> first = await useCase.ExecuteAsync(command);
        Assert.True(first.IsSuccess);
        Assert.Equal("RETURN_TO_UNMANAGED", first.Value!.ChainContracts[0].DefaultDisposition);

        ApplicationResult<PolicyRevisionView> replay = await useCase.ExecuteAsync(command);
        Assert.True(replay.IsSuccess);
        Assert.Equal(first.Value.ContentHashHex, replay.Value!.ContentHashHex);
    }

    [Fact]
    public async Task CatalogMappingAndServiceReplaceCoverBranches()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        byte[] hash = Convert.FromHexString(draft.Value!.ContentHashHex);
        UpsertAddressObjectUseCase upsertAddress = new(auth, policies, idempotency, audit);
        UpsertServiceObjectUseCase upsertService = new(auth, policies, idempotency, audit);
        GetPolicyRevisionUseCase get = new(auth, policies);

        ApplicationResult<PolicyRevisionView> withAddress = await upsertAddress.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "mixed",
            Family = IpAddressFamily.IPv4,
            Description = "desc",
            Entries =
            [
                new AddressObjectEntryView { Kind = "HOST", Address = "10.0.0.1" },
                new AddressObjectEntryView { Kind = "PREFIX", Address = "10.1.0.0", PrefixLength = 24 },
                new AddressObjectEntryView { Kind = "RANGE", Start = "10.2.0.1", End = "10.2.0.10" },
            ],
        });
        Assert.True(withAddress.IsSuccess);
        hash = Convert.FromHexString(withAddress.Value!.ContentHashHex);

        ApplicationResult<PolicyRevisionView> withService = await upsertService.ExecuteAsync(new UpsertServiceObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "mixed-svc",
            Description = "svc-desc",
            Terms =
            [
                new ServiceTermView
                {
                    Protocol = new IpProtocolView { Any = true },
                },
                new ServiceTermView
                {
                    Protocol = new IpProtocolView { Any = false, Number = 6, CanonicalName = "tcp" },
                    SourcePorts = [new PortIntervalView { Start = 1024, End = 2048 }],
                    DestinationPorts = [new PortIntervalView { Start = 443, End = 443 }],
                },
                new ServiceTermView
                {
                    Protocol = new IpProtocolView { Any = false, Number = 1, CanonicalName = "icmp" },
                    IcmpSelectors =
                    [
                        new IcmpSelectorView { Type = 8, Code = 0 },
                        new IcmpSelectorView { Type = 3 },
                    ],
                },
            ],
        });
        Assert.True(withService.IsSuccess);
        Guid serviceId = withService.Value!.ServiceObjects[0].Id;
        hash = Convert.FromHexString(withService.Value.ContentHashHex);

        ApplicationResult<PolicyRevisionView> replacedService = await upsertService.ExecuteAsync(new UpsertServiceObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            ObjectId = serviceId,
            Name = "mixed-svc-2",
            Terms =
            [
                new ServiceTermView
                {
                    Protocol = new IpProtocolView { Any = false, Number = 17, CanonicalName = "udp" },
                    DestinationPorts = [new PortIntervalView { Start = 53, End = 53 }],
                },
            ],
        });
        Assert.True(replacedService.IsSuccess);
        Assert.Single(replacedService.Value!.ServiceObjects);
        Assert.Equal("mixed-svc-2", replacedService.Value.ServiceObjects[0].Name);
        hash = Convert.FromHexString(replacedService.Value.ContentHashHex);

        ApplicationResult<PolicyRevisionView> loaded = await get.ExecuteAsync(new GetPolicyRevisionQuery
        {
            Actor = "admin",
            RevisionId = draft.Value.RevisionId,
        });
        Assert.True(loaded.IsSuccess);
        Assert.Equal(3, loaded.Value!.AddressObjects[0].Entries.Count);
        Assert.Contains(loaded.Value.AddressObjects[0].Entries, e => e.Kind == "HOST");
        Assert.Contains(loaded.Value.AddressObjects[0].Entries, e => e.Kind == "RANGE");
        Assert.Equal("desc", loaded.Value.AddressObjects[0].Description);
        Assert.Contains("mixed-svc-2", loaded.Value.ServiceObjects[0].Name, StringComparison.Ordinal);

        // Inject PREFIX-shaped catalog JSON (writer normalizes PREFIX→RANGE intervals).
        PolicyRevision? revision = await policies.GetRevisionAsync(new PolicyRevisionId(draft.Value.RevisionId));
        Assert.NotNull(revision);
        PolicyDocument current = PolicyDocumentReader.Read(revision!.CanonicalBytes);
        JsonElement prefixShaped = JsonDocument.Parse(
            """
            {"id":"ffffffff-ffff-ffff-ffff-ffffffffffff","name":"pfx","family":"IPv6","description":"v6",
             "entries":[{"kind":"PREFIX","address":"2001:db8::","prefix_length":64},
                        {"kind":"HOST","address":"2001:db8::1"}]}
            """).RootElement.Clone();
        JsonElement skipBogusEntry = JsonDocument.Parse(
            """
            {"id":"55555555-5555-5555-5555-555555555555","name":"bogus-entry","family":"IPv4",
             "entries":[{"kind":"BOGUS","address":"x"}]}
            """).RootElement.Clone();
        JsonElement skipNoName = JsonDocument.Parse("""{"id":"11111111-1111-1111-1111-111111111111","family":"IPv4","entries":[]}""")
            .RootElement.Clone();
        JsonElement skipBadFamily = JsonDocument.Parse(
            """{"id":"22222222-2222-2222-2222-222222222222","name":"badfam","family":"IPX","entries":[]}""")
            .RootElement.Clone();
        JsonElement serviceShaped = JsonDocument.Parse(
            """
            {"id":"33333333-3333-3333-3333-333333333333","name":"svcmap","description":"d",
             "terms":[{"protocol":{"any":true}},
                      {"protocol":{"number":6,"canonical_name":"tcp"},
                       "source_ports":[{"start":1,"end":2}],
                       "destination_ports":[{"start":80,"end":80}],
                       "icmp_selectors":[{"type":8,"code":0},{"type":3}]},
                      {"protocol":{"number":1}}]}
            """).RootElement.Clone();
        JsonElement skipService = JsonDocument.Parse("""{"id":"44444444-4444-4444-4444-444444444444","terms":[]}""")
            .RootElement.Clone();
        PolicyDocument mapped = new(
            current.Kind,
            current.OwnerScope,
            current.SchemaVersion,
            current.ChainContracts,
            current.ZoneDefinitions,
            [prefixShaped, skipBogusEntry, skipNoName, skipBadFamily],
            [serviceShaped, skipService],
            current.Rules,
            current.Tests,
            current.ExceptionMetadata);
        revision.ReplaceDocument(mapped, revision.ParentContextHash);
        await policies.SaveRevisionAsync(revision);

        ApplicationResult<PolicyRevisionView> remapped = await get.ExecuteAsync(new GetPolicyRevisionQuery
        {
            Actor = "admin",
            RevisionId = draft.Value.RevisionId,
        });
        Assert.True(remapped.IsSuccess);
        Assert.Contains(remapped.Value!.AddressObjects, a => a.Name == "pfx" && a.Family == IpAddressFamily.IPv6);
        Assert.Contains(
            remapped.Value.AddressObjects.SelectMany(a => a.Entries),
            e => e.Kind == "PREFIX" && e.PrefixLength == 64);
        Assert.Contains(remapped.Value.ServiceObjects, s => s.Name == "svcmap" && s.Terms.Count >= 2);
        hash = Convert.FromHexString(remapped.Value.ContentHashHex);

        ApplicationResult<PolicyRevisionView> missingPrefix = await upsertAddress.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "bad-prefix",
            Family = IpAddressFamily.IPv4,
            Entries = [new AddressObjectEntryView { Kind = "PREFIX", Address = "10.9.0.0" }],
        });
        Assert.True(missingPrefix.IsFailure);

        ApplicationResult<PolicyRevisionView> badIp = await upsertAddress.ExecuteAsync(new UpsertAddressObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "bad-ip",
            Family = IpAddressFamily.IPv4,
            Entries = [new AddressObjectEntryView { Kind = "HOST", Address = "not-an-ip" }],
        });
        Assert.True(badIp.IsFailure);

        ApplicationResult<PolicyRevisionView> badProtocol = await upsertService.ExecuteAsync(new UpsertServiceObjectCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value.RevisionId,
            ExpectedContentHash = hash,
            Name = "bad-proto",
            Terms =
            [
                new ServiceTermView { Protocol = new IpProtocolView { Any = false, Number = null } },
            ],
        });
        Assert.True(badProtocol.IsFailure);
    }

    [Fact]
    public async Task DiffFailsOnCorruptCatalogAndCoversOwnerKinds()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> before = await CreateDraftAsync(auth, policies, idempotency, audit, "before");
        ApplicationResult<PolicyDraftView> after = await CreateDraftAsync(auth, policies, idempotency, audit, "after");
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit);
        ApplicationResult<PolicyDraftView> nodeOverlay = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = "node-overlay",
            Kind = PolicyKind.NodeOverlay,
            OwnerScope = PolicyOwnerScope.Node,
            OwnerId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            ParentContextHash = new byte[32],
        });
        Assert.True(nodeOverlay.IsSuccess);
        ApplicationResult<PolicyDraftView> exceptionDraft = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = "exception",
            Kind = PolicyKind.Exception,
            OwnerScope = PolicyOwnerScope.Site,
            OwnerId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
            ParentContextHash = new byte[32],
        });
        Assert.True(exceptionDraft.IsSuccess);

        DiffPolicyRevisionsUseCase diff = new(auth, policies);
        ApplicationResult<PolicyRevisionDiffView> nodeDiff = await diff.ExecuteAsync(new DiffPolicyRevisionsQuery
        {
            Actor = "admin",
            BeforeRevisionId = nodeOverlay.Value!.RevisionId,
            AfterRevisionId = exceptionDraft.Value!.RevisionId,
        });
        Assert.True(nodeDiff.IsSuccess);

        PolicyRevision? afterRevision = await policies.GetRevisionAsync(new PolicyRevisionId(after.Value!.RevisionId));
        Assert.NotNull(afterRevision);
        PolicyDocument afterDoc = PolicyDocumentReader.Read(afterRevision!.CanonicalBytes);
        JsonElement badAddress = JsonDocument.Parse("""{"name":"no-id","family":"IPv4","entries":[]}""").RootElement.Clone();
        JsonElement zone = JsonDocument.Parse(
            """{"id":"dddddddd-dddd-dddd-dddd-dddddddddddd","name":"z1"}""").RootElement.Clone();
        PolicyDocument corrupt = new(
            afterDoc.Kind,
            afterDoc.OwnerScope,
            afterDoc.SchemaVersion,
            afterDoc.ChainContracts,
            [zone],
            [badAddress],
            afterDoc.ServiceObjects,
            afterDoc.Rules,
            afterDoc.Tests,
            afterDoc.ExceptionMetadata);
        afterRevision.ReplaceDocument(corrupt, afterRevision.ParentContextHash);
        await policies.SaveRevisionAsync(afterRevision);

        ApplicationResult<PolicyRevisionDiffView> badDiff = await diff.ExecuteAsync(new DiffPolicyRevisionsQuery
        {
            Actor = "admin",
            BeforeRevisionId = before.Value!.RevisionId,
            AfterRevisionId = after.Value.RevisionId,
        });
        Assert.True(badDiff.IsFailure);
        Assert.Equal("validation", badDiff.Error!.Code);

        PolicyRevision? beforeRevision = await policies.GetRevisionAsync(new PolicyRevisionId(before.Value.RevisionId));
        Assert.NotNull(beforeRevision);
        PolicyDocument beforeDoc = PolicyDocumentReader.Read(beforeRevision!.CanonicalBytes);
        JsonElement badService = JsonDocument.Parse("""{"id":"eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"}""").RootElement.Clone();
        PolicyDocument corruptService = new(
            beforeDoc.Kind,
            beforeDoc.OwnerScope,
            beforeDoc.SchemaVersion,
            beforeDoc.ChainContracts,
            beforeDoc.ZoneDefinitions,
            beforeDoc.AddressObjects,
            [badService],
            beforeDoc.Rules,
            beforeDoc.Tests,
            beforeDoc.ExceptionMetadata);
        beforeRevision.ReplaceDocument(corruptService, beforeRevision.ParentContextHash);
        await policies.SaveRevisionAsync(beforeRevision);

        ApplicationResult<PolicyDraftView> clean = await CreateDraftAsync(auth, policies, idempotency, audit, "clean");
        ApplicationResult<PolicyRevisionDiffView> badServiceDiff = await diff.ExecuteAsync(new DiffPolicyRevisionsQuery
        {
            Actor = "admin",
            BeforeRevisionId = before.Value.RevisionId,
            AfterRevisionId = clean.Value!.RevisionId,
        });
        Assert.True(badServiceDiff.IsFailure);

        // Skip unmappable address/service catalog rows on Get (MapAddresses/MapServices soft-skip).
        ApplicationResult<PolicyRevisionView> loadedCorrupt = await new GetPolicyRevisionUseCase(auth, policies)
            .ExecuteAsync(new GetPolicyRevisionQuery { Actor = "admin", RevisionId = after.Value.RevisionId });
        Assert.True(loadedCorrupt.IsSuccess);
        Assert.Empty(loadedCorrupt.Value!.AddressObjects);

        ApplicationResult<PolicyRevisionView> exceptionAddressBlocked = await new UpsertAddressObjectUseCase(
            auth, policies, idempotency, audit).ExecuteAsync(new UpsertAddressObjectCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                RevisionId = exceptionDraft.Value.RevisionId,
                ExpectedContentHash = Convert.FromHexString(exceptionDraft.Value.ContentHashHex),
                Name = "x",
                Family = IpAddressFamily.IPv4,
                Entries = [new AddressObjectEntryView { Kind = "HOST", Address = "10.0.0.1" }],
            });
        Assert.True(exceptionAddressBlocked.IsFailure);
        Assert.Contains("EXCEPTION", exceptionAddressBlocked.Error!.Message, StringComparison.Ordinal);

        ApplicationResult<PolicyRevisionView> exceptionServiceBlocked = await new UpsertServiceObjectUseCase(
            auth, policies, idempotency, audit).ExecuteAsync(new UpsertServiceObjectCommand
            {
                Actor = "admin",
                IdempotencyKey = Guid.NewGuid(),
                RevisionId = exceptionDraft.Value.RevisionId,
                ExpectedContentHash = Convert.FromHexString(exceptionDraft.Value.ContentHashHex),
                Name = "x",
                Terms =
            [
                new ServiceTermView
                {
                    Protocol = new IpProtocolView { Any = false, Number = 6, CanonicalName = "tcp" },
                    DestinationPorts = [new PortIntervalView { Start = 80, End = 80 }],
                },
            ],
            });
        Assert.True(exceptionServiceBlocked.IsFailure);
    }

    [Fact]
    public async Task InvalidTestsJsonThrowsJsonExceptionPath()
    {
        (FakeAuthorizationBoundary auth, FakePolicyStore policies, FakeIdempotencyStore idempotency, FakeAuditEventWriter audit) =
            CreateDeps();
        ApplicationResult<PolicyDraftView> draft = await CreateDraftAsync(auth, policies, idempotency, audit);
        ReplacePolicyTestsUseCase replaceTests = new(auth, policies, idempotency, audit);
        ApplicationResult<PolicyRevisionView> badJson = await replaceTests.ExecuteAsync(new ReplacePolicyTestsCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            RevisionId = draft.Value!.RevisionId,
            ExpectedContentHash = Convert.FromHexString(draft.Value.ContentHashHex),
            TestsJson = "{not-json",
        });
        Assert.True(badJson.IsFailure);
        Assert.Equal("validation", badJson.Error!.Code);
    }

    private static (
        FakeAuthorizationBoundary Auth,
        FakePolicyStore Policies,
        FakeIdempotencyStore Idempotency,
        FakeAuditEventWriter Audit) CreateDeps()
        => (new FakeAuthorizationBoundary(), new FakePolicyStore(), new FakeIdempotencyStore(), new FakeAuditEventWriter());

    private static async Task<ApplicationResult<PolicyDraftView>> CreateDraftAsync(
        FakeAuthorizationBoundary auth,
        FakePolicyStore policies,
        FakeIdempotencyStore idempotency,
        FakeAuditEventWriter audit,
        string name = "baseline")
    {
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit);
        return await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = name,
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
        });
    }
}
