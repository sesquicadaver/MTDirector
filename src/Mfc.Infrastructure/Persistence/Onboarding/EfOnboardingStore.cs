using System.Text.Json;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mfc.Infrastructure.Persistence.Onboarding;

/// <summary>EF Core onboarding plan/operation/step store (Onboarding Spec §25 / §5 / §54 / M5-01).</summary>
public sealed class EfOnboardingStore : IOnboardingStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly MfcDbContext _db;

    public EfOnboardingStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AddPlanAsync(OnboardingPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        OnboardingPlanEntity entity = new()
        {
            Id = plan.Id.Value,
            NodeId = plan.NodeId.Value,
            NodeMembershipHash = plan.NodeMembershipHash.Bytes.ToArray(),
            TopologyProjectionHash = plan.TopologyProjectionHash.Bytes.ToArray(),
            CreatedBy = plan.CreatedBy.Value,
            CreatedAtUtc = plan.CreatedAtUtc,
            ExpiresAtUtc = plan.ExpiresAtUtc,
            PlanHash = plan.PlanHash.Bytes.ToArray(),
        };
        foreach (DeviceOnboardingPlan devicePlan in plan.DevicePlans)
        {
            entity.DevicePlans.Add(ToDeviceEntity(plan.Id.Value, devicePlan));
        }

        _db.OnboardingPlans.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OnboardingPlan?> GetPlanAsync(
        OnboardingPlanId id,
        CancellationToken cancellationToken = default)
    {
        OnboardingPlanEntity? entity = await _db.OnboardingPlans
            .AsNoTracking()
            .Include(p => p.DevicePlans)
            .ThenInclude(d => d.Placements)
            .SingleOrDefaultAsync(p => p.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddOperationAsync(OnboardingOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _db.OnboardingOperations.Add(ToEntity(operation));
        await SaveChangesMappingConflictsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveOperationAsync(OnboardingOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        OnboardingOperationEntity entity = await _db.OnboardingOperations
            .SingleAsync(o => o.Id == operation.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        entity.State = (short)operation.State;
        entity.StartedAtUtc = operation.StartedAtUtc;
        entity.CompletedAtUtc = operation.CompletedAtUtc;
        entity.ErrorCode = operation.ErrorCode;
        entity.RowVersion = (long)operation.RowVersion;
        entity.UpdatedAtUtc = operation.UpdatedAtUtc;
        await SaveChangesMappingConflictsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<OnboardingOperation?> GetOperationAsync(
        OnboardingOperationId id,
        CancellationToken cancellationToken = default)
    {
        OnboardingOperationEntity? entity = await _db.OnboardingOperations.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<OnboardingOperation>> ListNonterminalByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
    {
        List<OnboardingOperationEntity> rows = await _db.OnboardingOperations.AsNoTracking()
            .Where(o => o.NodeId == nodeId.Value
                        && o.State != OnboardingOperationEntity.CommittedState
                        && o.State != OnboardingOperationEntity.RolledBackState
                        && o.State != OnboardingOperationEntity.BlockedState
                        && o.State != OnboardingOperationEntity.RecoveryRequiredState)
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task AddStepAsync(OnboardingStep onboardingStep, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onboardingStep);
        _db.OnboardingSteps.Add(ToEntity(onboardingStep));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveStepAsync(OnboardingStep onboardingStep, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onboardingStep);
        OnboardingStepEntity entity = await _db.OnboardingSteps
            .SingleAsync(s => s.Id == onboardingStep.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        entity.State = (short)onboardingStep.State;
        entity.UpdatedAtUtc = onboardingStep.UpdatedAtUtc;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<OnboardingStep>> ListStepsAsync(
        OnboardingOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        List<OnboardingStepEntity> rows = await _db.OnboardingSteps.AsNoTracking()
            .Where(s => s.OperationId == operationId.Value)
            .OrderBy(s => s.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    private static OnboardingPlan ToDomain(OnboardingPlanEntity entity)
        => OnboardingPlan.Reconstitute(
            new OnboardingPlanId(entity.Id),
            new NodeId(entity.NodeId),
            Hash256.Create(entity.NodeMembershipHash),
            Hash256.Create(entity.TopologyProjectionHash),
            entity.DevicePlans
                .OrderBy(static d => d.DeviceId)
                .Select(ToDomain)
                .ToArray(),
            new UserId(entity.CreatedBy),
            entity.CreatedAtUtc,
            entity.ExpiresAtUtc,
            Hash256.Create(entity.PlanHash));

    private static DeviceOnboardingPlan ToDomain(OnboardingDevicePlanEntity entity)
    {
        AnchorKeyDto[] keys = JsonSerializer.Deserialize<AnchorKeyDto[]>(entity.RequiredAnchorSetJson, JsonOptions)
            ?? [];
        return DeviceOnboardingPlan.Reconstitute(
            new DeviceId(entity.DeviceId),
            entity.ExpectedRouterOsVersion,
            Hash256.Create(entity.ExpectedCapabilityHash),
            Hash256.Create(entity.ExpectedConfigurationHash),
            Hash256.Create(entity.ExpectedCompatibilityHash),
            Hash256.Create(entity.ExpectedApiServiceHash),
            Hash256.Create(entity.ExpectedReadAccountHash),
            Hash256.Create(entity.ExpectedDeploymentAccountHash),
            Hash256.Create(entity.ExpectedDeviceModeHash),
            Hash256.Create(entity.ExpectedGuardHash),
            keys.Select(static k => AnchorKey.Create((IpAddressFamily)k.Family, (FilterBuiltInContext)k.Chain)).ToArray(),
            entity.Placements
                .OrderBy(static p => p.Family)
                .ThenBy(static p => p.Chain)
                .Select(ToDomain)
                .ToArray(),
            Hash256.Create(entity.BootstrapArtifactHash),
            TimeSpan.FromSeconds(entity.WatchdogTtlSeconds));
    }

    private static AnchorPlacement ToDomain(OnboardingAnchorPlacementEntity entity)
        => AnchorPlacement.Reconstitute(
            (IpAddressFamily)entity.Family,
            (FilterBuiltInContext)entity.Chain,
            (AnchorPlacementMode)entity.Mode,
            (uint)entity.ExpectedAnchorOrdinal,
            entity.ReferenceRuleFingerprint is null ? null : Hash256.Create(entity.ReferenceRuleFingerprint),
            entity.ReferenceOccurrenceRank is null ? null : (uint)entity.ReferenceOccurrenceRank.Value,
            entity.ExpectedPredecessorFingerprint is null
                ? null
                : Hash256.Create(entity.ExpectedPredecessorFingerprint),
            entity.ExpectedSuccessorFingerprint is null
                ? null
                : Hash256.Create(entity.ExpectedSuccessorFingerprint));

    private static OnboardingOperation ToDomain(OnboardingOperationEntity entity)
        => OnboardingOperation.Reconstitute(
            new OnboardingOperationId(entity.Id),
            new NodeId(entity.NodeId),
            new OnboardingPlanId(entity.PlanId),
            (OnboardingOperationState)entity.State,
            new UserId(entity.CreatedBy),
            entity.StartedAtUtc,
            entity.CompletedAtUtc,
            entity.ErrorCode,
            (ulong)entity.RowVersion,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    private static OnboardingStep ToDomain(OnboardingStepEntity entity)
        => OnboardingStep.Reconstitute(
            new OnboardingStepId(entity.Id),
            new OnboardingOperationId(entity.OperationId),
            new DeviceId(entity.DeviceId),
            (int)entity.Sequence,
            (OnboardingStepKind)entity.Kind,
            Hash256.Create(entity.ExpectedBeforeHash),
            Hash256.Create(entity.DesiredAfterHash),
            (OnboardingStepState)entity.State,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    private static OnboardingDevicePlanEntity ToDeviceEntity(Guid planId, DeviceOnboardingPlan plan)
    {
        OnboardingDevicePlanEntity entity = new()
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            DeviceId = plan.DeviceId.Value,
            ExpectedRouterOsVersion = plan.ExpectedRouterOsVersion,
            ExpectedCapabilityHash = plan.ExpectedCapabilityHash.Bytes.ToArray(),
            ExpectedConfigurationHash = plan.ExpectedConfigurationHash.Bytes.ToArray(),
            ExpectedCompatibilityHash = plan.ExpectedCompatibilityHash.Bytes.ToArray(),
            ExpectedApiServiceHash = plan.ExpectedApiServiceHash.Bytes.ToArray(),
            ExpectedReadAccountHash = plan.ExpectedReadAccountHash.Bytes.ToArray(),
            ExpectedDeploymentAccountHash = plan.ExpectedDeploymentAccountHash.Bytes.ToArray(),
            ExpectedDeviceModeHash = plan.ExpectedDeviceModeHash.Bytes.ToArray(),
            ExpectedGuardHash = plan.ExpectedGuardHash.Bytes.ToArray(),
            RequiredAnchorSetJson = JsonSerializer.Serialize(
                plan.RequiredAnchorSet.Select(static k => new AnchorKeyDto
                {
                    Family = (byte)k.Family,
                    Chain = (byte)k.Chain,
                }).ToArray(),
                JsonOptions),
            BootstrapArtifactHash = plan.BootstrapArtifactHash.Bytes.ToArray(),
            WatchdogTtlSeconds = (int)plan.WatchdogTtl.TotalSeconds,
        };
        foreach (AnchorPlacement placement in plan.AnchorPlacements)
        {
            entity.Placements.Add(new OnboardingAnchorPlacementEntity
            {
                Id = Guid.NewGuid(),
                DevicePlanId = entity.Id,
                Family = (short)placement.Family,
                Chain = (short)placement.Chain,
                Mode = (short)placement.Mode,
                ReferenceRuleFingerprint = placement.ReferenceRuleFingerprint?.Bytes.ToArray(),
                ReferenceOccurrenceRank = placement.ReferenceOccurrenceRank,
                ExpectedPredecessorFingerprint = placement.ExpectedPredecessorFingerprint?.Bytes.ToArray(),
                ExpectedSuccessorFingerprint = placement.ExpectedSuccessorFingerprint?.Bytes.ToArray(),
                ExpectedAnchorOrdinal = placement.ExpectedAnchorOrdinal,
            });
        }

        return entity;
    }

    private static OnboardingOperationEntity ToEntity(OnboardingOperation operation)
        => new()
        {
            Id = operation.Id.Value,
            NodeId = operation.NodeId.Value,
            PlanId = operation.PlanId.Value,
            State = (short)operation.State,
            CreatedBy = operation.CreatedBy.Value,
            StartedAtUtc = operation.StartedAtUtc,
            CompletedAtUtc = operation.CompletedAtUtc,
            ErrorCode = operation.ErrorCode,
            RowVersion = (long)operation.RowVersion,
            CreatedAtUtc = operation.CreatedAtUtc,
            UpdatedAtUtc = operation.UpdatedAtUtc,
        };

    private static OnboardingStepEntity ToEntity(OnboardingStep onboardingStep)
        => new()
        {
            Id = onboardingStep.Id.Value,
            OperationId = onboardingStep.OperationId.Value,
            DeviceId = onboardingStep.DeviceId.Value,
            Sequence = onboardingStep.Sequence,
            Kind = (short)onboardingStep.Kind,
            ExpectedBeforeHash = onboardingStep.ExpectedBeforeHash.Bytes.ToArray(),
            DesiredAfterHash = onboardingStep.DesiredAfterHash.Bytes.ToArray(),
            State = (short)onboardingStep.State,
            CreatedAtUtc = onboardingStep.CreatedAtUtc,
            UpdatedAtUtc = onboardingStep.UpdatedAtUtc,
        };

    private async Task SaveChangesMappingConflictsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            PersistenceConflictException? conflict = TryMapConflict(ex);
            if (conflict is not null)
            {
                throw conflict;
            }

            throw;
        }
    }

    private static PersistenceConflictException? TryMapConflict(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException pg)
        {
            return null;
        }

        string constraint = pg.ConstraintName ?? string.Empty;
        if (pg.SqlState == PostgresErrorCodes.UniqueViolation
            && constraint.Contains("onboarding_operations", StringComparison.Ordinal))
        {
            return new PersistenceConflictException(
                OnboardingCodes.NonterminalExists,
                "Node already has a nonterminal onboarding operation.",
                ex);
        }

        return null;
    }

    private sealed class AnchorKeyDto
    {
        public byte Family { get; set; }

        public byte Chain { get; set; }
    }
}
