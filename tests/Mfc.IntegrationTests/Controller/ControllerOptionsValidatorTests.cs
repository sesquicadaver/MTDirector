using Mfc.Controller.Configuration;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Mfc.IntegrationTests.Controller;

public sealed class ControllerOptionsValidatorTests
{
    private static ControllerOptions ValidDevelopmentLoopbackHttp()
        => new()
        {
            Grpc = new GrpcHostOptions
            {
                ListenAddress = "http://127.0.0.1:5101",
                ShutdownTimeoutSeconds = 15,
                AllowInsecureLoopback = true,
            },
            Security = new SecurityHostOptions { RequireTls = true },
            Authentication = new AuthenticationHostOptions { AllowDevelopmentAuthentication = true },
        };

    [Fact]
    public void ProductionWithoutTlsIsBlocked()
    {
        ControllerOptions options = new()
        {
            Grpc = new GrpcHostOptions
            {
                ListenAddress = "http://10.0.0.5:5101",
                AllowInsecureLoopback = false,
            },
            Security = new SecurityHostOptions { RequireTls = true },
            Authentication = new AuthenticationHostOptions(),
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ControllerOptionsValidator.Validate(options, Environments.Production));

        Assert.Contains("TLS", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DevelopmentAuthenticationOnNonLoopbackIsBlocked()
    {
        ControllerOptions options = new()
        {
            Grpc = new GrpcHostOptions
            {
                ListenAddress = "https://10.0.0.8:5101",
                AllowInsecureLoopback = false,
            },
            Security = new SecurityHostOptions { RequireTls = true },
            Authentication = new AuthenticationHostOptions { AllowDevelopmentAuthentication = true },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => ControllerOptionsValidator.Validate(options, Environments.Development));

        Assert.Contains("loopback", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidConfigurationFailsValidation()
    {
        ControllerOptions options = new()
        {
            Grpc = new GrpcHostOptions
            {
                ListenAddress = "not-a-uri",
            },
            Security = new SecurityHostOptions(),
            Authentication = new AuthenticationHostOptions(),
        };

        Assert.Throws<InvalidOperationException>(
            () => ControllerOptionsValidator.Validate(options, Environments.Development));
    }

    [Fact]
    public void DevelopmentLoopbackHttpWithExplicitFlagIsAllowed()
    {
        ControllerOptionsValidator.Validate(ValidDevelopmentLoopbackHttp(), Environments.Development);
    }

    [Fact]
    public void LoopbackDetectionRecognizesLocalhostAndIp()
    {
        Assert.True(ControllerOptionsValidator.IsLoopback(new Uri("http://127.0.0.1:1")));
        Assert.True(ControllerOptionsValidator.IsLoopback(new Uri("http://localhost:1")));
        Assert.True(ControllerOptionsValidator.IsLoopback(new Uri("http://[::1]:1")));
        Assert.False(ControllerOptionsValidator.IsLoopback(new Uri("http://192.168.1.10:1")));
    }
}
