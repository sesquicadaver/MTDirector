using Mfc.Application.Abstractions.Audit;
using Mfc.Application.Abstractions.ConnectionProfiles;
using Mfc.Application.Abstractions.RouterOs;
using Mfc.Application.Abstractions.Secrets;
using Mfc.Infrastructure.Audit;
using Mfc.Infrastructure.RouterOs;
using Mfc.Infrastructure.Secrets;
using Microsoft.Extensions.DependencyInjection;

namespace Mfc.Infrastructure.Security;

/// <summary>Registers master-key, secret protector, connection profile, and audit services.</summary>
public static class SecurityServiceCollectionExtensions
{
    public static IServiceCollection AddMfcSecrets(
        this IServiceCollection services,
        string masterKeyProviderName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(masterKeyProviderName);

        if (string.Equals(masterKeyProviderName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IMasterKeyProvider, DevelopmentMasterKeyProvider>();
        }
        else if (string.Equals(masterKeyProviderName, "OsKeyStore", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IMasterKeyProvider, EnvironmentMasterKeyProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown Mfc:Security:MasterKeyProvider '{masterKeyProviderName}'. Supported: Development, OsKeyStore.");
        }

        services.AddSingleton<ISecretProtector, AesGcmSecretProtector>();
        services.AddOptions<TrustedCaStoreOptions>();
        // SEC-04: directory-backed store (empty until ProfilesDirectory is configured; fail-closed).
        services.AddSingleton<IRouterOsTrustedCaStore, DirectoryRouterOsTrustedCaStore>();
        services.AddScoped<IRouterOsConnectionMaterializer, EfRouterOsConnectionMaterializer>();
        services.AddScoped<IAuditEventWriter, EfAuditEventWriter>();
        services.AddScoped<IAuditEventReadStore, EfAuditEventReadStore>();
        services.AddScoped<IConnectionProfileService, ConnectionProfileService>();
        return services;
    }
}
