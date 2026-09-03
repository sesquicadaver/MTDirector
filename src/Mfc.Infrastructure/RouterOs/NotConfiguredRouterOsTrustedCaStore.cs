using Mfc.Application.Abstractions.RouterOs;

namespace Mfc.Infrastructure.RouterOs;

/// <summary>
/// Explicit empty CA store retained for tests/doubles. Production DI registers
/// <see cref="DirectoryRouterOsTrustedCaStore"/> (SEC-04).
/// </summary>
public sealed class NotConfiguredRouterOsTrustedCaStore : IRouterOsTrustedCaStore
{
    public IReadOnlyList<byte[]> GetCertificateDerBytes(string caProfileRef) => [];
}
