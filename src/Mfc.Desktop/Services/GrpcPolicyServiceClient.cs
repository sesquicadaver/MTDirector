using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>gRPC PolicyService client bound to the current controller channel.</summary>
public sealed class GrpcPolicyServiceClient : IPolicyServiceClient
{
    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcPolicyServiceClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<ListRulesResponse> ListRulesAsync(
        Guid revisionId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.ListRulesAsync(
                new ListRulesRequest
                {
                    RevisionId = DesktopProtoUuid.FromGuid(revisionId),
                    ActiveOnly = activeOnly,
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<PolicyRevision> GetPolicyRevisionAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default)
    {
        PolicyService.PolicyServiceClient client = CreateClient();
        return await client.GetPolicyRevisionAsync(
                new GetPolicyRevisionRequest { RevisionId = DesktopProtoUuid.FromGuid(revisionId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private PolicyService.PolicyServiceClient CreateClient()
    {
        GrpcChannel channel = _connection.Channel
            ?? throw new InvalidOperationException("Controller is not connected.");
        return new PolicyService.PolicyServiceClient(channel);
    }

    private Metadata ActorHeaders()
        => new() { { "x-mfc-actor", string.IsNullOrWhiteSpace(_options.Actor) ? "desktop" : _options.Actor } };
}
