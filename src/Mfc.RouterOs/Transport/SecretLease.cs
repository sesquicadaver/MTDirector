using System.Security.Cryptography;

namespace Mfc.RouterOs.Transport;

/// <summary>
/// Short-lived plaintext secret buffer. Cleared on dispose (Spec §15.3).
/// </summary>
public sealed class SecretLease : IDisposable
{
    private byte[]? _utf8;
    private bool _disposed;

    public SecretLease(ReadOnlySpan<byte> passwordUtf8)
    {
        _utf8 = passwordUtf8.ToArray();
    }

    public ReadOnlySpan<byte> Utf8
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _utf8!;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_utf8 is not null)
        {
            CryptographicOperations.ZeroMemory(_utf8);
            _utf8 = null;
        }

        GC.SuppressFinalize(this);
    }
}
