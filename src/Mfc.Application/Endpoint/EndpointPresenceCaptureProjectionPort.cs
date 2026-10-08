using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Endpoint;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Endpoint;

/// <summary>
/// M7-PRES-01 / F13: production post-capture caller for <see cref="OpenEndpointPresenceUseCase"/>.
/// Opens presence for the device management IP with inventory anchors (site/node/device).
/// Certainty may be Unknown when capture payloads lack ARP/DHCP MAC evidence — still a real production path.
/// </summary>
public sealed class EndpointPresenceCaptureProjectionPort : IEndpointPresenceCaptureProjectionPort
{
    public const string AnalyzerNamespace = "mfc.endpoint-presence.capture.v1";

    public const string DeviceNotFoundCode = "presence_projection_device_not_found";

    public const string NodeNotFoundCode = "presence_projection_node_not_found";

    public const string ManagementHostNotIpCode = "presence_projection_management_host_not_ip";

    public const string OpenFailedCode = "presence_projection_open_failed";

    public const string ProjectionFailedCode = "presence_projection_failed";

    private readonly OpenEndpointPresenceUseCase _open;
    private readonly IDeviceStore _devices;
    private readonly INodeStore _nodes;
    private readonly IEndpointPresenceStore _presence;
    private readonly string _systemActor;

    public EndpointPresenceCaptureProjectionPort(
        OpenEndpointPresenceUseCase open,
        IDeviceStore devices,
        INodeStore nodes,
        IEndpointPresenceStore presence,
        string systemActor)
    {
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(presence);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemActor);
        _open = open;
        _devices = devices;
        _nodes = nodes;
        _presence = presence;
        _systemActor = systemActor.Trim();
    }

    /// <inheritdoc />
    public async Task<EndpointPresenceCaptureProjectionResult> ProjectFromCapturePayloadsAsync(
        DeviceId deviceId,
        ReadOnlyMemory<byte> configurationPayload,
        ReadOnlyMemory<byte> observationPayload,
        CancellationToken cancellationToken = default)
    {
        // Capture payloads reserved for future ARP/DHCP MAC enrichment; management IP is the v1 anchor.
        _ = configurationPayload;
        _ = observationPayload;
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            Device? device = await _devices.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
            if (device is null)
            {
                return EndpointPresenceCaptureProjectionResult.Fail(
                    DeviceNotFoundCode,
                    $"Device '{deviceId.Value:D}' not found for presence projection.");
            }

            Node? node = await _nodes.GetAsync(device.NodeId, cancellationToken).ConfigureAwait(false);
            if (node is null)
            {
                return EndpointPresenceCaptureProjectionResult.Fail(
                    NodeNotFoundCode,
                    $"Node '{device.NodeId.Value:D}' not found for presence projection.");
            }

            HostNameOrIp host = device.ManagementEndpoint.Host;
            if (host.HostKind == HostNameOrIp.Kind.DnsHostName || host.Address is null)
            {
                return EndpointPresenceCaptureProjectionResult.Fail(
                    ManagementHostNotIpCode,
                    "Management host is not an IP address; presence projection skipped.");
            }

            string family = host.HostKind == HostNameOrIp.Kind.IPv6 ? "ipv6" : "ipv4";
            string ip = host.Value;
            EndpointId endpointId = DeterministicEndpointId(device.Id, family, ip);

            EndpointPresenceInterval? active = await _presence
                .GetActiveIntervalAsync(endpointId, cancellationToken)
                .ConfigureAwait(false);
            if (active is not null
                && string.Equals(active.SourceAddress, ip, StringComparison.OrdinalIgnoreCase)
                && active.SiteId.Equals(node.SiteId)
                && active.NodeId.Equals(node.Id)
                && Nullable.Equals(active.DeviceId, device.Id))
            {
                return EndpointPresenceCaptureProjectionResult.Ok();
            }

            ApplicationResult<EndpointPresenceUpsertResultView> opened = await _open
                .ExecuteAsync(
                    new UpsertEndpointPresenceCommand
                    {
                        Actor = _systemActor,
                        EndpointId = endpointId.Value,
                        Query = new EndpointAttributionQuery
                        {
                            Family = family,
                            IpAddress = ip,
                            SiteId = node.SiteId,
                            NodeId = node.Id,
                            DeviceId = device.Id,
                        },
                        Snapshot = new EndpointAttributionSnapshot
                        {
                            SiteId = node.SiteId,
                            NodeId = node.Id,
                            DeviceId = device.Id,
                        },
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (opened.IsFailure)
            {
                return EndpointPresenceCaptureProjectionResult.Fail(
                    OpenFailedCode,
                    opened.Error?.Message ?? "OpenEndpointPresence failed.");
            }

            return EndpointPresenceCaptureProjectionResult.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DomainInvariantException ex)
        {
            return EndpointPresenceCaptureProjectionResult.Fail(ProjectionFailedCode, ex.Message);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return EndpointPresenceCaptureProjectionResult.Fail(
                ProjectionFailedCode,
                "Endpoint presence capture projection failed (sanitized).");
        }
    }

    /// <summary>Stable endpoint id from device + address family + IP (capture presence v1).</summary>
    public static EndpointId DeterministicEndpointId(DeviceId deviceId, string family, string ipAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(family);
        ArgumentException.ThrowIfNullOrWhiteSpace(ipAddress);
        string material =
            $"{AnalyzerNamespace}|{deviceId.Value:D}|{family.Trim().ToLowerInvariant()}|{ipAddress.Trim().ToLowerInvariant()}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(material));
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, 16).CopyTo(guidBytes);
        return new EndpointId(new Guid(guidBytes));
    }
}
