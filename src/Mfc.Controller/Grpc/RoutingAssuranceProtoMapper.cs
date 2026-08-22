using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Mfc.Application.Models;
using Mfc.Contracts.Mfc.V1;

namespace Mfc.Controller.Grpc;

/// <summary>Maps Application routing assurance views to Contracts proto messages (M7.1-10).</summary>
internal static class RoutingAssuranceProtoMapper
{
    public static RoutingAssuranceStateDetail ToProto(RoutingAssuranceDetailView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        RoutingAssuranceStateDetail message = new()
        {
            DeviceId = ProtoUuid.FromGuid(view.DeviceId),
            RouteExpectationCount = (uint)view.RouteExpectationCount,
            RouteFindingCount = (uint)view.RouteFindingCount,
            ResolutionTraceCount = (uint)view.ResolutionTraceCount,
            ConfigurationTableCount = (uint)view.ConfigurationTableCount,
            ConfigurationRuleCount = (uint)view.ConfigurationRuleCount,
            ConfigurationVrfCount = (uint)view.ConfigurationVrfCount,
            ConfigurationStaticRouteCount = (uint)view.ConfigurationStaticRouteCount,
            ConfigurationFilterRuleCount = (uint)view.ConfigurationFilterRuleCount,
            OperationalRouteCount = (uint)view.OperationalRouteCount,
            OperationalDefaultRouteCount = (uint)view.OperationalDefaultRouteCount,
            UpdatedAt = Timestamp.FromDateTimeOffset(view.UpdatedAtUtc),
            RowVersion = view.RowVersion,
        };

        if (TryHexToSha256(view.ConfigurationHashHex, out Sha256? configurationHash))
        {
            message.ConfigurationHash = configurationHash;
        }

        if (TryHexToSha256(view.OperationalHashHex, out Sha256? operationalHash))
        {
            message.OperationalHash = operationalHash;
        }

        message.Expectations.AddRange(view.Expectations.Select(ToProto));
        message.Findings.AddRange(view.Findings.Select(ToProto));
        message.TraceSummaries.AddRange(view.TraceSummaries.Select(ToProto));
        return message;
    }

    public static RouteExpectation ToProto(RouteExpectationView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        RouteExpectation message = new()
        {
            Family = view.Family,
            DestinationPrefix = view.DestinationPrefix,
            RequireCpuFirewallPath = view.RequireCpuFirewallPath,
            RequireReversePath = view.RequireReversePath,
            ExpectAsymmetricReversePath = view.ExpectAsymmetricReversePath,
            Critical = view.Critical,
        };

        if (view.NodeId is Guid nodeId)
        {
            message.NodeId = ProtoUuid.FromGuid(nodeId);
        }

        if (!string.IsNullOrWhiteSpace(view.SourceZone))
        {
            message.SourceZone = view.SourceZone;
        }

        if (!string.IsNullOrWhiteSpace(view.SourceAddress))
        {
            message.SourceAddress = view.SourceAddress;
        }

        if (!string.IsNullOrWhiteSpace(view.ExpectedVrf))
        {
            message.ExpectedVrf = view.ExpectedVrf;
        }

        if (!string.IsNullOrWhiteSpace(view.ExpectedTable))
        {
            message.ExpectedTable = view.ExpectedTable;
        }

        message.AllowedNextHops.AddRange(view.AllowedNextHops);
        message.AllowedEgressZones.AddRange(view.AllowedEgressZones);
        message.AllowedEgressInterfaces.AddRange(view.AllowedEgressInterfaces);
        message.RequiredRouteTypes.AddRange(view.RequiredRouteTypes);
        message.ForbiddenRouteTypes.AddRange(view.ForbiddenRouteTypes);
        return message;
    }

    public static RouteFinding ToProto(RouteFindingView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        RouteFinding message = new()
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

    public static RouteResolutionTraceSummary ToProto(RouteResolutionTraceSummaryView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        RouteResolutionTraceSummary message = new()
        {
            Family = view.Family,
        };

        if (!string.IsNullOrWhiteSpace(view.DestinationAddress))
        {
            message.DestinationAddress = view.DestinationAddress;
        }

        if (!string.IsNullOrWhiteSpace(view.SourceAddress))
        {
            message.SourceAddress = view.SourceAddress;
        }

        if (!string.IsNullOrWhiteSpace(view.SelectedVrf))
        {
            message.SelectedVrf = view.SelectedVrf;
        }

        if (!string.IsNullOrWhiteSpace(view.SelectedTable))
        {
            message.SelectedTable = view.SelectedTable;
        }

        if (!string.IsNullOrWhiteSpace(view.MatchedPrefix))
        {
            message.MatchedPrefix = view.MatchedPrefix;
        }

        if (!string.IsNullOrWhiteSpace(view.ExecutionPath))
        {
            message.ExecutionPath = view.ExecutionPath;
        }

        if (!string.IsNullOrWhiteSpace(view.Decision))
        {
            message.Decision = view.Decision;
        }

        if (!string.IsNullOrWhiteSpace(view.ReversePathSymmetryResult))
        {
            message.ReversePathSymmetryResult = view.ReversePathSymmetryResult;
        }

        message.NextHopGateways.AddRange(view.NextHopGateways);
        message.EgressInterfaces.AddRange(view.EgressInterfaces);
        message.DriftCodes.AddRange(view.DriftCodes);
        message.LatencyCodes.AddRange(view.LatencyCodes);
        return message;
    }

    private static bool TryHexToSha256(string? hex, out Sha256? sha)
    {
        sha = null;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return false;
        }

        try
        {
            byte[] bytes = Convert.FromHexString(hex);
            if (bytes.Length != 32)
            {
                return false;
            }

            sha = new Sha256 { Value = ByteString.CopyFrom(bytes) };
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
