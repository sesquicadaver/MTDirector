using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Inventory;

/// <summary>TLS trust policy for RouterOS API-SSL (Vertical Slice §6.4). Trust-all is forbidden.</summary>
public enum CertificateTrustMode : byte
{
    InternalCa = 0,
    SpkiPin = 1,
}

/// <summary>Opaque reference to a row in encrypted_secrets.</summary>
public readonly record struct SecretReference(Guid Value)
{
    public static SecretReference From(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new DomainInvariantException("Secret reference cannot be empty.");
        }

        return new SecretReference(id);
    }

    public override string ToString() => Value.ToString("D");
}

/// <summary>
/// Secure connection profile value object. Never holds a RouterOS password or arbitrary commands.
/// </summary>
public sealed class DeviceConnectionProfile : IEquatable<DeviceConnectionProfile>
{
    public const int MinConnectTimeoutMs = 1000;
    public const int MaxConnectTimeoutMs = 30000;
    public const int MinCommandTimeoutMs = 1000;
    public const int MaxCommandTimeoutMs = 120000;
    public const long MinMaxResponseBytes = 1_048_576;
    public const long MaxMaxResponseBytes = 268_435_456;
    public const ushort DefaultApiSslPort = ManagementEndpoint.DefaultApiSslPort;

    public DeviceId DeviceId { get; }

    public NonEmptyName Username { get; }

    public SecretReference SecretReference { get; private set; }

    public CertificateTrustMode TrustMode { get; private set; }

    public string? CaProfileRef { get; private set; }

    public Hash256? PinnedSpkiSha256 { get; private set; }

    public int ConnectTimeoutMs { get; }

    public int CommandTimeoutMs { get; }

    public long MaxResponseBytes { get; }

    public ulong RowVersion { get; private set; }

    private DeviceConnectionProfile(
        DeviceId deviceId,
        NonEmptyName username,
        SecretReference secretReference,
        CertificateTrustMode trustMode,
        string? caProfileRef,
        Hash256? pinnedSpkiSha256,
        int connectTimeoutMs,
        int commandTimeoutMs,
        long maxResponseBytes,
        ulong rowVersion)
    {
        DeviceId = deviceId;
        Username = username;
        SecretReference = secretReference;
        TrustMode = trustMode;
        CaProfileRef = caProfileRef;
        PinnedSpkiSha256 = pinnedSpkiSha256;
        ConnectTimeoutMs = connectTimeoutMs;
        CommandTimeoutMs = commandTimeoutMs;
        MaxResponseBytes = maxResponseBytes;
        RowVersion = rowVersion;
    }

    public static DeviceConnectionProfile Create(
        DeviceId deviceId,
        NonEmptyName username,
        SecretReference secretReference,
        CertificateTrustMode trustMode,
        string? caProfileRef,
        Hash256? pinnedSpkiSha256,
        int connectTimeoutMs = 5000,
        int commandTimeoutMs = 30000,
        long maxResponseBytes = 16_777_216)
    {
        ArgumentNullException.ThrowIfNull(username);
        ValidateTimeouts(connectTimeoutMs, commandTimeoutMs, maxResponseBytes);
        ValidateTrust(trustMode, caProfileRef, pinnedSpkiSha256);
        if (username.Value.Length > 64)
        {
            throw new DomainInvariantException("Connection username length must be between 1 and 64.");
        }

        return new DeviceConnectionProfile(
            deviceId,
            username,
            secretReference,
            trustMode,
            NormalizeCaRef(caProfileRef),
            pinnedSpkiSha256,
            connectTimeoutMs,
            commandTimeoutMs,
            maxResponseBytes,
            rowVersion: 1);
    }

    /// <summary>Rebuilds a profile from persistence without resetting row version.</summary>
    public static DeviceConnectionProfile Reconstitute(
        DeviceId deviceId,
        NonEmptyName username,
        SecretReference secretReference,
        CertificateTrustMode trustMode,
        string? caProfileRef,
        Hash256? pinnedSpkiSha256,
        int connectTimeoutMs,
        int commandTimeoutMs,
        long maxResponseBytes,
        ulong rowVersion)
    {
        ArgumentNullException.ThrowIfNull(username);
        if (rowVersion == 0)
        {
            throw new DomainInvariantException("row_version must be greater than zero.");
        }

        ValidateTimeouts(connectTimeoutMs, commandTimeoutMs, maxResponseBytes);
        ValidateTrust(trustMode, caProfileRef, pinnedSpkiSha256);
        if (username.Value.Length > 64)
        {
            throw new DomainInvariantException("Connection username length must be between 1 and 64.");
        }

        return new DeviceConnectionProfile(
            deviceId,
            username,
            secretReference,
            trustMode,
            NormalizeCaRef(caProfileRef),
            pinnedSpkiSha256,
            connectTimeoutMs,
            commandTimeoutMs,
            maxResponseBytes,
            rowVersion);
    }

