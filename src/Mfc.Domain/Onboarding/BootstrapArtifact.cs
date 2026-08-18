using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Fixed pass-through bootstrap artifact (Onboarding Spec §23). Seed hash is SHA-256 of the UTF-8 seed.
/// </summary>
public static class BootstrapArtifact
{
    public const string Seed = "mfc.bootstrap-artifact.v1";

    public const string ArtifactId = "8e40b9d4d67d42d6";

    public const string ReturnComment = "mfc:s:bootstrap-return:v1";

    public const string Sha256Hex = "8e40b9d4d67d42d6ff7111669c7a5dea61e691b9155fb804c6e263053f7b702e";

    /// <summary>SHA-256 of <see cref="Seed"/>; equals Spec §23.</summary>
    public static Hash256 Hash { get; } = Hash256.ParseHex(Sha256Hex);

    /// <summary>Bootstrap root chain name: <c>mfc{4|6}.{i|f|o}.r.{artifact-id}</c>.</summary>
    public static string RootChainName(IpAddressFamily family, FilterBuiltInContext chain)
    {
        string prefix = family switch
        {
            IpAddressFamily.IPv4 => "mfc4",
            IpAddressFamily.IPv6 => "mfc6",
            _ => throw new DomainInvariantException($"Unsupported address family '{family}'."),
        };

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{prefix}.{AnchorKey.ChainCode(chain)}.r.{ArtifactId}");
    }

    /// <summary>Computes SHA-256 of <see cref="Seed"/>; proves Spec §23 is not a stub constant.</summary>
    public static Hash256 ComputeSeedHash()
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(Seed)));
}
