using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Mfc.Desktop.Configuration;
using Mfc.Desktop.Services;
using Mfc.Desktop.ViewModels;
using Xunit;

namespace Mfc.UnitTests.Desktop;

/// <summary>DESK-MTLS-01 / W7-101: Desktop mTLS actor status Living Spec depth.</summary>
public sealed class DesktopMtlsActorLivingSpecTests
{
    public DesktopMtlsActorLivingSpecTests()
    {
        DesktopGrpcActorResolver.ClearCache();
    }

    [Fact]
    public void Ac1ResolverExposesResolveHeadersMetadataKeyAndDefaultActor()
    {
        Assert.Equal("x-mfc-actor", DesktopGrpcActorResolver.MetadataKey);
        Assert.Equal("desktop", DesktopGrpcActorResolver.DefaultActor);
        Assert.NotNull(typeof(DesktopGrpcActorResolver).GetMethod(nameof(DesktopGrpcActorResolver.Resolve)));
        Assert.NotNull(typeof(DesktopGrpcActorResolver).GetMethod(nameof(DesktopGrpcActorResolver.CreateHeaders)));
        Assert.NotNull(typeof(DesktopGrpcActorResolver).GetMethod(nameof(DesktopGrpcActorResolver.ClearCache)));
        string source = ReadSource("src/Mfc.Desktop/Services/DesktopGrpcActorResolver.cs");
        Assert.Contains("GetNameInfo(X509NameType.SimpleName", source, StringComparison.Ordinal);
        Assert.Contains("FallbackActor", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2ConfiguredActorIsUsedWhenClientCertificateAbsent()
    {
        DesktopOptions options = new()
        {
            Actor = "operator@lab",
            ClientCertificatePath = "",
        };

        Assert.Equal("operator@lab", DesktopGrpcActorResolver.Resolve(options));
        Assert.Equal(
            "operator@lab",
            DesktopGrpcActorResolver.CreateHeaders(options).GetValue(DesktopGrpcActorResolver.MetadataKey));
    }

    [Fact]
    public void Ac3ClientCertificateCnPreferredOverConfiguredActor()
    {
        string path = Path.Combine(Path.GetTempPath(), $"mfc-desk-mtls-01-{Guid.NewGuid():N}.pfx");
        try
        {
            WritePasswordlessClientPfx(path, "CN=desktop-mtls-desk-01");

            DesktopOptions options = new()
            {
                Actor = "should-be-ignored",
                ClientCertificatePath = path,
            };

            Assert.Equal("desktop-mtls-desk-01", DesktopGrpcActorResolver.Resolve(options));
            Assert.Equal(
                "desktop-mtls-desk-01",
                DesktopGrpcActorResolver.CreateHeaders(options).GetValue(DesktopGrpcActorResolver.MetadataKey));
            Assert.Equal(
                "Connected · actor: desktop-mtls-desk-01",
                DesktopConnectionStatusText.Format(ControllerConnectionState.Connected, options));
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
    public void Ac4ConnectedStatusIncludesActorNonConnectedOmitsActor()
    {
        DesktopOptions options = new()
        {
            Actor = "operator@lab",
            ClientCertificatePath = "",
        };

        Assert.Equal(
            "Connected · actor: operator@lab",
            DesktopConnectionStatusText.Format(ControllerConnectionState.Connected, options));
        Assert.Equal(
            "Disconnected",
            DesktopConnectionStatusText.Format(ControllerConnectionState.Disconnected, options));
        Assert.Equal(
            "AuthenticationFailed",
            DesktopConnectionStatusText.Format(ControllerConnectionState.AuthenticationFailed, options));
        Assert.Equal(
            "TlsError",
            DesktopConnectionStatusText.Format(ControllerConnectionState.TlsError, options));
        Assert.DoesNotContain("actor:", DesktopConnectionStatusText.Format(
            ControllerConnectionState.AuthenticationFailed,
            options), StringComparison.Ordinal);
    }

    [Fact]
    public void Ac5ShellAndMainWindowWireStatusTextThroughFormatter()
    {
        Assert.NotNull(typeof(ShellViewModel).GetProperty(nameof(ShellViewModel.StatusText)));
        string shell = ReadSource("src/Mfc.Desktop/ViewModels/ShellViewModel.cs");
        string axaml = ReadSource("src/Mfc.Desktop/MainWindow.axaml");
        Assert.Contains("DesktopConnectionStatusText.Format", shell, StringComparison.Ordinal);
        Assert.Contains("DesktopGrpcActorResolver", ReadSource("src/Mfc.Desktop/Services/DesktopConnectionStatusText.cs"), StringComparison.Ordinal);
        Assert.Contains("StatusText", axaml, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac6PriorW705AndW708LivingSpecsRemainPresent()
    {
        string root = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopGrpcActorFromCertCnW705LivingSpecTests.cs")));
        Assert.True(File.Exists(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopConnectionStatusActorW708LivingSpecTests.cs")));
        string w705 = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopGrpcActorFromCertCnW705LivingSpecTests.cs"));
        string w708 = File.ReadAllText(Path.Combine(root, "tests/Mfc.UnitTests/Desktop/DesktopConnectionStatusActorW708LivingSpecTests.cs"));
        Assert.Contains("DesktopGrpcActorResolver", w705, StringComparison.Ordinal);
        Assert.Contains("DesktopConnectionStatusText.Format", w708, StringComparison.Ordinal);
        string plan09 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-09-desktop-connection-status-operator-surface.md"));
        Assert.Contains("DESK-MTLS-01", plan09, StringComparison.Ordinal);
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

    private static string ReadSource(string relativePath)
        => File.ReadAllText(Path.Combine(FindRepoRoot(), relativePath));

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "ROADMAP.md")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
