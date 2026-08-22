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
}

/// <summary>Stores routing assurance state shell (M7.1-02).</summary>
public sealed class UpsertRoutingAssuranceStateUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IRoutingAssuranceStateStore _states;
    private readonly IClock _clock;

    public UpsertRoutingAssuranceStateUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IRoutingAssuranceStateStore states,
        IClock clock)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(clock);
        _auth = auth;
        _devices = devices;
        _states = states;
        _clock = clock;
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
        IReadOnlyList<RouteFinding> routeFindings = command.RouteExpectations.Count > 0 && resolutionTraces.Count > 0
            ? RouteExpectationEvaluator.Evaluate(
                command.RouteExpectations,
                resolutionTraces,
                command.Configuration,
                operationalState)
            : command.RouteFindings;
        RoutingAssuranceState? existing = await _states.GetAsync(deviceId, cancellationToken).ConfigureAwait(false);
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

        await _states.UpsertAsync(state, cancellationToken).ConfigureAwait(false);
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

    public async Task<ApplicationResult<RoutingAssuranceStateView>> ExecuteAsync(
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

        return ApplicationResults.Ok(RoutingAssuranceViewMapper.ToView(state));
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
}
