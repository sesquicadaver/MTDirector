using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W1.5: ListDeviceDriftEvents findings are mapped, not collapsed to a count.</summary>
public sealed class DriftViewModelTests
{
    [Fact]
    public void FromProtoKeepsFindingKindSeverityAndDetail()
    {
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        DriftEvent evt = new()
        {
            Id = DesktopProtoUuid.FromGuid(eventId),
            Outcome = DriftOutcome.CriticalDrift,
            ConfigurationDriftPresent = true,
            BlocksDeployment = true,
            SemanticDiffCanonical = "hash-only leftover",
            Findings =
            {
                new DriftFinding
                {
                    Kind = DriftFindingKind.ManagedRuleChanged,
                    Severity = DriftSeverity.Critical,
                    Detail = "fwc:rule:1 action drop→accept",
                },
                new DriftFinding
                {
                    Kind = DriftFindingKind.CountersChanged,
                    Severity = DriftSeverity.Ignored,
                },
            },
        };

        DriftEventListItem item = DriftEventListItem.FromProto(evt);

        Assert.Equal(eventId, item.Id);
        Assert.Equal("hash-only leftover", item.SemanticDiffCanonical);
        Assert.Equal(2, item.Findings.Count);

        DriftFindingListItem first = item.Findings[0];
        Assert.Equal(nameof(DriftFindingKind.ManagedRuleChanged), first.KindText);
        Assert.Equal(nameof(DriftSeverity.Critical), first.SeverityText);
        Assert.Equal("fwc:rule:1 action drop→accept", first.Detail);
        Assert.True(first.HasDetail);
        Assert.Contains("ManagedRuleChanged", first.SummaryLine, StringComparison.Ordinal);
        Assert.Contains("drop→accept", first.SummaryLine, StringComparison.Ordinal);

        DriftFindingListItem second = item.Findings[1];
        Assert.Equal(nameof(DriftFindingKind.CountersChanged), second.KindText);
        Assert.Equal(nameof(DriftSeverity.Ignored), second.SeverityText);
        Assert.False(second.HasDetail);
        Assert.Equal("Ignored · CountersChanged", second.SummaryLine);
    }
}
