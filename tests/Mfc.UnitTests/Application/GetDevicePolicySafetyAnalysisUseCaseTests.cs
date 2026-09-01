using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class GetDevicePolicySafetyAnalysisUseCaseTests
{
    [Fact]
    public async Task MissingPrefixesFailsValidationWithoutInventingDefault()
    {
        GetDevicePolicySafetyAnalysisUseCase useCase = CreateUseCase(
            out _,
            out _,
            out _,
            out _,
            out FakeAuthorizationBoundary _);

        ApplicationResult<PolicySafetyAnalysisView> result = await useCase.ExecuteAsync(
            new GetDevicePolicySafetyAnalysisQuery
            {
                Actor = "admin",
                DeviceId = Guid.NewGuid(),
                ControllerSourcePrefixes = [],
            });

        Assert.True(result.IsFailure);
        Assert.Equal("validation", result.Error!.Code);
        Assert.Contains("controller_source_prefixes", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PolicyReadDeniedFails()
    {
        GetDevicePolicySafetyAnalysisUseCase useCase = CreateUseCase(
            out _,
            out _,
            out _,
            out _,
            out FakeAuthorizationBoundary auth);
        auth.DeniedPermissions.Add(ApplicationPermissions.PolicyRead);

        ApplicationResult<PolicySafetyAnalysisView> result = await useCase.ExecuteAsync(
            new GetDevicePolicySafetyAnalysisQuery
            {
                Actor = "admin",
                DeviceId = Guid.NewGuid(),
                ControllerSourcePrefixes = ["192.0.2.0/24"],
            });

        Assert.True(result.IsFailure);
        Assert.Equal("forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task DeviceWithoutCompletedCaptureFailsClosed()
    {
        GetDevicePolicySafetyAnalysisUseCase useCase = CreateUseCase(
            out FakeDeviceStore devices,
            out FakeNodeStore nodes,
            out _,
            out _,
            out _);
        Device device = await SeedDeviceAsync(nodes, devices, captureId: null);

        ApplicationResult<PolicySafetyAnalysisView> result = await useCase.ExecuteAsync(
            new GetDevicePolicySafetyAnalysisQuery
            {
                Actor = "admin",
                DeviceId = device.Id.Value,
                ControllerSourcePrefixes = ["192.0.2.0/24"],
            });

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error!.Code);
        Assert.Contains("no completed capture", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownRevisionFailsNotFound()
    {
        GetDevicePolicySafetyAnalysisUseCase useCase = CreateUseCase(
            out FakeDeviceStore devices,
            out FakeNodeStore nodes,
            out FakeSnapshotStore snapshots,
            out _,
            out _);
        Guid captureId = Guid.NewGuid();
        Device device = await SeedDeviceAsync(nodes, devices, captureId);
        snapshots.SectionsBySnapshot[captureId] = [DisabledApiSslSection()];

        ApplicationResult<PolicySafetyAnalysisView> result = await useCase.ExecuteAsync(
            new GetDevicePolicySafetyAnalysisQuery
            {
                Actor = "admin",
                DeviceId = device.Id.Value,
                RevisionId = Guid.NewGuid(),
                ControllerSourcePrefixes = ["192.0.2.0/24"],
            });

        Assert.True(result.IsFailure);
        Assert.Equal("not_found", result.Error!.Code);
    }

    [Fact]
    public async Task LastCaptureMapsExistingManagementPathBlockersAndHashes()
    {
        GetDevicePolicySafetyAnalysisUseCase useCase = CreateUseCase(
            out FakeDeviceStore devices,
            out FakeNodeStore nodes,
            out FakeSnapshotStore snapshots,
            out _,
            out _);
        Guid captureId = Guid.NewGuid();
        Device device = await SeedDeviceAsync(nodes, devices, captureId);
        snapshots.SectionsBySnapshot[captureId] =
        [
            DisabledApiSslSection(),
            new CanonicalSection(
                CanonicalDomain.Configuration,
                CanonicalSectionIds.FirewallIpv4Filter,
                ordered: true,
                [
                    new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["ordinal"] = "0",
                        ["chain"] = "input",
                        ["action"] = "accept",
                        ["comment"] = "fwc:guard:api-ssl",
                        ["protocol"] = "tcp",
                        ["src-address"] = "192.0.2.0/24",
                        ["dst-address"] = "192.0.2.10",
                        ["dst-port"] = "8729",
                        ["connection-state"] = "new,established",
                    }),
                ]),
        ];

        ApplicationResult<PolicySafetyAnalysisView> result = await useCase.ExecuteAsync(
            new GetDevicePolicySafetyAnalysisQuery
            {
                Actor = "admin",
                DeviceId = device.Id.Value,
                ControllerSourcePrefixes = ["192.0.2.0/24"],
            });

        Assert.True(result.IsSuccess);
        PolicySafetyAnalysisView view = result.Value!;
        Assert.Equal(device.Id.Value, view.DeviceId);
        Assert.Equal(captureId, view.CaptureId);
        Assert.Equal(64, view.ManagementPathContextHashHex.Length);
        Assert.Equal(64, view.FastTrackContextHashHex.Length);
        Assert.True(view.BlocksManagementPath);
        Assert.Contains(view.ManagementPathFindings, f => f.Code == ManagementPathAnalysisCodes.ServiceDisabled);
        Assert.False(view.RequiresAcceptFallback);
        Assert.False(view.AllowsSafeFastTrack);
        Assert.Equal(string.Empty, view.RiskFloor);
    }

    private static GetDevicePolicySafetyAnalysisUseCase CreateUseCase(
        out FakeDeviceStore devices,
        out FakeNodeStore nodes,
        out FakeSnapshotStore snapshots,
        out FakePolicyStore policies,
        out FakeAuthorizationBoundary auth)
    {
        auth = new FakeAuthorizationBoundary();
        devices = new FakeDeviceStore();
        nodes = new FakeNodeStore();
        snapshots = new FakeSnapshotStore();
        policies = new FakePolicyStore();
        return new GetDevicePolicySafetyAnalysisUseCase(auth, devices, nodes, snapshots, policies);
    }

    private static async Task<Device> SeedDeviceAsync(
        FakeNodeStore nodes,
        FakeDeviceStore devices,
        Guid? captureId)
    {
        Node node = Node.Reconstitute(
            new NodeId(Guid.NewGuid()),
            new SiteId(Guid.NewGuid()),
            NonEmptyName.Create("core"),
            NodeKind.Router,
            DeclaredUplinkMode.One,
            NodeStatus.Draft,
            ManagementState.Unmanaged,
            rowVersion: 1);
        await nodes.AddAsync(node);
        Device device = Device.Reconstitute(
            new DeviceId(Guid.NewGuid()),
            node.Id,
            NonEmptyName.Create("chr"),
            ManagementEndpoint.Create("192.0.2.10", 8729),
            DeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1,
            lastCompletedCaptureId: captureId);
        await devices.AddAsync(device);
        return device;
    }

    private static CanonicalSection DisabledApiSslSection()
        => new(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.ManagementIpServices,
            ordered: false,
            [
                new CanonicalRecord(new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["api-ssl.disabled"] = "true",
                    ["api-ssl.port"] = "8729",
                    ["api-ssl.address"] = "10.0.0.0/8",
                }),
            ]);
}
