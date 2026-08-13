using System.Text;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Snapshots;
using Mfc.Domain.Canonicalization;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Diff;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Snapshots;
using Mfc.UnitTests.Application.Fakes;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class SnapshotSectionAndRawPayloadUseCaseTests
{
    private static CanonicalRecord Record(params (string Key, string Value)[] props)
    {
        Dictionary<string, string> map = new(StringComparer.Ordinal);
        foreach ((string key, string value) in props)
        {
            map[key] = value;
        }

        return new CanonicalRecord(map);
    }

    private static async Task<Guid> SeedSnapshotAsync(
        FakeSnapshotStore snapshots,
        IReadOnlyList<CanonicalSection> sections,
        Hash256? rawPayloadHash = null)
    {
        DeviceId deviceId = new(Guid.NewGuid());
        StoredSnapshot snapshot = new()
        {
            Metadata = SnapshotMetadata.CreateCompleted(
                deviceId,
                ConfigurationHash.FromDigest(Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray())),
                ObservationHash.FromDigest(Hash256.Create(Enumerable.Repeat((byte)2, 32).ToArray())),
                CapabilityHash.FromDigest(Hash256.Create(Enumerable.Repeat((byte)3, 32).ToArray())),
                SnapshotHash.FromDigest(Hash256.Create(Enumerable.Repeat((byte)4, 32).ToArray())),
                DateTimeOffset.UtcNow),
            SchemaVersion = 1,
            RawPayloadHash = rawPayloadHash,
        };
        await snapshots.AddAsync(snapshot);
        snapshots.SectionsBySnapshot[snapshot.Metadata.Id.Value] = sections.ToList();
        return snapshot.Metadata.Id.Value;
    }

    [Fact]
    public async Task GetSnapshotSectionReturnsPagedRecordsAndMapsDomains()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSnapshotStore snapshots = new();
        List<CanonicalRecord> filterRecords =
        [
            Record(("chain", "forward"), ("action", "accept")),
            Record(("chain", "input"), ("action", "drop")),
            Record(("chain", "forward"), ("action", "reject")),
        ];
        Guid captureId = await SeedSnapshotAsync(
            snapshots,
            [
                new CanonicalSection(
                    CanonicalDomain.Configuration,
                    CanonicalSectionIds.FirewallIpv4Filter,
                    ordered: true,
                    filterRecords),
                new CanonicalSection(
                    CanonicalDomain.Observations,
                    CanonicalSectionIds.NetworkInterfaces,
                    ordered: false,
                    [Record(("name", "ether1"), ("running", "true"))]),
                new CanonicalSection(
                    CanonicalDomain.Configuration,
                    "capability.profile",
                    ordered: false,
                    [Record(("model", "CHR"), ("version", "7"))]),
                new CanonicalSection(
                    CanonicalDomain.Configuration,
                    CanonicalSectionIds.CompatibilityFindings,
                    ordered: false,
                    [Record(("code", "W001"), ("severity", "warn"))]),
            ]);

        GetSnapshotSectionUseCase useCase = new(auth, snapshots);

        ApplicationResult<SnapshotSectionPageView> orderedPage = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = captureId,
                SectionId = CanonicalSectionIds.FirewallIpv4Filter,
                Limit = 2,
            });
        Assert.True(orderedPage.IsSuccess);
        Assert.True(orderedPage.Value!.Ordered);
        Assert.Equal(2, orderedPage.Value.Records.Count);
        Assert.NotNull(orderedPage.Value.Records[0].Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(orderedPage.Value.NextCursor));

        ApplicationResult<SnapshotSectionPageView> page2 = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = captureId,
                SectionId = $"  {CanonicalSectionIds.FirewallIpv4Filter}  ",
                Limit = 2,
                Cursor = orderedPage.Value.NextCursor,
            });
        Assert.True(page2.IsSuccess);
        Assert.Single(page2.Value!.Records);
        Assert.Null(page2.Value.NextCursor);

        ApplicationResult<SnapshotSectionPageView> observation = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = captureId,
                SectionId = CanonicalSectionIds.NetworkInterfaces,
                Domain = DiffDomain.Observation,
            });
        Assert.True(observation.IsSuccess);
        Assert.Single(observation.Value!.Records);
        Assert.Equal("ether1", observation.Value.Records[0].Observations["name"]);
        Assert.Empty(observation.Value.Records[0].Configuration);

        ApplicationResult<SnapshotSectionPageView> capability = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = captureId,
                SectionId = "capability.profile",
            });
        Assert.True(capability.IsSuccess);
        Assert.Empty(capability.Value!.Records[0].Configuration);
        Assert.Empty(capability.Value!.Records[0].Observations);

        ApplicationResult<SnapshotSectionPageView> compatibility = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = captureId,
                SectionId = CanonicalSectionIds.CompatibilityFindings,
            });
        Assert.True(compatibility.IsSuccess);
        Assert.Empty(compatibility.Value!.Records[0].Configuration);
        Assert.Empty(compatibility.Value!.Records[0].Observations);
    }

    [Fact]
    public async Task GetSnapshotSectionSurfacesAuthValidationAndNotFoundErrors()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSnapshotStore snapshots = new();
        GetSnapshotSectionUseCase useCase = new(auth, snapshots);
        Guid captureId = await SeedSnapshotAsync(
            snapshots,
            [
                new CanonicalSection(
                    CanonicalDomain.Configuration,
                    CanonicalSectionIds.SystemIdentity,
                    ordered: false,
                    [Record(("name", "gw"))]),
            ]);

        auth.DeniedPermissions.Add(ApplicationPermissions.SnapshotRead);
        ApplicationResult<SnapshotSectionPageView> forbidden = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "guest",
                CaptureId = captureId,
                SectionId = CanonicalSectionIds.SystemIdentity,
            });
        Assert.Equal("forbidden", forbidden.Error!.Code);

        auth.DeniedPermissions.Clear();
        ApplicationResult<SnapshotSectionPageView> blankSection = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = captureId,
                SectionId = "   ",
            });
        Assert.Equal("failed", blankSection.Error!.Code);

        ApplicationResult<SnapshotSectionPageView> missingCapture = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = Guid.NewGuid(),
                SectionId = CanonicalSectionIds.SystemIdentity,
            });
        Assert.Equal("not_found", missingCapture.Error!.Code);

        ApplicationResult<SnapshotSectionPageView> missingSection = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = captureId,
                SectionId = CanonicalSectionIds.RoutingRules,
            });
        Assert.Equal("not_found", missingSection.Error!.Code);

        ApplicationResult<SnapshotSectionPageView> badCursor = await useCase.ExecuteAsync(
            new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = captureId,
                SectionId = CanonicalSectionIds.SystemIdentity,
                Cursor = "not-a-number",
            });
        Assert.Equal("failed", badCursor.Error!.Code);
    }

    [Fact]
    public async Task GetRawSnapshotPayloadRequiresBothPermissionsAndStoredPayload()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSnapshotStore snapshots = new();
        Hash256 storedHash = snapshots.RegisterPayload(Encoding.UTF8.GetBytes("{\"sanitized\":true}"));
        Guid storedCaptureId = await SeedSnapshotAsync(snapshots, [], rawPayloadHash: storedHash);

        Hash256 orphanHash = Hash256.Create(Enumerable.Repeat((byte)7, 32).ToArray());
        Guid orphanCaptureId = await SeedSnapshotAsync(snapshots, [], rawPayloadHash: orphanHash);

        StoredSnapshot noRawHash = new()
        {
            Metadata = SnapshotMetadata.CreateCompleted(
                new DeviceId(Guid.NewGuid()),
                ConfigurationHash.FromDigest(Hash256.Create(Enumerable.Repeat((byte)1, 32).ToArray())),
                ObservationHash.FromDigest(Hash256.Create(Enumerable.Repeat((byte)2, 32).ToArray())),
                CapabilityHash.FromDigest(Hash256.Create(Enumerable.Repeat((byte)3, 32).ToArray())),
                SnapshotHash.FromDigest(Hash256.Create(Enumerable.Repeat((byte)4, 32).ToArray())),
                DateTimeOffset.UtcNow),
            SchemaVersion = 1,
            RawPayloadHash = null,
        };
        await snapshots.AddAsync(noRawHash);

        GetRawSnapshotPayloadUseCase useCase = new(auth, snapshots);

        ApplicationResult<StoredSnapshotPayload> ok = await useCase.ExecuteAsync(
            new GetRawSnapshotPayloadQuery
            {
                Actor = "admin",
                SnapshotId = storedCaptureId,
            });
        Assert.True(ok.IsSuccess);
        Assert.Equal(SnapshotPayloadKind.RawSanitized, ok.Value!.Kind);
        Assert.True(ok.Value.UncompressedBytes.Length > 0);

        auth.DeniedPermissions.Add(ApplicationPermissions.SnapshotRead);
        ApplicationResult<StoredSnapshotPayload> missingRead = await useCase.ExecuteAsync(
            new GetRawSnapshotPayloadQuery { Actor = "guest", SnapshotId = storedCaptureId });
        Assert.Equal("forbidden", missingRead.Error!.Code);

        auth.DeniedPermissions.Clear();
        auth.DeniedPermissions.Add(ApplicationPermissions.SnapshotRawRead);
        ApplicationResult<StoredSnapshotPayload> missingRaw = await useCase.ExecuteAsync(
            new GetRawSnapshotPayloadQuery { Actor = "guest", SnapshotId = storedCaptureId });
        Assert.Equal("forbidden", missingRaw.Error!.Code);

        auth.DeniedPermissions.Clear();
        ApplicationResult<StoredSnapshotPayload> missingSnapshot = await useCase.ExecuteAsync(
            new GetRawSnapshotPayloadQuery { Actor = "admin", SnapshotId = Guid.NewGuid() });
        Assert.Equal("not_found", missingSnapshot.Error!.Code);

        ApplicationResult<StoredSnapshotPayload> noRaw = await useCase.ExecuteAsync(
            new GetRawSnapshotPayloadQuery
            {
                Actor = "admin",
                SnapshotId = noRawHash.Metadata.Id.Value,
            });
        Assert.Equal("failed", noRaw.Error!.Code);

        ApplicationResult<StoredSnapshotPayload> missingPayload = await useCase.ExecuteAsync(
            new GetRawSnapshotPayloadQuery { Actor = "admin", SnapshotId = orphanCaptureId });
        Assert.Equal("not_found", missingPayload.Error!.Code);
    }

    [Fact]
    public async Task CompareSnapshotsUsesSemanticDiffAndRejectsMixedDevices()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSnapshotStore snapshots = new();
        DeviceId deviceA = new(Guid.NewGuid());
        DeviceId deviceB = new(Guid.NewGuid());
        Hash256 leftDigest = Hash256.Create(Enumerable.Repeat((byte)10, 32).ToArray());
        Hash256 rightDigest = Hash256.Create(Enumerable.Repeat((byte)11, 32).ToArray());

        StoredSnapshot left = new()
        {
            Metadata = SnapshotMetadata.CreateCompleted(
                deviceA,
                ConfigurationHash.FromDigest(leftDigest),
                ObservationHash.FromDigest(leftDigest),
                CapabilityHash.FromDigest(leftDigest),
                SnapshotHash.FromDigest(leftDigest),
                DateTimeOffset.UtcNow),
            SchemaVersion = 1,
        };
        StoredSnapshot rightSameDevice = new()
        {
            Metadata = SnapshotMetadata.CreateCompleted(
                deviceA,
                ConfigurationHash.FromDigest(rightDigest),
                ObservationHash.FromDigest(rightDigest),
                CapabilityHash.FromDigest(rightDigest),
                SnapshotHash.FromDigest(rightDigest),
                DateTimeOffset.UtcNow),
            SchemaVersion = 1,
        };
        StoredSnapshot rightOtherDevice = new()
        {
            Metadata = SnapshotMetadata.CreateCompleted(
                deviceB,
                ConfigurationHash.FromDigest(rightDigest),
                ObservationHash.FromDigest(rightDigest),
                CapabilityHash.FromDigest(rightDigest),
                SnapshotHash.FromDigest(rightDigest),
                DateTimeOffset.UtcNow),
            SchemaVersion = 1,
        };
        await snapshots.AddAsync(left);
        await snapshots.AddAsync(rightSameDevice);
        await snapshots.AddAsync(rightOtherDevice);

        CanonicalSection baseSec = new(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.NetworkInterfaceLists,
            ordered: false,
            [Record(("list", "WAN"), ("members", "ether1,ether2"))]);
        CanonicalSection targetSec = new(
            CanonicalDomain.Configuration,
            CanonicalSectionIds.NetworkInterfaceLists,
            ordered: false,
            [Record(("list", "WAN"), ("members", "ether2,ether5"))]);
        snapshots.SectionsBySnapshot[left.Metadata.Id.Value] = [baseSec];
        snapshots.SectionsBySnapshot[rightSameDevice.Metadata.Id.Value] = [targetSec];

        CompareSnapshotsUseCase compare = new(auth, snapshots);
        ApplicationResult<SnapshotDiffView> mixed = await compare.ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "admin",
                LeftSnapshotId = left.Metadata.Id.Value,
                RightSnapshotId = rightOtherDevice.Metadata.Id.Value,
            });
        Assert.Equal("snapshots_from_different_devices", mixed.Error!.Code);

        ApplicationResult<SnapshotDiffView> semantic = await compare.ExecuteAsync(
            new CompareSnapshotsQuery
            {
                Actor = "admin",
                LeftSnapshotId = left.Metadata.Id.Value,
                RightSnapshotId = rightSameDevice.Metadata.Id.Value,
            });
        Assert.True(semantic.IsSuccess);
        Assert.False(semantic.Value!.Identical);
        Assert.NotEmpty(semantic.Value.Entries);
        Assert.NotEmpty(semantic.Value.Entries[0].FieldChanges);
        Assert.NotEmpty(semantic.Value.ChangedFields);
    }

    [Fact]
    public async Task ListSnapshotsAndSectionQueryClampNonPositiveLimits()
    {
        FakeAuthorizationBoundary auth = new();
        FakeSnapshotStore snapshots = new();
        DeviceId deviceId = new(Guid.NewGuid());
        for (int i = 0; i < 3; i++)
        {
            Hash256 digest = Hash256.Create(Enumerable.Repeat((byte)(20 + i), 32).ToArray());
            await snapshots.AddAsync(new StoredSnapshot
            {
                Metadata = SnapshotMetadata.CreateCompleted(
                    deviceId,
                    ConfigurationHash.FromDigest(digest),
                    ObservationHash.FromDigest(digest),
                    CapabilityHash.FromDigest(digest),
                    SnapshotHash.FromDigest(digest),
                    DateTimeOffset.UtcNow.AddMinutes(-i)),
                SchemaVersion = 1,
            });
        }

        ApplicationResult<SnapshotListPageView> listed = await new ListSnapshotsUseCase(auth, snapshots).ExecuteAsync(
            new ListSnapshotsQuery { Actor = "admin", DeviceId = deviceId.Value, Limit = 0 });
        Assert.True(listed.IsSuccess);
        Assert.Equal(3, listed.Value!.Items.Count);

        Guid captureId = await SeedSnapshotAsync(
            snapshots,
            [
                new CanonicalSection(
                    CanonicalDomain.Configuration,
                    CanonicalSectionIds.SystemIdentity,
                    ordered: false,
                    [Record(("name", "gw"))]),
            ]);
        ApplicationResult<SnapshotSectionPageView> section = await new GetSnapshotSectionUseCase(auth, snapshots)
            .ExecuteAsync(new GetSnapshotSectionQuery
            {
                Actor = "admin",
                CaptureId = captureId,
                SectionId = CanonicalSectionIds.SystemIdentity,
                Limit = 0,
            });
        Assert.True(section.IsSuccess);
        Assert.Single(section.Value!.Records);
    }
}
