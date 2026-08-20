using System.Globalization;
using System.Reflection;
using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Deployment;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-04 AC 1–11 (Safe Deployment Spec §17 / §19 / Compiler Spec §26).
/// </summary>
public sealed class DetachedChainStagingLivingSpecTests
{
    private const string ArtifactId = "0123456789abcdef";

    [Fact]
    public async Task Ac1DenyChainsAreStagedBeforeRootChains()
    {
        ChainArtifactDraft root = Chain(FilterChainArtifactRole.Root, "mfc:s:terminal", "drop");
        ChainArtifactDraft deny = Chain(FilterChainArtifactRole.CompanyDeny, "mfc:s:return:company-deny", "return");
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        DetachedChainsStagingResult result = await StageDetachedChainsUseCase.ExecuteAsync([root, deny], session);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(2, result.Chains.Count);
        Assert.Equal(deny.Name, result.Chains[0].ChainName);
        Assert.Equal(root.Name, result.Chains[1].ChainName);
        Assert.True(
            channel.Sent.FindIndex(s => s.Attributes.Any(a => a.Key == "chain" && a.Value == deny.Name))
            < channel.Sent.FindIndex(s => s.Attributes.Any(a => a.Key == "chain" && a.Value == root.Name)));
    }

    [Fact]
    public async Task Ac2ExistingExactChainIsReused()
    {
        ChainArtifactDraft desired = Chain(FilterChainArtifactRole.NodeDeny, "mfc:s:return:node-deny", "return");
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        SeedRule(channel, desired, desired.Rules[0]);
        FilterChainStagingResult result = await StageDetachedChainsUseCase.StageOneAsync(desired, session);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(FilterChainStagingAction.Reuse, result.Action);
        Assert.Equal(0, result.AddedCount);
        Assert.Empty(channel.Sent);
    }

    [Fact]
    public async Task Ac3ExactDesiredPrefixIsExtendedWithSuffix()
    {
        FilterRuleArtifact first = FilterRuleArtifact.Create(0, "return", "mfc:s:return:company-deny", structuralRole: "return");
        FilterRuleArtifact second = FilterRuleArtifact.Create(1, "drop", "mfc:s:terminal", structuralRole: "terminal");
        ChainArtifactDraft desired = new()
        {
            Family = IpAddressFamily.IPv4,
            BuiltInContext = FilterBuiltInContext.Input,
            Name = ManagedChainNamespace.ChainName(
                IpAddressFamily.IPv4,
                FilterBuiltInContext.Input,
                FilterChainArtifactRole.CompanyDeny,
                ArtifactId),
            Role = FilterChainArtifactRole.CompanyDeny,
            Rules = [first, second],
        };
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        SeedRule(channel, desired, first);
        FilterChainStagingResult result = await StageDetachedChainsUseCase.StageOneAsync(desired, session);
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(FilterChainStagingAction.AppendSuffix, result.Action);
        Assert.Equal(1, result.AddedCount);
        Assert.Contains(
            channel.Sent.SelectMany(static s => s.Attributes),
            static a => a.Key == "comment" && a.Value == "mfc:s:terminal");
    }

    [Fact]
    public void Ac4AnyOtherDivergenceCreatesCollision()
    {
        ChainArtifactDraft desired = Chain(FilterChainArtifactRole.CompanyDeny, "mfc:s:return:company-deny", "return");
        FilterChainStagingPlan plan = FilterChainCreateOrVerify.Plan(
            desired,
            [
                new ActualFilterChainRule(
                    desired.Name,
                    "drop",
                    comment: "mfc:s:return:company-deny"),
            ]);
        Assert.False(plan.Succeeded);
        Assert.Equal(DeploymentCodes.StagingResourceCollision, plan.Code);
    }

