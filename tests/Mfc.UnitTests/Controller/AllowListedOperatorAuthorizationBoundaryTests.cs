using Mfc.Application.Abstractions.Authorization;
using Mfc.Controller.Authorization;
using Mfc.Controller.Configuration;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>AUDIT-AUTH-01: deny-by-default operator allowlist (grant / deny / unknown / empty / system actor).</summary>
public sealed class AllowListedOperatorAuthorizationBoundaryTests
{
    private const string SystemActor = "system:operational-jobs";

    [Fact]
    public async Task GrantsAllowlistedPermission()
    {
        AllowListedOperatorAuthorizationBoundary boundary = Create(
            new OperatorAuthorizationEntry
            {
                Actor = "desktop-operator",
                Permissions = [ApplicationPermissions.InventoryRead, ApplicationPermissions.SnapshotRead],
            });

        await boundary.EnsureAllowedAsync("desktop-operator", ApplicationPermissions.InventoryRead);
        await boundary.EnsureAllowedAsync(" desktop-operator ", ApplicationPermissions.SnapshotRead);
    }

    [Fact]
    public async Task DeniesPermissionNotOnActorGrant()
    {
        AllowListedOperatorAuthorizationBoundary boundary = Create(
            new OperatorAuthorizationEntry
            {
                Actor = "desktop-operator",
                Permissions = [ApplicationPermissions.InventoryRead],
            });

        UnauthorizedAccessException ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            boundary.EnsureAllowedAsync("desktop-operator", ApplicationPermissions.DeploymentWrite));
        Assert.Contains(ApplicationPermissions.DeploymentWrite, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeniesUnknownActor()
    {
        AllowListedOperatorAuthorizationBoundary boundary = Create(
            new OperatorAuthorizationEntry
            {
                Actor = "desktop-operator",
                Permissions = [ApplicationPermissions.InventoryRead],
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            boundary.EnsureAllowedAsync("other-operator", ApplicationPermissions.InventoryRead));
    }

    [Fact]
    public async Task DeniesUnknownPermissionNameEvenIfListed()
    {
        AllowListedOperatorAuthorizationBoundary boundary = Create(
            new OperatorAuthorizationEntry
            {
                Actor = "desktop-operator",
                Permissions = ["inventory.*", ApplicationPermissions.InventoryRead],
            });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            boundary.EnsureAllowedAsync("desktop-operator", "inventory.*"));
        await boundary.EnsureAllowedAsync("desktop-operator", ApplicationPermissions.InventoryRead);
    }

    [Fact]
    public async Task EmptyOperatorsFailClosedLikeDenyAll()
    {
        AllowListedOperatorAuthorizationBoundary empty = new([]);
        DenyAllAuthorizationBoundary denyAll = new();

        UnauthorizedAccessException listed = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            empty.EnsureAllowedAsync("desktop-operator", ApplicationPermissions.InventoryRead));
        UnauthorizedAccessException deny = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            denyAll.EnsureAllowedAsync("desktop-operator", ApplicationPermissions.InventoryRead));

        Assert.Contains(ApplicationPermissions.InventoryRead, listed.Message, StringComparison.Ordinal);
        Assert.Contains(ApplicationPermissions.InventoryRead, deny.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemActorWrapperStillAllowsInProcessJobsWhenAllowlistEmpty()
    {
        SystemActorAuthorizationBoundary boundary = new(
            new AllowListedOperatorAuthorizationBoundary([]),
            SystemActor);

        await boundary.EnsureAllowedAsync(SystemActor, ApplicationPermissions.DeploymentWrite);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            boundary.EnsureAllowedAsync("operator@lab", ApplicationPermissions.DeploymentWrite));
    }

    private static AllowListedOperatorAuthorizationBoundary Create(params OperatorAuthorizationEntry[] operators)
        => new(operators);
}
