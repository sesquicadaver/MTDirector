using Mfc.Application.Abstractions.Authorization;
using Mfc.Application.Abstractions.Jobs;
using Mfc.Application.Abstractions.Persistence;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Abstractions.Time;
using Mfc.Application.Audit;
using Mfc.Application.Deployment;
using Mfc.Application.Drift;
using Mfc.Application.Endpoint;
using Mfc.Application.Incident;
using Mfc.Application.Inventory;
using Mfc.Application.Jobs;
using Mfc.Application.Onboarding;
using Mfc.Application.Policies;
using Mfc.Application.Routing;
using Mfc.Application.Snapshots;
using Mfc.Application.Workflow;
using Mfc.Application.Zones;
using Mfc.Controller.Authorization;
using Mfc.Controller.Configuration;
using Mfc.Controller.Grpc;
using Mfc.Controller.Jobs;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Logging;
using Mfc.Infrastructure.Security;
using Mfc.RouterOs.Ports;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Mfc.Controller;

/// <summary>
/// Composition root: health + inventory/snapshot/zone/policy/onboarding/deployment/drift/audit gRPC host with PostgreSQL schema guard
/// (M0-05/M0-07, M1-25/M1-26, M2-05/M2-06, M4-12, M6-04) + bounded operational jobs (M6-03).
/// </summary>
public static class Program
{
    public const string MigrateOnlyArgument = "--migrate-only";

    // Preserve composition-root project references for architecture analysis.
    private static readonly Type ApplicationAnchor = typeof(Application.AssemblyMarker);
    private static readonly Type InfrastructureAnchor = typeof(Infrastructure.AssemblyMarker);
    private static readonly Type RouterOsAnchor = typeof(RouterOs.AssemblyMarker);
    private static readonly Type ContractsAnchor = typeof(Contracts.AssemblyMarker);

    public static async Task<int> Main(string[] args)
    {
        _ = ApplicationAnchor;
        _ = InfrastructureAnchor;
        _ = RouterOsAnchor;
        _ = ContractsAnchor;

        try
        {
            bool migrateOnly = ContainsMigrateOnly(args);
            string[] hostArgs = StripMigrateOnly(args);

            await using WebApplication app = BuildHost(hostArgs);

            if (migrateOnly)
            {
                await app.Services.MigrateAsync().ConfigureAwait(false);
                await Console.Out.WriteLineAsync("Database migrations applied successfully.");
                return 0;
            }

            await app.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            string safeMessage = RedactingJsonConsoleLoggerProvider.RedactForTests(ex.Message);
            await Console.Error.WriteLineAsync($"Controller startup failed: {safeMessage}");
            return 1;
        }
    }

