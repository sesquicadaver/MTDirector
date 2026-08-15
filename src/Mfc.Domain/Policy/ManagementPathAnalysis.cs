using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Policy;

/// <summary>
/// Management-path safety validation (Policy Model §46 / M2-13).
/// Does not create, move, or rewrite guards. Does not skip in-band API-SSL when OOB is set.
/// Does not treat RouterOS implicit accept as a proven management allow.
/// </summary>
public static class ManagementPathAnalysis
{
    public const string AnalyzerVersion = "mfc.management-path.v1";

    public const string ManagementPathContextPrefix = "mfc.policy.management_path_context.v1";

    public const string AnalysisContextPrefix = "mfc.policy.analysis_context.v1";

    public const ushort DefaultApiSslPort = 8729;

    private static readonly HashSet<string> InputGuardMatchers = new(StringComparer.Ordinal)
    {
        "protocol",
        "src-address",
        "dst-address",
        "dst-port",
        "connection-state",
        "in-interface",
        "in-interface-list",
    };

    private static readonly HashSet<string> OutputGuardMatchers = new(StringComparer.Ordinal)
    {
        "protocol",
        "src-address",
        "dst-address",
        "src-port",
        "connection-state",
        "out-interface",
        "out-interface-list",
    };

    /// <summary>
    /// Validates API-SSL, source restrictions, and the pre-anchor management guard on one physical device.
    /// Caller iterates VRRP members with <see cref="ManagementAccessProfile.WithDestination"/>.
    /// </summary>
    public static ManagementPathAnalysisResult Analyze(
        ManagementAccessProfile profile,
        ManagementIpServiceFacts service,
        IReadOnlyList<ActualFilterRule> rules,
        IReadOnlyList<string>? candidateComments = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(rules);
        IReadOnlyList<string> candidates = candidateComments ?? [];

        List<ManagementPathFinding> findings = [];
        PolicyWitnessPacket? inputWitness = TryWitness(profile, PolicyFilterChain.Input, ConnectionState.New);
        PolicyWitnessPacket? outputWitness = TryWitness(profile, PolicyFilterChain.Output, ConnectionState.Established);

        CheckVipOnlyDestination(profile, findings);
        CheckService(profile, service, findings, inputWitness);
        CheckSourceRestrictions(profile, service, findings, inputWitness);

        IpAddressFamily family = ResolveFamily(profile);
        IReadOnlyList<ActualFilterRule> familyRules = rules
            .Where(r => r.Family == family && !r.Disabled)
            .OrderBy(static r => r.Ordinal)
            .ToArray();

        CheckCandidateWouldChangeGuard(candidates, findings);
        CheckChain(
            profile,
            family,
            "input",
            familyRules,
            inputWitness,
            findings,
            requireNew: true);
        CheckChain(
            profile,
            family,
            "output",
            familyRules,
            outputWitness,
            findings,
            requireNew: false);

        IReadOnlyList<ManagementPathFinding> ordered = findings
            .GroupBy(static f => (f.Code, f.Chain, f.Ordinal, f.Message))
            .Select(static g => g.First())
            .OrderBy(static f => f.Code, StringComparer.Ordinal)
            .ThenBy(static f => f.Chain ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(static f => f.Ordinal ?? -1)
            .ThenBy(static f => f.Message, StringComparer.Ordinal)
            .ToArray();

        return new ManagementPathAnalysisResult
        {
            Findings = ordered,
            ManagementPathContextHash = HashManagementPathContext(profile, service, rules, candidates),
            SystemTests = BuildSystemTests(inputWitness, outputWitness),
        };
    }

    /// <summary>SHA-256 of ordered profile, API-SSL facts, filter identity, and candidate comments.</summary>
    public static Hash256 HashManagementPathContext(
        ManagementAccessProfile profile,
        ManagementIpServiceFacts service,
        IReadOnlyList<ActualFilterRule> rules,
        IReadOnlyList<string>? candidateComments = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(rules);
        IReadOnlyList<string> candidates = candidateComments ?? [];
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, ManagementPathContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, profile.ManagementDestination);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, profile.ApiSslPort.ToString(CultureInfo.InvariantCulture));
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, profile.ExpectedIngressInterface ?? string.Empty);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, profile.ExpectedEgressInterface ?? string.Empty);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, profile.TrustProfile ?? string.Empty);
        hasher.AppendData([(byte)0]);
        hasher.AppendData([(byte)(profile.OutOfBandIndependent ? 1 : 0)]);
        hasher.AppendData([(byte)0]);
        foreach (AddressPrefix prefix in profile.ControllerSourcePrefixes.OrderBy(static p => p.ToString(), StringComparer.Ordinal))
        {
            AppendUtf8(hasher, prefix.ToString());
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        foreach (string address in profile.PhysicalManagementAddresses.OrderBy(static a => a, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, address);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        foreach (string address in profile.VirtualManagementAddresses.OrderBy(static a => a, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, address);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)1]);
        hasher.AppendData([(byte)(service.Found ? 1 : 0)]);
        hasher.AppendData([(byte)(service.Disabled ? 1 : 0)]);
        AppendUtf8(hasher, service.Port ?? string.Empty);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, service.AddressPrefixes ?? string.Empty);
        hasher.AppendData([(byte)1]);
        foreach (ActualFilterRule rule in rules
                     .OrderBy(static r => r.Family)
                     .ThenBy(static r => r.Chain, StringComparer.Ordinal)
                     .ThenBy(static r => r.Ordinal))
        {
            hasher.AppendData([(byte)(int)rule.Family]);
            AppendUtf8(hasher, rule.Chain);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Ordinal.ToString(CultureInfo.InvariantCulture));
            hasher.AppendData([(byte)0]);
            hasher.AppendData([(byte)(rule.Disabled ? 1 : 0)]);
            hasher.AppendData([(byte)(rule.Dynamic ? 1 : 0)]);
            AppendUtf8(hasher, rule.Action ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.JumpTarget ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, rule.Comment ?? string.Empty);
            hasher.AppendData([(byte)0]);
            AppendMatchers(hasher, rule.KnownMatchers);
            AppendMatchers(hasher, rule.UnknownMatchers);
            hasher.AppendData([(byte)1]);
        }

        foreach (string comment in candidates.OrderBy(static c => c, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, comment);
            hasher.AppendData([(byte)0]);
        }

        return Hash256.Create(hasher.GetHashAndReset());
    }

    /// <summary>
    /// analysis_context_hash that includes the M2-12 actual-filter slot, the N1-04 packet-path slot,
    /// and this management-path slot. Does not change the one-argument
    /// <see cref="ActualFilterAnalysis.HashAnalysisContext(Hash256)"/> or two-argument
    /// <see cref="PacketPathAnalysis.HashAnalysisContext(Hash256, Hash256)"/> preimages.
    /// </summary>
    public static Hash256 HashAnalysisContext(
        Hash256 actualFilterContextHash,
        Hash256 packetPathContextHash,
        Hash256 managementPathContextHash)
    {
        ArgumentNullException.ThrowIfNull(actualFilterContextHash);
        ArgumentNullException.ThrowIfNull(packetPathContextHash);
        ArgumentNullException.ThrowIfNull(managementPathContextHash);
        using IncrementalHash hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendUtf8(hasher, AnalysisContextPrefix);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, ActualFilterAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(actualFilterContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, PacketPathAnalysis.AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(packetPathContextHash.Bytes);
        hasher.AppendData([(byte)0]);
        AppendUtf8(hasher, AnalyzerVersion);
        hasher.AppendData([(byte)0]);
        hasher.AppendData(managementPathContextHash.Bytes);
        return Hash256.Create(hasher.GetHashAndReset());
    }

    private static void CheckVipOnlyDestination(ManagementAccessProfile profile, List<ManagementPathFinding> findings)
    {
        if (profile.VirtualManagementAddresses.Count > 0 && profile.PhysicalManagementAddresses.Count == 0)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                "VRRP virtual address is the only management endpoint; each member must be checked by physical address."));
            return;
        }

        if (!TryParseHost(profile.ManagementDestination, out IPAddress? dest))
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"Management destination '{profile.ManagementDestination}' is not a concrete IP; VIP vs physical cannot be proven."));
            return;
        }

        bool destIsVirtual = profile.VirtualManagementAddresses.Any(v => HostEquals(v, dest));
        bool destIsPhysical = profile.PhysicalManagementAddresses.Any(p => HostEquals(p, dest));
        if (destIsVirtual && !destIsPhysical)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"Management destination {dest} is a VRRP virtual address, not a physical member address."));
        }
    }

    private static void CheckService(
        ManagementAccessProfile profile,
        ManagementIpServiceFacts service,
        List<ManagementPathFinding> findings,
        PolicyWitnessPacket? witness)
    {
        if (!service.Found)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.ServiceDisabled,
                "API-SSL service was not found.",
                witness: witness));
            return;
        }

        if (service.Disabled)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.ServiceDisabled,
                "API-SSL service is disabled.",
                witness: witness));
            return;
        }

        if (!TryParsePort(service.Port, out ushort actualPort) || actualPort != profile.ApiSslPort)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.ServiceDisabled,
                $"API-SSL port '{service.Port ?? "(missing)"}' does not match profile {profile.ApiSslPort}.",
                witness: witness));
        }
    }

    private static void CheckSourceRestrictions(
        ManagementAccessProfile profile,
        ManagementIpServiceFacts service,
        List<ManagementPathFinding> findings,
        PolicyWitnessPacket? witness)
    {
        if (!service.Found || service.Disabled || string.IsNullOrWhiteSpace(service.AddressPrefixes))
        {
            return;
        }

        List<AddressPrefix> allowed = ParsePrefixList(service.AddressPrefixes, out bool invalidRestriction);
        if (invalidRestriction || allowed.Count == 0)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"API-SSL address restriction '{service.AddressPrefixes}' cannot be parsed.",
                witness: witness));
            return;
        }

        foreach (AddressPrefix controller in profile.ControllerSourcePrefixes)
        {
            if (allowed.Any(a => a.Contains(controller)))
            {
                continue;
            }

            findings.Add(Finding(
                ManagementPathAnalysisCodes.SourceNotAllowed,
                $"Controller source {controller} is not allowed by API-SSL address restriction '{service.AddressPrefixes}'.",
                witness: witness));
        }
    }

    private static void CheckCandidateWouldChangeGuard(
        IReadOnlyList<string> candidateComments,
        List<ManagementPathFinding> findings)
    {
        foreach (string comment in candidateComments)
        {
            if (ActualFilterMarker.IsGuard(comment))
            {
                findings.Add(Finding(
                    ManagementPathAnalysisCodes.GuardMoved,
                    "Candidate policy contains a management-guard marker and would change the protected guard."));
                return;
            }
        }
    }

    private static void CheckChain(
        ManagementAccessProfile profile,
        IpAddressFamily family,
        string chain,
        IReadOnlyList<ActualFilterRule> familyRules,
        PolicyWitnessPacket? witness,
        List<ManagementPathFinding> findings,
        bool requireNew)
    {
        List<ActualFilterRule> chainRules = familyRules
            .Where(r => string.Equals(r.Chain, chain, StringComparison.OrdinalIgnoreCase))
            .OrderBy(static r => r.Ordinal)
            .ToList();
        ActualFilterRule? anchor = chainRules.FirstOrDefault(static r => ActualFilterMarker.IsAnchor(r.Comment));
        List<ActualFilterRule> guards = chainRules.Where(static r => ActualFilterMarker.IsGuard(r.Comment)).ToList();
        string missingCode = requireNew
            ? ManagementPathAnalysisCodes.GuardMissing
            : ManagementPathAnalysisCodes.OutputBlocked;
        string blockedCode = requireNew
            ? ManagementPathAnalysisCodes.InputBlocked
            : ManagementPathAnalysisCodes.OutputBlocked;

        if (guards.Count == 0)
        {
            findings.Add(Finding(
                missingCode,
                requireNew
                    ? "Management input guard is missing."
                    : "Management output guard is missing; ESTABLISHED reply path is not proven.",
                chain,
                witness: witness));
            CheckUnmanagedBefore(chainRules, limitExclusive: anchor?.Ordinal, chain, blockedCode, witness, findings);
            return;
        }

        foreach (ActualFilterRule guard in guards)
        {
            if (!ActualFilterMarker.IsValidGuardMarker(guard.Comment)
                || !MarkerStartsComment(guard.Comment))
            {
                findings.Add(Finding(
                    ManagementPathAnalysisCodes.PathIndeterminate,
                    "Management guard ownership marker is invalid.",
                    chain,
                    guard.Ordinal,
                    witness));
                continue;
            }

            if (anchor is not null && guard.Ordinal >= anchor.Ordinal)
            {
                findings.Add(Finding(
                    ManagementPathAnalysisCodes.GuardMoved,
                    $"Management {chain} guard at ordinal {guard.Ordinal} is not before the managed anchor at {anchor.Ordinal}.",
                    chain,
                    guard.Ordinal,
                    witness));
            }

            EvaluateGuard(
                profile,
                family,
                guard,
                chain,
                requireNew,
                blockedCode,
                witness,
                findings);
        }

        int firstGuardOrdinal = guards.Min(static g => g.Ordinal);
        CheckUnmanagedBefore(chainRules, firstGuardOrdinal, chain, blockedCode, witness, findings);
    }

    private static void EvaluateGuard(
        ManagementAccessProfile profile,
        IpAddressFamily family,
        ActualFilterRule guard,
        string chain,
        bool requireNew,
        string blockedCode,
        PolicyWitnessPacket? witness,
        List<ManagementPathFinding> findings)
    {
        if (guard.Dynamic)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                "Management guard is a dynamic rule.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        if (guard.UnknownMatchers.Count > 0)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"Management {chain} guard has an unknown matcher.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        HashSet<string> allow = requireNew ? InputGuardMatchers : OutputGuardMatchers;
        foreach (string key in guard.KnownMatchers.Keys)
        {
            if (!allow.Contains(key))
            {
                findings.Add(Finding(
                    ManagementPathAnalysisCodes.PathIndeterminate,
                    $"Management {chain} guard uses forbidden matcher '{key}'.",
                    chain,
                    guard.Ordinal,
                    witness));
                return;
            }
        }

        if (!string.Equals(guard.Action, "accept", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Finding(
                blockedCode,
                $"Management {chain} guard action '{guard.Action ?? "(missing)"}' is not accept.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        if (!IsTcp(Known(guard, "protocol")))
        {
            findings.Add(Finding(
                blockedCode,
                $"Management {chain} guard protocol '{Known(guard, "protocol") ?? "(missing)"}' is not tcp.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        string? states = Known(guard, "connection-state");
        if (string.IsNullOrWhiteSpace(states))
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"Management {chain} guard omits connection-state (Onboarding §16 requires an explicit set).",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        if (requireNew && !HasToken(states, "new"))
        {
            findings.Add(Finding(
                blockedCode,
                "Management input guard does not allow TCP NEW.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        if (!requireNew && !HasToken(states, "established"))
        {
            findings.Add(Finding(
                blockedCode,
                "Management output guard does not allow TCP ESTABLISHED.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        string? portField = requireNew ? Known(guard, "dst-port") : Known(guard, "src-port");
        if (!PortContains(portField, profile.ApiSslPort))
        {
            findings.Add(Finding(
                blockedCode,
                $"Management {chain} guard port '{portField ?? "(missing)"}' does not include API-SSL {profile.ApiSslPort}.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        string? sourceField = requireNew ? Known(guard, "src-address") : Known(guard, "dst-address");
        if (string.IsNullOrWhiteSpace(sourceField))
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"Management {chain} guard omits controller source prefixes.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        List<AddressPrefix> sourceMatchers = ParsePrefixList(sourceField, out bool invalidSources);
        if (invalidSources || sourceMatchers.Count == 0)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"Management {chain} guard source matcher '{sourceField}' cannot be parsed.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }
        foreach (AddressPrefix controller in profile.ControllerSourcePrefixes.Where(p => p.Family == family))
        {
            if (!sourceMatchers.Any(m => m.Contains(controller)))
            {
                findings.Add(Finding(
                    blockedCode,
                    $"Management {chain} guard source matcher '{sourceField}' does not cover controller prefix {controller}.",
                    chain,
                    guard.Ordinal,
                    witness));
                return;
            }
        }

        if (!TryParseHost(profile.ManagementDestination, out IPAddress? dest))
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                "Management destination is not a concrete IP; guard destination match cannot be proven.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        string? destField = requireNew ? Known(guard, "dst-address") : Known(guard, "src-address");
        if (string.IsNullOrWhiteSpace(destField))
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"Management {chain} guard omits the physical management address.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        List<AddressPrefix> destMatchers = ParsePrefixList(destField, out bool invalidDest);
        if (invalidDest || destMatchers.Count == 0)
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"Management {chain} guard address '{destField}' cannot be parsed.",
                chain,
                guard.Ordinal,
                witness));
            return;
        }

        if (!destMatchers.Any(m => m.Contains(dest)))
        {
            findings.Add(Finding(
                blockedCode,
                $"Management {chain} guard address '{destField}' does not contain physical destination {dest}.",
                chain,
                guard.Ordinal,
                witness));
        }

        string? expectedIface = requireNew ? profile.ExpectedIngressInterface : profile.ExpectedEgressInterface;
        if (expectedIface is null)
        {
            return;
        }

        string? iface = requireNew
            ? Known(guard, "in-interface") ?? Known(guard, "in-interface-list")
            : Known(guard, "out-interface") ?? Known(guard, "out-interface-list");
        if (!string.Equals(iface, expectedIface, StringComparison.Ordinal))
        {
            findings.Add(Finding(
                ManagementPathAnalysisCodes.PathIndeterminate,
                $"Management {chain} guard interface '{iface ?? "(missing)"}' does not match expected '{expectedIface}'.",
                chain,
                guard.Ordinal,
                witness));
        }
    }

    private static void CheckUnmanagedBefore(
        IReadOnlyList<ActualFilterRule> chainRules,
        int? limitExclusive,
        string chain,
        string blockedCode,
        PolicyWitnessPacket? witness,
        List<ManagementPathFinding> findings)
    {
        foreach (ActualFilterRule rule in chainRules)
        {
            if (limitExclusive is int limit && rule.Ordinal >= limit)
            {
                break;
            }

            if (!ActualFilterMarker.IsUnmanaged(rule.Comment))
            {
                continue;
            }

            if (rule.UnknownMatchers.Count > 0
                || string.IsNullOrWhiteSpace(rule.Action)
                || string.Equals(rule.Action, "jump", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rule.Action, "fasttrack-connection", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rule.Action, "tarpit", StringComparison.OrdinalIgnoreCase)
                || !(string.Equals(rule.Action, "accept", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(rule.Action, "drop", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(rule.Action, "reject", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(rule.Action, "log", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(rule.Action, "passthrough", StringComparison.OrdinalIgnoreCase)))
            {
                findings.Add(Finding(
                    ManagementPathAnalysisCodes.PathIndeterminate,
                    $"Unmanaged pre-anchor {chain} rule at ordinal {rule.Ordinal} has an unknown matcher or action on the management path.",
                    chain,
                    rule.Ordinal,
                    witness));
                continue;
            }

            if (string.Equals(rule.Action, "drop", StringComparison.OrdinalIgnoreCase)
                || string.Equals(rule.Action, "reject", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(Finding(
                    blockedCode,
                    $"Unmanaged pre-anchor {chain} {rule.Action} at ordinal {rule.Ordinal} blocks the management path.",
                    chain,
                    rule.Ordinal,
                    witness));
            }
        }
    }

    private static List<ManagementSystemTest> BuildSystemTests(
        PolicyWitnessPacket? inputWitness,
        PolicyWitnessPacket? outputWitness)
    {
        List<ManagementSystemTest> tests = [];
        if (inputWitness is not null)
        {
            tests.Add(new ManagementSystemTest
            {
                Origin = ManagementSystemTest.OriginSystem,
                Chain = PolicyFilterChain.Input,
                Expected = ManagementSystemTest.ExpectedAccept,
                Packet = inputWitness,
            });
        }

        if (outputWitness is not null)
        {
            tests.Add(new ManagementSystemTest
            {
                Origin = ManagementSystemTest.OriginSystem,
                Chain = PolicyFilterChain.Output,
                Expected = ManagementSystemTest.ExpectedAccept,
                Packet = outputWitness,
            });
        }

        return tests;
    }

    private static PolicyWitnessPacket? TryWitness(
        ManagementAccessProfile profile,
        PolicyFilterChain chain,
        ConnectionState state)
    {
        if (!TryParseHost(profile.ManagementDestination, out IPAddress? dest))
        {
            return null;
        }

        AddressPrefix? source = profile.ControllerSourcePrefixes.FirstOrDefault(p => p.Family == FamilyOf(dest));
        if (source is null)
        {
            return null;
        }

        string controller = source.Address.ToString();
        string destination = dest.ToString();
        bool input = chain == PolicyFilterChain.Input;
        return new PolicyWitnessPacket
        {
            Family = FamilyOf(dest),
            Chain = chain,
            SourceAddress = input ? controller : destination,
            DestinationAddress = input ? destination : controller,
            Protocol = IpProtocol.Tcp,
            SourcePort = input ? null : profile.ApiSslPort,
            DestinationPort = input ? profile.ApiSslPort : null,
            ConnectionState = state,
        };
    }

    private static IpAddressFamily ResolveFamily(ManagementAccessProfile profile)
    {
        if (TryParseHost(profile.ManagementDestination, out IPAddress? dest))
        {
            return FamilyOf(dest);
        }

        return profile.ControllerSourcePrefixes[0].Family;
    }

    private static IpAddressFamily FamilyOf(IPAddress address)
        => address.AddressFamily == AddressFamily.InterNetwork
            ? IpAddressFamily.IPv4
            : IpAddressFamily.IPv6;

    private static bool TryParseHost(string value, out IPAddress address)
        => IPAddress.TryParse(value.Trim(), out address!);

    private static bool HostEquals(string value, IPAddress address)
        => TryParseHost(value, out IPAddress parsed) && parsed.Equals(address);

    private static bool TryParsePort(string? value, out ushort port)
    {
        port = 0;
        return !string.IsNullOrWhiteSpace(value)
               && ushort.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out port)
               && port != 0;
    }

    private static bool IsTcp(string? protocol)
        => string.Equals(protocol, "tcp", StringComparison.OrdinalIgnoreCase)
           || string.Equals(protocol, "6", StringComparison.Ordinal);

    private static bool HasToken(string csv, string token)
        => csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(part => string.Equals(part, token, StringComparison.OrdinalIgnoreCase));

    private static bool PortContains(string? field, ushort port)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return false;
        }

        foreach (string token in field.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            string[] bounds = token.Split('-', 2, StringSplitOptions.TrimEntries);
            if (!ushort.TryParse(bounds[0], NumberStyles.None, CultureInfo.InvariantCulture, out ushort start))
            {
                continue;
            }

            ushort end = start;
            if (bounds.Length == 2
                && !ushort.TryParse(bounds[1], NumberStyles.None, CultureInfo.InvariantCulture, out end))
            {
                continue;
            }

            if (port >= start && port <= end)
            {
                return true;
            }
        }

        return false;
    }

    private static List<AddressPrefix> ParsePrefixList(string? csv, out bool hadInvalid)
    {
        hadInvalid = false;
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        List<AddressPrefix> prefixes = [];
        foreach (string token in csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParsePrefixOrHost(token, out AddressPrefix? prefix) && prefix is not null)
            {
                prefixes.Add(prefix);
            }
            else
            {
                hadInvalid = true;
            }
        }

        return prefixes;
    }

    private static bool TryParsePrefixOrHost(string token, out AddressPrefix? prefix)
    {
        prefix = null;
        try
        {
            if (token.Contains('/', StringComparison.Ordinal))
            {
                prefix = AddressPrefix.Parse(token);
                return true;
            }

            if (!IPAddress.TryParse(token, out IPAddress? address))
            {
                return false;
            }

            byte bits = address.AddressFamily == AddressFamily.InterNetwork ? (byte)32 : (byte)128;
            prefix = AddressPrefix.Create(address, bits);
            return true;
        }
        catch (DomainInvariantException)
        {
            return false;
        }
    }

    private static bool MarkerStartsComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return false;
        }

        string trimmed = comment.TrimStart();
        return trimmed.StartsWith(ActualFilterMarker.FwcGuardPrefix, StringComparison.Ordinal)
               || trimmed.StartsWith(ActualFilterMarker.MfcGuardPrefix, StringComparison.Ordinal);
    }

    private static string? Known(ActualFilterRule rule, string key)
        => rule.KnownMatchers.TryGetValue(key, out string? value) ? value : null;

    private static ManagementPathFinding Finding(
        string code,
        string message,
        string? chain = null,
        int? ordinal = null,
        PolicyWitnessPacket? witness = null)
        => new()
        {
            Code = code,
            Severity = ManagementPathAnalysisCodes.SeverityBlocker,
            Message = message,
            Chain = chain,
            Ordinal = ordinal,
            Witness = witness,
        };

    private static void AppendMatchers(IncrementalHash hasher, IReadOnlyDictionary<string, string> matchers)
    {
        foreach (KeyValuePair<string, string> pair in matchers.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            AppendUtf8(hasher, pair.Key);
            hasher.AppendData([(byte)0]);
            AppendUtf8(hasher, pair.Value);
            hasher.AppendData([(byte)0]);
        }

        hasher.AppendData([(byte)2]);
    }

    private static void AppendUtf8(IncrementalHash hasher, string value)
        => hasher.AppendData(Encoding.UTF8.GetBytes(value));
}
