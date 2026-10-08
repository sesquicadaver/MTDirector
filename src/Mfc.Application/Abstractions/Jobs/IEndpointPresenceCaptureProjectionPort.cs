using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Abstractions.Jobs;

/// <summary>Outcome of projecting capture into endpoint presence (M7-PRES-01 / F13).</summary>
public sealed class EndpointPresenceCaptureProjectionResult
{
    public required bool Succeeded { get; init; }

    public string? ErrorCode { get; init; }

    public string? Detail { get; init; }

    public static EndpointPresenceCaptureProjectionResult Ok()
        => new() { Succeeded = true };

    public static EndpointPresenceCaptureProjectionResult Fail(string errorCode, string detail)
        => new()
        {
            Succeeded = false,
            ErrorCode = errorCode,
            Detail = detail,
        };
}

/// <summary>
/// Projects a successful capture into <see cref="Mfc.Domain.Endpoint.EndpointPresenceInterval"/>
/// via <c>OpenEndpointPresenceUseCase</c>. Failed projection must not fail the capture itself.
/// </summary>
public interface IEndpointPresenceCaptureProjectionPort
{
    Task<EndpointPresenceCaptureProjectionResult> ProjectFromCapturePayloadsAsync(
        DeviceId deviceId,
        ReadOnlyMemory<byte> configurationPayload,
        ReadOnlyMemory<byte> observationPayload,
        CancellationToken cancellationToken = default);
}

/// <summary>Fail-closed stub until a capture presence projector is registered.</summary>
public sealed class NotConfiguredEndpointPresenceCaptureProjectionPort : IEndpointPresenceCaptureProjectionPort
{
    public const string NotConfiguredCode = "presence_projection_not_configured";

    public Task<EndpointPresenceCaptureProjectionResult> ProjectFromCapturePayloadsAsync(
        DeviceId deviceId,
        ReadOnlyMemory<byte> configurationPayload,
        ReadOnlyMemory<byte> observationPayload,
        CancellationToken cancellationToken = default)
    {
        _ = deviceId;
        _ = configurationPayload;
        _ = observationPayload;
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(EndpointPresenceCaptureProjectionResult.Fail(
            NotConfiguredCode,
            "Endpoint presence capture projection is not_configured."));
    }
}
