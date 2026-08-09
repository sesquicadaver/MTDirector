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
using Mfc.Domain.Snapshots;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Inventory;

public sealed class CreateSiteCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }
}

public sealed class CreateSiteUseCase
{
    public const string Operation = "inventory.create_site";

    private readonly IAuthorizationBoundary _auth;
    private readonly ISiteStore _sites;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public CreateSiteUseCase(
        IAuthorizationBoundary auth,
        ISiteStore sites,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _sites = sites;
        _idempotency = idempotency;
        _audit = audit;
    }

    public async Task<ApplicationResult<SiteView>> ExecuteAsync(
        CreateSiteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryWrite, cancellationToken).ConfigureAwait(false);
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
            command.Code,
            command.Name,
        });
        ApplicationResult<SiteView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                Site? existing = await _sites.GetAsync(new SiteId(id), ct).ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Site '{id}' not found."))
                    : ApplicationResults.Ok(ViewMapper.ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        try
        {
            SiteCode code = SiteCode.Create(command.Code);
            NonEmptyName name = NonEmptyName.Create(command.Name);
            if (await _sites.CodeExistsAsync(code, cancellationToken).ConfigureAwait(false))
            {
                return ApplicationResults.Fail(
                    ApplicationError.Conflict($"Site code '{code}' already exists."));
            }

            Site site = Site.Create(code, name);
            await _sites.AddAsync(site, cancellationToken).ConfigureAwait(false);
            await _idempotency.SaveAsync(
                command.Actor, Operation, command.IdempotencyKey, requestHash, site.Id.Value, cancellationToken)
                .ConfigureAwait(false);
            await _audit.AppendAsync(
                command.Actor,
                Operation,
                JsonSerializer.Serialize(new { site_id = site.Id.Value, code = site.Code.Value }),
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(site));
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

public sealed class ListSitesQuery
{
    public required string Actor { get; init; }

    public int Limit { get; init; } = 50;

    public string? Cursor { get; init; }
}

public sealed class ListSitesUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISiteStore _sites;

    public ListSitesUseCase(IAuthorizationBoundary auth, ISiteStore sites)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(sites);
        _auth = auth;
        _sites = sites;
    }

    public async Task<ApplicationResult<SiteListPageView>> ExecuteAsync(
        ListSitesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        int limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 200);
        SitePage page = await _sites.ListPageAsync(limit, query.Cursor, cancellationToken).ConfigureAwait(false);
        return ApplicationResults.Ok(new SiteListPageView
        {
            Items = page.Items.Select(ViewMapper.ToView).ToArray(),
            NextCursor = page.NextCursor,
        });
    }
}

public sealed class ListNodesQuery
{
    public required string Actor { get; init; }

    public required Guid SiteId { get; init; }

    public int Limit { get; init; } = 50;

    public string? Cursor { get; init; }
}

