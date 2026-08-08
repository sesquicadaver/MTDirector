using System.Text;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Inventory;
using Mfc.Application.Models;
using Mfc.Application.Snapshots;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class InventoryUseCaseTests
{
    [Fact]
    public async Task CreateSiteAndNodeAndRegisterDevice()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();

        ApplicationResult<SiteView> site = await new CreateSiteUseCase(auth, sites).ExecuteAsync(
            new CreateSiteCommand { Actor = "admin", Code = "EDGE01", Name = "Edge" });
        Assert.True(site.IsSuccess);
        Assert.Equal("EDGE01", site.Value!.Code);

        ApplicationResult<NodeView> node = await new CreateNodeUseCase(auth, sites, nodes).ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "admin",
                SiteId = site.Value.Id,
                Name = "r1",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            });
        Assert.True(node.IsSuccess);

        ApplicationResult<DeviceView> device = await new RegisterDeviceUseCase(auth, nodes, devices).ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "admin",
                NodeId = node.Value!.Id,
                DisplayName = "r1",
                ManagementHost = "10.0.0.1",
                Role = DeviceRole.Router,
            });
        Assert.True(device.IsSuccess);
        Assert.Equal("10.0.0.1", device.Value!.ManagementHost);
        Assert.Null(device.Value.GetType().GetProperty("Password"));
    }

    [Fact]
    public async Task CreateSiteRejectsDuplicateCode()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        CreateSiteUseCase useCase = new(auth, sites);
        Assert.True((await useCase.ExecuteAsync(new CreateSiteCommand
        {
            Actor = "admin",
            Code = "DUP1",
            Name = "One",
        })).IsSuccess);

        ApplicationResult<SiteView> second = await useCase.ExecuteAsync(new CreateSiteCommand
        {
            Actor = "admin",
            Code = "DUP1",
            Name = "Two",
        });
        Assert.False(second.IsSuccess);
        Assert.Equal("conflict", second.Error!.Code);
    }

    [Fact]
    public async Task CreateSiteRejectsInvalidCodeAndBlankName()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        CreateSiteUseCase useCase = new(auth, sites);
        ApplicationResult<SiteView> badCode = await useCase.ExecuteAsync(
            new CreateSiteCommand { Actor = "admin", Code = "bad code!", Name = "X" });
        Assert.True(badCode.IsFailure);
        Assert.Equal("validation", badCode.Error!.Code);

        ApplicationResult<SiteView> blank = await useCase.ExecuteAsync(
            new CreateSiteCommand { Actor = "admin", Code = "OK01", Name = "   " });
        Assert.Equal("validation", blank.Error!.Code);
    }

    [Fact]
    public async Task CreateNodeRejectsMissingSiteDuplicateNameAndInvalidName()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        SiteView site = (await new CreateSiteUseCase(auth, sites).ExecuteAsync(
            new CreateSiteCommand { Actor = "a", Code = "SITE1", Name = "Site" })).Value!;
        CreateNodeUseCase useCase = new(auth, sites, nodes);

        ApplicationResult<NodeView> missingSite = await useCase.ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                SiteId = Guid.NewGuid(),
                Name = "n1",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            });
        Assert.Equal("not_found", missingSite.Error!.Code);

        Assert.True((await useCase.ExecuteAsync(new CreateNodeCommand
        {
            Actor = "a",
            SiteId = site.Id,
            Name = "dup",
            DeclaredKind = NodeKind.Router,
            DeclaredUplinkMode = DeclaredUplinkMode.One,
        })).IsSuccess);

        ApplicationResult<NodeView> duplicate = await useCase.ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                SiteId = site.Id,
                Name = "dup",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            });
        Assert.Equal("conflict", duplicate.Error!.Code);

        ApplicationResult<NodeView> invalid = await useCase.ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                SiteId = site.Id,
                Name = " ",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            });
        Assert.Equal("validation", invalid.Error!.Code);
    }

    [Fact]
    public async Task RegisterDeviceRejectsMissingNodeAndInvalidEndpoint()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        SiteView site = (await new CreateSiteUseCase(auth, sites).ExecuteAsync(
            new CreateSiteCommand { Actor = "a", Code = "SITE2", Name = "Site" })).Value!;
        NodeView node = (await new CreateNodeUseCase(auth, sites, nodes).ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                SiteId = site.Id,
                Name = "n",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            })).Value!;
        RegisterDeviceUseCase useCase = new(auth, nodes, devices);

        ApplicationResult<DeviceView> missing = await useCase.ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                NodeId = Guid.NewGuid(),
                DisplayName = "d",
                ManagementHost = "10.0.0.1",
                Role = DeviceRole.Router,
            });
        Assert.Equal("not_found", missing.Error!.Code);

        ApplicationResult<DeviceView> invalid = await useCase.ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                NodeId = node.Id,
                DisplayName = "d",
                ManagementHost = "not an ip",
                Role = DeviceRole.Router,
            });
        Assert.Equal("validation", invalid.Error!.Code);
    }

    [Fact]
    public async Task UpdateConnectionProfileDelegatesWithoutReturningSecretMaterial()
    {
        FakeAuthorizationBoundary auth = new();
        FakeConnectionProfileService profiles = new();
        ApplicationResult<ConnectionProfileView> result =
            await new UpdateConnectionProfileUseCase(auth, profiles).ExecuteAsync(
                new UpsertConnectionProfileCommand
                {
                    Actor = "admin",
                    DeviceId = Guid.NewGuid(),
                    Username = "ro",
                    PasswordUtf8 = Encoding.UTF8.GetBytes("secret"),
                    TrustMode = CertificateTrustMode.InternalCa,
                    CaProfileRef = "lab-ca",
                });

        Assert.True(result.IsSuccess);
        Assert.Single(profiles.Upserts);
        Assert.DoesNotContain(
            result.Value!.GetType().GetProperties(),
            p => p.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task UpdateConnectionProfileMapsServiceFailures()
    {
        FakeAuthorizationBoundary auth = new();
        FakeConnectionProfileService profiles = new();
        UpdateConnectionProfileUseCase useCase = new(auth, profiles);
        UpsertConnectionProfileCommand command = new()
        {
            Actor = "admin",
            DeviceId = Guid.NewGuid(),
            Username = "ro",
            PasswordUtf8 = Encoding.UTF8.GetBytes("secret"),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "lab-ca",
        };

        profiles.ThrowOnUpsert = new InvalidOperationException("boom");
        ApplicationResult<ConnectionProfileView> failed = await useCase.ExecuteAsync(command);
        Assert.Equal("failed", failed.Error!.Code);

        profiles.ThrowOnUpsert = new ArgumentException("bad arg");
        ApplicationResult<ConnectionProfileView> validation = await useCase.ExecuteAsync(command);
        Assert.Equal("validation", validation.Error!.Code);

        auth.DeniedPermissions.Add(ApplicationPermissions.ConnectionProfileWrite);
        ApplicationResult<ConnectionProfileView> forbidden = await useCase.ExecuteAsync(command);
        Assert.Equal("forbidden", forbidden.Error!.Code);
    }
}

public sealed class SnapshotUseCaseTests
{
    [Fact]
    public async Task DiscoverDeviceIsReadOnlyAndCaptureIsIdempotentByHash()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        FakeDeviceStore devices = new();
        FakeConnectionProfileReadStore profiles = new();
        FakeRouterOsReadPort routerOs = new();
        FakeSnapshotCapturePort capture = new();
        FakeSnapshotStore snapshots = new();

        SiteView site = (await new CreateSiteUseCase(auth, sites).ExecuteAsync(
            new CreateSiteCommand { Actor = "a", Code = "LAB01", Name = "Lab" })).Value!;
        NodeView node = (await new CreateNodeUseCase(auth, sites, nodes).ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                SiteId = site.Id,
                Name = "core",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            })).Value!;
        DeviceView device = (await new RegisterDeviceUseCase(auth, nodes, devices).ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                NodeId = node.Id,
                DisplayName = "core",
                ManagementHost = "192.0.2.1",
                Role = DeviceRole.Router,
            })).Value!;

        profiles.ByDevice[device.Id] = new ConnectionProfileReadModel
        {
            SecretReference = SecretReference.From(Guid.NewGuid()),
            TrustMode = CertificateTrustMode.InternalCa,
            CaProfileRef = "ca",
        };

        ApplicationResult<DeviceDiscoveryView> discovery =
            await new DiscoverDeviceUseCase(auth, devices, profiles, routerOs).ExecuteAsync(
                new DiscoverDeviceCommand { Actor = "a", DeviceId = device.Id });
        Assert.True(discovery.IsSuccess);
        Assert.False(discovery.Value!.RouterOsMutated);
        Assert.False(routerOs.MutatedRouterOs);

        CaptureSnapshotUseCase captureUseCase = new(auth, devices, profiles, capture, snapshots);
        ApplicationResult<SnapshotView> first = await captureUseCase.ExecuteAsync(
            new CaptureSnapshotCommand { Actor = "a", DeviceId = device.Id });
        ApplicationResult<SnapshotView> second = await captureUseCase.ExecuteAsync(
            new CaptureSnapshotCommand { Actor = "a", DeviceId = device.Id });

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value!.Id, second.Value!.Id);
        Assert.Equal(2, capture.CaptureCount);

        ApplicationResult<IReadOnlyList<SnapshotView>> list =
            await new ListSnapshotsUseCase(auth, snapshots).ExecuteAsync(
                new ListSnapshotsQuery { Actor = "a", DeviceId = device.Id });
        Assert.True(list.IsSuccess);
        Assert.Single(list.Value!);

        capture.NextResult = FakeSnapshotCapturePort.CreateResult(Enumerable.Repeat((byte)2, 32).ToArray());
        ApplicationResult<SnapshotView> third = await captureUseCase.ExecuteAsync(
            new CaptureSnapshotCommand { Actor = "a", DeviceId = device.Id });
        Assert.NotEqual(first.Value.Id, third.Value!.Id);

        ApplicationResult<SnapshotDiffView> diff = await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "a",
                LeftSnapshotId = first.Value.Id,
                RightSnapshotId = third.Value.Id,
            });
        Assert.True(diff.IsSuccess);
        Assert.False(diff.Value!.Identical);
        Assert.Contains("snapshot_hash", diff.Value.ChangedFields);

        ApplicationResult<SnapshotView> got = await new GetSnapshotUseCase(auth, snapshots).ExecuteAsync(
            new GetSnapshotQuery { Actor = "a", SnapshotId = first.Value.Id });
        Assert.True(got.IsSuccess);
        Assert.Equal(1, got.Value!.SchemaVersion);

        ApplicationResult<SnapshotDiffView> identical = await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "a",
                LeftSnapshotId = first.Value.Id,
                RightSnapshotId = first.Value.Id,
            });
        Assert.True(identical.Value!.Identical);
        Assert.Empty(identical.Value.ChangedFields);
    }

    [Fact]
    public async Task SnapshotUseCasesSurfaceNotFoundProfileMissingAndAuthFailures()
    {
        FakeAuthorizationBoundary auth = new();
        FakeDeviceStore devices = new();
        FakeConnectionProfileReadStore profiles = new();
        FakeRouterOsReadPort routerOs = new();
        FakeSnapshotCapturePort capture = new();
        FakeSnapshotStore snapshots = new();
        Guid missingId = Guid.NewGuid();

        ApplicationResult<DeviceDiscoveryView> discoverMissing =
            await new DiscoverDeviceUseCase(auth, devices, profiles, routerOs).ExecuteAsync(
                new DiscoverDeviceCommand { Actor = "a", DeviceId = missingId });
        Assert.Equal("not_found", discoverMissing.Error!.Code);

        ApplicationResult<SnapshotView> captureMissing =
            await new CaptureSnapshotUseCase(auth, devices, profiles, capture, snapshots).ExecuteAsync(
                new CaptureSnapshotCommand { Actor = "a", DeviceId = missingId });
        Assert.Equal("not_found", captureMissing.Error!.Code);

        ApplicationResult<SnapshotView> getMissing =
            await new GetSnapshotUseCase(auth, snapshots).ExecuteAsync(
                new GetSnapshotQuery { Actor = "a", SnapshotId = missingId });
        Assert.Equal("not_found", getMissing.Error!.Code);

        ApplicationResult<SnapshotDiffView> compareMissing =
            await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
                new CompareSnapshotsQuery
                {
                    Actor = "a",
                    LeftSnapshotId = missingId,
                    RightSnapshotId = missingId,
                });
        Assert.Equal("not_found", compareMissing.Error!.Code);

        FakeSiteStore sites = new();
        FakeNodeStore nodes = new();
        SiteView site = (await new CreateSiteUseCase(auth, sites).ExecuteAsync(
            new CreateSiteCommand { Actor = "a", Code = "LAB02", Name = "Lab" })).Value!;
        NodeView node = (await new CreateNodeUseCase(auth, sites, nodes).ExecuteAsync(
            new CreateNodeCommand
            {
                Actor = "a",
                SiteId = site.Id,
                Name = "core",
                DeclaredKind = NodeKind.Router,
                DeclaredUplinkMode = DeclaredUplinkMode.One,
            })).Value!;
        DeviceView device = (await new RegisterDeviceUseCase(auth, nodes, devices).ExecuteAsync(
            new RegisterDeviceCommand
            {
                Actor = "a",
                NodeId = node.Id,
                DisplayName = "core",
                ManagementHost = "192.0.2.2",
                Role = DeviceRole.Router,
            })).Value!;

        ApplicationResult<DeviceDiscoveryView> noProfile =
            await new DiscoverDeviceUseCase(auth, devices, profiles, routerOs).ExecuteAsync(
                new DiscoverDeviceCommand { Actor = "a", DeviceId = device.Id });
        Assert.Equal("failed", noProfile.Error!.Code);

        ApplicationResult<SnapshotView> captureNoProfile =
            await new CaptureSnapshotUseCase(auth, devices, profiles, capture, snapshots).ExecuteAsync(
                new CaptureSnapshotCommand { Actor = "a", DeviceId = device.Id });
        Assert.Equal("failed", captureNoProfile.Error!.Code);

        auth.DeniedPermissions.Add(ApplicationPermissions.DiscoveryRead);
        auth.DeniedPermissions.Add(ApplicationPermissions.SnapshotCapture);
        auth.DeniedPermissions.Add(ApplicationPermissions.SnapshotRead);
        auth.DeniedPermissions.Add(ApplicationPermissions.SnapshotCompare);

        Assert.Equal(
            "forbidden",
            (await new DiscoverDeviceUseCase(auth, devices, profiles, routerOs).ExecuteAsync(
                new DiscoverDeviceCommand { Actor = "guest", DeviceId = device.Id })).Error!.Code);
        Assert.Equal(
            "forbidden",
            (await new CaptureSnapshotUseCase(auth, devices, profiles, capture, snapshots).ExecuteAsync(
                new CaptureSnapshotCommand { Actor = "guest", DeviceId = device.Id })).Error!.Code);
        Assert.Equal(
            "forbidden",
            (await new GetSnapshotUseCase(auth, snapshots).ExecuteAsync(
                new GetSnapshotQuery { Actor = "guest", SnapshotId = missingId })).Error!.Code);
        Assert.Equal(
            "forbidden",
            (await new ListSnapshotsUseCase(auth, snapshots).ExecuteAsync(
                new ListSnapshotsQuery { Actor = "guest", DeviceId = device.Id })).Error!.Code);
        Assert.Equal(
            "forbidden",
            (await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
                new CompareSnapshotsQuery
                {
                    Actor = "guest",
                    LeftSnapshotId = missingId,
                    RightSnapshotId = missingId,
                })).Error!.Code);
    }

    [Fact]
    public async Task CompareSnapshotsHandlesNullHashPairs()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSnapshotStore snapshots = new();
        DeviceId deviceId = new(Guid.NewGuid());
        StoredSnapshot left = new()
        {
            Metadata = SnapshotMetadata.CreateFailed(deviceId, DateTimeOffset.UtcNow),
            SchemaVersion = 1,
        };
        StoredSnapshot right = new()
        {
            Metadata = SnapshotMetadata.CreateFailed(deviceId, DateTimeOffset.UtcNow),
            SchemaVersion = 1,
        };
        await snapshots.AddAsync(left);
        await snapshots.AddAsync(right);

        ApplicationResult<SnapshotDiffView> bothNull = await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "a",
                LeftSnapshotId = left.Metadata.Id.Value,
                RightSnapshotId = right.Metadata.Id.Value,
            });
        Assert.True(bothNull.IsSuccess);
        Assert.True(bothNull.Value!.Identical);

        byte[] digest = Enumerable.Repeat((byte)9, 32).ToArray();
        Hash256 hash = Hash256.Create(digest);
        StoredSnapshot completed = new()
        {
            Metadata = SnapshotMetadata.CreateCompleted(
                deviceId,
                ConfigurationHash.FromDigest(hash),
                ObservationHash.FromDigest(hash),
                CapabilityHash.FromDigest(hash),
                SnapshotHash.FromDigest(hash),
                DateTimeOffset.UtcNow),
            SchemaVersion = 1,
        };
        await snapshots.AddAsync(completed);

        ApplicationResult<SnapshotDiffView> mixed = await new CompareSnapshotsUseCase(auth, snapshots).ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "a",
                LeftSnapshotId = left.Metadata.Id.Value,
                RightSnapshotId = completed.Metadata.Id.Value,
            });
        Assert.False(mixed.Value!.Identical);
        Assert.Contains("configuration_hash", mixed.Value.ChangedFields);
        Assert.Contains("observation_hash", mixed.Value.ChangedFields);
        Assert.Contains("capability_hash", mixed.Value.ChangedFields);
        Assert.Contains("snapshot_hash", mixed.Value.ChangedFields);
    }

    [Fact]
    public async Task AuthorizationBoundaryIsHonored()
    {
        FakeAuthorizationBoundary auth = new();
        auth.DeniedPermissions.Add(ApplicationPermissions.InventoryWrite);
        FakeSiteStore sites = new();

        ApplicationResult<SiteView> result = await new CreateSiteUseCase(auth, sites).ExecuteAsync(
            new CreateSiteCommand { Actor = "guest", Code = "NOPE1", Name = "Nope" });
        Assert.False(result.IsSuccess);
        Assert.Equal("forbidden", result.Error!.Code);
    }
}

public sealed class ApplicationResultTests
{
    [Fact]
    public void ApplicationResultsFactoriesAndErrorCodes()
    {
        ApplicationResult<int> ok = ApplicationResults.Ok(42);
        Assert.True(ok.IsSuccess);
        Assert.False(ok.IsFailure);
        Assert.Equal(42, ok.Value);
        Assert.Null(ok.Error);

        ApplicationResult<int> fail = ApplicationResults.Fail(ApplicationError.Unauthorized("x"));
        Assert.True(fail.IsFailure);
        Assert.Equal("unauthorized", fail.Error!.Code);

        Assert.Equal("dependency", ApplicationError.Dependency("d").Code);
        Assert.Equal("forbidden", ApplicationError.Forbidden().Code);
    }
}
