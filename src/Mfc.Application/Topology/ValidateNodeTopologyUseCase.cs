using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Common;
using Mfc.Domain.Capabilities;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Topology;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Topology;

public sealed class ValidateNodeTopologyCommand
{
    public required string Actor { get; init; }

    public required Node Node { get; init; }

    public required IReadOnlyList<DeviceTopologyFacts> DeviceFacts { get; init; }

    /// <summary>Optional per-device capability caches (M1-17 invalidation rule).</summary>
    public IReadOnlyDictionary<DeviceId, TopologyValidationCache>? CapabilityCaches { get; init; }
}

/// <summary>
/// Application port for node topology validation (M1-18).
/// Passes through explicit observations only — no network scan.
/// </summary>
public sealed class ValidateNodeTopologyUseCase
{
    private readonly IAuthorizationBoundary _auth;

    public ValidateNodeTopologyUseCase(IAuthorizationBoundary auth)
    {
        ArgumentNullException.ThrowIfNull(auth);
        _auth = auth;
    }

    public async Task<ApplicationResult<NodeTopologyValidationResult>> ExecuteAsync(
        ValidateNodeTopologyCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Node);
        ArgumentNullException.ThrowIfNull(command.DeviceFacts);

        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        NodeTopologyValidationResult result = NodeTopologyValidator.Validate(
            command.Node,
            command.DeviceFacts,
            command.CapabilityCaches);

        return ApplicationResults.Ok(result);
    }
}