    /// <summary>Replaces the secret reference without changing device identity (rotation).</summary>
    public void RotateSecret(SecretReference newSecretReference)
    {
        SecretReference = newSecretReference;
        RowVersion++;
    }

    /// <summary>Updates SPKI pin; caller must emit an audit event.</summary>
    public void ChangeSpkiPin(Hash256 newPin)
    {
        ArgumentNullException.ThrowIfNull(newPin);
        if (TrustMode != CertificateTrustMode.SpkiPin)
        {
            throw new DomainInvariantException("SPKI pin can only be changed when TrustMode is SpkiPin.");
        }

        PinnedSpkiSha256 = newPin;
        RowVersion++;
    }

    public void ChangeInternalCaProfile(string caProfileRef)
    {
        if (TrustMode != CertificateTrustMode.InternalCa)
        {
            throw new DomainInvariantException("CA profile can only be changed when TrustMode is InternalCa.");
        }

        ValidateTrust(CertificateTrustMode.InternalCa, caProfileRef, pinnedSpkiSha256: null);
        CaProfileRef = NormalizeCaRef(caProfileRef);
        RowVersion++;
    }

    public bool Equals(DeviceConnectionProfile? other)
    {
        if (other is null)
        {
            return false;
        }

        return DeviceId == other.DeviceId
               && Username.Equals(other.Username)
               && SecretReference == other.SecretReference
               && TrustMode == other.TrustMode
               && string.Equals(CaProfileRef, other.CaProfileRef, StringComparison.Ordinal)
               && Equals(PinnedSpkiSha256, other.PinnedSpkiSha256)
               && ConnectTimeoutMs == other.ConnectTimeoutMs
               && CommandTimeoutMs == other.CommandTimeoutMs
               && MaxResponseBytes == other.MaxResponseBytes
               && RowVersion == other.RowVersion;
    }

    public override bool Equals(object? obj) => obj is DeviceConnectionProfile other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(DeviceId, Username, SecretReference, TrustMode, CaProfileRef, PinnedSpkiSha256, RowVersion);

    private static void ValidateTimeouts(int connectTimeoutMs, int commandTimeoutMs, long maxResponseBytes)
    {
        if (connectTimeoutMs is < MinConnectTimeoutMs or > MaxConnectTimeoutMs)
        {
            throw new DomainInvariantException(
                $"connect_timeout_ms must be between {MinConnectTimeoutMs} and {MaxConnectTimeoutMs}.");
        }

        if (commandTimeoutMs is < MinCommandTimeoutMs or > MaxCommandTimeoutMs)
        {
            throw new DomainInvariantException(
                $"command_timeout_ms must be between {MinCommandTimeoutMs} and {MaxCommandTimeoutMs}.");
        }

        if (maxResponseBytes is < MinMaxResponseBytes or > MaxMaxResponseBytes)
        {
            throw new DomainInvariantException(
                $"max_response_bytes must be between {MinMaxResponseBytes} and {MaxMaxResponseBytes}.");
        }
    }

    private static void ValidateTrust(
        CertificateTrustMode trustMode,
        string? caProfileRef,
        Hash256? pinnedSpkiSha256)
    {
        switch (trustMode)
        {
            case CertificateTrustMode.InternalCa:
                if (string.IsNullOrWhiteSpace(caProfileRef))
                {
                    throw new DomainInvariantException("INTERNAL_CA trust requires ca_profile_ref.");
                }

                if (pinnedSpkiSha256 is not null)
                {
                    throw new DomainInvariantException("INTERNAL_CA trust must not set pinned_spki_sha256.");
                }

                break;
            case CertificateTrustMode.SpkiPin:
                if (pinnedSpkiSha256 is null)
                {
                    throw new DomainInvariantException("SPKI_PIN trust requires pinned_spki_sha256.");
                }

                if (!string.IsNullOrWhiteSpace(caProfileRef))
                {
                    throw new DomainInvariantException("SPKI_PIN trust must not set ca_profile_ref.");
                }

                break;
            default:
                throw new DomainInvariantException("Unknown certificate trust mode (trust-all is forbidden).");
        }
    }

    private static string? NormalizeCaRef(string? caProfileRef)
        => caProfileRef is null ? null : caProfileRef.Trim();
}
