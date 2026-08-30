using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W3.3: Onboarding Start consumes Watch stream, not only Start.Timeline.</summary>
public sealed class OnboardingViewModelTests
{
    [Fact]
    public async Task StartWatchesProgressAndPrefersStreamOverStartTimeline()
    {
        Guid planId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid operationId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Sha256 planHash = Hash("plan");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        FakeOnboardingClient client = new()
        {
            StartResponse = new OnboardingOperationSummary
            {
                OperationId = DesktopProtoUuid.FromGuid(operationId),
                PlanId = DesktopProtoUuid.FromGuid(planId),
                State = OnboardingOperationState.EnablingAnchors,
                Timeline = { "from-start-only" },
            },
            WatchEvents =
            [
                new OnboardingProgress
                {
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    State = OnboardingOperationState.EnablingAnchors,
                    TimelineEntry = "anchors enabling",
                },
                new OnboardingProgress
                {
                    OperationId = DesktopProtoUuid.FromGuid(operationId),
                    State = OnboardingOperationState.Committed,
                    TimelineEntry = "committed",
                },
            ],
        };

        using OnboardingViewModel vm = new(client, connection, inventory)
        {
            PlanId = planId,
            PlanHash = planHash,
        };

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.StartCalls);
        Assert.Equal(1, client.WatchCalls);
        Assert.Equal(operationId, client.WatchedOperationId);
        Assert.Equal(operationId, vm.OperationId);
        Assert.Equal("Operation Committed.", vm.StatusText);
        Assert.Equal(
            ["EnablingAnchors: anchors enabling", "Committed: committed"],
            vm.ProgressLines.ToArray());
        Assert.DoesNotContain("from-start-only", vm.ProgressLines);
    }

    [Fact]
    public async Task StartFallsBackToTimelineWhenWatchIsEmpty()
    {
        Guid planId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid operationId = Guid.Parse("22222222-3333-4444-5555-666666666666");
        FakeConnection connection = new();
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        FakeOnboardingClient client = new()
        {
            StartResponse = new OnboardingOperationSummary
            {
                OperationId = DesktopProtoUuid.FromGuid(operationId),
                State = OnboardingOperationState.Committed,
                Timeline = { "queued", "committed" },
            },
        };

        using OnboardingViewModel vm = new(client, connection, inventory)
        {
            PlanId = planId,
            PlanHash = Hash("plan"),
        };

        await vm.StartCommand.ExecuteAsync(null);

        Assert.Equal(1, client.WatchCalls);
        Assert.Equal(["queued", "committed"], vm.ProgressLines.ToArray());
        Assert.Equal("Operation Committed.", vm.StatusText);
    }

    private static Sha256 Hash(string seed)
        => new() { Value = ByteString.CopyFrom(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed))) };

    private sealed class FakeConnection : IControllerConnectionService
    {
        public ControllerConnectionState State { get; set; } = ControllerConnectionState.Connected;

        public string? LastError => null;

        public Grpc.Net.Client.GrpcChannel? Channel => null;

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class EmptyTreeService : IInventoryTreeService
    {
        public InventoryTreeLoadResult Current { get; } = new()
        {
            Roots = [],
            Succeeded = true,
            IsCached = false,
            IsRefreshing = false,
        };

        public Task<InventoryTreeLoadResult> RefreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Current);
    }

    private sealed class FakeOnboardingClient : IOnboardingServiceClient
    {
        public OnboardingOperationSummary StartResponse { get; init; } = new();

        public IReadOnlyList<OnboardingProgress> WatchEvents { get; init; } = [];

        public int StartCalls { get; private set; }

        public int WatchCalls { get; private set; }

        public Guid WatchedOperationId { get; private set; }

        public Task<OnboardingPrerequisiteReport> ValidatePrerequisitesAsync(
            Guid nodeId,
            IReadOnlyList<OnboardingDevicePrerequisiteFacts> devices,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingPlanSummary> CreatePlanAsync(
            Guid nodeId,
            Sha256 membershipHash,
            Sha256 topologyHash,
            IReadOnlyList<OnboardingDevicePlanInput> devices,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingOperationSummary> StartAsync(
            Guid planId,
            Sha256 planHash,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StartCalls++;
            return Task.FromResult(StartResponse);
        }

        public async IAsyncEnumerable<OnboardingProgress> WatchAsync(
            Guid operationId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            WatchCalls++;
            WatchedOperationId = operationId;
            foreach (OnboardingProgress progress in WatchEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return progress;
                await Task.Yield();
            }
        }

        public Task<OnboardingOperationSummary> RollbackAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<OnboardingRecoveryStatus> GetRecoveryStatusAsync(
            Guid nodeId,
            Guid? operationId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
