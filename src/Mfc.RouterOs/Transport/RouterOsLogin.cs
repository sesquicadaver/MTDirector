using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using System.Text;
using Mfc.RouterOs.Protocol;

namespace Mfc.RouterOs.Transport;

/// <summary>
/// Untagged RouterOS <c>/login</c> (modern password flow only). No concurrency, no retry (Spec §15).
/// </summary>
internal static class RouterOsLogin
{
    public static async Task AuthenticateAsync(
        PipeReader reader,
        PipeWriter writer,
        string username,
        SecretLease password,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(password);

        byte[]? passwordAttribute = null;
        try
        {
            ArrayBufferWriter<byte> buffer = new();
            ApiSentenceEncoder.WriteWord(buffer, "/login"u8);
            WriteEqualsAttribute(buffer, "name"u8, Encoding.ASCII.GetBytes(username));

            passwordAttribute = BuildPasswordAttribute(password.Utf8);
            ApiSentenceEncoder.WriteWord(buffer, passwordAttribute);
            ApiSentenceEncoder.WriteWord(buffer, ReadOnlySpan<byte>.Empty);

            await writer.WriteAsync(buffer.WrittenMemory, cancellationToken).ConfigureAwait(false);
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (passwordAttribute is not null)
            {
                CryptographicOperations.ZeroMemory(passwordAttribute);
            }
        }

        using ApiSentenceParser parser = new();
        bool sawDone = false;
        bool sawTrap = false;
        bool sawLegacyChallenge = false;

        while (!sawDone)
        {
            ReadResult read = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> sequence = read.Buffer;
            try
            {
                while (sequence.Length > 0)
                {
                    ApiSentenceParseStatus status = parser.TryRead(
                        ref sequence,
                        out RosSentenceLease? lease,
                        out RouterOsProtocolError? error);

                    if (status == ApiSentenceParseStatus.NeedMoreData)
                    {
                        break;
                    }

                    if (status == ApiSentenceParseStatus.Faulted)
                    {
                        throw new ApiSslException(
                            ApiSslErrors.AuthenticationFailed,
                            error?.Message ?? "Login response framing failed.");
                    }

                    using (lease)
                    {
                        if (lease is null)
                        {
                            continue;
                        }

                        InspectLoginSentence(lease.Sentence, ref sawDone, ref sawTrap, ref sawLegacyChallenge);
                    }

                    if (sawDone)
                    {
                        break;
                    }
                }

                if (read.IsCompleted && !sawDone)
                {
                    throw new ApiSslException(
                        ApiSslErrors.AuthenticationFailed,
                        "Connection closed during login.");
                }
            }
            finally
            {
                reader.AdvanceTo(sequence.Start, sequence.End);
            }
        }

        if (sawLegacyChallenge)
        {
            throw new ApiSslException(
                ApiSslErrors.UnsupportedLegacyAuth,
                "RouterOS legacy challenge/MD5 login is not supported.");
        }

        if (sawTrap)
        {
            throw new ApiSslException(
                ApiSslErrors.AuthenticationFailed,
                "RouterOS rejected login credentials.");
        }
    }

    private static void InspectLoginSentence(
        RosSentence sentence,
        ref bool sawDone,
        ref bool sawTrap,
        ref bool sawLegacyChallenge)
    {
        if (sentence.Head is null)
        {
            return;
        }

        if (!RosWord.TryDecodeStrictAscii(sentence.Head.Value.Payload.Span, out string? marker) || marker is null)
        {
            throw new ApiSslException(ApiSslErrors.AuthenticationFailed, "Invalid login reply marker.");
        }

        foreach (RosAttributeEntry attribute in sentence.Attributes)
        {
            if (attribute.Name.Span.SequenceEqual("ret"u8))
            {
                sawLegacyChallenge = true;
            }
        }

        switch (marker)
        {
            case "!done":
                sawDone = true;
                break;
            case "!trap":
                sawTrap = true;
                break;
            case "!fatal":
                throw new ApiSslException(ApiSslErrors.AuthenticationFailed, "RouterOS sent !fatal during login.");
            case "!re":
            case "!empty":
                break;
            default:
                throw new ApiSslException(
                    ApiSslErrors.AuthenticationFailed,
                    $"Unexpected login reply marker '{marker}'.");
        }
    }

    private static void WriteEqualsAttribute(
        ArrayBufferWriter<byte> writer,
        ReadOnlySpan<byte> name,
        ReadOnlySpan<byte> value)
    {
        int total = 1 + name.Length + 1 + value.Length;
        int prefixLength = ApiWordLengthCodec.GetEncodedPrefixLength((uint)total);
        Span<byte> dest = writer.GetSpan(prefixLength + total);
        int written = ApiWordLengthCodec.Encode((uint)total, dest);
        int offset = written;
        dest[offset++] = (byte)'=';
        name.CopyTo(dest[offset..]);
        offset += name.Length;
        dest[offset++] = (byte)'=';
        value.CopyTo(dest[offset..]);
        writer.Advance(written + total);
    }

    private static byte[] BuildPasswordAttribute(ReadOnlySpan<byte> passwordUtf8)
    {
        // =password=<secret> — never via string interpolation.
        byte[] attribute = new byte[1 + 8 + 1 + passwordUtf8.Length];
        attribute[0] = (byte)'=';
        "password"u8.CopyTo(attribute.AsSpan(1));
        attribute[9] = (byte)'=';
        passwordUtf8.CopyTo(attribute.AsSpan(10));
        return attribute;
    }
}
