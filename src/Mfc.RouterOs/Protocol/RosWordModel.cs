using System.Text;
using System.Text.Unicode;

namespace Mfc.RouterOs.Protocol;

/// <summary>Classification of a RouterOS API word (Read Adapter Spec §8).</summary>
public enum RosWordKind : byte
{
    Empty = 0,
    Command = 1,
    Reply = 2,
    Attribute = 3,
    ApiAttribute = 4,
    Query = 5,
    Unknown = 6,
}

/// <summary>Byte-preserving word payload; no premature UTF-8 assumption.</summary>
public readonly struct RosWord
{
    public RosWord(RosWordKind kind, ReadOnlyMemory<byte> payload)
    {
        Kind = kind;
        Payload = payload;
    }

    public RosWordKind Kind { get; }

    public ReadOnlyMemory<byte> Payload { get; }

    public static RosWordKind Classify(ReadOnlySpan<byte> payload)
    {
        if (payload.IsEmpty)
        {
            return RosWordKind.Empty;
        }

        return payload[0] switch
        {
            (byte)'/' => RosWordKind.Command,
            (byte)'!' => RosWordKind.Reply,
            (byte)'=' => RosWordKind.Attribute,
            (byte)'.' => RosWordKind.ApiAttribute,
            (byte)'?' => RosWordKind.Query,
            _ => RosWordKind.Unknown,
        };
    }

    /// <summary>
    /// Attempts strict UTF-8 decode without replacement characters.
    /// Invalid sequences return <c>false</c> and leave <paramref name="text"/> null.
    /// </summary>
    public static bool TryDecodeUtf8(ReadOnlySpan<byte> payload, out string? text)
    {
        if (!Utf8.IsValid(payload))
        {
            text = null;
            return false;
        }

        text = Encoding.UTF8.GetString(payload);
        return true;
    }

    /// <summary>Strict ASCII decode for command/reply/attribute names and .tag.</summary>
    public static bool TryDecodeStrictAscii(ReadOnlySpan<byte> payload, out string? text)
    {
        for (int i = 0; i < payload.Length; i++)
        {
            if (payload[i] > 0x7F)
            {
                text = null;
                return false;
            }
        }

        text = Encoding.ASCII.GetString(payload);
        return true;
    }
}

/// <summary>Parsed <c>=name=value</c> or <c>.name=value</c> attribute (order-preserving).</summary>
public readonly struct RosAttributeEntry
{
    public RosAttributeEntry(ReadOnlyMemory<byte> name, ReadOnlyMemory<byte> value, bool isApiAttribute)
    {
        Name = name;
        Value = value;
        IsApiAttribute = isApiAttribute;
    }

    public ReadOnlyMemory<byte> Name { get; }

    public ReadOnlyMemory<byte> Value { get; }

    public bool IsApiAttribute { get; }

    /// <summary>
    /// Splits attribute payload on the second <c>=</c> so values may contain <c>=</c>.
    /// Accepts both <c>=name=value</c> and <c>.name=value</c> forms.
    /// </summary>
    public static bool TryParse(
        ReadOnlyMemory<byte> ownedPayload,
        bool isApiAttribute,
        out RosAttributeEntry attribute,
        out RouterOsProtocolError? error)
    {
        attribute = default;
        error = null;
        ReadOnlySpan<byte> payload = ownedPayload.Span;

        byte expectedPrefix = isApiAttribute ? (byte)'.' : (byte)'=';
        if (payload.Length < 2 || payload[0] != expectedPrefix)
        {
            error = RouterOsProtocolError.MalformedAttribute(
                isApiAttribute
                    ? "API attribute word must start with '.'."
                    : "Attribute word must start with '='.");
            return false;
        }

        int secondEqualsRelative = payload[1..].IndexOf((byte)'=');
        if (secondEqualsRelative < 0)
        {
            error = RouterOsProtocolError.MalformedAttribute("Attribute word is missing the second '='.");
            return false;
        }

        int nameLength = secondEqualsRelative;
        if (nameLength == 0)
        {
            error = RouterOsProtocolError.MalformedAttribute("Attribute name must not be empty.");
            return false;
        }

        int valueStart = 1 + secondEqualsRelative + 1;
        attribute = new RosAttributeEntry(
            ownedPayload.Slice(1, nameLength),
            ownedPayload[valueStart..],
            isApiAttribute);
        return true;
    }
}
