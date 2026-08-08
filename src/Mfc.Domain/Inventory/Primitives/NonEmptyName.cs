namespace Mfc.Domain.Inventory.Primitives;

/// <summary>
/// Non-empty trimmed display name with length bounds (1–128).
/// </summary>
public sealed class NonEmptyName : IEquatable<NonEmptyName>
{
    public const int MaxLength = 128;

    public string Value { get; }

    private NonEmptyName(string value) => Value = value;

    public static NonEmptyName Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string trimmed = value.Trim();
        if (trimmed.Length is < 1 or > MaxLength)
        {
            throw new DomainInvariantException(
                $"Name length must be between 1 and {MaxLength} characters.");
        }

        return new NonEmptyName(trimmed);
    }

    public bool Equals(NonEmptyName? other) => other is not null && Value == other.Value;

    public override bool Equals(object? obj) => obj is NonEmptyName other && Equals(other);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    public override string ToString() => Value;
}
