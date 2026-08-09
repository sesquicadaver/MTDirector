using System.Buffers;
using System.Text;
using Mfc.RouterOs.Protocol;

namespace Mfc.UnitTests.RouterOs.FaultInjection;

/// <summary>
/// Lab-only framing helpers for M1-33 / Read Adapter Spec §51 fault injection
/// (split-after-byte, oversize length, mid-stream close). No product write path.
/// </summary>
public static class FaultInjectionTransport
{
    /// <summary>Encodes words then yields every non-empty prefix (split after every byte).</summary>
    public static IEnumerable<byte[]> SplitAfterEveryByte(params ReadOnlyMemory<byte>[] words)
    {
        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.EncodeWords(buffer, words);
        byte[] full = buffer.WrittenSpan.ToArray();
        for (int i = 1; i <= full.Length; i++)
        {
            yield return full.AsSpan(0, i).ToArray();
        }
    }

    public static byte[] EncodeReply(string marker, ulong tag, params (string Name, string Value)[] attributes)
    {
        ArrayBufferWriter<byte> buffer = new();
        List<ReadOnlyMemory<byte>> words = [Encoding.ASCII.GetBytes(marker)];
        foreach ((string name, string value) in attributes)
        {
            words.Add(Encoding.UTF8.GetBytes($"={name}={value}"));
        }

        words.Add(Encoding.ASCII.GetBytes($".tag={tag}"));
        ApiSentenceEncoder.EncodeWords(buffer, words.ToArray());
        return buffer.WrittenSpan.ToArray();
    }

    public static byte[] EncodeOversizeLengthPrefix(uint claimedLength)
    {
        Span<byte> prefix = stackalloc byte[8];
        int written = ApiWordLengthCodec.Encode(claimedLength, prefix);
        return prefix[..written].ToArray();
    }

    public static (ApiSentenceParseStatus Status, RouterOsProtocolError? Error) FeedThenComplete(
        ReadOnlySpan<byte> partialFrame)
    {
        using ApiSentenceParser parser = new();
        ReadOnlySequence<byte> sequence = new(partialFrame.ToArray());
        ApiSentenceParseStatus status = parser.TryRead(ref sequence, out _, out RouterOsProtocolError? error);
        if (status == ApiSentenceParseStatus.Faulted)
        {
            return (status, error);
        }

        status = parser.Complete(out _, out error);
        return (status, error);
    }
}
