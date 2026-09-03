using Mfc.Application.Deployment;
using Mfc.Domain;
using Mfc.Domain.Deployment;
using Mfc.Domain.Inventory;
using Mfc.Domain.Inventory.Primitives;
using Mfc.Domain.Onboarding;
using Mfc.Domain.Policy;

namespace Mfc.RouterOs.Deployment;

/// <summary>
/// Live VRRP member runtime over a single <see cref="RouterOsDeploymentDeviceSession"/> (P2-08 / M4-10).
/// </summary>
internal sealed class RouterOsVrrpMemberDeploymentRuntime : IVrrpMemberDeploymentRuntime
{
    private readonly RouterOsDeploymentDeviceSession _device;
    private readonly DeviceDeploymentPlan _devicePlan;
    private readonly DeploymentStagingArtifacts _artifacts;
    private readonly DateTimeOffset _routerClock;
    private DeploymentWatchdogBundle? _armed;

    public RouterOsVrrpMemberDeploymentRuntime(
        RouterOsDeploymentDeviceSession device,
        DeviceDeploymentPlan devicePlan,
        DeploymentStagingArtifacts artifacts,
        DateTimeOffset routerClock)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(devicePlan);
        ArgumentNullException.ThrowIfNull(artifacts);
        _device = device;
        _devicePlan = devicePlan;
        _artifacts = artifacts;
        _routerClock = routerClock;
        DeviceId = device.DeviceId;
    }

    public DeviceId DeviceId { get; }

    public Task<bool> IsReachableAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<VrrpMemberRoleSnapshot> ReadRoleSnapshotAsync(CancellationToken cancellationToken = default)
        => _device.ReadVrrpRoleSnapshotAsync(cancellationToken);

    public async Task PrecheckAsync(CancellationToken cancellationToken = default)
    {
        _ = await _device.Session.ReadManagedStateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StageArtifactAsync(CancellationToken cancellationToken = default)
    {
        foreach (AddressListArtifactDraft list in _artifacts.AddressLists)
        {
            AddressListStagingResult staged = await StageAddressListUseCase.ExecuteAsync(
                list,
                _device.Session,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!staged.Succeeded)
            {
                throw new DomainInvariantException(staged.Code ?? DeploymentCodes.StagingResourceCollision);
            }
        }

        if (_artifacts.Chains.Count > 0)
        {
            DetachedChainsStagingResult stagedChains = await StageDetachedChainsUseCase.ExecuteAsync(
                _artifacts.Chains,
                _device.Session,
                activeRootChainNames: null,
                cancellationToken).ConfigureAwait(false);
            if (!stagedChains.Succeeded || !stagedChains.ArtifactStaged)
            {
                throw new DomainInvariantException(stagedChains.Code ?? DeploymentCodes.StagingResourceCollision);
            }
        }
    }

    public async Task ArmWatchdogAsync(CancellationToken cancellationToken = default)
    {
        DeploymentSystemNameFacts names = await _device.ReadSystemNamesAsync(cancellationToken).ConfigureAwait(false);
        DeploymentWatchdogPlanResult planned = PlanDeploymentWatchdogUseCase.PlanWatchdog(
            _device.OperationId,
            _devicePlan,
            names);
        if (planned.HasBlockers || planned.Watchdog is null)
        {
            string code = planned.Findings.Count > 0
                ? planned.Findings[0].Code
                : DeploymentCodes.WatchdogArmFailed;
            throw new DomainInvariantException(code);
        }

        DeploymentWatchdogExecutionResult arm = await _device.Watchdog.ArmWatchdogAsync(
            planned.Watchdog,
            _routerClock,
            _devicePlan.RollbackTtl,
            cancellationToken).ConfigureAwait(false);
        if (!arm.Succeeded)
        {
            throw new DomainInvariantException(arm.Code);
        }

        _armed = planned.Watchdog;
    }

    public async Task ActivateAsync(CancellationToken cancellationToken = default)
    {
        AnchorActivationResult activated = await ActivateAnchorsUseCase.ExecuteAsync(
            _devicePlan,
            _device.Session,
            () => _devicePlan.RollbackTtl,
            cancellationToken).ConfigureAwait(false);
        if (!activated.Succeeded)
        {
            throw new DomainInvariantException(activated.Code ?? DeploymentCodes.AnchorSetFailed);
        }
    }

    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        if (_armed is null)
        {
            throw new DomainInvariantException(DeploymentCodes.WatchdogNotArmed);
        }

        DeploymentVerificationResult verified = await VerifyDeploymentActivationUseCase.ExecuteAsync(
            _devicePlan,
            priorSessionIdentity: _device.Session,
            _device.FreshSessions,
            _devicePlan.NewArtifactHash,
            _armed,
            _devicePlan.RollbackTtl,
            observeFromArtifact: _artifacts.SealedArtifact,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!verified.Succeeded)
        {
            throw new DomainInvariantException(verified.Code ?? DeploymentCodes.DeploymentProbeFailed);
        }
    }

    public async Task DisarmWatchdogAsync(CancellationToken cancellationToken = default)
    {
        if (_armed is null)
        {
            return;
        }

        DeploymentWatchdogExecutionResult disarmed = await _device.Watchdog.DisarmWatchdogAsync(
            _armed,
            _devicePlan.RollbackTtl,
            cancellationToken).ConfigureAwait(false);
        if (!disarmed.Succeeded)
        {
            throw new DomainInvariantException(disarmed.Code);
        }
    }

    public async Task RollbackActivationAsync(CancellationToken cancellationToken = default)
    {
        foreach (AnchorKey key in _devicePlan.AnchorRollbackOrder)
        {
            AnchorTarget old = _devicePlan.OldAnchorTargets.Single(t => t.Key.Equals(key));
            DeploymentWriteExecutionResult restored = await _device.Session.SetAnchorTargetAsync(
                new AnchorTargetWrite(old.Key.Family, old.Key.Chain, old.JumpTarget),
                cancellationToken).ConfigureAwait(false);
            if (!restored.Succeeded)
            {
                throw new DomainInvariantException(DeploymentCodes.RecoveryRequired);
            }
        }

        if (_armed is not null)
        {
            _ = await _device.Watchdog.DisarmWatchdogAsync(_armed, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
