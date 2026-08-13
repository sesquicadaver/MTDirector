using System.Text.Json;
using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;
using Mfc.Domain.Policy.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Zones;

public sealed class UpsertNodeZoneBindingCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid NodeId { get; init; }

    public required Guid ZoneId { get; init; }

    public required NodeZoneBindingKind Kind { get; init; }

    public required IReadOnlyList<string> Values { get; init; }

    /// <summary>Expected dependency hash (32 bytes). When null, defaults to empty-resolved hash.</summary>
    public byte[]? ExpectedDependencyHash { get; init; }

    /// <summary>Required when updating an existing binding; ignored on create.</summary>
    public ulong? ExpectedRowVersion { get; init; }
}

public sealed class UpsertNodeZoneBindingUseCase
{
    public const string Operation = "zone.upsert_binding";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IZoneDefinitionStore _zones;
    private readonly INodeZoneBindingStore _bindings;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public UpsertNodeZoneBindingUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IZoneDefinitionStore zones,
        INodeZoneBindingStore bindings,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _nodes = nodes;
        _zones = zones;
        _bindings = bindings;
        _idempotency = idempotency;
        _audit = audit;
    }

    public async Task<ApplicationResult<NodeZoneBindingView>> ExecuteAsync(
        UpsertNodeZoneBindingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.ZoneWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? keyError = IdempotencySupport.ValidateKey(command.IdempotencyKey);
        if (keyError is not null)
        {
            return ApplicationResults.Fail(keyError);
        }

        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.NodeId,
            command.ZoneId,
            kind = command.Kind.ToString(),
            values = command.Values,
            expected_hash = command.ExpectedDependencyHash is null
                ? null
                : Convert.ToHexString(command.ExpectedDependencyHash).ToLowerInvariant(),
            command.ExpectedRowVersion,
        });
        ApplicationResult<NodeZoneBindingView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                NodeZoneBinding? existing = await _bindings.GetAsync(new NodeZoneBindingId(id), ct)
                    .ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Binding '{id}' not found."))
                    : ApplicationResults.Ok(ViewMapper.ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        NodeId nodeId = new(command.NodeId);
        ZoneId zoneId = new(command.ZoneId);
        Node? node = await _nodes.GetAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{command.NodeId}' not found."));
        }

        ZoneDefinition? zone = await _zones.GetAsync(zoneId, cancellationToken).ConfigureAwait(false);
        if (zone is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Zone '{command.ZoneId}' not found."));
        }

        try
        {
            Hash256 expectedHash = ResolveExpectedHash(command.Kind, command.Values, command.ExpectedDependencyHash);
            NodeZoneBinding? existing = await _bindings
                .GetByNodeAndZoneAsync(nodeId, zoneId, cancellationToken)
                .ConfigureAwait(false);

            if (existing is null)
            {
                NodeZoneBinding created = NodeZoneBinding.Create(
                    nodeId,
                    zoneId,
                    command.Kind,
                    command.Values,
                    expectedHash);
                await _bindings.AddAsync(created, cancellationToken).ConfigureAwait(false);
                await _idempotency.SaveAsync(
                    command.Actor, Operation, command.IdempotencyKey, requestHash, created.Id.Value, cancellationToken)
                    .ConfigureAwait(false);
                await _audit.AppendAsync(
                    command.Actor,
                    Operation,
                    JsonSerializer.Serialize(new
                    {
                        binding_id = created.Id.Value,
                        node_id = nodeId.Value,
                        zone_id = zoneId.Value,
                        action = "create",
                    }),
                    cancellationToken).ConfigureAwait(false);
                return ApplicationResults.Ok(ViewMapper.ToView(created));
            }

            if (command.ExpectedRowVersion is null)
            {
                return ApplicationResults.Fail(
                    ApplicationError.Validation("expected_row_version is required when updating an existing binding."));
            }

            if (existing.RowVersion != command.ExpectedRowVersion.Value)
            {
                return ApplicationResults.Fail(
                    ApplicationError.Conflict(
                        $"Binding row_version mismatch: expected {command.ExpectedRowVersion}, actual {existing.RowVersion}."));
            }

            existing.ReplaceBinding(command.Kind, command.Values, expectedHash);
            await _bindings.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            await _idempotency.SaveAsync(
                command.Actor, Operation, command.IdempotencyKey, requestHash, existing.Id.Value, cancellationToken)
                .ConfigureAwait(false);
            await _audit.AppendAsync(
                command.Actor,
                Operation,
                JsonSerializer.Serialize(new
                {
                    binding_id = existing.Id.Value,
                    node_id = nodeId.Value,
                    zone_id = zoneId.Value,
                    action = "update",
                    row_version = existing.RowVersion,
                }),
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(existing));
        }
        catch (DomainInvariantException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApplicationResults.Fail(ApplicationError.Validation(ex.Message));
        }
    }

    private static Hash256 ResolveExpectedHash(
        NodeZoneBindingKind kind,
        IReadOnlyList<string> values,
        byte[]? expectedBytes)
    {
        if (expectedBytes is { Length: Hash256.Size })
        {
            return Hash256.Create(expectedBytes);
        }

        if (expectedBytes is { Length: > 0 })
        {
            throw new DomainInvariantException("expected_dependency_hash must be exactly 32 bytes.");
        }

        return NodeZoneBinding.ComputeDependencyHash(kind, values, resolvedMembers: []);
    }
}

public sealed class DeleteNodeZoneBindingCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid BindingId { get; init; }

    public required ulong ExpectedRowVersion { get; init; }
}

public sealed class DeleteNodeZoneBindingUseCase
{
    public const string Operation = "zone.delete_binding";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeZoneBindingStore _bindings;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public DeleteNodeZoneBindingUseCase(
        IAuthorizationBoundary auth,
        INodeZoneBindingStore bindings,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _bindings = bindings;
        _idempotency = idempotency;
        _audit = audit;
    }

    public async Task<ApplicationResult<bool>> ExecuteAsync(
        DeleteNodeZoneBindingCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.ZoneWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        ApplicationError? keyError = IdempotencySupport.ValidateKey(command.IdempotencyKey);
        if (keyError is not null)
        {
            return ApplicationResults.Fail(keyError);
        }

        byte[] requestHash = IdempotencySupport.HashRequest(new
        {
            command.BindingId,
            command.ExpectedRowVersion,
        });
        ApplicationResult<bool>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            (_, _) => Task.FromResult(ApplicationResults.Ok(true)),
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        NodeZoneBindingId bindingId = new(command.BindingId);
        NodeZoneBinding? binding = await _bindings.GetAsync(bindingId, cancellationToken).ConfigureAwait(false);
        if (binding is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Binding '{command.BindingId}' not found."));
        }

        if (binding.RowVersion != command.ExpectedRowVersion)
        {
            return ApplicationResults.Fail(
                ApplicationError.Conflict(
                    $"Binding row_version mismatch: expected {command.ExpectedRowVersion}, actual {binding.RowVersion}."));
        }

        await _bindings.DeleteAsync(bindingId, cancellationToken).ConfigureAwait(false);
        await _idempotency.SaveAsync(
            command.Actor, Operation, command.IdempotencyKey, requestHash, bindingId.Value, cancellationToken)
            .ConfigureAwait(false);
        await _audit.AppendAsync(
            command.Actor,
            Operation,
            JsonSerializer.Serialize(new { binding_id = bindingId.Value }),
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(true);
    }
}

public sealed class ListNodeZoneBindingsQuery
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }
}

public sealed class ListNodeZoneBindingsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly INodeZoneBindingStore _bindings;

    public ListNodeZoneBindingsUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        INodeZoneBindingStore bindings)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(bindings);
        _auth = auth;
        _nodes = nodes;
        _bindings = bindings;
    }

    public async Task<ApplicationResult<IReadOnlyList<NodeZoneBindingView>>> ExecuteAsync(
        ListNodeZoneBindingsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.ZoneRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        NodeId nodeId = new(query.NodeId);
        Node? node = await _nodes.GetAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{query.NodeId}' not found."));
        }

        IReadOnlyList<NodeZoneBinding> bindings = await _bindings
            .ListByNodeAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);
        return ApplicationResults.Ok<IReadOnlyList<NodeZoneBindingView>>(
            bindings.Select(ViewMapper.ToView).ToArray());
    }
}

public sealed class ResolveZonesForDeviceCommand
{
    public required string Actor { get; init; }

    public required Guid DeviceId { get; init; }
}

public sealed class ResolveZonesForDeviceUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly INodeZoneBindingStore _bindings;
    private readonly IZoneResolveObservationSource _observations;

    public ResolveZonesForDeviceUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        INodeZoneBindingStore bindings,
        IZoneResolveObservationSource observations)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(observations);
        _auth = auth;
        _devices = devices;
        _bindings = bindings;
        _observations = observations;
    }

    public async Task<ApplicationResult<ZoneResolveBatchView>> ExecuteAsync(
        ResolveZonesForDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.ZoneWrite, cancellationToken).ConfigureAwait(false);
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

        ZoneResolveDeviceObservation observation = await _observations
            .GetForDeviceAsync(deviceId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<NodeZoneBinding> bindings = await _bindings
            .ListByNodeAsync(device.NodeId, cancellationToken)
            .ConfigureAwait(false);

        List<ZoneBindingResolveView> results = [];
        foreach (NodeZoneBinding binding in bindings.OrderBy(b => b.Id.Value))
        {
            ulong loadedVersion = binding.RowVersion;
            ZoneBindingResolveResult resolved = ZoneResolveEngine.Resolve(binding, observation);
            binding.ApplyResolveOutcome(resolved.FreshDependencyHash, resolved.AnalysisStale);

            NodeZoneBinding? current = await _bindings.GetAsync(binding.Id, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Binding '{binding.Id.Value}' not found."));
            }

            if (current.RowVersion != loadedVersion)
            {
                return ApplicationResults.Fail(
                    ApplicationError.Conflict(
                        $"Binding row_version mismatch during resolve: expected {loadedVersion}, actual {current.RowVersion}."));
            }

            await _bindings.UpdateAsync(binding, cancellationToken).ConfigureAwait(false);
            results.Add(ViewMapper.ToView(resolved, binding));
        }

        return ApplicationResults.Ok(new ZoneResolveBatchView { Results = results });
    }
}

public sealed class ResolveZonesForNodeCommand
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }
}

public sealed class ResolveZonesForNodeUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;
    private readonly INodeZoneBindingStore _bindings;
    private readonly IZoneResolveObservationSource _observations;

    public ResolveZonesForNodeUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeviceStore devices,
        INodeZoneBindingStore bindings,
        IZoneResolveObservationSource observations)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(observations);
        _auth = auth;
        _nodes = nodes;
        _devices = devices;
        _bindings = bindings;
        _observations = observations;
    }

    public async Task<ApplicationResult<ZoneResolveBatchView>> ExecuteAsync(
        ResolveZonesForNodeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.ZoneWrite, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        NodeId nodeId = new(command.NodeId);
        Node? node = await _nodes.GetAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{command.NodeId}' not found."));
        }

        IReadOnlyList<NodeZoneBinding> bindings = await _bindings
            .ListByNodeAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<Device> devices = await _devices
            .ListByNodeAsync(nodeId, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, ulong> loadedVersions = bindings.ToDictionary(b => b.Id.Value, b => b.RowVersion);
        Dictionary<Guid, (bool AnyStale, Hash256? LastFresh)> outcomes = bindings.ToDictionary(
            b => b.Id.Value,
            _ => (AnyStale: false, LastFresh: (Hash256?)null));
        List<(ZoneBindingResolveResult Resolved, Guid BindingId)> pending = [];

        foreach (Device device in devices.OrderBy(d => d.DisplayName.Value, StringComparer.Ordinal)
                     .ThenBy(d => d.Id.Value))
        {
            ZoneResolveDeviceObservation observation = await _observations
                .GetForDeviceAsync(device.Id, cancellationToken)
                .ConfigureAwait(false);
            foreach (NodeZoneBinding binding in bindings.OrderBy(b => b.Id.Value))
            {
                ZoneBindingResolveResult resolved = ZoneResolveEngine.Resolve(binding, observation);
                (bool anyStale, Hash256? _) = outcomes[binding.Id.Value];
                outcomes[binding.Id.Value] = (anyStale || resolved.AnalysisStale, resolved.FreshDependencyHash);
                pending.Add((resolved, binding.Id.Value));
            }
        }

        Dictionary<Guid, NodeZoneBinding> bindingById = bindings.ToDictionary(b => b.Id.Value);
        foreach (NodeZoneBinding binding in bindings.OrderBy(b => b.Id.Value))
        {
            (bool anyStale, Hash256? lastFresh) = outcomes[binding.Id.Value];
            if (lastFresh is null)
            {
                continue;
            }

            ulong loadedVersion = loadedVersions[binding.Id.Value];
            binding.ApplyResolveOutcome(lastFresh, anyStale);

            NodeZoneBinding? current = await _bindings.GetAsync(binding.Id, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                return ApplicationResults.Fail(ApplicationError.NotFound($"Binding '{binding.Id.Value}' not found."));
            }

            if (current.RowVersion != loadedVersion)
            {
                return ApplicationResults.Fail(
                    ApplicationError.Conflict(
                        $"Binding row_version mismatch during resolve: expected {loadedVersion}, actual {current.RowVersion}."));
            }

            await _bindings.UpdateAsync(binding, cancellationToken).ConfigureAwait(false);
        }

        List<ZoneBindingResolveView> results = pending
            .Select(p => ViewMapper.ToView(p.Resolved, bindingById[p.BindingId]))
            .ToList();

        return ApplicationResults.Ok(new ZoneResolveBatchView { Results = results });
    }
}
