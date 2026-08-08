using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Common;
using Mfc.Application.Mapping;
using Mfc.Application.Models;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Auth = Mfc.Application.Common.AuthorizationGuard;

namespace Mfc.Application.Inventory;

public sealed class CreateSiteCommand
{
    public required string Actor { get; init; }

    public required string Code { get; init; }

    public required string Name { get; init; }
}

public sealed class CreateSiteUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISiteStore _sites;

    public CreateSiteUseCase(IAuthorizationBoundary auth, ISiteStore sites)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(sites);
        _auth = auth;
        _sites = sites;
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

public sealed class CreateNodeCommand
{
    public required string Actor { get; init; }

    public required Guid SiteId { get; init; }

    public required string Name { get; init; }

    public required NodeKind DeclaredKind { get; init; }

    public required DeclaredUplinkMode DeclaredUplinkMode { get; init; }
}

public sealed class CreateNodeUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly ISiteStore _sites;
    private readonly INodeStore _nodes;

    public CreateNodeUseCase(IAuthorizationBoundary auth, ISiteStore sites, INodeStore nodes)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(sites);
        ArgumentNullException.ThrowIfNull(nodes);
        _auth = auth;
        _sites = sites;
        _nodes = nodes;
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

public sealed class RegisterDeviceCommand
{
    public required string Actor { get; init; }

    public required Guid NodeId { get; init; }

    public required string DisplayName { get; init; }

    public required string ManagementHost { get; init; }

    public ushort ManagementPort { get; init; } = ManagementEndpoint.DefaultApiSslPort;

    public required DeviceRole Role { get; init; }
}

public sealed class RegisterDeviceUseCase
{
    private readonly IAuthorizationBoundary _auth;
    private readonly INodeStore _nodes;
    private readonly IDeviceStore _devices;

    public RegisterDeviceUseCase(IAuthorizationBoundary auth, INodeStore nodes, IDeviceStore devices)
    {
        ArgumentNullException.ThrowIfNull(auth);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(devices);
        _auth = auth;
        _nodes = nodes;
        _devices = devices;
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