    [Fact]
    public void Ac5UnmanagedRuleInGeneratedChainBlocksStaging()
    {
        ChainArtifactDraft desired = Chain(FilterChainArtifactRole.CompanyDeny, "mfc:s:return:company-deny", "return");
        FilterChainStagingPlan plan = FilterChainCreateOrVerify.Plan(
            desired,
            [new ActualFilterChainRule(desired.Name, "return", comment: "operator-edit")]);
        Assert.False(plan.Succeeded);
        Assert.Equal(DeploymentCodes.StagingResourceCollision, plan.Code);
        Assert.Contains("Unmanaged", plan.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6RuleOrderIsVerified()
    {
        FilterRuleArtifact a = FilterRuleArtifact.Create(0, "return", "mfc:s:return:company-deny");
        FilterRuleArtifact b = FilterRuleArtifact.Create(1, "drop", "mfc:s:terminal");
        ChainArtifactDraft desired = new()
        {
            Family = IpAddressFamily.IPv4,
            BuiltInContext = FilterBuiltInContext.Input,
            Name = ManagedChainNamespace.ChainName(
                IpAddressFamily.IPv4,
                FilterBuiltInContext.Input,
                FilterChainArtifactRole.CompanyDeny,
                ArtifactId),
            Role = FilterChainArtifactRole.CompanyDeny,
            Rules = [a, b],
        };
        // Actual has swapped order → prefix diverge at ordinal 0
        FilterChainStagingPlan plan = FilterChainCreateOrVerify.Plan(
            desired,
            [
                new ActualFilterChainRule(desired.Name, "drop", comment: "mfc:s:terminal"),
                new ActualFilterChainRule(desired.Name, "return", comment: "mfc:s:return:company-deny"),
            ]);
        Assert.False(plan.Succeeded);
        Assert.Equal(DeploymentCodes.StagingPrefixDiverged, plan.Code);
    }

    [Fact]
    public void Ac7DisabledOrInvalidRuleBlocksStaging()
    {
        ChainArtifactDraft desired = Chain(FilterChainArtifactRole.CompanyDeny, "mfc:s:return:company-deny", "return");
        Assert.Equal(
            DeploymentCodes.StagingRuleInvalid,
            FilterChainCreateOrVerify.Plan(
                desired,
                [new ActualFilterChainRule(desired.Name, "return", comment: "mfc:s:return:company-deny", disabled: true)]).Code);
        Assert.Equal(
            DeploymentCodes.StagingRuleInvalid,
            FilterChainCreateOrVerify.Plan(
                desired,
                [new ActualFilterChainRule(desired.Name, "return", comment: "mfc:s:return:company-deny", invalid: true)]).Code);
    }

    [Fact]
    public void Ac8ActiveRootChainIsNotUsedAsStagingTarget()
    {
        ChainArtifactDraft desired = Chain(FilterChainArtifactRole.Root, "mfc:s:terminal", "drop");
        FilterChainStagingPlan builtin = FilterChainCreateOrVerify.Plan(
            new ChainArtifactDraft
            {
                Family = desired.Family,
                BuiltInContext = desired.BuiltInContext,
                Name = "input",
                Role = FilterChainArtifactRole.Root,
                Rules = desired.Rules,
            },
            []);
        Assert.False(builtin.Succeeded);
        Assert.Equal(DeploymentCodes.StagingRuleInvalid, builtin.Code);

        FilterChainStagingPlan active = FilterChainCreateOrVerify.Plan(
            desired,
            [],
            activeRootChainNames: new HashSet<string>(StringComparer.Ordinal) { desired.Name });
        Assert.False(active.Succeeded);
        Assert.Equal(DeploymentCodes.StagingResourceCollision, active.Code);
    }

    [Fact]
    public async Task Ac9FinalCanonicalChainHashMatches()
    {
        ChainArtifactDraft desired = Chain(FilterChainArtifactRole.CompanyDeny, "mfc:s:return:company-deny", "return");
        await using RouterOsDeploymentSession session = Session(out _);
        FilterChainStagingResult result = await StageDetachedChainsUseCase.StageOneAsync(desired, session);
        Assert.True(result.Succeeded, result.Message);
        Hash256 expected = RouterOsFilterArtifactIdentity.HashChainContent(
            desired.Family,
            desired.BuiltInContext,
            desired.Role,
            desired.Name,
            desired.Rules.OrderBy(static r => r.Ordinal).ToArray());
        Assert.Equal(expected.ToString(), result.ObservedChainHash!.ToString());
    }

    [Fact]
    public async Task Ac10PartialArtifactDoesNotReceiveStaged()
    {
        ChainArtifactDraft deny = Chain(FilterChainArtifactRole.CompanyDeny, "mfc:s:return:company-deny", "return");
        ChainArtifactDraft root = Chain(FilterChainArtifactRole.Root, "mfc:s:terminal", "drop");
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        // Pre-seed root with unmanaged rule so second chain fails after deny succeeds.
        channel.Seed(
            DeploymentReadSurface.Ipv4Filter,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [".id"] = "*bad",
                ["chain"] = root.Name,
                ["action"] = "accept",
                ["comment"] = "foreign",
                ["disabled"] = "no",
            });
        DetachedChainsStagingResult result = await StageDetachedChainsUseCase.ExecuteAsync([deny, root], session);
        Assert.False(result.Succeeded);
        Assert.False(result.ArtifactStaged);
        Assert.True(result.Chains[0].Succeeded);
        Assert.False(result.Chains[1].Succeeded);
    }

    [Fact]
    public async Task Ac11StagingReconnectRecoversWithCreateOrVerify()
    {
        FilterRuleArtifact first = FilterRuleArtifact.Create(0, "return", "mfc:s:return:company-deny");
        FilterRuleArtifact second = FilterRuleArtifact.Create(1, "drop", "mfc:s:terminal");
        ChainArtifactDraft desired = new()
        {
            Family = IpAddressFamily.IPv4,
            BuiltInContext = FilterBuiltInContext.Input,
            Name = ManagedChainNamespace.ChainName(
                IpAddressFamily.IPv4,
                FilterBuiltInContext.Input,
                FilterChainArtifactRole.CompanyDeny,
                ArtifactId),
            Role = FilterChainArtifactRole.CompanyDeny,
            Rules = [first, second],
        };
        await using RouterOsDeploymentSession session = Session(out RecordingChannel channel);
        SeedRule(channel, desired, first);
        FilterChainStagingResult firstPass = await StageDetachedChainsUseCase.StageOneAsync(desired, session);
        Assert.True(firstPass.Succeeded, firstPass.Message);
        Assert.Equal(1, firstPass.AddedCount);

        int sent = channel.Sent.Count;
        FilterChainStagingResult secondPass = await StageDetachedChainsUseCase.StageOneAsync(desired, session);
        Assert.True(secondPass.Succeeded, secondPass.Message);
        Assert.Equal(FilterChainStagingAction.Reuse, secondPass.Action);
        Assert.Equal(0, secondPass.AddedCount);
        Assert.Equal(sent, channel.Sent.Count);
        Assert.Null(typeof(StageDetachedChainsUseCase).GetMethod(
            "BlindAddAsync",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static));
    }

    private static ChainArtifactDraft Chain(FilterChainArtifactRole role, string comment, string action)
    {
        FilterRuleArtifact rule = FilterRuleArtifact.Create(0, action, comment, structuralRole: "s");
        return new ChainArtifactDraft
        {
            Family = IpAddressFamily.IPv4,
            BuiltInContext = FilterBuiltInContext.Input,
            Name = ManagedChainNamespace.ChainName(
                IpAddressFamily.IPv4,
                FilterBuiltInContext.Input,
                role,
                ArtifactId),
            Role = role,
            Rules = [rule],
        };
    }

    private static void SeedRule(RecordingChannel channel, ChainArtifactDraft chain, FilterRuleArtifact rule)
    {
        Dictionary<string, string> row = new(StringComparer.Ordinal)
        {
            [".id"] = "*" + rule.Comment.GetHashCode(StringComparison.Ordinal).ToString(CultureInfo.InvariantCulture),
            ["chain"] = chain.Name,
            ["action"] = rule.Action,
            ["comment"] = rule.Comment,
            ["disabled"] = "no",
            ["invalid"] = "no",
            ["dynamic"] = "false",
            ["log"] = rule.Log ? "yes" : "no",
        };
        foreach ((string key, string value) in rule.Matchers)
        {
            row[key] = value;
        }

        channel.Seed(DeploymentReadSurface.Ipv4Filter, row);
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
            if (path is DeploymentWritePath.Ipv4FilterAdd or DeploymentWritePath.Ipv6FilterAdd)
            {
                DeploymentReadSurface surface = path == DeploymentWritePath.Ipv4FilterAdd
                    ? DeploymentReadSurface.Ipv4Filter
                    : DeploymentReadSurface.Ipv6Filter;
                Dictionary<string, string> row = attributes.ToDictionary(
                    static a => a.Key,
                    static a => a.Value,
                    StringComparer.Ordinal);
                row[".id"] = "*" + _nextId.ToString(CultureInfo.InvariantCulture);
                row["disabled"] = row.GetValueOrDefault("disabled") ?? "no";
                row["invalid"] = "no";
                row["dynamic"] = "false";
                row["log"] = row.GetValueOrDefault("log") ?? "no";
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
