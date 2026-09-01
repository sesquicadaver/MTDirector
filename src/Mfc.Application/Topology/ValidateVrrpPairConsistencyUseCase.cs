using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Topology;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Topology;

public sealed class ValidateVrrpPairConsistencyQuery
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }
}

public sealed class VrrpPairConsistencyFindingView
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public required string Severity { get; init; }

    public string? Subject { get; init; }

    public Guid? DeviceId { get; init; }
}

public sealed class VrrpPairConsistencyView
{
    public required Guid NodeId { get; init; }

    public required bool Passed { get; init; }

    public required int MemberCount { get; init; }

    public required int CaptureCount { get; init; }

    public required IReadOnlyList<VrrpPairConsistencyFindingView> Findings { get; init; }
}

/// <summary>
/// Node-scoped VRRP pair consistency from last completed captures (W6-02).
/// Read-only; does not StartCapture (Desktop may capture members first, then call this).
/// </summary>
public sealed class ValidateVrrpPairConsistencyUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly VrrpPairConsistencyLoader _loader;

    public ValidateVrrpPairConsistencyUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        VrrpPairConsistencyLoader loader)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(loader);
        _auth = auth;
        _nodes = nodes;
        _loader = loader;
    }

    public async Task<ApplicationResult<VrrpPairConsistencyView>> ExecuteAsync(
        ValidateVrrpPairConsistencyQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        Node? node = await _nodes.GetAsync(new NodeId(query.NodeId), cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{query.NodeId}' not found."));
        }

        VrrpPairConsistencyResult result = await _loader
            .AnalyzeNodeAsync(node, cancellationToken)
            .ConfigureAwait(false);
        return ApplicationResults.Ok(ToView(result));
    }

    internal static VrrpPairConsistencyView ToView(VrrpPairConsistencyResult result)
        => new()
        {
            NodeId = result.NodeId.Value,
            Passed = result.Passed,
            MemberCount = result.MemberCount,
            CaptureCount = result.CaptureCount,
            Findings = result.Findings.Select(static f => new VrrpPairConsistencyFindingView
            {
                Code = f.Code,
                Message = f.Message,
                Severity = f.Severity.ToString(),
                Subject = f.Subject,
                DeviceId = f.DeviceId,
            }).ToArray(),
        };
}
