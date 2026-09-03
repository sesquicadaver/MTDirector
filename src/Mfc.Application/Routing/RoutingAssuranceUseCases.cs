using System.Globalization;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Common;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Routing;

/// <summary>Upserts routing assurance configuration + operational snapshots for one Device.</summary>
public sealed class UpsertRoutingAssuranceStateCommand
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }

    public required RoutingConfigurationSnapshot Configuration { get; init; }

    public required RoutingOperationalSnapshot OperationalState { get; init; }

    /// <summary>Declarative route expectations evaluated when traces are available (M7.1-06).</summary>
    public IReadOnlyList<RouteExpectation> RouteExpectations { get; init; } = [];

    /// <summary>Computed by <see cref="RouteExpectationEvaluator"/> when expectations and traces are present.</summary>
    public IReadOnlyList<RouteFinding> RouteFindings { get; init; } = [];

    /// <summary>Precomputed traces; when empty and <see cref="TraceQueries"/> is set, traces are computed on upsert.</summary>
    public IReadOnlyList<RouteResolutionTrace> ResolutionTraces { get; init; } = [];

    /// <summary>Optional probes analyzed by <see cref="RouteResolutionTraceEngine"/> during upsert.</summary>
    public IReadOnlyList<RouteResolutionQuery> TraceQueries { get; init; } = [];

    /// <summary>Network path latency profiles bound to traces on upsert (M7.1-08).</summary>
    public IReadOnlyList<NetworkPathProfile> NetworkPathProfiles { get; init; } = [];

    /// <summary>Optional scripted latency measurements evaluated against profiles and traces (M7.1-08).</summary>
    public IReadOnlyList<NetworkPathLatencyEvaluationInput> LatencyEvaluations { get; init; } = [];
}

/// <summary>Stores routing assurance state shell (M7.1-02).</summary>
public sealed class UpsertRoutingAssuranceStateUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IRoutingAssuranceStateStore _states;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public UpsertRoutingAssuranceStateUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IRoutingAssuranceStateStore states,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _devices = devices;
        _states = states;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<RoutingAssuranceStateView>> ExecuteAsync(
        UpsertRoutingAssuranceStateCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        DeviceId deviceId = new(command.DeviceId);
        Device? device = await _devices.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (device is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Device '{command.DeviceId}' not found."));
        }

        if (command.Configuration is null || command.OperationalState is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.Validation("Configuration and operational_state snapshots are required."));
        }

        RoutingOperationalSnapshot operationalState = DynamicRouteOriginAnalyzer.EnsureAnalysis(command.OperationalState);
        DateTimeOffset now = _clock.UtcNow;
        IReadOnlyList<RouteResolutionTrace> resolutionTraces = command.ResolutionTraces.Count > 0
            ? command.ResolutionTraces
            : command.TraceQueries.Count > 0
                ? RouteResolutionTraceEngine.AnalyzeMany(
                    command.TraceQueries,
                    command.Configuration,
                    operationalState)
                : [];
        resolutionTraces = EnrichWithReversePathSymmetry(
            resolutionTraces,
            command.Configuration,
            operationalState,
            command.TraceQueries);
        resolutionTraces = NetworkPathProfileBinder.AttachBindings(resolutionTraces, command.NetworkPathProfiles);
        IReadOnlyList<RouteFinding> routeFindings = command.RouteExpectations.Count > 0 && resolutionTraces.Count > 0
            ? RouteExpectationEvaluator.Evaluate(
                command.RouteExpectations,
                resolutionTraces,
                command.Configuration,
                operationalState)
            : command.RouteFindings;
        if (command.LatencyEvaluations.Count > 0)
        {
            List<RouteFinding> merged = routeFindings.ToList();
            merged.AddRange(NetworkPathLatencyEvaluator.EvaluateMany(command.LatencyEvaluations));
            routeFindings = merged;
        }
        RoutingAssuranceState? existing = await _states.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            RoutingDriftClassification drift = RoutingDriftAnalyzer.Analyze(
                existing,
                command.Configuration,
                operationalState);
            if (drift.Findings.Count > 0)
            {
                routeFindings = MergeDriftFindings(routeFindings, drift.Findings);
            }
        }

        RoutingAssuranceState state = existing is null
            ? RoutingAssuranceState.Create(
                deviceId,
                command.Configuration,
                operationalState,
                now,
                command.RouteExpectations,
                routeFindings,
                resolutionTraces)
            : existing.With(
                command.Configuration,
                operationalState,
                now,
                command.RouteExpectations,
                routeFindings,
                resolutionTraces);

        await _unitOfWork.ExecuteAsync(
            async ct => await _states.UpsertAsync(state, ct).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(RoutingAssuranceViewMapper.ToView(state));
    }

    private static IReadOnlyList<RouteResolutionTrace> EnrichWithReversePathSymmetry(
        IReadOnlyList<RouteResolutionTrace> traces,
        RoutingConfigurationSnapshot configuration,
        RoutingOperationalSnapshot operational,
        IReadOnlyList<RouteResolutionQuery> traceQueries)
    {
        if (traces.Count == 0)
        {
            return traces;
        }

        List<RouteResolutionTrace> enriched = new(traces.Count);
        for (int i = 0; i < traces.Count; i++)
        {
            RouteResolutionTrace trace = traces[i];
            if (string.IsNullOrWhiteSpace(trace.SourceAddress)
                || string.IsNullOrWhiteSpace(trace.DestinationAddress)
                || trace.ReversePathSymmetry is not null)
            {
                enriched.Add(trace);
                continue;
            }

            ReversePathSymmetryAnalyzerOptions? options = i < traceQueries.Count
                                                        && traceQueries[i].ExpectAsymmetricReversePath
                ? new ReversePathSymmetryAnalyzerOptions { ExpectAsymmetricReversePath = true }
                : null;
            ReversePathSymmetryAnalysis analysis = ReversePathSymmetryAnalyzer.Analyze(
                trace,
                configuration,
                operational,
                options);
            enriched.Add(trace.WithReversePathSymmetry(analysis));
        }

        return enriched;
    }

    private static List<RouteFinding> MergeDriftFindings(
        IReadOnlyList<RouteFinding> existing,
        IReadOnlyList<RouteFinding> driftFindings)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<RouteFinding> merged = [];
        foreach (RouteFinding finding in existing.Concat(driftFindings))
        {
            string key = $"{finding.Code}|{finding.Subject ?? string.Empty}";
            if (seen.Add(key))
            {
                merged.Add(finding);
            }
        }

        return merged;
    }
}

