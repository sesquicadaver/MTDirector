using System.Globalization;
using System.Reflection;
using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Onboarding;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>
/// Living Spec matrix for Issue Set M5-05 AC 1–12 (Onboarding Spec §23 / §27).
/// </summary>
public sealed class OnboardingBootstrapWriterLivingSpecTests
{
    [Fact]
    public void Ac1WritePathsAreCompileTimeAllowlisted()
    {
        Assert.Equal("/ip/firewall/filter/add", OnboardingWritePaths.Fixed(OnboardingWritePath.Ipv4FilterAdd));
        Assert.Equal("/ipv6/firewall/filter/add", OnboardingWritePaths.Fixed(OnboardingWritePath.Ipv6FilterAdd));
        Assert.Equal("/ip/firewall/filter/set", OnboardingWritePaths.Fixed(OnboardingWritePath.Ipv4FilterSet));
        Assert.Equal("/ip/firewall/filter/remove", OnboardingWritePaths.Fixed(OnboardingWritePath.Ipv4FilterRemove));
        Assert.Equal(
            6,
            Enum.GetValues<OnboardingWritePath>().Count(static p =>
                OnboardingWritePaths.Fixed(p).Contains("/firewall/filter", StringComparison.Ordinal)));
        Assert.DoesNotContain(
            Enum.GetValues<OnboardingWritePath>(),
            static p => OnboardingWritePaths.Fixed(p).Contains("/move", StringComparison.Ordinal));
    }

    [Fact]
    public void Ac2BootstrapRootContainsExactlyOneUnconditionalReturn()
    {
        OnboardingBootstrapWrite ret = OnboardingBootstrapWrite.AddBootstrapReturn(
            IpAddressFamily.IPv4,
            FilterBuiltInContext.Input);
        Assert.Equal(4, ret.Attributes.Count);
        Assert.Equal("return", ret.Attributes.Single(static a => a.Key == "action").Value);
        Assert.DoesNotContain(ret.Attributes, static a => a.Key is "jump-target" or "log" or "src-address");
        OnboardingBootstrapWritePlanner.AssertSingleUnconditionalReturn(ret);
    }

