using Mfc.Application.Common;
using Mfc.Application.Topology;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Topology;
using Mfc.UnitTests.Application.Fakes;
using Mfc.UnitTests.Onboarding;
using Xunit;

namespace Mfc.UnitTests.Topology;

/// <summary>W6-02 Application: loader, Validate use case, CreatePlan gate.</summary>
public sealed class ValidateVrrpPairConsistencyUseCaseTests
{
    [Fact]
    public async Task ValidateReturnsNotFoundForUnknownNode()
    {
        ValidateVrrpPairConsistencyUseCase useCase = CreateUseCase(
            out _,
            out _,
            out _,
            out _);

        ApplicationResult<VrrpPairConsistencyView> result = await useCase.ExecuteAsync(
            new ValidateVrrpPairConsistencyQuery { Actor = "tester", NodeId = Guid.NewGuid() });

        Assert.False(result.IsSuccess);
        Assert.Equal("not_found", result.Error!.Code);
    }

    [Fact]
    public async Task ValidateReportsMissingCapturesOnVrrpNode()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        ValidateVrrpPairConsistencyUseCase useCase = CreateUseCase(
            out FakeNodeStore nodes,
            out FakeDeviceStore devices,
            out _,
            out _);
        await nodes.AddAsync(node);
        await devices.AddAsync(first);
        await devices.AddAsync(second);

        ApplicationResult<VrrpPairConsistencyView> result = await useCase.ExecuteAsync(
            new ValidateVrrpPairConsistencyQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Passed);
        Assert.Contains(
            result.Value.Findings,
            static f => f.Code == VrrpPairConsistencyFinding.MissingCapture);
    }

    [Fact]
    public async Task ValidatePassesWhenMemberCapturesAgree()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        Guid captureA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        Guid captureB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        first.RecordCompletedCapture(captureA);
        second.RecordCompletedCapture(captureB);

        ValidateVrrpPairConsistencyUseCase useCase = CreateUseCase(
            out FakeNodeStore nodes,
            out FakeDeviceStore devices,
            out FakeSnapshotStore snapshots,
            out _);
        await nodes.AddAsync(node);
        await devices.AddAsync(first);
        await devices.AddAsync(second);
        snapshots.SectionsBySnapshot[captureA] = MemberSections("a", priority: "100", role: "Master");
        snapshots.SectionsBySnapshot[captureB] = MemberSections("b", priority: "90", role: "Backup");

        ApplicationResult<VrrpPairConsistencyView> result = await useCase.ExecuteAsync(
            new ValidateVrrpPairConsistencyQuery { Actor = "tester", NodeId = node.Id.Value });

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Passed);
        Assert.Equal(2, result.Value.MemberCount);
        Assert.Equal(2, result.Value.CaptureCount);
    }

    [Fact]
    public async Task PlanGateAllowsIncompleteCapturesForOnboarding()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        FakeDeviceStore devices = new();
        await devices.AddAsync(first);
        await devices.AddAsync(second);
        VrrpPairConsistencyLoader loader = new(devices, new FakeSnapshotStore(), new FakeDeviceHashStateStore());

        ApplicationError? error = await VrrpPairPlanGate.BlockIfFailedAsync(
            loader,
            node,
            allowIncompleteCaptures: true);

        Assert.Null(error);
    }

    [Fact]
    public async Task PlanGateBlocksMissingCapturesForDeploy()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        FakeDeviceStore devices = new();
        await devices.AddAsync(first);
        await devices.AddAsync(second);
        VrrpPairConsistencyLoader loader = new(devices, new FakeSnapshotStore(), new FakeDeviceHashStateStore());

        ApplicationError? error = await VrrpPairPlanGate.BlockIfFailedAsync(
            loader,
            node,
            allowIncompleteCaptures: false);

        Assert.NotNull(error);
        Assert.Equal("conflict", error!.Code);
        Assert.Contains(VrrpPairConsistencyFinding.MissingCapture, error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlanGateSkipsNonVrrpNodes()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        FakeDeviceStore devices = new();
        await devices.AddAsync(device);
        VrrpPairConsistencyLoader loader = new(devices, new FakeSnapshotStore(), new FakeDeviceHashStateStore());

        ApplicationError? error = await VrrpPairPlanGate.BlockIfFailedAsync(loader, node);

        Assert.Null(error);
    }

    [Fact]
    public async Task PlanGateBlocksConfigMismatchEvenWhenIncompleteAllowed()
    {
        Node node = OnboardingTestFactory.VrrpWithMembers(out Device first, out Device second);
        Guid captureA = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        Guid captureB = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        first.RecordCompletedCapture(captureA);
        second.RecordCompletedCapture(captureB);
        FakeDeviceStore devices = new();
        FakeSnapshotStore snapshots = new();
        await devices.AddAsync(first);
        await devices.AddAsync(second);
        snapshots.SectionsBySnapshot[captureA] = MemberSections("a", priority: "100", vip: "10.0.0.1/32");
        snapshots.SectionsBySnapshot[captureB] = MemberSections("b", priority: "90", vip: "10.0.0.2/32");
        VrrpPairConsistencyLoader loader = new(devices, snapshots, new FakeDeviceHashStateStore());

        ApplicationError? error = await VrrpPairPlanGate.BlockIfFailedAsync(
            loader,
            node,
            allowIncompleteCaptures: true);

        Assert.NotNull(error);
        Assert.Contains(VrrpPairConsistencyFinding.ConfigFieldMismatch, error!.Message, StringComparison.Ordinal);
    }

    private static ValidateVrrpPairConsistencyUseCase CreateUseCase(
        out FakeNodeStore nodes,
        out FakeDeviceStore devices,
        out FakeSnapshotStore snapshots,
        out FakeDeviceHashStateStore hashes)
    {
        nodes = new FakeNodeStore();
        devices = new FakeDeviceStore();
        snapshots = new FakeSnapshotStore();
        hashes = new FakeDeviceHashStateStore();
        return new ValidateVrrpPairConsistencyUseCase(
            new FakeAuthorizationBoundary(),
            nodes,
            new VrrpPairConsistencyLoader(devices, snapshots, hashes));
    }

    private static List<CanonicalSection> MemberSections(
        string name,
        string priority,
        string vip = "10.0.0.1/32",
        string role = "Backup")
        =>
        [
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.HaVrrp,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["group"] = "Ipv4/vrid=10/if=ether1",
                        ["name"] = name + "-vrrp",
                        ["interface"] = name == "a" ? "ether1" : "ether2",
                        ["vrid"] = "10",
                        ["family"] = "Ipv4",
                        ["priority"] = priority,
                        ["version"] = "3",
                        ["interval"] = "1s",
                        ["preemption-mode"] = "yes",
                        ["disabled"] = "false",
                        ["sync-connection-tracking"] = "yes",
                        ["connection-tracking-port"] = "3780",
                        ["remote-address"] = "10.255.10.12",
                        ["addresses"] = vip,
                    }),
                ]),
            new CanonicalSection(
                CanonicalDomain.Observations,
                CanonicalSectionIds.HaVrrp,
                ordered: false,
                [
                    new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["group"] = "Ipv4/vrid=10/if=ether1",
                        ["role"] = role,
                    }),
                ]),
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.FirewallIpv4Filter,
                ordered: true,
                [
                    new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["ordinal"] = "0",
                        ["chain"] = "forward",
                        ["action"] = "accept",
                        ["comment"] = "lab",
                    }),
                ]),
        ];
}
