using Xunit;

namespace Mfc.UnitTests.Documentation;

/// <summary>
/// CTRL-HTTP-HEALTH-01: HTTP /health/live + /health/ready alongside gRPC health.
/// </summary>
public sealed class CtrlHttpHealth01ControllerHttpProbesLivingSpecTests
{
    [Fact]
    public void Ac1ProgramMapsHttpLiveAndReadyAlongsideGrpcHealth()
    {
        string root = RepoRoot();
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string healthCheck = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/DatabaseReadyHealthCheck.cs"));

        Assert.Contains("MapHealthChecks(\"/health/live\"", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks(\"/health/ready\"", program, StringComparison.Ordinal);
        Assert.Contains("MapGrpcHealthChecksService", program, StringComparison.Ordinal);
        Assert.Contains("HttpProtocols.Http1AndHttp2", program, StringComparison.Ordinal);
        Assert.Contains("HttpProtocols.Http2", program, StringComparison.Ordinal);
        Assert.Contains("listenHttps", program, StringComparison.Ordinal);
        Assert.Contains("DatabaseReadyHealthCheck", program, StringComparison.Ordinal);
        Assert.Contains("tags: [\"live\"]", program, StringComparison.Ordinal);
        Assert.Contains("tags: [\"ready\"]", program, StringComparison.Ordinal);

        Assert.Contains("CanConnectAsync", healthCheck, StringComparison.Ordinal);
        Assert.Contains("Unhealthy", healthCheck, StringComparison.Ordinal);
        Assert.Contains("fail-closed", healthCheck, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ac2DocsDocumentHttpProbePaths()
    {
        string root = RepoRoot();
        string installation = File.ReadAllText(Path.Combine(root, "docs/operations/installation.md"));
        string packagingDoc = File.ReadAllText(Path.Combine(root, "packaging/doc/mfc/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));

        Assert.Contains("/health/live", installation, StringComparison.Ordinal);
        Assert.Contains("/health/ready", installation, StringComparison.Ordinal);
        Assert.Contains("gRPC health", installation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/health/live", packagingDoc, StringComparison.Ordinal);
        Assert.Contains("/health/ready", packagingDoc, StringComparison.Ordinal);

        Assert.Contains("CTRL-HTTP-HEALTH-01", testing, StringComparison.Ordinal);
        Assert.Contains("CtrlHttpHealth01ControllerHttpProbesLivingSpecTests", testing, StringComparison.Ordinal);
        Assert.Contains("Intentional residual (W7-314 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-HTTP-HEALTH-01 DONE", limitations, StringComparison.Ordinal);
        Assert.Contains(
            "W7-314 | [#1034](https://github.com/sesquicadaver/MTDirector/issues/1034) | CTRL-HTTP-HEALTH-01 — HTTP liveness/readiness probes beyond gRPC health | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-345 (#1096)", roadmap, StringComparison.Ordinal);
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
