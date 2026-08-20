using System.Globalization;
using System.Reflection;
using Mfc.Application.Deployment;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Deployment;
using Xunit;

namespace Mfc.UnitTests.Deployment;

/// <summary>
/// Living Spec matrix for Issue Set M4-05 AC 1–12 (Safe Deployment Spec §22–§27).
/// </summary>
public sealed class DeploymentWatchdogLivingSpecTests
{
    private static DeploymentSystemNameFacts EmptyNames()
        => new() { ScriptNames = [], SchedulerNames = [] };

    [Fact]
    public async Task Ac1WatchdogHasScriptDeadlineAndStartupSchedulers()
    {
        (DeploymentWatchdogBundle bundle, _, RecordingChannel channel) = await ArmedAsync();
        Assert.StartsWith("mfc-rb-s-", bundle.ScriptName, StringComparison.Ordinal);
        Assert.StartsWith("mfc-rb-d-", bundle.DeadlineSchedulerName, StringComparison.Ordinal);
        Assert.StartsWith("mfc-rb-b-", bundle.StartupSchedulerName, StringComparison.Ordinal);
        Assert.Equal("startup", bundle.StartupAttributes.Single(static a => a.Key == "start-time").Value);
        Assert.Equal(2, channel.Sent.Count(static s => s.Path == DeploymentWritePath.SystemSchedulerAdd));
        Assert.Contains(channel.Sent, static s => s.Path == DeploymentWritePath.SystemScriptAdd);
    }

