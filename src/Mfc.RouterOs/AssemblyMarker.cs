namespace Mfc.RouterOs;

/// <summary>
/// Assembly marker. Read adapter through discovery, capability, topology, and path class (M1-06…M1-17, N1-01…03).
/// Generic <c>Mfc.RouterOs.Write</c> is absent; onboarding uses closed <see cref="Onboarding.OnboardingBootstrapWriter"/>.
/// </summary>
public static class AssemblyMarker
{
    /// <summary>Preserves Application/Domain project references for boundary analysis.</summary>
    public static Type ApplicationDependencyAnchor { get; } = typeof(Application.AssemblyMarker);

    public static Type DomainDependencyAnchor { get; } = typeof(Domain.AssemblyMarker);

    /// <summary>Roots the word-length codec for architecture and smoke scans.</summary>
    public static Type WordLengthCodecAnchor { get; } = typeof(Protocol.ApiWordLengthCodec);

    /// <summary>Roots the sentence parser for architecture and smoke scans.</summary>
    public static Type SentenceParserAnchor { get; } = typeof(Protocol.ApiSentenceParser);

    /// <summary>Roots the tagged session for architecture and smoke scans.</summary>
    public static Type SessionAnchor { get; } = typeof(Session.RosSession);

    /// <summary>Roots the authenticated API-SSL connection for architecture scans.</summary>
    public static Type ApiSslConnectionAnchor { get; } = typeof(Transport.AuthenticatedRosConnection);

    /// <summary>Roots the allowlisted read executor for architecture scans.</summary>
    public static Type ReadCommandExecutorAnchor { get; } = typeof(Commands.RosReadCommandExecutor);

    /// <summary>Roots system/service discovery for architecture scans.</summary>
    public static Type SystemServiceDiscoveryAnchor { get; } = typeof(Discovery.SystemServiceDiscovery);

    /// <summary>Roots interface/address discovery for architecture scans.</summary>
    public static Type InterfaceAddressDiscoveryAnchor { get; } = typeof(Discovery.InterfaceAddressDiscovery);

    /// <summary>Roots firewall filter discovery for architecture scans.</summary>
    public static Type FirewallFilterDiscoveryAnchor { get; } = typeof(Discovery.FirewallFilterDiscovery);

    /// <summary>Roots routing/firewall-dependency discovery for architecture scans.</summary>
    public static Type RoutingDependencyDiscoveryAnchor { get; } = typeof(Discovery.RoutingDependencyDiscovery);

    /// <summary>Roots VRRP discovery for architecture scans.</summary>
    public static Type VrrpDiscoveryAnchor { get; } = typeof(Discovery.VrrpDiscovery);

    /// <summary>Roots bridge/VLAN/switch discovery for architecture scans.</summary>
    public static Type BridgeSwitchDiscoveryAnchor { get; } = typeof(Discovery.BridgeSwitchDiscovery);

    /// <summary>Roots N1 packet-path allowlist commands for architecture scans.</summary>
    public static Type PacketPathAllowlistAnchor { get; } = typeof(Commands.PacketPathAllowlist);

    /// <summary>Roots M7.1 routing-assurance allowlist commands for architecture scans.</summary>
    public static Type RoutingAssuranceAllowlistAnchor { get; } = typeof(Commands.RoutingAssuranceAllowlist);

    /// <summary>Roots capability profile evaluation for architecture scans.</summary>
    public static Type CapabilityProfileEvaluatorAnchor { get; } = typeof(Capabilities.CapabilityProfileEvaluator);

    /// <summary>Roots packet-path topology projection for architecture scans.</summary>
    public static Type PacketPathTopologyDiscoveryAnchor { get; } = typeof(Discovery.PacketPathTopologyDiscovery);

    /// <summary>Roots packet-path classification for architecture scans.</summary>
    public static Type PacketPathClassifierAnchor { get; } = typeof(Discovery.PacketPathClassifier);

    /// <summary>Roots production snapshot capture for architecture scans (P2-05).</summary>
    public static Type SnapshotCapturePortAnchor { get; } = typeof(Ports.RouterOsSnapshotCapturePort);

    /// <summary>Roots the closed onboarding bootstrap writer (M5-05).</summary>
    public static Type OnboardingBootstrapWriterAnchor { get; } = typeof(Onboarding.OnboardingBootstrapWriter);

    /// <summary>Roots production onboarding runtime (P2-07).</summary>
    public static Type OnboardingRuntimeAnchor { get; } = typeof(Onboarding.RouterOsOnboardingRuntime);

    /// <summary>Roots production deployment runtime (P2-08).</summary>
    public static Type DeploymentRuntimeAnchor { get; } = typeof(Deployment.RouterOsDeploymentRuntime);
}
