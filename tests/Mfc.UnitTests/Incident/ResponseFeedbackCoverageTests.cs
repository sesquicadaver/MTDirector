using Mfc.Domain.Incident;
using Xunit;

namespace Mfc.UnitTests.Incident;

public sealed class ResponseFeedbackCoverageTests
{
    [Fact]
    public void PlannedCodeIsStable()
    {
        Assert.Equal("RESPONSE_PLANNED", ResponseFeedbackEventCodes.Planned);
    }
}
