using System.Buffers;
using System.Text;
using Mfc.RouterOs.Protocol;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class ApiSentenceCodecTests
{
    [Theory]
    [InlineData("!re")]
    [InlineData("!done")]
    [InlineData("!empty")]
    [InlineData("!trap")]
    [InlineData("!fatal")]
    public void ParsesReplyMarkers(string marker)
    {
        byte[] encoded = EncodeRawWords(Encoding.ASCII.GetBytes(marker));
        using RosSentenceLease lease = ParseOne(encoded);
        Assert.Equal(RosWordKind.Reply, lease.Sentence.Head!.Value.Kind);
        Assert.True(RosWord.TryDecodeStrictAscii(lease.Sentence.Head.Value.Payload.Span, out string? text));
        Assert.Equal(marker, text);
    }

    [Fact]
    public void ParsesMultipleSentencesInOneBuffer()
    {
        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.EncodeWords(buffer, new ReadOnlyMemory<byte>[] { Encoding.ASCII.GetBytes("!re") });
        ApiSentenceEncoder.EncodeWords(buffer, new ReadOnlyMemory<byte>[] { Encoding.ASCII.GetBytes("!done") });

        using ApiSentenceParser parser = new();
        ReadOnlySequence<byte> sequence = new(buffer.WrittenMemory);
        Assert.Equal(ApiSentenceParseStatus.Sentence, parser.TryRead(ref sequence, out RosSentenceLease? first, out _));
        using (first)
        {
            Assert.Equal("!re", Encoding.ASCII.GetString(first!.Sentence.Head!.Value.Payload.Span));
        }

        Assert.Equal(ApiSentenceParseStatus.Sentence, parser.TryRead(ref sequence, out RosSentenceLease? second, out _));
        using (second)
        {
            Assert.Equal("!done", Encoding.ASCII.GetString(second!.Sentence.Head!.Value.Payload.Span));
        }

        Assert.True(sequence.IsEmpty);
    }

    [Fact]
    public void EmptyWordTerminatesSentenceAndPreservesAttributeOrderWithEqualsInValue()
    {
        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.EncodeWords(
            buffer,
            new ReadOnlyMemory<byte>[]
            {
                Encoding.ASCII.GetBytes("!re"),
                Encoding.ASCII.GetBytes("=a=1"),
                Encoding.ASCII.GetBytes("=b=x=y=z"),
                Encoding.ASCII.GetBytes("=a=2"),
            });

        using RosSentenceLease lease = ParseOne(buffer.WrittenMemory.ToArray());
        Assert.Equal(3, lease.Sentence.Attributes.Count);
        Assert.Equal("a", Encoding.ASCII.GetString(lease.Sentence.Attributes[0].Name.Span));
        Assert.Equal("1", Encoding.UTF8.GetString(lease.Sentence.Attributes[0].Value.Span));
        Assert.Equal("x=y=z", Encoding.UTF8.GetString(lease.Sentence.Attributes[1].Value.Span));

        Assert.False(lease.Sentence.TryGetUniqueAttribute("a"u8, out _, out RouterOsProtocolError? dup));
        Assert.Equal(RouterOsProtocolError.DuplicateAttribute, dup!.Code);
    }

    [Fact]
    public void DuplicateTagIsProtocolFaultOnLookup()
    {
        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.EncodeWords(
            buffer,
            new ReadOnlyMemory<byte>[]
            {
                Encoding.ASCII.GetBytes("!done"),
                Encoding.ASCII.GetBytes(".tag=1"),
                Encoding.ASCII.GetBytes(".tag=2"),
            });

        using RosSentenceLease lease = ParseOne(buffer.WrittenMemory.ToArray());
        Assert.False(lease.Sentence.TryGetUniqueTag(out _, out RouterOsProtocolError? error));
        Assert.Equal(RouterOsProtocolError.DuplicateAttribute, error!.Code);
    }

    [Fact]
    public void MalformedAttributeFaultsParser()
    {
        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.EncodeWords(
            buffer,
            new ReadOnlyMemory<byte>[]
            {
                Encoding.ASCII.GetBytes("!re"),
                Encoding.ASCII.GetBytes("=name"),
            });

        using ApiSentenceParser parser = new();
        ReadOnlySequence<byte> sequence = new(buffer.WrittenMemory);
        ApiSentenceParseStatus status = parser.TryRead(ref sequence, out _, out RouterOsProtocolError? error);
        Assert.Equal(ApiSentenceParseStatus.Faulted, status);
        Assert.Equal(RouterOsProtocolError.AttributeMalformed, error!.Code);
        Assert.True(parser.IsFaulted);
    }

    [Fact]
    public void InvalidUtf8ValueIsPreservedWithoutReplacement()
    {
        byte[] value = [0x41, 0xFF, 0x41]; // A, invalid, A
        byte[] attr = new byte[3 + value.Length]; // = v = + value
        attr[0] = (byte)'=';
        attr[1] = (byte)'v';
        attr[2] = (byte)'=';
        value.AsSpan().CopyTo(attr.AsSpan(3));

        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.EncodeWords(
            buffer,
            new ReadOnlyMemory<byte>[]
            {
                Encoding.ASCII.GetBytes("!re"),
                attr,
            });

        using RosSentenceLease lease = ParseOne(buffer.WrittenMemory.ToArray());
        ReadOnlySpan<byte> raw = lease.Sentence.Attributes[0].Value.Span;
        Assert.True(raw.SequenceEqual(value));
        Assert.False(RosWord.TryDecodeUtf8(raw, out string? text));
        Assert.Null(text);
        Assert.DoesNotContain('\uFFFD', text ?? string.Empty);
    }

    [Fact]
    public void SupportsFragmentedTcpFrames()
    {
        byte[] full = EncodeRawWords(Encoding.ASCII.GetBytes("!done"));
        using ApiSentenceParser parser = new();
        for (int split = 1; split < full.Length; split++)
        {
            using ApiSentenceParser local = new();
            ReadOnlySequence<byte> first = new(full.AsMemory(0, split));
            Assert.Equal(ApiSentenceParseStatus.NeedMoreData, local.TryRead(ref first, out _, out _));
            // Remaining unread from first + second chunk.
            byte[] remainder = first.ToArray();
            byte[] combined = remainder.Concat(full.AsMemory(split).ToArray()).ToArray();
            ReadOnlySequence<byte> second = new(combined);
            Assert.Equal(ApiSentenceParseStatus.Sentence, local.TryRead(ref second, out RosSentenceLease? lease, out _));
            using (lease)
            {
                Assert.Equal("!done", Encoding.ASCII.GetString(lease!.Sentence.Head!.Value.Payload.Span));
            }
        }

        // Keep analyzer happy about unused parser in outer scope pattern.
        Assert.False(parser.IsFaulted);
    }

    [Fact]
    public void OversizedSentenceFaultsBeforeFullBuffering()
    {
        ApiSentenceLimits limits = new() { MaxSentencePayloadBytes = 16 };
        ArrayBufferWriter<byte> buffer = new();
        byte[] big = Encoding.ASCII.GetBytes("=x=" + new string('a', 64));
        ApiSentenceEncoder.EncodeWords(
            buffer,
            new ReadOnlyMemory<byte>[]
            {
                Encoding.ASCII.GetBytes("!re"),
                big,
            });

        using ApiSentenceParser parser = new(limits);
        ReadOnlySequence<byte> sequence = new(buffer.WrittenMemory);
        ApiSentenceParseStatus status = parser.TryRead(ref sequence, out _, out RouterOsProtocolError? error);
        Assert.Equal(ApiSentenceParseStatus.Faulted, status);
        Assert.Equal(RouterOsProtocolError.SentenceTooLarge, error!.Code);
    }

    [Fact]
    public void ConnectionCloseMidWordFaults()
    {
        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.WriteWord(buffer, Encoding.ASCII.GetBytes("!done"));
        // Prefix+partial body missing terminator and incomplete on purpose: take only first 2 bytes of frame.
        byte[] partial = buffer.WrittenSpan[..Math.Min(2, buffer.WrittenCount)].ToArray();

        using ApiSentenceParser parser = new();
        ReadOnlySequence<byte> sequence = new(partial);
        Assert.Equal(ApiSentenceParseStatus.NeedMoreData, parser.TryRead(ref sequence, out _, out _));
        Assert.Equal(ApiSentenceParseStatus.Faulted, parser.Complete(out _, out RouterOsProtocolError? error));
        Assert.Equal(RouterOsProtocolError.UnexpectedEndOfStream, error!.Code);
    }

    [Fact]
    public void EncoderRejectsNullCommandWord()
    {
        ArrayBufferWriter<byte> writer = new();
        Assert.Throws<ArgumentNullException>(() => ApiSentenceEncoder.Encode(writer, command: null!));
    }

    [Fact]
    public void EncoderRoundTripCommandWithTag()
    {
        byte[] encoded = ApiSentenceEncoder.EncodeToArray(
            "/system/resource/print",
            attributes: [("proplist", "uptime")],
            apiAttributes: [("tag", "7")]);

        using RosSentenceLease lease = ParseOne(encoded);
        Assert.Equal(RosWordKind.Command, lease.Sentence.Head!.Value.Kind);
        Assert.True(lease.Sentence.TryGetUniqueTag(out ReadOnlyMemory<byte> tag, out _));
        Assert.Equal("7", Encoding.ASCII.GetString(tag.Span));
        Assert.True(lease.Sentence.TryGetUniqueAttribute("proplist"u8, out RosAttributeEntry attr, out _));
        Assert.Equal("uptime", Encoding.UTF8.GetString(attr.Value.Span));
    }

    private static byte[] EncodeRawWords(params ReadOnlyMemory<byte>[] words)
    {
        ArrayBufferWriter<byte> buffer = new();
        ApiSentenceEncoder.EncodeWords(buffer, words);
        return buffer.WrittenSpan.ToArray();
    }

    private static RosSentenceLease ParseOne(byte[] encoded)
    {
        using ApiSentenceParser parser = new();
        ReadOnlySequence<byte> sequence = new(encoded);
        ApiSentenceParseStatus status = parser.TryRead(ref sequence, out RosSentenceLease? lease, out RouterOsProtocolError? error);
        Assert.Equal(ApiSentenceParseStatus.Sentence, status);
        Assert.Null(error);
        Assert.NotNull(lease);
        return lease!;
    }
}
