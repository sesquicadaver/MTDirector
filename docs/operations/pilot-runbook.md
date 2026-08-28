# RouterOS pilot runbook (P2)

Enable production RouterOS adapters only after connection profiles, secrets, and lab CHR are ready.
Read and write paths are **independent** fail-closed gates — see [`controller-configuration.md`](controller-configuration.md).

## Prerequisites

- PostgreSQL reachable; Controller migrated (`dotnet run --project src/Mfc.Controller -- --migrate-only`).
- Device registered with encrypted connection profile (INTERNAL_CA or SPKI pin).
- Lab CHR (or pilot router) reachable on API-SSL (`8729`); trust material configured.
- Desktop or gRPC client with actor metadata (`x-mfc-actor`).
- For write path: lab CHR isolated from production; confirm management path + VRRP topology if deploying dual members.

## Enable production read path

Set **`Mfc:RouterOs:Enabled=true`** (fail-closed default is `false`):

```bash
export MFC__RouterOs__Enabled=true
# or CLI: --Mfc:RouterOs:Enabled=true
```

Restart Controller. With the flag enabled, DI registers:

| Port | Implementation |
|------|----------------|
| `IRouterOsReadPort` | `RouterOsReadPort` |
| `ISnapshotCapturePort` | `RouterOsSnapshotCapturePort` |

With the flag **false** (CI default), `ValidateDeviceConnection` and `StartCapture` remain fail-closed on probe/capture stubs.

## Read-only pilot checklist

1. **Inventory** — `CreateSite` → `CreateNode` → `RegisterDevice` → `UpdateDeviceConnection`.
2. **Probe** — `ValidateDeviceConnection` returns identity + `SupportState` without RouterOS mutation.
3. **Capture** — `StartCapture` + `WatchCapture` completes; snapshot persisted in PostgreSQL.
4. **Diff** — `ListCaptures` → `CompareSnapshots` shows section-grouped semantic diff (no local recompute).
5. **Verify fail-closed** — set `RouterOs:Enabled=false`, restart; probe/capture must return not-configured errors again.

## Enable production write path

Set **`Mfc:RouterOs:WriteEnabled=true`** (fail-closed default is `false`). Independent of `Enabled`:

```bash
export MFC__RouterOs__WriteEnabled=true
# or CLI: --Mfc:RouterOs:WriteEnabled=true
```

Restart Controller. With the flag enabled, DI registers:

| Port | Implementation |
|------|----------------|
| `IOnboardingRuntime` | `RouterOsOnboardingRuntime` |
| `IDeploymentRuntime` | `RouterOsDeploymentRuntime` |
| `IWatchdogResidueCleanupPort` | `RouterOsWatchdogResidueCleanupPort` |

With the flag **false** (CI default), `StartOnboarding` / `StartDeployment` / residue cleanup remain fail-closed on NotConfigured stubs.

## Write-path pilot checklist (lab CHR)

Perform on an **isolated** lab CHR (or approved pilot pair). Keep Desktop/gRPC actor with `onboarding.write` / `deployment.write` permissions.

1. **Enable write gate** — `WriteEnabled=true`, restart; confirm Controller logs no DI errors.
2. **Baseline read** — with `Enabled=true`, capture a snapshot before mutation (optional but recommended).
3. **Onboarding** — create onboarding plan → `StartOnboarding` → watch progress → verify management state / anchors on device.
4. **Deploy** — compile filter artifact → create deployment plan → `StartDeployment` → confirm activation / committed state.
5. **Rollback** — `RollbackDeployment` (or crash-recovery path) restores previous anchors; watchdog residue cleaned.
6. **Residue cleanup** — operational job / cleanup path removes only temporary `mfc-rb-*` / `mfc-ob-*` / `mfc-cap-*` resources.
7. **Verify fail-closed** — set `RouterOs:WriteEnabled=false`, restart; `StartOnboarding` / `StartDeployment` must return not-configured errors again.

Live CHR matrix remains **OFF** in CI; this checklist is the operator DoD substitute until an isolated runner exists.

## Rollback

| Gate | Action |
|------|--------|
| Read | `Mfc:RouterOs:Enabled=false`, restart Controller |
| Write | `Mfc:RouterOs:WriteEnabled=false`, restart Controller |

No database migration rollback required for either gate.

## References

- [`controller-configuration.md`](controller-configuration.md) — full config keys (`Enabled`, `WriteEnabled`)
- [`known-limitations.md`](../release/known-limitations.md) — lab residuals
- [`ROADMAP.md`](../../ROADMAP.md) §3.B7 — P2 write-path queue
- Living Specs: `PilotReadinessLivingSpecTests` (read), `WritePathReadinessLivingSpecTests` (DI gate), `WritePathPilotLivingSpecTests` (pilot)
