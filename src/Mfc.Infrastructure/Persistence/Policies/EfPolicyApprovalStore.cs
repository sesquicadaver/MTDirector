using System.Text.Json;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Mfc.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mfc.Infrastructure.Persistence.Policies;

/// <summary>EF Core analysis/approval/binding store (Policy Model §66–§67 / M2-17).</summary>
public sealed class EfPolicyApprovalStore : IPolicyApprovalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly MfcDbContext _db;

    public EfPolicyApprovalStore(MfcDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    public async Task AddAnalysisRunAsync(PolicyAnalysisRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        _db.PolicyAnalysisRuns.Add(new PolicyAnalysisRunEntity
        {
            Id = run.Id.Value,
            RevisionId = run.RevisionId.Value,
            RevisionContentHash = run.RevisionContentHash.Bytes.ToArray(),
            LogicalEffectiveHash = run.LogicalEffectiveHash.Bytes.ToArray(),
            AnalysisContextHash = run.AnalysisContextHash.Bytes.ToArray(),
            EvidenceContextHash = run.EvidenceContextHash.Bytes.ToArray(),
            TopologyProjectionHash = run.TopologyProjectionHash.Bytes.ToArray(),
            ImpactSetHash = run.ImpactSetHash.Bytes.ToArray(),
            PerDeviceAnalysisHashes = Concat(run.PerDeviceAnalysisHashes),
            BundleHash = run.BundleHash.Bytes.ToArray(),
            DependencyFingerprint = run.DependencyFingerprint.Bytes.ToArray(),
            RiskLevel = run.RiskLevel,
            EvidenceSignalsPresent = run.EvidenceSignalsPresent,
            AnalyzerVersion = run.AnalyzerVersion,
            PolicySchemaVersion = run.PolicySchemaVersion,
            PipelineVersion = run.PipelineVersion,
            FindingsJson = JsonSerializer.Serialize(run.Findings.Select(ToFindingDto).ToArray(), JsonOptions),
            TestResultsJson = JsonSerializer.Serialize(run.TestResults.Select(ToTestDto).ToArray(), JsonOptions),
            CreatedBy = run.CreatedBy.Value,
            CreatedAtUtc = run.CreatedAtUtc,
        });
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PolicyAnalysisRun?> GetAnalysisRunAsync(
        PolicyAnalysisRunId id,
        CancellationToken cancellationToken = default)
    {
        PolicyAnalysisRunEntity? entity = await _db.PolicyAnalysisRuns.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<PolicyAnalysisRun>> ListAnalysisRunsForRevisionAsync(
        PolicyRevisionId revisionId,
        CancellationToken cancellationToken = default)
    {
        List<PolicyAnalysisRunEntity> rows = await _db.PolicyAnalysisRuns.AsNoTracking()
            .Where(r => r.RevisionId == revisionId.Value)
            .OrderBy(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task AddWarningAcknowledgmentAsync(
        PolicyWarningAcknowledgment acknowledgment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(acknowledgment);
        _db.WarningAcknowledgments.Add(new PolicyWarningAcknowledgmentEntity
        {
            Id = acknowledgment.Id.Value,
            AnalysisRunId = acknowledgment.AnalysisRunId.Value,
            WarningHash = acknowledgment.WarningHash.Bytes.ToArray(),
            AcknowledgedBy = acknowledgment.AcknowledgedBy.Value,
            AcknowledgedAtUtc = acknowledgment.AcknowledgedAtUtc,
        });
        await SaveChangesMappingConflictsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PolicyWarningAcknowledgment>> ListAcknowledgmentsAsync(
        PolicyAnalysisRunId analysisRunId,
        CancellationToken cancellationToken = default)
    {
        List<PolicyWarningAcknowledgmentEntity> rows = await _db.WarningAcknowledgments.AsNoTracking()
            .Where(a => a.AnalysisRunId == analysisRunId.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(static a => PolicyWarningAcknowledgment.Reconstitute(
            new PolicyWarningAcknowledgmentId(a.Id),
            new PolicyAnalysisRunId(a.AnalysisRunId),
            Hash256.Create(a.WarningHash),
            new UserId(a.AcknowledgedBy),
            a.AcknowledgedAtUtc)).ToArray();
    }

    public async Task AddApprovalAsync(PolicyApproval approval, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approval);
        _db.PolicyApprovals.Add(new PolicyApprovalEntity
        {
            Id = approval.Id.Value,
            RevisionId = approval.RevisionId.Value,
            AnalysisRunId = approval.AnalysisRunId.Value,
            BundleHash = approval.BundleHash.Bytes.ToArray(),
            ReviewerId = approval.ReviewerId.Value,
            IsSecurityOwner = approval.IsSecurityOwner,
            RecordedAtUtc = approval.RecordedAtUtc,
        });
        await SaveChangesMappingConflictsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PolicyApproval?> GetApprovalAsync(
        PolicyApprovalId id,
        CancellationToken cancellationToken = default)
    {
        PolicyApprovalEntity? entity = await _db.PolicyApprovals.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<PolicyApproval>> ListApprovalsAsync(
        PolicyRevisionId revisionId,
        CancellationToken cancellationToken = default)
    {
        List<PolicyApprovalEntity> rows = await _db.PolicyApprovals.AsNoTracking()
            .Where(a => a.RevisionId == revisionId.Value)
            .OrderBy(a => a.RecordedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task AddBindingAsync(PolicyDesiredBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        _db.PolicyBindings.Add(ToEntity(binding));
        await SaveChangesMappingConflictsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveBindingAsync(PolicyDesiredBinding binding, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(binding);
        PolicyBindingEntity entity = await _db.PolicyBindings
            .SingleAsync(b => b.Id == binding.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        entity.State = (short)binding.State;
        entity.RowVersion = (long)binding.RowVersion;
        entity.UpdatedAtUtc = binding.UpdatedAtUtc;
        await SaveChangesMappingConflictsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PolicyDesiredBinding?> GetBindingAsync(
        PolicyBindingId id,
        CancellationToken cancellationToken = default)
    {
        PolicyBindingEntity? entity = await _db.PolicyBindings.AsNoTracking()
            .SingleOrDefaultAsync(b => b.Id == id.Value, cancellationToken)
            .ConfigureAwait(false);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<PolicyDesiredBinding>> ListActiveBindingsAsync(
        PolicyBindingScope scope,
        Guid? scopeId,
        CancellationToken cancellationToken = default)
    {
        List<PolicyBindingEntity> rows = await _db.PolicyBindings.AsNoTracking()
            .Where(b => b.Scope == (short)scope && b.ScopeId == scopeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<PolicyDesiredBinding>> ListDueExceptionBindingsAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1)
        {
            return [];
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        List<PolicyBindingEntity> rows = await _db.PolicyBindings.AsNoTracking()
            .Where(b => b.Scope == (short)PolicyBindingScope.Exception
                        && b.State == PolicyBindingEntity.ActiveState
                        && b.ValidUntilUtc != null
                        && b.ValidUntilUtc <= now)
            .OrderBy(b => b.ValidUntilUtc)
            .ThenBy(b => b.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    public async Task<IReadOnlyList<PolicyDesiredBinding>> ListDueIncidentDenyOverlayBindingsAsync(
        DateTimeOffset nowUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1)
        {
            return [];
        }

        DateTimeOffset now = nowUtc.ToUniversalTime();
        List<PolicyBindingEntity> rows = await _db.PolicyBindings.AsNoTracking()
            .Where(b => b.Scope == (short)PolicyBindingScope.IncidentDenyOverlay
                        && b.State == PolicyBindingEntity.ActiveState
                        && b.ValidUntilUtc != null
                        && b.ValidUntilUtc <= now)
            .OrderBy(b => b.ValidUntilUtc)
            .ThenBy(b => b.Id)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(ToDomain).ToArray();
    }

    private static PolicyAnalysisRun ToDomain(PolicyAnalysisRunEntity entity)
    {
        FindingDto[] findings = JsonSerializer.Deserialize<FindingDto[]>(entity.FindingsJson, JsonOptions) ?? [];
        TestDto[] tests = JsonSerializer.Deserialize<TestDto[]>(entity.TestResultsJson, JsonOptions) ?? [];
        return PolicyAnalysisRun.Reconstitute(
            new PolicyAnalysisRunId(entity.Id),
            new PolicyRevisionId(entity.RevisionId),
            Hash256.Create(entity.RevisionContentHash),
            Hash256.Create(entity.LogicalEffectiveHash),
            Hash256.Create(entity.AnalysisContextHash),
            Hash256.Create(entity.EvidenceContextHash),
            Hash256.Create(entity.TopologyProjectionHash),
            Hash256.Create(entity.ImpactSetHash),
            SplitHashes(entity.PerDeviceAnalysisHashes),
            Hash256.Create(entity.BundleHash),
            Hash256.Create(entity.DependencyFingerprint),
            entity.RiskLevel,
            entity.EvidenceSignalsPresent,
            entity.AnalyzerVersion,
            entity.PolicySchemaVersion,
            entity.PipelineVersion,
            findings.Select(static f => new PolicyApprovalFinding
            {
                Code = f.Code,
                Severity = f.Severity,
                Message = f.Message,
                Target = f.Target,
                WarningHash = Hash256.ParseHex(f.WarningHashHex),
            }).ToArray(),
            tests.Select(static t => new PolicyApprovalTestOutcome
            {
                TestId = new PolicyTestId(t.TestId),
                Origin = t.Origin,
                Outcome = t.Outcome,
                Proof = t.Proof,
            }).ToArray(),
            new UserId(entity.CreatedBy),
            entity.CreatedAtUtc);
    }

    private static PolicyApproval ToDomain(PolicyApprovalEntity entity)
        => PolicyApproval.Reconstitute(
            new PolicyApprovalId(entity.Id),
            new PolicyRevisionId(entity.RevisionId),
            new PolicyAnalysisRunId(entity.AnalysisRunId),
            Hash256.Create(entity.BundleHash),
            new UserId(entity.ReviewerId),
            entity.IsSecurityOwner,
            entity.RecordedAtUtc);

    private static PolicyDesiredBinding ToDomain(PolicyBindingEntity entity)
        => PolicyDesiredBinding.Reconstitute(
            new PolicyBindingId(entity.Id),
            (PolicyBindingScope)entity.Scope,
            entity.ScopeId,
            new PolicyId(entity.PolicyId),
            new PolicyRevisionId(entity.DesiredRevisionId),
            new PolicyAnalysisRunId(entity.AnalysisRunId),
            Hash256.Create(entity.BundleHash),
            (PolicyBindingState)entity.State,
            entity.ValidFromUtc,
            entity.ValidUntilUtc,
            (ulong)entity.RowVersion,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    private static PolicyBindingEntity ToEntity(PolicyDesiredBinding binding)
        => new()
        {
            Id = binding.Id.Value,
            Scope = (short)binding.Scope,
            ScopeId = binding.ScopeId,
            PolicyId = binding.PolicyId.Value,
            DesiredRevisionId = binding.DesiredRevisionId.Value,
            AnalysisRunId = binding.AnalysisRunId.Value,
            BundleHash = binding.BundleHash.Bytes.ToArray(),
            State = (short)binding.State,
            ValidFromUtc = binding.ValidFromUtc,
            ValidUntilUtc = binding.ValidUntilUtc,
            RowVersion = (long)binding.RowVersion,
            CreatedAtUtc = binding.CreatedAtUtc,
            UpdatedAtUtc = binding.UpdatedAtUtc,
        };

    private static FindingDto ToFindingDto(PolicyApprovalFinding finding)
        => new()
        {
            Code = finding.Code,
            Severity = finding.Severity,
            Message = finding.Message,
            Target = finding.Target,
            WarningHashHex = finding.WarningHash.ToString(),
        };

    private static TestDto ToTestDto(PolicyApprovalTestOutcome test)
        => new()
        {
            TestId = test.TestId.Value,
            Origin = test.Origin,
            Outcome = test.Outcome,
            Proof = test.Proof,
        };

    private static byte[] Concat(IReadOnlyList<Hash256> hashes)
    {
        byte[] buffer = new byte[hashes.Count * Hash256.Size];
        int offset = 0;
        foreach (Hash256 hash in hashes)
        {
            hash.Bytes.CopyTo(buffer.AsSpan(offset, Hash256.Size));
            offset += Hash256.Size;
        }

        return buffer;
    }

    private static List<Hash256> SplitHashes(byte[] packed)
    {
        if (packed.Length % Hash256.Size != 0)
        {
            throw new InvalidOperationException("Packed per-device hashes must be a multiple of 32 bytes.");
        }

        List<Hash256> hashes = [];
        for (int offset = 0; offset < packed.Length; offset += Hash256.Size)
        {
            hashes.Add(Hash256.Create(packed.AsSpan(offset, Hash256.Size)));
        }

        return hashes;
    }

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
        string message = pg.MessageText ?? string.Empty;
        if (pg.SqlState == PostgresErrorCodes.UniqueViolation
            && constraint.Contains("policy_approvals", StringComparison.Ordinal))
        {
            return new PersistenceConflictException(
                PolicyApprovalCodes.SeparationOfDuties,
                "This reviewer already recorded an approval vote for this bundle.",
                ex);
        }

        if (pg.SqlState == PostgresErrorCodes.UniqueViolation
            && constraint.Contains("policy_bindings", StringComparison.Ordinal))
        {
            return new PersistenceConflictException(
                PolicyApprovalCodes.BindingCardinality,
                "Active binding cardinality conflict.",
                ex);
        }

        if (message.Contains("POLICY_BINDING_CARDINALITY", StringComparison.Ordinal)
            || (pg.SqlState == PostgresErrorCodes.CheckViolation
                && constraint.Contains("policy_bindings", StringComparison.Ordinal)))
        {
            return new PersistenceConflictException(
                PolicyApprovalCodes.BindingCardinality,
                "Active EXCEPTION bindings exceed the 256 cap.",
                ex);
        }

        return null;
    }

    private sealed class FindingDto
    {
        public string Code { get; set; } = string.Empty;

        public string Severity { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string WarningHashHex { get; set; } = string.Empty;
    }

    private sealed class TestDto
    {
        public Guid TestId { get; set; }

        public string Origin { get; set; } = string.Empty;

        public string Outcome { get; set; } = string.Empty;

        public string Proof { get; set; } = string.Empty;
    }
}
