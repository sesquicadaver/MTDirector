using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-ERRDETAIL-LOG-01: Controller JSON logs include the ErrorDetail correlation id
/// Desktop ErrorText already shows. Trailer contract and prior transport locks stay.
/// </summary>
public sealed class CtrlErrdetailLog01ControllerFaultCorrelationLivingSpecTests
{
    [Fact]
    public void Ac1MapperLogsFaultCorrelationOnExistingJsonLoggerWithoutChangingTrailer()
    {
        string root = RepoRoot();
        string mapper = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Grpc/GrpcApplicationErrorMapper.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string fault = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopRpcFaultText.cs"));
        string unary = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Services/DesktopGrpcUnaryCall.cs"));
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string configuration = File.ReadAllText(Path.Combine(root, "docs/operations/controller-configuration.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));

        Assert.Contains("ILogger<GrpcApplicationErrorMapper>", mapper, StringComparison.Ordinal);
        Assert.Contains("mfc-error-detail-bin", mapper, StringComparison.Ordinal);
        Assert.Contains("correlationId ?? Guid.NewGuid()", mapper, StringComparison.Ordinal);
        Assert.Contains(
            "gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}",
            mapper,
            StringComparison.Ordinal);
        Assert.Contains("EventId = 5301", mapper, StringComparison.Ordinal);
        Assert.Contains("BindForStaticCallSites", mapper, StringComparison.Ordinal);

        Assert.Contains("AddSingleton<GrpcApplicationErrorMapper>()", program, StringComparison.Ordinal);
        Assert.Contains("GetRequiredService<GrpcApplicationErrorMapper>().BindForStaticCallSites()", program, StringComparison.Ordinal);
        Assert.Contains("RedactingJsonConsoleLoggerProvider", program, StringComparison.Ordinal);

        Assert.Contains("mfc-error-detail-bin", fault, StringComparison.Ordinal);
        Assert.Contains("correlation", fault, StringComparison.Ordinal);
        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", unary, StringComparison.Ordinal);

        Assert.Contains("CTRL-ERRDETAIL-LOG-01", installation, StringComparison.Ordinal);
        Assert.Contains("correlation_id", installation, StringComparison.Ordinal);
        Assert.Contains("CTRL-ERRDETAIL-LOG-01", configuration, StringComparison.Ordinal);
        Assert.Contains("event **5301**", configuration, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-358 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-ERRDETAIL-LOG-01 DONE", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-358 | [#1122](https://github.com/sesquicadaver/MTDirector/issues/1122) | CTRL-ERRDETAIL-LOG-01 — Log fault code, status, correlation id, and retryable | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-359 | [#1123](https://github.com/sesquicadaver/MTDirector/issues/1123) | Seed next after CTRL-ERRDETAIL-LOG-01 (PLAN-53 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-397 (#1200)", roadmap, StringComparison.Ordinal);
    }

    private static string RepoRoot()
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
