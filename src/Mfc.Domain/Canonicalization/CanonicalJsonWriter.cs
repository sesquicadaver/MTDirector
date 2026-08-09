using System.Globalization;
using System.Text;

namespace Mfc.Domain.Canonicalization;

/// <summary>
/// Deterministic UTF-8 JSON writer without whitespace (Canonical Spec §5–6, M1-21 AC#6).
/// Object property order is the order keys are written by the caller (schema-fixed) or sorted.
/// </summary>
public sealed class CanonicalJsonWriter
{
    private readonly StringBuilder _builder = new();

    public void WriteRaw(string text) => _builder.Append(text);

    public void WriteNull() => _builder.Append("null");

    public void WriteBoolean(bool value) => _builder.Append(value ? "true" : "false");

    public void WriteNumber(long value)
        => _builder.Append(value.ToString(CultureInfo.InvariantCulture));

    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _builder.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    _builder.Append("\\\"");
                    break;
                case '\\':
                    _builder.Append("\\\\");
                    break;
                case '\b':
                    _builder.Append("\\b");
                    break;
                case '\f':
                    _builder.Append("\\f");
                    break;
                case '\n':
                    _builder.Append("\\n");
                    break;
                case '\r':
                    _builder.Append("\\r");
                    break;
                case '\t':
                    _builder.Append("\\t");
                    break;
                default:
                    if (c <= 0x1F)
                    {
                        _builder.Append("\\u");
                        _builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        _builder.Append(c);
                    }

                    break;
            }
        }

        _builder.Append('"');
    }

    public void WriteObjectStart() => _builder.Append('{');

    public void WriteObjectEnd() => _builder.Append('}');

    public void WriteArrayStart() => _builder.Append('[');

    public void WriteArrayEnd() => _builder.Append(']');

    public void WriteComma() => _builder.Append(',');

    public void WriteColon() => _builder.Append(':');

    public void WritePropertyName(string name)
    {
        WriteString(name);
        WriteColon();
    }

    /// <summary>Writes a JSON object with keys in the given order (deterministic property order).</summary>
    public void WriteObject(IEnumerable<(string Key, Action<CanonicalJsonWriter> WriteValue)> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        WriteObjectStart();
        bool first = true;
        foreach ((string key, Action<CanonicalJsonWriter> writeValue) in properties)
        {
            if (!first)
            {
                WriteComma();
            }

            first = false;
            WritePropertyName(key);
            writeValue(this);
        }

        WriteObjectEnd();
    }

    /// <summary>Writes a JSON object with keys sorted bytewise (ordinal).</summary>
    public void WriteSortedObject(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        WriteObjectStart();
        bool first = true;
        foreach ((string key, string value) in properties.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            if (!first)
            {
                WriteComma();
            }

            first = false;
            WritePropertyName(key);
            WriteString(value);
        }

        WriteObjectEnd();
    }

    public byte[] ToUtf8Bytes() => Encoding.UTF8.GetBytes(_builder.ToString());

    public string ToUtf8String() => _builder.ToString();
}