public sealed class ListNodesUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISiteStore _sites;
    private readonly INodeStore _nodes;

    public ListNodesUseCase(IAuthorizationBoundary auth, ISiteStore sites, INodeStore nodes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(nodes);
        _auth = auth;
        _sites = sites;
        _nodes = nodes;
    }

    /// <summary>
    /// Lists nodes for a site with stable cursor pagination (name, then id).
    /// MVP pages in-memory from <see cref="INodeStore.ListBySiteAsync"/>.
    /// </summary>
    public async Task<ApplicationResult<NodeListPageView>> ExecuteAsync(
        ListNodesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, query.Actor, ApplicationPermissions.InventoryRead, cancellationToken).ConfigureAwait(false);
        if (authError is not null)
        {
            return ApplicationResults.Fail(authError);
        }

        SiteId siteId = new(query.SiteId);
        Site? site = await _sites.GetAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Site '{query.SiteId}' not found."));
        }

        int limit = query.Limit <= 0 ? 50 : Math.Min(query.Limit, 200);
        IReadOnlyList<Node> all = await _nodes.ListBySiteAsync(siteId, cancellationToken).ConfigureAwait(false);
        IEnumerable<Node> ordered = all
            .OrderBy(n => n.Name.Value, StringComparer.Ordinal)
            .ThenBy(n => n.Id.Value);

        if (!string.IsNullOrWhiteSpace(query.Cursor)
            && TryDecodeNodeCursor(query.Cursor, out string cursorName, out Guid cursorId))
        {
            ordered = ordered.Where(n =>
                string.Compare(n.Name.Value, cursorName, StringComparison.Ordinal) > 0
                || (string.Equals(n.Name.Value, cursorName, StringComparison.Ordinal)
                    && n.Id.Value.CompareTo(cursorId) > 0));
        }

        List<Node> page = ordered.Take(limit + 1).ToList();
        string? next = null;
        if (page.Count > limit)
        {
            Node last = page[limit - 1];
            next = EncodeNodeCursor(last.Name.Value, last.Id.Value);
            page.RemoveAt(limit);
        }

        return ApplicationResults.Ok(new NodeListPageView
        {
            Items = page.Select(ViewMapper.ToView).ToArray(),
            NextCursor = next,
        });
    }

    private static string EncodeNodeCursor(string name, Guid id)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{name}\n{id:D}"));

    private static bool TryDecodeNodeCursor(string cursor, out string name, out Guid id)
    {
        name = string.Empty;
        id = Guid.Empty;
        try
        {
            string decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            string[] parts = decoded.Split('\n');
            if (parts.Length != 2 || !Guid.TryParse(parts[1], out id))
            {
                return false;
            }

            name = parts[0];
            return name.Length > 0;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed class CreateNodeCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid SiteId { get; init; }

    public required string Name { get; init; }

    public required NodeKind DeclaredKind { get; init; }

    public required DeclaredUplinkMode DeclaredUplinkMode { get; init; }
}

public sealed class CreateNodeUseCase
{
    public const string Operation = "inventory.create_node";

    private readonly IAuthorizationBoundary _auth;
    private readonly ISiteStore _sites;
    private readonly INodeStore _nodes;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public CreateNodeUseCase(
        IAuthorizationBoundary auth,
        ISiteStore sites,
        INodeStore nodes,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _sites = sites;
        _nodes = nodes;
        _idempotency = idempotency;
        _audit = audit;
    }

    public async Task<ApplicationResult<NodeView>> ExecuteAsync(
        CreateNodeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryWrite, cancellationToken).ConfigureAwait(false);
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
            command.SiteId,
            command.Name,
            kind = command.DeclaredKind.ToString(),
            uplink = command.DeclaredUplinkMode.ToString(),
        });
        ApplicationResult<NodeView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                Node? existing = await _nodes.GetAsync(new NodeId(id), ct).ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Node '{id}' not found."))
                    : ApplicationResults.Ok(ViewMapper.ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        SiteId siteId = new(command.SiteId);
        Site? site = await _sites.GetAsync(siteId, cancellationToken).ConfigureAwait(false);
        if (site is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Site '{command.SiteId}' not found."));
        }

        try
        {
            NonEmptyName name = NonEmptyName.Create(command.Name);
            if (await _nodes.NameExistsAsync(siteId, name, cancellationToken).ConfigureAwait(false))
            {
                return ApplicationResults.Fail(
                    ApplicationError.Conflict($"Node name '{name}' already exists in the site."));
            }

            Node node = Node.Create(siteId, name, command.DeclaredKind, command.DeclaredUplinkMode);
            await _nodes.AddAsync(node, cancellationToken).ConfigureAwait(false);
            await _idempotency.SaveAsync(
                command.Actor, Operation, command.IdempotencyKey, requestHash, node.Id.Value, cancellationToken)
                .ConfigureAwait(false);
            await _audit.AppendAsync(
                command.Actor,
                Operation,
                JsonSerializer.Serialize(new { node_id = node.Id.Value, site_id = siteId.Value }),
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(node));
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

public sealed class GetNodeQuery
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }
}

