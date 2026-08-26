using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Snapshots;
using Mfc.RouterOs.Configuration;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Snapshot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mfc.RouterOs.DependencyInjection;

/// <summary>Registers RouterOS read-path services for the Controller composition root (P2-06).</summary>
public static class RouterOsServiceCollectionExtensions
{
    public const string ConfigurationSectionPath = "Mfc:RouterOs";

    /// <summary>
    /// Registers fail-closed RouterOS stubs by default, or production read/capture ports when
    /// <see cref="RouterOsHostOptions.Enabled"/> is <see langword="true"/>.
    /// </summary>
    public static IServiceCollection AddMfcRouterOs(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        IConfigurationSection section = configuration.GetSection(ConfigurationSectionPath);
        services.Configure<RouterOsHostOptions>(section);
        RouterOsHostOptions options = section.Get<RouterOsHostOptions>() ?? new RouterOsHostOptions();

        if (options.Enabled)
        {
            RegisterProductionServices(services);
        }
        else
        {
            services.TryAddSingleton<IRouterOsReadPort, ProbeOnlyRouterOsReadPort>();
            services.TryAddSingleton<ISnapshotCapturePort, NotConfiguredSnapshotCapturePort>();
        }

        return services;
    }

    /// <summary>Registers production RouterOS read/capture adapters (scoped — uses EF materializer).</summary>
    public static IServiceCollection AddRouterOsProductionServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        RegisterProductionServices(services);
        return services;
    }

    private static void RegisterProductionServices(IServiceCollection services)
    {
        services.AddSingleton<StableReadCoordinator>();
        services.AddScoped<IRouterOsStableReadAttemptFactoryProvider, MaterializingRouterOsStableReadAttemptFactoryProvider>();
        services.AddScoped<IStableReadCoordinatorPort, RouterOsStableReadCoordinatorPort>();
        services.AddScoped<IRouterOsReadPort, RouterOsReadPort>();
        services.AddScoped<ISnapshotCapturePort, RouterOsSnapshotCapturePort>();
        services.AddScoped<CoordinateStableReadUseCase>();
    }
}
