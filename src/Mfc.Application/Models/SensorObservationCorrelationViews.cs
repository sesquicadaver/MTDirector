using Mfc.Domain.Incident;

namespace Mfc.Application.Models;

public sealed class SensorObservationCorrelationFindingView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

public sealed class SensorObservationCorrelationResultView
{
    public required string Status { get; init; }

    public required IReadOnlyList<SensorObservationCorrelationFindingView> Findings { get; init; }

    public static SensorObservationCorrelationResultView FromResult(SensorObservationCorrelationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new SensorObservationCorrelationResultView
        {
            Status = result.Status.ToString(),
            Findings = result.Findings
                .Select(static f => new SensorObservationCorrelationFindingView
                {
                    Code = f.Code,
                    Message = f.Message,
                    Subject = f.Subject,
                })
                .ToList(),
        };
    }
}
