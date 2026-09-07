using Google.Protobuf;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-ONBOARD-01 / W7-83: Desktop Onboarding panel Living Spec vs OnboardingGrpcHost.</summary>
public sealed class DesktopOnboardingLivingSpecTests
{
    private static readonly string[] ExpectedWireMethods =
    [
        "CreatePlan",
        "GetRecoveryStatus",
        "Rollback",
        "Start",
        "ValidatePrerequisites",
        "Watch",
    ];

    [Fact]
    public void Ac1WireAndDesktopClientExposeValidatePlanStartWatchRollbackAndRecovery()
    {
        string[] methods = OnboardingService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(ExpectedWireMethods, methods);

        Type client = typeof(IOnboardingServiceClient);
        Assert.NotNull(client.GetMethod(nameof(IOnboardingServiceClient.ValidatePrerequisitesAsync)));
        Assert.NotNull(client.GetMethod(nameof(IOnboardingServiceClient.CreatePlanAsync)));
        Assert.NotNull(client.GetMethod(nameof(IOnboardingServiceClient.StartAsync)));
        Assert.NotNull(client.GetMethod(nameof(IOnboardingServiceClient.WatchAsync)));
        Assert.NotNull(client.GetMethod(nameof(IOnboardingServiceClient.RollbackAsync)));
        Assert.NotNull(client.GetMethod(nameof(IOnboardingServiceClient.GetRecoveryStatusAsync)));

        string source = ReadSource("src/Mfc.Desktop/Services/GrpcOnboardingServiceClient.cs");
        Assert.Contains("ValidatePrerequisitesAsync", source, StringComparison.Ordinal);
        Assert.Contains("CreatePlanAsync", source, StringComparison.Ordinal);
        Assert.Contains("StartAsync", source, StringComparison.Ordinal);
        Assert.Contains("WatchAsync", source, StringComparison.Ordinal);
        Assert.Contains("RollbackAsync", source, StringComparison.Ordinal);
        Assert.Contains("GetRecoveryStatusAsync", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ViewModelExposesValidatePlanStartRollbackRecoveryWithoutScriptOrArbitraryWrite()
    {
        using OnboardingViewModel vm = CreateVm(
            new FakeOnboardingClient(),
            ControllerConnectionState.Connected,
            selectNode: false);

        Assert.NotNull(vm.GetType().GetProperty(nameof(OnboardingViewModel.ValidateCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(OnboardingViewModel.CreatePlanCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(OnboardingViewModel.StartCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(OnboardingViewModel.RollbackCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(OnboardingViewModel.RecoveryCommand)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(OnboardingViewModel.Findings)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(OnboardingViewModel.Placements)));
        Assert.NotNull(vm.GetType().GetProperty(nameof(OnboardingViewModel.ProgressLines)));
        Assert.False(vm.HasScriptSource);
        Assert.False(vm.HasArbitraryWriteControls);
    }

    [Fact]
    public async Task Ac3ValidateLoadsFindingsWhenNodeSelected()
    {
        Guid nodeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeOnboardingClient client = new()
        {
            Report = new OnboardingPrerequisiteReport
            {
                Passed = false,
            },
        };
        client.Report.Findings.Add(new OnboardingFinding
        {
            Code = "MGMT_PATH",
            Severity = OnboardingFindingSeverity.Blocker,
            Message = "management path missing",
        });

        using OnboardingViewModel vm = CreateVm(
            client,
            ControllerConnectionState.Connected,
            selectNode: true,
            nodeId: nodeId);

        await vm.ValidateCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(1, client.ValidateCalls);
        Assert.Equal("MGMT_PATH", Assert.Single(vm.Findings).Code);
        Assert.Contains("blockers", vm.StatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ac4ValidateRequiresInventoryNodeSelection()
    {
        FakeOnboardingClient client = new();
        using OnboardingViewModel vm = CreateVm(
            client,
            ControllerConnectionState.Connected,
            selectNode: false);

        await vm.ValidateCommand.ExecuteAsync(null);

        Assert.Equal(0, client.ValidateCalls);
        Assert.Contains("Select a Node", vm.ErrorText, StringComparison.Ordinal);
        Assert.False(vm.HasScriptSource);
        Assert.False(vm.HasArbitraryWriteControls);
    }

    [Fact]
    public void Ac5MainWindowBindsOnboardingValidatePlanStartWatchRollbackAndRecovery()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("Onboarding.ValidateCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.CreatePlanCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.StartCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.RollbackCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.RecoveryCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.Findings", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.Placements", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.ProgressLines", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.RecoveryFactsText", axaml, StringComparison.Ordinal);
        Assert.Contains("Onboarding.StatusText", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/OnboardingGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/OnboardingGrpcHostTests.cs"));
        Assert.Contains("ValidateCreateStartWatchAndRecoveryStatus", host, StringComparison.Ordinal);
        Assert.Contains("CreatePlanAndRollbackAreIdempotent", host, StringComparison.Ordinal);
    }

    private static OnboardingViewModel CreateVm(
        IOnboardingServiceClient client,
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

        return new OnboardingViewModel(client, connection, inventory);
    }

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

    private sealed class FakeOnboardingClient : IOnboardingServiceClient
    {
        public OnboardingPrerequisiteReport Report { get; init; } = new() { Passed = true };

        public int ValidateCalls { get; private set; }

        public Task<OnboardingPrerequisiteReport> ValidatePrerequisitesAsync(
            Guid nodeId,
            IReadOnlyList<OnboardingDevicePrerequisiteFacts> devices,
            CancellationToken cancellationToken = default)
        {
            ValidateCalls++;
            return Task.FromResult(Report);
        }

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
            => throw new NotSupportedException();

        public async IAsyncEnumerable<OnboardingProgress> WatchAsync(
            Guid operationId,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
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
