using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>One sealed compile artifact ready for Controller CreatePlanFromSealedArtifacts.</summary>
public sealed class SealedCompileArtifactRef
{
    public required Guid DeviceId { get; init; }

    public required Sha256 ResourceHash { get; init; }

    public required string ArtifactId { get; init; }
}

/// <summary>Node-scoped Compile → Deploy handoff (AUDIT-GUI-01 / AUDIT-CTX-01 invalidation on node switch).</summary>
public sealed class SealedCompileDeployHandoff
{
    public required Guid NodeId { get; init; }

    public required Guid AnalysisRunId { get; init; }

    public required Sha256 LogicalEffectivePolicyHash { get; init; }

    public required IReadOnlyList<SealedCompileArtifactRef> Artifacts { get; init; }
}

/// <summary>Stores the latest successful CompileNodeFilterArtifacts result for Deployment CreatePlan.</summary>
public interface ISealedCompileDeployHandoffStore
{
    SealedCompileDeployHandoff? Current { get; }

    void Replace(SealedCompileDeployHandoff handoff);

    void Clear();

    /// <summary>Drops handoff when selection leaves the owning node (AUDIT-CTX-01).</summary>
    void InvalidateUnlessNode(Guid? nodeId);
}

/// <inheritdoc />
public sealed class SealedCompileDeployHandoffStore : ISealedCompileDeployHandoffStore
{
    private readonly object _gate = new();
    private SealedCompileDeployHandoff? _current;

    public SealedCompileDeployHandoff? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Replace(SealedCompileDeployHandoff handoff)
    {
        ArgumentNullException.ThrowIfNull(handoff);
        if (handoff.Artifacts.Count == 0)
        {
            throw new ArgumentException("Sealed compile handoff requires at least one artifact.", nameof(handoff));
        }

        lock (_gate)
        {
            _current = handoff;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _current = null;
        }
    }

    public void InvalidateUnlessNode(Guid? nodeId)
    {
        lock (_gate)
        {
            if (_current is null)
            {
                return;
            }

            if (nodeId is null || _current.NodeId != nodeId.Value)
            {
                _current = null;
            }
        }
    }
}
