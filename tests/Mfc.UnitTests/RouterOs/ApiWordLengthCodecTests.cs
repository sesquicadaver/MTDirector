using System.Buffers.Binary;
using Mfc.RouterOs.Protocol;
using Xunit;

namespace Mfc.UnitTests.RouterOs;

public sealed class ApiWordLengthCodecTests
{
    public static TheoryData<uint, byte[]> NormativeVectors { get; } = new()
    {
        { 0u, [0x00] },
        { 1u, [0x01] },
        { 127u, [0x7F] },
        { 128u, [0x80, 0x80] },
        { 16_383u, [0xBF, 0xFF] },
        { 16_384u, [0xC0, 0x40, 0x00] },
        { 2_097_151u, [0xDF, 0xFF, 0xFF] },
        { 2_097_152u, [0xE0, 0x20, 0x00, 0x00] },
        { 268_435_455u, [0xEF, 0xFF, 0xFF, 0xFF] },
        { 268_435_456u, [0xF0, 0x10, 0x00, 0x00, 0x00] },
        { 4_294_967_295u, [0xF0, 0xFF, 0xFF, 0xFF, 0xFF] },
    };

    [Theory]
    [MemberData(nameof(NormativeVectors))]
    public void EncodeMatchesNormativeVectors(uint length, byte[] expected)
    {
        Span<byte> buffer = stackalloc byte[8];
        int written = ApiWordLengthCodec.Encode(length, buffer);
        Assert.Equal(expected.Length, written);
        Assert.Equal(expected, buffer[..written].ToArray());
        Assert.Equal(expected.Length, ApiWordLengthCodec.GetEncodedPrefixLength(length));
    }

    [Theory]
    [MemberData(nameof(NormativeVectors))]
    public void DecodeMatchesNormativeVectorsWhenLimitAllows(uint length, byte[] encoded)
    {
        ApiLengthDecodeStatus status = ApiWordLengthCodec.TryDecode(
            encoded,
            maxWordPayloadBytes: ApiWordLengthCodec.UnlimitedMaxWordPayloadBytes,
            out uint decoded,
            out int consumed,
            out RouterOsProtocolError? error);

        Assert.Equal(ApiLengthDecodeStatus.Success, status);
        Assert.Null(error);
        Assert.Equal(length, decoded);
        Assert.Equal(encoded.Length, consumed);
    }

    [Fact]
    public void DecodeRejectsLengthAboveConfiguredMaximumBeforeAllocation()
    {
        Span<byte> encoded = stackalloc byte[8];
        int written = ApiWordLengthCodec.Encode(length: 1024, encoded);
        ApiLengthDecodeStatus status = ApiWordLengthCodec.TryDecode(
            encoded[..written],
            maxWordPayloadBytes: 512,
            out _,
            out _,
            out RouterOsProtocolError? error);

        Assert.Equal(ApiLengthDecodeStatus.Faulted, status);
        Assert.Equal(RouterOsProtocolError.WordTooLarge, error!.Code);
    }

    [Fact]
    public void DefaultMaximumIs256KiB()
    {
        Assert.Equal(256 * 1024, ApiWordLengthCodec.DefaultMaxWordPayloadBytes);
        uint max = (uint)ApiWordLengthCodec.DefaultMaxWordPayloadBytes;

        Span<byte> ok = stackalloc byte[8];
        int okLen = ApiWordLengthCodec.Encode(max, ok);
        Assert.Equal(
            ApiLengthDecodeStatus.Success,
            ApiWordLengthCodec.TryDecode(ok[..okLen], max, out _, out _, out _));

        Span<byte> over = stackalloc byte[8];
        int overLen = ApiWordLengthCodec.Encode(max + 1, over);
        Assert.Equal(
            ApiLengthDecodeStatus.Faulted,
            ApiWordLengthCodec.TryDecode(
                over[..overLen],
                max,
                out _,
                out _,
                out RouterOsProtocolError? error));
        Assert.Equal(RouterOsProtocolError.WordTooLarge, error!.Code);
    }

