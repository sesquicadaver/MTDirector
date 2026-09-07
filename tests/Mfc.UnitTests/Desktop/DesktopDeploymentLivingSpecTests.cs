using Google.Protobuf;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-DEPLOY-01 / W7-81: Desktop Deployment panel Living Spec vs DeploymentGrpcHost.</summary>
public sealed class DesktopDeploymentLivingSpecTests
{
    private static readonly string[] ExpectedWireMethods =
    [
        "CreatePlan",
        "GetRecoveryStatus",
        "Rollback",
        "Start",
        "Watch",
    ];

    [Fact]
    public void Ac1WireAndDesktopClientExposePlanStartWatchRollbackAndRecovery()
    {
        string[] methods = DeploymentService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedWireMethods, methods);

        Type client = typeof(IDeploymentServiceClient);
        Assert.NotNull(client.GetMethod(nameof(IDeploymentServiceClient.CreatePlanAsync)));
        Assert.NotNull(client.GetMethod(nameof(IDeploymentServiceClient.StartAsync)));
        Assert.NotNull(client.GetMethod(nameof(IDeploymentServiceClient.WatchAsync)));
        Assert.NotNull(client.GetMethod(nameof(IDeploymentServiceClient.RollbackAsync)));
        Assert.NotNull(client.GetMethod(nameof(IDeploymentServiceClient.GetRecoveryStatusAsync)));

        string source = ReadSource("src/Mfc.Desktop/Services/GrpcDeploymentServiceClient.cs");
        foreach (string rpc in ExpectedWireMethods)
        {
            Assert.Contains(rpc + "Async", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Ac2ViewModelExposesPlanStartRollbackRecoveryWithoutForceApplyOrRawCommands()
    {
        using DeploymentViewModel vm = CreateVm(
            new FakeDeploymentClient(),
            ControllerConnectionState.Connected,
            selectNode: false);

        Assert.NotNull(vm.GetType().GetProperty(nameof(DeploymentViewModel.CreatePlanCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(DeploymentViewModel.StartCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(DeploymentViewModel.RollbackCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(DeploymentViewModel.RecoveryCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(DeploymentViewModel.SemanticDiffRows)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(DeploymentViewModel.ProgressLines)));
        Assert.False(vm.HasForceApply);
        Assert.False(vm.HasRawRouterOsCommands);
    }

    [Fact]
    public async Task Ac3CreatePlanLoadsSemanticDiffWhenNodeSelected()
    {
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid planId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        FakeDeploymentClient client = new()
        {
            Plan = new DeploymentPlanSummary
            {
                PlanId = DesktopProtoUuid.FromGuid(planId),
                NodeId = DesktopProtoUuid.FromGuid(nodeId),
                PlanHash = Hash(1),
            },
        };
        client.Plan.SemanticDiff.Add(new DeploymentSemanticDiffEntry
        {
            Kind = DeploymentSemanticDiffKind.ArtifactChanged,
            Path = "filter/forward",
            Before = "old",
            After = "new",
            HashDelta = "delta",
        });

        using DeploymentViewModel vm = CreateVm(
            client,
            ControllerConnectionState.Connected,
            selectNode: true,
            nodeId: nodeId);

        await vm.CreatePlanCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.CreatePlanCalls);
        Assert.Equal(planId, vm.PlanId);
        Assert.Equal("filter/forward", Assert.Single(vm.SemanticDiffRows).PathText);
        Assert.Contains("Plan", vm.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac4CreatePlanRequiresInventoryNodeSelection()
    {
        FakeDeploymentClient client = new();
        using DeploymentViewModel vm = CreateVm(
            client,
            ControllerConnectionState.Connected,
            selectNode: false);

        await vm.CreatePlanCommand.ExecuteAsync(null);

        Assert.Equal(0, client.CreatePlanCalls);
        Assert.Contains("Select a Node", vm.ErrorText, StringComparison.Ordinal);
        Assert.False(vm.HasForceApply);
        Assert.False(vm.HasRawRouterOsCommands);
    }

    [Fact]
    public void Ac5MainWindowBindsDeploymentPlanStartWatchRollbackAndRecovery()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Deployment.CreatePlanCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.StartCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.RollbackCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.RecoveryCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.SemanticDiffRows", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.SemanticDiffLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.ProgressLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.ArtifactLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Deployment.RecoveryFactsText", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/DeploymentGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/DeploymentGrpcHostTests.cs"));
        Assert.Contains("CreatePlanStartWatchAndRecoveryStatus", host, StringComparison.Ordinal);
        Assert.Contains("CreatePlanAndRollbackAreIdempotent", host, StringComparison.Ordinal);
    }

    private static DeploymentViewModel CreateVm(
        IDeploymentServiceClient client,
        ControllerConnectionState state,
        bool selectNode,
        Guid? nodeId = null)
    {
        Guid resolvedNodeId = nodeId ?? Guid.Parse("11111111-2222-3333-4444-555555555555");
        Guid deviceId = Guid.Parse("99999999-8888-7777-6666-555555555555");
        FakeConnection connection = new(state);
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
                    Id = resolvedNodeId,
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
        if (selectNode)
        {
            inventory.SelectedNode = site.Children[0];
        }

        return new DeploymentViewModel(client, connection, inventory);
    }

    private static Sha256 Hash(byte fill)
        => new() { Value = ByteString.CopyFrom(Enumerable.Repeat(fill, 32).ToArray()) };

    private static string ReadSource(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

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

    private sealed class FakeDeploymentClient : IDeploymentServiceClient
    {
        public DeploymentPlanSummary Plan { get; init; } = new();

        public int CreatePlanCalls { get; private set; }

        public Task<DeploymentPlanSummary> CreatePlanAsync(
            Guid nodeId,
            Sha256 logicalPolicyHash,
            Sha256 analysisBundleHash,
            Sha256 topologyHash,
            IReadOnlyList<DeploymentDevicePlanInput> devices,
            CancellationToken cancellationToken = default)
        {
            CreatePlanCalls++;
            return Task.FromResult(Plan);
        }

        public Task<DeploymentOperationSummary> StartAsync(
            Guid planId,
            Sha256 planHash,
            IReadOnlyList<DeploymentPacketPathPairFact> packetPathPairs,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<DeploymentProgress> WatchAsync(
            Guid operationId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task<DeploymentOperationSummary> RollbackAsync(
            Guid operationId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DeploymentRecoveryStatus> GetRecoveryStatusAsync(
            Guid nodeId,
            Guid? operationId = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeConnection(ControllerConnectionState state) : IControllerConnectionService
    {
        public GrpcChannel? Channel => null;

        public ControllerConnectionState State { get; } = state;

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
