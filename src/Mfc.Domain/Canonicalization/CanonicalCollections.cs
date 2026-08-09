using System.Text;

namespace Mfc.Domain.Canonicalization;

/// <summary>Set / list helpers for canonical encoding (M1-21 AC#2–3).</summary>
public static class CanonicalCollections
{
    /// <summary>
    /// Returns a sorted unique set ordered by canonical encoded UTF-8 bytes of elements.
    /// </summary>
    public static IReadOnlyList<string> CanonicalizeSet(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        SortedSet<string> set = new(StringComparer.Ordinal);
        foreach (string value in values)
        {
            ArgumentNullException.ThrowIfNull(value);
            set.Add(value);
        }

        return set.ToArray();
    }

    /// <summary>Preserves order of an ordered collection (firewall tables, etc.).</summary>
    public static IReadOnlyList<T> PreserveOrder<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.ToArray();
    }

    /// <summary>Stable ordinal key used when sorting unordered records.</summary>
    public static string StableSortKey(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        CanonicalJsonWriter writer = new();
        writer.WriteSortedObject(properties);
        return writer.ToUtf8String();
    }

    /// <summary>Compares two strings by their UTF-8 encoded bytes (ordinal).</summary>
    public static int CompareEncodedBytes(string left, string right)
    {
        byte[] a = Encoding.UTF8.GetBytes(left);
        byte[] b = Encoding.UTF8.GetBytes(right);
        return a.AsSpan().SequenceCompareTo(b);
    }
}
