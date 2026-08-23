using Google.Protobuf;
using Mfc.Application.Models;
using Mfc.Application.Policies;
using Mfc.Contracts.Mfc.V1;
using DomainAddressType = Mfc.Domain.Policy.AddressType;
using DomainConnectionNatState = Mfc.Domain.Policy.ConnectionNatState;
using DomainConnectionState = Mfc.Domain.Policy.ConnectionState;
using DomainFamily = Mfc.Domain.Inventory.IpAddressFamily;
using DomainFilterChain = Mfc.Domain.Policy.PolicyFilterChain;
using DomainIpsecDirection = Mfc.Domain.Policy.IpsecDirection;
using DomainIpsecPolicyKind = Mfc.Domain.Policy.IpsecPolicyKind;
using DomainKind = Mfc.Domain.Policy.PolicyKind;
using DomainOwnerScope = Mfc.Domain.Policy.PolicyOwnerScope;
using DomainRejectMode = Mfc.Domain.Policy.RejectMode;
using DomainRevisionState = Mfc.Domain.Policy.PolicyRevisionState;
using DomainRuleEffect = Mfc.Domain.Policy.PolicyRuleEffect;
using DomainStage = Mfc.Domain.Policy.PolicyPipelineStage;
using DomainTcpHeaderBit = Mfc.Domain.Policy.TcpHeaderBit;
using ProtoAddressType = Mfc.Contracts.Mfc.V1.AddressType;
using ProtoBindingScope = Mfc.Contracts.Mfc.V1.PolicyBindingScope;
using ProtoBindingState = Mfc.Contracts.Mfc.V1.PolicyBindingState;
using ProtoConnectionNatState = Mfc.Contracts.Mfc.V1.ConnectionNatState;
using ProtoConnectionState = Mfc.Contracts.Mfc.V1.ConnectionState;
using ProtoFamily = Mfc.Contracts.Mfc.V1.IpAddressFamily;
using ProtoFilterChain = Mfc.Contracts.Mfc.V1.PolicyFilterChain;
using ProtoIpsecDirection = Mfc.Contracts.Mfc.V1.IpsecDirection;
using ProtoIpsecPolicyKind = Mfc.Contracts.Mfc.V1.IpsecPolicyKind;
using ProtoKind = Mfc.Contracts.Mfc.V1.PolicyKind;
using ProtoOwnerScope = Mfc.Contracts.Mfc.V1.PolicyOwnerScope;
using ProtoRejectMode = Mfc.Contracts.Mfc.V1.RejectMode;
using ProtoRevisionState = Mfc.Contracts.Mfc.V1.PolicyRevisionState;
using ProtoRuleEffect = Mfc.Contracts.Mfc.V1.PolicyRuleEffect;
using ProtoStage = Mfc.Contracts.Mfc.V1.PolicyPipelineStage;
using ProtoTcpHeaderBit = Mfc.Contracts.Mfc.V1.TcpHeaderBit;

namespace Mfc.Controller.Grpc;

internal static class PolicyProtoMapper
{
    public static PolicyDraft ToProto(PolicyDraftView view)
    {
        PolicyDraft message = new()
        {
            PolicyId = ProtoUuid.FromGuid(view.PolicyId),
            RevisionId = ProtoUuid.FromGuid(view.RevisionId),
            Name = view.Name,
            Kind = ToProto(view.Kind),
            OwnerScope = ToProto(view.OwnerScope),
            RevisionNumber = view.RevisionNumber,
            ContentHash = HexToSha256(view.ContentHashHex),
        };
        if (view.OwnerId is Guid ownerId)
        {
            message.OwnerId = ProtoUuid.FromGuid(ownerId);
        }

        return message;
    }

    public static global::Mfc.Contracts.Mfc.V1.PolicyRevision ToProto(PolicyRevisionView view)
    {
        global::Mfc.Contracts.Mfc.V1.PolicyRevision message = new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            PolicyId = ProtoUuid.FromGuid(view.PolicyId),
            RevisionNumber = view.RevisionNumber,
            SchemaVersion = view.SchemaVersion,
            State = ToProto(view.State),
            ContentHash = HexToSha256(view.ContentHashHex),
            Kind = ToProto(view.Kind),
            OwnerScope = ToProto(view.OwnerScope),
        };
        if (!string.IsNullOrWhiteSpace(view.ParentContextHashHex))
        {
            message.ParentContextHash = HexToSha256(view.ParentContextHashHex);
        }

        message.Rules.AddRange(view.Rules.Select(ToProto));
        message.Warnings.AddRange(view.Warnings.Select(ToProto));
        if (view.ExceptionMetadata is not null)
        {
            message.ExceptionMetadata = ToProto(view.ExceptionMetadata);
        }

