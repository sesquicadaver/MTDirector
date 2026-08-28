using Mfc.Application.Abstractions.Deployment;
using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Onboarding;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Deployment;
using Mfc.Application.Snapshots;
using Mfc.RouterOs.Configuration;
using Mfc.RouterOs.Deployment;
using Mfc.RouterOs.Jobs;
using Mfc.RouterOs.Onboarding;
using Mfc.RouterOs.Ports;
using Mfc.RouterOs.Snapshot;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Mfc.RouterOs.DependencyInjection;

/// <summary>
/// Registers RouterOS read-path (P2-06) and write-path (P2-10) services for the Controller composition root.
/// </summary>
public static class RouterOsServiceCollectionExtensions
{
    public const string ConfigurationSectionPath = "Mfc:RouterOs";

    /// <summary>
    /// Registers fail-closed RouterOS stubs by default, or production adapters when
    /// <see cref="RouterOsHostOptions.Enabled"/> / <see cref="RouterOsHostOptions.WriteEnabled"/> are set.
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

        if (options.WriteEnabled)
        {
            RegisterWriteServices(services);
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

    /// <summary>
    /// Registers production write-path adapters (onboarding / deploy / watchdog residue).
    /// Scoped — uses EF connection materializer and inventory stores (P2-10).
    /// </summary>
    public static IServiceCollection AddRouterOsWriteServices(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        RegisterWriteServices(services);
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

    private static void RegisterWriteServices(IServiceCollection services)
    {
        services.AddScoped<IDeploymentArtifactMaterializer, AnchorOnlyDeploymentArtifactMaterializer>();
        services.AddScoped<IRouterOsOnboardingSessionFactory, RouterOsOnboardingSessionFactory>();
        services.AddScoped<IOnboardingRuntime, RouterOsOnboardingRuntime>();
        services.AddScoped<IRouterOsDeploymentSessionFactory, RouterOsDeploymentSessionFactory>();
        services.AddScoped<IDeploymentRuntime, RouterOsDeploymentRuntime>();
        services.AddScoped<IRouterOsWatchdogResidueSessionFactory, RouterOsWatchdogResidueSessionFactory>();
        services.AddScoped<IWatchdogResidueCleanupPort, RouterOsWatchdogResidueCleanupPort>();
    }
}
