using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Controller.Jobs;
using Microsoft.Extensions.Options;

namespace Mfc.Controller.Grpc;

/// <summary>
/// Resolves operator actor from gRPC metadata (SEC-01).
/// Reserved <see cref="OperationalJobsOptions.SystemActor"/> is for in-process jobs only —
/// clients cannot assert it via <c>x-mfc-actor</c>.
/// </summary>
public sealed class GrpcRequestActorResolver
{
    public const string MetadataKey = InventoryGrpcService.ActorMetadataKey;

    private readonly string _systemActor;

    public GrpcRequestActorResolver(IOptions<OperationalJobsOptions> jobOptions)
    {
        ArgumentNullException.ThrowIfNull(jobOptions);
        string configured = jobOptions.Value.SystemActor;
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Mfc:OperationalJobs:SystemActor must be configured.");
        }

        _systemActor = configured.Trim();
    }

    /// <summary>Configured reserved system actor (in-process jobs only).</summary>
    public string SystemActor => _systemActor;

    /// <summary>
    /// Resolves actor from metadata. Rejects reserved system actor.
    /// When metadata is missing and environment is Development, returns <paramref name="developmentFallback"/>.
    /// </summary>
    public string Resolve(
        ServerCallContext context,
        IHostEnvironment environment,
        string developmentFallback = "dev")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(developmentFallback);

        string? actor = context.RequestHeaders.GetValue(MetadataKey);
        if (!string.IsNullOrWhiteSpace(actor))
        {
            string trimmed = actor.Trim();
            if (string.Equals(trimmed, _systemActor, StringComparison.Ordinal))
            {
                throw GrpcApplicationErrorMapper.ToRpcException(
                    ApplicationError.Unauthorized(
                        "System actor cannot be asserted via gRPC metadata."));
            }

            return trimmed;
        }

        if (environment.IsDevelopment())
        {
            return developmentFallback.Trim();
        }

        throw GrpcApplicationErrorMapper.ToRpcException(
            ApplicationError.Unauthorized("Missing x-mfc-actor metadata."));
    }
}
