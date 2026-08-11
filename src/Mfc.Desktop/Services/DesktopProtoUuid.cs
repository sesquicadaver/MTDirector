using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Desktop.Services;

/// <summary>Network-byte-order UUID helpers for Desktop gRPC clients.</summary>
public static class DesktopProtoUuid
{
    public static Uuid FromGuid(Guid value)
    {
        byte[] bytes = value.ToByteArray(bigEndian: true);
        return new Uuid { Value = ByteString.CopyFrom(bytes) };
    }

    public static Guid ToGuid(Uuid? value)
    {
        if (value is null || value.Value.Length != 16)
        {
            throw new ArgumentException("Uuid.value must be exactly 16 bytes.");
        }

        return new Guid(value.Value.Span, bigEndian: true);
    }
}
