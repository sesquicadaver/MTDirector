using System.Text;

namespace Mfc.Domain.Policy;

/// <summary>Per-rule logging specification (Policy Model §27).</summary>
public sealed class LogSpecification
{
    public const int MaxPrefixLength = 32;

    public bool Enabled { get; }

    /// <summary>Optional ASCII prefix (≤32, no control characters). Null when disabled or unset.</summary>
    public string? Prefix { get; }

    private LogSpecification(bool enabled, string? prefix)
    {
        Enabled = enabled;
        Prefix = prefix;
    }

    public static LogSpecification Disabled { get; } = new(enabled: false, prefix: null);

    public static LogSpecification Create(bool enabled, string? prefix = null)
    {
        if (!enabled)
        {
            if (!string.IsNullOrEmpty(prefix))
            {
                throw new DomainInvariantException("Log prefix is forbidden when logging is disabled.");
            }

            return Disabled;
        }

        if (prefix is null)
        {
            return new LogSpecification(enabled: true, prefix: null);
        }

        return new LogSpecification(enabled: true, ValidatePrefix(prefix));
    }

    private static string ValidatePrefix(string prefix)
    {
        if (prefix.Length > MaxPrefixLength)
        {
            throw new DomainInvariantException(
                $"Log prefix must be at most {MaxPrefixLength} ASCII characters.");
        }

        for (int i = 0; i < prefix.Length; i++)
        {
            char c = prefix[i];
            if (c > 0x7F)
            {
                throw new DomainInvariantException("Log prefix must be ASCII.");
            }

            if (char.IsControl(c))
            {
                throw new DomainInvariantException("Log prefix must not contain control characters.");
            }
        }

        // Ensure round-trip as ASCII bytes (defensive).
        _ = Encoding.ASCII.GetBytes(prefix);
        return prefix;
    }
}
