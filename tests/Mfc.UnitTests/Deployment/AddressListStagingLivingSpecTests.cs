using System.Globalization;
using System.Reflection;
using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Deployment;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-03 AC 1–10 (Safe Deployment Spec §18 / Compiler Spec §26–§27).
/// </summary>
public sealed class AddressListStagingLivingSpecTests
{
    [Fact]
    public async Task Ac1ExistingExactListIsReused()
    {
        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "10.0.0.0/8", "192.0.2.1");
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        SeedList(channel, desired, "10.0.0.0/8", "192.0.2.1");
        AddressListStagingResult result = await StageAddressListUseCase.ExecuteAsync(desired, session);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(AddressListStagingAction.Reuse, result.Action);
        Assert.Equal(0, result.AddedCount);
        Assert.Empty(channel.Sent);
        Assert.True(result.ReadBeforeWriteCount >= 1);
    }

    [Fact]
    public async Task Ac2ExactSubsetIsSupplementedWithMissingEntries()
    {
        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "10.0.0.0/8", "192.0.2.1", "198.51.100.0/24");
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        SeedList(channel, desired, "10.0.0.0/8", "192.0.2.1");
        AddressListStagingResult result = await StageAddressListUseCase.ExecuteAsync(desired, session);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(AddressListStagingAction.AddMissing, result.Action);
        Assert.Equal(1, result.AddedCount);
        Assert.Contains(channel.Sent, static s => s.Path == DeploymentWritePath.Ipv4AddressListAdd);
        Assert.Contains(
            channel.Sent.SelectMany(static s => s.Attributes),
            static a => a.Key == "address" && a.Value == "198.51.100.0/24");
        Assert.DoesNotContain(channel.Sent, static s => DeploymentWritePaths.IsFilterSet(s.Path));
    }

    [Fact]
    public void Ac3ExtraOrDivergentEntryCreatesCollision()
    {
        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "10.0.0.0/8");
        AddressListStagingPlan plan = AddressListCreateOrVerify.Plan(
            desired,
            [
                new ActualAddressListEntry(desired.Name, "10.0.0.0/8"),
                new ActualAddressListEntry(desired.Name, "203.0.113.1"),
            ]);
        Assert.False(plan.Succeeded);
        Assert.Equal(DeploymentCodes.StagingResourceCollision, plan.Code);
        Assert.Empty(plan.MissingAddresses);
    }

    [Fact]
    public void Ac4UnmanagedEntryInGeneratedListBlocksStaging()
    {
        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "10.0.0.0/8");
        AddressListStagingPlan plan = AddressListCreateOrVerify.Plan(
            desired,
            [new ActualAddressListEntry(desired.Name, "10.0.0.0/8", comment: "operator-note")]);
        Assert.False(plan.Succeeded);
        Assert.Equal(DeploymentCodes.StagingResourceCollision, plan.Code);
        Assert.Contains("Unmanaged", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ac5BlindAddRetryAfterConnectionLossIsAbsent()
    {
        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "10.0.0.0/8", "192.0.2.1");
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        // First entry already present (simulates add that completed before reply loss).
        SeedList(channel, desired, "10.0.0.0/8");
        AddressListStagingResult first = await StageAddressListUseCase.ExecuteAsync(desired, session);
        Assert.True(first.Succeeded, first.Message);
        Assert.Equal(1, first.AddedCount);

        // Second attempt must read again and only add the still-missing address — never re-add present ones.
        int sentBefore = channel.Sent.Count;
        AddressListStagingResult second = await StageAddressListUseCase.ExecuteAsync(desired, session);
        Assert.True(second.Succeeded, second.Message);
        Assert.Equal(AddressListStagingAction.Reuse, second.Action);
        Assert.Equal(0, second.AddedCount);
        Assert.Equal(sentBefore, channel.Sent.Count);
        Assert.True(second.ReadBeforeWriteCount >= 1);
        Assert.Null(typeof(StageAddressListUseCase).GetMethod(
            "BlindAddAsync",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
    }

    [Fact]
    public async Task Ac6ActualStateIsReadBeforeRetry()
    {
        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "10.0.0.0/8", "192.0.2.1");
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        SeedList(channel, desired, "10.0.0.0/8");
        AddressListStagingResult result = await StageAddressListUseCase.ExecuteAsync(desired, session);
        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.ReadBeforeWriteCount >= 1);
        Assert.True(channel.PrintCount >= result.ReadBeforeWriteCount);
        // First read happens before any add.
        Assert.True(channel.PrintCount > channel.Sent.Count);
    }

    [Fact]
    public async Task Ac7FinalUnorderedContentHashIsVerified()
    {
        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "192.0.2.1", "10.0.0.0/8");
        await using RouterOsDeploymentSession session = Session(out _);
        AddressListStagingResult result = await StageAddressListUseCase.ExecuteAsync(desired, session);
        Assert.True(result.Succeeded, result.Message);
        Hash256 expected = RouterOsFilterArtifactIdentity.HashAddressListContent(
            desired.Family,
            desired.Entries.OrderBy(static e => e.Address, StringComparer.Ordinal).ToArray());
        Assert.Equal(expected.ToString(), result.ObservedContentHash!.ToString());
    }

    [Fact]
    public void Ac8DynamicEntryInGeneratedListBlocksStaging()
    {
        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "10.0.0.0/8");
        AddressListStagingPlan dynamicRow = AddressListCreateOrVerify.Plan(
            desired,
            [new ActualAddressListEntry(desired.Name, "10.0.0.0/8", dynamic: true)]);
        Assert.False(dynamicRow.Succeeded);
        Assert.Equal(DeploymentCodes.StagingRuleInvalid, dynamicRow.Code);

        AddressListStagingPlan timed = AddressListCreateOrVerify.Plan(
            desired,
            [new ActualAddressListEntry(desired.Name, "10.0.0.0/8", timeout: "1d")]);
        Assert.False(timed.Succeeded);
        Assert.Equal(DeploymentCodes.StagingRuleInvalid, timed.Code);
    }

    [Fact]
    public void Ac9ActiveListsAreNotEditedInPlace()
    {
        Assert.DoesNotContain(
            Enum.GetValues<DeploymentWritePath>(),
            static p =>
            {
                string path = DeploymentWritePaths.Fixed(p);
                return path.Contains("address-list/set", StringComparison.Ordinal)
                       || path.Contains("address-list/remove", StringComparison.Ordinal);
            });
        Assert.Null(typeof(IRouterOsDeploymentSession).GetMethod("SetAddressListEntryAsync"));
        Assert.Null(typeof(IRouterOsDeploymentSession).GetMethod("RemoveAddressListEntryAsync"));

        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "10.0.0.0/8");
        AddressListStagingPlan plan = AddressListCreateOrVerify.Plan(
            desired,
            [
                new ActualAddressListEntry(desired.Name, "10.0.0.0/8"),
                new ActualAddressListEntry(desired.Name, "203.0.113.9"),
            ]);
        Assert.False(plan.Succeeded);
        Assert.Contains("no in-place edit", plan.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(plan.MissingAddresses);
    }

    [Fact]
    public void Ac10RecordAndPayloadLimitsAreApplied()
    {
        AddressListArtifactDraft desired = Desired(IpAddressFamily.IPv4, "10.0.0.0/8", "192.0.2.1");
        AddressListStagingPlan overEntries = AddressListCreateOrVerify.Plan(
            desired,
            [],
            new AddressListCompileLimits { MaxLists = 10, MaxEntriesPerFamily = 1 });
        Assert.False(overEntries.Succeeded);
        Assert.Equal(DeploymentCodes.StagingLimitExceeded, overEntries.Code);

        Assert.Throws<DomainInvariantException>(() =>
            new AddressListCompileLimits
            {
                MaxLists = AddressListCompileLimits.LayoutV1MaxLists + 1,
                MaxEntriesPerFamily = 10,
            }.EnsureWithinLayoutV1());
    }

    private static AddressListArtifactDraft Desired(IpAddressFamily family, params string[] addresses)
    {
        AddressListEntryArtifact[] entries = addresses
            .Select(AddressListEntryArtifact.Create)
            .OrderBy(static e => e.Address, StringComparer.Ordinal)
            .ToArray();
        Hash256 hash = RouterOsFilterArtifactIdentity.HashAddressListContent(family, entries);
        string name = ManagedChainNamespace.AddressListName(
            family,
            hash.ToString()[..RouterOsFilterArtifactIdentity.ArtifactIdHexLength]);
        return new AddressListArtifactDraft
        {
            Family = family,
            Name = name,
            Entries = entries,
        };
    }

    private static void SeedList(RecordingChannel channel, AddressListArtifactDraft desired, params string[] addresses)
    {
        DeploymentReadSurface surface = desired.Family == IpAddressFamily.IPv4
            ? DeploymentReadSurface.Ipv4AddressList
            : DeploymentReadSurface.Ipv6AddressList;
        foreach (string address in addresses)
        {
            channel.Seed(
                surface,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [".id"] = "*" + address.GetHashCode(StringComparison.Ordinal).ToString(CultureInfo.InvariantCulture),
                    ["list"] = desired.Name,
                    ["address"] = address,
                    ["dynamic"] = "false",
                });
        }
    }

    private static RouterOsDeploymentSession Session(out RecordingChannel channel)
    {
        channel = new RecordingChannel();
        return new RouterOsDeploymentSession(channel);
    }

    private sealed class RecordingChannel : IDeploymentWriteChannel
    {
        private readonly Dictionary<DeploymentReadSurface, List<Dictionary<string, string>>> _prints = new();
        private int _nextId = 1;

        public List<(DeploymentWritePath Path, IReadOnlyList<KeyValuePair<string, string>> Attributes)> Sent { get; } = [];

        public int PrintCount { get; private set; }

        public void Seed(DeploymentReadSurface surface, Dictionary<string, string> row)
        {
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                list = [];
                _prints[surface] = list;
            }

            list.Add(new Dictionary<string, string>(row, StringComparer.Ordinal));
        }

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            DeploymentWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((path, attributes.ToArray()));
            if (DeploymentWritePaths.IsAddressListAdd(path))
            {
                DeploymentReadSurface surface = path == DeploymentWritePath.Ipv4AddressListAdd
                    ? DeploymentReadSurface.Ipv4AddressList
                    : DeploymentReadSurface.Ipv6AddressList;
                Dictionary<string, string> row = attributes.ToDictionary(
                    static a => a.Key,
                    static a => a.Value,
                    StringComparer.Ordinal);
                row[".id"] = "*" + _nextId.ToString(CultureInfo.InvariantCulture);
                row["dynamic"] = "false";
                _nextId++;
                Seed(surface, row);
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal));
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
            DeploymentReadSurface surface,
            CancellationToken cancellationToken = default)
        {
            PrintCount++;
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>([]);
            }

            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>(
                list.Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                    .ToArray());
        }

        public Task<ChannelPingResult> PingAsync(
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChannelPingResult { Sent = 0, Received = 0 });
    }
}