public sealed class GetNodeUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;
    private readonly ISnapshotStore _snapshots;

    public GetNodeUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeviceStore devices,
        ISnapshotStore snapshots)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(snapshots);
        _auth = auth;
        _nodes = nodes;
        _devices = devices;
        _snapshots = snapshots;
    }

    public async Task<ApplicationResult<NodeDetailsView>> ExecuteAsync(
        GetNodeQuery query,
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

        IReadOnlyList<Device> devices = await _devices
            .ListByNodeAsync(node.Id, cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, DateTimeOffset> completedAtByCapture = await ResolveLastSnapshotTimesAsync(
            devices,
            cancellationToken).ConfigureAwait(false);

        DeviceView[] deviceViews = devices.Select(device =>
        {
            DateTimeOffset? lastSnapshot = null;
            if (device.LastCompletedCaptureId is Guid captureId
                && completedAtByCapture.TryGetValue(captureId, out DateTimeOffset completedAt))
            {
                lastSnapshot = completedAt;
            }

            return ViewMapper.ToView(device, lastSnapshot);
        }).ToArray();

        return ApplicationResults.Ok(new NodeDetailsView
        {
            Node = ViewMapper.ToView(node),
            Devices = deviceViews,
        });
    }

    private async Task<Dictionary<Guid, DateTimeOffset>> ResolveLastSnapshotTimesAsync(
        IReadOnlyList<Device> devices,
        CancellationToken cancellationToken)
    {
        Guid[] captureIds = devices
            .Select(d => d.LastCompletedCaptureId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();

        Dictionary<Guid, DateTimeOffset> map = new(captureIds.Length);
        foreach (Guid captureId in captureIds)
        {
            StoredSnapshot? snapshot = await _snapshots
                .GetAsync(new SnapshotId(captureId), cancellationToken)
                .ConfigureAwait(false);
            if (snapshot?.Metadata.CompletedAtUtc is DateTimeOffset completedAt)
            {
                map[captureId] = completedAt;
            }
        }

        return map;
    }
}

public sealed class RegisterDeviceCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid NodeId { get; init; }

    public required string DisplayName { get; init; }

    public required string ManagementHost { get; init; }

    public ushort ManagementPort { get; init; } = ManagementEndpoint.DefaultApiSslPort;

    public required DeviceRole Role { get; init; }
}

public sealed class RegisterDeviceUseCase
{
    public const string Operation = "inventory.register_device";

    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public RegisterDeviceUseCase(
        IAuthorizationBoundary auth,
        INodeStore nodes,
        IDeviceStore devices,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _nodes = nodes;
        _devices = devices;
        _idempotency = idempotency;
        _audit = audit;
    }

