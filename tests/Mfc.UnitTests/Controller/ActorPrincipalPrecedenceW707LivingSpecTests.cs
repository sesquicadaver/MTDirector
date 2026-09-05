using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Grpc.Core;
using Mfc.Controller.Grpc;
using Mfc.Controller.Security;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>W7-07: principal precedence — HttpContext.User over peer identity.</summary>
public sealed class ActorPrincipalPrecedenceW707LivingSpecTests
{
    [Fact]
    public void Ac1AuthenticatedUserWinsOverDifferentPeerIdentity()
    {
        ClaimsPrincipal user = MtlsClientCertificatePrincipalFactory.TryCreate(CreateSelfSigned("CN=user-principal"))!;
        AuthContext peer = CreatePeerAuth("peer-identity");

        string? actor = GrpcRequestActorResolver.TryResolvePrincipal(user, clientCertificate: null, peer);

        Assert.Equal("user-principal", actor);
    }

    [Fact]
    public void Ac1AuthenticatedUserWinsOverConnectionClientCertificate()
    {
        ClaimsPrincipal user = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "claims-user")],
            authenticationType: "Certificate"));
        using X509Certificate2 cert = CreateSelfSigned("CN=cert-cn");

        string? actor = GrpcRequestActorResolver.TryResolvePrincipal(user, cert, authContext: null);

        Assert.Equal("claims-user", actor);
    }

    [Fact]
    public void Ac1ClientCertificateUsedWhenUserNotAuthenticated()
    {
        ClaimsPrincipal anonymous = new(new ClaimsIdentity()); // not authenticated
        using X509Certificate2 cert = CreateSelfSigned("CN=connection-cert");
        AuthContext peer = CreatePeerAuth("peer-identity");

        string? actor = GrpcRequestActorResolver.TryResolvePrincipal(anonymous, cert, peer);

        Assert.Equal("connection-cert", actor);
    }

    [Fact]
    public void Ac1PeerIdentityUsedWhenUserAndCertAbsent()
    {
        string? actor = GrpcRequestActorResolver.TryResolvePrincipal(
            user: null,
            clientCertificate: null,
            CreatePeerAuth("peer-only"));

        Assert.Equal("peer-only", actor);
    }

    [Fact]
    public void Ac1ReturnsNullWhenNoPrincipalSourcesPresent()
    {
        Assert.Null(GrpcRequestActorResolver.TryResolvePrincipal(null, null, authContext: null));
    }

    private static AuthContext CreatePeerAuth(string peerIdentity)
    {
        const string propertyName = "x509_common_name";
        return new AuthContext(
            propertyName,
            new Dictionary<string, List<AuthProperty>>
            {
                [propertyName] =
                [
                    AuthProperty.Create(propertyName, Encoding.UTF8.GetBytes(peerIdentity)),
                ],
            });
    }

    private static X509Certificate2 CreateSelfSigned(string subject)
    {
        using RSA rsa = RSA.Create(2048);
        CertificateRequest request = new(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }
}
