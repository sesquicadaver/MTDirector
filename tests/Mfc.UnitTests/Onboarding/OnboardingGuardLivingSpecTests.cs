using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Mfc.Application.Onboarding;
using Mfc.Domain;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Onboarding.Primitives;
using Mfc.Domain.Policy;
using Xunit;

namespace Mfc.UnitTests.Onboarding;

/// <summary>
/// Living Spec matrix for Issue Set M5-03 AC 1–10 (Onboarding Spec §13–§17 / §58).
/// </summary>
public sealed class OnboardingGuardLivingSpecTests
{
    private static readonly GuardProfileId ProfileId = GuardProfileId.Parse("0123456789abcdef");

    [Fact]
    public void Ac1GuardProfileIsTyped()
    {
        GuardProfile profile = ValidProfile(out DeviceId deviceId);
        Assert.Equal(ProfileId, profile.Id);
        Assert.Equal(deviceId, profile.DeviceId);
        Assert.Equal(IpAddressFamily.IPv4, profile.Family);
        Assert.Equal((ushort)8729, profile.ApiSslPort);
        Assert.Equal(32, profile.CanonicalHash.Bytes.Length);
        Assert.Throws<DomainInvariantException>(() =>
            GuardProfile.Create(
                ProfileId,
                deviceId,
                IpAddressFamily.IPv4,
                [AddressPrefix.Parse("0.0.0.0/0")],
                IPAddress.Parse("192.0.2.10"),
                8729,
                [InputMarker(0)],
                [OutputMarker(0)]));
    }

