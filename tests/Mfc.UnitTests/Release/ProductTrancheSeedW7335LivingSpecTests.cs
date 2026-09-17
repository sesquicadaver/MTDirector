using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-335: PLAN-47 COMPLETE; known-limitations / queue seed locked PLAN-48 inventory (W7-336)
/// and follow-up seed W7-337 after CTRL-GRPC-MSGSIZE-01.
/// </summary>
public sealed class ProductTrancheSeedW7335LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedPlan48AfterPlan47Complete()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan47 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-47-controller-grpc-message-size-limits.md"));
        string plan48 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-48-controller-kestrel-request-body-limits.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string contracts = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/GrpcTransportLimits.cs"));

        Assert.Contains("Intentional residual (W7-335 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-47 COMPLETE", limitations, StringComparison.Ordinal);
        Assert.Contains("PLAN-48", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-336", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-337", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-BODY-01", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-336**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-335 | [#1076](https://github.com/sesquicadaver/MTDirector/issues/1076) | Seed next after CTRL-GRPC-MSGSIZE-01 (PLAN-47 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-336 | [#1079](https://github.com/sesquicadaver/MTDirector/issues/1079) | PLAN-48 — Inventory Controller Kestrel request-body / HTTP2 limits after gRPC message-size | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-337 | [#1080](https://github.com/sesquicadaver/MTDirector/issues/1080) | Seed first PLAN-48 atomic row after inventory → CTRL-KESTREL-BODY-01 | **OPEN**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-336 (#1079)", roadmap, StringComparison.Ordinal);

        Assert.Contains("PLAN-47 COMPLETE", plan47, StringComparison.Ordinal);
        Assert.Contains("W7-335 (#1076) DONE", plan47, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-336 (#1079)", plan47, StringComparison.Ordinal);
        Assert.Contains("plan-48-controller-kestrel-request-body-limits.md", plan47, StringComparison.Ordinal);

        Assert.Contains("PLAN-48", plan, StringComparison.Ordinal);
        Assert.Contains("W7-336", plan, StringComparison.Ordinal);
        Assert.Contains("W7-335 DONE", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-336 (#1079)", plan, StringComparison.Ordinal);
        Assert.Contains("plan-48-controller-kestrel-request-body-limits.md", plan, StringComparison.Ordinal);

        Assert.Contains("CTRL-KESTREL-BODY-01", plan48, StringComparison.Ordinal);
        Assert.Contains("Inventory **OPEN**", plan48, StringComparison.Ordinal);
        Assert.Contains("W7-336", plan48, StringComparison.Ordinal);
        Assert.Contains("W7-337", plan48, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-336 (#1079)", plan48, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize", plan48, StringComparison.Ordinal);

        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("MaxMessageBytes = 256 * 1024 * 1024", contracts, StringComparison.Ordinal);
        Assert.Contains("ConfigureKestrel", program, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxRequestBodySize", program, StringComparison.Ordinal);
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
