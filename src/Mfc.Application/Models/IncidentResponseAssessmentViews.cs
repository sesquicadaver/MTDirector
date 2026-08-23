using Mfc.Domain.Endpoint;
using Mfc.Domain.Incident;

namespace Mfc.Application.Models;

public sealed class IncidentResponseAssessmentFindingView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

public sealed class IncidentResponseAssessmentBindingView
{
    public required Guid IncidentId { get; init; }

    public required FlowTupleView CorrelationFlow { get; init; }

    public required ResponseAssessmentView Assessment { get; init; }

    public required IReadOnlyList<IncidentResponseAssessmentFindingView> Findings { get; init; }

    public static IncidentResponseAssessmentBindingView FromBinding(IncidentResponseAssessmentBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return new IncidentResponseAssessmentBindingView
        {
            IncidentId = binding.IncidentId.Value,
            CorrelationFlow = FlowTupleView.FromDomain(binding.CorrelationFlow)!,
            Assessment = ResponseAssessmentView.FromDomain(binding.Assessment),
            Findings = binding.Findings
                .Select(static f => new IncidentResponseAssessmentFindingView
                {
                    Code = f.Code,
                    Message = f.Message,
                    Subject = f.Subject,
                })
                .ToList(),
        };
    }
}
