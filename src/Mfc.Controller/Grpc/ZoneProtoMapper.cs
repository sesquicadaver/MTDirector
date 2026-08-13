using Google.Protobuf;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;
using DomainKind = Mfc.Domain.Policy.NodeZoneBindingKind;
using DomainOwnerScope = Mfc.Domain.Policy.PolicyOwnerScope;
using ProtoKind = Mfc.Contracts.Mfc.V1.NodeZoneBindingKind;
using ProtoOwnerScope = Mfc.Contracts.Mfc.V1.PolicyOwnerScope;

namespace Mfc.Controller.Grpc;

internal static class ZoneProtoMapper
{
    public static ZoneDefinition ToProto(ZoneDefinitionView view)
    {
        ZoneDefinition message = new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            OwnerScope = ToProto(view.OwnerScope),
            Key = view.Key,
            Name = view.Name,
            RowVersion = view.RowVersion,
        };
        if (view.OwnerId is Guid ownerId)
        {
            message.OwnerId = ProtoUuid.FromGuid(ownerId);
        }

        if (!string.IsNullOrWhiteSpace(view.Description))
        {
            message.Description = view.Description;
        }

        return message;
    }

    public static NodeZoneBinding ToProto(NodeZoneBindingView view)
    {
        NodeZoneBinding message = new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            ZoneId = ProtoUuid.FromGuid(view.ZoneId),
            Kind = ToProto(view.Kind),
            ExpectedDependencyHash = HexToSha256(view.ExpectedDependencyHashHex),
            AnalysisStale = view.AnalysisStale,
            RowVersion = view.RowVersion,
        };
        message.Values.AddRange(view.Values);
        if (!string.IsNullOrWhiteSpace(view.LastResolvedDependencyHashHex))
        {
            message.LastResolvedDependencyHash = HexToSha256(view.LastResolvedDependencyHashHex);
        }

        return message;
    }

    public static ZoneBindingResolveResult ToProto(ZoneBindingResolveView view)
    {
        ZoneBindingResolveResult message = new()
        {
            BindingId = ProtoUuid.FromGuid(view.BindingId),
            ZoneId = ProtoUuid.FromGuid(view.ZoneId),
            DeviceId = ProtoUuid.FromGuid(view.DeviceId),
            FreshDependencyHash = HexToSha256(view.FreshDependencyHashHex),
            AnalysisStale = view.AnalysisStale,
            Binding = ToProto(view.Binding),
        };
        message.ResolvedMembers.AddRange(view.ResolvedMembers);
        message.Blockers.AddRange(view.Blockers.Select(b =>
        {
            ZoneResolveBlocker blocker = new()
            {
                Code = b.Code,
                Message = b.Message,
            };
            if (!string.IsNullOrWhiteSpace(b.Subject))
            {
                blocker.Subject = b.Subject;
            }

            return blocker;
        }));
        return message;
    }

    public static DomainOwnerScope ToDomain(ProtoOwnerScope scope) => scope switch
    {
        ProtoOwnerScope.Company => DomainOwnerScope.Company,
        ProtoOwnerScope.Site => DomainOwnerScope.Site,
        ProtoOwnerScope.Node => DomainOwnerScope.Node,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported policy owner scope."),
    };

    public static ProtoOwnerScope ToProto(DomainOwnerScope scope) => scope switch
    {
        DomainOwnerScope.Company => ProtoOwnerScope.Company,
        DomainOwnerScope.Site => ProtoOwnerScope.Site,
        DomainOwnerScope.Node => ProtoOwnerScope.Node,
        _ => ProtoOwnerScope.Unspecified,
    };

    public static DomainKind ToDomain(ProtoKind kind) => kind switch
    {
        ProtoKind.InterfaceList => DomainKind.InterfaceList,
        ProtoKind.SingleInterface => DomainKind.SingleInterface,
        ProtoKind.ExplicitInterfaceSet => DomainKind.ExplicitInterfaceSet,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported node zone binding kind."),
    };

    public static ProtoKind ToProto(DomainKind kind) => kind switch
    {
        DomainKind.InterfaceList => ProtoKind.InterfaceList,
        DomainKind.SingleInterface => ProtoKind.SingleInterface,
        DomainKind.ExplicitInterfaceSet => ProtoKind.ExplicitInterfaceSet,
        _ => ProtoKind.Unspecified,
    };

    public static byte[]? ToHashBytes(Sha256? hash)
    {
        if (hash is null || hash.Value.Length == 0)
        {
            return null;
        }

        if (hash.Value.Length != 32)
        {
            throw new ArgumentException("Sha256.value must be exactly 32 bytes.", nameof(hash));
        }

        return hash.Value.ToByteArray();
    }

    private static Sha256 HexToSha256(string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        if (bytes.Length != 32)
        {
            throw new InvalidOperationException("Dependency hash must be 32 bytes.");
        }

        return new Sha256 { Value = ByteString.CopyFrom(bytes) };
    }
}
