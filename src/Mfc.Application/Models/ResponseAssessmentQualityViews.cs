using Mfc.Domain.Endpoint;

namespace Mfc.Application.Models;

public sealed class ResponseAssessmentQualityFindingView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? Subject { get; init; }
}

public sealed class ResponseAssessmentQualityResultView
{
    public required string VisibilityStatus { get; init; }

    public required int Confidence { get; init; }

    public required IReadOnlyList<ResponseAssessmentQualityFindingView> Findings { get; init; }

    public static ResponseAssessmentQualityResultView FromResult(ResponseAssessmentQualityResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new ResponseAssessmentQualityResultView
        {
            VisibilityStatus = result.VisibilityStatus.ToString(),
            Confidence = result.Confidence,
            Findings = result.Findings
                .Select(static f => new ResponseAssessmentQualityFindingView
                {
                    Code = f.Code,
                    Message = f.Message,
                    Subject = f.Subject,
                })
                .ToList(),
        };
    }
}
