using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using Grpc.AspNetCore.Server;
using Grpc.Core;
using Mfc.Application.Common;
using Mfc.Controller.Jobs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Mfc.Controller.Grpc;

/// <summary>
/// Resolves operator actor for gRPC calls (SEC-01 + W7-02).
/// Prefer authenticated TLS/auth principal. Metadata <c>x-mfc-actor</c> is Development-only
/// (lab shortcut); Production requires a principal. Reserved
/// <see cref="OperationalJobsOptions.SystemActor"/> is for in-process jobs only.
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
    /// Resolves actor from authenticated principal when present; otherwise Development metadata path.
    /// Rejects reserved system actor from principal or metadata. Rejects metadata that disagrees with principal.
    /// </summary>
    public string Resolve(
        ServerCallContext context,
        IHostEnvironment environment,
        string developmentFallback = "dev")
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrWhiteSpace(developmentFallback);

        string? metadataActor = ReadMetadataActor(context);
        string? principal = TryResolvePrincipal(context);

        if (!string.IsNullOrWhiteSpace(principal))
        {
            string trimmedPrincipal = principal.Trim();
            RejectIfSystemActor(trimmedPrincipal, "System actor cannot be asserted via authenticated principal.");

            if (!string.IsNullOrWhiteSpace(metadataActor)
                && !string.Equals(metadataActor, trimmedPrincipal, StringComparison.Ordinal))
            {
                throw GrpcApplicationErrorMapper.ToRpcException(
                    ApplicationError.Unauthorized(
                        "x-mfc-actor metadata must match the authenticated principal."));
            }

            return trimmedPrincipal;
        }

        if (!environment.IsDevelopment())
        {
            throw GrpcApplicationErrorMapper.ToRpcException(
                ApplicationError.Unauthorized(
                    "Authenticated principal required for actor binding."));
        }

        if (!string.IsNullOrWhiteSpace(metadataActor))
        {
            RejectIfSystemActor(metadataActor, "System actor cannot be asserted via gRPC metadata.");
            return metadataActor;
        }

        return developmentFallback.Trim();
    }

    private void RejectIfSystemActor(string actor, string detail)
    {
        if (string.Equals(actor, _systemActor, StringComparison.Ordinal))
        {
            throw GrpcApplicationErrorMapper.ToRpcException(ApplicationError.Unauthorized(detail));
        }
    }

    private static string? ReadMetadataActor(ServerCallContext context)
    {
        string? actor = context.RequestHeaders.GetValue(MetadataKey);
        return string.IsNullOrWhiteSpace(actor) ? null : actor.Trim();
    }

    /// <summary>
    /// Principal from ASP.NET identity / client certificate, or gRPC peer identity (mTLS).
    /// </summary>
    internal static string? TryResolvePrincipal(ServerCallContext context)
    {
        HttpContext? http = TryGetHttpContext(context);
        if (http is not null)
        {
            ClaimsPrincipal? user = http.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                if (!string.IsNullOrWhiteSpace(user.Identity.Name))
                {
                    return user.Identity.Name.Trim();
                }

                Claim? nameId = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
                if (nameId is not null && !string.IsNullOrWhiteSpace(nameId.Value))
                {
                    return nameId.Value.Trim();
                }
            }

            X509Certificate2? cert = http.Connection.ClientCertificate;
            if (cert is not null)
            {
                string? cn = cert.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
                if (!string.IsNullOrWhiteSpace(cn))
                {
                    return cn.Trim();
                }
            }
        }

        AuthContext auth = context.AuthContext;
        if (auth.IsPeerAuthenticated)
        {
            foreach (AuthProperty property in auth.PeerIdentity)
            {
                if (!string.IsNullOrWhiteSpace(property.Value))
                {
                    return property.Value.Trim();
                }
            }
        }

        return null;
    }

    private static HttpContext? TryGetHttpContext(ServerCallContext context)
    {
        try
        {
            return context.GetHttpContext();
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