    [Theory]
    [InlineData(new byte[] { 0x80, 0x7F }, RouterOsProtocolError.LengthEncodingNonCanonical)]
    [InlineData(new byte[] { 0xC0, 0x3F, 0xFF }, RouterOsProtocolError.LengthEncodingNonCanonical)]
    [InlineData(new byte[] { 0xE0, 0x1F, 0xFF, 0xFF }, RouterOsProtocolError.LengthEncodingNonCanonical)]
    [InlineData(new byte[] { 0xF0, 0x0F, 0xFF, 0xFF, 0xFF }, RouterOsProtocolError.LengthEncodingNonCanonical)]
    [InlineData(new byte[] { 0xF1 }, RouterOsProtocolError.LengthPrefixUnsupported)]
    [InlineData(new byte[] { 0xF7 }, RouterOsProtocolError.LengthPrefixUnsupported)]
    [InlineData(new byte[] { 0xF8 }, RouterOsProtocolError.ReservedControlByte)]
    [InlineData(new byte[] { 0xFF }, RouterOsProtocolError.ReservedControlByte)]
    public void DecodeRejectsNonCanonicalAndReservedPrefixes(byte[] source, string expectedCode)
    {
        ApiLengthDecodeStatus status = ApiWordLengthCodec.TryDecode(
            source,
            ApiWordLengthCodec.UnlimitedMaxWordPayloadBytes,
            out _,
            out _,
            out RouterOsProtocolError? error);

        Assert.Equal(ApiLengthDecodeStatus.Faulted, status);
        Assert.Equal(expectedCode, error!.Code);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x80 })]
    [InlineData(new byte[] { 0xC0 })]
    [InlineData(new byte[] { 0xC0, 0x40 })]
    [InlineData(new byte[] { 0xE0 })]
    [InlineData(new byte[] { 0xE0, 0x20, 0x00 })]
    [InlineData(new byte[] { 0xF0 })]
    [InlineData(new byte[] { 0xF0, 0x10, 0x00, 0x00 })]
    public void DecodeSupportsFragmentedPrefixInput(byte[] fragment)
    {
        ApiLengthDecodeStatus status = ApiWordLengthCodec.TryDecode(
            fragment,
            ApiWordLengthCodec.UnlimitedMaxWordPayloadBytes,
            out _,
            out int consumed,
            out RouterOsProtocolError? error);

        Assert.Equal(ApiLengthDecodeStatus.NeedMoreData, status);
        Assert.Equal(0, consumed);
        Assert.Null(error);
    }

    [Fact]
    public void FragmentedThenCompleteRoundTrip()
    {
        byte[] full = [0xC0, 0x40, 0x00];
        Assert.Equal(
            ApiLengthDecodeStatus.NeedMoreData,
            ApiWordLengthCodec.TryDecode(
                full.AsSpan(0, 1),
                ApiWordLengthCodec.UnlimitedMaxWordPayloadBytes,
                out _,
                out _,
                out _));
        Assert.Equal(
            ApiLengthDecodeStatus.NeedMoreData,
            ApiWordLengthCodec.TryDecode(
                full.AsSpan(0, 2),
                ApiWordLengthCodec.UnlimitedMaxWordPayloadBytes,
                out _,
                out _,
                out _));

        ApiLengthDecodeStatus status = ApiWordLengthCodec.TryDecode(
            full,
            ApiWordLengthCodec.UnlimitedMaxWordPayloadBytes,
            out uint length,
            out int consumed,
            out RouterOsProtocolError? error);
        Assert.Equal(ApiLengthDecodeStatus.Success, status);
        Assert.Equal(16_384u, length);
        Assert.Equal(3, consumed);
        Assert.Null(error);
    }

    [Fact]
    public void EncodeThrowsWhenDestinationTooSmall()
    {
        byte[] tiny = new byte[1];
        Assert.Throws<ArgumentException>(() => ApiWordLengthCodec.Encode(128, tiny));
    }

    [Fact]
    public void EncodingIsIndependentOfHostEndianness()
    {
        Span<byte> buffer = stackalloc byte[4];
        ApiWordLengthCodec.Encode(2_097_152u, buffer);
        Assert.Equal(0xE0, buffer[0]);
        Assert.Equal(0x20, buffer[1]);
        Assert.Equal(0x00, buffer[2]);
        Assert.Equal(0x00, buffer[3]);
        Assert.Equal(0xE0200000u, BinaryPrimitives.ReadUInt32BigEndian(buffer));
    }

    /// <summary>
    /// Property-style round-trip over the production-allowed range (≤ configured max).
    /// Fixed seed keeps CI deterministic.
    /// </summary>
    [Fact]
    public void PropertyRoundTripWithinConfiguredMaximum()
    {
        uint max = (uint)ApiWordLengthCodec.DefaultMaxWordPayloadBytes;
        Random rng = new(0x4D314F36);
        Span<byte> buffer = stackalloc byte[8];

        uint[] fixedPoints =
        [
            0,
            1,
            0x7F,
            0x80,
            0x3FFF,
            0x4000,
            max,
        ];

        foreach (uint length in fixedPoints)
        {
            AssertRoundTrip(length, max, buffer);
        }

        for (int i = 0; i < 5_000; i++)
        {
            uint length = (uint)rng.Next(0, (int)max + 1);
            AssertRoundTrip(length, max, buffer);
        }
    }

    private static void AssertRoundTrip(uint length, uint maxWordPayloadBytes, Span<byte> buffer)
    {
        int written = ApiWordLengthCodec.Encode(length, buffer);
        ApiLengthDecodeStatus status = ApiWordLengthCodec.TryDecode(
            buffer[..written],
            maxWordPayloadBytes,
            out uint decoded,
            out int consumed,
            out RouterOsProtocolError? error);

        Assert.Equal(ApiLengthDecodeStatus.Success, status);
        Assert.Null(error);
        Assert.Equal(length, decoded);
        Assert.Equal(written, consumed);
        Assert.Equal(written, ApiWordLengthCodec.GetEncodedPrefixLength(length));
    }
}
