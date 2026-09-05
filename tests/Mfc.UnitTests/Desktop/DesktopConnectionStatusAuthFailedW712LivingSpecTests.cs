using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W7-12: non-Connected Desktop status labels omit actor (AuthenticationFailed + all states).</summary>
public sealed class DesktopConnectionStatusAuthFailedW712LivingSpecTests
{
    public static TheoryData<ControllerConnectionState, string> NonConnectedStates => new()
    {
        { ControllerConnectionState.Disconnected, "Disconnected" },
        { ControllerConnectionState.Connecting, "Connecting" },
        { ControllerConnectionState.AuthenticationFailed, "AuthenticationFailed" },
        { ControllerConnectionState.TlsError, "TlsError" },
    };

    [Theory]
    [MemberData(nameof(NonConnectedStates))]
    public void Ac1NonConnectedStatusOmitsActorSuffix(ControllerConnectionState state, string expectedLabel)
    {
        DesktopOptions options = new() { Actor = "operator@lab" };

        string status = DesktopConnectionStatusText.Format(state, options);

        Assert.Equal(expectedLabel, status);
        Assert.DoesNotContain("actor:", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("operator@lab", status, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac1AuthenticationFailedLabelIsLocked()
    {
        DesktopOptions options = new() { Actor = "should-not-appear" };

        Assert.Equal(
            "AuthenticationFailed",
            DesktopConnectionStatusText.Format(ControllerConnectionState.AuthenticationFailed, options));
    }

    [Fact]
    public void Ac1AllControllerConnectionStatesAreCoveredByFormatter()
    {
        DesktopOptions options = new() { Actor = "desktop" };
        foreach (ControllerConnectionState state in Enum.GetValues<ControllerConnectionState>())
        {
            string status = DesktopConnectionStatusText.Format(state, options);
            Assert.False(string.IsNullOrWhiteSpace(status));
            if (state == ControllerConnectionState.Connected)
            {
                Assert.StartsWith("Connected · actor:", status, StringComparison.Ordinal);
            }
            else
            {
                Assert.DoesNotContain("actor:", status, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