    /// <summary>
    /// Builds a configured Controller host. Used by Main and integration tests.
    /// Tests may replace <see cref="IRouterOsReadPort"/> (and other services) via <paramref name="configure"/>.
    /// </summary>
    public static WebApplication BuildHost(string[] args, Action<WebApplicationBuilder>? configure = null)
    {
        string environmentName = ResolveEnvironmentName(args);

        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            EnvironmentName = environmentName,
        });

        builder.Configuration.AddEnvironmentVariables(prefix: "MFC__");

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new RedactingJsonConsoleLoggerProvider());
        builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

        builder.Services
            .AddOptions<ControllerOptions>()
            .Bind(builder.Configuration.GetSection(ControllerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services
            .AddOptions<OperationalJobsOptions>()
            .Bind(builder.Configuration.GetSection($"{ControllerOptions.SectionName}:{OperationalJobsOptions.SectionName}"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        ControllerOptions options = builder.Configuration
            .GetSection(ControllerOptions.SectionName)
            .Get<ControllerOptions>()
            ?? throw new InvalidOperationException("Mfc configuration section is missing.");

        OperationalJobsOptions jobOptions = builder.Configuration
            .GetSection($"{ControllerOptions.SectionName}:{OperationalJobsOptions.SectionName}")
            .Get<OperationalJobsOptions>()
            ?? new OperationalJobsOptions();

        ControllerOptionsValidator.Validate(options, builder.Environment.EnvironmentName);

        builder.Services.Configure<HostOptions>(host =>
        {
            host.ShutdownTimeout = TimeSpan.FromSeconds(options.Grpc.ShutdownTimeoutSeconds);
        });

        builder.Services.AddMfcPersistence(options.Database.ConnectionString);
        builder.Services.AddMfcSecrets(options.Security.MasterKeyProvider);

        RegisterAuthorization(builder.Services, options, jobOptions, builder.Environment.EnvironmentName);
        RegisterInventoryApplication(builder.Services);
        RegisterSnapshotApplication(builder.Services);
        RegisterZoneApplication(builder.Services);
        RegisterPolicyApplication(builder.Services);
        RegisterOnboardingApplication(builder.Services);
        RegisterDeploymentApplication(builder.Services);
        RegisterOperationalJobs(builder.Services, jobOptions);
        builder.Services.TryAddSingleton<IRouterOsReadPort, ProbeOnlyRouterOsReadPort>();
        builder.Services.TryAddSingleton<ISnapshotCapturePort, NotConfiguredSnapshotCapturePort>();
        builder.Services.TryAddSingleton<Mfc.Application.Abstractions.Onboarding.IOnboardingRuntime, Mfc.Application.Abstractions.Onboarding.NotConfiguredOnboardingRuntime>();
        builder.Services.TryAddSingleton<Mfc.Application.Abstractions.Deployment.IDeploymentRuntime, Mfc.Application.Abstractions.Deployment.NotConfiguredDeploymentRuntime>();
        builder.Services.TryAddSingleton<IWatchdogResidueCleanupPort, NotConfiguredWatchdogResidueCleanupPort>();
        builder.Services.AddSingleton<ValidateDeviceConnectionCoordinator>();
        builder.Services.AddSingleton<CaptureProgressHub>();
        builder.Services.AddSingleton<OnboardingProgressHub>();
        builder.Services.AddSingleton<DeploymentProgressHub>();

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.ConfigureEndpointDefaults(endpoint =>
            {
                endpoint.Protocols = HttpProtocols.Http2;
            });
        });

        builder.WebHost.UseUrls(options.Grpc.ListenAddress);

        builder.Services.AddGrpc();
        builder.Services.AddGrpcHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("process"), tags: ["live"]);

        configure?.Invoke(builder);

        WebApplication app = builder.Build();
        app.MapGrpcHealthChecksService();
        app.MapGrpcService<InventoryGrpcService>();
        app.MapGrpcService<SnapshotGrpcService>();
        app.MapGrpcService<ZoneGrpcService>();
        app.MapGrpcService<PolicyGrpcService>();
        app.MapGrpcService<OnboardingGrpcService>();
        app.MapGrpcService<DeploymentGrpcService>();
        app.MapGrpcService<DriftGrpcService>();
        app.MapGrpcService<AuditGrpcService>();
        app.MapGrpcService<RoutingAssuranceGrpcService>();
        return app;
    }

    private static void RegisterAuthorization(
        IServiceCollection services,
        ControllerOptions options,
        OperationalJobsOptions jobOptions,
        string environmentName)
    {
        bool isDevelopment = string.Equals(
            environmentName,
            Environments.Development,
            StringComparison.OrdinalIgnoreCase);
        IAuthorizationBoundary inner = isDevelopment && options.Authentication.AllowDevelopmentAuthentication
            ? new AllowAllAuthorizationBoundary()
            : new DenyAllAuthorizationBoundary();
        services.AddSingleton<IAuthorizationBoundary>(
            new SystemActorAuthorizationBoundary(inner, jobOptions.SystemActor));
    }

    private static void RegisterOperationalJobs(IServiceCollection services, OperationalJobsOptions jobOptions)
    {
        services.AddSingleton(OperationalJobQueues.Create(jobOptions.MaxQueueDepth));
        services.AddSingleton<OperationalJobTickPlanner>();
        services.AddSingleton<OperationalJobExecutor>();
        services.AddScoped<RecoverNonterminalOperationsJobUseCase>();
        services.AddScoped<PollManagedDriftJobUseCase>();
        services.AddScoped<ReconcileExpiredExceptionBindingsJobUseCase>();
        services.AddScoped<HeartbeatDeploymentLocksJobUseCase>();
        services.AddScoped<CleanupDisabledWatchdogResidueJobUseCase>();
        // Hosted scheduler is opt-in via Mfc:OperationalJobs:Enabled (false in IntegrationTests).
        if (jobOptions.Enabled)
        {
            services.AddHostedService<OperationalJobSchedulerHostedService>();
        }
    }

    private static void RegisterInventoryApplication(IServiceCollection services)
    {
        services.AddScoped<ListSitesUseCase>();
        services.AddScoped<ListNodesUseCase>();
        services.AddScoped<CreateSiteUseCase>();
        services.AddScoped<CreateNodeUseCase>();
        services.AddScoped<GetNodeUseCase>();
        services.AddScoped<ProjectNodeWorkflowUseCase>();
        services.AddScoped<UpsertDeviceHashStateUseCase>();
        services.AddScoped<GetDeviceHashStateUseCase>();
        services.AddScoped<UpsertRoutingAssuranceStateUseCase>();
        services.AddScoped<GetRoutingAssuranceStateUseCase>();
        services.AddScoped<OpenEndpointPresenceUseCase>();
        services.AddScoped<GetEndpointRoutingContextUseCase>();
        services.AddScoped<ResolveEndpointAttributionUseCase>();
        services.AddScoped<IngestIncidentSignalUseCase>();
        services.AddScoped<ResolveActiveStateIntervalUseCase>();
        services.AddScoped<ResolveIncidentSessionContextUseCase>();
        services.AddScoped<CorrelateSensorObservationUseCase>();
        services.AddScoped<EvaluateResponseAssessmentQualityUseCase>();
        services.AddScoped<BindIncidentResponseAssessmentUseCase>();
        services.AddScoped<ValidateIncidentDenyOverlayUseCase>();
        services.AddScoped<AssessResponseIntentFeasibilityUseCase>();
        services.AddScoped<DeployIncidentDenyOverlayUseCase>();
        services.AddScoped<DetectManagedDriftUseCase>();
        services.AddScoped<GetDriftEventUseCase>();
        services.AddScoped<ListDeviceDriftEventsUseCase>();
        services.AddScoped<ListAuditEventsUseCase>();
        services.AddScoped<RegisterDeviceUseCase>();
        services.AddScoped<UpdateDeviceUseCase>();
        services.AddScoped<UpdateConnectionProfileUseCase>();
        services.AddScoped<DiscoverDeviceUseCase>();
    }

    private static void RegisterSnapshotApplication(IServiceCollection services)
    {
        services.AddScoped<CaptureSnapshotUseCase>();
        services.AddScoped<ListSnapshotsUseCase>();
        services.AddScoped<GetSnapshotUseCase>();
        services.AddScoped<GetSnapshotSectionUseCase>();
        services.AddScoped<CompareSnapshotsUseCase>();
        services.AddScoped<GetRawSnapshotPayloadUseCase>();
    }

    private static void RegisterZoneApplication(IServiceCollection services)
    {
        services.AddScoped<IZoneResolveObservationSource, SnapshotZoneResolveObservationSource>();
        services.AddScoped<CreateZoneDefinitionUseCase>();
        services.AddScoped<UpdateZoneDefinitionUseCase>();
        services.AddScoped<ListZoneDefinitionsUseCase>();
        services.AddScoped<DeleteZoneDefinitionUseCase>();
        services.AddScoped<UpsertNodeZoneBindingUseCase>();
        services.AddScoped<DeleteNodeZoneBindingUseCase>();
        services.AddScoped<ListNodeZoneBindingsUseCase>();
        services.AddScoped<ResolveZonesForDeviceUseCase>();
        services.AddScoped<ResolveZonesForNodeUseCase>();
    }

    private static void RegisterPolicyApplication(IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<CreateDraftPolicyUseCase>();
        services.AddScoped<GetPolicyRevisionUseCase>();
        services.AddScoped<ListRulesUseCase>();
        services.AddScoped<GetRuleUseCase>();
        services.AddScoped<AddRuleUseCase>();
        services.AddScoped<UpdateRuleUseCase>();
        services.AddScoped<DeleteRuleUseCase>();
        services.AddScoped<ReorderRulesUseCase>();
        services.AddScoped<ComposeEffectivePolicyUseCase>();
        services.AddScoped<UpdateExceptionMetadataUseCase>();
        services.AddScoped<RecordAnalysisRunUseCase>();
        services.AddScoped<AcknowledgeWarningUseCase>();
        services.AddScoped<SubmitRevisionForReviewUseCase>();
        services.AddScoped<ApproveRevisionUseCase>();
        services.AddScoped<ActivateDesiredBindingUseCase>();
        services.AddScoped<ExpireExceptionBindingUseCase>();
        services.AddScoped<ValidateRevisionUseCase>();
        services.AddScoped<UpsertAddressObjectUseCase>();
        services.AddScoped<UpsertServiceObjectUseCase>();
        services.AddScoped<ReplaceChainContractsUseCase>();
        services.AddScoped<ReplacePolicyTestsUseCase>();
        services.AddScoped<DiffPolicyRevisionsUseCase>();
        services.AddScoped<CompileNodeFilterArtifactsUseCase>();
    }

    private static void RegisterOnboardingApplication(IServiceCollection services)
    {
        services.AddScoped<ValidateOnboardingPrerequisitesWorkflowUseCase>();
        services.AddScoped<CreateOnboardingPlanUseCase>();
        services.AddScoped<StartOnboardingUseCase>();
        services.AddScoped<RollbackOnboardingWorkflowUseCase>();
        services.AddScoped<GetOnboardingRecoveryStatusUseCase>();
    }

    private static void RegisterDeploymentApplication(IServiceCollection services)
    {
        services.AddScoped<CreateDeploymentPlanUseCase>();
        services.AddScoped<StartDeploymentUseCase>();
        services.AddScoped<RollbackDeploymentWorkflowUseCase>();
        services.AddScoped<GetDeploymentRecoveryStatusUseCase>();
    }

    public static bool ContainsMigrateOnly(IEnumerable<string> args)
        => args.Any(a => string.Equals(a, MigrateOnlyArgument, StringComparison.OrdinalIgnoreCase));

    public static string[] StripMigrateOnly(string[] args)
        => args.Where(a => !string.Equals(a, MigrateOnlyArgument, StringComparison.OrdinalIgnoreCase)).ToArray();

    private static string ResolveEnvironmentName(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "--environment", StringComparison.OrdinalIgnoreCase)
                && i + 1 < args.Length)
            {
                return args[i + 1];
            }

            const string prefix = "--environment=";
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return arg[prefix.Length..];
            }
        }

        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environments.Production;
    }
}
