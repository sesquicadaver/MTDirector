using Grpc.Core;
using Grpc.Net.Client;
using Mfc.Contracts.Mfc.V1;
using Mfc.Desktop.Configuration;

namespace Mfc.Desktop.Services;

/// <summary>Paged SnapshotService client bound to the current controller channel.</summary>
public sealed class GrpcSnapshotViewerClient : ISnapshotViewerClient
{
    private const uint DefaultPageSize = 50;

    private readonly IControllerConnectionService _connection;
    private readonly DesktopOptions _options;

    public GrpcSnapshotViewerClient(IControllerConnectionService connection, DesktopOptions options)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<StartCaptureResponse> StartCaptureAsync(
        Guid deviceId,
        Guid idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        SnapshotService.SnapshotServiceClient client = CreateClient();
        return await client.StartCaptureAsync(
                new StartCaptureRequest
                {
                    DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                    IdempotencyKey = DesktopProtoUuid.FromGuid(idempotencyKey),
                },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async IAsyncEnumerable<CaptureProgress> WatchCaptureAsync(
        Guid operationId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        SnapshotService.SnapshotServiceClient client = CreateClient();
        using AsyncServerStreamingCall<CaptureProgress> call = client.WatchCapture(
            new WatchCaptureRequest { OperationId = DesktopProtoUuid.FromGuid(operationId) },
            ActorHeaders(),
            cancellationToken: cancellationToken);
        await foreach (CaptureProgress progress in call.ResponseStream.ReadAllAsync(cancellationToken)
                           .ConfigureAwait(false))
        {
            yield return progress;
        }
    }

    public async Task<IReadOnlyList<SnapshotSummary>> ListCapturesAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        SnapshotService.SnapshotServiceClient client = CreateClient();
        Metadata headers = ActorHeaders();
        List<SnapshotSummary> all = [];
        string pageToken = string.Empty;
        do
        {
            ListCapturesResponse response = await client.ListCapturesAsync(
                    new ListCapturesRequest
                    {
                        DeviceId = DesktopProtoUuid.FromGuid(deviceId),
                        Page = new PageRequest { PageSize = DefaultPageSize, PageToken = pageToken },
                    },
                    headers,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            all.AddRange(response.Captures);
            pageToken = response.Page?.NextPageToken ?? string.Empty;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return all;
    }

    public async Task<SnapshotSummary> GetSummaryAsync(
        Guid captureId,
        CancellationToken cancellationToken = default)
    {
        SnapshotService.SnapshotServiceClient client = CreateClient();
        return await client.GetSnapshotSummaryAsync(
                new GetSnapshotSummaryRequest { CaptureId = DesktopProtoUuid.FromGuid(captureId) },
                ActorHeaders(),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<SnapshotRecord>> GetAllSectionRecordsAsync(
        Guid captureId,
        string sectionId,
        DiffDomain domain,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        SnapshotService.SnapshotServiceClient client = CreateClient();
        Metadata headers = ActorHeaders();
        List<SnapshotRecord> all = [];
        string pageToken = string.Empty;
        do
        {
            SnapshotSectionPage page = await client.GetSnapshotSectionAsync(
                    new GetSnapshotSectionRequest
                    {
                        CaptureId = DesktopProtoUuid.FromGuid(captureId),
                        SectionId = sectionId.Trim(),
                        Domain = domain,
                        Page = new PageRequest { PageSize = DefaultPageSize, PageToken = pageToken },
                    },
                    headers,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            all.AddRange(page.Records);
            pageToken = page.NextPageToken ?? string.Empty;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return all;
    }

    public async Task<DiffPage> CompareSnapshotsAsync(
        Guid leftCaptureId,
        Guid rightCaptureId,
        CancellationToken cancellationToken = default)
    {
        SnapshotService.SnapshotServiceClient client = CreateClient();
        Metadata headers = ActorHeaders();
        DiffPage aggregate = new();
        string pageToken = string.Empty;
        do
        {
            DiffPage page = await client.CompareSnapshotsAsync(
                    new CompareSnapshotsRequest
                    {
                        LeftCaptureId = DesktopProtoUuid.FromGuid(leftCaptureId),
                        RightCaptureId = DesktopProtoUuid.FromGuid(rightCaptureId),
                        Page = new PageRequest { PageSize = DefaultPageSize, PageToken = pageToken },
                    },
                    headers,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (aggregate.Entries.Count == 0)
            {
                aggregate.Identical = page.Identical;
                aggregate.Warnings.AddRange(page.Warnings);
            }

            aggregate.Entries.AddRange(page.Entries);
            pageToken = page.NextPageToken ?? string.Empty;
        }
        while (!string.IsNullOrEmpty(pageToken));

        return aggregate;
    }

    private SnapshotService.SnapshotServiceClient CreateClient()
    {
        GrpcChannel? channel = _connection.Channel;
        if (channel is null || _connection.State != ControllerConnectionState.Connected)
        {
            throw new InvalidOperationException("Controller channel is not connected.");
        }

        return new SnapshotService.SnapshotServiceClient(channel);
    }

    private Metadata ActorHeaders() => new()
    {
        { "x-mfc-actor", string.IsNullOrWhiteSpace(_options.Actor) ? "desktop" : _options.Actor.Trim() },
    };
}
