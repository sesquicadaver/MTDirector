using System.Text.RegularExpressions;

namespace Mfc.Domain.Inventory.Primitives;

/// <summary>
/// Immutable site code matching <c>^[A-Z][A-Z0-9_-]{1,31}$</c>.
/// </summary>
public sealed partial class SiteCode : IEquatable<SiteCode>
{
    private static readonly Regex Pattern = SiteCodeRegex();

    public string Value { get; }

    private SiteCode(string value) => Value = value;

    public static SiteCode Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string trimmed = value.Trim();
        if (!Pattern.IsMatch(trimmed))
        {
            throw new DomainInvariantException(
                "Site.code must match ^[A-Z][A-Z0-9_-]{1,31}$.");
        }

        return new SiteCode(trimmed);
    }

    public bool Equals(SiteCode? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is SiteCode other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(SiteCode? left, SiteCode? right)
        => Equals(left, right);

    public static bool operator !=(SiteCode? left, SiteCode? right)
        => !Equals(left, right);

    [GeneratedRegex("^[A-Z][A-Z0-9_-]{1,31}$", RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    private static partial Regex SiteCodeRegex();
}
