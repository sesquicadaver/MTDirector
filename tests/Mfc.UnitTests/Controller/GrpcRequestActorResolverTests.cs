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

/// <summary>SEC-01: system actor must not be assertable via gRPC metadata.</summary>
public sealed class GrpcRequestActorResolverTests
{
    private const string SystemActor = "system:operational-jobs";

    [Fact]
    public void RejectsReservedSystemActorViaMetadata()
    {
        GrpcRequestActorResolver resolver = CreateResolver(SystemActor);
        ServerCallContext context = new TestServerCallContext(
            new Metadata { { GrpcRequestActorResolver.MetadataKey, SystemActor } });

        RpcException ex = Assert.Throws<RpcException>(() =>
            resolver.Resolve(context, new TestHostEnvironment(Environments.Production)));

        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
        Assert.Contains("System actor cannot be asserted", ex.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void AllowsNonSystemActorMetadata()
    {
        GrpcRequestActorResolver resolver = CreateResolver(SystemActor);
        ServerCallContext context = new TestServerCallContext(
            new Metadata { { GrpcRequestActorResolver.MetadataKey, "operator@lab" } });

        string actor = resolver.Resolve(context, new TestHostEnvironment(Environments.Production));
        Assert.Equal("operator@lab", actor);
    }

    [Fact]
    public void TrimsActorAndRejectsSystemActorWithWhitespace()
    {
        GrpcRequestActorResolver resolver = CreateResolver(SystemActor);
        ServerCallContext context = new TestServerCallContext(
            new Metadata { { GrpcRequestActorResolver.MetadataKey, $"  {SystemActor}  " } });

        Assert.Throws<RpcException>(() =>
            resolver.Resolve(context, new TestHostEnvironment(Environments.Development)));
    }

    [Fact]
    public void DevelopmentFallbackWhenMetadataMissing()
    {
        GrpcRequestActorResolver resolver = CreateResolver(SystemActor);
        ServerCallContext context = new TestServerCallContext(new Metadata());

        string actor = resolver.Resolve(
            context,
            new TestHostEnvironment(Environments.Development),
            developmentFallback: "dev");
        Assert.Equal("dev", actor);
    }

    [Fact]
    public void ProductionRequiresMetadata()
    {
        GrpcRequestActorResolver resolver = CreateResolver(SystemActor);
        ServerCallContext context = new TestServerCallContext(new Metadata());

        RpcException ex = Assert.Throws<RpcException>(() =>
            resolver.Resolve(context, new TestHostEnvironment(Environments.Production)));
        Assert.Equal(StatusCode.Unauthenticated, ex.StatusCode);
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

    private static GrpcRequestActorResolver CreateResolver(string systemActor)
        => new(Options.Create(new OperationalJobsOptions { SystemActor = systemActor }));

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

        public TestServerCallContext(Metadata requestHeaders)
        {
            _requestHeaders = requestHeaders;
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

        protected override AuthContext AuthContextCore { get; } = new(null, new Dictionary<string, List<AuthProperty>>());

        protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
            => throw new NotSupportedException();

        protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
            => Task.CompletedTask;
    }
}