/// <summary>Loads one routing assurance state row.</summary>
public sealed class GetRoutingAssuranceStateQuery
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }
}

/// <summary>Reads persisted routing assurance state (M7.1-02).</summary>
public sealed class GetRoutingAssuranceStateUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IRoutingAssuranceStateStore _states;

    public GetRoutingAssuranceStateUseCase(IAuthorizationBoundary auth, IRoutingAssuranceStateStore states)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(states);
        _auth = auth;
        _states = states;
    }

    public async Task<ApplicationResult<RoutingAssuranceDetailView>> ExecuteAsync(
        GetRoutingAssuranceStateQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        RoutingAssuranceState? state = await _states
            .GetAsync(new DeviceId(query.DeviceId), cancellationToken)
            .ConfigureAwait(false);
        if (state is null)
        {
            return ApplicationResults.Fail(
                ApplicationError.NotFound(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Routing assurance state '{query.DeviceId}' not found.")));
        }

        return ApplicationResults.Ok(RoutingAssuranceViewMapper.ToDetailView(state));
    }
}

internal static class RoutingAssuranceViewMapper
{
    public static RoutingAssuranceStateView ToView(RoutingAssuranceState state)
        => new()
        {
            DeviceId = state.DeviceId.Value,
            ConfigurationHashHex = state.ConfigurationHash.ToString(),
            OperationalHashHex = state.OperationalHash.ToString(),
            RouteExpectationCount = state.RouteExpectations.Count,
            RouteFindingCount = state.RouteFindings.Count,
            ResolutionTraceCount = state.ResolutionTraces.Count,
            ConfigurationTableCount = state.Configuration.Tables.Count,
            ConfigurationRuleCount = state.Configuration.Rules.Count,
            ConfigurationVrfCount = state.Configuration.Vrfs.Count,
            ConfigurationStaticRouteCount = state.Configuration.StaticRoutes.Count,
            ConfigurationFilterRuleCount = state.Configuration.FilterRules.Count,
            OperationalRouteCount = state.OperationalState.Routes.Count,
            OperationalDefaultRouteCount = state.OperationalState.DefaultRoutes.Count,
            UpdatedAtUtc = state.UpdatedAtUtc,
            RowVersion = state.RowVersion,
        };

    public static RoutingAssuranceDetailView ToDetailView(RoutingAssuranceState state)
        => new()
        {
            DeviceId = state.DeviceId.Value,
            ConfigurationHashHex = state.ConfigurationHash.ToString(),
            OperationalHashHex = state.OperationalHash.ToString(),
            RouteExpectationCount = state.RouteExpectations.Count,
            RouteFindingCount = state.RouteFindings.Count,
            ResolutionTraceCount = state.ResolutionTraces.Count,
            ConfigurationTableCount = state.Configuration.Tables.Count,
            ConfigurationRuleCount = state.Configuration.Rules.Count,
            ConfigurationVrfCount = state.Configuration.Vrfs.Count,
            ConfigurationStaticRouteCount = state.Configuration.StaticRoutes.Count,
            ConfigurationFilterRuleCount = state.Configuration.FilterRules.Count,
            OperationalRouteCount = state.OperationalState.Routes.Count,
            OperationalDefaultRouteCount = state.OperationalState.DefaultRoutes.Count,
            UpdatedAtUtc = state.UpdatedAtUtc,
            RowVersion = state.RowVersion,
            Expectations = state.RouteExpectations.Select(ToExpectationView).ToArray(),
            Findings = state.RouteFindings.Select(ToFindingView).ToArray(),
            TraceSummaries = state.ResolutionTraces
                .Select(trace => ToTraceSummaryView(trace, state.RouteFindings))
                .ToArray(),
        };

    private static RouteExpectationView ToExpectationView(RouteExpectation expectation)
        => new()
        {
            NodeId = expectation.NodeId,
            Family = expectation.Family,
            SourceZone = expectation.SourceZone,
            SourceAddress = expectation.SourceAddress,
            DestinationPrefix = expectation.DestinationPrefix,
            ExpectedVrf = expectation.ExpectedVrf,
            ExpectedTable = expectation.ExpectedTable,
            AllowedNextHops = expectation.AllowedNextHops,
            AllowedEgressZones = expectation.AllowedEgressZones,
            AllowedEgressInterfaces = expectation.AllowedEgressInterfaces,
            RequiredRouteTypes = expectation.RequiredRouteTypes,
            ForbiddenRouteTypes = expectation.ForbiddenRouteTypes,
            RequireCpuFirewallPath = expectation.RequireCpuFirewallPath,
            RequireReversePath = expectation.RequireReversePath,
            ExpectAsymmetricReversePath = expectation.ExpectAsymmetricReversePath,
            Critical = expectation.Critical,
        };

    private static RouteFindingView ToFindingView(RouteFinding finding)
        => new()
        {
            Code = finding.Code,
            Message = finding.Message,
            Subject = finding.Subject,
        };

    private static RouteResolutionTraceSummaryView ToTraceSummaryView(
        RouteResolutionTrace trace,
        IReadOnlyList<RouteFinding> findings)
    {
        List<string> nextHops = [];
        foreach (ImmediateNextHop hop in trace.ImmediateNextHops)
        {
            if (!string.IsNullOrWhiteSpace(hop.Gateway))
            {
                nextHops.Add(hop.Gateway);
            }

            if (nextHops.Count >= RouteResolutionTraceSummaryView.MaxNextHopGateways)
            {
                break;
            }
        }

        List<string> egress = trace.EgressInterfaces
            .Where(static e => !string.IsNullOrWhiteSpace(e))
            .Take(RouteResolutionTraceSummaryView.MaxEgressInterfaces)
            .ToList();

        string? destination = trace.DestinationAddress;
        List<string> driftCodes = [];
        List<string> latencyCodes = [];
        foreach (RouteFinding finding in findings)
        {
            if (!SubjectMatchesDestination(finding.Subject, destination))
            {
                continue;
            }

            if (finding.Code.StartsWith("ROUTING_", StringComparison.Ordinal))
            {
                driftCodes.Add(finding.Code);
            }
            else if (finding.Code.StartsWith("NETWORK_PATH_", StringComparison.Ordinal)
                     || finding.Code.StartsWith("ROUTE_PATH_CHANGED", StringComparison.Ordinal))
            {
                latencyCodes.Add(finding.Code);
            }
        }

        return new RouteResolutionTraceSummaryView
        {
            Family = trace.Family,
            DestinationAddress = destination,
            SourceAddress = trace.SourceAddress,
            SelectedVrf = trace.SelectedVrf,
            SelectedTable = trace.SelectedTable,
            MatchedPrefix = trace.MatchedPrefix,
            NextHopGateways = nextHops,
            EgressInterfaces = egress,
            ExecutionPath = trace.ExecutionPath,
            Decision = trace.Decision,
            DriftCodes = driftCodes,
            LatencyCodes = latencyCodes,
            ReversePathSymmetryResult = trace.ReversePathSymmetry?.Result,
        };
    }

    private static bool SubjectMatchesDestination(string? subject, string? destination)
    {
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(destination))
        {
            return false;
        }

        return string.Equals(subject, destination, StringComparison.Ordinal)
               || destination.StartsWith(subject, StringComparison.Ordinal)
               || subject.StartsWith(destination, StringComparison.Ordinal);
    }
}
