using Mfc.Controller.Configuration;
using Mfc.Infrastructure.Persistence;
using Mfc.Infrastructure.Persistence.Logging;
using Mfc.Infrastructure.Security;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Mfc.Controller;

/// <summary>
/// Composition root: health-only gRPC host with PostgreSQL schema guard (M0-05/M0-07).
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
    /// Builds a configured health-only host. Used by Main and integration tests.
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

        ControllerOptions options = builder.Configuration
            .GetSection(ControllerOptions.SectionName)
            .Get<ControllerOptions>()
            ?? throw new InvalidOperationException("Mfc configuration section is missing.");

        ControllerOptionsValidator.Validate(options, builder.Environment.EnvironmentName);

        builder.Services.Configure<HostOptions>(host =>
        {
            host.ShutdownTimeout = TimeSpan.FromSeconds(options.Grpc.ShutdownTimeoutSeconds);
        });

        builder.Services.AddMfcPersistence(options.Database.ConnectionString);
        builder.Services.AddMfcSecrets(options.Security.MasterKeyProvider);

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

        // No RouterOS client registration in M0.

        configure?.Invoke(builder);

        WebApplication app = builder.Build();
        app.MapGrpcHealthChecksService();
        return app;
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
