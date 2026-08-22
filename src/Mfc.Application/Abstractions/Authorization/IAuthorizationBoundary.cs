namespace Mfc.Application.Abstractions.Authorization;

/// <summary>
/// Thin authorization boundary for use cases. Does not implement controller auth policy —
/// only checks that an actor is allowed to invoke a named permission.
/// </summary>
public interface IAuthorizationBoundary
{
    Task EnsureAllowedAsync(string actor, string permission, CancellationToken cancellationToken = default);
}

public static class ApplicationPermissions
{
    public const string InventoryWrite = "inventory.write";
    public const string InventoryRead = "inventory.read";
    public const string ConnectionProfileWrite = "connection_profile.write";
    public const string DiscoveryRead = "discovery.read";
    public const string SnapshotCapture = "snapshot.capture";
    public const string SnapshotRead = "snapshot.read";
    public const string SnapshotCompare = "snapshot.compare";

    /// <summary>Required to return raw (sanitized) snapshot payload bytes (M1-23 AC#11).</summary>
    public const string SnapshotRawRead = "snapshot.raw.read";

    /// <summary>Read zone definitions and node bindings (M2-05).</summary>
    public const string ZoneRead = "zone.read";

    /// <summary>Mutate zone definitions and node bindings (M2-05).</summary>
    public const string ZoneWrite = "zone.write";

    /// <summary>Read policy revisions and rules (M2-06).</summary>
    public const string PolicyRead = "policy.read";

    /// <summary>Create drafts and mutate policy rules (M2-06).</summary>
    public const string PolicyWrite = "policy.write";

    /// <summary>Record approval votes (M2-17). Does not activate desired binding.</summary>
    public const string PolicyApprove = "policy.approve";

    /// <summary>Security/network-owner stamp required for CRITICAL approval (M2-17).</summary>
    public const string PolicyApproveSecurity = "policy.approve.security";

    /// <summary>Activate or expire desired bindings without deploying (M2-17).</summary>
    public const string PolicyBind = "policy.bind";

    /// <summary>Read onboarding plans, operations, and recovery status (M5-09).</summary>
    public const string OnboardingRead = "onboarding.read";

    /// <summary>Create plans and start/rollback onboarding (M5-09).</summary>
    public const string OnboardingWrite = "onboarding.write";

    /// <summary>Read deployment plans, operations, and recovery status (M4-12).</summary>
    public const string DeploymentRead = "deployment.read";

    /// <summary>Create plans and start/rollback deployment (M4-12).</summary>
    public const string DeploymentWrite = "deployment.write";

    /// <summary>Read append-only audit events (M6-04). No mutate path.</summary>
    public const string AuditRead = "audit.read";

    /// <summary>Ingest normalized incident signals (M7.3-01). No raw syslog store.</summary>
    public const string IncidentSignalIngest = "incident.signal.ingest";

    /// <summary>Resolve historical active-state context for incident correlation (M7.3-02).</summary>
    public const string IncidentContextRead = "incident.context.read";
}
