using Google.Protobuf.WellKnownTypes;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Controller.Grpc;

/// <summary>Maps Application audit views to Contracts proto messages (M6-04).</summary>
internal static class AuditProtoMapper
{
    public static AuditEvent ToProto(AuditEventView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new AuditEvent
        {
            Id = ProtoUuid.FromGuid(view.Id),
            OccurredAt = Timestamp.FromDateTimeOffset(view.OccurredAtUtc),
            Actor = view.Actor,
            Action = view.Action,
            PayloadJson = view.PayloadJson,
        };
    }
}
