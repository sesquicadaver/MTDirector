using System.Text.Json;
using Mfc.Domain;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Xunit;

namespace Mfc.UnitTests.Policy;

public sealed class PolicyLifecycleTests
{
    [Theory]
    [InlineData(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company, false)]
    [InlineData(PolicyKind.SiteOverlay, PolicyOwnerScope.Site, true)]
    [InlineData(PolicyKind.NodeOverlay, PolicyOwnerScope.Node, true)]
    [InlineData(PolicyKind.Exception, PolicyOwnerScope.Site, true)]
    [InlineData(PolicyKind.Exception, PolicyOwnerScope.Node, true)]
    public void CreateSupportsNormativeKinds(PolicyKind kind, PolicyOwnerScope scope, bool requiresOwner)
    {
        Guid? owner = requiresOwner ? Guid.NewGuid() : null;
        Mfc.Domain.Policy.Policy policy = Mfc.Domain.Policy.Policy.Create(
            NonEmptyName.Create("p1"),
            kind,
            scope,
            owner);
        Assert.Equal(kind, policy.Kind);
        Assert.Equal(PolicyStatus.Active, policy.Status);
    }

    [Fact]
    public void CompanyWideExceptionIsForbidden()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            Mfc.Domain.Policy.Policy.Create(
                NonEmptyName.Create("bad"),
                PolicyKind.Exception,
                PolicyOwnerScope.Company,
                ownerId: null));
        Assert.Contains("company-wide", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LifecycleDraftToApprovedAndTerminalTransitions()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = NewBaselineDraft();
        revision.MarkValidated();
        Assert.Equal(PolicyRevisionState.Validated, revision.State);

        revision.SubmitForReview();
        Assert.Equal(PolicyRevisionState.InReview, revision.State);

        DateTimeOffset approvedAt = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        revision.Approve(approvedAt);
        Assert.Equal(PolicyRevisionState.Approved, revision.State);
        Assert.Equal(approvedAt, revision.ApprovedAtUtc);

        Assert.Throws<DomainInvariantException>(() =>
            revision.ReplaceDocument(PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope), null));

        revision.Supersede();
        Assert.Equal(PolicyRevisionState.Superseded, revision.State);
    }

    [Fact]
    public void RejectFromInReviewIsTerminalForPayload()
    {
        (_, PolicyRevision revision) = NewBaselineDraft();
        revision.MarkValidated();
        revision.SubmitForReview();
        revision.Reject();
        Assert.Equal(PolicyRevisionState.Rejected, revision.State);
        Assert.Throws<DomainInvariantException>(() =>
            revision.ReplaceDocument(
                PolicyDocument.CreateEmpty(PolicyKind.CompanyBaseline, PolicyOwnerScope.Company),
                null));
    }

    [Fact]
    public void RevokeFromApproved()
    {
        (_, PolicyRevision revision) = NewBaselineDraft();
        revision.MarkValidated();
        revision.SubmitForReview();
        revision.Approve(DateTimeOffset.UtcNow);
        revision.Revoke();
        Assert.Equal(PolicyRevisionState.Revoked, revision.State);
    }

    [Fact]
    public void ReplaceDocumentOnValidatedReturnsToDraftAndChangesHash()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = NewBaselineDraft();
        Hash256 before = revision.ContentHash;
        revision.MarkValidated();

        using JsonDocument rule = JsonDocument.Parse("""{"id":"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"}""");
        PolicyDocument edited = PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope)
            .WithRules([rule.RootElement.Clone()]);
        revision.ReplaceDocument(edited, parentContextHash: null);

        Assert.Equal(PolicyRevisionState.Draft, revision.State);
        Assert.NotEqual(before.ToString(), revision.ContentHash.ToString());
    }

    [Fact]
    public void CloneApprovedCreatesNewDraftWithSameContentHash()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = NewBaselineDraft();
        revision.MarkValidated();
        revision.SubmitForReview();
        revision.Approve(DateTimeOffset.UtcNow);

        PolicyRevision clone = revision.CloneToDraft(
            policy,
            nextRevisionNumber: 2,
            UserId.New(),
            DateTimeOffset.UtcNow);

        Assert.Equal(PolicyRevisionState.Draft, clone.State);
        Assert.NotEqual(revision.Id, clone.Id);
        Assert.Equal(2u, clone.RevisionNumber);
        Assert.Equal(revision.ContentHash.ToString(), clone.ContentHash.ToString());
        Assert.Null(clone.ApprovedAtUtc);
    }

    [Fact]
    public void CloneNonApprovedIsRejected()
    {
        (Mfc.Domain.Policy.Policy policy, PolicyRevision revision) = NewBaselineDraft();
        Assert.Throws<DomainInvariantException>(() =>
            revision.CloneToDraft(policy, 2, UserId.New(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void InvalidTransitionsThrow()
    {
        (_, PolicyRevision revision) = NewBaselineDraft();
        Assert.Throws<DomainInvariantException>(() => revision.SubmitForReview());
        Assert.Throws<DomainInvariantException>(() => revision.Approve(DateTimeOffset.UtcNow));
        revision.MarkValidated();
        Assert.Throws<DomainInvariantException>(() => revision.Reject());
    }

    private static (Mfc.Domain.Policy.Policy Policy, PolicyRevision Revision) NewBaselineDraft()
    {
        Mfc.Domain.Policy.Policy policy = Mfc.Domain.Policy.Policy.Create(
            NonEmptyName.Create("baseline"),
            PolicyKind.CompanyBaseline,
            PolicyOwnerScope.Company,
            ownerId: null);
        PolicyRevision revision = PolicyRevision.CreateDraft(
            policy,
            revisionNumber: 1,
            PolicyDocument.CreateEmpty(policy.Kind, policy.OwnerScope),
            parentContextHash: null,
            UserId.New(),
            DateTimeOffset.UtcNow);
        return (policy, revision);
    }
}
