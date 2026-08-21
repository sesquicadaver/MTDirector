using System.Text.Json;
using System.Text.Json.Serialization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Routing;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace Mfc.Infrastructure.Persistence.Routing;

/// <summary>EF Core store for <see cref="RoutingAssuranceState"/> (M7.1-02).</summary>
public sealed class EfRoutingAssuranceStateStore : IRoutingAssuranceStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly MfcDbContext _db;

    public EfRoutingAssuranceStateStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task UpsertAsync(RoutingAssuranceState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        RoutingAssuranceStateEntity? entity = await _db.RoutingAssuranceStates
            .SingleOrDefaultAsync(e => e.DeviceId == state.DeviceId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            _db.RoutingAssuranceStates.Add(ToEntity(state));
        }
        else
        {
            entity.ConfigurationHash = state.ConfigurationHash.Bytes.ToArray();
            entity.OperationalHash = state.OperationalHash.Bytes.ToArray();
            entity.ConfigurationJson = SerializeConfiguration(state.Configuration);
            entity.OperationalJson = SerializeOperational(state.OperationalState);
            entity.RouteExpectationsJson = JsonSerializer.Serialize(state.RouteExpectations, JsonOptions);
            entity.RouteFindingsJson = JsonSerializer.Serialize(state.RouteFindings, JsonOptions);
            entity.ResolutionTracesJson = JsonSerializer.Serialize(state.ResolutionTraces, JsonOptions);
            entity.UpdatedAtUtc = state.UpdatedAtUtc;
            entity.RowVersion = (long)state.RowVersion;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<RoutingAssuranceState?> GetAsync(DeviceId deviceId, CancellationToken cancellationToken = default)
    {
        RoutingAssuranceStateEntity? entity = await _db.RoutingAssuranceStates.AsNoTracking()
            .SingleOrDefaultAsync(e => e.DeviceId == deviceId.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    private static RoutingAssuranceStateEntity ToEntity(RoutingAssuranceState state) => new()
    {
        DeviceId = state.DeviceId.Value,
        ConfigurationHash = state.ConfigurationHash.Bytes.ToArray(),
        OperationalHash = state.OperationalHash.Bytes.ToArray(),
        ConfigurationJson = SerializeConfiguration(state.Configuration),
        OperationalJson = SerializeOperational(state.OperationalState),
        RouteExpectationsJson = JsonSerializer.Serialize(state.RouteExpectations, JsonOptions),
        RouteFindingsJson = JsonSerializer.Serialize(state.RouteFindings, JsonOptions),
        ResolutionTracesJson = JsonSerializer.Serialize(state.ResolutionTraces, JsonOptions),
        UpdatedAtUtc = state.UpdatedAtUtc,
        RowVersion = (long)state.RowVersion,
    };

    private static RoutingAssuranceState ToDomain(RoutingAssuranceStateEntity entity)
    {
        ConfigurationDto configDto = JsonSerializer.Deserialize<ConfigurationDto>(entity.ConfigurationJson, JsonOptions)
            ?? new ConfigurationDto();
        OperationalDto opsDto = JsonSerializer.Deserialize<OperationalDto>(entity.OperationalJson, JsonOptions)
            ?? new OperationalDto();
        RouteExpectation[] expectations =
            JsonSerializer.Deserialize<RouteExpectation[]>(entity.RouteExpectationsJson, JsonOptions) ?? [];
        RouteFinding[] findings =
            JsonSerializer.Deserialize<RouteFinding[]>(entity.RouteFindingsJson, JsonOptions) ?? [];
        RouteResolutionTrace[] traces =
            JsonSerializer.Deserialize<RouteResolutionTrace[]>(entity.ResolutionTracesJson, JsonOptions) ?? [];

        RoutingConfigurationSnapshot configuration = ToConfiguration(configDto);
        RoutingOperationalSnapshot operational = ToOperational(opsDto);

        return RoutingAssuranceState.Reconstitute(
            new DeviceId(entity.DeviceId),
            configuration,
            operational,
            Hash256.Create(entity.ConfigurationHash),
            Hash256.Create(entity.OperationalHash),
            expectations,
            findings,
            traces,
            entity.UpdatedAtUtc,
            (ulong)entity.RowVersion);
    }

    private static string SerializeConfiguration(RoutingConfigurationSnapshot snapshot)
        => JsonSerializer.Serialize(ConfigurationDto.From(snapshot), JsonOptions);

    private static string SerializeOperational(RoutingOperationalSnapshot snapshot)
        => JsonSerializer.Serialize(OperationalDto.From(snapshot), JsonOptions);

    private static RoutingConfigurationSnapshot ToConfiguration(ConfigurationDto dto)
        => new(
            dto.Tables ?? [],
            dto.Settings ?? RoutingSettingsFact.Empty,
            dto.Rules ?? [],
            dto.Vrfs ?? [],
            dto.StaticRoutes ?? [],
            dto.FilterRules ?? [],
            dto.FilterSelectRules ?? [],
            dto.HashMaterial ?? new Dictionary<string, string>(StringComparer.Ordinal));

    private static RoutingOperationalSnapshot ToOperational(OperationalDto dto)
        => new(
            dto.Routes ?? [],
            dto.DefaultRoutes ?? [],
            dto.HashMaterial ?? new Dictionary<string, string>(StringComparer.Ordinal));

    private sealed class ConfigurationDto
    {
        public List<RoutingTableFact>? Tables { get; set; }

        public RoutingSettingsFact? Settings { get; set; }

        public List<RoutingRuleFact>? Rules { get; set; }

        public List<VrfDefinitionFact>? Vrfs { get; set; }

        public List<StaticRouteConfigFact>? StaticRoutes { get; set; }

        public List<RouteFilterRuleFact>? FilterRules { get; set; }

        public List<RouteFilterSelectRuleFact>? FilterSelectRules { get; set; }

        public Dictionary<string, string>? HashMaterial { get; set; }

        public static ConfigurationDto From(RoutingConfigurationSnapshot snapshot) => new()
        {
            Tables = snapshot.Tables.ToList(),
            Settings = snapshot.Settings,
            Rules = snapshot.Rules.ToList(),
            Vrfs = snapshot.Vrfs.ToList(),
            StaticRoutes = snapshot.StaticRoutes.ToList(),
            FilterRules = snapshot.FilterRules.ToList(),
            FilterSelectRules = snapshot.FilterSelectRules.ToList(),
            HashMaterial = snapshot.HashMaterial.ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal),
        };
    }

    private sealed class OperationalDto
    {
        public List<RouteObservationFact>? Routes { get; set; }

        public List<DefaultRouteObservationFact>? DefaultRoutes { get; set; }

        public Dictionary<string, string>? HashMaterial { get; set; }

        public static OperationalDto From(RoutingOperationalSnapshot snapshot) => new()
        {
            Routes = snapshot.Routes.ToList(),
            DefaultRoutes = snapshot.DefaultRoutes.ToList(),
            HashMaterial = snapshot.HashMaterial.ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal),
        };
    }
}
