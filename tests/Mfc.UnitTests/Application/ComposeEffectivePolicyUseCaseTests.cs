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
using PolicyContainer = Mfc.Domain.Policy.Policy;

namespace Mfc.UnitTests.Application;

public sealed class ComposeEffectivePolicyUseCaseTests
{
    [Fact]
    public async Task A1LoadsNodeUniqueCompanyAndLatestApproved()
    {
        (ComposeEffectivePolicyUseCase useCase, FakeNodeStore nodes, FakePolicyStore policies, Node node) =
            await SeedAsync();
        PolicyContainer company = CreateCompany();
        await policies.AddPolicyAsync(company);
        PolicyDocument empty = PolicyDocument.CreateEmpty(company.Kind, company.OwnerScope);
        PolicyRevision stale = Approve(company, empty, parent: null, revisionNumber: 1);
        await policies.AddRevisionAsync(stale);
        PolicyRule rule = AcceptRule();
        PolicyRevision latest = Approve(company, empty.WithRules([rule]), parent: null, revisionNumber: 2);
        await policies.AddRevisionAsync(latest);

        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsSuccess);
        Assert.Equal(node.Id.Value, result.Value!.NodeId);
        Assert.Equal(latest.Id.Value, result.Value.Company.RevisionId);
        Assert.Equal(2u, result.Value.Company.RevisionNumber);
        Assert.Single(result.Value.ActiveRules);
        Assert.Equal(32, result.Value.LogicalEffectiveHash.Length);
    }

    [Fact]
    public async Task A2MissingSiteAndNodeOverlayIsOk()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        await AddApprovedCompanyAsync(policies);
        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Site);
        Assert.Null(result.Value.Node);
    }

    [Fact]
    public async Task A3ParentContextMismatchMapsComposeCode()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        PolicyContainer companyPolicy = await AddApprovedCompanyAsync(policies);
        PolicyRevision? companyRev = (await policies.ListRevisionsAsync(companyPolicy.Id))
            .Single(r => r.State == PolicyRevisionState.Approved);
        PolicyContainer sitePolicy = PolicyContainer.Create(
            NonEmptyName.Create("site-overlay"),
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            node.SiteId.Value);
        await policies.AddPolicyAsync(sitePolicy);
        PolicyDocument siteDoc = PolicyDocument.CreateEmpty(sitePolicy.Kind, sitePolicy.OwnerScope);
        PolicyRevision siteRev = Approve(sitePolicy, siteDoc, Hash256.Create(new byte[32]));
        await policies.AddRevisionAsync(siteRev);
        _ = companyRev;

        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.ParentContextMismatch, result.Error!.Code);
        Assert.NotEqual("conflict", result.Error.Code);
        Assert.NotEqual("failed", result.Error.Code);
    }

    [Fact]
    public async Task A4ZeroActiveCompaniesReturnsCompanyRequired()
    {
        (ComposeEffectivePolicyUseCase useCase, _, _, Node node) = await SeedAsync();
        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.CompanyRequired, result.Error!.Code);
        Assert.NotEqual("conflict", result.Error.Code);
        Assert.NotEqual("failed", result.Error.Code);
    }

    [Fact]
    public async Task A4TwoActiveCompaniesReturnsPolicyNotUnique()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        await AddApprovedCompanyAsync(policies, "c1");
        await AddApprovedCompanyAsync(policies, "c2");
        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.PolicyNotUnique, result.Error!.Code);
        Assert.NotEqual("conflict", result.Error.Code);
        Assert.NotEqual("failed", result.Error.Code);
    }

    [Fact]
    public async Task A5ArchivedCompanyIsIgnored()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        PolicyContainer company = CreateCompany();
        company.Archive();
        await policies.AddPolicyAsync(company);
        await policies.AddRevisionAsync(Approve(company, PolicyDocument.CreateEmpty(company.Kind, company.OwnerScope), null));
        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.CompanyRequired, result.Error!.Code);
    }

    [Fact]
    public async Task A6MissingNodeIsNotFoundNotComposeCode()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, _) = await SeedAsync();
        await AddApprovedCompanyAsync(policies);
        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(Guid.NewGuid()));
        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error!.Code);
        Assert.False(result.Error.Code.StartsWith("POLICY_COMPOSE_", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A7DuplicateSiteOwnerIsPolicyNotUnique()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        PolicyContainer company = await AddApprovedCompanyAsync(policies);
        PolicyRevision companyRev = (await policies.ListRevisionsAsync(company.Id))
            .Single(r => r.State == PolicyRevisionState.Approved);
        Hash256 parent = companyRev.ContentHash;
        PolicyContainer first = PolicyContainer.Create(
            NonEmptyName.Create("site-a"),
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            node.SiteId.Value);
        PolicyContainer second = PolicyContainer.Create(
            NonEmptyName.Create("site-b"),
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            node.SiteId.Value);
        await policies.AddPolicyAsync(first);
        await policies.AddPolicyAsync(second);
        await policies.AddRevisionAsync(
            Approve(first, PolicyDocument.CreateEmpty(first.Kind, first.OwnerScope), parent));
        await policies.AddRevisionAsync(
            Approve(second, PolicyDocument.CreateEmpty(second.Kind, second.OwnerScope), parent));

        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.PolicyNotUnique, result.Error!.Code);
    }

    [Fact]
    public async Task CompanyWithoutApprovedRevisionIsCompanyRequired()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        PolicyContainer company = CreateCompany();
        await policies.AddPolicyAsync(company);
        PolicyRevision draft = PolicyRevision.CreateDraft(
            company,
            1,
            PolicyDocument.CreateEmpty(company.Kind, company.OwnerScope),
            parentContextHash: null,
            UserId.New(),
            DateTimeOffset.UtcNow);
        await policies.AddRevisionAsync(draft);
        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyComposeCodes.CompanyRequired, result.Error!.Code);
    }

    [Fact]
    public async Task OverlayWithoutApprovedRevisionIsAbsent()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        await AddApprovedCompanyAsync(policies);
        PolicyContainer site = PolicyContainer.Create(
            NonEmptyName.Create("site-draft-only"),
            PolicyKind.SiteOverlay,
            PolicyOwnerScope.Site,
            node.SiteId.Value);
        await policies.AddPolicyAsync(site);
        PolicyRevision draft = PolicyRevision.CreateDraft(
            site,
            1,
            PolicyDocument.CreateEmpty(site.Kind, site.OwnerScope),
            Hash256.Create(new byte[32]),
            UserId.New(),
            DateTimeOffset.UtcNow);
        await policies.AddRevisionAsync(draft);

        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value!.Site);
    }

    [Fact]
    public async Task ComposeEffectiveUnauthorized()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.PolicyRead);
        ComposeEffectivePolicyUseCase useCase = new(
            auth, new FakeNodeStore(), new FakePolicyStore(), new FakeZoneDefinitionStore(), new FakeClock());
        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(Guid.NewGuid()));
        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task UnusedObjectFindingSurfacesOnSuccess()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        PolicyContainer company = CreateCompany();
        await policies.AddPolicyAsync(company);
        Guid unused = Guid.NewGuid();
        PolicyDocument document = new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects: [JsonDocument.Parse("{\"id\":\"" + unused + "\"}").RootElement.Clone()],
            rules: [AcceptRule()]);
        await policies.AddRevisionAsync(Approve(company, document, null));
        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsSuccess);
        Assert.Contains(result.Value!.Findings, f => f.Code == PolicyComposeCodes.UnusedPolicyObject);
    }

    [Fact]
    public async Task A3ApprovedEmptyExceptionMetadataIsExceptionCode()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        (PolicyContainer company, PolicyRevision companyRev, PolicyRule deny) =
            await AddApprovedCompanyWithDenyAsync(policies);
        _ = company;
        _ = deny;
        PolicyContainer exception = PolicyContainer.Create(
            NonEmptyName.Create("empty-meta"),
            PolicyKind.Exception,
            PolicyOwnerScope.Site,
            node.SiteId.Value);
        await policies.AddPolicyAsync(exception);
        await policies.AddRevisionAsync(
            Approve(
                exception,
                PolicyDocument.CreateEmpty(exception.Kind, exception.OwnerScope),
                Hash256.Create(new byte[32])));

        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsFailure);
        Assert.Equal(PolicyExceptionCodes.MetadataInvalid, result.Error!.Code);
        Assert.StartsWith("POLICY_EXCEPTION_", result.Error.Code, StringComparison.Ordinal);
        Assert.NotEqual("conflict", result.Error.Code);
        Assert.NotEqual("failed", result.Error.Code);
        Assert.NotEqual("not_found", result.Error.Code);
        _ = companyRev;
    }

    [Fact]
    public async Task A13ExpiredExceptionIsSkipped()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        (PolicyContainer _, PolicyRevision companyRev, PolicyRule deny) =
            await AddApprovedCompanyWithDenyAsync(policies);
        DateTimeOffset until = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        await AddApprovedExceptionAsync(
            policies,
            node.SiteId.Value,
            companyRev,
            deny,
            until: until);

        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.ActiveRules);
        Assert.Equal(deny.Id.Value, result.Value.ActiveRules[0].Id);
        Assert.DoesNotContain(
            result.Value.ActiveRules,
            r => r.Stage == PolicyPipelineStage.CompanyDenyExemptions);
    }

    [Fact]
    public async Task ALoadTwoExceptionsSameOwnerNeverPolicyNotUnique()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        Guid addrA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid addrB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        PolicyRule denyA = DenyRule(addrA);
        PolicyRule denyB = DenyRule(addrB, ordinal: 1);
        (PolicyContainer _, PolicyRevision companyRev, _) =
            await AddApprovedCompanyWithDenyAsync(policies, denyA, denyB);
        await AddApprovedExceptionAsync(policies, node.SiteId.Value, companyRev, denyA, name: "ex-a");
        await AddApprovedExceptionAsync(policies, node.SiteId.Value, companyRev, denyB, name: "ex-b");

        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value!.ActiveRules.Count);
        Assert.Equal(2, result.Value.ActiveRules.Count(r => r.Stage == PolicyPipelineStage.CompanyDenyExemptions));
        Assert.NotEqual(PolicyComposeCodes.PolicyNotUnique, result.Error?.Code);
    }

    [Fact]
    public async Task A1LoadsSiteAndNodeExceptionsLatestApproved()
    {
        (ComposeEffectivePolicyUseCase useCase, _, FakePolicyStore policies, Node node) = await SeedAsync();
        Guid addrA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid addrB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        PolicyRule denyA = DenyRule(addrA);
        PolicyRule denyB = DenyRule(addrB, ordinal: 1);
        (PolicyContainer _, PolicyRevision companyRev, _) =
            await AddApprovedCompanyWithDenyAsync(policies, denyA, denyB);
        await AddApprovedExceptionAsync(
            policies, node.SiteId.Value, companyRev, denyA, name: "site-ex", scope: PolicyOwnerScope.Site);
        await AddApprovedExceptionAsync(
            policies, node.Id.Value, companyRev, denyB, name: "node-ex", scope: PolicyOwnerScope.Node);

        ApplicationResult<EffectivePolicyView> result = await useCase.ExecuteAsync(Query(node.Id.Value));
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.ActiveRules.Count(r => r.Stage == PolicyPipelineStage.CompanyDenyExemptions));
    }

    private static async Task<(
        ComposeEffectivePolicyUseCase UseCase,
        FakeNodeStore Nodes,
        FakePolicyStore Policies,
        Node Node)> SeedAsync()
    {
        FakeAuthorizationBoundary auth = new();
        FakeNodeStore nodes = new();
        FakePolicyStore policies = new();
        FakeZoneDefinitionStore zones = new();
        Node node = Node.Create(
            SiteId.New(),
            NonEmptyName.Create("edge"),
            NodeKind.Router,
            DeclaredUplinkMode.One);
        await nodes.AddAsync(node);
        return (new ComposeEffectivePolicyUseCase(auth, nodes, policies, zones, new FakeClock()), nodes, policies, node);
    }

    private static ComposeEffectivePolicyQuery Query(Guid nodeId)
        => new() { Actor = "admin", NodeId = nodeId };

    private static PolicyContainer CreateCompany(string name = "baseline")
        => PolicyContainer.Create(NonEmptyName.Create(name), PolicyKind.CompanyBaseline, PolicyOwnerScope.Company, null);

    private static async Task<PolicyContainer> AddApprovedCompanyAsync(FakePolicyStore policies, string name = "baseline")
    {
        PolicyContainer company = CreateCompany(name);
        await policies.AddPolicyAsync(company);
        await policies.AddRevisionAsync(
            Approve(company, PolicyDocument.CreateEmpty(company.Kind, company.OwnerScope), null));
        return company;
    }

    private static PolicyRevision Approve(
        PolicyContainer policy,
        PolicyDocument document,
        Hash256? parent,
        uint revisionNumber = 1)
    {
        PolicyRevision revision = PolicyRevision.CreateDraft(
            policy,
            revisionNumber,
            document,
            parent,
            UserId.New(),
            DateTimeOffset.UtcNow);
        revision.MarkValidated();
        revision.SubmitForReview();
        revision.Approve(DateTimeOffset.UtcNow);
        return revision;
    }

    private static async Task<(PolicyContainer Company, PolicyRevision Revision, PolicyRule Deny)>
        AddApprovedCompanyWithDenyAsync(
            FakePolicyStore policies,
            PolicyRule? deny = null,
            PolicyRule? extraDeny = null)
    {
        deny ??= DenyRule(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        PolicyContainer company = CreateCompany();
        await policies.AddPolicyAsync(company);
        List<PolicyRule> rules = [deny];
        if (extraDeny is not null)
        {
            rules.Add(extraDeny);
        }

        PolicyDocument document = new(
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            addressObjects:
            [
                ObjectJson(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                ObjectJson(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            ],
            rules: rules);
        PolicyRevision revision = Approve(company, document, null);
        await policies.AddRevisionAsync(revision);
        return (company, revision, deny);
    }

    private static async Task AddApprovedExceptionAsync(
        FakePolicyStore policies,
        Guid ownerId,
        PolicyRevision companyRev,
        PolicyRule waived,
        DateTimeOffset? until = null,
        string name = "exception",
        PolicyOwnerScope scope = PolicyOwnerScope.Site)
    {
        DateTimeOffset from = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset validUntil = until ?? new(2026, 12, 31, 0, 0, 0, TimeSpan.Zero);
        ExceptionMetadata meta = ExceptionMetadata.Create(
            scope,
            ownerId,
            PolicyPipelineStage.CompanyDeny,
            waived.Id,
            from,
            validUntil,
            "change window",
            "TICKET-1");
        PolicyRule exempt = PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDenyExemptions,
            0,
            TrafficPredicate.Create(
                sourceAddresses: AddressSelector.Create([new AddressObjectId(waived.Predicate.SourceAddresses!.Include[0].Value)])),
            RuleEffectSpec.Create(PolicyRuleEffect.ExemptDenyStage));
        PolicyDocument document = new(
            PolicyKind.Exception,
            scope,
            rules: [exempt],
            exceptionMetadata: meta);
        Hash256 waivedHash = PolicyHashing.HashContent(PolicyCanonicalWriter.WriteRuleBytes(waived));
        Hash256 parent = PolicyHashing.ComputeParentContextHash(
            PolicyKind.Exception,
            companyRev.ContentHash,
            null,
            null,
            waivedHash)!;
        PolicyContainer policy = PolicyContainer.Create(NonEmptyName.Create(name), PolicyKind.Exception, scope, ownerId);
        await policies.AddPolicyAsync(policy);
        await policies.AddRevisionAsync(Approve(policy, document, parent));
    }

    private static PolicyRule DenyRule(Guid addr, uint ordinal = 0)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyDeny,
            ordinal,
            TrafficPredicate.Create(sourceAddresses: AddressSelector.Create([new AddressObjectId(addr)])),
            RuleEffectSpec.Create(PolicyRuleEffect.Drop),
            exceptionEligible: true);

    private static JsonElement ObjectJson(Guid id)
        => AddressJson(id);

    private static JsonElement AddressJson(Guid id)
    {
        if (id == Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))
        {
            return AddressPrefix(id, "10.0.0.0", 24);
        }

        if (id == Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"))
        {
            return AddressHost(id, "10.0.1.1");
        }

        return AddressHost(id, "10.0.0.1");
    }

    private static JsonElement AddressPrefix(Guid id, string address, int prefixLength)
        => JsonDocument.Parse(
            "{\"id\":\"" + id + "\",\"name\":\"addr\",\"family\":\"IPv4\",\"entries\":[{\"kind\":\"PREFIX\",\"address\":\"" +
            address + "\",\"prefix_length\":" + prefixLength + "}]}").RootElement.Clone();

    private static JsonElement AddressHost(Guid id, string address)
        => JsonDocument.Parse(
            "{\"id\":\"" + id + "\",\"name\":\"addr\",\"family\":\"IPv4\",\"entries\":[{\"kind\":\"HOST\",\"address\":\"" +
            address + "\"}]}").RootElement.Clone();

    private static PolicyRule AcceptRule()
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
}
