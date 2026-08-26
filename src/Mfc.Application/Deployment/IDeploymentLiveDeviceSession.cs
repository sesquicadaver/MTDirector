namespace Mfc.Application.Deployment;

/// <summary>
/// Live device ports opened for one deployment operation (standalone, VRRP member, rollback, recovery).
/// </summary>
public interface IDeploymentLiveDeviceSession : IStandaloneDeploymentDeviceRuntime, IDeploymentRollbackDeviceRuntime;