    [Fact]
    public void Ac2InputAndOutputGuardMarkersAreChecked()
    {
        GuardProfile profile = ValidProfile(out _);
        OnboardingGuardVerificationResult missing = Verify(profile, Anchor("input", 1), Anchor("output", 1));
        Assert.Contains(missing.Findings, static f => f.Code == OnboardingCodes.ManagementGuardMissing);

        OnboardingGuardVerificationResult loose = Verify(
            profile,
            InputGuard(0, comment: "fwc:guard:api-ssl"),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(loose.Findings, static f => f.Code == OnboardingCodes.ManagementGuardInvalid);
    }

    [Fact]
    public void Ac3GuardRulesMustBeStaticValidAndEnabled()
    {
        GuardProfile profile = ValidProfile(out _);
        OnboardingGuardVerificationResult disabled = Verify(
            profile,
            InputGuard(0, disabled: true),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(
            disabled.Findings,
            static f => f.Code == OnboardingCodes.ManagementGuardInvalid && f.Target == "enabled");

        OnboardingGuardVerificationResult dynamic = Verify(
            profile,
            InputGuard(0, dynamic: true),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(
            dynamic.Findings,
            static f => f.Code == OnboardingCodes.ManagementGuardInvalid && f.Target == "static");
    }

    [Fact]
    public void Ac4PredicateMustNotBeWiderThanProfile()
    {
        GuardProfile profile = ValidProfile(out _);
        Dictionary<string, string> wide = InputMatchers();
        wide["src-address"] = "192.0.2.0/16";
        OnboardingGuardVerificationResult result = Verify(
            profile,
            InputGuard(0, matchers: wide),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(result.Findings, static f => f.Code == OnboardingCodes.ManagementGuardTooBroad);
    }

    [Fact]
    public void Ac5DefaultRoutesAreRejected()
    {
        GuardProfile profile = ValidProfile(out _);
        Dictionary<string, string> inputSrc = InputMatchers();
        inputSrc["src-address"] = "0.0.0.0/0";
        OnboardingGuardVerificationResult v4Src = Verify(
            profile,
            InputGuard(0, matchers: inputSrc),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(v4Src.Findings, static f => f.Code == OnboardingCodes.ManagementGuardTooBroad);

        Dictionary<string, string> inputDst = InputMatchers();
        inputDst["dst-address"] = "0.0.0.0/0";
        OnboardingGuardVerificationResult v4Dst = Verify(
            profile,
            InputGuard(0, matchers: inputDst),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(v4Dst.Findings, static f => f.Code == OnboardingCodes.ManagementGuardTooBroad);

        Assert.Throws<DomainInvariantException>(() =>
            GuardProfile.Create(
                GuardProfileId.Parse("fedcba9876543210"),
                DeviceId.New(),
                IpAddressFamily.IPv6,
                [AddressPrefix.Parse("::/0")],
                IPAddress.Parse("2001:db8::1"),
                8729,
                [GuardMarker.Format(GuardProfileId.Parse("fedcba9876543210"), IpAddressFamily.IPv6, FilterBuiltInContext.Input, 0)],
                [GuardMarker.Format(GuardProfileId.Parse("fedcba9876543210"), IpAddressFamily.IPv6, FilterBuiltInContext.Output, 0)]));
    }

    [Fact]
    public void Ac6GuardMustPrecedePlannedAnchors()
    {
        GuardProfile profile = ValidProfile(out DeviceId deviceId);
        AnchorPlacement[] placements =
        [
            AnchorPlacement.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Input, AnchorPlacementMode.Append, 0),
            AnchorPlacement.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Output, AnchorPlacementMode.Append, 0),
            AnchorPlacement.Create(IpAddressFamily.IPv4, FilterBuiltInContext.Forward, AnchorPlacementMode.Append, 0),
        ];
        OnboardingGuardVerificationResult planned = VerifyManagementGuardUseCase.Execute(
            profile,
            [
                InputGuard(0),
                OutputGuard(0),
                Anchor("input", 1),
                Anchor("output", 1),
            ],
            profile.CanonicalHash,
            placements);
        Assert.Contains(planned.Findings, static f => f.Code == ManagementPathAnalysisCodes.GuardMoved);

        OnboardingGuardVerificationResult live = Verify(
            profile,
            Anchor("input", 0),
            InputGuard(1),
            OutputGuard(0),
            Anchor("output", 1));
        Assert.Contains(
            live.Findings,
            static f => f.Code == ManagementPathAnalysisCodes.GuardMoved && f.Chain == "input");
        Assert.Equal(deviceId, profile.DeviceId);
    }

    [Fact]
    public void Ac7DynamicListAndUnsupportedMatchersAreRejected()
    {
        GuardProfile profile = ValidProfile(out _);
        Dictionary<string, string> list = InputMatchers();
        list.Remove("src-address");
        list["src-address-list"] = "controllers";
        OnboardingGuardVerificationResult result = Verify(
            profile,
            InputGuard(0, matchers: list),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(
            result.Findings,
            static f => f.Code == OnboardingCodes.ManagementGuardInvalid && f.Target == "matcher");

        OnboardingGuardVerificationResult unknown = Verify(
            profile,
            InputGuard(0, unknown: new Dictionary<string, string>(StringComparer.Ordinal) { ["layer7-protocol"] = "x" }),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(unknown.Findings, static f => f.Code == OnboardingCodes.ManagementPathIndeterminate);
    }

    [Fact]
    public void Ac8NewApiSslConnectionThroughGuardPasses()
    {
        GuardProfile profile = ValidProfile(out _);
        OnboardingGuardVerificationResult ok = Verify(profile, SafeRules());
        Assert.True(ok.Passed);
        Assert.Empty(ok.Findings);

        Dictionary<string, string> noNew = InputMatchers();
        noNew["connection-state"] = "established";
        OnboardingGuardVerificationResult blocked = Verify(
            profile,
            InputGuard(0, matchers: noNew),
            OutputGuard(0),
            Anchor("input", 1),
            Anchor("output", 1));
        Assert.Contains(blocked.Findings, static f => f.Code == ManagementPathAnalysisCodes.InputBlocked);
    }

    [Fact]
    public void Ac9GuardHashEntersPlan()
    {
        Node node = OnboardingTestFactory.RouterWithDevice(out Device device);
        GuardProfile profile = ValidProfile(device.Id);
        DeviceOnboardingPlan devicePlan = DeviceOnboardingPlan.Create(
            device.Id,
            "7.16.2",
            H("cap"),
            H("cfg"),
            H("compat"),
            H("api"),
            H("read"),
            H("deploy"),
            H("mode"),
            profile.CanonicalHash,
            RequiredAnchorSet.For(NodeKind.Router, includeIpv6: false),
            PlacementsFor(NodeKind.Router));
        Assert.Equal(profile.CanonicalHash.ToString(), devicePlan.ExpectedGuardHash.ToString());
        Assert.Equal(profile.CanonicalHash.ToString(), GuardProfileHasher.Compute(profile).ToString());

        OnboardingGuardVerificationResult mismatch = VerifyManagementGuardUseCase.Execute(
            profile,
            SafeRules(),
            H("other-guard"));
        Assert.Contains(
            mismatch.Findings,
            static f => f.Code == OnboardingCodes.ManagementGuardInvalid && f.Target == "guard.hash");
    }

    [Fact]
    public void Ac10ControllerDoesNotCreateOrModifyGuard()
    {
        Assert.Null(typeof(OnboardingGuardVerifier).GetMethod("Create"));
        Assert.Null(typeof(OnboardingGuardVerifier).GetMethod("Apply"));
        Assert.Null(typeof(OnboardingGuardVerifier).GetMethod("Move"));
        Assert.Null(typeof(VerifyManagementGuardUseCase).GetMethod("Create"));
        Assert.DoesNotContain(
            typeof(OnboardingGuardVerifier).GetMethods(BindingFlags.Public | BindingFlags.Static),
            static m => m.Name.StartsWith("Set", StringComparison.Ordinal)
                        || m.Name.StartsWith("Write", StringComparison.Ordinal));
        Type? writeNs = typeof(Mfc.RouterOs.AssemblyMarker).Assembly.GetTypes()
            .FirstOrDefault(static t => t.Namespace == "Mfc.RouterOs.Write");
        Assert.Null(writeNs);

        GuardProfile profile = ValidProfile(out _);
        OnboardingGuardVerificationResult candidate = VerifyManagementGuardUseCase.Execute(
            profile,
            SafeRules(),
            profile.CanonicalHash,
            candidateComments: [InputMarker(0)]);
        Assert.Contains(candidate.Findings, static f => f.Code == ManagementPathAnalysisCodes.GuardMoved);
    }

    private static OnboardingGuardVerificationResult Verify(GuardProfile profile, params ActualFilterRule[] rules)
        => VerifyManagementGuardUseCase.Execute(profile, rules, profile.CanonicalHash);

    private static ActualFilterRule[] SafeRules()
        =>
        [
            InputGuard(0),
            Anchor("input", 1),
            OutputGuard(0),
            Anchor("output", 1),
        ];

    private static GuardProfile ValidProfile(out DeviceId deviceId)
    {
        deviceId = DeviceId.New();
        return ValidProfile(deviceId);
    }

    private static GuardProfile ValidProfile(DeviceId deviceId)
        => GuardProfile.Create(
            ProfileId,
            deviceId,
            IpAddressFamily.IPv4,
            [AddressPrefix.Parse("192.0.2.0/24")],
            IPAddress.Parse("192.0.2.10"),
            8729,
            [InputMarker(0)],
            [OutputMarker(0)]);

    private static string InputMarker(int ordinal)
        => GuardMarker.Format(ProfileId, IpAddressFamily.IPv4, FilterBuiltInContext.Input, ordinal);

    private static string OutputMarker(int ordinal)
        => GuardMarker.Format(ProfileId, IpAddressFamily.IPv4, FilterBuiltInContext.Output, ordinal);

    private static ActualFilterRule InputGuard(
        int ordinal,
        string? comment = null,
        bool disabled = false,
        bool dynamic = false,
        IReadOnlyDictionary<string, string>? matchers = null,
        IReadOnlyDictionary<string, string>? unknown = null)
        => ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "input",
            ordinal,
            "accept",
            disabled: disabled,
            dynamic: dynamic,
            comment: comment ?? InputMarker(ordinal),
            knownMatchers: matchers ?? InputMatchers(),
            unknownMatchers: unknown);

    private static ActualFilterRule OutputGuard(int ordinal)
        => ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            "output",
            ordinal,
            "accept",
            comment: OutputMarker(ordinal),
            knownMatchers: OutputMatchers());

    private static ActualFilterRule Anchor(string chain, int ordinal)
        => ActualFilterRule.Create(
            IpAddressFamily.IPv4,
            chain,
            ordinal,
            "jump",
            jumpTarget: $"mfc4.{(chain == "input" ? "i" : "o")}.r.0123456789abcdef",
            comment: $"mfc:anchor:v1:4:{(chain == "input" ? "i" : "o")}");

    private static Dictionary<string, string> InputMatchers()
        => new(StringComparer.Ordinal)
        {
            ["protocol"] = "tcp",
            ["src-address"] = "192.0.2.0/24",
            ["dst-address"] = "192.0.2.10",
            ["dst-port"] = "8729",
            ["connection-state"] = "new,established",
        };

    private static Dictionary<string, string> OutputMatchers()
        => new(StringComparer.Ordinal)
        {
            ["protocol"] = "tcp",
            ["src-address"] = "192.0.2.10",
            ["src-port"] = "8729",
            ["dst-address"] = "192.0.2.0/24",
            ["connection-state"] = "established,related",
        };

    private static List<AnchorPlacement> PlacementsFor(NodeKind kind)
    {
        List<AnchorPlacement> placements = [];
        uint ordinal = 1;
        foreach (AnchorKey key in RequiredAnchorSet.For(kind, includeIpv6: false))
        {
            placements.Add(AnchorPlacement.Create(
                key.Family,
                key.Chain,
                AnchorPlacementMode.Append,
                expectedAnchorOrdinal: ordinal));
            ordinal++;
        }

        return placements;
    }

    private static Hash256 H(string value)
        => Hash256.Create(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
