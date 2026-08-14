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
            auth, new FakeNodeStore(), new FakePolicyStore(), new FakeZoneDefinitionStore());
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
        return (new ComposeEffectivePolicyUseCase(auth, nodes, policies, zones), nodes, policies, node);
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

    private static PolicyRule AcceptRule()
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            PolicyPipelineStage.CompanyAllow,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(PolicyRuleEffect.Accept));
}
