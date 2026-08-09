using System.Buffers;
using System.Text;
using Mfc.RouterOs.Protocol;
using Xunit;

namespace Mfc.UnitTests.RouterOs.FaultInjection;

/// <summary>M1-33 protocol framing fault matrix (no production network).</summary>
public sealed class ProtocolFaultInjectionMatrixTests
{
    [Fact]
    public void FragmentedLengthPrefixAndWordResolveWithDefinedCodes()
    {
        byte[] full = EncodeWords(Encoding.ASCII.GetBytes("!done"));
        Assert.True(full.Length > 2);

        // Fragmented length prefix: first byte alone needs more data.
        using ApiSentenceParser prefixParser = new();
        ReadOnlySequence<byte> prefixOnly = new(full.AsMemory(0, 1));
        Assert.Equal(ApiSentenceParseStatus.NeedMoreData, prefixParser.TryRead(ref prefixOnly, out _, out _));

        // Fragmented word body then complete.
        using ApiSentenceParser wordParser = new();
        int mid = Math.Max(2, full.Length / 2);
        ReadOnlySequence<byte> first = new(full.AsMemory(0, mid));
        Assert.Equal(ApiSentenceParseStatus.NeedMoreData, wordParser.TryRead(ref first, out _, out _));
        byte[] remainder = first.ToArray().Concat(full.AsMemory(mid).ToArray()).ToArray();
        ReadOnlySequence<byte> second = new(remainder);
        Assert.Equal(ApiSentenceParseStatus.Sentence, wordParser.TryRead(ref second, out RosSentenceLease? lease, out _));
        using (lease)
        {
            Assert.Equal("!done", Encoding.ASCII.GetString(lease!.Sentence.Head!.Value.Payload.Span));
        }

        // Split-after-every-byte eventually yields a sentence (Spec §51).
        int sentenceHits = 0;
        foreach (byte[] prefix in FaultInjectionTransport.SplitAfterEveryByte(Encoding.ASCII.GetBytes("!re")))
        {
            using ApiSentenceParser local = new();
            ReadOnlySequence<byte> seq = new(prefix);
            if (local.TryRead(ref seq, out RosSentenceLease? hit, out _) == ApiSentenceParseStatus.Sentence)
            {
                sentenceHits++;
                hit!.Dispose();
            }
        }

        Assert.True(sentenceHits >= 1);
    }

    [Fact]
    public void ConnectionCloseMidSentenceYieldsUnexpectedEndOfStream()
    {
        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.WriteWord(buffer, Encoding.ASCII.GetBytes("!done"));
        byte[] partial = buffer.WrittenSpan[..Math.Min(2, buffer.WrittenCount)].ToArray();

        (ApiSentenceParseStatus status, RouterOsProtocolError? error) =
            FaultInjectionTransport.FeedThenComplete(partial);
        Assert.Equal(ApiSentenceParseStatus.Faulted, status);
        Assert.Equal(RouterOsProtocolError.UnexpectedEndOfStream, error!.Code);
    }

    [Fact]
    public void OversizedWordAndSentenceYieldTypedCodes()
    {
        Span<byte> encoded = stackalloc byte[8];
        int written = ApiWordLengthCodec.Encode(length: 2048, encoded);
        ApiLengthDecodeStatus lengthStatus = ApiWordLengthCodec.TryDecode(
            encoded[..written],
            maxWordPayloadBytes: 64,
            out _,
            out _,
            out RouterOsProtocolError? wordError);
        Assert.Equal(ApiLengthDecodeStatus.Faulted, lengthStatus);
        Assert.Equal(RouterOsProtocolError.WordTooLarge, wordError!.Code);

        ApiSentenceLimits limits = new() { MaxSentencePayloadBytes = 16 };
        ArrayBufferWriter<byte> buffer = new();
        byte[] big = Encoding.ASCII.GetBytes("=x=" + new string('z', 64));
        ApiSentenceEncoder.EncodeWords(
            buffer,
            new ReadOnlyMemory<byte>[]
            {
                Encoding.ASCII.GetBytes("!re"),
                big,
            });
        using ApiSentenceParser parser = new(limits);
        ReadOnlySequence<byte> sequence = new(buffer.WrittenMemory);
        Assert.Equal(ApiSentenceParseStatus.Faulted, parser.TryRead(ref sequence, out _, out RouterOsProtocolError? sentenceError));
        Assert.Equal(RouterOsProtocolError.SentenceTooLarge, sentenceError!.Code);
    }

    [Fact]
    public void TrapAndFatalMarkersParseAsReplyWords()
    {
        foreach (string marker in new[] { "!trap", "!fatal" })
        {
            byte[] encoded = EncodeWords(Encoding.ASCII.GetBytes(marker));
            using ApiSentenceParser parser = new();
            ReadOnlySequence<byte> sequence = new(encoded);
            Assert.Equal(ApiSentenceParseStatus.Sentence, parser.TryRead(ref sequence, out RosSentenceLease? lease, out _));
            using (lease)
            {
                Assert.Equal(marker, Encoding.ASCII.GetString(lease!.Sentence.Head!.Value.Payload.Span));
            }
        }
    }

    [Fact]
    public void RepeatedOversizedWordFaultsDoNotGrowManagedMemoryLinearly()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long before = GC.GetTotalMemory(forceFullCollection: true);

        Span<byte> encoded = stackalloc byte[8];
        for (int i = 0; i < 250; i++)
        {
            int written = ApiWordLengthCodec.Encode(length: 1_000_000, encoded);
            ApiLengthDecodeStatus status = ApiWordLengthCodec.TryDecode(
                encoded[..written],
                maxWordPayloadBytes: 128,
                out _,
                out _,
                out RouterOsProtocolError? error);
            Assert.Equal(ApiLengthDecodeStatus.Faulted, status);
            Assert.Equal(RouterOsProtocolError.WordTooLarge, error!.Code);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        long after = GC.GetTotalMemory(forceFullCollection: true);
        // Allow noise; reject multi-megabyte linear accumulation across 250 faults.
        Assert.True(after - before < 2 * 1024 * 1024, $"memory delta {after - before} bytes");
    }

    private static byte[] EncodeWords(params ReadOnlyMemory<byte>[] words)
    {
        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.EncodeWords(buffer, words);
        return buffer.WrittenSpan.ToArray();
    }
}
