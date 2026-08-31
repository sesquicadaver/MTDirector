using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W2.2: Routing assurance rows bind next-hop values and finding subject, not a count-only SummaryLine.</summary>
public sealed class RoutingAssuranceViewModelTests
{
    [Fact]
    public void ExpectationFromProtoListsAllowedNextHopsNotOnlyCount()
    {
        RouteExpectation proto = new()
        {
            Family = "ipv4",
            DestinationPrefix = "203.0.113.0/24",
            ExpectedTable = "main",
            ExpectedVrf = "corp",
            Critical = true,
            AllowedNextHops = { "192.0.2.1", "192.0.2.2" },
            AllowedEgressInterfaces = { "ether1" },
        };

        RouteExpectationLineItem item = RouteExpectationLineItem.FromProto(proto);

        Assert.Equal("192.0.2.1, 192.0.2.2", item.AllowedNextHopsText);
        Assert.DoesNotContain("next_hops=2", item.SummaryLine, StringComparison.Ordinal);
        Assert.Contains("203.0.113.0/24", item.SummaryLine, StringComparison.Ordinal);
        Assert.Equal("main", item.ExpectedTableText);
        Assert.Equal("ether1", item.AllowedEgressInterfacesText);
        Assert.Equal("critical", item.CriticalText);
    }

    [Fact]
    public void FindingFromProtoKeepsSubjectAsOwnField()
    {
        RouteFinding proto = new()
        {
            Code = "EXPECTED_TABLE_MISMATCH",
            Message = "table mismatch",
            Subject = "203.0.113.10",
        };

        RouteFindingLineItem item = RouteFindingLineItem.FromProto(proto);

        Assert.Equal("203.0.113.10", item.SubjectText);
        Assert.Equal("EXPECTED_TABLE_MISMATCH", item.Code);
        Assert.Contains("table mismatch", item.SummaryLine, StringComparison.Ordinal);
        Assert.DoesNotContain("subject=", item.SummaryLine, StringComparison.Ordinal);
    }

    [Fact]
    public void TraceFromProtoListsNextHopGateways()
    {
        RouteResolutionTraceSummary proto = new()
        {
            Family = "ipv4",
            DestinationAddress = "203.0.113.10",
            SelectedTable = "main",
            SelectedVrf = "corp",
            MatchedPrefix = "203.0.113.0/24",
            ExecutionPath = "cpu",
            Decision = "forward",
            NextHopGateways = { "192.0.2.1", "192.0.2.2" },
            EgressInterfaces = { "ether1" },
        };

        RouteResolutionTraceSummaryLineItem item = RouteResolutionTraceSummaryLineItem.FromProto(proto);

        Assert.Equal("192.0.2.1, 192.0.2.2", item.NextHopGatewaysText);
        Assert.Equal("ether1", item.EgressInterfacesText);
        Assert.Contains("203.0.113.10", item.SummaryLine, StringComparison.Ordinal);
        Assert.DoesNotContain("nh=192.0.2.1,192.0.2.2", item.SummaryLine, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshBindsTypedExpectationFindingAndTraceRows()
    {
        Guid deviceId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeConnection connection = new() { State = ControllerConnectionState.Connected };
        InventoryTreeViewModel inventory = new(new EmptyTreeService(), connection);
        inventory.SelectedNode = new InventoryNodeViewModel(new InventoryTreeItem
        {
            Kind = InventoryTreeKind.Device,
            Id = deviceId,
            DisplayName = "r1",
        });
        FakeClient client = new()
        {
            Detail = new RoutingAssuranceStateDetail
            {
                RouteExpectationCount = 1,
                RouteFindingCount = 1,
                ResolutionTraceCount = 1,
                Expectations =
                {
                    new RouteExpectation
                    {
                        Family = "ipv4",
                        DestinationPrefix = "203.0.113.0/24",
                        AllowedNextHops = { "192.0.2.1" },
                    },
                },
                Findings =
                {
                    new RouteFinding
                    {
                        Code = "EXPECTED_TABLE_MISMATCH",
                        Message = "table mismatch",
                        Subject = "203.0.113.10",
                    },
                },
                TraceSummaries =
                {
                    new RouteResolutionTraceSummary
                    {
                        Family = "ipv4",
                        DestinationAddress = "203.0.113.10",
                        NextHopGateways = { "192.0.2.1" },
                    },
                },
            },
        };

        using RoutingAssuranceViewModel vm = new(client, connection, inventory);
        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal("192.0.2.1", Assert.Single(vm.ExpectationLines).AllowedNextHopsText);
        Assert.Equal("203.0.113.10", Assert.Single(vm.FindingLines).SubjectText);
        Assert.Equal("192.0.2.1", Assert.Single(vm.TraceSummaryLines).NextHopGatewaysText);
        Assert.False(vm.HasRoutingWriteControls);
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

    private sealed class FakeClient : IRoutingAssuranceServiceClient
    {
        public required RoutingAssuranceStateDetail Detail { get; init; }

        public Task<RoutingAssuranceStateDetail> GetDeviceRoutingAssuranceStateAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Detail);
        }
    }
}
