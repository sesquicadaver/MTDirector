using Mfc.Domain.Incident;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Application.Models;

public sealed class ActiveStateIntervalView
{
    public required Guid DeviceId { get; init; }

    public required DateTimeOffset ValidFrom { get; init; }

    public DateTimeOffset? ValidUntil { get; init; }

    public string? PolicyHashHex { get; init; }

    public string? ArtifactHashHex { get; init; }

    public string? ConfigurationHashHex { get; init; }

    public string? TopologyHashHex { get; init; }

    public required string Certainty { get; init; }

    public static ActiveStateIntervalView? FromDomain(ActiveStateInterval? interval) =>
        interval is null
            ? null
            : new ActiveStateIntervalView
            {
                DeviceId = interval.DeviceId.Value,
                ValidFrom = interval.ValidFrom,
                ValidUntil = interval.ValidUntil,
                PolicyHashHex = interval.PolicyHash?.ToString(),
                ArtifactHashHex = interval.ArtifactHash?.ToString(),
                ConfigurationHashHex = interval.ConfigurationHash?.ToString(),
                TopologyHashHex = interval.TopologyHash?.ToString(),
                Certainty = interval.Certainty.ToString(),
            };
}

public sealed class ActiveStateIntervalFindingView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

public sealed class ActiveStateIntervalResultView
{
    public ActiveStateIntervalView? Interval { get; init; }

    public required string Certainty { get; init; }

    public required IReadOnlyList<ActiveStateIntervalFindingView> Findings { get; init; }

    public static ActiveStateIntervalResultView FromResult(ActiveStateIntervalResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new ActiveStateIntervalResultView
        {
            Interval = ActiveStateIntervalView.FromDomain(result.Interval),
            Certainty = result.Certainty.ToString(),
            Findings = result.Findings
                .Select(static f => new ActiveStateIntervalFindingView
                {
                    Code = f.Code,
                    Message = f.Message,
                    Subject = f.Subject,
                })
                .ToArray(),
        };
    }
}
