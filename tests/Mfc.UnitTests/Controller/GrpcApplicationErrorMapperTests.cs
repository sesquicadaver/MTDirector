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
}
