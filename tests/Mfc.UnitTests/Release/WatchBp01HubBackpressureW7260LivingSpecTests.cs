using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Xunit;
using DomainDeploymentState = Mfc.Domain.Deployment.DeploymentOperationState;
using DomainOnboardingState = Mfc.Domain.Onboarding.OnboardingOperationState;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-260 / WATCH-BP-01: ProgressHubs use bounded subscriber channels with disconnect-on-full
/// (fail-closed) and a live retained `_history` cap; AUDIT-INT-01 prune and OWN-01 ACL remain.
/// </summary>
public sealed class WatchBp01HubBackpressureW7260LivingSpecTests
{
    [Fact]
    public void Ac1HubsUseBoundedChannelsDisconnectOnFullAndHistoryCap()
    {
        string root = RepoRoot();
        string captureHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/CaptureProgressHub.cs"));
        string deploymentHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/DeploymentProgressHub.cs"));
        string onboardingHub = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/OnboardingProgressHub.cs"));

        foreach (string hub in new[] { captureHub, deploymentHub, onboardingHub })
        {
            Assert.Contains("Channel.CreateBounded", hub, StringComparison.Ordinal);
            Assert.DoesNotContain("Channel.CreateUnbounded", hub, StringComparison.Ordinal);
            Assert.Contains("BoundedChannelFullMode.Wait", hub, StringComparison.Ordinal);
            Assert.Contains("SubscriberChannelCapacity = 64", hub, StringComparison.Ordinal);
            Assert.Contains("MaxRetainedHistoryEvents = 256", hub, StringComparison.Ordinal);
            Assert.Contains("while (_history.Count > MaxRetainedHistoryEvents)", hub, StringComparison.Ordinal);
            Assert.Contains("if (!channel.Writer.TryWrite(progress))", hub, StringComparison.Ordinal);
            Assert.Contains("disconnect slow subscriber", hub, StringComparison.Ordinal);
            Assert.Contains("TryGetOwnerActor", hub, StringComparison.Ordinal);
        }

        Assert.Equal(64, CaptureProgressHub.SubscriberChannelCapacity);
        Assert.Equal(256, CaptureProgressHub.MaxRetainedHistoryEvents);
        Assert.Equal(CaptureProgressHub.SubscriberChannelCapacity, DeploymentProgressHub.SubscriberChannelCapacity);
        Assert.Equal(CaptureProgressHub.MaxRetainedHistoryEvents, DeploymentProgressHub.MaxRetainedHistoryEvents);
        Assert.Equal(CaptureProgressHub.SubscriberChannelCapacity, OnboardingProgressHub.SubscriberChannelCapacity);
        Assert.Equal(CaptureProgressHub.MaxRetainedHistoryEvents, OnboardingProgressHub.MaxRetainedHistoryEvents);
    }

    [Fact]
    public async Task Ac2LiveHistoryCapAppliesWhileOperationRetained()
    {
        CaptureProgressHub capture = new();
        Guid captureOp = capture.Begin(Guid.NewGuid(), "owner-a");
        int overflow = CaptureProgressHub.MaxRetainedHistoryEvents + 40;
        for (int i = 0; i < overflow; i++)
        {
            capture.Publish(captureOp, CaptureStage.Queued);
        }

        Assert.True(capture.Contains(captureOp));
        int replayed = 0;
        await foreach (CaptureProgress _ in capture.WatchAsync(captureOp, CancellationToken.None))
        {
            replayed++;
            if (replayed >= CaptureProgressHub.MaxRetainedHistoryEvents)
            {
                break;
            }
        }

        Assert.Equal(CaptureProgressHub.MaxRetainedHistoryEvents, replayed);
        Assert.True(capture.Contains(captureOp));

        DeploymentProgressHub deployment = new();
        Guid depOp = Guid.NewGuid();
        deployment.Ensure(depOp, "owner-a");
        for (int i = 0; i < overflow; i++)
        {
            deployment.Publish(depOp, DomainDeploymentState.Prechecking);
        }

        Assert.True(deployment.Contains(depOp));
        replayed = 0;
        await foreach (DeploymentProgress _ in deployment.WatchAsync(depOp, CancellationToken.None))
        {
            replayed++;
            if (replayed >= DeploymentProgressHub.MaxRetainedHistoryEvents)
            {
                break;
            }
        }

        Assert.Equal(DeploymentProgressHub.MaxRetainedHistoryEvents, replayed);
        Assert.True(deployment.Contains(depOp));

        OnboardingProgressHub onboarding = new();
        Guid onbOp = Guid.NewGuid();
        onboarding.Ensure(onbOp, "owner-a");
        for (int i = 0; i < overflow; i++)
        {
            onboarding.Publish(onbOp, DomainOnboardingState.Prechecking);
        }

        Assert.True(onboarding.Contains(onbOp));
        replayed = 0;
        await foreach (OnboardingProgress _ in onboarding.WatchAsync(onbOp, CancellationToken.None))
        {
            replayed++;
            if (replayed >= OnboardingProgressHub.MaxRetainedHistoryEvents)
            {
                break;
            }
        }

        Assert.Equal(OnboardingProgressHub.MaxRetainedHistoryEvents, replayed);
        Assert.True(onboarding.Contains(onbOp));
    }

    [Fact]
    public async Task Ac3SlowSubscriberDisconnectOnFullDoesNotBlockPublish()
    {
        CaptureProgressHub capture = new();
        Guid captureOp = capture.Begin(Guid.NewGuid(), "owner-a");

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        Task<int> watchTask = Task.Run(
            async () =>
            {
                int count = 0;
                await foreach (CaptureProgress _ in capture.WatchAsync(captureOp, cts.Token))
                {
                    count++;
                    // Stall after first live item so the bounded channel fills.
                    if (count == 1)
                    {
                        await Task.Delay(Timeout.Infinite, cts.Token);
                    }
                }

                return count;
            },
            cts.Token);

        await Task.Delay(50);
        int published = CaptureProgressHub.SubscriberChannelCapacity + 8;
        for (int i = 0; i < published; i++)
        {
            capture.Publish(captureOp, CaptureStage.Queued);
        }

        // Publish must remain non-blocking after slow-subscriber disconnect-on-full.
        capture.Publish(captureOp, CaptureStage.Connecting);
        Assert.True(capture.Contains(captureOp));
        cts.Cancel();
        try
        {
            await watchTask;
        }
        catch (OperationCanceledException)
        {
            // Expected when the stalled watcher is cancelled after disconnect/fill.
        }
    }

    [Fact]
    public void Ac4DocsAdvanceNextToPlan30CompleteSeed()
    {
        string root = RepoRoot();
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan30 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-30-watch-owner-acl-hub-backpressure.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string changelog = File.ReadAllText(Path.Combine(root, "CHANGELOG.md"));

        Assert.Contains(
            "W7-260 | [#927](https://github.com/sesquicadaver/MTDirector/issues/927) | WATCH-BP-01 — Bounded ProgressHub subscriber channels / slow-subscriber backpressure + live `_history` cap | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-298 (#1002)", roadmap, StringComparison.Ordinal);
        Assert.Contains("WATCH-BP-01", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-260", plan30, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-298 (#1002)", plan30, StringComparison.Ordinal);
        Assert.Contains("W7-260", continuous, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-298 (#1002)", continuous, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-260 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("WatchBp01HubBackpressureW7260", testing, StringComparison.Ordinal);
        Assert.Contains("W7-260", changelog, StringComparison.Ordinal);
        Assert.Contains("WATCH-BP-01", changelog, StringComparison.Ordinal);
    }

    private static string RepoRoot()
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
