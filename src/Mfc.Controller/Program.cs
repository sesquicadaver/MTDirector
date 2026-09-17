using System.Reflection;
using System.Security.Cryptography.X509Certificates;
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
using Mfc.Application.Topology;
using Mfc.Application.Workflow;
using Mfc.Application.Zones;
using Mfc.Contracts;
using Mfc.Controller.Authorization;
using Mfc.Controller.Configuration;
using Mfc.Controller.Grpc;
using Mfc.Controller.Jobs;
using Mfc.Controller.Security;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Logging;
using Mfc.Infrastructure.RouterOs;
using Mfc.Infrastructure.Security;
using Mfc.RouterOs.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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
        builder.Services
            .AddOptions<Mfc.Infrastructure.Security.TrustedCaStoreOptions>()
            .Bind(builder.Configuration.GetSection(Mfc.Infrastructure.Security.TrustedCaStoreOptions.SectionPath));
        builder.Services.AddMfcSecrets(options.Security.MasterKeyProvider);

        RegisterAuthorization(builder.Services, options, jobOptions, builder.Environment.EnvironmentName);
        builder.Services.AddSingleton<GrpcRequestActorResolver>();
        RegisterInventoryApplication(builder.Services);
        RegisterSnapshotApplication(builder.Services);
        RegisterZoneApplication(builder.Services);
        RegisterPolicyApplication(builder.Services);
        RegisterOnboardingApplication(builder.Services);
        RegisterDeploymentApplication(builder.Services);
        RegisterOperationalJobs(builder.Services, jobOptions);
        builder.Services.AddMfcRouterOs(builder.Configuration);
        builder.Services.TryAddSingleton<Mfc.Application.Abstractions.Onboarding.IOnboardingRuntime, Mfc.Application.Abstractions.Onboarding.NotConfiguredOnboardingRuntime>();
        builder.Services.TryAddSingleton<Mfc.Application.Abstractions.Deployment.IDeploymentRuntime, Mfc.Application.Abstractions.Deployment.NotConfiguredDeploymentRuntime>();
        builder.Services.TryAddSingleton<IWatchdogResidueCleanupPort, NotConfiguredWatchdogResidueCleanupPort>();
        builder.Services.TryAddSingleton<
            Mfc.Application.Abstractions.Integration.IResponseFeedbackDeliveryPort,
            Mfc.Infrastructure.Integration.NotConfiguredResponseFeedbackDeliveryPort>();
        builder.Services.AddSingleton<ValidateDeviceConnectionCoordinator>();
        builder.Services.AddSingleton<Mfc.Application.Abstractions.Inventory.IDeviceReachabilityObservationStore, Mfc.Application.Inventory.InMemoryDeviceReachabilityObservationStore>();
        builder.Services.AddSingleton<CaptureProgressHub>();
        builder.Services.AddSingleton<OnboardingProgressHub>();
        builder.Services.AddSingleton<DeploymentProgressHub>();

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            // CTRL-KESTREL-BODY-01: raise host MaxRequestBodySize to the same finite ceiling as
            // GrpcTransportLimits.MaxMessageBytes (256 MiB). ASP.NET Core default (~30 MiB) would
            // reject large snapshot/diff RPCs before gRPC framing — never unlimited / null.
            kestrel.Limits.MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes;

            // CTRL-GRPC-KEEPALIVE-01: finite HTTP/2 PING (shared with Desktop) so idle Watch
            // streams survive NAT/LB — never TimeSpan.MaxValue / InfiniteTimeSpan defaults.
            kestrel.Limits.Http2.KeepAlivePingDelay = GrpcHttp2KeepAlive.PingDelay;
            kestrel.Limits.Http2.KeepAlivePingTimeout = GrpcHttp2KeepAlive.PingTimeout;

            // CTRL-KESTREL-MINRATE-01: disable MinRequest/ResponseDataRate (null) so quiet
            // Capture/Deployment/Onboarding Watch server-streams are not killed by ASP.NET
            // Core defaults (240 B/s + 5s grace). HTTP/2 PING is framing-layer only and does
            // not count as response-body bytes — never leave the default rates enabled.
            kestrel.Limits.MinRequestBodyDataRate = null;
            kestrel.Limits.MinResponseDataRate = null;

            // HTTPS: ALPN negotiates h2 vs HTTP/1.1 (classic curl probes).
            // Cleartext http://: Http2-only — h2c prior-knowledge for gRPC; Http1AndHttp2 on
            // cleartext rejects HTTP/2 with HTTP_1_1_REQUIRED.
            Uri listenUri = new(options.Grpc.ListenAddress);
            bool listenHttps = string.Equals(listenUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
            HttpProtocols endpointProtocols = listenHttps
                ? HttpProtocols.Http1AndHttp2
                : HttpProtocols.Http2;
            kestrel.ConfigureEndpointDefaults(endpoint =>
            {
                endpoint.Protocols = endpointProtocols;
            });

            ClientCertificateMode clientCertificateMode =
                GrpcClientCertificateModeParser.Parse(options.Grpc.ClientCertificateMode);
            IReadOnlyList<X509Certificate2>? mtlsTrustedRoots = null;
            X509RevocationMode mtlsRevocationMode = X509RevocationMode.Online;
            if (GrpcClientCertificateModeParser.RequestsOrAllowsClientCertificate(clientCertificateMode))
            {
                mtlsTrustedRoots = LoadMtlsTrustedRoots(options.Security.TrustedCa);
                mtlsRevocationMode = TrustedCaRevocationModes.Parse(options.Security.TrustedCa.RevocationMode);
            }

            kestrel.ConfigureHttpsDefaults(https =>
            {
                https.ClientCertificateMode = clientCertificateMode;
                if (mtlsTrustedRoots is not null)
                {
                    IReadOnlyList<X509Certificate2> roots = mtlsTrustedRoots;
                    X509RevocationMode revocation = mtlsRevocationMode;
                    ClientCertificateMode mode = clientCertificateMode;
                    https.ClientCertificateValidation = (certificate, chain, errors) =>
                        TrustedCaClientCertificateValidator.Validate(
                            certificate,
                            chain,
                            errors,
                            mode,
                            roots,
                            revocation);
                }
            });
        });

        builder.WebHost.UseUrls(options.Grpc.ListenAddress);

        // CTRL-GRPC-MSGSIZE-01: finite MaxReceive/Send aligned with RawSnapshotLimits (256 MiB) via shared Contracts constant — never unlimited.
        builder.Services.AddGrpc(options =>
        {
            options.MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes;
            options.MaxSendMessageSize = GrpcTransportLimits.MaxMessageBytes;
        });
        builder.Services.AddGrpcHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("process"), tags: ["live"])
            .AddCheck<DatabaseReadyHealthCheck>("database", tags: ["ready"]);

        // CTRL-HTTP-METRICS-01: opt-in Prometheus scrape (default off / fail-closed — no /metrics).
        MetricsHostOptions metricsOptions = builder.Configuration
            .GetSection($"{ControllerOptions.SectionName}:{MetricsHostOptions.SectionName}")
            .Get<MetricsHostOptions>()
            ?? new MetricsHostOptions();
        string metricsScrapePath = NormalizeMetricsScrapePath(metricsOptions.ScrapePath);

        // CTRL-HTTP-OTEL-TRACE-01: opt-in tracing (default off / fail-closed — no exporters).
        TracingHostOptions tracingOptions = builder.Configuration
            .GetSection($"{ControllerOptions.SectionName}:{TracingHostOptions.SectionName}")
            .Get<TracingHostOptions>()
            ?? new TracingHostOptions();
        string? tracingOtlpEndpoint = string.IsNullOrWhiteSpace(tracingOptions.OtlpEndpoint)
            ? null
            : tracingOptions.OtlpEndpoint.Trim();
        if (tracingOptions.Enabled && tracingOtlpEndpoint is null && !tracingOptions.ConsoleExporter)
        {
            throw new InvalidOperationException(
                "Mfc:Tracing:Enabled=true requires Mfc:Tracing:OtlpEndpoint and/or Mfc:Tracing:ConsoleExporter=true (fail-closed).");
        }

        if (metricsOptions.Enabled || tracingOptions.Enabled)
        {
            // CTRL-HTTP-OTEL-RESOURCE-01: stable service identity via ResourceBuilder/ConfigureResource/AddService when OTel is opted in.
            var otel = builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource =>
                {
                    resource.AddService(
                        serviceName: "Mfc.Controller",
                        serviceVersion: ResolveControllerServiceVersion(),
                        serviceInstanceId: Environment.MachineName);
                });
            if (metricsOptions.Enabled)
            {
                otel.WithMetrics(metrics =>
                {
                    metrics
                        .AddAspNetCoreInstrumentation()
                        .AddRuntimeInstrumentation()
                        .AddPrometheusExporter();
                });
            }

            if (tracingOptions.Enabled)
            {
                otel.WithTracing(tracing =>
                {
                    tracing.AddAspNetCoreInstrumentation();
                    if (tracingOtlpEndpoint is not null)
                    {
                        tracing.AddOtlpExporter(exporter =>
                        {
                            exporter.Endpoint = new Uri(tracingOtlpEndpoint);
                        });
                    }

                    if (tracingOptions.ConsoleExporter)
                    {
                        tracing.AddConsoleExporter();
                    }
                });
            }
        }

        configure?.Invoke(builder);

        WebApplication app = builder.Build();

        ClientCertificateMode pipelineClientCertMode =
            GrpcClientCertificateModeParser.Parse(options.Grpc.ClientCertificateMode);
        if (GrpcClientCertificateModeParser.RequestsOrAllowsClientCertificate(pipelineClientCertMode))
        {
            app.UseMiddleware<MtlsClientCertificatePrincipalMiddleware>();
        }

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live"),
        });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
        });
        if (metricsOptions.Enabled)
        {
            app.MapPrometheusScrapingEndpoint(metricsScrapePath);
        }

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
        app.MapGrpcService<IncidentGrpcService>();
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
            : new AllowListedOperatorAuthorizationBoundary(options.Authorization.Operators);
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
        services.AddScoped<ExpireIncidentDenyOverlayBindingUseCase>();
        services.AddScoped<PlanIncidentDenyOverlayRemovalUseCase>();
        services.AddScoped<ReconcileExpiredIncidentDenyOverlayBindingsJobUseCase>();
        services.AddScoped<EmitResponseFeedbackUseCase>();
        services.AddScoped<ListResponseFeedbackEventsUseCase>();
        services.AddScoped<ReportIncidentDeploymentOutcomeUseCase>();
        services.AddScoped<DetectManagedDriftUseCase>();
        services.AddScoped<GetDriftEventUseCase>();
        services.AddScoped<ListDeviceDriftEventsUseCase>();
        services.AddScoped<ListAuditEventsUseCase>();
        services.AddScoped<RegisterDeviceUseCase>();
        services.AddScoped<UpdateDeviceUseCase>();
        services.AddScoped<UpdateConnectionProfileUseCase>();
        services.AddScoped<DiscoverDeviceUseCase>();
        services.AddScoped<ListNeighborCandidatesUseCase>();
        services.AddScoped<VrrpPairConsistencyLoader>();
        services.AddScoped<ValidateVrrpPairConsistencyUseCase>();
    }

    private static void RegisterSnapshotApplication(IServiceCollection services)
    {
        services.AddScoped<CaptureSnapshotUseCase>();
        services.AddScoped<CaptureNodeSnapshotsUseCase>();
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
        services.AddScoped<ListPoliciesUseCase>();
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
        services.AddScoped<ExpireIncidentDenyOverlayBindingUseCase>();
        services.AddScoped<ValidateRevisionUseCase>();
        services.AddScoped<UpsertAddressObjectUseCase>();
        services.AddScoped<UpsertServiceObjectUseCase>();
        services.AddScoped<ReplaceChainContractsUseCase>();
        services.AddScoped<ReplacePolicyTestsUseCase>();
        services.AddScoped<DiffPolicyRevisionsUseCase>();
        services.AddScoped<IPolicyDependencyFingerprintCalculator, LivePolicyDependencyFingerprintCalculator>();
        services.AddScoped<CompileNodeFilterArtifactsUseCase>();
        services.AddScoped<GetDevicePolicySafetyAnalysisUseCase>();
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
        services.AddScoped<CreateDeploymentPlanFromSealedArtifactsUseCase>();
        services.AddScoped<StartDeploymentUseCase>();
        services.AddScoped<RollbackDeploymentWorkflowUseCase>();
        services.AddScoped<GetDeploymentRecoveryStatusUseCase>();
    }

    public static bool ContainsMigrateOnly(IEnumerable<string> args)
        => args.Any(a => string.Equals(a, MigrateOnlyArgument, StringComparison.OrdinalIgnoreCase));

    public static string[] StripMigrateOnly(string[] args)
        => args.Where(a => !string.Equals(a, MigrateOnlyArgument, StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <summary>
    /// Normalizes scrape path to a rooted absolute path segment (default <c>/metrics</c>).
    /// </summary>
    internal static string NormalizeMetricsScrapePath(string? path)
    {
        string trimmed = string.IsNullOrWhiteSpace(path) ? "/metrics" : path.Trim();
        if (!trimmed.StartsWith('/'))
        {
            trimmed = "/" + trimmed;
        }

        return trimmed;
    }

    /// <summary>
    /// Resolves OTel <c>service.version</c> from assembly informational/file version (CTRL-HTTP-OTEL-RESOURCE-01).
    /// </summary>
    internal static string ResolveControllerServiceVersion()
    {
        Assembly assembly = typeof(Program).Assembly;
        string? informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            // Strip optional SourceRevisionId suffix ("1.2.3+abcdef").
            int plus = informational.IndexOf('+');
            return plus >= 0 ? informational[..plus] : informational;
        }

        string? fileVersion = assembly
            .GetCustomAttribute<AssemblyFileVersionAttribute>()
            ?.Version;
        if (!string.IsNullOrWhiteSpace(fileVersion))
        {
            return fileVersion;
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

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

    /// <summary>
    /// Loads TrustedCa roots for inbound mTLS (W7-04). Fail-closed when profile material is missing.
    /// </summary>
    private static IReadOnlyList<X509Certificate2> LoadMtlsTrustedRoots(TrustedCaHostOptions trustedCa)
    {
        string profileRef = trustedCa.ClientCaProfileRef?.Trim()
            ?? throw new InvalidOperationException("Mfc:Security:TrustedCa:ClientCaProfileRef is required for mTLS.");

        DirectoryRouterOsTrustedCaStore store = new(new TrustedCaStoreOptions
        {
            ProfilesDirectory = trustedCa.ProfilesDirectory,
            RevocationMode = trustedCa.RevocationMode,
            ClientCaProfileRef = profileRef,
        });

        IReadOnlyList<byte[]> der = store.GetCertificateDerBytes(profileRef);
        IReadOnlyList<X509Certificate2> roots = TrustedCaClientCertificateValidator.LoadTrustedRoots(der);
        if (roots.Count == 0)
        {
            throw new InvalidOperationException(
                $"mTLS TrustedCa profile '{profileRef}' has no certificate material under ProfilesDirectory. Fail-closed.");
        }

        return roots;
    }
}
