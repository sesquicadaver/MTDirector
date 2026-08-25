using Mfc.Application.Abstractions.RouterOs;

namespace Mfc.Infrastructure.RouterOs;

/// <summary>Fail-closed CA store until pilot CA profiles are configured (P2-06).</summary>
public sealed class NotConfiguredRouterOsTrustedCaStore : IRouterOsTrustedCaStore
{
    public IReadOnlyList<byte[]> GetCertificateDerBytes(string caProfileRef) => [];
}
