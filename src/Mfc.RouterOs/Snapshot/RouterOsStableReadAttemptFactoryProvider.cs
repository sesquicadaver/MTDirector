using Mfc.Application.Abstractions.RouterOs;
using Mfc.RouterOs.Transport;

namespace Mfc.RouterOs.Snapshot;

/// <summary>Creates stable-read attempt factories for a RouterOS read target.</summary>
public interface IRouterOsStableReadAttemptFactoryProvider
{
    IStableReadAttemptFactory<RouterOsDiscoveryDataset> Create(RouterOsReadTarget target);
}

/// <summary>Production provider using <see cref="IRouterOsConnectionMaterializer"/> + API-SSL sessions.</summary>
public sealed class MaterializingRouterOsStableReadAttemptFactoryProvider : IRouterOsStableReadAttemptFactoryProvider
{
    private readonly IRouterOsConnectionMaterializer _materializer;

    public MaterializingRouterOsStableReadAttemptFactoryProvider(IRouterOsConnectionMaterializer materializer)
    {
        ArgumentNullException.ThrowIfNull(materializer);
        _materializer = materializer;
    }

    public IStableReadAttemptFactory<RouterOsDiscoveryDataset> Create(RouterOsReadTarget target)
        => new RouterOsStableReadAttemptFactory(_materializer, target);
}