    [Fact]
    public void Ac2ScriptUsesFixedTemplate()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        string source = DeploymentWatchdogScript.Render(
            plan.OldAnchorTargets,
            plan.NewAnchorTargets,
            plan.AnchorRollbackOrder);
        Assert.StartsWith(DeploymentWatchdogScript.Header, source, StringComparison.Ordinal);
        Assert.Contains("mfc:anchor:v1:4:i", source, StringComparison.Ordinal);
        Assert.Contains("jump-target=", source, StringComparison.Ordinal);
        Assert.Contains("mfc-watchdog-abort", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ticket", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac3ScriptChecksOldAndNewTargetSet()
    {
        Assert.Equal(
            DeploymentWatchdogRestoreAction.RestoreOld,
            DeploymentWatchdogScript.DecideRestore("mfc4.i.r.new", "mfc4.i.r.old", "mfc4.i.r.new"));
        Assert.Equal(
            DeploymentWatchdogRestoreAction.NoOp,
            DeploymentWatchdogScript.DecideRestore("mfc4.i.r.old", "mfc4.i.r.old", "mfc4.i.r.new"));
        Assert.True(DeploymentWatchdogScript.ShouldApplySet(
            1, "input", "jump", disabled: false, "input", "mfc4.i.r.new", "mfc4.i.r.old", "mfc4.i.r.new"));
    }

    [Fact]
    public void Ac4UnknownThirdTargetIsNotChanged()
    {
        Assert.Equal(
            DeploymentWatchdogRestoreAction.Abort,
            DeploymentWatchdogScript.DecideRestore("mfc4.i.r.other", "mfc4.i.r.old", "mfc4.i.r.new"));
        Assert.False(DeploymentWatchdogScript.ShouldApplySet(
            1, "input", "jump", false, "input", "mfc4.i.r.other", "mfc4.i.r.old", "mfc4.i.r.new"));
    }

    [Fact]
    public void Ac5StaleWatchdogDoesNotRollBackLaterArtifact()
    {
        // Later artifact target is neither old nor new → abort (no set).
        Assert.Equal(
            DeploymentWatchdogRestoreAction.Abort,
            DeploymentWatchdogScript.DecideRestore(
                "mfc4.i.r.0123456789abcdef",
                "mfc4.i.r.aaaaaaaaaaaaaaaa",
                "mfc4.i.r.bbbbbbbbbbbbbbbb"));
    }

    [Fact]
    public void Ac6UserTextDoesNotEnterScript()
    {
        DeviceDeploymentPlan plan = DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        string source = DeploymentWatchdogScript.Render(
            plan.OldAnchorTargets,
            plan.NewAnchorTargets,
            plan.AnchorRollbackOrder);
        Assert.DoesNotContain("username", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/file", source, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<Domain.DomainInvariantException>(() =>
            DeploymentWatchdogScript.Render(
                [new AnchorTarget(AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input), "bad name")],
                [new AnchorTarget(AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input), "mfc4.i.r.ok")],
                [AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input)]));
    }

    [Fact]
    public void Ac7DontRequirePermissionsIsNo()
    {
        DeploymentWatchdogPlanResult planned = PlanDeploymentWatchdogUseCase.PlanWatchdog(
            DeploymentOperationId.New(),
            DeploymentTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router),
            EmptyNames());
        DeploymentWatchdogBundle bundle = Assert.IsType<DeploymentWatchdogBundle>(planned.Watchdog);
        Assert.Equal("no", bundle.ScriptAttributes.Single(static a => a.Key == "dont-require-permissions").Value);
        Assert.Equal(DeploymentWatchdogScript.Policy, bundle.ScriptAttributes.Single(static a => a.Key == "policy").Value);
        Assert.DoesNotContain(
            bundle.ScriptAttributes.Concat(bundle.DeadlineAttributes).Concat(bundle.StartupAttributes),
            static a => a.Key == "dont-require-permissions" && a.Value != "no");
    }

    [Fact]
    public async Task Ac8ScriptSourceHashIsVerified()
    {
        (DeploymentWatchdogBundle bundle, DeploymentWatchdogExecutionResult result, _) = await ArmedAsync();
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(bundle.ScriptSourceHash.ToString(), result.ObservedSourceHash!.ToString());
        Assert.Equal(
            DeploymentWatchdogScript.HashSource(bundle.ScriptSource).ToString(),
            bundle.ScriptSourceHash.ToString());
    }

    [Fact]
    public async Task Ac9TtlAndCommitMarginAreBounded()
    {
        Assert.Equal(60, DeploymentCodes.MinRollbackTtl.TotalSeconds);
        Assert.Equal(600, DeploymentCodes.MaxRollbackTtl.TotalSeconds);
        Assert.Equal(30, DeploymentCodes.MinCommitMargin.TotalSeconds);
        Assert.Throws<Domain.DomainInvariantException>(() =>
            WithTtl(DeviceId.New(), TimeSpan.FromSeconds(20)));
        Assert.Throws<Domain.DomainInvariantException>(() =>
            WithTtl(DeviceId.New(), TimeSpan.FromSeconds(601)));

        (DeploymentWatchdogBundle bundle, _, RecordingChannel channel) = await ArmedAsync();
        DeploymentWatchdogWriter writer = new(new RouterOsDeploymentSession(channel));
        DeploymentWatchdogExecutionResult tooClose = await writer.ArmWatchdogAsync(
            bundle,
            DateTimeOffset.UtcNow,
            remainingTtl: TimeSpan.FromSeconds(10));
        Assert.False(tooClose.Succeeded);
        Assert.Equal(DeploymentCodes.WatchdogDeadlineTooClose, tooClose.Code);
    }

    [Fact]
    public void Ac10AllDeviceWatchdogsMustBeArmedBeforeVrrpActivation()
    {
        DeviceId a = DeviceId.New();
        DeviceId b = DeviceId.New();
        DeploymentWatchdogPlanResult blocked = PlanDeploymentWatchdogUseCase.EnsureAllDevicesArmed(
            [a, b],
            new HashSet<DeviceId> { a });
        Assert.True(blocked.HasBlockers);
        Assert.Contains(blocked.Findings, static f => f.Code == DeploymentCodes.WatchdogNotArmed);

        DeploymentWatchdogPlanResult ok = PlanDeploymentWatchdogUseCase.EnsureAllDevicesArmed(
            [a, b],
            new HashSet<DeviceId> { a, b });
        Assert.False(ok.HasBlockers);
    }

    [Fact]
    public async Task Ac11SchedulerDisablingHasReadBack()
    {
        (DeploymentWatchdogBundle bundle, _, RecordingChannel channel) = await ArmedAsync();
        DeploymentWatchdogWriter writer = new(new RouterOsDeploymentSession(channel));
        DeploymentWatchdogExecutionResult disarm = await writer.DisarmWatchdogAsync(bundle);
        Assert.True(disarm.Succeeded, disarm.Error);
        Assert.Contains(channel.Sent, static s => s.Path == DeploymentWritePath.SystemSchedulerSet);
        Assert.Contains(
            channel.Sent.Where(static s => s.Path == DeploymentWritePath.SystemSchedulerSet)
                .SelectMany(static s => s.Attributes),
            static a => a.Key == "disabled" && a.Value == "yes");
        Assert.DoesNotContain(
            channel.Sent.Where(static s => s.Path == DeploymentWritePath.SystemSchedulerSet)
                .SelectMany(static s => s.Attributes),
            static a => a.Key is "on-event" or "name");
    }

    [Fact]
    public async Task Ac12CleanupIsIdempotent()
    {
        DeploymentOperationId deploymentId = DeploymentOperationId.New();
        DeviceId deviceId = DeviceId.New();
        (DeploymentWatchdogBundle bundle, _, RecordingChannel channel) = await ArmedAsync(deploymentId, deviceId);
        DeploymentWatchdogWriter writer = new(new RouterOsDeploymentSession(channel));
        DeploymentWatchdogExecutionResult first = await writer.CleanupWatchdogAsync(deploymentId, deviceId);
        Assert.True(first.Succeeded, first.Error);
        DeploymentWatchdogExecutionResult second = await writer.CleanupWatchdogAsync(deploymentId, deviceId);
        Assert.True(second.Succeeded, second.Error);
        Assert.Null(typeof(DeploymentWatchdogWriter).GetMethod("Execute"));
        Assert.DoesNotContain(
            typeof(DeploymentWatchdogWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            static m => m.GetParameters().Any(p => p.ParameterType == typeof(string) && p.Name is "command" or "menu"));
        Assert.DoesNotContain(bundle.ScriptName, channel.ScriptNames());
    }

    private static DeviceDeploymentPlan WithTtl(DeviceId deviceId, TimeSpan ttl)
    {
        DeviceDeploymentPlan basePlan = DeploymentTestFactory.DevicePlan(deviceId, NodeKind.Router);
        return DeviceDeploymentPlan.Create(
            basePlan.DeviceId,
            basePlan.ExpectedRouterOsVersion,
            basePlan.ExpectedCapabilityHash,
            basePlan.ExpectedConfigurationHash,
            basePlan.ExpectedCompatibilityHash,
            basePlan.ExpectedGuardContextHash,
            basePlan.ExpectedAnchorContextHash,
            basePlan.OldArtifactHash,
            basePlan.OldAnchorTargets,
            basePlan.NewArtifactHash,
            basePlan.NewAnchorTargets,
            basePlan.AnchorActivationOrder,
            basePlan.AnchorRollbackOrder,
            basePlan.TransitionStateHashes,
            ttl,
            basePlan.Probes);
    }

    private static async Task<(DeploymentWatchdogBundle Bundle, DeploymentWatchdogExecutionResult Result, RecordingChannel Channel)> ArmedAsync(
        DeploymentOperationId? deploymentId = null,
        DeviceId? deviceId = null)
    {
        DeploymentOperationId id = deploymentId ?? DeploymentOperationId.New();
        DeviceId device = deviceId ?? DeviceId.New();
        DeploymentWatchdogPlanResult planned = PlanDeploymentWatchdogUseCase.PlanWatchdog(
            id,
            DeploymentTestFactory.DevicePlan(device, NodeKind.Router),
            EmptyNames());
        Assert.False(planned.HasBlockers, string.Join(';', planned.Findings.Select(static f => f.Message)));
        DeploymentWatchdogBundle bundle = Assert.IsType<DeploymentWatchdogBundle>(planned.Watchdog);
        RecordingChannel channel = new();
        DeploymentWatchdogWriter writer = new(new RouterOsDeploymentSession(channel));
        DeploymentWatchdogExecutionResult result = await writer.ArmWatchdogAsync(
            bundle,
            new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.Zero));
        Assert.True(result.Succeeded, result.Error);
        return (bundle, result, channel);
    }

    private sealed class RecordingChannel : IDeploymentWriteChannel
    {
        private readonly Dictionary<DeploymentReadSurface, List<Dictionary<string, string>>> _prints = new();
        private int _nextId = 1;

        public List<(DeploymentWritePath Path, IReadOnlyList<KeyValuePair<string, string>> Attributes)> Sent { get; } = [];

        public IEnumerable<string> ScriptNames()
            => _prints.GetValueOrDefault(DeploymentReadSurface.Script)?.Select(r => r["name"]) ?? [];

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            DeploymentWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((path, attributes.ToArray()));
            string fixedPath = DeploymentWritePaths.Fixed(path);
            if (fixedPath.EndsWith("/add", StringComparison.Ordinal))
            {
                DeploymentReadSurface surface = path switch
                {
                    DeploymentWritePath.SystemScriptAdd => DeploymentReadSurface.Script,
                    DeploymentWritePath.SystemSchedulerAdd => DeploymentReadSurface.Scheduler,
                    _ => throw new InvalidOperationException(path.ToString()),
                };
                Dictionary<string, string> row = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
                row[".id"] = "*" + _nextId.ToString(CultureInfo.InvariantCulture);
                _nextId++;
                Seed(surface, row);
            }
            else if (path == DeploymentWritePath.SystemSchedulerSet)
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                Dictionary<string, string> row = _prints[DeploymentReadSurface.Scheduler].Single(r => r[".id"] == id);
                foreach ((string key, string value) in attributes.Where(static a => a.Key != ".id"))
                {
                    row[key] = value;
                }
            }
            else if (path is DeploymentWritePath.SystemScriptRemove or DeploymentWritePath.SystemSchedulerRemove)
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                DeploymentReadSurface surface = path == DeploymentWritePath.SystemScriptRemove
                    ? DeploymentReadSurface.Script
                    : DeploymentReadSurface.Scheduler;
                _prints[surface].RemoveAll(r => r[".id"] == id);
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

        private void Seed(DeploymentReadSurface surface, Dictionary<string, string> row)
        {
            if (!_prints.TryGetValue(surface, out List<Dictionary<string, string>>? list))
            {
                list = [];
                _prints[surface] = list;
            }

            list.Add(row);
        }
    }
}
