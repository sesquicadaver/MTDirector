using System.Text;
using Grpc.Core;
using Mfc.Controller.Authorization;
using Mfc.Controller.Grpc;
using Mfc.Controller.Jobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Mfc.UnitTests.Controller;

/// <summary>SEC-01 + W7-02: system actor boundary and principal-bound gRPC actor.</summary>
public sealed class GrpcRequestActorResolverTests
{
    private const string SystemActor = "system:operational-jobs";

    [Fact]
    public void RejectsReservedSystemActorViaMetadata()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(
            new Metadata { { GrpcRequestActorResolver.MetadataKey, SystemActor } });

        RpcException ex = Assert.Throws<RpcException>(() =>
            resolver.Resolve(context, new TestHostEnvironment(Environments.Development)));

        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
        Assert.Contains("System actor cannot be asserted", ex.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void DevelopmentAllowsNonSystemActorMetadata()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(
            new Metadata { { GrpcRequestActorResolver.MetadataKey, "operator@lab" } });

        string actor = resolver.Resolve(context, new TestHostEnvironment(Environments.Development));
        Assert.Equal("operator@lab", actor);
    }

    [Fact]
    public void ProductionRejectsMetadataWithoutPrincipal()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(
            new Metadata { { GrpcRequestActorResolver.MetadataKey, "operator@lab" } });

        RpcException ex = Assert.Throws<RpcException>(() =>
            resolver.Resolve(context, new TestHostEnvironment(Environments.Production)));

        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
        Assert.Contains("Authenticated principal required", ex.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionUsesPeerIdentityPrincipal()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(
            new Metadata(),
            peerIdentity: "desktop-operator");

        string actor = resolver.Resolve(context, new TestHostEnvironment(Environments.Production));
        Assert.Equal("desktop-operator", actor);
    }

    [Fact]
    public void ProductionRejectsMetadataThatDisagreesWithPrincipal()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(
            new Metadata { { GrpcRequestActorResolver.MetadataKey, "spoofed@lab" } },
            peerIdentity: "desktop-operator");

        RpcException ex = Assert.Throws<RpcException>(() =>
            resolver.Resolve(context, new TestHostEnvironment(Environments.Production)));

        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
        Assert.Contains("must match the authenticated principal", ex.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionAllowsMatchingMetadataWithPrincipal()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(
            new Metadata { { GrpcRequestActorResolver.MetadataKey, "desktop-operator" } },
            peerIdentity: "desktop-operator");

        string actor = resolver.Resolve(context, new TestHostEnvironment(Environments.Production));
        Assert.Equal("desktop-operator", actor);
    }

    [Fact]
    public void RejectsSystemActorViaPrincipal()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(
            new Metadata(),
            peerIdentity: SystemActor);

        RpcException ex = Assert.Throws<RpcException>(() =>
            resolver.Resolve(context, new TestHostEnvironment(Environments.Production)));

        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
        Assert.Contains("authenticated principal", ex.Status.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TrimsActorAndRejectsSystemActorWithWhitespace()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(
            new Metadata { { GrpcRequestActorResolver.MetadataKey, $"  {SystemActor}  " } });

        Assert.Throws<RpcException>(() =>
            resolver.Resolve(context, new TestHostEnvironment(Environments.Development)));
    }

    [Fact]
    public void DevelopmentFallbackWhenMetadataMissing()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(new Metadata());

        string actor = resolver.Resolve(
            context,
            new TestHostEnvironment(Environments.Development),
            developmentFallback: "dev");
        Assert.Equal("dev", actor);
    }

    [Fact]
    public void ProductionRequiresPrincipal()
    {
        GrpcRequestActorResolver resolver = CreateResolver();
        ServerCallContext context = new TestServerCallContext(new Metadata());

        RpcException ex = Assert.Throws<RpcException>(() =>
            resolver.Resolve(context, new TestHostEnvironment(Environments.Production)));
        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
        Assert.Contains("Authenticated principal required", ex.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SystemActorBoundaryStillAllowsInProcessJobActor()
    {
        SystemActorAuthorizationBoundary boundary = new(
            new DenyAllAuthorizationBoundary(),
            SystemActor);

        await boundary.EnsureAllowedAsync(SystemActor, "deployment.write");

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            boundary.EnsureAllowedAsync("operator@lab", "deployment.write"));
    }

    private static GrpcRequestActorResolver CreateResolver()
        => new(Options.Create(new OperationalJobsOptions { SystemActor = SystemActor }));

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public TestHostEnvironment(string environmentName)
        {
            EnvironmentName = environmentName;
        }

        public string EnvironmentName { get; set; }

        public string ApplicationName { get; set; } = "test";

        public string ContentRootPath { get; set; } = "/tmp";

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TestServerCallContext : ServerCallContext
    {
        private readonly Metadata _requestHeaders;
        private readonly AuthContext _authContext;

        public TestServerCallContext(Metadata requestHeaders, string? peerIdentity = null)
        {
            _requestHeaders = requestHeaders;
            if (string.IsNullOrWhiteSpace(peerIdentity))
            {
                _authContext = new AuthContext(null, new Dictionary<string, List<AuthProperty>>());
            }
            else
            {
                const string propertyName = "x509_common_name";
                _authContext = new AuthContext(
                    propertyName,
                    new Dictionary<string, List<AuthProperty>>
                    {
                        [propertyName] =
                        [
                            AuthProperty.Create(propertyName, Encoding.UTF8.GetBytes(peerIdentity)),
                        ],
                    });
            }
        }

        protected override string MethodCore => "test";

        protected override string HostCore => "localhost";

        protected override string PeerCore => "peer";

        protected override DateTime DeadlineCore => DateTime.UtcNow.AddMinutes(1);

        protected override Metadata RequestHeadersCore => _requestHeaders;

        protected override CancellationToken CancellationTokenCore => CancellationToken.None;

        protected override Metadata ResponseTrailersCore { get; } = [];

        protected override Status StatusCore { get; set; }

        protected override WriteOptions? WriteOptionsCore { get; set; }

        protected override AuthContext AuthContextCore => _authContext;

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            => Task.CompletedTask;
    }
}
