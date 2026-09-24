using Mfc.Application.Common;
using Mfc.Application.Policies;
using Mfc.Domain.Deployment;
using Mfc.Domain.Policy;

namespace Mfc.Application.Incident;

/// <summary>
/// AUDIT-M7-01 / F13: caller DevicePlans must match sealed compile artifact resource hashes.
/// </summary>
public static class IncidentCompileDevicePlanAlignment
{
    public const string MismatchCode = "INCIDENT_DEVICE_PLAN_COMPILE_MISMATCH";

    public static ApplicationError? EnsureMatch(
        IReadOnlyList<DeviceDeploymentPlan> devicePlans,
        IReadOnlyList<FilterArtifactSummaryView> artifacts)
    {
        ArgumentNullException.ThrowIfNull(devicePlans);
        ArgumentNullException.ThrowIfNull(artifacts);

        if (artifacts.Count == 0)
        {
            return new ApplicationError(
                MismatchCode,
                "Compile produced no sealed filter artifacts for incident overlay plan.");
        }

        if (devicePlans.Count != artifacts.Count)
        {
            return new ApplicationError(
                MismatchCode,
                $"DevicePlans count ({devicePlans.Count}) must equal compiled artifact count ({artifacts.Count}).");
        }

        Dictionary<Guid, byte[]> byDevice = artifacts.ToDictionary(
            static a => a.DeviceId,
            static a => a.ResourceHash);

        foreach (DeviceDeploymentPlan plan in devicePlans)
        {
            if (!byDevice.TryGetValue(plan.DeviceId.Value, out byte[]? expected))
            {
                return new ApplicationError(
                    MismatchCode,
                    $"DevicePlan '{plan.DeviceId}' has no matching sealed compile artifact.");
            }

            if (!plan.NewArtifactHash.Bytes.SequenceEqual(expected))
            {
                return new ApplicationError(
                    MismatchCode,
                    $"DevicePlan '{plan.DeviceId}' NewArtifactHash must equal sealed compile ResourceHash.");
            }
        }

        return null;
    }
}
