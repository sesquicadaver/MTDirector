using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Policy;

namespace Mfc.Application.Policies;

/// <summary>
/// Unit-test seam: returns <see cref="OverrideCurrent"/> when set; otherwise echoes
/// <see cref="DependencyFingerprintRequest.FrozenRunFingerprint"/> (AUDIT-AN-02 CAS host).
/// </summary>
public sealed class PassthroughPolicyDependencyFingerprintCalculator : IPolicyDependencyFingerprintCalculator
{
    /// <summary>When set, forces server-computed current (simulates live dependency change).</summary>
    public Hash256? OverrideCurrent { get; set; }

    public Task<Hash256> ComputeCurrentAsync(
        DependencyFingerprintRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (OverrideCurrent is not null)
        {
            return Task.FromResult(OverrideCurrent);
        }

        if (request.FrozenRunFingerprint is not null)
        {
            return Task.FromResult(request.FrozenRunFingerprint);
        }

        Hash256 empty = Hash256.Create(new byte[Hash256.Size]);
        return Task.FromResult(PolicyApprovalHasher.HashDependencyFingerprint(new PolicyApprovalDependencyVector
        {
            CompanyBindingHash = empty,
            SiteBindingHash = empty,
            NodeBindingHash = empty,
            ActiveExceptionsHash = empty,
            ZoneBindingHash = empty,
            NodeMembershipHash = empty,
            RouterOsConfigurationHash = empty,
            CapabilityHash = empty,
            CompatibilityHash = empty,
            ManagementAccessProfileHash = empty,
            AnchorGuardContextHash = empty,
            AnalyzerVersion = request.AnalyzerVersion,
            PolicySchemaVersion = request.PolicySchemaVersion,
            PipelineVersion = request.PipelineVersion,
        }));
    }
}
