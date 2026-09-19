using Xunit;

namespace Mfc.UnitTests.Release;

/// <summary>
/// W7-348: PLAN-51 inventory documents sole DESK-GRPC-DEADLINE-01 rank
/// (63 unary Grpc*Client methods lack CallOptions.Deadline; Watch streams excluded)
/// and opens DEADLINE implement after seed.
/// </summary>
public sealed class Plan51DesktopGrpcUnaryDeadlineW7348LivingSpecTests
{
    [Fact]
    public void Ac1Plan51InventoryDocumentsSoleDeskGrpcDeadline01RankAndSeedsImplement()
    {
        string root = RepoRoot();
        string plan51 = File.ReadAllText(Path.Combine(root, "docs/planning/plan-51-desktop-grpc-unary-deadline.md"));
        string limitations = File.ReadAllText(Path.Combine(root, "docs/release/known-limitations.md"));
        string roadmap = File.ReadAllText(Path.Combine(root, "ROADMAP.md"));
        string continuous = File.ReadAllText(Path.Combine(root, "docs/planning/continuous-queue-plan.md"));
        string docsIndex = File.ReadAllText(Path.Combine(root, "docs/README.md"));
        string testing = File.ReadAllText(Path.Combine(root, "docs/development/testing.md"));
        string desktopOptions = File.ReadAllText(Path.Combine(root, "src/Mfc.Desktop/Configuration/DesktopOptions.cs"));
        string servicesDir = Path.Combine(root, "src/Mfc.Desktop/Services");

        Assert.Contains("PLAN-51 — Desktop gRPC unary call deadline / timeout after transport saturates", plan51, StringComparison.Ordinal);
        Assert.Contains("Inventory **DONE**", plan51, StringComparison.Ordinal);
        Assert.Contains("DESK-GRPC-DEADLINE-01", plan51, StringComparison.Ordinal);
        Assert.Contains("9001354c", plan51, StringComparison.Ordinal);
        Assert.Contains("W7-350", plan51, StringComparison.Ordinal);
        Assert.Contains("W7-349", plan51, StringComparison.Ordinal);
        Assert.Contains("W7-348", plan51, StringComparison.Ordinal);
        Assert.Contains("sole rank", plan51, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("63", plan51, StringComparison.Ordinal);
        Assert.Contains("WatchCapture", plan51, StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-366 (#1138)", plan51, StringComparison.Ordinal);

        Assert.Contains("Intentional residual (W7-348 Living Spec lock)", limitations, StringComparison.Ordinal);
        Assert.Contains("DESK-GRPC-DEADLINE-01", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-350", limitations, StringComparison.Ordinal);
        Assert.Contains("W7-349", limitations, StringComparison.Ordinal);
        Assert.Contains("9001354c", limitations, StringComparison.Ordinal);
        Assert.Contains("63", limitations, StringComparison.Ordinal);

        Assert.Contains(
            "W7-348 | [#1103](https://github.com/sesquicadaver/MTDirector/issues/1103) | PLAN-51 — Inventory Desktop gRPC unary call deadline / timeout policy | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-349 | [#1104](https://github.com/sesquicadaver/MTDirector/issues/1104) | Seed first PLAN-51 atomic row after inventory → DESK-GRPC-DEADLINE-01 | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-350 | [#1106](https://github.com/sesquicadaver/MTDirector/issues/1106) | DESK-GRPC-DEADLINE-01 — Desktop unary gRPC CallOptions deadline policy | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains(
            "W7-351 | [#1107](https://github.com/sesquicadaver/MTDirector/issues/1107) | Seed next after DESK-GRPC-DEADLINE-01 (PLAN-51 COMPLETE) | **DONE**",
            roadmap,
            StringComparison.Ordinal);
        Assert.Contains("§3.C NEXT = W7-366 (#1138)", roadmap, StringComparison.Ordinal);

        Assert.Contains("W7-349", continuous, StringComparison.Ordinal);
        Assert.Contains("W7-350", continuous, StringComparison.Ordinal);
        Assert.Contains("PLAN-51", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-51-desktop-grpc-unary-deadline.md", continuous, StringComparison.Ordinal);
        Assert.Contains("plan-51-desktop-grpc-unary-deadline.md", docsIndex, StringComparison.Ordinal);
        Assert.Contains("Plan51DesktopGrpcUnaryDeadlineW7348", testing, StringComparison.Ordinal);

        // DEADLINE-01 shipped: unary helper + option; Watch streams stay unbounded.
        Assert.Contains("UnaryCallTimeoutSeconds", desktopOptions, StringComparison.Ordinal);
        Assert.Contains("= 30", desktopOptions, StringComparison.Ordinal);
        string helper = File.ReadAllText(Path.Combine(servicesDir, "DesktopGrpcUnaryCall.cs"));
        Assert.Contains("deadline: DateTime.UtcNow.AddSeconds(seconds)", helper, StringComparison.Ordinal);
        Assert.Contains("must be greater than 0", helper, StringComparison.Ordinal);

        foreach (string clientPath in Directory.EnumerateFiles(servicesDir, "Grpc*Client.cs"))
        {
            string client = File.ReadAllText(clientPath);
            Assert.Contains("DesktopGrpcUnaryCall.For", client, StringComparison.Ordinal);
            AssertWatchStreamsHaveNoUnaryDeadline(client);
        }

        string connection = File.ReadAllText(Path.Combine(servicesDir, "ControllerConnectionService.cs"));
        Assert.Contains("CancelAfter", connection, StringComparison.Ordinal);
        Assert.Contains("HealthCheckTimeoutSeconds", connection, StringComparison.Ordinal);
        Assert.Contains("MaxReceiveMessageSize = GrpcTransportLimits.MaxMessageBytes", connection, StringComparison.Ordinal);
    }

    private static void AssertWatchStreamsHaveNoUnaryDeadline(string client)
    {
        foreach (string marker in new[] { "client.Watch(", "client.WatchCapture(" })
        {
            int index = 0;
            while (true)
            {
                int start = client.IndexOf(marker, index, StringComparison.Ordinal);
                if (start < 0)
                {
                    break;
                }

                int end = client.IndexOf(';', start);
                Assert.True(end > start);
                string call = client[start..end];
                Assert.Contains("ActorHeaders()", call, StringComparison.Ordinal);
                Assert.DoesNotContain("DesktopGrpcUnaryCall", call, StringComparison.Ordinal);
                index = end + 1;
            }
        }
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
