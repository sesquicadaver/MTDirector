using Google.Protobuf;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Controller.Grpc;

/// <summary>UUID helpers for network-byte-order proto encoding.</summary>
public static class ProtoUuid
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

    public static Guid? ToNullableGuid(Uuid? value)
    {
        if (value is null || value.Value.Length == 0)
        {
            return null;
        }

        return ToGuid(value);
    }
}
