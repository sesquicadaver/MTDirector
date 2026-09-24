using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-337: known-limitations / queue seed locked CTRL-KESTREL-BODY-01 (W7-338) after PLAN-48 inventory.
/// </summary>
public sealed class ProductTrancheSeedW7337LivingSpecTests
{
    [Fact]
    public void Ac1KnownLimitationsAndQueueSeedCtrlKestrelBody01AsNext()
    {
        string root = RepoRoot();
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string plan = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string plan48 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-48-controller-kestrel-request-body-limits.md"));
        string program = File.ReadAllText(Path.Combine(root, "src/Mfc.Controller/Program.cs"));
        string contracts = File.ReadAllText(Path.Combine(root, "src/Mfc.Contracts/GrpcTransportLimits.cs"));

        Assert.Contains("Intentional residual (W7-337 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-BODY-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-338", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-339", limitations, StringComparison.Ordinal);
        Assert.Contains("seeded as **W7-338**", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-337 | [#1080](https://github.com/sesquicadaver/MTDirector/issues/1080) | Seed first PLAN-48 atomic row after inventory → CTRL-KESTREL-BODY-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-338 | [#1082](https://github.com/sesquicadaver/MTDirector/issues/1082) | CTRL-KESTREL-BODY-01 — Align Kestrel MaxRequestBodySize with GrpcTransportLimits (256 MiB) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-339 | [#1084](https://github.com/sesquicadaver/MTDirector/issues/1084) | Seed next after CTRL-KESTREL-BODY-01 (PLAN-48 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-340 | [#1087](https://github.com/sesquicadaver/MTDirector/issues/1087) | PLAN-49 — Inventory Controller/Desktop gRPC HTTP/2 keepalive after Kestrel body limits | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-341 | [#1088](https://github.com/sesquicadaver/MTDirector/issues/1088) | Seed first PLAN-49 atomic row after inventory → CTRL-GRPC-KEEPALIVE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-337", plan, StringComparison.Ordinal);
        Assert.Contains("W7-338", plan, StringComparison.Ordinal);
        Assert.Contains("W7-339", plan, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-BODY-01", plan, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan, StringComparison.Ordinal);

        Assert.Contains("W7-337 (#1080) DONE", plan48, StringComparison.Ordinal);
        Assert.Contains("CTRL-KESTREL-BODY-01", plan48, StringComparison.Ordinal);
        Assert.Contains("W7-338", plan48, StringComparison.Ordinal);
        Assert.Contains("W7-339", plan48, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-419 (#1236)", plan48, StringComparison.Ordinal);

        // BODY-01 shipped after this seed; MaxRequestBodySize now aligned.
        Assert.Contains("ConfigureKestrel", program, StringComparison.Ordinal);
        Assert.Contains("MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", program, StringComparison.Ordinal);
        Assert.Contains("MaxMessageBytes = 256 * 1024 * 1024", contracts, StringComparison.Ordinal);
        Assert.Contains("ConfigureResource", program, StringComparison.Ordinal);
        Assert.Contains("MapHealthChecks", program, StringComparison.Ordinal);
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
