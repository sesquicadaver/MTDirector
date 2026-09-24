using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Deployment;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Jobs;

/// <summary>
/// AUDIT-DRIFT-01 / F12: live API-SSL read of managed resources → observed resource hash
/// against the last committed sealed artifact.
/// </summary>
public sealed class RouterOsManagedDriftLiveReadPort : IManagedDriftLiveReadPort
{
    public const string DeviceMissingCode = "device_missing";
    public const string ProfileMissingCode = "connection_profile_missing";
    public const string ArtifactMissingCode = "committed_artifact_missing";
    public const string LiveReadFailedCode = "live_managed_state_read_failed";

    private readonly IDeviceStore _devices;
    private readonly IConnectionProfileReadStore _profiles;
    private readonly IRouterOsConnectionMaterializer _materializer;
    private readonly IFilterArtifactStore _filterArtifacts;

    public RouterOsManagedDriftLiveReadPort(
        IDeviceStore devices,
        IConnectionProfileReadStore profiles,
        IRouterOsConnectionMaterializer materializer,
        IFilterArtifactStore filterArtifacts)
    {
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(materializer);
        ArgumentNullException.ThrowIfNull(filterArtifacts);
        _devices = devices;
        _profiles = profiles;
        _materializer = materializer;
        _filterArtifacts = filterArtifacts;
    }

    public async Task<ManagedDriftLiveReadResult> ReadActualManagedResourceAsync(
        DeviceId deviceId,
        Hash256 expectedCommittedArtifactHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedCommittedArtifactHash);
        cancellationToken.ThrowIfCancellationRequested();

        Device? device = await _devices.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null || !device.Enabled)
        {
            return ManagedDriftLiveReadResult.Fail(
                DeviceMissingCode,
                $"Enabled device '{deviceId}' was not found.");
        }

        ConnectionProfileReadModel? profile = await _profiles.GetAsync(device.Id, cancellationToken)
            .ConfigureAwait(false);
        if (profile is null)
        {
            return ManagedDriftLiveReadResult.Fail(
                ProfileMissingCode,
                $"Connection profile for device '{device.Id}' is missing.");
        }

        byte[]? canonical = await _filterArtifacts
            .GetCanonicalBytesByResourceHashAsync(expectedCommittedArtifactHash, cancellationToken)
            .ConfigureAwait(false);
        StoredFilterArtifact? meta = await _filterArtifacts
            .GetByResourceHashAsync(expectedCommittedArtifactHash, cancellationToken)
            .ConfigureAwait(false);
        if (canonical is null || canonical.Length == 0 || meta is null)
        {
            return ManagedDriftLiveReadResult.Fail(
                ArtifactMissingCode,
                $"Committed filter artifact '{expectedCommittedArtifactHash}' is missing from store.");
        }

        RouterOsFilterArtifactReader.ParsedBody body = RouterOsFilterArtifactReader.Read(canonical);
        RouterOsFilterArtifact expected;
        try
        {
            expected = RouterOsFilterArtifact.Create(
                meta.CompilerProfileHash,
                meta.PhysicalSemanticsHash,
                meta.DeviceId,
                body.AddressLists,
                body.Chains,
                body.Anchors,
                body.LayoutVersion);
        }
        catch (DomainInvariantException ex)
        {
            return ManagedDriftLiveReadResult.Fail(ArtifactMissingCode, ex.Message);
        }

        if (!expected.ResourceHash.Equals(expectedCommittedArtifactHash))
        {
            return ManagedDriftLiveReadResult.Fail(
                ArtifactMissingCode,
                "Resealed committed artifact diverges from stored resource hash.");
        }

        RouterOsReadTarget target = new()
        {
            DeviceId = device.Id,
            Endpoint = device.ManagementEndpoint,
            SecretReference = profile.SecretReference,
            TrustMode = profile.TrustMode,
            CaProfileRef = profile.CaProfileRef,
            PinnedSpkiSha256 = profile.PinnedSpkiSha256,
        };

        try
        {
            using RouterOsConnectionMaterial material = await _materializer
                .MaterializeAsync(target, cancellationToken)
                .ConfigureAwait(false);
            using SecretLease password = new(material.Password.Plaintext);
            ApiSslConnectOptions options = RouterOsApiSslConnectOptionsBuilder.Build(material, password);
            await using AuthenticatedRosConnection connection = await AuthenticatedRosConnection
                .ConnectAsync(options, cancellationToken)
                .ConfigureAwait(false);
            await using RouterOsDeploymentSession session = new(
                new RouterOsDeploymentWriteChannel(connection.Session),
                ownedConnection: null);

            ActualManagedState state = await session.ReadManagedStateAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!ManagedResourceHashObservation.TryComputeFromManagedState(
                    expected,
                    state,
                    out Hash256 observed,
                    out string? error))
            {
                return ManagedDriftLiveReadResult.Diverged(
                    error ?? "Live managed resources diverge from committed artifact.");
            }

            return ManagedDriftLiveReadResult.Ok(observed.ToString());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return ManagedDriftLiveReadResult.Fail(LiveReadFailedCode, ex.Message);
        }
    }
}
