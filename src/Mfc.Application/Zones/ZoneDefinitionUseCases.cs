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

public sealed class CreateZoneDefinitionCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required PolicyOwnerScope OwnerScope { get; init; }

    public Guid? OwnerId { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }
}

public sealed class CreateZoneDefinitionUseCase
{
    public const string Operation = "zone.create_definition";

    private readonly IAuthorizationBoundary _auth;
    private readonly IZoneDefinitionStore _zones;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public CreateZoneDefinitionUseCase(
        IAuthorizationBoundary auth,
        IZoneDefinitionStore zones,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _zones = zones;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ZoneDefinitionView>> ExecuteAsync(
        CreateZoneDefinitionCommand command,
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
            owner_scope = command.OwnerScope.ToString(),
            command.OwnerId,
            command.Key,
            command.Name,
            command.Description,
        });
        ApplicationResult<ZoneDefinitionView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                ZoneDefinition? existing = await _zones.GetAsync(new ZoneId(id), ct).ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Zone '{id}' not found."))
                    : ApplicationResults.Ok(ViewMapper.ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        try
        {
            NonEmptyName key = NonEmptyName.Create(command.Key);
            NonEmptyName name = NonEmptyName.Create(command.Name);
            if (await _zones.KeyExistsAsync(command.OwnerScope, command.OwnerId, key, cancellationToken: cancellationToken)
                    .ConfigureAwait(false))
            {
                return ApplicationResults.Fail(
                    ApplicationError.Conflict($"Zone key '{key}' already exists in owner scope."));
            }

            ZoneDefinition zone = ZoneDefinition.Create(
                command.OwnerScope,
                command.OwnerId,
                key,
                name,
                command.Description);
            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    await _zones.AddAsync(zone, ct).ConfigureAwait(false);
                    await _idempotency.SaveAsync(
                            command.Actor, Operation, command.IdempotencyKey, requestHash, zone.Id.Value, ct)
                        .ConfigureAwait(false);
                    await _audit.AppendAsync(
                            command.Actor,
                            Operation,
                            JsonSerializer.Serialize(new { zone_id = zone.Id.Value, key = zone.Key.Value }),
                            ct)
                        .ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(zone));
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
}

public sealed class UpdateZoneDefinitionCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid ZoneId { get; init; }

    public required ulong ExpectedRowVersion { get; init; }

    public string? Name { get; init; }

    public string? Description { get; init; }

    public bool ClearDescription { get; init; }
}

public sealed class UpdateZoneDefinitionUseCase
{
    public const string Operation = "zone.update_definition";

    private readonly IAuthorizationBoundary _auth;
    private readonly IZoneDefinitionStore _zones;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateZoneDefinitionUseCase(
        IAuthorizationBoundary auth,
        IZoneDefinitionStore zones,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _zones = zones;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ZoneDefinitionView>> ExecuteAsync(
        UpdateZoneDefinitionCommand command,
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
            command.ZoneId,
            command.ExpectedRowVersion,
            command.Name,
            command.Description,
            command.ClearDescription,
        });
        ApplicationResult<ZoneDefinitionView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                ZoneDefinition? existing = await _zones.GetAsync(new ZoneId(id), ct).ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Zone '{id}' not found."))
                    : ApplicationResults.Ok(ViewMapper.ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        ZoneDefinition? zone = await _zones.GetAsync(new ZoneId(command.ZoneId), cancellationToken)
            .ConfigureAwait(false);
        if (zone is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Zone '{command.ZoneId}' not found."));
        }

        if (zone.RowVersion != command.ExpectedRowVersion)
        {
            return ApplicationResults.Fail(
                ApplicationError.Conflict(
                    $"Zone row_version mismatch: expected {command.ExpectedRowVersion}, actual {zone.RowVersion}."));
        }

        try
        {
            if (command.Name is not null)
            {
                zone.Rename(NonEmptyName.Create(command.Name));
            }

            if (command.ClearDescription)
            {
                zone.SetDescription(null);
            }
            else if (command.Description is not null)
            {
                zone.SetDescription(command.Description);
            }

            await _unitOfWork.ExecuteAsync(
                async ct =>
                {
                    await _zones.UpdateAsync(zone, ct).ConfigureAwait(false);
                    await _idempotency.SaveAsync(
                            command.Actor, Operation, command.IdempotencyKey, requestHash, zone.Id.Value, ct)
                        .ConfigureAwait(false);
                    await _audit.AppendAsync(
                            command.Actor,
                            Operation,
                            JsonSerializer.Serialize(new { zone_id = zone.Id.Value, row_version = zone.RowVersion }),
                            ct)
                        .ConfigureAwait(false);
                },
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(zone));
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
}

public sealed class ListZoneDefinitionsQuery
{
    public required string Actor { get; init; }

    public PolicyOwnerScope? OwnerScope { get; init; }

    public Guid? OwnerId { get; init; }
}

public sealed class ListZoneDefinitionsUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly IZoneDefinitionStore _zones;

    public ListZoneDefinitionsUseCase(IAuthorizationBoundary auth, IZoneDefinitionStore zones)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(zones);
        _auth = auth;
        _zones = zones;
    }

