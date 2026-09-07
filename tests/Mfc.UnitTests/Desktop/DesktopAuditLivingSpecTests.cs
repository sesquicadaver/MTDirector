using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-AUDIT-01 / W7-67: Desktop Audit panel Living Spec vs AuditGrpcHost (List-only).</summary>
public sealed class DesktopAuditLivingSpecTests
{
    [Fact]
    public void Ac1WireAndDesktopClientAreListOnly()
    {
        string[] methods = AuditService.Descriptor.Methods.Select(static m => m.Name).OrderBy(n => n).ToArray();
        Assert.Equal(["ListAuditEvents"], methods);

        Assert.NotNull(typeof(IAuditServiceClient).GetMethod(nameof(IAuditServiceClient.ListAuditEventsAsync)));
        Assert.Null(typeof(IAuditServiceClient).GetMethod("AppendAuditEventAsync"));
        Assert.Null(typeof(IAuditServiceClient).GetMethod("WriteAuditEventAsync"));

        string client = ReadSource("src/Mfc.Desktop/Services/GrpcAuditServiceClient.cs");
        Assert.Contains("ListAuditEventsAsync", client, StringComparison.Ordinal);
        Assert.Contains("ListAuditEventsRequest", client, StringComparison.Ordinal);
        Assert.DoesNotContain("Append", client, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac2ViewModelIsReadOnlyWithNoWriteCommands()
    {
        using AuditViewModel vm = CreateVm(new FakeAuditClient([]), ControllerConnectionState.Connected);
        Assert.True(vm.IsReadOnly);
        Assert.False(vm.HasWriteCommands);
        Assert.NotNull(vm.GetType().GetProperty(nameof(AuditViewModel.RefreshCommand)));
        Assert.Null(vm.GetType().GetProperty("AppendCommand"));
        Assert.Null(vm.GetType().GetProperty("WriteCommand"));
        Assert.Null(vm.GetType().GetMethod("AppendAsync"));
    }

    [Fact]
    public async Task Ac3RefreshLoadsNewestFirstEventsFromClient()
    {
        Guid newer = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Guid older = Guid.Parse("11111111-2222-3333-4444-555555555555");
        FakeAuditClient client = new(
        [
            CreateEvent(newer, "tester", "audit.second", """{"n":2}"""),
            CreateEvent(older, "tester", "audit.first", """{"n":1}"""),
        ]);
        using AuditViewModel vm = CreateVm(client, ControllerConnectionState.Connected);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Null(vm.ErrorText);
        Assert.Equal(2, vm.Events.Count);
        Assert.Equal(newer, vm.Events[0].Id);
        Assert.Equal("audit.second", vm.Events[0].Action);
        Assert.Equal(older, vm.Events[1].Id);
        Assert.Equal(newer, vm.SelectedEvent!.Id);
        Assert.Contains("newest first", vm.StatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(100u, client.LastPageSize);
    }

    [Fact]
    public async Task Ac4RefreshRequiresConnectedController()
    {
        FakeAuditClient client = new([]);
        using AuditViewModel vm = CreateVm(client, ControllerConnectionState.Disconnected);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(0, client.CallCount);
        Assert.Contains("Connect to Controller", vm.ErrorText, StringComparison.Ordinal);
        Assert.Empty(vm.Events);
    }

    [Fact]
    public void Ac5MainWindowBindsAuditListPayloadAndRefresh()
    {
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("<!-- Audit (read-only) -->", axaml, StringComparison.Ordinal);
        Assert.Contains("Audit.RefreshCommand", axaml, StringComparison.Ordinal);
        Assert.Contains("Audit.Events", axaml, StringComparison.Ordinal);
        Assert.Contains("Audit.SelectedEvent", axaml, StringComparison.Ordinal);
        Assert.Contains("Audit.SelectedEvent.PayloadJson", axaml, StringComparison.Ordinal);
        Assert.Contains("vm:AuditEventListItem", axaml, StringComparison.Ordinal);
        Assert.Contains("newest first", axaml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("No mutate actions", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6HostContractLivingSpecRemainsPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/AuditGrpcHostTests.cs")));
        string host = File.ReadAllText(Path.Combine(root, "tests/Mfc.IntegrationTests/Controller/AuditGrpcHostTests.cs"));
        Assert.Contains("ListAuditEventsAfterAppendIsNewestFirstAndPaged", host, StringComparison.Ordinal);
        Assert.Contains("AuditServiceHasNoMutationRpcsOnWire", host, StringComparison.Ordinal);
    }

    private static AuditViewModel CreateVm(IAuditServiceClient client, ControllerConnectionState state)
        => new(client, new FakeConnection(state));

    private static AuditEvent CreateEvent(Guid id, string actor, string action, string payload)
        => new()
        {
            Id = DesktopProtoUuid.FromGuid(id),
            OccurredAt = Timestamp.FromDateTimeOffset(DateTimeOffset.UtcNow),
            Actor = actor,
            Action = action,
            PayloadJson = payload,
        };

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

    private sealed class FakeAuditClient(IReadOnlyList<AuditEvent> events) : IAuditServiceClient
    {
        public int CallCount { get; private set; }

        public uint LastPageSize { get; private set; }

        public Task<IReadOnlyList<AuditEvent>> ListAuditEventsAsync(
            uint pageSize = 100,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastPageSize = pageSize;
            return Task.FromResult(events);
        }
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
}
