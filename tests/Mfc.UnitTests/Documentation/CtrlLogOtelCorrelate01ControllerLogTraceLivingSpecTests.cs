using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-LOG-OTEL-CORRELATE-01: Activity TraceId/SpanId on redacted JSON console logs alongside health/metrics/tracing.
/// </summary>
public sealed class CtrlLogOtelCorrelate01ControllerLogTraceLivingSpecTests
{
    [Fact]
    public void Ac1LoggerEnrichesJsonWithActivityTraceIdAndSpanId()
    {
        string root = RepoRoot();
        string logger = File.ReadAllText(Path.Combine(root, "src/Mfc.Infrastructure/Persistence/Logging/RedactingJsonConsoleLoggerProvider.cs"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));

        Assert.Contains("Activity.Current", logger, StringComparison.Ordinal);
        Assert.Contains("traceId", logger, StringComparison.Ordinal);
        Assert.Contains("spanId", logger, StringComparison.Ordinal);
        Assert.Contains("TraceId.ToString()", logger, StringComparison.Ordinal);
        Assert.Contains("SpanId.ToString()", logger, StringComparison.Ordinal);
        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01", logger, StringComparison.Ordinal);

        // Do not regress health / metrics / tracing opt-in.
        Assert.Contains("MapHealthChecks(\"/health/live\"", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/health/ready\"", program, StringComparison.Ordinal);
        Assert.Contains("MapPrometheusScrapingEndpoint", program, StringComparison.Ordinal);
        Assert.Contains("WithTracing", program, StringComparison.Ordinal);
        Assert.Contains("tracingOptions.Enabled", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Ac2DocsDocumentLogTraceCorrelation()
    {
        string root = RepoRoot();
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packagingDoc = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan45 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-45-controller-log-trace-correlation.md"));

        Assert.Contains("traceId", installation, StringComparison.Ordinal);
        Assert.Contains("spanId", installation, StringComparison.Ordinal);
        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01", installation, StringComparison.Ordinal);
        Assert.Contains("/health/live", installation, StringComparison.Ordinal);
        Assert.Contains("Mfc:Tracing:Enabled", installation, StringComparison.Ordinal);

        Assert.Contains("traceId", packagingDoc, StringComparison.Ordinal);
        Assert.Contains("spanId", packagingDoc, StringComparison.Ordinal);
        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01", packagingDoc, StringComparison.Ordinal);

        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01", testing, StringComparison.Ordinal);
        Assert.Contains("CtrlLogOtelCorrelate01ControllerLogTraceLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-326 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-326 | [#1058](https://github.com/sesquicadaver/MTDirector/issues/1058) | CTRL-LOG-OTEL-CORRELATE-01 — Enrich JSON console logs with Activity TraceId/SpanId | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-368 (#1143)", roadmap, StringComparison.Ordinal);
        Assert.Contains("CTRL-LOG-OTEL-CORRELATE-01", plan45, StringComparison.Ordinal);
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
