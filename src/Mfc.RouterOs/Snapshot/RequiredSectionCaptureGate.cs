using Mfc.RouterOs.Commands;

namespace Mfc.RouterOs.Snapshot;

/// <summary>
/// AUDIT-CAP-02 / Read Adapter §38.2: required discovery reads must succeed before a
/// capture may be treated as completed. Failures must not become empty "completed" config.
/// </summary>
public static class RequiredSectionCaptureGate
{
    public const string FailureCodePrefix = "SNAPSHOT_REQUIRED_SECTION_FAILED";

    /// <summary>
    /// Returns failed required command ids from the production discovery catalog.
    /// Conditional/optional traps are ignored.
    /// </summary>
    public static IReadOnlyList<RosReadCommandId> ListFailedRequired(
        IReadOnlyDictionary<RosReadCommandId, RosReadCommandResult> commandResults)
    {
        ArgumentNullException.ThrowIfNull(commandResults);
        List<RosReadCommandId> failed = [];
        foreach (RosReadCommandId commandId in RouterOsDiscoveryCommandCatalog.All)
        {
            RosReadCommandDefinition definition = RosReadCommandRegistry.Get(commandId);
            if (definition.Requirement != RosRequirement.Required)
            {
                continue;
            }

            if (!commandResults.TryGetValue(commandId, out RosReadCommandResult? result)
                || !result.IsSuccess)
            {
                failed.Add(commandId);
            }
        }

        return failed;
    }

    /// <summary>Throws when any catalog-required command is missing or unsuccessful.</summary>
    public static void EnsureRequiredSucceeded(
        IReadOnlyDictionary<RosReadCommandId, RosReadCommandResult> commandResults)
    {
        IReadOnlyList<RosReadCommandId> failed = ListFailedRequired(commandResults);
        if (failed.Count == 0)
        {
            return;
        }

        string names = string.Join(',', failed.Select(static id => id.ToString()));
        throw new RequiredSectionCaptureException(
            $"{FailureCodePrefix}: required discovery read(s) failed: {names}");
    }
}

/// <summary>Raised when a required RouterOS discovery section cannot be read.</summary>
public sealed class RequiredSectionCaptureException : InvalidOperationException
{
    public RequiredSectionCaptureException(string message)
        : base(message)
    {
    }
}