    public async Task<ApplicationResult<DeviceView>> ExecuteAsync(
        RegisterDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryWrite, cancellationToken).ConfigureAwait(false);
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
            command.DisplayName,
            command.ManagementHost,
            command.ManagementPort,
            role = command.Role.ToString(),
        });
        ApplicationResult<DeviceView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                Device? existing = await _devices.GetAsync(new DeviceId(id), ct).ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Device '{id}' not found."))
                    : ApplicationResults.Ok(ViewMapper.ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        NodeId nodeId = new(command.NodeId);
        Node? node = await _nodes.GetAsync(nodeId, cancellationToken).ConfigureAwait(false);
        if (node is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Node '{command.NodeId}' not found."));
        }

        try
        {
            Device device = node.AddDevice(
                NonEmptyName.Create(command.DisplayName),
                ManagementEndpoint.Create(command.ManagementHost, command.ManagementPort),
                command.Role);
            await _devices.AddAsync(device, cancellationToken).ConfigureAwait(false);
            await _nodes.UpdateAsync(node, cancellationToken).ConfigureAwait(false);
            await _idempotency.SaveAsync(
                command.Actor, Operation, command.IdempotencyKey, requestHash, device.Id.Value, cancellationToken)
                .ConfigureAwait(false);
            await _audit.AppendAsync(
                command.Actor,
                Operation,
                JsonSerializer.Serialize(new
                {
                    device_id = device.Id.Value,
                    node_id = nodeId.Value,
                    management_host = device.ManagementEndpoint.Host.Value,
                }),
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(device));
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

public sealed class UpdateDeviceCommand
{
    public required string Actor { get; init; }

    public required Guid IdempotencyKey { get; init; }

    public required Guid DeviceId { get; init; }

    public required ulong ExpectedRowVersion { get; init; }

    public string? DisplayName { get; init; }

    public string? ManagementHost { get; init; }

    public ushort? ManagementPort { get; init; }

    public bool? Enabled { get; init; }

    public DeviceRole? Role { get; init; }
}

public sealed class UpdateDeviceUseCase
{
    public const string Operation = "inventory.update_device";

    private readonly IAuthorizationBoundary _auth;
    private readonly IDeviceStore _devices;
    private readonly IIdempotencyStore _idempotency;
    private readonly IAuditEventWriter _audit;

    public UpdateDeviceUseCase(
        IAuthorizationBoundary auth,
        IDeviceStore devices,
        IIdempotencyStore idempotency,
        IAuditEventWriter audit)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(devices);
        ArgumentNullException.ThrowIfNull(idempotency);
        ArgumentNullException.ThrowIfNull(audit);
        _auth = auth;
        _devices = devices;
        _idempotency = idempotency;
        _audit = audit;
    }

    public async Task<ApplicationResult<DeviceView>> ExecuteAsync(
        UpdateDeviceCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ApplicationError? authError = await Auth.EnsureAsync(
            _auth, command.Actor, ApplicationPermissions.InventoryWrite, cancellationToken).ConfigureAwait(false);
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
            command.DeviceId,
            command.ExpectedRowVersion,
            command.DisplayName,
            command.ManagementHost,
            command.ManagementPort,
            command.Enabled,
            role = command.Role?.ToString(),
        });
        ApplicationResult<DeviceView>? replay = await IdempotencySupport.TryReplayAsync(
            _idempotency,
            command.Actor,
            Operation,
            command.IdempotencyKey,
            requestHash,
            async (id, ct) =>
            {
                Device? existing = await _devices.GetAsync(new DeviceId(id), ct).ConfigureAwait(false);
                return existing is null
                    ? ApplicationResults.Fail(ApplicationError.NotFound($"Device '{id}' not found."))
                    : ApplicationResults.Ok(ViewMapper.ToView(existing));
            },
            cancellationToken).ConfigureAwait(false);
        if (replay is not null)
        {
            return replay.Value;
        }

        Device? device = await _devices.GetAsync(new DeviceId(command.DeviceId), cancellationToken)
            .ConfigureAwait(false);
        if (device is null)
        {
            return ApplicationResults.Fail(ApplicationError.NotFound($"Device '{command.DeviceId}' not found."));
        }

        if (device.RowVersion != command.ExpectedRowVersion)
        {
            return ApplicationResults.Fail(
                ApplicationError.Conflict(
                    $"Device row_version mismatch: expected {command.ExpectedRowVersion}, actual {device.RowVersion}."));
        }

        try
        {
            if (command.DisplayName is not null)
            {
                device.Rename(NonEmptyName.Create(command.DisplayName));
            }

            if (command.ManagementHost is not null || command.ManagementPort is not null)
            {
                string host = command.ManagementHost ?? device.ManagementEndpoint.Host.Value;
                ushort port = command.ManagementPort ?? device.ManagementEndpoint.Port;
                device.Relocate(ManagementEndpoint.Create(host, port));
            }

            if (command.Enabled is not null)
            {
                device.SetEnabled(command.Enabled.Value);
            }

            if (command.Role is not null)
            {
                device.SetRole(command.Role.Value);
            }

            await _devices.UpdateAsync(device, cancellationToken).ConfigureAwait(false);
            await _idempotency.SaveAsync(
                command.Actor, Operation, command.IdempotencyKey, requestHash, device.Id.Value, cancellationToken)
                .ConfigureAwait(false);
            await _audit.AppendAsync(
                command.Actor,
                Operation,
                JsonSerializer.Serialize(new
                {
                    device_id = device.Id.Value,
                    row_version = device.RowVersion,
                }),
                cancellationToken).ConfigureAwait(false);
            return ApplicationResults.Ok(ViewMapper.ToView(device));
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