    public async Task<ApplicationResult<IReadOnlyList<ZoneDefinitionView>>> ExecuteAsync(
        ListZoneDefinitionsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.ZoneRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        IReadOnlyList<ZoneDefinition> zones = await _zones
            .ListAsync(query.OwnerScope, query.OwnerId, cancellationToken)
            .ConfigureAwait(false);
        return ApplicationResults.Ok<IReadOnlyList<ZoneDefinitionView>>(
            zones.Select(ViewMapper.ToView).ToArray());
    }
}

public sealed class DeleteZoneDefinitionCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid ZoneId { get; init; }

    public required ulong ExpectedRowVersion { get; init; }
}

public sealed class DeleteZoneDefinitionUseCase
{
    public const string Operation = "zone.delete_definition";

    private readonly IAuthorizationBoundary _auth;
    private readonly IZoneDefinitionStore _zones;
    private readonly INodeZoneBindingStore _bindings;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteZoneDefinitionUseCase(
        IAuthorizationBoundary auth,
        IZoneDefinitionStore zones,
        INodeZoneBindingStore bindings,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(zones);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _auth = auth;
        _zones = zones;
        _bindings = bindings;
        _idempotency = idempotency;
        _audit = audit;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<bool>> ExecuteAsync(
        DeleteZoneDefinitionCommand command,
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
            command.ZoneId,
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

        ZoneId zoneId = new(command.ZoneId);
        ZoneDefinition? zone = await _zones.GetAsync(zoneId, cancellationToken).ConfigureAwait(false);
        if (zone is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Zone '{command.ZoneId}' not found."));
        }

        if (zone.RowVersion != command.ExpectedRowVersion)
        {
            return ApplicationResults.Fail(
                ApplicationError.Conflict(
                    $"Zone row_version mismatch: expected {command.ExpectedRowVersion}, actual {zone.RowVersion}."));
        }

        int bindingCount = await _bindings.CountByZoneAsync(zoneId, cancellationToken).ConfigureAwait(false);
        if (bindingCount > 0)
        {
            return ApplicationResults.Fail(
                ApplicationError.Conflict(
                    $"Zone '{command.ZoneId}' still has {bindingCount} node binding(s); delete bindings first."));
        }

        await _unitOfWork.ExecuteAsync(
            async ct =>
            {
                await _zones.DeleteAsync(zoneId, ct).ConfigureAwait(false);
                await _idempotency.SaveAsync(
                        command.Actor, Operation, command.IdempotencyKey, requestHash, zoneId.Value, ct)
                    .ConfigureAwait(false);
                await _audit.AppendAsync(
                        command.Actor,
                        Operation,
                        JsonSerializer.Serialize(new { zone_id = zoneId.Value }),
                        ct)
                    .ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(true);
    }
}
