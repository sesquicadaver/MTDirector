using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class ListPoliciesUseCaseTests
{
    [Fact]
    public async Task EmptyStoreReturnsEmptyCatalog()
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyStore policies = new();
        ListPoliciesUseCase list = new(auth, policies);

        ApplicationResult<PolicyCatalogListView> result = await list.ExecuteAsync(new ListPoliciesQuery
        {
            Actor = "admin",
        });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Policies);
    }

    [Fact]
    public async Task ListsActivePoliciesWithLatestRevisionSortedByName()
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyStore policies = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit, new FakeUnitOfWork());

        ApplicationResult<PolicyDraftView> beta = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = "beta-baseline",
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
        });
        ApplicationResult<PolicyDraftView> alpha = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = "alpha-baseline",
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
        });
        Assert.True(beta.IsSuccess);
        Assert.True(alpha.IsSuccess);

        ListPoliciesUseCase list = new(auth, policies);
        ApplicationResult<PolicyCatalogListView> result = await list.ExecuteAsync(new ListPoliciesQuery
        {
            Actor = "admin",
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Policies.Count);
        Assert.Equal("alpha-baseline", result.Value.Policies[0].Name);
        Assert.Equal(alpha.Value!.PolicyId, result.Value.Policies[0].PolicyId);
        Assert.Equal(alpha.Value.RevisionId, result.Value.Policies[0].LatestRevisionId);
        Assert.Equal(1u, result.Value.Policies[0].LatestRevisionNumber);
        Assert.Equal(PolicyRevisionState.Draft, result.Value.Policies[0].LatestRevisionState);
        Assert.Equal(alpha.Value.ContentHashHex, result.Value.Policies[0].ContentHashHex);
        Assert.Equal("beta-baseline", result.Value.Policies[1].Name);
        Assert.Equal(beta.Value!.RevisionId, result.Value.Policies[1].LatestRevisionId);
    }

    [Fact]
    public async Task KindFilterExcludesOtherKinds()
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyStore policies = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<PolicyDraftView> company = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = "company-baseline",
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
        });
        Assert.True(company.IsSuccess);

        ListPoliciesUseCase list = new(auth, policies);
        ApplicationResult<PolicyCatalogListView> filtered = await list.ExecuteAsync(new ListPoliciesQuery
        {
            Actor = "admin",
            Kind = PolicyKind.SiteOverlay,
        });

        Assert.True(filtered.IsSuccess);
        Assert.Empty(filtered.Value!.Policies);

        ApplicationResult<PolicyCatalogListView> companyOnly = await list.ExecuteAsync(new ListPoliciesQuery
        {
            Actor = "admin",
            Kind = PolicyKind.CompanyBaseline,
        });
        Assert.True(companyOnly.IsSuccess);
        Assert.Equal(company.Value!.PolicyId, Assert.Single(companyOnly.Value!.Policies).PolicyId);
    }

    [Fact]
    public async Task ArchivedPoliciesAreOmitted()
    {
        FakeAuthorizationBoundary auth = new();
        FakePolicyStore policies = new();
        FakeIdempotencyStore idempotency = new();
        FakeAuditEventWriter audit = new();
        CreateDraftPolicyUseCase create = new(auth, policies, idempotency, audit, new FakeUnitOfWork());
        ApplicationResult<PolicyDraftView> draft = await create.ExecuteAsync(new CreateDraftPolicyCommand
        {
            Actor = "admin",
            IdempotencyKey = Guid.NewGuid(),
            Name = "archived-baseline",
            Kind = PolicyKind.CompanyBaseline,
            OwnerScope = PolicyOwnerScope.Company,
        });
        Assert.True(draft.IsSuccess);

        Domain.Policy.Policy? policy = await policies.GetPolicyAsync(new PolicyId(draft.Value!.PolicyId));
        Assert.NotNull(policy);
        policy.Archive();
        await policies.UpdatePolicyAsync(policy);

        ListPoliciesUseCase list = new(auth, policies);
        ApplicationResult<PolicyCatalogListView> result = await list.ExecuteAsync(new ListPoliciesQuery
        {
            Actor = "admin",
        });

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!.Policies);
    }

    [Fact]
    public async Task PolicyReadDeniedFails()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.PolicyRead);
        ListPoliciesUseCase list = new(auth, new FakePolicyStore());

        ApplicationResult<PolicyCatalogListView> result = await list.ExecuteAsync(new ListPoliciesQuery
        {
            Actor = "admin",
        });

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error!.Code);
    }
}
