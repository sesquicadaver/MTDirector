using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>Fixed one-shot scheduler proof resources (Onboarding Spec §12).</summary>
public static class SchedulerCapabilityProof
{
    public const string NoOpSource = ":local mfcCapabilityProbe true;";

    public const string Policy = "read,write";

    public const string DontRequirePermissions = "no";

    public static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(5);

    public static Hash256 SourceHash { get; } = Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(NoOpSource)));

    public static Hash256 ComputeSourceHash(string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        return Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
    }
}
