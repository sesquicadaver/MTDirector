using System.Buffers;
using System.Text;

namespace Mfc.RouterOs.Protocol;

/// <summary>
/// Disposable lease over pooled sentence memory. Must not leave the RouterOS assembly
/// without copying (Read Adapter Spec §9.4).
/// </summary>
public sealed class RosSentenceLease : IDisposable
{
    private readonly IMemoryOwner<byte> _owner;
    private bool _disposed;

    internal RosSentenceLease(IMemoryOwner<byte> owner, RosSentence sentence)
    {
        _owner = owner;
        Sentence = sentence;
    }

    public RosSentence Sentence { get; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        // Clear pooled buffer before return (login-related data hygiene).
        _owner.Memory.Span.Clear();
        _owner.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Parsed RouterOS sentence: reply/command marker plus order-preserving words.</summary>
public sealed class RosSentence
{
    private readonly IReadOnlyList<RosWord> _words;
    private readonly IReadOnlyList<RosAttributeEntry> _attributes;
    private readonly IReadOnlyList<RosAttributeEntry> _apiAttributes;
    private readonly IReadOnlyList<RosWord> _queries;

    internal RosSentence(
        RosWord? head,
        IReadOnlyList<RosWord> words,
        IReadOnlyList<RosAttributeEntry> attributes,
        IReadOnlyList<RosAttributeEntry> apiAttributes,
        IReadOnlyList<RosWord> queries,
        int payloadBytes)
    {
        Head = head;
        _words = words;
        _attributes = attributes;
        _apiAttributes = apiAttributes;
        _queries = queries;
        PayloadBytes = payloadBytes;
    }

    /// <summary>First non-empty word (reply marker or command), if present.</summary>
    public RosWord? Head { get; }

    public IReadOnlyList<RosWord> Words => _words;

    /// <summary>Order-preserving <c>=name=value</c> attributes (not a dictionary).</summary>
    public IReadOnlyList<RosAttributeEntry> Attributes => _attributes;

    public IReadOnlyList<RosAttributeEntry> ApiAttributes => _apiAttributes;

    /// <summary>Query words in significant order.</summary>
    public IReadOnlyList<RosWord> Queries => _queries;

    public int PayloadBytes { get; }

    public bool IsEmptySentence => Head is null && _words.Count == 0;

    /// <summary>
    /// Duplicate policy (Spec §9.3): scalar lookup fails with <see cref="RouterOsProtocolError.DuplicateAttribute"/>
    /// when the same attribute name appears more than once. Sequence itself always retains all entries.
    /// </summary>
    public bool TryGetUniqueAttribute(
        ReadOnlySpan<byte> name,
        out RosAttributeEntry attribute,
        out RouterOsProtocolError? error)
    {
        attribute = default;
        error = null;
        RosAttributeEntry? found = null;
        foreach (RosAttributeEntry candidate in _attributes)
        {
            if (!candidate.Name.Span.SequenceEqual(name))
            {
                continue;
            }

            if (found is not null)
            {
                error = RouterOsProtocolError.Duplicate(
                    $"Duplicate scalar attribute '{Encoding.ASCII.GetString(name)}'.");
                attribute = default;
                return false;
            }

            found = candidate;
        }

        if (found is null)
        {
            return false;
        }

        attribute = found.Value;
        return true;
    }

    /// <summary>Duplicate <c>.tag</c> is a protocol fault (Spec §9.3).</summary>
    public bool TryGetUniqueTag(out ReadOnlyMemory<byte> tag, out RouterOsProtocolError? error)
    {
        tag = default;
        error = null;
        ReadOnlyMemory<byte>? found = null;
        ReadOnlySpan<byte> tagName = "tag"u8;
        foreach (RosAttributeEntry api in _apiAttributes)
        {
            if (!api.Name.Span.SequenceEqual(tagName))
            {
                continue;
            }

            if (found is not null)
            {
                error = RouterOsProtocolError.Duplicate("Duplicate .tag attribute.");
                return false;
            }

            found = api.Value;
        }

        if (found is null)
        {
            return false;
        }

        tag = found.Value;
        return true;
    }
}
