using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Controller;

public sealed class GrpcApplicationErrorMapperTests
{
    [Fact]
    public void PredicateComplexityIsFailedPreconditionNotRetryable()
    {
        RpcException ex = GrpcApplicationErrorMapper.ToRpcException(
            new ApplicationError(PredicateAlgebraCodes.ComplexityLimit, "too many cubes"));
        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        byte[]? trailer = ex.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey);
        Assert.NotNull(trailer);
        ErrorDetail detail = ErrorDetail.Parser.ParseFrom(trailer);
        Assert.Equal(PredicateAlgebraCodes.ComplexityLimit, detail.Code);
        Assert.False(detail.Retryable);
    }

    [Fact]
    public void RuleUnsatisfiableIsFailedPreconditionNotRetryable()
    {
        RpcException ex = GrpcApplicationErrorMapper.ToRpcException(
            new ApplicationError(PolicyAnalysisCodes.Unsatisfiable, "empty selector"));
        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        byte[]? trailer = ex.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey);
        Assert.NotNull(trailer);
        ErrorDetail detail = ErrorDetail.Parser.ParseFrom(trailer);
        Assert.Equal(PolicyAnalysisCodes.Unsatisfiable, detail.Code);
        Assert.False(detail.Retryable);
    }

    [Theory]
    [InlineData(PolicyAnalysisCodes.ShadowIndeterminate)]
    [InlineData(PolicyAnalysisCodes.EarlierAllowBypassesDeny)]
    [InlineData(PolicyAnalysisCodes.FasttrackOverlap)]
    [InlineData(ActualFilterAnalysisCodes.PreAnchorAcceptBypasses)]
    [InlineData(ActualFilterAnalysisCodes.JumpCycle)]
    [InlineData(ActualFilterAnalysisCodes.AnalysisIndeterminate)]
    [InlineData(ActualFilterAnalysisCodes.PreAnchorIndeterminate)]
    [InlineData(PacketPathAnalysisCodes.BypassesIpFirewall)]
    [InlineData(PacketPathAnalysisCodes.NotProven)]
    [InlineData(ManagementPathAnalysisCodes.GuardMissing)]
    [InlineData(ManagementPathAnalysisCodes.PathIndeterminate)]
    [InlineData(ManagementPathAnalysisCodes.InputBlocked)]
    [InlineData(TopologyDependencyAnalysisCodes.VrrpMemberMissing)]
    [InlineData(TopologyDependencyAnalysisCodes.StrictRpfWithVrrp)]
    [InlineData(TopologyDependencyAnalysisCodes.RawNotrackIntersectsStateful)]
    [InlineData(TopologyDependencyAnalysisCodes.SwitchForwardPolicyUnsupported)]
    [InlineData(FastTrackAnalysisCodes.ContextUnsupported)]
    [InlineData(FastTrackAnalysisCodes.LoggingUnsupported)]
    [InlineData(FastTrackAnalysisCodes.CapabilityUnsupported)]
    [InlineData(PolicyEvidenceAnalysisCodes.SafetyTestFailed)]
    [InlineData(PolicyEvidenceAnalysisCodes.SystemTestDisabled)]
    [InlineData(PolicyEvidenceAnalysisCodes.NodeEffectiveIndeterminate)]
    public void SequenceAndActualFilterBlockersAreFailedPreconditionNotRetryable(string code)
    {
        RpcException ex = GrpcApplicationErrorMapper.ToRpcException(
            new ApplicationError(code, "sequence blocker"));
        Assert.Equal(StatusCode.FailedPrecondition, ex.StatusCode);
        byte[]? trailer = ex.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey);
        Assert.NotNull(trailer);
        ErrorDetail detail = ErrorDetail.Parser.ParseFrom(trailer);
        Assert.Equal(code, detail.Code);
        Assert.False(detail.Retryable);
    }
}
