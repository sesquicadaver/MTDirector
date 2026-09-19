using System.Text.Json;
using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Contracts.Mfc.V1;
using Mfc.Controller.Grpc;
using Mfc.Domain.Policy;
using Mfc.Infrastructure.Persistence.Logging;
using Microsoft.Extensions.Logging;
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
    [InlineData(PolicyApprovalCodes.Blocker)]
    [InlineData(PolicyApprovalCodes.BindingNotApproved)]
    [InlineData(PolicyApprovalCodes.Stale)]
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

    [Fact]
    public void LogsCodeStatusCorrelationIdAndRetryableOnExistingJsonLogger()
    {
        Guid id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        string line = CaptureFaultLog(
            new ApplicationError("not_found", "missing device"),
            id,
            out RpcException exception);

        using JsonDocument doc = JsonDocument.Parse(line);
        string message = doc.RootElement.GetProperty("message").GetString() ?? string.Empty;
        Assert.Contains("gRPC application fault code=not_found status=NotFound correlation_id=11111111-2222-3333-4444-555555555555 retryable=False", message, StringComparison.Ordinal);
        Assert.Equal("Warning", doc.RootElement.GetProperty("level").GetString());
        Assert.Equal(5301, doc.RootElement.GetProperty("eventId").GetInt32());

        ErrorDetail detail = ErrorDetail.Parser.ParseFrom(
            exception.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey));
        Assert.Equal("not_found", detail.Code);
        Assert.False(detail.Retryable);
        Assert.Equal(id, ProtoUuid.ToGuid(detail.CorrelationId));
    }

    [Fact]
    public void LoggedCorrelationIdMatchesTrailerWhenCallerOmitsIt()
    {
        string line = CaptureFaultLog(
            new ApplicationError("dependency", "upstream down"),
            correlationId: null,
            out RpcException exception);

        ErrorDetail detail = ErrorDetail.Parser.ParseFrom(
            exception.Trailers.GetValueBytes(GrpcApplicationErrorMapper.ErrorDetailMetadataKey));
        string id = ProtoUuid.ToGuid(detail.CorrelationId).ToString("D");
        using JsonDocument doc = JsonDocument.Parse(line);
        string message = doc.RootElement.GetProperty("message").GetString() ?? string.Empty;
        Assert.Contains($"code=dependency status=Unavailable correlation_id={id} retryable=True", message, StringComparison.Ordinal);
    }

    private static string CaptureFaultLog(ApplicationError error, Guid? correlationId, out RpcException exception)
    {
        using StringWriter writer = new();
        TextWriter original = Console.Out;
        Console.SetOut(writer);
        using RedactingJsonConsoleLoggerProvider provider = new();
        using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(provider));
        GrpcApplicationErrorMapper mapper = new(factory.CreateLogger<GrpcApplicationErrorMapper>());
        mapper.BindForStaticCallSites();
        try
        {
            exception = GrpcApplicationErrorMapper.ToRpcException(error, correlationId);
        }
        finally
        {
            mapper.UnbindStaticCallSites();
            Console.SetOut(original);
        }

        string output = writer.ToString().Trim();
        Assert.False(string.IsNullOrWhiteSpace(output));
        return output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[^1];
    }
}
