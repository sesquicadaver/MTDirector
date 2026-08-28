# Read-only pilot runbook (P2)

Enable production RouterOS **read path** only after connection profiles, secrets, and lab CHR are ready.
Write path (`Mfc:RouterOs:WriteEnabled`) is a separate fail-closed gate — see [`controller-configuration.md`](controller-configuration.md). Full write-path pilot checklist is **P2-11**.

## Prerequisites

- PostgreSQL reachable; Controller migrated (`dotnet run --project src/Mfc.Controller -- --migrate-only`).
- Device registered with encrypted connection profile (INTERNAL_CA or SPKI pin).
- Lab CHR (or pilot router) reachable on API-SSL (`8729`); trust material configured.
- Desktop or gRPC client with actor metadata (`x-mfc-actor`).

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

## Rollback

Set `Mfc:RouterOs:Enabled=false` and restart Controller. No database migration rollback required.

## References

- [`controller-configuration.md`](controller-configuration.md) — full config keys
- [`known-limitations.md`](../release/known-limitations.md) — remaining stubs (onboarding/deploy)
- [`ROADMAP.md`](../../ROADMAP.md) §3.B5 — P2 pilot queue
