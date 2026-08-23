using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Incident;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.UnitTests.Application.Fakes;
using Xunit;
using DomainPolicy = Mfc.Domain.Policy.Policy;

namespace Mfc.UnitTests.Incident;

/// <summary>Living Spec matrix for Issue Set M7.4-01 AC (INCIDENT_PRE_STATE_DENY / INCIDENT_DENY_OVERLAY).</summary>
public sealed class IncidentDenyOverlayLivingSpecTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid NodeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly IncidentId IncidentId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [Fact]
    public void Ac1PipelineStageFollowsProtectedControlPlaneBeforeMandatoryPreStateDeny()
    {
        Assert.Equal(
            PolicyPipelineStage.IncidentPreStateDeny,
            PolicyPipelineV1.OrderedStages[1]);
        Assert.True(
            PolicyPipelineV1.Ordinal(PolicyPipelineStage.ProtectedControlPlane)
            < PolicyPipelineV1.Ordinal(PolicyPipelineStage.IncidentPreStateDeny));
        Assert.True(
            PolicyPipelineV1.Ordinal(PolicyPipelineStage.IncidentPreStateDeny)
            < PolicyPipelineV1.Ordinal(PolicyPipelineStage.MandatoryPreStateDeny));
    }

    [Fact]
    public void Ac2IncidentStageAllowsDropOnlyForIncidentDenyOverlay()
    {
        Assert.Equal(
            [PolicyRuleEffect.Drop],
            PolicyPipelineV1.AllowedEffects(PolicyPipelineStage.IncidentPreStateDeny));
        PolicyPipelineV1.EnsureOwnerEffectAllowed(
            PolicyPipelineStage.IncidentPreStateDeny,
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            PolicyRuleEffect.Drop);
    }

    [Fact]
    public void Ac3RejectAndAcceptAreForbiddenInIncidentStage()
    {
        Assert.False(PolicyPipelineV1.IsOwnerEffectAllowed(
            PolicyPipelineStage.IncidentPreStateDeny,
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            PolicyRuleEffect.Reject));
        Assert.False(PolicyPipelineV1.IsOwnerEffectAllowed(
            PolicyPipelineStage.IncidentPreStateDeny,
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            PolicyRuleEffect.Accept));
    }

    [Fact]
    public void Ac4OverlayMetadataRequiresIncidentNodeReasonEvidenceAndExpiry()
    {
        IncidentDenyOverlayMetadata metadata = SampleMetadata();
        Assert.Equal(IncidentId, metadata.IncidentId);
        Assert.Equal(NodeId, metadata.NodeId);
        Assert.Equal("malware callback", metadata.Reason);
        Assert.Single(metadata.EvidenceRefs);
    }

    [Fact]
    public void Ac5OverlayDocumentRequiresIncidentPreStateDenyDropRules()
    {
        Assert.Equal(
            IncidentDenyOverlayCodes.ValidDocument,
            IncidentDenyOverlayDocumentGuard.Validate(ValidDocument()));
    }

    [Fact]
    public void Ac6WrongStageFailsValidation()
    {
        PolicyDocument wrongStage = ValidDocument(rules:
        [
            PolicyRule.Reconstitute(
                RuleId.New(),
                IpAddressFamily.IPv4,
                PolicyFilterChain.Forward,
                PolicyPipelineStage.NodeDeny,
                ordinal: 0,
                enabled: true,
                TrafficPredicate.Create(),
                RuleEffectSpec.Create(PolicyRuleEffect.Drop),
                LogSpecification.Disabled,
                exceptionEligible: false,
                description: null),
        ]);
        Assert.Equal(
            IncidentDenyOverlayCodes.StageViolation,
            IncidentDenyOverlayDocumentGuard.Validate(wrongStage));
    }

    [Fact]
    public void Ac7CanonicalRoundTripPreservesOverlayMetadata()
    {
        PolicyDocument document = ValidDocument();
        PolicyDocument roundTrip = PolicyDocumentReader.Read(PolicyCanonicalWriter.Write(document));
        Assert.Equal(document.IncidentDenyOverlayMetadata!.IncidentId, roundTrip.IncidentDenyOverlayMetadata!.IncidentId);
        Assert.Equal(document.IncidentDenyOverlayMetadata.EvidenceRefs, roundTrip.IncidentDenyOverlayMetadata.EvidenceRefs);
    }

    [Fact]
    public void Ac8ManagedLayoutPlacesIncidentRulesAfterProtectedControlPlane()
    {
        Guid incidentRuleId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        Guid protectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        RouterOsFilterArtifact artifact = ManagedChainLayoutBuilder.Build(new ManagedChainLayoutRequest
        {
            CompilerProfileHash = Hash256.ParseHex("1111111111111111111111111111111111111111111111111111111111111111"),
            PhysicalSemanticsHash = Hash256.ParseHex("2222222222222222222222222222222222222222222222222222222222222222"),
            DeviceId = new(Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")),
            Surfaces =
            [
                new ManagedChainSurfacePlan
                {
                    Family = IpAddressFamily.IPv4,
                    BuiltInContext = FilterBuiltInContext.Forward,
                    DefaultDisposition = ChainDefaultDisposition.Drop,
                    ProtectedControlPlane =
                    [
                        FilterRuleArtifact.Create(0, "accept", $"mfc:r:{protectId:D}:0", logicalRuleId: protectId),
                    ],
                    IncidentPreStateDeny =
                    [
                        FilterRuleArtifact.Create(0, "drop", $"mfc:r:{incidentRuleId:D}:0", logicalRuleId: incidentRuleId),
                    ],
                },
            ],
        });

        ChainArtifact root = Assert.Single(artifact.Chains, static c => c.Role == FilterChainArtifactRole.Root);
        Assert.Equal(3, root.Rules.Length);
        Assert.Equal($"mfc:r:{protectId:D}:0", root.Rules[0].Comment);
        Assert.Equal($"mfc:r:{incidentRuleId:D}:0", root.Rules[1].Comment);
        Assert.Equal(ManagedChainLayoutBuilder.TerminalComment, root.Rules[2].Comment);
    }

    [Fact]
    public void Ac9PolicyKindIncidentDenyOverlayRequiresNodeOwner()
    {
        DomainPolicy policy = DomainPolicy.Create(
            NonEmptyName.Create("incident-overlay"),
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            NodeId);
        Assert.Equal(PolicyKind.IncidentDenyOverlay, policy.Kind);
        Assert.Throws<DomainInvariantException>(() =>
            DomainPolicy.Create(
                NonEmptyName.Create("bad"),
                PolicyKind.IncidentDenyOverlay,
                PolicyOwnerScope.Site,
                Guid.NewGuid()));
    }

    [Fact]
    public async Task Ac10UseCaseReturnsViewAndRejectsUnauthorized()
    {
        FakeAuthorizationBoundary auth = new();
        ValidateIncidentDenyOverlayUseCase useCase = new(auth);
        ApplicationResult<IncidentDenyOverlayValidationView> ok =
            await useCase.ExecuteAsync(new ValidateIncidentDenyOverlayCommand
            {
                Actor = "tester",
                Document = ValidDocument(),
                PolicyOwnerNodeId = NodeId,
            });
        Assert.True(ok.IsSuccess);
        Assert.Equal(IncidentDenyOverlayCodes.ValidDocument, ok.Value!.ValidationCode);

        auth.DeniedPermissions.Add(ApplicationPermissions.IncidentOverlayValidate);
        ApplicationResult<IncidentDenyOverlayValidationView> denied =
            await useCase.ExecuteAsync(new ValidateIncidentDenyOverlayCommand
            {
                Actor = "tester",
                Document = ValidDocument(),
            });
        Assert.False(denied.IsSuccess);
    }

    private static IncidentDenyOverlayMetadata SampleMetadata()
        => IncidentDenyOverlayMetadata.Create(
            IncidentId,
            NodeId,
            T0.AddHours(1),
            "malware callback",
            ["evt:abc123"]);

    private static PolicyDocument ValidDocument(IReadOnlyList<PolicyRule>? rules = null)
        => new(
            PolicyKind.IncidentDenyOverlay,
            PolicyOwnerScope.Node,
            rules: rules ??
            [
                IncidentRule(PolicyPipelineStage.IncidentPreStateDeny, PolicyRuleEffect.Drop),
            ],
            incidentDenyOverlayMetadata: SampleMetadata());

    private static PolicyRule IncidentRule(PolicyPipelineStage stage, PolicyRuleEffect effect)
        => PolicyRule.Create(
            IpAddressFamily.IPv4,
            PolicyFilterChain.Forward,
            stage,
            ordinal: 0,
            TrafficPredicate.Create(),
            RuleEffectSpec.Create(effect));
}
