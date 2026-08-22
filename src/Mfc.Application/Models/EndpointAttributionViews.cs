using Mfc.Domain.Endpoint;

namespace Mfc.Application.Models;

/// <summary>Application view of endpoint attribution resolver output (M7.2-01).</summary>
public sealed class EndpointAttributionView
{
    public required string Certainty { get; init; }

    public required IReadOnlyList<EndpointAttributionHopView> Hops { get; init; }

    public required IReadOnlyList<EndpointAttributionFindingView> Findings { get; init; }

    public static EndpointAttributionView FromResult(EndpointAttributionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new EndpointAttributionView
        {
            Certainty = result.Certainty.ToString(),
            Hops = result.Chain.Hops
                .Select(static h => new EndpointAttributionHopView
                {
                    Kind = h.Kind.ToString(),
                    Value = h.Value,
                    Detail = h.Detail,
                })
                .ToArray(),
            Findings = result.Findings
                .Select(static f => new EndpointAttributionFindingView
                {
                    Code = f.Code,
                    Message = f.Message,
                    Subject = f.Subject,
                })
                .ToArray(),
        };
    }
}

public sealed class EndpointAttributionHopView
{
    public required string Kind { get; init; }

    public required string Value { get; init; }

    public string? Detail { get; init; }
}

public sealed class EndpointAttributionFindingView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}
