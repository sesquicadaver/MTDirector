using System.Globalization;
using Mfc.Domain.Inventory.Primitives;

namespace Mfc.Domain.Routing;

/// <summary>
/// Compares previous routing assurance state to current snapshots and emits classified drift findings (M7.1-09).
/// </summary>
public static class RoutingDriftAnalyzer
{
    /// <summary>Analyzes drift between persisted state and incoming snapshots.</summary>
    public static RoutingDriftClassification Analyze(
        RoutingAssuranceState previous,
        RoutingConfigurationSnapshot currentConfiguration,
        RoutingOperationalSnapshot currentOperational)
    {
        ArgumentNullException.ThrowIfNull(previous);
        ArgumentNullException.ThrowIfNull(currentConfiguration);
        ArgumentNullException.ThrowIfNull(currentOperational);
        return AnalyzeInternal(
            previous.Configuration.HashMaterial,
            previous.OperationalState.HashMaterial,
            currentConfiguration.HashMaterial,
            currentOperational.HashMaterial);
    }

    /// <summary>Analyzes drift between explicit previous and current hash materials.</summary>
    public static RoutingDriftClassification Analyze(
        RoutingConfigurationSnapshot previousConfiguration,
        RoutingOperationalSnapshot previousOperational,
        RoutingConfigurationSnapshot currentConfiguration,
        RoutingOperationalSnapshot currentOperational)
    {
        ArgumentNullException.ThrowIfNull(previousConfiguration);
        ArgumentNullException.ThrowIfNull(previousOperational);
        ArgumentNullException.ThrowIfNull(currentConfiguration);
        ArgumentNullException.ThrowIfNull(currentOperational);
        return AnalyzeInternal(
            previousConfiguration.HashMaterial,
            previousOperational.HashMaterial,
            currentConfiguration.HashMaterial,
            currentOperational.HashMaterial);
    }

    private static RoutingDriftClassification AnalyzeInternal(
        IReadOnlyDictionary<string, string> previousConfigurationMaterial,
        IReadOnlyDictionary<string, string> previousOperationalMaterial,
        IReadOnlyDictionary<string, string> currentConfigurationMaterial,
        IReadOnlyDictionary<string, string> currentOperationalMaterial)
    {
        ArgumentNullException.ThrowIfNull(previousConfigurationMaterial);
        ArgumentNullException.ThrowIfNull(previousOperationalMaterial);
        ArgumentNullException.ThrowIfNull(currentConfigurationMaterial);
        ArgumentNullException.ThrowIfNull(currentOperationalMaterial);

        Hash256 previousConfigHash = RoutingAssuranceHashContract.HashConfiguration(previousConfigurationMaterial);
        Hash256 currentConfigHash = RoutingAssuranceHashContract.HashConfiguration(currentConfigurationMaterial);
        Hash256 previousOpsHash = RoutingAssuranceHashContract.HashOperational(previousOperationalMaterial);
        Hash256 currentOpsHash = RoutingAssuranceHashContract.HashOperational(currentOperationalMaterial);

        bool configurationHashChanged = !previousConfigHash.Equals(currentConfigHash);
        bool operationalHashChanged = !previousOpsHash.Equals(currentOpsHash);
        if (!configurationHashChanged && !operationalHashChanged)
        {
            return RoutingDriftClassification.None;
        }

        List<RouteFinding> findings = [];
        bool configurationDrift = false;
        bool operationalChange = false;

        foreach ((string key, string? previousValue, string? currentValue) in DiffMaterial(
                     previousConfigurationMaterial,
                     currentConfigurationMaterial))
        {
            configurationDrift = true;
            RoutingDriftKind kind = RoutingDriftClassifier.ClassifyMaterialKey(key, isConfigurationMaterial: true);
            findings.Add(CreateFinding(kind, key, previousValue, currentValue));
        }

        foreach ((string key, string? previousValue, string? currentValue) in DiffMaterial(
                     previousOperationalMaterial,
                     currentOperationalMaterial))
        {
            operationalChange = true;
            RoutingDriftKind kind = RoutingDriftClassifier.ClassifyOperationalChange(key, previousValue, currentValue);
            findings.Add(CreateFinding(kind, key, previousValue, currentValue));
        }

        if (configurationDrift)
        {
            findings.Insert(
                0,
                new RouteFinding
                {
                    Code = RoutingDriftCodes.ConfigurationDrift,
                    Message = "Routing configuration hash material changed.",
                    Subject = null,
                });
        }

        if (operationalChange)
        {
            int insertAt = configurationDrift ? 1 : 0;
            findings.Insert(
                insertAt,
                new RouteFinding
                {
                    Code = RoutingDriftCodes.OperationalChange,
                    Message = "Routing operational hash material changed.",
                    Subject = null,
                });
        }

        return new RoutingDriftClassification
        {
            IsConfigurationDrift = configurationDrift,
            IsOperationalChange = operationalChange,
            ConfigurationHashChanged = configurationHashChanged,
            OperationalHashChanged = operationalHashChanged,
            Findings = findings,
        };
    }

    private static RouteFinding CreateFinding(
        RoutingDriftKind kind,
        string subject,
        string? previousValue,
        string? currentValue)
    {
        string code = RoutingDriftCodes.CodeForKind(kind);
        string message = string.Create(
            CultureInfo.InvariantCulture,
            $"Routing drift: {kind} ({FormatValue(previousValue)} → {FormatValue(currentValue)}).");
        return new RouteFinding
        {
            Code = code,
            Message = message,
            Subject = subject,
        };
    }

    private static string FormatValue(string? value) => value ?? "<removed>";

    private static IEnumerable<(string Key, string? PreviousValue, string? CurrentValue)> DiffMaterial(
        IReadOnlyDictionary<string, string> previous,
        IReadOnlyDictionary<string, string> current)
    {
        HashSet<string> allKeys = new(previous.Keys, StringComparer.Ordinal);
        allKeys.UnionWith(current.Keys);
        foreach (string key in allKeys.OrderBy(static k => k, StringComparer.Ordinal))
        {
            previous.TryGetValue(key, out string? previousValue);
            current.TryGetValue(key, out string? currentValue);
            if (string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            {
                continue;
            }

            yield return (key, previousValue, currentValue);
        }
    }
}
