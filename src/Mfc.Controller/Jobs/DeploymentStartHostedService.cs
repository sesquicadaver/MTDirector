using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Common;
using Mfc.Application.Deployment;
using Mfc.Controller.Grpc;

namespace Mfc.Controller.Jobs;

/// <summary>
/// Drains accepted deployment Starts off the unary RPC thread (AUDIT-RPC-01).
/// </summary>
public sealed partial class DeploymentStartHostedService : BackgroundService
{
    private readonly ChannelDeploymentStartWorkChannel _queue;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DeploymentStartHostedService> _logger;

    public DeploymentStartHostedService(
        ChannelDeploymentStartWorkChannel queue,
        IServiceScopeFactory scopes,
        ILogger<DeploymentStartHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(queue);
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(logger);
        _queue = queue;
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (DeploymentStartWorkItem item in _queue.Reader.ReadAllAsync(stoppingToken)
                           .ConfigureAwait(false))
        {
            try
            {
                await using AsyncServiceScope scope = _scopes.CreateAsyncScope();
                StartDeploymentUseCase start = scope.ServiceProvider.GetRequiredService<StartDeploymentUseCase>();
                await start.ContinueAcceptedAsync(item, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                LogStartFailed(_logger, item.OperationId, ex);
                item.Completion.TrySetResult(
                    ApplicationResults.Fail(ApplicationError.Failed(ex.Message)));
            }
        }
    }

    [LoggerMessage(
        EventId = 5401,
        Level = LogLevel.Error,
        Message = "Background deployment start failed for operation {OperationId}")]
    private static partial void LogStartFailed(ILogger logger, Guid operationId, Exception exception);
}
