using System.Globalization;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs.FaultInjection;

/// <summary>W7-11: timeout/cancel pending clear uses bounded poll, not fixed short sleep.</summary>
public sealed class SessionTimeoutPendingClearW711LivingSpecTests
{
    [Fact]
    public async Task Ac1CommandTimeoutWaitsUntilPendingClearedWithoutFixedSleepOnly()
    {
        await using FaultInjectionSessionHarness harness = await FaultInjectionSessionHarness.StartAsync(
            async (request, respond) =>
            {
                if (request.Command == "/cancel")
                {
                    await respond.ReplyAsync("!done", request.Tag);
                    return;
                }

                await Task.Delay(Timeout.Infinite);
            },
            new RosSessionOptions
            {
                DefaultCommandTimeout = TimeSpan.FromMilliseconds(80),
                CancelGracePeriod = TimeSpan.FromMilliseconds(40),
            });

        RosCommandResult result = await harness.Session.ExecuteAsync("/system/identity/print")
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RosCommandLifecycle.TimedOut, result.Lifecycle);
        Assert.Equal("API_COMMAND_TIMEOUT", result.Error!.Code);

        await WaitUntilPendingClearedAsync(harness.Session);
    }

    [Fact]
    public void Ac1MatrixTestUsesBoundedPendingPollHelper()
    {
        string root = FindRepoRoot();
        string source = File.ReadAllText(
            Path.Combine(root, "tests/Mfc.UnitTests/RouterOs/FaultInjection/SessionFaultInjectionMatrixTests.cs"));

        Assert.Contains("WaitUntilPendingClearedAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "await Task.Delay(50);\n        Assert.Equal(0, harness.Session.PendingCount);",
            source,
            StringComparison.Ordinal);
        Assert.Contains("CommandTimeoutYieldsApiCommandTimeoutAndClearsPending", source, StringComparison.Ordinal);
    }

    private static async Task WaitUntilPendingClearedAsync(RosSession session)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(2));
        while (session.PendingCount != 0)
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }

        Assert.Equal(0, session.PendingCount);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
