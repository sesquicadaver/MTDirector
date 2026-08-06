namespace Mfc.Controller;

/// <summary>
/// Composition-root entry point placeholder. Host wiring (health, DI, gRPC) lands in M0-05.
/// </summary>
public static class Program
{
    // Preserve composition-root project references for architecture analysis.
    private static readonly Type ApplicationAnchor = typeof(Application.AssemblyMarker);
    private static readonly Type InfrastructureAnchor = typeof(Infrastructure.AssemblyMarker);
    private static readonly Type RouterOsAnchor = typeof(RouterOs.AssemblyMarker);
    private static readonly Type ContractsAnchor = typeof(Contracts.AssemblyMarker);

    public static void Main(string[] args)
    {
        _ = args;
        _ = ApplicationAnchor;
        _ = InfrastructureAnchor;
        _ = RouterOsAnchor;
        _ = ContractsAnchor;
    }
}