        message.AddressObjects.AddRange(view.AddressObjects.Select(ToProto));
        message.ServiceObjects.AddRange(view.ServiceObjects.Select(ToProto));
        message.ChainContracts.AddRange(view.ChainContracts.Select(ToProto));
        message.TestsJson = view.TestsJson ?? "[]";
        return message;
    }

    public static AddressObject ToProto(AddressObjectView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        AddressObject message = new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            Name = view.Name,
            Family = ToProto(view.Family),
        };
        message.Entries.AddRange(view.Entries.Select(ToProto));
        if (!string.IsNullOrWhiteSpace(view.Description))
        {
            message.Description = view.Description;
        }

        return message;
    }

    public static AddressObjectEntry ToProto(AddressObjectEntryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        AddressObjectEntry message = new() { Kind = view.Kind };
        if (view.Address is not null)
        {
            message.Address = view.Address;
        }

        if (view.PrefixLength is byte prefix)
        {
            message.PrefixLength = prefix;
        }

        if (view.Start is not null)
        {
            message.Start = view.Start;
        }

        if (view.End is not null)
        {
            message.End = view.End;
        }

        return message;
    }

    public static ServiceObject ToProto(ServiceObjectView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ServiceObject message = new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            Name = view.Name,
        };
        message.Terms.AddRange(view.Terms.Select(ToProto));
        if (!string.IsNullOrWhiteSpace(view.Description))
        {
            message.Description = view.Description;
        }

        return message;
    }

    public static ServiceTerm ToProto(ServiceTermView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ServiceTerm message = new()
        {
            Protocol = new IpProtocolSpec
            {
                Any = view.Protocol.Any,
            },
        };
        if (view.Protocol.Number is byte number)
        {
            message.Protocol.Number = number;
        }

        if (!string.IsNullOrWhiteSpace(view.Protocol.CanonicalName))
        {
            message.Protocol.CanonicalName = view.Protocol.CanonicalName;
        }

        message.SourcePorts.AddRange(view.SourcePorts.Select(static p => new PortInterval
        {
            Start = p.Start,
            End = p.End,
        }));
        message.DestinationPorts.AddRange(view.DestinationPorts.Select(static p => new PortInterval
        {
            Start = p.Start,
            End = p.End,
        }));
        message.IcmpSelectors.AddRange(view.IcmpSelectors.Select(static i =>
        {
            IcmpSelector selector = new() { Type = i.Type };
            if (i.Code is byte code)
            {
                selector.Code = code;
            }

            return selector;
        }));
        return message;
    }

    public static ChainContract ToProto(ChainContractView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ChainContract message = new()
        {
            Family = ToProto(view.Family),
            Chain = ToProto(view.Chain),
            DefaultDisposition = view.DefaultDisposition,
        };
        if (view.RejectMode is DomainRejectMode mode)
        {
            message.RejectMode = ToProto(mode);
        }

        return message;
    }

    public static PolicyRevisionDiff ToProto(PolicyRevisionDiffView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        PolicyRevisionDiff message = new()
        {
            BeforeRevisionId = ProtoUuid.FromGuid(view.BeforeRevisionId),
            AfterRevisionId = ProtoUuid.FromGuid(view.AfterRevisionId),
            RiskLevel = view.RiskLevel,
        };
        message.RuleChanges.AddRange(view.RuleChanges.Select(static line =>
        {
            PolicyRuleDiffLine proto = new() { RuleId = ProtoUuid.FromGuid(line.RuleId) };
            proto.Changes.AddRange(line.Changes);
            return proto;
        }));
        message.SemanticClasses.AddRange(view.SemanticClasses);
        message.PacketSpaceClasses.AddRange(view.PacketSpaceClasses);
        message.RiskDrivers.AddRange(view.RiskDrivers);
        message.FindingSummaries.AddRange(view.FindingSummaries.Select(static f => new PolicyAnalysisFinding
        {
            Code = f.Code,
            Severity = f.Severity,
            Message = f.Message,
        }));
        return message;
    }

    public static CompileNodeFilterArtifactsResponse ToProto(CompileNodeFilterArtifactsView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        CompileNodeFilterArtifactsResponse message = new()
        {
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            LogicalEffectivePolicyHash = ToSha256(view.LogicalEffectivePolicyHash),
        };
        message.Artifacts.AddRange(view.Artifacts.Select(static a => new FilterArtifactSummary
        {
            DeviceId = ProtoUuid.FromGuid(a.DeviceId),
            ArtifactId = a.ArtifactId,
            ResourceHash = ToSha256(a.ResourceHash),
            PhysicalSemanticsHash = ToSha256(a.PhysicalSemanticsHash),
            DeviceResolvedPolicyHash = ToSha256(a.DeviceResolvedPolicyHash),
            AnalysisBundleHash = ToSha256(a.AnalysisBundleHash),
            AddressListCount = checked((uint)a.AddressListCount),
            ChainCount = checked((uint)a.ChainCount),
            RuleCount = checked((uint)a.RuleCount),
            AnchorTargetCount = checked((uint)a.AnchorTargetCount),
            StoredAsNew = a.StoredAsNew,
        }));
        return message;
    }

    public static AddressObjectEntryView ToInput(AddressObjectEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new AddressObjectEntryView
        {
            Kind = entry.Kind,
            Address = entry.HasAddress ? entry.Address : null,
            PrefixLength = entry.HasPrefixLength ? (byte)entry.PrefixLength : null,
            Start = entry.HasStart ? entry.Start : null,
            End = entry.HasEnd ? entry.End : null,
        };
    }

    public static ServiceTermView ToInput(ServiceTerm term)
    {
        ArgumentNullException.ThrowIfNull(term);
        ArgumentNullException.ThrowIfNull(term.Protocol);
        return new ServiceTermView
        {
            Protocol = new IpProtocolView
            {
                Any = term.Protocol.Any,
                Number = term.Protocol.HasNumber ? (byte)term.Protocol.Number : null,
                CanonicalName = term.Protocol.HasCanonicalName ? term.Protocol.CanonicalName : null,
            },
            SourcePorts = term.SourcePorts.Select(static p => new PortIntervalView
            {
                Start = (ushort)p.Start,
                End = (ushort)p.End,
            }).ToArray(),
            DestinationPorts = term.DestinationPorts.Select(static p => new PortIntervalView
            {
                Start = (ushort)p.Start,
                End = (ushort)p.End,
            }).ToArray(),
            IcmpSelectors = term.IcmpSelectors.Select(static i => new IcmpSelectorView
            {
                Type = (byte)i.Type,
                Code = i.HasCode ? (byte)i.Code : null,
            }).ToArray(),
        };
    }

    public static ChainContractView ToInput(ChainContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return new ChainContractView
        {
            Family = ToDomain(contract.Family),
            Chain = ToDomain(contract.Chain),
            DefaultDisposition = contract.DefaultDisposition,
            RejectMode = contract.HasRejectMode ? ToDomain(contract.RejectMode) : null,
        };
    }

    public static global::Mfc.Contracts.Mfc.V1.PolicyRule ToProto(PolicyRuleView view)
    {
        global::Mfc.Contracts.Mfc.V1.PolicyRule message = new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            Family = ToProto(view.Family),
            Chain = ToProto(view.Chain),
            Stage = ToProto(view.Stage),
            Ordinal = view.Ordinal,
            Enabled = view.Enabled,
            Predicate = ToProto(view.Predicate),
            Effect = ToProto(view.Effect),
            Logging = ToProto(view.Logging),
            ExceptionEligible = view.ExceptionEligible,
            Description = view.Description,
        };
        message.Warnings.AddRange(view.Warnings.Select(ToProto));
        return message;
    }

    public static PolicyRuleMutation ToProto(PolicyRuleMutationView view)
    {
        PolicyRuleMutation message = new()
        {
            ContentHash = HexToSha256(view.ContentHashHex),
        };
        if (view.Rule is not null)
        {
            message.Rule = ToProto(view.Rule);
        }

        message.Rules.AddRange(view.Rules.Select(ToProto));
        message.Warnings.AddRange(view.Warnings.Select(ToProto));
        return message;
    }

    public static ListRulesResponse ToProto(PolicyRuleListView view)
    {
        ListRulesResponse message = new()
        {
            RevisionId = ProtoUuid.FromGuid(view.RevisionId),
            ContentHash = HexToSha256(view.ContentHashHex),
        };
        message.Rules.AddRange(view.Rules.Select(ToProto));
        message.Warnings.AddRange(view.Warnings.Select(ToProto));
        return message;
    }

    public static EffectivePolicy ToProto(EffectivePolicyView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        EffectivePolicy message = new()
        {
            NodeId = ProtoUuid.FromGuid(view.NodeId),
            LogicalEffectiveHash = ToSha256(view.LogicalEffectiveHash),
            Company = ToProto(view.Company),
        };
        if (view.Site is not null)
        {
            message.Site = ToProto(view.Site);
        }

        if (view.Node is not null)
        {
            message.Node = ToProto(view.Node);
        }

        message.Rules.AddRange(view.ActiveRules.Select(ToProto));
        message.Findings.AddRange(view.Findings.Select(ToProto));
        return message;
    }

    public static PolicyRevisionRef ToProto(PolicyRevisionRefView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return new PolicyRevisionRef
        {
            PolicyId = ProtoUuid.FromGuid(view.PolicyId),
            RevisionId = ProtoUuid.FromGuid(view.RevisionId),
            RevisionNumber = view.RevisionNumber,
            ContentHash = ToSha256(view.ContentHash),
        };
    }

    public static PolicyWarning ToProto(PolicyWarningView view)
    {
        PolicyWarning message = new()
        {
            Code = view.Code,
            Message = view.Message,
        };
        if (!string.IsNullOrWhiteSpace(view.Subject))
        {
            message.Subject = view.Subject;
        }

        return message;
    }

    public static TrafficPredicate ToProto(TrafficPredicateView view)
    {
        TrafficPredicate message = new();
        if (view.SourceAddresses is not null)
        {
            message.SourceAddresses = ToProto(view.SourceAddresses);
        }

        if (view.DestinationAddresses is not null)
        {
            message.DestinationAddresses = ToProto(view.DestinationAddresses);
        }

        if (view.IngressZones is not null)
        {
            message.IngressZones = ToProto(view.IngressZones);
        }

        if (view.EgressZones is not null)
        {
            message.EgressZones = ToProto(view.EgressZones);
        }

        if (view.Services is not null)
        {
            message.Services = ToProto(view.Services);
        }

        if (view.ConnectionStates is not null)
        {
            message.ConnectionStates.AddRange(view.ConnectionStates.Select(ToProto));
        }

        if (view.ConnectionNatStates is not null)
        {
            message.ConnectionNatStates.AddRange(view.ConnectionNatStates.Select(ToProto));
        }

        if (view.SourceAddressTypes is not null)
        {
            message.SourceAddressTypes.AddRange(view.SourceAddressTypes.Select(ToProto));
        }

        if (view.DestinationAddressTypes is not null)
        {
            message.DestinationAddressTypes.AddRange(view.DestinationAddressTypes.Select(ToProto));
        }

        if (view.TcpFlags is not null)
        {
            message.TcpFlags = new TcpFlagConstraint();
            message.TcpFlags.RequiredPresent.AddRange(view.TcpFlags.RequiredPresent.Select(ToProto));
            message.TcpFlags.RequiredAbsent.AddRange(view.TcpFlags.RequiredAbsent.Select(ToProto));
        }

        if (view.IpsecPolicy is not null)
        {
            message.IpsecPolicy = new IpsecPolicyPredicate
            {
                Direction = ToProto(view.IpsecPolicy.Direction),
                Policy = ToProto(view.IpsecPolicy.Policy),
            };
        }

        return message;
    }

    public static RuleEffect ToProto(RuleEffectView view)
    {
        RuleEffect message = new() { Kind = ToProto(view.Kind) };
        if (view.RejectMode is DomainRejectMode mode)
        {
            message.RejectMode = ToProto(mode);
        }

        return message;
    }

    public static LogSpecification ToProto(LogSpecificationView view)
    {
        LogSpecification message = new() { Enabled = view.Enabled };
        if (view.Prefix is not null)
        {
            message.Prefix = view.Prefix;
        }

        return message;
    }

    public static ZoneSelector ToProto(ZoneSelectorView view)
    {
        ZoneSelector message = new();
        message.Include.AddRange(view.Include.Select(ProtoUuid.FromGuid));
        message.Exclude.AddRange(view.Exclude.Select(ProtoUuid.FromGuid));
        return message;
    }

    public static AddressSelector ToProto(AddressSelectorView view)
    {
        AddressSelector message = new();
        message.Include.AddRange(view.Include.Select(ProtoUuid.FromGuid));
        message.Exclude.AddRange(view.Exclude.Select(ProtoUuid.FromGuid));
        return message;
    }

    public static ServiceSelector ToProto(ServiceSelectorView view)
    {
        ServiceSelector message = new();
        message.Include.AddRange(view.Include.Select(ProtoUuid.FromGuid));
        return message;
    }

    public static TrafficPredicateInput? ToInput(TrafficPredicate? predicate)
    {
        if (predicate is null)
        {
            return null;
        }

        return new TrafficPredicateInput
        {
            SourceAddresses = ToInput(predicate.SourceAddresses),
            DestinationAddresses = ToInput(predicate.DestinationAddresses),
            IngressZones = ToInput(predicate.IngressZones),
            EgressZones = ToInput(predicate.EgressZones),
            Services = ToInput(predicate.Services),
            ConnectionStates = predicate.ConnectionStates.Count == 0
                ? null
                : predicate.ConnectionStates.Select(ToDomain).ToArray(),
            ConnectionNatStates = predicate.ConnectionNatStates.Count == 0
                ? null
                : predicate.ConnectionNatStates.Select(ToDomain).ToArray(),
            SourceAddressTypes = predicate.SourceAddressTypes.Count == 0
                ? null
                : predicate.SourceAddressTypes.Select(ToDomain).ToArray(),
            DestinationAddressTypes = predicate.DestinationAddressTypes.Count == 0
                ? null
                : predicate.DestinationAddressTypes.Select(ToDomain).ToArray(),
            TcpFlags = predicate.TcpFlags is null
                ? null
                : new TcpFlagConstraintInput
                {
                    RequiredPresent = predicate.TcpFlags.RequiredPresent.Select(ToDomain).ToArray(),
                    RequiredAbsent = predicate.TcpFlags.RequiredAbsent.Select(ToDomain).ToArray(),
                },
            IpsecPolicy = predicate.IpsecPolicy is null
                ? null
                : new IpsecPolicyPredicateInput
                {
                    Direction = ToDomain(predicate.IpsecPolicy.Direction),
                    Policy = ToDomain(predicate.IpsecPolicy.Policy),
                },
        };
    }

    public static RuleEffectInput ToInput(RuleEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);
        return new RuleEffectInput
        {
            Kind = ToDomain(effect.Kind),
            RejectMode = effect.HasRejectMode ? ToDomain(effect.RejectMode) : null,
        };
    }

    public static global::Mfc.Contracts.Mfc.V1.ExceptionMetadata ToProto(ExceptionMetadataView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        global::Mfc.Contracts.Mfc.V1.ExceptionMetadata message = new()
        {
            TargetScope = ToProto(view.TargetScope),
            TargetScopeId = ProtoUuid.FromGuid(view.TargetScopeId),
            TargetStage = ToProto(view.TargetStage),
            WaivedRuleId = ProtoUuid.FromGuid(view.WaivedRuleId),
            ValidFrom = Domain.Policy.ExceptionMetadata.FormatTimestamp(view.ValidFrom),
            ValidUntil = Domain.Policy.ExceptionMetadata.FormatTimestamp(view.ValidUntil),
            Reason = view.Reason,
            TicketReference = view.TicketReference,
        };
        if (view.SupersedesExceptionId is Guid supersedes)
        {
            message.SupersedesExceptionId = ProtoUuid.FromGuid(supersedes);
        }

        return message;
    }

    public static ExceptionMetadataInput ToInput(global::Mfc.Contracts.Mfc.V1.ExceptionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        return new ExceptionMetadataInput
        {
            TargetScope = ToDomain(metadata.TargetScope),
            TargetScopeId = ProtoUuid.ToGuid(metadata.TargetScopeId),
            TargetStage = ToDomain(metadata.TargetStage),
            WaivedRuleId = ProtoUuid.ToGuid(metadata.WaivedRuleId),
            ValidFrom = Domain.Policy.ExceptionMetadata.ParseTimestamp(metadata.ValidFrom, "valid_from"),
            ValidUntil = Domain.Policy.ExceptionMetadata.ParseTimestamp(metadata.ValidUntil, "valid_until"),
            Reason = metadata.Reason,
            TicketReference = metadata.TicketReference,
            SupersedesExceptionId = metadata.SupersedesExceptionId is null
                ? null
                : ProtoUuid.ToGuid(metadata.SupersedesExceptionId),
        };
    }

    public static LogSpecificationInput? ToInput(LogSpecification? logging)
    {
        if (logging is null)
        {
            return null;
        }

        return new LogSpecificationInput
        {
            Enabled = logging.Enabled,
            Prefix = logging.HasPrefix ? logging.Prefix : null,
        };
    }

    public static AddressSelectorInput? ToInput(AddressSelector? selector)
    {
        if (selector is null)
        {
            return null;
        }

        return new AddressSelectorInput
        {
            Include = selector.Include.Select(ProtoUuid.ToGuid).ToArray(),
            Exclude = selector.Exclude.Select(ProtoUuid.ToGuid).ToArray(),
        };
    }

    public static ZoneSelectorInput? ToInput(ZoneSelector? selector)
    {
        if (selector is null)
        {
            return null;
        }

        return new ZoneSelectorInput
        {
            Include = selector.Include.Select(ProtoUuid.ToGuid).ToArray(),
            Exclude = selector.Exclude.Select(ProtoUuid.ToGuid).ToArray(),
        };
    }

    public static ServiceSelectorInput? ToInput(ServiceSelector? selector)
    {
        if (selector is null)
        {
            return null;
        }

        return new ServiceSelectorInput
        {
            Include = selector.Include.Select(ProtoUuid.ToGuid).ToArray(),
        };
    }

    public static byte[] ToHashBytes(Sha256 hash)
    {
        ArgumentNullException.ThrowIfNull(hash);
        if (hash.Value.Length != 32)
        {
            throw new ArgumentException("Sha256.value must be exactly 32 bytes.", nameof(hash));
        }

        return hash.Value.ToByteArray();
    }

    public static byte[]? ToOptionalHashBytes(Sha256? hash)
    {
        if (hash is null || hash.Value.Length == 0)
        {
            return null;
        }

        return ToHashBytes(hash);
    }

    public static DomainKind ToDomain(ProtoKind kind) => kind switch
    {
        ProtoKind.CompanyBaseline => DomainKind.CompanyBaseline,
        ProtoKind.SiteOverlay => DomainKind.SiteOverlay,
        ProtoKind.NodeOverlay => DomainKind.NodeOverlay,
        ProtoKind.Exception => DomainKind.Exception,
        ProtoKind.IncidentDenyOverlay => DomainKind.IncidentDenyOverlay,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported policy kind."),
    };

    public static ProtoKind ToProto(DomainKind kind) => kind switch
    {
        DomainKind.CompanyBaseline => ProtoKind.CompanyBaseline,
        DomainKind.SiteOverlay => ProtoKind.SiteOverlay,
        DomainKind.NodeOverlay => ProtoKind.NodeOverlay,
        DomainKind.Exception => ProtoKind.Exception,
        DomainKind.IncidentDenyOverlay => ProtoKind.IncidentDenyOverlay,
        _ => ProtoKind.Unspecified,
    };

    public static DomainOwnerScope ToDomain(ProtoOwnerScope scope) => ZoneProtoMapper.ToDomain(scope);

    public static ProtoOwnerScope ToProto(DomainOwnerScope scope) => ZoneProtoMapper.ToProto(scope);

    public static ProtoRevisionState ToProto(DomainRevisionState state) => state switch
    {
        DomainRevisionState.Draft => ProtoRevisionState.Draft,
        DomainRevisionState.Validated => ProtoRevisionState.Validated,
        DomainRevisionState.InReview => ProtoRevisionState.InReview,
        DomainRevisionState.Approved => ProtoRevisionState.Approved,
        DomainRevisionState.Rejected => ProtoRevisionState.Rejected,
        DomainRevisionState.Superseded => ProtoRevisionState.Superseded,
        DomainRevisionState.Revoked => ProtoRevisionState.Revoked,
        _ => ProtoRevisionState.Unspecified,
    };

    public static DomainFamily ToDomain(ProtoFamily family) => family switch
    {
        ProtoFamily.Ipv4 => DomainFamily.IPv4,
        ProtoFamily.Ipv6 => DomainFamily.IPv6,
        _ => throw new ArgumentOutOfRangeException(nameof(family), family, "Unsupported address family."),
    };

    public static ProtoFamily ToProto(DomainFamily family) => family switch
    {
        DomainFamily.IPv4 => ProtoFamily.Ipv4,
        DomainFamily.IPv6 => ProtoFamily.Ipv6,
        _ => ProtoFamily.Unspecified,
    };

    public static DomainFilterChain ToDomain(ProtoFilterChain chain) => chain switch
    {
        ProtoFilterChain.Input => DomainFilterChain.Input,
        ProtoFilterChain.Forward => DomainFilterChain.Forward,
        ProtoFilterChain.Output => DomainFilterChain.Output,
        _ => throw new ArgumentOutOfRangeException(nameof(chain), chain, "Unsupported filter chain."),
    };

    public static ProtoFilterChain ToProto(DomainFilterChain chain) => chain switch
    {
        DomainFilterChain.Input => ProtoFilterChain.Input,
        DomainFilterChain.Forward => ProtoFilterChain.Forward,
        DomainFilterChain.Output => ProtoFilterChain.Output,
        _ => ProtoFilterChain.Unspecified,
    };

    public static DomainStage ToDomain(ProtoStage stage) => stage switch
    {
        ProtoStage.ProtectedControlPlane => DomainStage.ProtectedControlPlane,
        ProtoStage.IncidentPreStateDeny => DomainStage.IncidentPreStateDeny,
        ProtoStage.MandatoryPreStateDeny => DomainStage.MandatoryPreStateDeny,
        ProtoStage.StatePrelude => DomainStage.StatePrelude,
        ProtoStage.CompanyDenyExemptions => DomainStage.CompanyDenyExemptions,
        ProtoStage.CompanyDeny => DomainStage.CompanyDeny,
        ProtoStage.SiteDenyExemptions => DomainStage.SiteDenyExemptions,
        ProtoStage.SiteDeny => DomainStage.SiteDeny,
        ProtoStage.NodeDenyExemptions => DomainStage.NodeDenyExemptions,
        ProtoStage.NodeDeny => DomainStage.NodeDeny,
        ProtoStage.CompanyAllow => DomainStage.CompanyAllow,
        ProtoStage.SiteAllow => DomainStage.SiteAllow,
        ProtoStage.NodeAllow => DomainStage.NodeAllow,
        ProtoStage.DefaultDisposition => DomainStage.DefaultDisposition,
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported pipeline stage."),
    };

    public static ProtoStage ToProto(DomainStage stage) => stage switch
    {
        DomainStage.ProtectedControlPlane => ProtoStage.ProtectedControlPlane,
        DomainStage.IncidentPreStateDeny => ProtoStage.IncidentPreStateDeny,
        DomainStage.MandatoryPreStateDeny => ProtoStage.MandatoryPreStateDeny,
        DomainStage.StatePrelude => ProtoStage.StatePrelude,
        DomainStage.CompanyDenyExemptions => ProtoStage.CompanyDenyExemptions,
        DomainStage.CompanyDeny => ProtoStage.CompanyDeny,
        DomainStage.SiteDenyExemptions => ProtoStage.SiteDenyExemptions,
        DomainStage.SiteDeny => ProtoStage.SiteDeny,
        DomainStage.NodeDenyExemptions => ProtoStage.NodeDenyExemptions,
        DomainStage.NodeDeny => ProtoStage.NodeDeny,
        DomainStage.CompanyAllow => ProtoStage.CompanyAllow,
        DomainStage.SiteAllow => ProtoStage.SiteAllow,
        DomainStage.NodeAllow => ProtoStage.NodeAllow,
        DomainStage.DefaultDisposition => ProtoStage.DefaultDisposition,
        _ => ProtoStage.Unspecified,
    };

    public static DomainRuleEffect ToDomain(ProtoRuleEffect effect) => effect switch
    {
        ProtoRuleEffect.Accept => DomainRuleEffect.Accept,
        ProtoRuleEffect.Drop => DomainRuleEffect.Drop,
        ProtoRuleEffect.Reject => DomainRuleEffect.Reject,
        ProtoRuleEffect.FasttrackAccept => DomainRuleEffect.FasttrackAccept,
        ProtoRuleEffect.ExemptDenyStage => DomainRuleEffect.ExemptDenyStage,
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "Unsupported rule effect."),
    };

    public static ProtoRuleEffect ToProto(DomainRuleEffect effect) => effect switch
    {
        DomainRuleEffect.Accept => ProtoRuleEffect.Accept,
        DomainRuleEffect.Drop => ProtoRuleEffect.Drop,
        DomainRuleEffect.Reject => ProtoRuleEffect.Reject,
        DomainRuleEffect.FasttrackAccept => ProtoRuleEffect.FasttrackAccept,
        DomainRuleEffect.ExemptDenyStage => ProtoRuleEffect.ExemptDenyStage,
        _ => ProtoRuleEffect.Unspecified,
    };

    public static DomainRejectMode ToDomain(ProtoRejectMode mode) => mode switch
    {
        ProtoRejectMode.TcpReset => DomainRejectMode.TcpReset,
        ProtoRejectMode.AdminProhibited => DomainRejectMode.AdminProhibited,
        ProtoRejectMode.PortUnreachable => DomainRejectMode.PortUnreachable,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported reject mode."),
    };

    public static ProtoRejectMode ToProto(DomainRejectMode mode) => mode switch
    {
        DomainRejectMode.TcpReset => ProtoRejectMode.TcpReset,
        DomainRejectMode.AdminProhibited => ProtoRejectMode.AdminProhibited,
        DomainRejectMode.PortUnreachable => ProtoRejectMode.PortUnreachable,
        _ => ProtoRejectMode.Unspecified,
    };

    public static DomainConnectionState ToDomain(ProtoConnectionState state) => state switch
    {
        ProtoConnectionState.New => DomainConnectionState.New,
        ProtoConnectionState.Established => DomainConnectionState.Established,
        ProtoConnectionState.Related => DomainConnectionState.Related,
        ProtoConnectionState.Invalid => DomainConnectionState.Invalid,
        ProtoConnectionState.Untracked => DomainConnectionState.Untracked,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported connection state."),
    };

    public static ProtoConnectionState ToProto(DomainConnectionState state) => state switch
    {
        DomainConnectionState.New => ProtoConnectionState.New,
        DomainConnectionState.Established => ProtoConnectionState.Established,
        DomainConnectionState.Related => ProtoConnectionState.Related,
        DomainConnectionState.Invalid => ProtoConnectionState.Invalid,
        DomainConnectionState.Untracked => ProtoConnectionState.Untracked,
        _ => ProtoConnectionState.Unspecified,
    };

    public static DomainConnectionNatState ToDomain(ProtoConnectionNatState state) => state switch
    {
        ProtoConnectionNatState.Srcnat => DomainConnectionNatState.SrcNat,
        ProtoConnectionNatState.Dstnat => DomainConnectionNatState.DstNat,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported connection NAT state."),
    };

    public static ProtoConnectionNatState ToProto(DomainConnectionNatState state) => state switch
    {
        DomainConnectionNatState.SrcNat => ProtoConnectionNatState.Srcnat,
        DomainConnectionNatState.DstNat => ProtoConnectionNatState.Dstnat,
        _ => ProtoConnectionNatState.Unspecified,
    };

    public static DomainAddressType ToDomain(ProtoAddressType type) => type switch
    {
        ProtoAddressType.Local => DomainAddressType.Local,
        ProtoAddressType.Unicast => DomainAddressType.Unicast,
        ProtoAddressType.Broadcast => DomainAddressType.Broadcast,
        ProtoAddressType.Multicast => DomainAddressType.Multicast,
        ProtoAddressType.Anycast => DomainAddressType.Anycast,
        ProtoAddressType.Blackhole => DomainAddressType.Blackhole,
        ProtoAddressType.Prohibit => DomainAddressType.Prohibit,
        ProtoAddressType.Unreachable => DomainAddressType.Unreachable,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported address type."),
    };

    public static ProtoAddressType ToProto(DomainAddressType type) => type switch
    {
        DomainAddressType.Local => ProtoAddressType.Local,
        DomainAddressType.Unicast => ProtoAddressType.Unicast,
        DomainAddressType.Broadcast => ProtoAddressType.Broadcast,
        DomainAddressType.Multicast => ProtoAddressType.Multicast,
        DomainAddressType.Anycast => ProtoAddressType.Anycast,
        DomainAddressType.Blackhole => ProtoAddressType.Blackhole,
        DomainAddressType.Prohibit => ProtoAddressType.Prohibit,
        DomainAddressType.Unreachable => ProtoAddressType.Unreachable,
        _ => ProtoAddressType.Unspecified,
    };

    public static DomainTcpHeaderBit ToDomain(ProtoTcpHeaderBit bit) => bit switch
    {
        ProtoTcpHeaderBit.Fin => DomainTcpHeaderBit.Fin,
        ProtoTcpHeaderBit.Syn => DomainTcpHeaderBit.Syn,
        ProtoTcpHeaderBit.Rst => DomainTcpHeaderBit.Rst,
        ProtoTcpHeaderBit.Psh => DomainTcpHeaderBit.Psh,
        ProtoTcpHeaderBit.Ack => DomainTcpHeaderBit.Ack,
        ProtoTcpHeaderBit.Urg => DomainTcpHeaderBit.Urg,
        ProtoTcpHeaderBit.Ece => DomainTcpHeaderBit.Ece,
        ProtoTcpHeaderBit.Cwr => DomainTcpHeaderBit.Cwr,
        _ => throw new ArgumentOutOfRangeException(nameof(bit), bit, "Unsupported TCP header bit."),
    };

    public static ProtoTcpHeaderBit ToProto(DomainTcpHeaderBit bit) => bit switch
    {
        DomainTcpHeaderBit.Fin => ProtoTcpHeaderBit.Fin,
        DomainTcpHeaderBit.Syn => ProtoTcpHeaderBit.Syn,
        DomainTcpHeaderBit.Rst => ProtoTcpHeaderBit.Rst,
        DomainTcpHeaderBit.Psh => ProtoTcpHeaderBit.Psh,
        DomainTcpHeaderBit.Ack => ProtoTcpHeaderBit.Ack,
        DomainTcpHeaderBit.Urg => ProtoTcpHeaderBit.Urg,
        DomainTcpHeaderBit.Ece => ProtoTcpHeaderBit.Ece,
        DomainTcpHeaderBit.Cwr => ProtoTcpHeaderBit.Cwr,
        _ => ProtoTcpHeaderBit.Unspecified,
    };

    public static DomainIpsecDirection ToDomain(ProtoIpsecDirection direction) => direction switch
    {
        ProtoIpsecDirection.In => DomainIpsecDirection.In,
        ProtoIpsecDirection.Out => DomainIpsecDirection.Out,
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported IPsec direction."),
    };

    public static ProtoIpsecDirection ToProto(DomainIpsecDirection direction) => direction switch
    {
        DomainIpsecDirection.In => ProtoIpsecDirection.In,
        DomainIpsecDirection.Out => ProtoIpsecDirection.Out,
        _ => ProtoIpsecDirection.Unspecified,
    };

    public static DomainIpsecPolicyKind ToDomain(ProtoIpsecPolicyKind policy) => policy switch
    {
        ProtoIpsecPolicyKind.Ipsec => DomainIpsecPolicyKind.Ipsec,
        ProtoIpsecPolicyKind.None => DomainIpsecPolicyKind.None,
        _ => throw new ArgumentOutOfRangeException(nameof(policy), policy, "Unsupported IPsec policy."),
    };

    public static ProtoIpsecPolicyKind ToProto(DomainIpsecPolicyKind policy) => policy switch
    {
        DomainIpsecPolicyKind.Ipsec => ProtoIpsecPolicyKind.Ipsec,
        DomainIpsecPolicyKind.None => ProtoIpsecPolicyKind.None,
        _ => ProtoIpsecPolicyKind.Unspecified,
    };

    private static Sha256 ToSha256(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != 32)
        {
            throw new InvalidOperationException("SHA-256 digest must be 32 bytes.");
        }

        return new Sha256 { Value = ByteString.CopyFrom(bytes) };
    }

    private static Sha256 HexToSha256(string hex)
    {
        byte[] bytes = Convert.FromHexString(hex);
        return ToSha256(bytes);
    }

    public static PolicyApprovalFindingInput ToInput(PolicyAnalysisFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        return new PolicyApprovalFindingInput
        {
            Code = finding.Code,
            Severity = finding.Severity,
            Message = finding.Message,
            Target = finding.Target,
        };
    }

    public static PolicyApprovalTestInput ToInput(PolicyAnalysisTestResult test)
    {
        ArgumentNullException.ThrowIfNull(test);
        return new PolicyApprovalTestInput
        {
            TestId = ProtoUuid.ToGuid(test.TestId),
            Origin = test.Origin,
            Outcome = test.Outcome,
            Proof = test.Proof,
        };
    }

    public static global::Mfc.Contracts.Mfc.V1.PolicyAnalysisRun ToProto(PolicyAnalysisRunView view)
        => new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            RevisionId = ProtoUuid.FromGuid(view.RevisionId),
            BundleHash = HexToSha256(view.BundleHashHex),
            DependencyFingerprint = HexToSha256(view.DependencyFingerprintHex),
            RiskLevel = view.RiskLevel,
            EffectiveRiskLevel = view.EffectiveRiskLevel,
            EvidenceSignalsPresent = view.EvidenceSignalsPresent,
        };

    public static PolicyApprovalVote ToProto(PolicyApprovalVoteView view)
    {
        PolicyApprovalVote message = new()
        {
            ApprovalId = ProtoUuid.FromGuid(view.ApprovalId),
            RevisionId = ProtoUuid.FromGuid(view.RevisionId),
            RevisionState = ToProto(view.RevisionState),
            CompletesApproval = view.CompletesApproval,
            BundleHash = HexToSha256(view.BundleHashHex),
        };
        message.BindingIds.AddRange(view.BindingIds.Select(ProtoUuid.FromGuid));
        return message;
    }

    public static global::Mfc.Contracts.Mfc.V1.PolicyBinding ToProto(PolicyBindingView view)
    {
        global::Mfc.Contracts.Mfc.V1.PolicyBinding message = new()
        {
            Id = ProtoUuid.FromGuid(view.Id),
            Scope = ToProto(view.Scope),
            PolicyId = ProtoUuid.FromGuid(view.PolicyId),
            DesiredRevisionId = ProtoUuid.FromGuid(view.DesiredRevisionId),
            State = ToProto(view.State),
            RowVersion = view.RowVersion,
            DeploymentStarted = view.DeploymentStarted,
        };
        if (view.ScopeId is Guid scopeId)
        {
            message.ScopeId = ProtoUuid.FromGuid(scopeId);
        }

        if (view.ValidUntilUtc is DateTimeOffset until)
        {
            message.ValidUntil = Mfc.Domain.Policy.ExceptionMetadata.FormatTimestamp(until);
        }

        return message;
    }

    public static ProtoBindingScope ToProto(Mfc.Domain.Policy.PolicyBindingScope scope) => scope switch
    {
        Mfc.Domain.Policy.PolicyBindingScope.Company => ProtoBindingScope.Company,
        Mfc.Domain.Policy.PolicyBindingScope.Site => ProtoBindingScope.Site,
        Mfc.Domain.Policy.PolicyBindingScope.Node => ProtoBindingScope.Node,
        Mfc.Domain.Policy.PolicyBindingScope.Exception => ProtoBindingScope.Exception,
        Mfc.Domain.Policy.PolicyBindingScope.IncidentDenyOverlay => ProtoBindingScope.IncidentDenyOverlay,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unsupported binding scope."),
    };

    public static ProtoBindingState ToProto(Mfc.Domain.Policy.PolicyBindingState state) => state switch
    {
        Mfc.Domain.Policy.PolicyBindingState.Active => ProtoBindingState.Active,
        Mfc.Domain.Policy.PolicyBindingState.Disabled => ProtoBindingState.Disabled,
        Mfc.Domain.Policy.PolicyBindingState.ExpiredPendingReconciliation => ProtoBindingState.ExpiredPendingReconciliation,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported binding state."),
    };
}
