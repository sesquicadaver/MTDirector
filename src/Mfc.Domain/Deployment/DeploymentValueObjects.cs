using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Deployment;

/// <summary>Exact permanent-anchor jump target captured on a deployment plan (Safe Deployment Spec §9).</summary>
public sealed class AnchorTarget : IEquatable<AnchorTarget>
{
    public AnchorTarget(AnchorKey key, string jumpTarget)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(jumpTarget);
        Key = key;
        JumpTarget = jumpTarget.Trim();
    }

    public AnchorKey Key { get; }

    public string JumpTarget { get; }

    public bool Equals(AnchorTarget? other)
        => other is not null
           && Key.Equals(other.Key)
           && string.Equals(JumpTarget, other.JumpTarget, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is AnchorTarget other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Key, JumpTarget);
}

/// <summary>Bounded verification probe recorded on the plan. Destination is a host identity, not a script.</summary>
public sealed class DeploymentProbe
{
    public const int MinTimeoutMs = 100;

    public const int MaxTimeoutMs = 5000;

    public DeploymentProbe(DeploymentProbeKind kind, string destination, int timeoutMilliseconds)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new DomainInvariantException($"Unknown deployment probe kind '{kind}'.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(destination);
        if (timeoutMilliseconds is < MinTimeoutMs or > MaxTimeoutMs)
        {
            throw new DomainInvariantException(
                $"Probe timeout must be between {MinTimeoutMs} and {MaxTimeoutMs} ms.");
        }

        Kind = kind;
        Destination = destination.Trim();
        TimeoutMilliseconds = timeoutMilliseconds;
    }

    public DeploymentProbeKind Kind { get; }

    public string Destination { get; }

    public int TimeoutMilliseconds { get; }
}
