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

1. **Inventory** — Desktop **Add router** wizard (`CreateSite` → `CreateNode` → `RegisterDevice` → `UpdateDeviceConnection`) or equivalent gRPC calls.
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

## Production mTLS checklist (W7-09)

Use when Desktop→Controller identity must be certificate-bound (not Development metadata actor). Pair with [`controller-configuration.md`](controller-configuration.md).

### Controller

1. **HTTPS bind** — `Mfc:Grpc:ListenAddress` uses `https://` and `Mfc:Security:RequireTls=true`.
2. **Client certificate mode** — set `Mfc:Grpc:ClientCertificateMode=RequireCertificate` (or `AllowCertificate` for staged rollout).
3. **Trusted client CA** — `Mfc:Security:TrustedCa:ProfilesDirectory` (absolute) + `Mfc:Security:TrustedCa:ClientCaProfileRef` pointing at the client-issuing profile under that directory; `RevocationMode` Online/Offline/NoCheck as appropriate.
4. **Restart** — Controller must fail closed at startup if Allow/Require is set but TrustedCa profile material / refs are missing.

### Desktop

1. **Endpoint** — `Desktop:ControllerEndpoint` matches the Controller HTTPS URL.
2. **Client PFX** — `Desktop:ClientCertificatePath` (+ optional `Desktop:ClientCertificatePassword`) presents a cert issued under the TrustedCa client profile.
3. **Actor chrome** — after Connect, status shows `Connected · actor: <CN>` (same string as `x-mfc-actor` via `DesktopGrpcActorResolver` / W7-08). Without a PFX, actor falls back to `Desktop:Actor`.
4. **Verify** — Connect succeeds; AuthenticationFailed / TlsError must not appear when trust material is correct.

### Fail-closed checks

- Controller with `RequireCertificate` and no/invalid client cert → Desktop TLS or auth failure (not silent anonymous actor).
- Desktop with wrong CA / expired PFX → Connect fails closed (no Connected status with spoofed actor).
- Production must keep `Mfc:Authentication:AllowMetadataActor=false`.

## Rollback

| Gate | Action |
|------|--------|
| Read | `Mfc:RouterOs:Enabled=false`, restart Controller |
| Write | `Mfc:RouterOs:WriteEnabled=false`, restart Controller |

No database migration rollback required for either gate.

## References

- [`controller-configuration.md`](controller-configuration.md) — full config keys (`Enabled`, `WriteEnabled`, mTLS)
- [`../development/connection-profiles.md`](../development/connection-profiles.md) — Desktop **Add router** / trust modes
- [`operations-manual.md`](operations-manual.md) — day-2 Desktop surfaces
- [`known-limitations.md`](../release/known-limitations.md) — lab residuals
- [`../release/readiness.md`](../release/readiness.md) — readiness baseline
- [`ROADMAP.md`](../../ROADMAP.md) §3.B7 — P2 write-path queue (**CLOSED** / empty); §3.C W7 mTLS
- Living Specs: `PilotReadinessLivingSpecTests` (read), `WritePathReadinessLivingSpecTests` (DI gate), `WritePathPilotLivingSpecTests` (pilot), `ProductionMtlsChecklistW709LivingSpecTests` (mTLS), `AddRouterWizardViewModelTests` / Desktop AC#2b
