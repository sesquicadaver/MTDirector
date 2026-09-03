using System.Reflection;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Application.Routing;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Mfc.Desktop;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.UnitTests.Application.Fakes;
using Xunit;
using DomainDevice = Mfc.Domain.Inventory.Device;
using DomainDeviceRole = Mfc.Domain.Inventory.DeviceRole;

namespace Mfc.UnitTests.Desktop;

/// <summary>Living Spec — Desktop routing assurance viewers (M7.1-10) AC 1–8.</summary>
public sealed class DesktopRoutingAssuranceLivingSpecTests
{
    [Fact]
    public void Ac1GrpcProtoAndServiceRegistered()
    {
        string[] methods = RoutingAssuranceService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["GetDeviceRoutingAssuranceState"], methods);

        Assert.NotNull(typeof(RoutingAssuranceGrpcService));
        string program = ReadSource("src/Mfc.Controller/Program.cs");
        Assert.Contains("MapGrpcService<RoutingAssuranceGrpcService>", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ViewModelExposesExpectationsFindingsAndTraceCollections()
    {
        Type vm = typeof(RoutingAssuranceViewModel);
        Assert.NotNull(vm.GetProperty(nameof(RoutingAssuranceViewModel.ExpectationLines)));
        Assert.NotNull(vm.GetProperty(nameof(RoutingAssuranceViewModel.FindingLines)));
        Assert.NotNull(vm.GetProperty(nameof(RoutingAssuranceViewModel.TraceSummaryLines)));
        Assert.NotNull(vm.GetProperty(nameof(RoutingAssuranceViewModel.ConfigurationHashText)));
        Assert.NotNull(vm.GetProperty(nameof(RoutingAssuranceViewModel.OperationalHashText)));
        Assert.NotNull(typeof(RouteExpectationLineItem).GetProperty(nameof(RouteExpectationLineItem.AllowedNextHopsText)));
        Assert.NotNull(typeof(RouteFindingLineItem).GetProperty(nameof(RouteFindingLineItem.SubjectText)));
        Assert.NotNull(typeof(RouteResolutionTraceSummaryLineItem).GetProperty(
            nameof(RouteResolutionTraceSummaryLineItem.NextHopGatewaysText)));
    }

    /// <summary>W2.2: Routing assurance binds next-hop values and finding subject instead of a single SummaryLine.</summary>
    [Fact]
    public void Ac9RoutingAssuranceBindsNextHopAndSubjectFields()
    {
        Assert.NotNull(typeof(RouteExpectationLineItem).GetProperty(nameof(RouteExpectationLineItem.AllowedNextHopsText)));
        Assert.NotNull(typeof(RouteExpectationLineItem).GetProperty(nameof(RouteExpectationLineItem.ExpectedTableText)));
        Assert.NotNull(typeof(RouteFindingLineItem).GetProperty(nameof(RouteFindingLineItem.SubjectText)));
        Assert.NotNull(typeof(RouteResolutionTraceSummaryLineItem).GetProperty(
            nameof(RouteResolutionTraceSummaryLineItem.NextHopGatewaysText)));
        Assert.NotNull(typeof(RouteResolutionTraceSummaryLineItem).GetProperty(
            nameof(RouteResolutionTraceSummaryLineItem.EgressInterfacesText)));

        string axaml = ReadMainWindowAxaml();
        Assert.Contains("AllowedNextHopsText", axaml, StringComparison.Ordinal);
        Assert.Contains("SubjectText", axaml, StringComparison.Ordinal);
        Assert.Contains("NextHopGatewaysText", axaml, StringComparison.Ordinal);
        Assert.Contains("Next hops:", axaml, StringComparison.Ordinal);
        Assert.Contains("Subject:", axaml, StringComparison.Ordinal);
        Assert.DoesNotContain("next-hops:", axaml, StringComparison.Ordinal);
        Assert.Contains("vm:RouteExpectationLineItem", axaml, StringComparison.Ordinal);
        Assert.Contains("vm:RouteFindingLineItem", axaml, StringComparison.Ordinal);
        Assert.Contains("vm:RouteResolutionTraceSummaryLineItem", axaml, StringComparison.Ordinal);

        string vm = ReadSource("src/Mfc.Desktop/ViewModels/RoutingAssuranceViewModel.cs");
        Assert.Contains("AllowedNextHopsText", vm, StringComparison.Ordinal);
        Assert.Contains("JoinOrDash", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("next_hops={expectation.AllowedNextHops.Count}", vm, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteEnabled", vm, StringComparison.Ordinal);
        Assert.Contains("no routing writes", vm, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac3NoRoutingWriteSurfaceOnDesktop()
    {
        Type vm = typeof(RoutingAssuranceViewModel);
        Assert.False((bool)vm.GetProperty(nameof(RoutingAssuranceViewModel.HasRoutingWriteControls))!
            .GetValue(CreateVmForFlags())!);

        Assert.Null(vm.GetMethod("UpsertRoutingAssurance"));
        Assert.Null(vm.GetMethod("WriteRoute"));
        Assert.Null(vm.GetProperty("UpsertCommand"));
        Assert.Null(vm.GetProperty("WriteRouteCommand"));

        foreach (string name in RoutingAssuranceService.Descriptor.Methods.Select(static m => m.Name))
        {
            Assert.DoesNotContain("Upsert", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Write", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Set", name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Add", name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Ac4MainWindowContainsRoutingAssuranceSection()
    {
        string axaml = ReadMainWindowAxaml();
        Assert.Contains("Routing assurance (read-only)", axaml, StringComparison.Ordinal);
        Assert.Contains("RoutingAssurance.ExpectationLines", axaml, StringComparison.Ordinal);
        Assert.Contains("RoutingAssurance.FindingLines", axaml, StringComparison.Ordinal);
        Assert.Contains("RoutingAssurance.TraceSummaryLines", axaml, StringComparison.Ordinal);
        Assert.Contains("AllowedNextHopsText", axaml, StringComparison.Ordinal);
        Assert.Contains("SubjectText", axaml, StringComparison.Ordinal);
        Assert.Contains("NextHopGatewaysText", axaml, StringComparison.Ordinal);
        Assert.Contains("Route expectations", axaml, StringComparison.Ordinal);
        Assert.Contains("Trace summaries", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac5GetUseCaseReturnsDetailViewWithExpectationsAndFindings()
    {
        FakeAuthorizationBoundary auth = new();
        FakeRoutingAssuranceStateStore store = new();
        Mfc.Domain.Inventory.Device device = CreateDevice();
        FakeDeviceStore devices = new();
        await devices.AddAsync(device);

        Mfc.Domain.Routing.RouteExpectation expectation = new()
        {
            NodeId = null,
            Family = "ipv4",
            DestinationPrefix = "203.0.113.0/24",
            ExpectedTable = "main",
        };
        Mfc.Domain.Routing.RouteFinding finding = new()
        {
            Code = RouteExpectationCodes.ExpectedTableMismatch,
            Message = "table mismatch",
            Subject = "203.0.113.10",
        };
        RoutingAssuranceState state = RoutingAssuranceState.Create(
            device.Id,
            SampleConfiguration(),
            SampleOperational(),
            DateTimeOffset.UtcNow,
            [expectation],
            [finding]);
        await store.UpsertAsync(state);

        ApplicationResult<RoutingAssuranceDetailView> loaded = await new GetRoutingAssuranceStateUseCase(auth, store)
            .ExecuteAsync(new GetRoutingAssuranceStateQuery { Actor = "tester", DeviceId = device.Id.Value });
        Assert.True(loaded.IsSuccess);
        Assert.Single(loaded.Value!.Expectations);
        Assert.Single(loaded.Value.Findings);
        Assert.Equal("203.0.113.0/24", loaded.Value.Expectations[0].DestinationPrefix);
        Assert.Equal(RouteExpectationCodes.ExpectedTableMismatch, loaded.Value.Findings[0].Code);
    }

    [Fact]
    public async Task Ac6TraceSummaryBoundedWithoutFullRouteTableDump()
    {
        FakeAuthorizationBoundary auth = new();
        FakeRoutingAssuranceStateStore store = new();
        Mfc.Domain.Inventory.Device device = CreateDevice();

        RoutingConfigurationSnapshot configuration = SampleConfiguration();
        RoutingOperationalSnapshot operational = SampleOperational();
        RouteResolutionTrace trace = RouteResolutionTraceEngine.Analyze(
            new RouteResolutionQuery { Family = "ipv4", DestinationAddress = "203.0.113.10" },
            configuration,
            operational);

        RoutingAssuranceState state = RoutingAssuranceState.Create(
            device.Id,
            configuration,
            operational,
            DateTimeOffset.UtcNow,
            [],
            [],
            [trace]);
        await store.UpsertAsync(state);

        ApplicationResult<RoutingAssuranceDetailView> loaded = await new GetRoutingAssuranceStateUseCase(auth, store)
            .ExecuteAsync(new GetRoutingAssuranceStateQuery { Actor = "tester", DeviceId = device.Id.Value });
        Assert.True(loaded.IsSuccess);
        Assert.Single(loaded.Value!.TraceSummaries);
        RouteResolutionTraceSummaryView summary = loaded.Value.TraceSummaries[0];
        Assert.Equal("203.0.113.10", summary.DestinationAddress);
        Assert.True(summary.NextHopGateways.Count <= RouteResolutionTraceSummaryView.MaxNextHopGateways);
        Assert.True(summary.EgressInterfaces.Count <= RouteResolutionTraceSummaryView.MaxEgressInterfaces);

        string[] protoFields = RouteResolutionTraceSummary.Descriptor.Fields
            .InFieldNumberOrder()
            .Select(static f => f.Name)
            .ToArray();
        Assert.DoesNotContain(protoFields, static f => f.Contains("route_candidates", StringComparison.Ordinal));
        Assert.DoesNotContain(protoFields, static f => f.Contains("recursive", StringComparison.Ordinal));
        Assert.DoesNotContain(protoFields, static f => f.Contains("operational", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac7DesktopHasNoDomainOrRouterOsReferences()
    {
        Assembly desktop = typeof(MainWindow).Assembly;
        string[] forbidden =
        [
            "Mfc.Domain",
            "Mfc.Application",
            "Mfc.Infrastructure",
            "Mfc.RouterOs",
            "Npgsql",
            "Microsoft.EntityFrameworkCore",
        ];
        foreach (AssemblyName reference in desktop.GetReferencedAssemblies())
        {
            Assert.DoesNotContain(forbidden, f => string.Equals(reference.Name, f, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Ac8SevenMvpModulesRemainUnchanged()
    {
        string[] expected =
        [
            nameof(ShellNavigationModule.Inventory),
            nameof(ShellNavigationModule.Node),
            nameof(ShellNavigationModule.Snapshots),
            nameof(ShellNavigationModule.Policies),
            nameof(ShellNavigationModule.Operations),
            nameof(ShellNavigationModule.Drift),
            nameof(ShellNavigationModule.Audit),
        ];
        Assert.Equal(expected, Enum.GetNames<ShellNavigationModule>());
        Assert.Equal(7, expected.Length);
    }

    private static RoutingAssuranceViewModel CreateVmForFlags()
        => new(
            new NullRoutingAssuranceClient(),
            new NullConnection(),
            new InventoryTreeViewModel(new NullInventoryTree(), new NullConnection()));

    private static DomainDevice CreateDevice()
        => DomainDevice.Reconstitute(
            DeviceId.New(),
            NodeId.New(),
            NonEmptyName.Create("r1"),
            ManagementEndpoint.Create("192.0.2.1", 8729),
            DomainDeviceRole.Router,
            enabled: true,
            lastSupportState: null,
            ManagementState.Unmanaged,
            rowVersion: 1,
            lastCompletedCaptureId: null);

    private static RoutingConfigurationSnapshot SampleConfiguration()
        => Config([Table("main")], staticRoutes: [Route("0.0.0.0/0", "1.1.1.1", "main")]);

    private static RoutingOperationalSnapshot SampleOperational()
        => Ops([Obs("0.0.0.0/0", "1.1.1.1", "main", immediateGw: "1.1.1.1%ether1")]);

    private static RoutingTableFact Table(string name)
        => new() { Name = name, Fib = "yes", Disabled = "false" };

    private static StaticRouteConfigFact Route(string dst, string gateway, string table, int distance = 1)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            Gateway = gateway,
            RoutingTable = table,
            Distance = distance,
            Scope = null,
            TargetScope = null,
            PrefSrc = null,
            CheckGateway = null,
            Disabled = "false",
        };

    private static RouteObservationFact Obs(
        string dst,
        string gateway,
        string table,
        string? immediateGw = null)
        => new()
        {
            Family = "ipv4",
            DstAddress = dst,
            RoutingTable = table,
            Gateway = gateway,
            Active = "true",
            ImmediateGateway = immediateGw,
            GatewayStatus = "reachable",
            IsDynamic = false,
            HwOffloaded = null,
        };

    private static RoutingConfigurationSnapshot Config(
        IReadOnlyList<RoutingTableFact> tables,
        IReadOnlyList<StaticRouteConfigFact>? staticRoutes = null)
        => new(
            tables,
            new RoutingSettingsFact
            {
                PolicyRules = "lookup",
                CheckGatewayPingCount = null,
                CheckGatewayPingInterval = null,
                CheckGatewayPingTimeout = null,
                ConnectedInChain = null,
                DynamicInChain = null,
                SingleProcess = "yes",
            },
            [],
            [],
            staticRoutes ?? [],
            [],
            [],
            new Dictionary<string, string>(StringComparer.Ordinal));

    private static RoutingOperationalSnapshot Ops(IReadOnlyList<RouteObservationFact> routes)
        => new(routes, [], new Dictionary<string, string>(StringComparer.Ordinal));

    private static string ReadMainWindowAxaml()
        => ReadSource("src/Mfc.Desktop/MainWindow.axaml");

    private static string ReadSource(string relativePath)
    {
        string root = FindRepoRoot();
        return File.ReadAllText(Path.Combine(root, relativePath));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MikroTikFirewallController.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class NullRoutingAssuranceClient : IRoutingAssuranceServiceClient
    {
        public Task<RoutingAssuranceStateDetail> GetDeviceRoutingAssuranceStateAsync(
            Guid deviceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new RoutingAssuranceStateDetail());
    }

    private sealed class NullConnection : IControllerConnectionService
    {
        public Grpc.Net.Client.GrpcChannel? Channel => null;

        public ControllerConnectionState State => ControllerConnectionState.Disconnected;

        public string? LastError => null;

        public event EventHandler? StateChanged
        {
            add { }
            remove { }
        }

        public Task ConnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task DisconnectAsync() => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NullInventoryTree : IInventoryTreeService
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
