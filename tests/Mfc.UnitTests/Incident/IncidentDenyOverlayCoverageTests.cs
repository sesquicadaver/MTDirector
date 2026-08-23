using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Incident;

/// <summary>Extra branch coverage for M7.4-01 incident deny overlay modules.</summary>
public sealed class IncidentDenyOverlayCoverageTests
{
    [Fact]
    public void MetadataRejectsDuplicateEvidenceRefs()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentDenyOverlayMetadata.Create(
                new IncidentId(Guid.NewGuid()),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddHours(1),
                "reason",
                ["evt:1", "evt:1"]));
        Assert.Contains("unique", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MetadataRejectsEmptyEvidenceRefs()
    {
        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentDenyOverlayMetadata.Create(
                new IncidentId(Guid.NewGuid()),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddHours(1),
                "reason",
                []));
        Assert.Contains("evidence", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GuardRejectsNodeBindingMismatch()
    {
        PolicyDocument document = new(
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            rules:
            [
                PolicyRule.Create(
                    IpAddressFamily.IPv4,
                    PolicyFilterChain.Forward,
                    PolicyPipelineStage.IncidentPreStateDeny,
                    ordinal: 0,
                    TrafficPredicate.Create(),
                    RuleEffectSpec.Create(PolicyRuleEffect.Drop)),
            ],
            incidentDenyOverlayMetadata: IncidentDenyOverlayMetadata.Create(
                new IncidentId(Guid.NewGuid()),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow.AddHours(2),
                "reason",
                ["evt:1"]));

        DomainInvariantException ex = Assert.Throws<DomainInvariantException>(() =>
            IncidentDenyOverlayDocumentGuard.EnsureNodeBinding(document, Guid.NewGuid()));
        Assert.Contains(IncidentDenyOverlayCodes.NodeMismatch, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatStageUsesIncidentPreStateDenyLabel()
    {
        Assert.Equal(
            "INCIDENT_PRE_STATE_DENY",
            PolicyPipelineV1.FormatStage(PolicyPipelineStage.IncidentPreStateDeny));
    }
}
