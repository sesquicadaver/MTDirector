using System.Globalization;
using System.Reflection;
using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Deployment;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-06 AC 1–11 (Safe Deployment Spec §28–§31).
/// </summary>
public sealed class AnchorActivationLivingSpecTests
{
    [Fact]
    public void Ac1AllIntermediateOldNewCombinationsAreAnalyzed()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        TransitionStateValidationResult result = PlanTransitionStatesUseCase.ValidateTransitions(
            plan.AnchorActivationOrder,
            plan.OldAnchorTargets,
            plan.NewAnchorTargets,
            TransitionStateValidator.AllSafeEvidence(plan.AnchorActivationOrder.Count));
        Assert.False(result.HasBlockers);
        Assert.Equal(plan.AnchorActivationOrder.Count + 1, result.States.Count);
        Assert.Equal(0, result.States[0].Targets.Count(t =>
            string.Equals(
                t.JumpTarget,
                plan.NewAnchorTargets.Single(n => n.Key.Equals(t.Key)).JumpTarget,
                StringComparison.Ordinal)));
        Assert.Equal(
            plan.AnchorActivationOrder.Count,
            result.States[^1].Targets.Count(t =>
                string.Equals(
                    t.JumpTarget,
                    plan.NewAnchorTargets.Single(n => n.Key.Equals(t.Key)).JumpTarget,
                    StringComparison.Ordinal)));
    }

    [Fact]
    public void Ac2UnsafeStateBlocksPlan()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        List<TransitionStateEvidence> evidence = TransitionStateValidator.AllSafeEvidence(plan.AnchorActivationOrder.Count).ToList();
        evidence[1] = new TransitionStateEvidence(1, isSafe: false, DeploymentCodes.TransitionStateUnsafe);
        TransitionStateValidationResult result = PlanTransitionStatesUseCase.ValidateTransitions(
            plan.AnchorActivationOrder,
            plan.OldAnchorTargets,
            plan.NewAnchorTargets,
            evidence);
        Assert.True(result.HasBlockers);
        Assert.Contains(result.Findings, static f => f.Code == DeploymentCodes.TransitionStateUnsafe);
    }

    [Fact]
    public void Ac3ManagementCriticalAnchorsAreActivatedLast()
    {
        IReadOnlyList<AnchorKey> keys = RequiredAnchorSet.For(NodeKind.Router, includeIpv6: false);
        IReadOnlyList<AnchorKey> order = PlanTransitionStatesUseCase.PlanActivationOrder(keys);
        Assert.Equal(FilterBuiltInContext.Forward, order[0].Chain);
        Assert.Equal(FilterBuiltInContext.Input, order[^1].Chain);
        Assert.True(DeploymentAnchorOrder.IsManagementCriticalLast(order));
        Assert.False(DeploymentAnchorOrder.IsManagementCriticalLast(
        [
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Forward),
        ]));
    }

    [Fact]
    public async Task Ac4AnchorIsReReadBeforeEverySet()
    {
        (DeviceDeploymentPlan plan, RecordingChannel channel, RouterOsDeploymentSession session) = SeededSession();
        int printsBefore = channel.PrintCount;
        AnchorActivationResult result = await ActivateAnchorsUseCase.ExecuteAsync(
            plan,
            session,
            static () => TimeSpan.FromSeconds(120));
        Assert.True(result.Succeeded, result.Message);
        Assert.True(result.ReadCount > plan.AnchorActivationOrder.Count);
        Assert.True(channel.PrintCount > printsBefore);
        Assert.Equal(plan.AnchorActivationOrder.Count, channel.Sent.Count(static s => DeploymentWritePaths.IsFilterSet(s.Path)));
    }

    [Fact]
    public async Task Ac5CurrentTargetMustEqualExpectedOldOrDesiredNew()
    {
        (DeviceDeploymentPlan plan, RecordingChannel channel, RouterOsDeploymentSession session) = SeededSession();
        AnchorKey first = plan.AnchorActivationOrder[0];
        Dictionary<string, string> row = channel.FindAnchor(first)!;
        row["jump-target"] = plan.NewAnchorTargets.Single(t => t.Key.Equals(first)).JumpTarget;
        AnchorActivationResult result = await ActivateAnchorsUseCase.ExecuteAsync(
            plan,
            session,
            static () => TimeSpan.FromSeconds(120));
        Assert.True(result.Succeeded, result.Message);
        Assert.Contains(result.Journal, e => e.Key.Equals(first) && e.Code == "ANCHOR_ALREADY_APPLIED");
    }

    [Fact]
    public async Task Ac6UnknownTargetStartsRecovery()
    {
        (DeviceDeploymentPlan plan, RecordingChannel channel, RouterOsDeploymentSession session) = SeededSession();
        AnchorKey first = plan.AnchorActivationOrder[0];
        channel.FindAnchor(first)!["jump-target"] = "mfc4.f.r.third-party";
        AnchorActivationResult result = await ActivateAnchorsUseCase.ExecuteAsync(
            plan,
            session,
            static () => TimeSpan.FromSeconds(120));
        Assert.False(result.Succeeded);
        Assert.True(result.RecoveryRequired);
        Assert.Equal(DeploymentCodes.RecoveryRequired, result.Code);
        Assert.DoesNotContain(channel.Sent, static s => DeploymentWritePaths.IsFilterSet(s.Path));
    }

    [Fact]
    public async Task Ac7UnknownSetResultIsVerifiedByRead()
    {
        (DeviceDeploymentPlan plan, RecordingChannel channel, RouterOsDeploymentSession session) = SeededSession();
        channel.FailNextFilterSet = true;
        AnchorKey first = plan.AnchorActivationOrder[0];
        string desired = plan.NewAnchorTargets.Single(t => t.Key.Equals(first)).JumpTarget;
        // Simulate set that actually applied despite transport error: mutate before Fail returns.
        channel.OnFilterSet = (id, attrs) =>
        {
            Dictionary<string, string> row = channel.Ipv4Filters().Single(r => r[".id"] == id);
            row["jump-target"] = attrs.Single(a => a.Key == "jump-target").Value;
        };
        AnchorActivationResult result = await ActivateAnchorsUseCase.ExecuteAsync(
            plan,
            session,
            static () => TimeSpan.FromSeconds(120));
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(desired, channel.FindAnchor(first)!["jump-target"]);
    }

    [Fact]
    public async Task Ac8BlindSetRetryIsAbsent()
    {
        MethodInfo? method = typeof(ActivateAnchorsUseCase).GetMethod(
            nameof(ActivateAnchorsUseCase.ExecuteAsync),
            BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(method);
        Assert.False(AnchorActivationPlanner.AllowsControlledRetry(
            AnchorActivationPlanner.ClassifyAfterUnknownSet("other", "old", "new")));
        Assert.True(AnchorActivationPlanner.AllowsControlledRetry(
            AnchorActivationPlanner.ClassifyAfterUnknownSet("old", "old", "new")));

        (DeviceDeploymentPlan plan, RecordingChannel channel, RouterOsDeploymentSession session) = SeededSession();
        channel.FailNextFilterSet = true;
        // Leave jump-target as old so controlled retry is allowed once — not blind.
        AnchorActivationResult result = await ActivateAnchorsUseCase.ExecuteAsync(
            plan,
            session,
            static () => TimeSpan.FromSeconds(120));
        Assert.True(result.Succeeded, result.Message);
        int sets = channel.Sent.Count(static s => DeploymentWritePaths.IsFilterSet(s.Path));
        Assert.Equal(plan.AnchorActivationOrder.Count + 1, sets);
    }

    [Fact]
    public async Task Ac9WritesPerDeviceAreSequential()
    {
        (DeviceDeploymentPlan plan, _, RouterOsDeploymentSession session) = SeededSession();
        AnchorActivationResult result = await ActivateAnchorsUseCase.ExecuteAsync(
            plan,
            session,
            static () => TimeSpan.FromSeconds(120));
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(
            plan.AnchorActivationOrder.Select(static k => k.Marker).ToArray(),
            result.Journal.Where(static j => j.State == DeploymentStepState.Verified)
                .Select(static j => j.Key.Marker)
                .ToArray());
    }

    [Fact]
    public async Task Ac10WatchdogMarginIsCheckedAfterEachAnchor()
    {
        (DeviceDeploymentPlan plan, _, RouterOsDeploymentSession session) = SeededSession();
        int calls = 0;
        AnchorActivationResult result = await ActivateAnchorsUseCase.ExecuteAsync(
            plan,
            session,
            () =>
            {
                calls++;
                return calls <= 1 ? TimeSpan.FromSeconds(120) : TimeSpan.FromSeconds(10);
            });
        Assert.False(result.Succeeded);
        Assert.Equal(DeploymentCodes.WatchdogDeadlineTooClose, result.Code);
        Assert.True(calls >= 2);
    }

    [Fact]
    public async Task Ac11StepJournalRecordsIntentAndVerifiedResult()
    {
        (DeviceDeploymentPlan plan, _, RouterOsDeploymentSession session) = SeededSession();
        AnchorActivationResult result = await ActivateAnchorsUseCase.ExecuteAsync(
            plan,
            session,
            static () => TimeSpan.FromSeconds(120));
        Assert.True(result.Succeeded, result.Message);
        Assert.All(result.Journal, static e =>
        {
            Assert.Equal(DeploymentStepState.Verified, e.State);
            Assert.NotNull(e.ObservedBefore);
            Assert.NotNull(e.ObservedAfter);
            Assert.NotNull(e.ExpectedBeforeHash);
            Assert.NotNull(e.DesiredAfterHash);
        });
        Assert.Contains(result.Journal, static e => e.Code == "ANCHOR_SET_VERIFIED" || e.Code == "ANCHOR_ALREADY_APPLIED");
    }

    private static (DeviceDeploymentPlan Plan, RecordingChannel Channel, RouterOsDeploymentSession Session) SeededSession()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        RecordingChannel channel = new();
        int id = 1;
        foreach (AnchorTarget target in plan.OldAnchorTargets)
        {
            string chain = target.Key.Chain switch
            {
                FilterBuiltInContext.Input => "input",
                FilterBuiltInContext.Forward => "forward",
                FilterBuiltInContext.Output => "output",
                _ => "input",
            };
            channel.Seed(
                DeploymentReadSurface.Ipv4Filter,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [".id"] = "*" + id.ToString(CultureInfo.InvariantCulture),
                    ["chain"] = chain,
                    ["action"] = "jump",
                    ["jump-target"] = target.JumpTarget,
                    ["comment"] = target.Key.Marker,
                    ["disabled"] = "false",
                });
            id++;
        }

        return (plan, channel, new RouterOsDeploymentSession(channel));
    }

    private sealed class RecordingChannel : IDeploymentWriteChannel
    {
        private readonly Dictionary<DeploymentReadSurface, List<Dictionary<string, string>>> _prints = new();

        public List<(DeploymentWritePath Path, IReadOnlyList<KeyValuePair<string, string>> Attributes)> Sent { get; } = [];

        public int PrintCount { get; private set; }

        public bool FailNextFilterSet { get; set; }

        public Action<string, IReadOnlyList<KeyValuePair<string, string>>>? OnFilterSet { get; set; }

        public void Seed(DeploymentReadSurface surface, Dictionary<string, string> row)
        {
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                list = [];
                _prints[surface] = list;
            }

            list.Add(new Dictionary<string, string>(row, StringComparer.Ordinal));
        }

        public Dictionary<string, string>? FindAnchor(AnchorKey key)
        {
            string chain = key.Chain switch
            {
                FilterBuiltInContext.Input => "input",
                FilterBuiltInContext.Forward => "forward",
                FilterBuiltInContext.Output => "output",
                _ => string.Empty,
            };
            return Ipv4Filters().FirstOrDefault(r =>
                string.Equals(r.GetValueOrDefault("comment"), key.Marker, StringComparison.Ordinal)
                && string.Equals(r.GetValueOrDefault("chain"), chain, StringComparison.OrdinalIgnoreCase));
        }

        public List<Dictionary<string, string>> Ipv4Filters()
            => _prints.GetValueOrDefault(DeploymentReadSurface.Ipv4Filter) ?? [];

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            DeploymentWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((path, attributes.ToArray()));
            if (DeploymentWritePaths.IsFilterSet(path))
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                OnFilterSet?.Invoke(id, attributes);
                if (FailNextFilterSet)
                {
                    FailNextFilterSet = false;
                    throw new InvalidOperationException("simulated transport loss during set");
                }

                Dictionary<string, string> row = _prints[DeploymentReadSurface.Ipv4Filter].Single(r => r[".id"] == id);
                foreach ((string key, string value) in attributes.Where(static a => a.Key != ".id"))
                {
                    row[key] = value;
                }

                return Task.FromResult<IReadOnlyDictionary<string, string>>(
                    new Dictionary<string, string>(StringComparer.Ordinal));
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
