using System.Collections.Concurrent;
using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W1.5 list findings + W3.7 GetDriftEvent detail payload.</summary>
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

    [Fact]
    public async Task RefreshLoadsGetDriftEventDetailInsteadOfTruncatedListHashes()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid nodeId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = DeviceInventory(deviceId, connection);
        RecordingDriftClient client = new();
        client.ListEvents =
        [
            new DriftEvent
            {
                Id = DesktopProtoUuid.FromGuid(eventId),
                DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                NodeId = DesktopProtoUuid.FromGuid(nodeId),
                Outcome = DriftOutcome.WarningDrift,
                BaselineCommittedHash = HashFill(0xab),
                ActualManagedResourceHash = HashFill(0xcd),
                SemanticDiffCanonical = "list-diff",
                Findings =
                {
                    new DriftFinding
                    {
                        Kind = DriftFindingKind.CountersChanged,
                        Severity = DriftSeverity.Ignored,
                    },
                },
            },
        ];
        client.GetEvents[eventId] = new DriftEvent
        {
            Id = DesktopProtoUuid.FromGuid(eventId),
            DeviceId = DesktopProtoUuid.FromGuid(deviceId),
            NodeId = DesktopProtoUuid.FromGuid(nodeId),
            Outcome = DriftOutcome.WarningDrift,
            Immutable = true,
            BaselineCommittedHash = HashFill(0xab),
            ActualManagedResourceHash = HashFill(0xcd),
            DesiredArtifactHashIgnoredForBaseline = HashFill(0x11),
            SemanticDiffHash = HashFill(0x22),
            SemanticDiffCanonical = "get-diff-canonical",
            Findings =
            {
                new DriftFinding
                {
                    Kind = DriftFindingKind.ManagedRuleChanged,
                    Severity = DriftSeverity.Critical,
                    Detail = "rule 1 changed",
                },
                new DriftFinding
                {
                    Kind = DriftFindingKind.CountersChanged,
                    Severity = DriftSeverity.Ignored,
                },
            },
        };

        using DriftViewModel vm = new(client, connection, inventory);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.ListCalls);
        Assert.Equal(1, client.GetCalls);
        Assert.Equal(eventId, client.LastGetId);
        Assert.True(vm.HasSelectedEventDetail);
        Assert.Equal(nodeId.ToString("D"), vm.DetailNodeIdText);
        Assert.Equal(Convert.ToHexString(Enumerable.Repeat((byte)0xab, 32).ToArray()).ToLowerInvariant(), vm.DetailBaselineHashText);
        Assert.Equal(Convert.ToHexString(Enumerable.Repeat((byte)0x11, 32).ToArray()).ToLowerInvariant(), vm.DetailDesiredHashText);
        Assert.Equal(Convert.ToHexString(Enumerable.Repeat((byte)0x22, 32).ToArray()).ToLowerInvariant(), vm.DetailSemanticDiffHashText);
        Assert.Equal("immutable", vm.DetailImmutableText);
        Assert.Equal("get-diff-canonical", vm.SemanticDiffText);
        Assert.Equal(2, vm.SelectedEventFindings.Count);
        Assert.Equal(nameof(DriftFindingKind.ManagedRuleChanged), vm.SelectedEventFindings[0].KindText);
        Assert.EndsWith("…", vm.SelectedEvent!.BaselineHashText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDriftEventFailureKeepsListPayload()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid eventId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = DeviceInventory(deviceId, connection);
        RecordingDriftClient client = new()
        {
            GetError = new InvalidOperationException("drift-store down"),
            ListEvents =
            [
                new DriftEvent
                {
                    Id = DesktopProtoUuid.FromGuid(eventId),
                    SemanticDiffCanonical = "list-diff",
                    Findings =
                    {
                        new DriftFinding
                        {
                            Kind = DriftFindingKind.CountersChanged,
                            Severity = DriftSeverity.Ignored,
                        },
                    },
                },
            ],
        };

        using DriftViewModel vm = new(client, connection, inventory);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("drift-store down", vm.ErrorText);
        Assert.Contains("list payload", vm.StatusText, StringComparison.Ordinal);
        Assert.False(vm.HasSelectedEventDetail);
        Assert.Equal("list-diff", vm.SemanticDiffText);
        Assert.Equal(nameof(DriftFindingKind.CountersChanged), Assert.Single(vm.SelectedEventFindings).KindText);
    }

    [Fact]
    public async Task StaleGetDriftEventDoesNotOverwriteNewerSelection()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid firstId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid secondId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff");
        Guid firstNode = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Guid secondNode = Guid.Parse("22222222-2222-2222-2222-222222222222");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = DeviceInventory(deviceId, connection);
        RecordingDriftClient client = new();
        DriftEventListItem first = DriftEventListItem.FromProto(new DriftEvent
        {
            Id = DesktopProtoUuid.FromGuid(firstId),
            SemanticDiffCanonical = "first-list",
        });
        DriftEventListItem second = DriftEventListItem.FromProto(new DriftEvent
        {
            Id = DesktopProtoUuid.FromGuid(secondId),
            SemanticDiffCanonical = "second-list",
        });
        client.BlockGetForId = firstId;
        client.GetEvents[firstId] = new DriftEvent
        {
            Id = DesktopProtoUuid.FromGuid(firstId),
            NodeId = DesktopProtoUuid.FromGuid(firstNode),
            Immutable = true,
            SemanticDiffCanonical = "first-get",
        };
        client.GetEvents[secondId] = new DriftEvent
        {
            Id = DesktopProtoUuid.FromGuid(secondId),
            NodeId = DesktopProtoUuid.FromGuid(secondNode),
            Immutable = true,
            SemanticDiffCanonical = "second-get",
        };

        using DriftViewModel vm = new(client, connection, inventory);
        vm.Events.Add(first);
        vm.Events.Add(second);
        vm.SelectedEvent = first;
        await client.WaitUntilGetStartedAsync(firstId);
        vm.SelectedEvent = second;
        await WaitUntilAsync(() => vm.DetailNodeIdText == secondNode.ToString("D"));
        client.ReleaseBlockedGet();
        await Task.Delay(50);

        Assert.Equal(secondNode.ToString("D"), vm.DetailNodeIdText);
        Assert.Equal("second-get", vm.SemanticDiffText);
        Assert.Equal(secondId, vm.SelectedEvent?.Id);
    }

    private static InventoryTreeViewModel DeviceInventory(Guid deviceId, FakeConnection connection)
    {
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        InventoryNodeViewModel site = new(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Site,
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            DisplayName = "LAB",
            Children =
            [
                new InventoryTreeItem
                {
                    Kind = InventoryTreeKind.Node,
                    Id = Guid.Parse("99999999-8888-7777-6666-555555555555"),
                    DisplayName = "core",
                    Children =
                    [
                        new InventoryTreeItem
                        {
                            Kind = InventoryTreeKind.Device,
                            Id = deviceId,
                            DisplayName = "chr-seed",
                        },
                    ],
                },
            ],
        });
        inventory.Roots.Add(site);
        inventory.SelectedNode = site.Children[0].Children[0];
        return inventory;
    }

    private static Sha256 HashFill(byte fill)
        => new() { Value = ByteString.CopyFrom(Enumerable.Repeat(fill, 32).ToArray()) };

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        for (int i = 0; i < 200; i++)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(10);
        }

        throw new TimeoutException("Condition was not met.");
    }

    private sealed class RecordingDriftClient : IDriftServiceClient
    {
        private readonly TaskCompletionSource _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ConcurrentDictionary<Guid, TaskCompletionSource> _started = [];

        public IReadOnlyList<DriftEvent> ListEvents { get; set; } = [];

        public Dictionary<Guid, DriftEvent> GetEvents { get; } = [];

        public Guid? BlockGetForId { get; set; }

        public Exception? GetError { get; init; }

        public int ListCalls { get; private set; }

        public int GetCalls { get; private set; }

        public Guid? LastGetId { get; private set; }

        public Task<IReadOnlyList<DriftEvent>> ListDeviceDriftEventsAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            ListCalls++;
            return Task.FromResult(ListEvents);
        }

        public async Task<DriftEvent> GetDriftEventAsync(
            Guid driftEventId,
            CancellationToken cancellationToken = default)
        {
            GetCalls++;
            LastGetId = driftEventId;
            TaskCompletionSource started = _started.GetOrAdd(
                driftEventId,
                static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            started.TrySetResult();
            if (BlockGetForId == driftEventId)
            {
                await _blocked.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (GetError is not null)
            {
                throw GetError;
            }

            return GetEvents.TryGetValue(driftEventId, out DriftEvent? evt) ? evt : new DriftEvent();
        }

        public Task WaitUntilGetStartedAsync(Guid driftEventId)
        {
            TaskCompletionSource started = _started.GetOrAdd(
                driftEventId,
                static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
            return started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }

        public void ReleaseBlockedGet() => _blocked.TrySetResult();
    }

    private sealed class FakeConnection : IControllerConnectionService
    {
        public ControllerConnectionState State { get; set; } = ControllerConnectionState.Disconnected;

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
}
