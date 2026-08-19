using System.Globalization;
using System.Reflection;
using Mfc.Application.Onboarding;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.RouterOs.Onboarding;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>
/// Living Spec matrix for Issue Set M5-06 AC 1–12 (Onboarding Spec §12 / §27.2 / §32–§36).
/// </summary>
public sealed class OnboardingWatchdogLivingSpecTests
{
    private static OnboardingSystemNameFacts EmptyNames()
        => new() { ScriptNames = [], SchedulerNames = [] };

    [Fact]
    public async Task Ac1OneShotProofUsesFixedNoOpScript()
    {
        DeviceId deviceId = DeviceId.New();
        OnboardingWatchdogPlanResult planned = PlanOnboardingWatchdogUseCase.PlanProof(deviceId, EmptyNames());
        Assert.False(planned.HasBlockers);
        SchedulerProofPlan proof = Assert.IsType<SchedulerProofPlan>(planned.Proof);
        Assert.Equal(SchedulerCapabilityProof.NoOpSource, proof.ScriptSource);
        Assert.Equal(":local mfcCapabilityProbe true;", proof.ScriptSource);
        Assert.Equal(SchedulerCapabilityProof.SourceHash.ToString(), proof.ScriptSourceHash.ToString());
        Assert.StartsWith("mfc-cap-s-", proof.ScriptName, StringComparison.Ordinal);
        Assert.StartsWith("mfc-cap-d-", proof.SchedulerName, StringComparison.Ordinal);

        (OnboardingWatchdogWriter writer, RecordingChannel channel) = Writer();
        OnboardingWatchdogExecutionResult result = await writer.ProveSchedulerAsync(
            proof,
            new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero));
        Assert.True(result.Succeeded);
        Assert.Contains(channel.Sent, static s =>
            s.Path == OnboardingWritePath.SystemScriptAdd
            && s.Attributes.Any(a => a.Key == "source" && a.Value == SchedulerCapabilityProof.NoOpSource));
        Assert.Equal("/system/script/add", OnboardingWritePaths.Fixed(OnboardingWritePath.SystemScriptAdd));
        Assert.Equal("/system/scheduler/add", OnboardingWritePaths.Fixed(OnboardingWritePath.SystemSchedulerAdd));
        Assert.DoesNotContain(
            Enum.GetValues<OnboardingWritePath>(),
            static p => OnboardingWritePaths.Fixed(p).Contains("/move", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ac2RunCountMustEqualOne()
    {
        SchedulerProofPlan proof = RequiredProof();
        (OnboardingWatchdogWriter writer, _) = Writer();
        OnboardingWatchdogExecutionResult ok = await writer.ProveSchedulerAsync(proof, DateTimeOffset.UtcNow);
        Assert.True(ok.Succeeded);
        Assert.Equal(1, ok.RunCount);

        RecordingChannel stuck = new() { AutoCompleteRunCount = false };
        OnboardingWatchdogWriter failing = new(stuck, new ElapsedTimeoutProvider());
        OnboardingWatchdogExecutionResult failed = await failing.ProveSchedulerAsync(proof, DateTimeOffset.UtcNow);
        Assert.False(failed.Succeeded);
        Assert.Equal(OnboardingCodes.SchedulerCapabilityTestFailed, failed.Code);
    }

    [Fact]
    public async Task Ac3ProofResourcesAreRemoved()
    {
        SchedulerProofPlan proof = RequiredProof();
        (OnboardingWatchdogWriter writer, RecordingChannel channel) = Writer();
        OnboardingWatchdogExecutionResult result = await writer.ProveSchedulerAsync(proof, DateTimeOffset.UtcNow);
        Assert.True(result.Succeeded);
        Assert.Contains(channel.Sent, static s => s.Path == OnboardingWritePath.SystemSchedulerRemove);
        Assert.Contains(channel.Sent, static s => s.Path == OnboardingWritePath.SystemScriptRemove);
        Assert.DoesNotContain(proof.ScriptName, channel.ScriptNames());
        Assert.DoesNotContain(proof.SchedulerName, channel.SchedulerNames());
        Assert.Contains(result.Paths, static p => p == "/system/script/remove");
        Assert.Contains(result.Paths, static p => p == "/system/scheduler/remove");
    }

    [Fact]
    public async Task Ac4WatchdogHasDeadlineAndStartupSchedulers()
    {
        (OnboardingWatchdogBundle bundle, _, RecordingChannel channel) = await ArmedWatchdogAsync();
        Assert.StartsWith("mfc-ob-s-", bundle.ScriptName, StringComparison.Ordinal);
        Assert.StartsWith("mfc-ob-d-", bundle.DeadlineSchedulerName, StringComparison.Ordinal);
        Assert.StartsWith("mfc-ob-b-", bundle.StartupSchedulerName, StringComparison.Ordinal);
        Assert.Equal("startup", bundle.StartupAttributes.Single(static a => a.Key == "start-time").Value);
        Assert.Equal(2, channel.Sent.Count(static s => s.Path == OnboardingWritePath.SystemSchedulerAdd));
        Assert.Contains(channel.Sent, static s =>
            s.Path == OnboardingWritePath.SystemSchedulerAdd
            && s.Attributes.Any(a => a.Key == "start-time" && a.Value == "startup"));
        Assert.Contains(channel.Sent, static s =>
            s.Path == OnboardingWritePath.SystemSchedulerAdd
            && s.Attributes.Any(a => a.Key == "name" && a.Value.StartsWith("mfc-ob-d-", StringComparison.Ordinal)));
    }

    [Fact]
    public void Ac5ScriptSourceUsesFixedTemplate()
    {
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        string source = OnboardingWatchdogScript.Render(plan.RequiredAnchorSet);
        Assert.StartsWith(OnboardingWatchdogScript.Header, source, StringComparison.Ordinal);
        Assert.Contains("mfc:anchor:v1:4:i", source, StringComparison.Ordinal);
        Assert.Contains(
            BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            source,
            StringComparison.Ordinal);
        Assert.Contains("set $mfcId disabled=yes", source, StringComparison.Ordinal);
        Assert.DoesNotContain("disabled=no", source, StringComparison.Ordinal);
        Assert.DoesNotContain("jump-target=", source.Replace("[get $mfcId jump-target]", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6DontRequirePermissionsIsNo()
    {
        SchedulerProofPlan proof = RequiredProof();
        Assert.Equal("no", proof.ScriptAttributes.Single(static a => a.Key == "dont-require-permissions").Value);
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        OnboardingWatchdogPlanResult watchdog = PlanOnboardingWatchdogUseCase.PlanWatchdog(
            OnboardingOperationId.New(),
            plan,
            EmptyNames());
        Assert.Equal(
            "no",
            Assert.IsType<OnboardingWatchdogBundle>(watchdog.Watchdog)
                .ScriptAttributes.Single(static a => a.Key == "dont-require-permissions").Value);
        Assert.DoesNotContain(
            watchdog.Watchdog!.ScriptAttributes.Concat(watchdog.Watchdog.DeadlineAttributes).Concat(watchdog.Watchdog.StartupAttributes),
            static a => a.Key == "dont-require-permissions" && a.Value != "no");
    }

    [Fact]
    public void Ac7ScriptMayOnlyDisableExactBootstrapAnchors()
    {
        Assert.True(OnboardingWatchdogScript.ShouldDisable(
            matchCount: 1,
            chain: "input",
            action: "jump",
            jumpTarget: BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            expectedChain: "input",
            bootstrapRoot: BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            disabled: false));
        Assert.False(OnboardingWatchdogScript.ShouldDisable(
            2,
            "input",
            "jump",
            BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            "input",
            BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Input),
            disabled: false));
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        string source = OnboardingWatchdogScript.Render(plan.RequiredAnchorSet);
        Assert.Contains(":if ($mfcN = 1)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("remove", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("enable", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac8UserInputDoesNotEnterScript()
    {
        AnchorKey key = AnchorKey.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input);
        string source = OnboardingWatchdogScript.Render([key]);
        Assert.DoesNotContain("ticket", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/file", source, StringComparison.Ordinal);
        Assert.Contains(key.Marker, source, StringComparison.Ordinal);
        Assert.Contains(BootstrapArtifact.RootChainName(key.Family, key.Chain), source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac9StaleWatchdogIsNoOpForNonBootstrapTarget()
    {
        string bootstrap = BootstrapArtifact.RootChainName(IpAddressFamily.IPv4, FilterBuiltInContext.Forward);
        Assert.False(OnboardingWatchdogScript.ShouldDisable(
            matchCount: 1,
            chain: "forward",
            action: "jump",
            jumpTarget: "mfc4.f.a.managedartifact",
            expectedChain: "forward",
            bootstrapRoot: bootstrap,
            disabled: false));
        Assert.False(OnboardingWatchdogScript.ShouldDisable(
            1,
            "forward",
            "jump",
            bootstrap,
            "forward",
            bootstrap,
            disabled: true));
    }

    [Fact]
    public async Task Ac10SourceHashIsCheckedAfterAdd()
    {
        (OnboardingWatchdogBundle bundle, _, _) = await ArmedWatchdogAsync();
        Assert.Equal(bundle.ScriptSourceHash.ToString(), OnboardingWatchdogScript.HashSource(bundle.ScriptSource).ToString());

        RecordingChannel tamper = new() { TamperSource = "# tampered" };
        OnboardingWatchdogWriter failing = new(tamper);
        OnboardingWatchdogExecutionResult result = await failing.ArmWatchdogAsync(
            bundle,
            new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero));
        Assert.False(result.Succeeded);
        Assert.Equal(OnboardingCodes.OnboardingWatchdogInvalid, result.Code);
    }

    [Fact]
    public async Task Ac11TtlAndCommitMarginAreBounded()
    {
        Assert.Equal(60, OnboardingCodes.MinWatchdogTtl.TotalSeconds);
        Assert.Equal(180, OnboardingCodes.DefaultWatchdogTtl.TotalSeconds);
        Assert.Equal(600, OnboardingCodes.MaxWatchdogTtl.TotalSeconds);
        Assert.Equal(30, OnboardingCodes.MinCommitMargin.TotalSeconds);
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        Assert.InRange(plan.WatchdogTtl, OnboardingCodes.MinWatchdogTtl, OnboardingCodes.MaxWatchdogTtl);

        OnboardingWatchdogBundle bundle = Assert.IsType<OnboardingWatchdogBundle>(
            PlanOnboardingWatchdogUseCase.PlanWatchdog(OnboardingOperationId.New(), plan, EmptyNames()).Watchdog);
        (OnboardingWatchdogWriter writer, _) = Writer();
        OnboardingWatchdogExecutionResult tooClose = await writer.ArmWatchdogAsync(
            bundle,
            DateTimeOffset.UtcNow,
            remainingTtl: TimeSpan.FromSeconds(29));
        Assert.False(tooClose.Succeeded);
        Assert.Equal(OnboardingCodes.OnboardingWatchdogDeadlineTooClose, tooClose.Code);
    }

    [Fact]
    public void Ac12CollisionBlocksOperation()
    {
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        OnboardingOperationId operationId = OnboardingOperationId.New();
        string token = OnboardingWatchdogNames.Token(operationId, plan.DeviceId);
        OnboardingWatchdogPlanResult blocked = PlanOnboardingWatchdogUseCase.PlanWatchdog(
            operationId,
            plan,
            new OnboardingSystemNameFacts
            {
                ScriptNames = [OnboardingWatchdogNames.RollbackScript(token)],
                SchedulerNames = [],
            });
        Assert.True(blocked.HasBlockers);
        Assert.Null(blocked.Watchdog);
        Assert.Contains(blocked.Findings, static f => f.Code == OnboardingCodes.OnboardingWatchdogCollision);
        Assert.Contains(blocked.Findings, static f => f.Code == OnboardingCodes.MfcNamespaceCollision);

        OnboardingWatchdogPlanResult leftover = PlanOnboardingWatchdogUseCase.PlanProof(
            DeviceId.New(),
            new OnboardingSystemNameFacts { ScriptNames = ["mfc-cap-s-deadbeefdeadbeef"], SchedulerNames = [] });
        Assert.True(leftover.HasBlockers);
        Assert.Null(typeof(OnboardingWatchdogWriter).GetMethod("Execute"));
        Assert.DoesNotContain(
            typeof(OnboardingWatchdogWriter).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            static m => m.GetParameters().Any(p => p.ParameterType == typeof(string) && p.Name is "command" or "path"));
        Assert.Null(typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes()
            .FirstOrDefault(static t => t.Namespace == "Mfc.RouterOs.Write"));
    }

    private static SchedulerProofPlan RequiredProof()
    {
        OnboardingWatchdogPlanResult planned = PlanOnboardingWatchdogUseCase.PlanProof(DeviceId.New(), EmptyNames());
        return Assert.IsType<SchedulerProofPlan>(planned.Proof);
    }

    private static async Task<(OnboardingWatchdogBundle Bundle, OnboardingWatchdogWriter Writer, RecordingChannel Channel)> ArmedWatchdogAsync()
    {
        DeviceOnboardingPlan plan = OnboardingTestFactory.DevicePlan(DeviceId.New(), NodeKind.Router);
        OnboardingWatchdogBundle bundle = Assert.IsType<OnboardingWatchdogBundle>(
            PlanOnboardingWatchdogUseCase.PlanWatchdog(OnboardingOperationId.New(), plan, EmptyNames()).Watchdog);
        (OnboardingWatchdogWriter writer, RecordingChannel channel) = Writer();
        OnboardingWatchdogExecutionResult result = await writer.ArmWatchdogAsync(
            bundle,
            new DateTimeOffset(2026, 8, 19, 12, 0, 0, TimeSpan.Zero));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(bundle.ScriptSourceHash.ToString(), result.ObservedSourceHash?.ToString());
        return (bundle, writer, channel);
    }

    private static (OnboardingWatchdogWriter Writer, RecordingChannel Channel) Writer()
    {
        RecordingChannel channel = new();
        return (new OnboardingWatchdogWriter(channel), channel);
    }

    private sealed class ElapsedTimeoutProvider : TimeProvider
    {
        private int _stamps;

        public override long TimestampFrequency => 1;

        public override long GetTimestamp()
            => Interlocked.Increment(ref _stamps) == 1
                ? 0
                : (long)OnboardingCodes.SchedulerProofTimeout.TotalSeconds + 1;
    }

    private sealed class RecordingChannel : IOnboardingWriteChannel
    {
        private readonly List<Dictionary<string, string>> _scripts = [];
        private readonly List<Dictionary<string, string>> _schedulers = [];
        private int _nextId = 1;

        public bool AutoCompleteRunCount { get; init; } = true;

        public string? TamperSource { get; init; }

        public List<(OnboardingWritePath Path, IReadOnlyList<KeyValuePair<string, string>> Attributes)> Sent { get; } = [];

        public IEnumerable<string> ScriptNames() => _scripts.Select(static r => r["name"]);

        public IEnumerable<string> SchedulerNames() => _schedulers.Select(static r => r["name"]);

        public Task<IReadOnlyDictionary<string, string>> SendAsync(
            OnboardingWritePath path,
            IReadOnlyList<KeyValuePair<string, string>> attributes,
            CancellationToken cancellationToken = default)
        {
            Sent.Add((path, attributes.ToArray()));
            string fixedPath = OnboardingWritePaths.Fixed(path);
            if (fixedPath == "/system/script/add")
            {
                Dictionary<string, string> row = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
                row[".id"] = NextId();
                if (TamperSource is not null)
                {
                    row["source"] = TamperSource;
                }

                _scripts.Add(row);
            }
            else if (fixedPath == "/system/scheduler/add")
            {
                Dictionary<string, string> row = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
                row[".id"] = NextId();
                row["run-count"] = AutoCompleteRunCount ? "1" : "0";
                _schedulers.Add(row);
            }
            else if (fixedPath == "/system/script/remove")
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                _scripts.RemoveAll(r => r[".id"] == id);
            }
            else if (fixedPath == "/system/scheduler/remove")
            {
                string id = attributes.Single(static a => a.Key == ".id").Value;
                _schedulers.RemoveAll(r => r[".id"] == id);
            }

            return Task.FromResult<IReadOnlyDictionary<string, string>>(
                new Dictionary<string, string>(StringComparer.Ordinal) { ["ok"] = "true" });
        }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintAsync(
            IpAddressFamily family,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, string>>>([]);

        public Task<IReadOnlyList<IReadOnlyDictionary<string, string>>> PrintSystemAsync(
            OnboardingSystemSurface surface,
            CancellationToken cancellationToken = default)
        {
            List<Dictionary<string, string>> rows = surface == OnboardingSystemSurface.Script ? _scripts : _schedulers;
            IReadOnlyList<IReadOnlyDictionary<string, string>> copy = rows
                .Select(static r => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(r, StringComparer.Ordinal))
                .ToArray();
            return Task.FromResult(copy);
        }

        private string NextId() => string.Create(CultureInfo.InvariantCulture, $"*{_nextId++}");
    }
}
