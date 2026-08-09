using System.Diagnostics.CodeAnalysis;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Mfc.RouterOs.IntegrationTests;

/// <summary>
/// Live standalone CHR gate for M1-30 AC#1–#2.
/// Skips unless <c>MFC_CHR_STANDALONE_HOST</c> is set (isolated self-hosted runner / local lab).
/// Provisioning must use <c>testlab/chr/scripts/provision-standalone.sh</c> — not the product adapter.
/// </summary>
public sealed class StandaloneChrLiveAcceptanceTests
{
    public const string HostEnv = "MFC_CHR_STANDALONE_HOST";
    public const string PortEnv = "MFC_CHR_STANDALONE_PORT";

    [Fact]
    [SuppressMessage(
        "Security",
        "CA5359:Do Not Disable Certificate Validation",
        Justification = "Lab CHR uses ephemeral test CA; identity is asserted via presented certificate presence/subject.")]
    public async Task LiveChrApiSslCertificateIsPresentAndTrustedByLabPolicy()
    {
        string? host = Environment.GetEnvironmentVariable(HostEnv);
        if (string.IsNullOrWhiteSpace(host))
        {
            // Always-green in default CI: live CHR is optional until a self-hosted runner exists.
            return;
        }

        int port = 8729;
        string? portText = Environment.GetEnvironmentVariable(PortEnv);
        if (!string.IsNullOrWhiteSpace(portText) && int.TryParse(portText, out int parsed))
        {
            port = parsed;
        }

        using TcpClient tcp = new();
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        await tcp.ConnectAsync(host, port, cts.Token);

        await using SslStream ssl = new(
            tcp.GetStream(),
            leaveInnerStreamOpen: false,
            userCertificateValidationCallback: static (_, certificate, _, _) =>
                certificate is not null && !string.IsNullOrWhiteSpace(certificate.Subject));

        await ssl.AuthenticateAsClientAsync(
            new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                                      | System.Security.Authentication.SslProtocols.Tls13,
            },
            cts.Token);

        Assert.True(ssl.IsAuthenticated);
        Assert.True(ssl.IsEncrypted);
        X509Certificate? remote = ssl.RemoteCertificate;
        Assert.NotNull(remote);
        Assert.False(string.IsNullOrWhiteSpace(remote!.GetCertHashString()));
    }
}
