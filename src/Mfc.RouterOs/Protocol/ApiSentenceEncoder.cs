using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;

namespace Mfc.RouterOs.Protocol;

/// <summary>
/// Encodes RouterOS API sentences into an <see cref="IBufferWriter{T}"/> (PipeWriter-compatible).
/// Does not perform blocking I/O.
/// </summary>
public static class ApiSentenceEncoder
{
    /// <summary>
    /// Writes <paramref name="command"/>, optional API/attributes/queries, and a terminating empty word.
    /// </summary>
    public static void Encode(
        IBufferWriter<byte> writer,
        byte[] command,
        ReadOnlySpan<RosAttributeEntry> attributes = default,
        ReadOnlySpan<RosAttributeEntry> apiAttributes = default,
        ReadOnlySpan<ReadOnlyMemory<byte>> queries = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(command);
        Encode(writer, (ReadOnlySpan<byte>)command, attributes, apiAttributes, queries);
    }

    /// <summary>
    /// Writes <paramref name="command"/>, optional API/attributes/queries, and a terminating empty word.
    /// </summary>
    public static void Encode(
        IBufferWriter<byte> writer,
        ReadOnlySpan<byte> command,
        ReadOnlySpan<RosAttributeEntry> attributes = default,
        ReadOnlySpan<RosAttributeEntry> apiAttributes = default,
        ReadOnlySpan<ReadOnlyMemory<byte>> queries = default)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (command.Length == 0)
        {
            throw new ArgumentException("Command word must not be empty.", nameof(command));
        }

        if (command[0] != (byte)'/')
        {
            throw new ArgumentException("Command word must start with '/'.", nameof(command));
        }

        WriteWord(writer, command);
        foreach (RosAttributeEntry api in apiAttributes)
        {
            WriteAttributeWord(writer, api, apiPrefix: true);
        }

        foreach (RosAttributeEntry attribute in attributes)
        {
            WriteAttributeWord(writer, attribute, apiPrefix: false);
        }

        foreach (ReadOnlyMemory<byte> query in queries)
        {
            if (query.IsEmpty || query.Span[0] != (byte)'?')
            {
                throw new ArgumentException("Query word must be non-empty and start with '?'.", nameof(queries));
            }

            WriteWord(writer, query.Span);
        }

        WriteWord(writer, ReadOnlySpan<byte>.Empty);
    }

    /// <summary>Encodes a raw list of word payloads followed by the empty terminator.</summary>
    public static void EncodeWords(IBufferWriter<byte> writer, ReadOnlySpan<ReadOnlyMemory<byte>> words)
    {
        ArgumentNullException.ThrowIfNull(writer);
        foreach (ReadOnlyMemory<byte> word in words)
        {
            WriteWord(writer, word.Span);
        }

        WriteWord(writer, ReadOnlySpan<byte>.Empty);
    }

    public static void WriteWord(IBufferWriter<byte> writer, ReadOnlySpan<byte> payload)
    {
        ArgumentNullException.ThrowIfNull(writer);
        int prefixLength = ApiWordLengthCodec.GetEncodedPrefixLength((uint)payload.Length);
        Span<byte> prefix = writer.GetSpan(prefixLength);
        int written = ApiWordLengthCodec.Encode((uint)payload.Length, prefix);
        writer.Advance(written);

        if (payload.IsEmpty)
        {
            return;
        }

        Span<byte> body = writer.GetSpan(payload.Length);
        payload.CopyTo(body);
        writer.Advance(payload.Length);
    }

    private static void WriteAttributeWord(IBufferWriter<byte> writer, RosAttributeEntry attribute, bool apiPrefix)
    {
        ReadOnlySpan<byte> name = attribute.Name.Span;
        ReadOnlySpan<byte> value = attribute.Value.Span;
        if (name.IsEmpty)
        {
            throw new ArgumentException("Attribute name must not be empty.");
        }

        int total = 1 + name.Length + 1 + value.Length;
        int prefixLength = ApiWordLengthCodec.GetEncodedPrefixLength((uint)total);
        Span<byte> dest = writer.GetSpan(prefixLength + total);
        int written = ApiWordLengthCodec.Encode((uint)total, dest);
        int offset = written;
        dest[offset++] = apiPrefix ? (byte)'.' : (byte)'=';
        name.CopyTo(dest[offset..]);
        offset += name.Length;
        dest[offset++] = (byte)'=';
        value.CopyTo(dest[offset..]);
        writer.Advance(written + total);
    }

    public static byte[] EncodeToArray(
        string command,
        IEnumerable<(string Name, string Value)>? attributes = null,
        IEnumerable<(string Name, string Value)>? apiAttributes = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArrayBufferWriter<byte> buffer = new();
        List<RosAttributeEntry> attrs = [];
        if (attributes is not null)
        {
            foreach ((string name, string value) in attributes)
            {
                attrs.Add(new RosAttributeEntry(
                    Encoding.ASCII.GetBytes(name),
                    Encoding.UTF8.GetBytes(value),
                    isApiAttribute: false));
            }
        }

        List<RosAttributeEntry> apis = [];
        if (apiAttributes is not null)
        {
            foreach ((string name, string value) in apiAttributes)
            {
                apis.Add(new RosAttributeEntry(
                    Encoding.ASCII.GetBytes(name),
                    Encoding.ASCII.GetBytes(value),
                    isApiAttribute: true));
            }
        }

        Encode(
            buffer,
            Encoding.ASCII.GetBytes(command),
            CollectionsMarshal.AsSpan(attrs),
            CollectionsMarshal.AsSpan(apis));
        return buffer.WrittenSpan.ToArray();
    }
}
