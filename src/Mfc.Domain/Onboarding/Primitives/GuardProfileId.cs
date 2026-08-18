namespace Mfc.Domain.Onboarding.Primitives;

/// <summary>
/// Guard profile identity: exactly 16 lowercase hexadecimal characters (Onboarding Spec §15).
/// </summary>
public readonly record struct GuardProfileId
{
    public const int HexLength = 16;

    public string Value { get; }

    private GuardProfileId(string value) => Value = value;

    /// <summary>Parses and validates a 16-char lowercase hex profile id.</summary>
    public static GuardProfileId Parse(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string trimmed = value.Trim();
        if (trimmed.Length != HexLength)
        {
            throw new DomainInvariantException(
                $"GuardProfileId must be exactly {HexLength} lowercase hexadecimal characters.");
        }

        for (int i = 0; i < trimmed.Length; i++)
        {
            char c = trimmed[i];
            if (c is (>= '0' and <= '9') or (>= 'a' and <= 'f'))
            {
                continue;
            }

            throw new DomainInvariantException(
                "GuardProfileId must be lowercase hexadecimal (0-9, a-f).");
        }

        return new GuardProfileId(trimmed);
    }

    /// <summary>Creates a new random 16-hex profile id.</summary>
    public static GuardProfileId New()
    {
        Span<byte> bytes = stackalloc byte[8];
        Random.Shared.NextBytes(bytes);
        return Parse(Convert.ToHexString(bytes).ToLowerInvariant());
    }

    public override string ToString() => Value;

    public bool Equals(GuardProfileId other)
        => string.Equals(Value, other.Value, StringComparison.Ordinal);

    public override int GetHashCode()
        => StringComparer.Ordinal.GetHashCode(Value);
}
