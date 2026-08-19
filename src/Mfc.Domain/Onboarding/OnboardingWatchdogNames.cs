using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding.Primitives;

namespace Mfc.Domain.Onboarding;

/// <summary>
/// Deterministic onboarding resource names (Onboarding Spec §12.1 / §33).
/// Token is the first 16 hex chars of SHA-256(operation_id || device_id).
/// </summary>
public static class OnboardingWatchdogNames
{
    public const int TokenHexLength = 16;

    public static string Token(OnboardingOperationId operationId, DeviceId deviceId)
    {
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hasher.AppendData(Encoding.UTF8.GetBytes(operationId.Value.ToString("D") + deviceId.Value.ToString("D")));
        string hex = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        return hex[..TokenHexLength];
    }

    public static string RollbackScript(string token) => $"mfc-ob-s-{RequireToken(token)}";

    public static string DeadlineScheduler(string token) => $"mfc-ob-d-{RequireToken(token)}";

    public static string StartupScheduler(string token) => $"mfc-ob-b-{RequireToken(token)}";

    public static string CapabilityScript(string token) => $"mfc-cap-s-{RequireToken(token)}";

    public static string CapabilityScheduler(string token) => $"mfc-cap-d-{RequireToken(token)}";

    public static bool IsOnboardingWatchdogName(string? name)
        => name is not null
           && (name.StartsWith("mfc-ob-s-", StringComparison.Ordinal)
               || name.StartsWith("mfc-ob-d-", StringComparison.Ordinal)
               || name.StartsWith("mfc-ob-b-", StringComparison.Ordinal));

    public static bool IsCapabilityProofName(string? name)
        => name is not null
           && (name.StartsWith("mfc-cap-s-", StringComparison.Ordinal)
               || name.StartsWith("mfc-cap-d-", StringComparison.Ordinal));

    private static string RequireToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (token.Length != TokenHexLength || token.Any(static c => c is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new DomainInvariantException(
                $"Onboarding token must be {TokenHexLength} lowercase hex characters.");
        }

        return token;
    }
}
