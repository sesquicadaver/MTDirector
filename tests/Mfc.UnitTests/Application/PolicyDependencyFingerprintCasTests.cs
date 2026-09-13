using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Common;
using Mfc.Application.Policies;
using Mfc.Domain.Inventory.Primitives;
using Xunit;

namespace Mfc.UnitTests.Application;

public sealed class PolicyDependencyFingerprintCasTests
{
    [Fact]
    public void EvaluateRejectsClientCasMismatch()
    {
        Hash256 run = H("run");
        Hash256 client = H("client");
        Hash256 server = H("server");
        ApplicationError? error = PolicyDependencyFingerprintCas.Evaluate(
            run,
            client,
            server,
            "STALE",
            "cas mismatch");
        Assert.NotNull(error);
        Assert.Equal("STALE", error!.Code);
        Assert.Equal("cas mismatch", error.Message);
    }

    [Fact]
    public void EvaluateRejectsStaleRunEvenWhenClientMatchesServer()
    {
        Hash256 run = H("run");
        Hash256 server = H("server");
        ApplicationError? error = PolicyDependencyFingerprintCas.Evaluate(
            run,
            server,
            server,
            "STALE",
            "cas mismatch");
        Assert.NotNull(error);
        Assert.Equal("STALE", error!.Code);
        Assert.Contains("stale relative to server-computed", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EvaluateAcceptsFreshRunAndMatchingClient()
    {
        Hash256 digest = H("same");
        Assert.Null(PolicyDependencyFingerprintCas.Evaluate(
            digest,
            digest,
            digest,
            "STALE",
            "cas mismatch"));
        Assert.True(PolicyDependencyFingerprintCas.IsAnalysisCurrent(digest, digest));
    }

    private static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