    [Fact]
    public void Ac3BootstrapArtifactIdMatchesSpec()
    {
        Assert.Equal("8e40b9d4d67d42d6", BootstrapArtifact.ArtifactId);
        Assert.Equal(BootstrapArtifact.Hash.ToString(), BootstrapArtifact.ComputeSeedHash().ToString());
        Assert.Equal(
            "mfc4.i.r.8e40b9d4d67d42d6",
            BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input));
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        Assert.Equal(BootstrapArtifact.Hash.ToString(), plan.BootstrapArtifactHash.ToString());
    }

    [Fact]
    public async Task Ac4PermanentAnchorIsCreatedDisabled()
    {
        (OnboardingBootstrapWriter writer, RecordingChannel channel) = Writer();
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        OnboardingBootstrapWrite add = OnboardingBootstrapWrite.AddDisabledAnchor(plan.AnchorPlacements[0]);
        OnboardingBootstrapWriteExecutionResult result = await writer.ApplyAsync(add, []);
        Assert.True(result.Succeeded);
        Assert.Equal("yes", add.Attributes.Single(static a => a.Key == "disabled").Value);
        Assert.Equal("yes", result.ReadBack["disabled"]);
        Assert.Contains(channel.Sent, static s => s.Attributes.Any(a => a.Key == "disabled" && a.Value == "yes"));
    }

    [Fact]
    public async Task Ac5AnchorTargetIsBootstrapRoot()
    {
        (OnboardingBootstrapWriter writer, _) = Writer();
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        AnchorPlacement placement = plan.AnchorPlacements[0];
        OnboardingBootstrapWrite add = OnboardingBootstrapWrite.AddDisabledAnchor(placement);
        OnboardingBootstrapWriteExecutionResult result = await writer.ApplyAsync(add, []);
        Assert.True(result.Succeeded);
        Assert.Equal(
            BootstrapArtifact.RootChainName(placement.Family, placement.Chain),
            add.Attributes.Single(static a => a.Key == "jump-target").Value);
        Assert.Equal(add.RootChainName, result.ReadBack["jump-target"]);
    }

    [Fact]
    public async Task Ac6PlaceBeforeOrAppendIsUsed()
    {
        (OnboardingBootstrapWriter writer, RecordingChannel channel) = Writer();
        DeviceOnboardingPlan appendPlan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        OnboardingBootstrapWrite append = OnboardingBootstrapWrite.AddDisabledAnchor(appendPlan.AnchorPlacements[0]);
        await writer.ApplyAsync(append, []);
        Assert.DoesNotContain(channel.Sent[0].Attributes, static a => a.Key == "place-before");

        ActualFilterRule reference = ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "input",
            0,
            "accept",
            comment: "ref",
            knownMatchers: new Dictionary<string, string>(StringComparer.Ordinal) { ["src-address"] = "10.0.0.0/8" });
        Hash256 fp = FilterRuleFingerprint.Compute(reference);
        channel.SeedPrint(reference, itemId: "*3");
        AnchorPlacement before = AnchorPlacement.Create(
            IpAddressFamily.IPv4,
            FilterBuiltInContext.Input,
            AnchorPlacementMode.BeforeStaticRule,
            expectedAnchorOrdinal: 0,
            referenceRuleFingerprint: fp,
            referenceOccurrenceRank: 0);
        OnboardingBootstrapWriteExecutionResult placed = await writer.ApplyAsync(
            OnboardingBootstrapWrite.AddDisabledAnchor(before),
            [reference]);
        Assert.True(placed.Succeeded);
        Assert.Contains(placed.SentAttributes, static a => a.Key == "place-before" && a.Value == "*3");
    }

    [Fact]
    public void Ac7MoveIsNotUsed()
    {
        Assert.DoesNotContain(
            Enum.GetValues<OnboardingWritePath>(),
            static p => OnboardingWritePaths.Fixed(p).Contains("/move", StringComparison.Ordinal));
        Assert.Null(typeof(OnboardingBootstrapWriter).GetMethod("Move"));
        Assert.Null(typeof(OnboardingBootstrapWriter).GetMethod("MoveAsync"));
    }

    [Fact]
    public async Task Ac8SetAllowsOnlyAnchorDisabled()
    {
        (OnboardingBootstrapWriter writer, RecordingChannel channel) = Writer();
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        AnchorPlacement placement = plan.AnchorPlacements[0];
        Assert.True((await writer.ApplyAsync(OnboardingBootstrapWrite.AddDisabledAnchor(placement), [])).Succeeded);
        OnboardingBootstrapWrite set = OnboardingBootstrapWrite.SetAnchorDisabled(
            placement.Family,
            placement.Chain,
            disabled: false);
        Assert.Equal("disabled", Assert.Single(set.Attributes).Key);
        OnboardingBootstrapWriteExecutionResult result = await writer.ApplyAsync(set, []);
        Assert.True(result.Succeeded);
        Assert.Equal("/ip/firewall/filter/set", result.Path);
        Assert.Equal(2, result.SentAttributes.Count);
        Assert.Contains(result.SentAttributes, static a => a.Key == ".id");
        Assert.Contains(result.SentAttributes, static a => a.Key == "disabled" && a.Value == "no");
        Assert.DoesNotContain(result.SentAttributes, static a => a.Key == "jump-target");
        Assert.DoesNotContain(channel.Sent, static s => s.Attributes.Any(a => a.Key == "jump-target") && OnboardingWritePaths.Fixed(s.Path).EndsWith("/set", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac9RemoveAllowsOnlyExactOnboardingResources()
    {
        (OnboardingBootstrapWriter writer, _) = Writer();
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        AnchorPlacement placement = plan.AnchorPlacements[0];
        Assert.True((await writer.ApplyAsync(OnboardingBootstrapWrite.AddBootstrapReturn(placement.Family, placement.Chain), [])).Succeeded);
        Assert.True((await writer.ApplyAsync(OnboardingBootstrapWrite.AddDisabledAnchor(placement), [])).Succeeded);
        OnboardingBootstrapWriteExecutionResult removeAnchor = await writer.ApplyAsync(
            OnboardingBootstrapWrite.RemoveDisabledAnchor(placement.Family, placement.Chain),
            []);
        Assert.True(removeAnchor.Succeeded);
        OnboardingBootstrapWriteExecutionResult removeReturn = await writer.ApplyAsync(
            OnboardingBootstrapWrite.RemoveBootstrapReturn(placement.Family, placement.Chain),
            []);
        Assert.True(removeReturn.Succeeded);
    }

    [Fact]
    public async Task Ac10EachWriteHasActualStateReadBack()
    {
        (OnboardingBootstrapWriter writer, RecordingChannel channel) = Writer();
        OnboardingBootstrapWrite ret = OnboardingBootstrapWrite.AddBootstrapReturn(
            IpAddressFamily.IPv4,
            FilterBuiltInContext.Input);
        OnboardingBootstrapWriteExecutionResult result = await writer.ApplyAsync(ret, []);
        Assert.True(result.Succeeded);
        Assert.NotEmpty(result.ReadBack);
        Assert.True(channel.PrintCount >= 1);
        Assert.Equal(BootstrapArtifact.ReturnComment, result.ReadBack["comment"]);
    }

    [Fact]
    public void Ac11GenericCommandMethodIsAbsent()
    {
        Assert.Null(typeof(OnboardingBootstrapWriter).GetMethod("Execute"));
        Assert.DoesNotContain(
            typeof(OnboardingBootstrapWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            static m => m.GetParameters().Any(p => p.ParameterType == typeof(string) && p.Name is "command" or "path"));
        Assert.Null(typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes()
            .FirstOrDefault(static t => t.Namespace == "Mfc.RouterOs.Write"));
        Assert.Contains(
            typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes(),
            static t => t == typeof(OnboardingBootstrapWriter));
    }

    [Fact]
    public void Ac12NamespaceCollisionBlocksOperation()
    {
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        ActualFilterRule collision = ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            0,
            "return",
            comment: BootstrapArtifact.ReturnComment);
        OnboardingBootstrapWritePlan blocked = PlanOnboardingBootstrapWritesUseCase.Execute(plan, [collision]);
        Assert.True(blocked.HasBlockers);
        Assert.Empty(blocked.Writes);
        Assert.Contains(blocked.Findings, static f => f.Code == OnboardingCodes.MfcNamespaceCollision);
        Assert.Contains(blocked.Findings, static f => f.Code == OnboardingCodes.BootstrapRootCollision);
    }

    private static (OnboardingBootstrapWriter Writer, RecordingChannel Channel) Writer()
    {
        RecordingChannel channel = new();
        return (new OnboardingBootstrapWriter(channel), channel);
    }

    private sealed class RecordingChannel : IOnboardingWriteChannel
    {
        private readonly List<Dictionary<string, string>> _rows = [];
        private int _nextId = 1;

        public List<(OnboardingWritePath Path, IReadOnlyList<KeyValuePair<string, string>> Attributes)> Sent { get; } = [];

        public int PrintCount { get; private set; }

        public void SeedPrint(ActualFilterRule rule, string itemId)
        {
            _rows.Add(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [".id"] = itemId,
                ["chain"] = rule.Chain,
                ["action"] = rule.Action ?? string.Empty,
                ["disabled"] = rule.Disabled ? "yes" : "no",
                ["comment"] = rule.Comment ?? string.Empty,
                ["ordinal"] = rule.Ordinal.ToString(CultureInfo.InvariantCulture),
            });
        }

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            OnboardingWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((path, attributes.ToArray()));
            string fixedPath = OnboardingWritePaths.Fixed(path);
            if (fixedPath.EndsWith("/add", StringComparison.Ordinal))
            {
                Dictionary<string, string> row = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
                row[".id"] = string.Create(CultureInfo.InvariantCulture, $"*{_nextId++}");
                row.Remove("place-before");
                _rows.Add(row);
            }
            else if (fixedPath.EndsWith("/set", StringComparison.Ordinal))
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                Dictionary<string, string> row = _rows.Single(r => r[".id"] == id);
                foreach (KeyValuePair<string, string> pair in attributes.Where(static a => a.Key != ".id"))
                {
                    row[pair.Key] = pair.Value;
                }
            }
            else if (fixedPath.EndsWith("/remove", StringComparison.Ordinal))
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                _rows.RemoveAll(r => r[".id"] == id);
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["ok"] = "true" });
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
            IpAddressFamily family,
            CancellationToken cancellationToken = default)
        {
            PrintCount++;
            IReadOnlyList<IReadOnlyDictionary<string, string>> copy = _rows
                .Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                .ToArray();
            return Task.FromResult(copy);
        }
    }
}
