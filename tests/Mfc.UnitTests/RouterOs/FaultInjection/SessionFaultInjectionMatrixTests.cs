using System.Globalization;
using Mfc.RouterOs.Commands;
using Mfc.RouterOs.Protocol;
using Mfc.RouterOs.Session;
using Xunit;

namespace Mfc.UnitTests.RouterOs.FaultInjection;

/// <summary>M1-33 RosSession /cancel / timeout / trap / fatal / TLS-close-equivalent faults.</summary>
public sealed class SessionFaultInjectionMatrixTests
{
    [Fact]
    public async Task InterleavedTaggedRepliesCompleteWithZeroPending()
    {
        await using FaultInjectionSessionHarness harness = await FaultInjectionSessionHarness.StartAsync(
            async (request, respond) =>
            {
                await Task.Delay(Random.Shared.Next(0, 15));
                await respond.ReplyAsync("!re", request.Tag, [("n", request.Tag.ToString(CultureInfo.InvariantCulture))]);
                await respond.ReplyAsync("!done", request.Tag);
            });

        Task<RosCommandResult>[] tasks = Enumerable.Range(0, 8)
            .Select(i => harness.Session.ExecuteAsync($"/cmd/{i}"))
            .ToArray();
        RosCommandResult[] results = await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.All(results, r => Assert.Equal(RosCommandLifecycle.Completed, r.Lifecycle));
        Assert.Equal(0, harness.Session.PendingCount);
    }

    [Fact]
    public async Task TrapYieldsRosTrapCodeAndClearsPending()
    {
        await using FaultInjectionSessionHarness harness = await FaultInjectionSessionHarness.StartAsync(
            async (request, respond) =>
            {
                await respond.ReplyAsync("!trap", request.Tag, [("category", "2"), ("message", "denied")]);
                await respond.ReplyAsync("!done", request.Tag);
            });

        RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
            harness.Session,
            RosReadCommandId.SystemIdentity);
        Assert.False(result.IsSuccess);
        Assert.Equal(RosReadCommandExecutor.TrapErrorCode, result.Error!.Code);
        Assert.Equal(0, harness.Session.PendingCount);
        Assert.False(harness.Session.IsFaulted);
    }

    [Fact]
    public async Task FatalYieldsApiFatalAndFaultsSession()
    {
        await using FaultInjectionSessionHarness harness = await FaultInjectionSessionHarness.StartAsync(
            async (_, respond) =>
            {
                await respond.ReplyAsync("!fatal", tag: 1);
            });

        RosReadCommandResult result = await RosReadCommandExecutor.ExecuteAsync(
            harness.Session,
            RosReadCommandId.SystemIdentity);
        Assert.True(result.SessionInvalidated);
        Assert.True(harness.Session.IsFaulted);
        Assert.Equal(RosReadCommandExecutor.FatalErrorCode, result.Error!.Code);
        Assert.Equal(0, harness.Session.PendingCount);
    }

    [Fact]
    public async Task CommandTimeoutYieldsApiCommandTimeoutAndClearsPending()
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

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task CancelCommandIsDeterministicAcrossRepeats(int iteration)
    {
        _ = iteration;
        TaskCompletionSource<ulong> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<ulong> sawCancel = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await using FaultInjectionSessionHarness harness = await FaultInjectionSessionHarness.StartAsync(
            async (request, respond) =>
            {
                if (request.Command == "/cancel")
                {
                    foreach ((string Name, string Value) attr in request.Attributes)
                    {
                        if (attr.Name == "tag"
                            && ulong.TryParse(attr.Value, CultureInfo.InvariantCulture, out ulong target))
                        {
                            sawCancel.TrySetResult(target);
                            break;
                        }
                    }

                    await respond.ReplyAsync("!done", request.Tag);
                    return;
                }

                started.TrySetResult(request.Tag);
                ulong cancelled = await sawCancel.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.Equal(request.Tag, cancelled);
                await respond.ReplyAsync("!trap", request.Tag, [("category", "2")]);
                await respond.ReplyAsync("!done", request.Tag);
            });

        Task<RosCommandResult> execute = harness.Session.ExecuteAsync("/interface/print");
        ulong tag = await started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await harness.Session.CancelCommandAsync(tag);
        RosCommandResult result = await execute.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RosCommandLifecycle.Completed, result.Lifecycle);
        Assert.NotEmpty(result.Traps);
        Assert.Equal(0, harness.Session.PendingCount);
    }

    [Fact]
    public async Task PeerCloseMidCommandYieldsDefinedLifecycleWithoutHang()
    {
        // TLS close / socket reset equivalent: peer completes pipes mid-command.
        await using FaultInjectionSessionHarness harness = await FaultInjectionSessionHarness.StartAsync(
            async (_, _) => await Task.Delay(Timeout.Infinite));

        Task<RosCommandResult> execute = harness.Session.ExecuteAsync(
            "/system/identity/print",
            timeout: TimeSpan.FromSeconds(5));
        await harness.ClosePeerAsync();
        RosCommandResult result = await execute.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(
            result.Lifecycle is RosCommandLifecycle.Faulted
                or RosCommandLifecycle.Cancelled
                or RosCommandLifecycle.TimedOut);
        Assert.NotNull(result.Error);
        Assert.Equal(0, harness.Session.PendingCount);
    }

    [Fact]
    public async Task ControllerCancellationPropagatesAndClearsPending()
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
            new RosSessionOptions { CancelGracePeriod = TimeSpan.FromMilliseconds(40) });

        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(40));
        RosCommandResult result = await harness.Session.ExecuteAsync(
                "/system/identity/print",
                cancellationToken: cts.Token)
            .WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(RosCommandLifecycle.Cancelled, result.Lifecycle);
        Assert.Equal("API_COMMAND_CANCELLED", result.Error!.Code);
        await WaitUntilPendingClearedAsync(harness.Session);
    }

    /// <summary>W7-11: poll until cancel-grace clears pending (no fixed short sleep).</summary>
    private static async Task WaitUntilPendingClearedAsync(
        RosSession session,
        TimeSpan? timeout = null)
    {
        TimeSpan limit = timeout ?? TimeSpan.FromSeconds(2);
        using CancellationTokenSource cts = new(limit);
        while (session.PendingCount != 0)
        {
            cts.Token.ThrowIfCancellationRequested();
            await Task.Delay(10, cts.Token);
        }

        Assert.Equal(0, session.PendingCount);
    }
}
