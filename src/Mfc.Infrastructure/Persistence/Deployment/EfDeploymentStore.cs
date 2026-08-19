using System.Text.Json;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Deployment;
using Mfc.Domain.Deployment.Primitives;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mfc.Infrastructure.Persistence.Deployment;

/// <summary>EF Core deployment plan/operation/lock/step store (Safe Deployment Spec §9–§16 / M4-01).</summary>
public sealed class EfDeploymentStore : IDeploymentStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly MfcDbContext _db;

    public EfDeploymentStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AddPlanAsync(DeploymentPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        DeploymentPlanEntity entity = new()
        {
            Id = plan.Id.Value,
            NodeId = plan.NodeId.Value,
            LogicalPolicyHash = plan.LogicalPolicyHash.Bytes.ToArray(),
            AnalysisBundleHash = plan.AnalysisBundleHash.Bytes.ToArray(),
            TopologyProjectionHash = plan.TopologyProjectionHash.Bytes.ToArray(),
            ActivationOrderJson = JsonSerializer.Serialize(plan.ActivationOrder.Select(static id => id.Value).ToArray(), JsonOptions),
            RollbackOrderJson = JsonSerializer.Serialize(plan.RollbackOrder.Select(static id => id.Value).ToArray(), JsonOptions),
            CreatedBy = plan.CreatedBy.Value,
            CreatedAtUtc = plan.CreatedAtUtc,
            ExpiresAtUtc = plan.ExpiresAtUtc,
            PlanHash = plan.PlanHash.Bytes.ToArray(),
        };
        foreach (DeviceDeploymentPlan devicePlan in plan.DevicePlans)
        {
            entity.DevicePlans.Add(ToDeviceEntity(plan.Id.Value, devicePlan));
        }

        _db.DeploymentPlans.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeploymentPlan?> GetPlanAsync(
        DeploymentPlanId id,
        CancellationToken cancellationToken = default)
    {
        DeploymentPlanEntity? entity = await _db.DeploymentPlans
            .AsNoTracking()
            .Include(p => p.DevicePlans)
            .SingleOrDefaultAsync(p => p.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddOperationAsync(DeploymentOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        _db.DeploymentOperations.Add(ToEntity(operation));
        await SaveChangesMappingConflictsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveOperationAsync(DeploymentOperation operation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        DeploymentOperationEntity entity = await _db.DeploymentOperations
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

    public async Task<DeploymentOperation?> GetOperationAsync(
        DeploymentOperationId id,
        CancellationToken cancellationToken = default)
    {
        DeploymentOperationEntity? entity = await _db.DeploymentOperations.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<DeploymentOperation>> ListNonterminalByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
    {
        List<DeploymentOperationEntity> rows = await _db.DeploymentOperations.AsNoTracking()
            .Where(o => o.NodeId == nodeId.Value
                        && o.State != DeploymentOperationEntity.CommittedState
                        && o.State != DeploymentOperationEntity.RolledBackState
                        && o.State != DeploymentOperationEntity.BlockedState
                        && o.State != DeploymentOperationEntity.NoChangesState
                        && o.State != DeploymentOperationEntity.CanceledState
                        && o.State != DeploymentOperationEntity.FailedState
                        && o.State != DeploymentOperationEntity.RecoveryRequiredState)
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task AddDeviceStateAsync(DeviceDeployment device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        _db.DeploymentDeviceStates.Add(ToEntity(device));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveDeviceStateAsync(DeviceDeployment device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);
        DeploymentDeviceStateEntity entity = await _db.DeploymentDeviceStates
            .SingleAsync(
                s => s.OperationId == device.OperationId.Value && s.DeviceId == device.DeviceId.Value,
                cancellationToken)
            .ConfigureAwait(false);
        entity.State = (short)device.State;
        entity.UpdatedAtUtc = device.UpdatedAtUtc;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DeviceDeployment>> ListDeviceStatesAsync(
        DeploymentOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        List<DeploymentDeviceStateEntity> rows = await _db.DeploymentDeviceStates.AsNoTracking()
            .Where(s => s.OperationId == operationId.Value)
            .OrderBy(s => s.DeviceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task AddStepAsync(DeploymentStep deploymentStep, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deploymentStep);
        _db.DeploymentSteps.Add(ToEntity(deploymentStep));
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveStepAsync(DeploymentStep deploymentStep, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deploymentStep);
        DeploymentStepEntity entity = await _db.DeploymentSteps
            .SingleAsync(s => s.Id == deploymentStep.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        entity.State = (short)deploymentStep.State;
        entity.UpdatedAtUtc = deploymentStep.UpdatedAtUtc;
        entity.SanitizedError = deploymentStep.SanitizedError;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DeploymentStep>> ListStepsAsync(
        DeploymentOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        List<DeploymentStepEntity> rows = await _db.DeploymentSteps.AsNoTracking()
            .Where(s => s.OperationId == operationId.Value)
            .OrderBy(s => s.Sequence)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task AddLockAsync(DeploymentLock deploymentLock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deploymentLock);
        Guid nodeId = deploymentLock.NodeId.Value;
        bool tracked = _db.ChangeTracker.Entries<DeploymentLockEntity>()
            .Any(e => e.Entity.NodeId == nodeId);
        if (tracked
            || await _db.DeploymentLocks.AsNoTracking()
                .AnyAsync(l => l.NodeId == nodeId, cancellationToken)
                .ConfigureAwait(false))
        {
            throw new PersistenceConflictException(
                DeploymentCodes.LockHeld,
                "Node already has a deployment lock.");
        }

        _db.DeploymentLocks.Add(ToEntity(deploymentLock));
        await SaveChangesMappingConflictsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveLockAsync(DeploymentLock deploymentLock, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deploymentLock);
        DeploymentLockEntity entity = await _db.DeploymentLocks
            .SingleAsync(l => l.NodeId == deploymentLock.NodeId.Value, cancellationToken)
            .ConfigureAwait(false);
        entity.HeartbeatAtUtc = deploymentLock.HeartbeatAtUtc;
        entity.ExpiresAtUtc = deploymentLock.ExpiresAtUtc;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<DeploymentLock?> GetLockByNodeAsync(
        NodeId nodeId,
        CancellationToken cancellationToken = default)
    {
        DeploymentLockEntity? entity = await _db.DeploymentLocks.AsNoTracking()
            .SingleOrDefaultAsync(l => l.NodeId == nodeId.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    private static DeploymentPlan ToDomain(DeploymentPlanEntity entity)
    {
        Guid[] activation = JsonSerializer.Deserialize<Guid[]>(entity.ActivationOrderJson, JsonOptions) ?? [];
        Guid[] rollback = JsonSerializer.Deserialize<Guid[]>(entity.RollbackOrderJson, JsonOptions) ?? [];
        return DeploymentPlan.Reconstitute(
            new DeploymentPlanId(entity.Id),
            new NodeId(entity.NodeId),
            Hash256.Create(entity.LogicalPolicyHash),
            Hash256.Create(entity.AnalysisBundleHash),
            Hash256.Create(entity.TopologyProjectionHash),
            entity.DevicePlans.OrderBy(static d => d.DeviceId).Select(ToDomain).ToArray(),
            activation.Select(static id => new DeviceId(id)).ToArray(),
            rollback.Select(static id => new DeviceId(id)).ToArray(),
            new UserId(entity.CreatedBy),
            entity.CreatedAtUtc,
            entity.ExpiresAtUtc,
            Hash256.Create(entity.PlanHash));
    }

    private static DeviceDeploymentPlan ToDomain(DeploymentDevicePlanEntity entity)
    {
        AnchorTargetDto[] oldTargets = JsonSerializer.Deserialize<AnchorTargetDto[]>(entity.OldAnchorTargetsJson, JsonOptions) ?? [];
        AnchorTargetDto[] newTargets = JsonSerializer.Deserialize<AnchorTargetDto[]>(entity.NewAnchorTargetsJson, JsonOptions) ?? [];
        AnchorKeyDto[] activation = JsonSerializer.Deserialize<AnchorKeyDto[]>(entity.AnchorActivationOrderJson, JsonOptions) ?? [];
        AnchorKeyDto[] rollback = JsonSerializer.Deserialize<AnchorKeyDto[]>(entity.AnchorRollbackOrderJson, JsonOptions) ?? [];
        string[] hashes = JsonSerializer.Deserialize<string[]>(entity.TransitionStateHashesJson, JsonOptions) ?? [];
        ProbeDto[] probes = JsonSerializer.Deserialize<ProbeDto[]>(entity.ProbesJson, JsonOptions) ?? [];
        return DeviceDeploymentPlan.Reconstitute(
            new DeviceId(entity.DeviceId),
            entity.ExpectedRouterOsVersion,
            Hash256.Create(entity.ExpectedCapabilityHash),
            Hash256.Create(entity.ExpectedConfigurationHash),
            Hash256.Create(entity.ExpectedCompatibilityHash),
            Hash256.Create(entity.ExpectedGuardContextHash),
            Hash256.Create(entity.ExpectedAnchorContextHash),
            Hash256.Create(entity.OldArtifactHash),
            oldTargets.Select(ToDomain).ToArray(),
            Hash256.Create(entity.NewArtifactHash),
            newTargets.Select(ToDomain).ToArray(),
            activation.Select(ToDomain).ToArray(),
            rollback.Select(ToDomain).ToArray(),
            hashes.Select(Hash256.ParseHex).ToArray(),
            TimeSpan.FromSeconds(entity.RollbackTtlSeconds),
            probes.Select(ToDomain).ToArray());
    }

    private static AnchorTarget ToDomain(AnchorTargetDto dto)
        => new(ToDomain(new AnchorKeyDto { Family = dto.Family, Chain = dto.Chain }), dto.JumpTarget);

    private static AnchorKey ToDomain(AnchorKeyDto dto)
        => AnchorKey.Create((IpAddressFamily)dto.Family, (FilterBuiltInContext)dto.Chain);

    private static DeploymentProbe ToDomain(ProbeDto dto)
        => new((DeploymentProbeKind)dto.Kind, dto.Destination, dto.TimeoutMilliseconds);

    private static DeploymentOperation ToDomain(DeploymentOperationEntity entity)
        => DeploymentOperation.Reconstitute(
            new DeploymentOperationId(entity.Id),
            new NodeId(entity.NodeId),
            new DeploymentPlanId(entity.PlanId),
            (DeploymentOperationState)entity.State,
            new UserId(entity.CreatedBy),
            entity.StartedAtUtc,
            entity.CompletedAtUtc,
            entity.ErrorCode,
            (ulong)entity.RowVersion,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    private static DeviceDeployment ToDomain(DeploymentDeviceStateEntity entity)
        => DeviceDeployment.Reconstitute(
            new DeploymentOperationId(entity.OperationId),
            new DeviceId(entity.DeviceId),
            (DeviceDeploymentState)entity.State,
            entity.UpdatedAtUtc);

    private static DeploymentStep ToDomain(DeploymentStepEntity entity)
        => DeploymentStep.Reconstitute(
            new DeploymentStepId(entity.Id),
            new DeploymentOperationId(entity.OperationId),
            new DeviceId(entity.DeviceId),
            (int)entity.Sequence,
            (DeploymentStepKind)entity.Kind,
            Hash256.Create(entity.ExpectedBeforeHash),
            Hash256.Create(entity.DesiredAfterHash),
            (DeploymentStepState)entity.State,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.SanitizedError);

    private static DeploymentLock ToDomain(DeploymentLockEntity entity)
        => DeploymentLock.Reconstitute(
            new NodeId(entity.NodeId),
            new DeploymentOperationId(entity.DeploymentId),
            entity.OwnerInstanceId,
            entity.AcquiredAtUtc,
            entity.HeartbeatAtUtc,
            entity.ExpiresAtUtc);

    private static DeploymentDevicePlanEntity ToDeviceEntity(Guid planId, DeviceDeploymentPlan plan)
        => new()
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            DeviceId = plan.DeviceId.Value,
            ExpectedRouterOsVersion = plan.ExpectedRouterOsVersion,
            ExpectedCapabilityHash = plan.ExpectedCapabilityHash.Bytes.ToArray(),
            ExpectedConfigurationHash = plan.ExpectedConfigurationHash.Bytes.ToArray(),
            ExpectedCompatibilityHash = plan.ExpectedCompatibilityHash.Bytes.ToArray(),
            ExpectedGuardContextHash = plan.ExpectedGuardContextHash.Bytes.ToArray(),
            ExpectedAnchorContextHash = plan.ExpectedAnchorContextHash.Bytes.ToArray(),
            OldArtifactHash = plan.OldArtifactHash.Bytes.ToArray(),
            NewArtifactHash = plan.NewArtifactHash.Bytes.ToArray(),
            OldAnchorTargetsJson = JsonSerializer.Serialize(plan.OldAnchorTargets.Select(ToDto).ToArray(), JsonOptions),
            NewAnchorTargetsJson = JsonSerializer.Serialize(plan.NewAnchorTargets.Select(ToDto).ToArray(), JsonOptions),
            AnchorActivationOrderJson = JsonSerializer.Serialize(plan.AnchorActivationOrder.Select(ToDto).ToArray(), JsonOptions),
            AnchorRollbackOrderJson = JsonSerializer.Serialize(plan.AnchorRollbackOrder.Select(ToDto).ToArray(), JsonOptions),
            TransitionStateHashesJson = JsonSerializer.Serialize(plan.TransitionStateHashes.Select(static h => h.ToString()).ToArray(), JsonOptions),
            RollbackTtlSeconds = (int)plan.RollbackTtl.TotalSeconds,
            ProbesJson = JsonSerializer.Serialize(plan.Probes.Select(ToDto).ToArray(), JsonOptions),
        };

    private static AnchorTargetDto ToDto(AnchorTarget target)
        => new()
        {
            Family = (byte)target.Key.Family,
            Chain = (byte)target.Key.Chain,
            JumpTarget = target.JumpTarget,
        };

    private static AnchorKeyDto ToDto(AnchorKey key)
        => new()
        {
            Family = (byte)key.Family,
            Chain = (byte)key.Chain,
        };

    private static ProbeDto ToDto(DeploymentProbe probe)
        => new()
        {
            Kind = (byte)probe.Kind,
            Destination = probe.Destination,
            TimeoutMilliseconds = probe.TimeoutMilliseconds,
        };

    private static DeploymentOperationEntity ToEntity(DeploymentOperation operation)
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

    private static DeploymentDeviceStateEntity ToEntity(DeviceDeployment device)
        => new()
        {
            OperationId = device.OperationId.Value,
            DeviceId = device.DeviceId.Value,
            State = (short)device.State,
            UpdatedAtUtc = device.UpdatedAtUtc,
        };

    private static DeploymentStepEntity ToEntity(DeploymentStep step)
        => new()
        {
            Id = step.Id.Value,
            OperationId = step.OperationId.Value,
            DeviceId = step.DeviceId.Value,
            Sequence = step.Sequence,
            Kind = (short)step.Kind,
            ExpectedBeforeHash = step.ExpectedBeforeHash.Bytes.ToArray(),
            DesiredAfterHash = step.DesiredAfterHash.Bytes.ToArray(),
            State = (short)step.State,
            CreatedAtUtc = step.CreatedAtUtc,
            UpdatedAtUtc = step.UpdatedAtUtc,
            SanitizedError = step.SanitizedError,
        };

    private static DeploymentLockEntity ToEntity(DeploymentLock deploymentLock)
        => new()
        {
            NodeId = deploymentLock.NodeId.Value,
            DeploymentId = deploymentLock.DeploymentId.Value,
            OwnerInstanceId = deploymentLock.OwnerInstanceId,
            AcquiredAtUtc = deploymentLock.AcquiredAtUtc,
            HeartbeatAtUtc = deploymentLock.HeartbeatAtUtc,
            ExpiresAtUtc = deploymentLock.ExpiresAtUtc,
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
        if (pg.SqlState != PostgresErrorCodes.UniqueViolation)
        {
            return null;
        }

        if (constraint.Contains("deployment_operations", StringComparison.Ordinal))
        {
            return new PersistenceConflictException(
                DeploymentCodes.NonterminalExists,
                "Node already has a nonterminal deployment operation.",
                ex);
        }

        if (constraint.Contains("deployment_locks", StringComparison.Ordinal))
        {
            return new PersistenceConflictException(
                DeploymentCodes.LockHeld,
                "Node already has a deployment lock.",
                ex);
        }

        return null;
    }

    private sealed class AnchorKeyDto
    {
        public byte Family { get; set; }

        public byte Chain { get; set; }
    }

    private sealed class AnchorTargetDto
    {
        public byte Family { get; set; }

        public byte Chain { get; set; }

        public string JumpTarget { get; set; } = string.Empty;
    }

    private sealed class ProbeDto
    {
        public byte Kind { get; set; }

        public string Destination { get; set; } = string.Empty;

        public int TimeoutMilliseconds { get; set; }
    }
}
