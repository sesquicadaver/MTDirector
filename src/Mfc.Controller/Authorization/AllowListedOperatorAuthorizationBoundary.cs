using System.Collections.Frozen;
using System.Reflection;
using Mfc.Application.Abstractions.Authorization;
using Mfc.Controller.Configuration;

namespace Mfc.Controller.Authorization;

/// <summary>
/// Deny-by-default operator permission allowlist (AUDIT-AUTH-01). Empty configuration
/// is fail-closed, matching <see cref="DenyAllAuthorizationBoundary"/>.
/// </summary>
public sealed class AllowListedOperatorAuthorizationBoundary : IAuthorizationBoundary
{
    private static readonly FrozenSet<string> KnownPermissions = typeof(ApplicationPermissions)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(static field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
        .Select(static field => (string)field.GetRawConstantValue()!)
        .ToFrozenSet(StringComparer.Ordinal);

    private readonly Dictionary<string, HashSet<string>> _grants;
    private readonly DenyAllAuthorizationBoundary _empty = new();

    public AllowListedOperatorAuthorizationBoundary(IEnumerable<OperatorAuthorizationEntry>? operators)
    {
        Dictionary<string, HashSet<string>> grants = new(StringComparer.Ordinal);
        foreach (OperatorAuthorizationEntry entry in operators ?? [])
        {
            string actor = entry.Actor?.Trim() ?? string.Empty;
            if (actor.Length == 0)
            {
                continue;
            }

            if (!grants.TryGetValue(actor, out HashSet<string>? permissions))
            {
                permissions = new HashSet<string>(StringComparer.Ordinal);
                grants[actor] = permissions;
            }

            foreach (string raw in entry.Permissions ?? [])
            {
                string permission = raw?.Trim() ?? string.Empty;
                if (permission.Length == 0)
                {
                    continue;
                }

                permissions.Add(permission);
            }
        }

        _grants = grants;
    }

    public Task EnsureAllowedAsync(string actor, string permission, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        cancellationToken.ThrowIfCancellationRequested();

        if (_grants.Count == 0)
        {
            return _empty.EnsureAllowedAsync(actor, permission, cancellationToken);
        }

        string trimmedActor = actor.Trim();
        string trimmedPermission = permission.Trim();
        if (!KnownPermissions.Contains(trimmedPermission)
            || !_grants.TryGetValue(trimmedActor, out HashSet<string>? allowed)
            || !allowed.Contains(trimmedPermission))
        {
            throw new UnauthorizedAccessException(
                $"Actor '{actor}' is forbidden from '{permission}'.");
        }

        return Task.CompletedTask;
    }
}
