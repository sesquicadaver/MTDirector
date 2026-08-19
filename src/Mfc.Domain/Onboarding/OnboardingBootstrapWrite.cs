using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Domain.Onboarding;

/// <summary>Closed onboarding filter-write kinds (Onboarding Spec §27.3 / M5-05).</summary>
public enum OnboardingBootstrapWriteKind : byte
{
    AddBootstrapReturn = 0,
    AddDisabledAnchor = 1,
    SetAnchorDisabled = 2,
    RemoveDisabledAnchor = 3,
    RemoveBootstrapReturn = 4,
}

/// <summary>
/// One allowlisted bootstrap write. Attributes never include RouterOS <c>.id</c> in the plan;
/// live <c>place-before</c> is resolved at execute time.
/// </summary>
public sealed class OnboardingBootstrapWrite
{
    private OnboardingBootstrapWrite(
        OnboardingBootstrapWriteKind kind,
        IpAddressFamily family,
        FilterBuiltInContext builtIn,
        IReadOnlyList<KeyValuePair<string, string>> attributes,
        AnchorPlacementMode? placementMode,
        Hash256? placeBeforeFingerprint,
        uint? placeBeforeRank,
        bool? disabledValue)
    {
        Kind = kind;
        Family = family;
        BuiltIn = builtIn;
        Attributes = attributes;
        PlacementMode = placementMode;
        PlaceBeforeFingerprint = placeBeforeFingerprint;
        PlaceBeforeRank = placeBeforeRank;
        DisabledValue = disabledValue;
    }

    public OnboardingBootstrapWriteKind Kind { get; }

    public IpAddressFamily Family { get; }

    public FilterBuiltInContext BuiltIn { get; }

    public IReadOnlyList<KeyValuePair<string, string>> Attributes { get; }

    public AnchorPlacementMode? PlacementMode { get; }

    public Hash256? PlaceBeforeFingerprint { get; }

    public uint? PlaceBeforeRank { get; }

    public bool? DisabledValue { get; }

    public string RootChainName => BootstrapArtifact.RootChainName(Family, BuiltIn);

    public string AnchorMarker => AnchorKey.Create(Family, BuiltIn).Marker;

    public string BuiltinChainName => BuiltIn switch
    {
        FilterBuiltInContext.Input => "input",
        FilterBuiltInContext.Forward => "forward",
        FilterBuiltInContext.Output => "output",
        _ => throw new DomainInvariantException($"Unsupported built-in '{BuiltIn}'."),
    };

    public static OnboardingBootstrapWrite AddBootstrapReturn(IpAddressFamily family, FilterBuiltInContext builtIn)
    {
        string chain = BootstrapArtifact.RootChainName(family, builtIn);
        KeyValuePair<string, string>[] attributes =
        [
            new("chain", chain),
            new("action", "return"),
            new("disabled", "no"),
            new("comment", BootstrapArtifact.ReturnComment),
        ];
        ValidateReturnAttributes(attributes);
        return new OnboardingBootstrapWrite(
            OnboardingBootstrapWriteKind.AddBootstrapReturn,
            family,
            builtIn,
            attributes,
            placementMode: null,
            placeBeforeFingerprint: null,
            placeBeforeRank: null,
            disabledValue: false);
    }

    public static OnboardingBootstrapWrite AddDisabledAnchor(AnchorPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        string target = BootstrapArtifact.RootChainName(placement.Family, placement.Chain);
        string chain = BuiltinName(placement.Chain);
        KeyValuePair<string, string>[] attributes =
        [
            new("chain", chain),
            new("action", "jump"),
            new("jump-target", target),
            new("disabled", "yes"),
            new("comment", new AnchorKey(placement.Family, placement.Chain).Marker),
        ];
        if (attributes.Any(static a => a.Key == "place-before"))
        {
            throw new DomainInvariantException("Plans must not store RouterOS place-before .id.");
        }

        return new OnboardingBootstrapWrite(
            OnboardingBootstrapWriteKind.AddDisabledAnchor,
            placement.Family,
            placement.Chain,
            attributes,
            placement.Mode,
            placement.ReferenceRuleFingerprint,
            placement.ReferenceOccurrenceRank,
            disabledValue: true);
    }

    public static OnboardingBootstrapWrite SetAnchorDisabled(
        IpAddressFamily family,
        FilterBuiltInContext builtIn,
        bool disabled)
        => new(
            OnboardingBootstrapWriteKind.SetAnchorDisabled,
            family,
            builtIn,
            [new("disabled", disabled ? "yes" : "no")],
            placementMode: null,
            placeBeforeFingerprint: null,
            placeBeforeRank: null,
            disabledValue: disabled);

    public static OnboardingBootstrapWrite RemoveDisabledAnchor(IpAddressFamily family, FilterBuiltInContext builtIn)
        => new(
            OnboardingBootstrapWriteKind.RemoveDisabledAnchor,
            family,
            builtIn,
            [],
            placementMode: null,
            placeBeforeFingerprint: null,
            placeBeforeRank: null,
            disabledValue: true);

    public static OnboardingBootstrapWrite RemoveBootstrapReturn(IpAddressFamily family, FilterBuiltInContext builtIn)
        => new(
            OnboardingBootstrapWriteKind.RemoveBootstrapReturn,
            family,
            builtIn,
            [],
            placementMode: null,
            placeBeforeFingerprint: null,
            placeBeforeRank: null,
            disabledValue: false);

    internal static void ValidateReturnAttributes(IReadOnlyList<KeyValuePair<string, string>> attributes)
    {
        if (attributes.Count != 4)
        {
            throw new DomainInvariantException("Bootstrap return must have exactly chain/action/disabled/comment.");
        }

        Dictionary<string, string> map = attributes.ToDictionary(static a => a.Key, static a => a.Value, StringComparer.Ordinal);
        if (!string.Equals(map["action"], "return", StringComparison.Ordinal)
            || !string.Equals(map["disabled"], "no", StringComparison.Ordinal)
            || !string.Equals(map["comment"], BootstrapArtifact.ReturnComment, StringComparison.Ordinal)
            || map.ContainsKey("jump-target")
            || map.ContainsKey("log")
            || map.ContainsKey(".id"))
        {
            throw new DomainInvariantException("Bootstrap return attributes violate Spec §23.2.");
        }
    }

    private static string BuiltinName(FilterBuiltInContext chain)
        => chain switch
        {
            FilterBuiltInContext.Input => "input",
            FilterBuiltInContext.Forward => "forward",
            FilterBuiltInContext.Output => "output",
            _ => throw new DomainInvariantException($"Unsupported built-in '{chain}'."),
        };
}
