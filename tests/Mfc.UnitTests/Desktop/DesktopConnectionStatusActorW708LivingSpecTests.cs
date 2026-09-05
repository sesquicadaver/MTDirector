using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>W7-08: Connected status shows resolved gRPC actor (x-mfc-actor).</summary>
public sealed class DesktopConnectionStatusActorW708LivingSpecTests
{
    public DesktopConnectionStatusActorW708LivingSpecTests()
    {
        DesktopGrpcActorResolver.ClearCache();
    }

    [Fact]
    public void Ac1ConnectedStatusIncludesConfiguredActor()
    {
        DesktopOptions options = new()
        {
            Actor = "operator@lab",
            ClientCertificatePath = "",
        };

        string status = DesktopConnectionStatusText.Format(ControllerConnectionState.Connected, options);

        Assert.Equal("Connected · actor: operator@lab", status);
        Assert.Equal("operator@lab", DesktopGrpcActorResolver.Resolve(options));
    }

    [Fact]
    public void Ac1ConnectedStatusIncludesClientCertificateCn()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mfc-w708-{Guid.NewGuid():N}.pfx");
        try
        {
            WritePasswordlessClientPfx(path, "CN=desktop-mtls-actor");

            DesktopOptions options = new()
            {
                Actor = "should-be-ignored",
                ClientCertificatePath = path,
            };

            string status = DesktopConnectionStatusText.Format(ControllerConnectionState.Connected, options);

            Assert.Equal("Connected · actor: desktop-mtls-actor", status);
            Assert.Equal(
                "desktop-mtls-actor",
                DesktopGrpcActorResolver.CreateHeaders(options).GetValue(DesktopGrpcActorResolver.MetadataKey));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Ac1DisconnectedStatusOmitsActor()
    {
        DesktopOptions options = new() { Actor = "operator@lab" };

        Assert.Equal(
            "Disconnected",
            DesktopConnectionStatusText.Format(ControllerConnectionState.Disconnected, options));
        Assert.Equal(
            "Connecting",
            DesktopConnectionStatusText.Format(ControllerConnectionState.Connecting, options));
        Assert.Equal(
            "TlsError",
            DesktopConnectionStatusText.Format(ControllerConnectionState.TlsError, options));
    }

    [Fact]
    public void Ac1ShellUsesFormatterAndStatusBinding()
    {
        string root = FindRepoRoot();
        string shell = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/ViewModels/ShellViewModel.cs"));
        string axaml = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/MainWindow.axaml"));

        Assert.Contains("DesktopConnectionStatusText.Format", shell, StringComparison.Ordinal);
        Assert.Contains("StatusText, StringFormat=Status: {0}", axaml, StringComparison.Ordinal);
    }

    private static void WritePasswordlessClientPfx(string path, string subject)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(30));
        File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx));
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MTDirector.sln"))
                || File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
