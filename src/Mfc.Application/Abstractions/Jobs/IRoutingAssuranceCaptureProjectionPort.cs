using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;

namespace Mfc.Application.Abstractions.Jobs;

/// <summary>Outcome of projecting capture payloads into routing assurance state (AUDIT-M7-01 / F13).</summary>
public sealed class RoutingAssuranceCaptureProjectionResult
{
    public required bool Succeeded { get; init; }

    public string? ErrorCode { get; init; }

    public string? Detail { get; init; }

    public static RoutingAssuranceCaptureProjectionResult Ok()
        => new() { Succeeded = true };

    public static RoutingAssuranceCaptureProjectionResult Fail(string errorCode, string detail)
        => new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            Detail = detail,
        };
}

/// <summary>
/// Projects last capture configuration/observation into <see cref="RoutingAssuranceState"/>.
/// Failed projection must not fail the capture itself; callers log and continue.
/// </summary>
public interface IRoutingAssuranceCaptureProjectionPort
{
    Task<RoutingAssuranceCaptureProjectionResult> ProjectFromCapturePayloadsAsync(
        DeviceId deviceId,
        ReadOnlyMemory<byte> configurationPayload,
        ReadOnlyMemory<byte> observationPayload,
        CancellationToken cancellationToken = default);
}

/// <summary>Fail-closed stub until a RouterOS/capture projector is registered.</summary>
public sealed class NotConfiguredRoutingAssuranceCaptureProjectionPort : IRoutingAssuranceCaptureProjectionPort
{
    public const string NotConfiguredCode = "routing_projection_not_configured";

    public Task<RoutingAssuranceCaptureProjectionResult> ProjectFromCapturePayloadsAsync(
        DeviceId deviceId,
        ReadOnlyMemory<byte> configurationPayload,
        ReadOnlyMemory<byte> observationPayload,
        CancellationToken cancellationToken = default)
    {
        _ = deviceId;
        _ = configurationPayload;
        _ = observationPayload;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(RoutingAssuranceCaptureProjectionResult.Fail(
            NotConfiguredCode,
            "Routing assurance capture projection is not_configured."));
    }
}
