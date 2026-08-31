# Troubleshooting — read-path (M1)

## Controller will not start

| Symptom | Check |
|---------|--------|
| Schema guard fail | Run `--migrate-only`; confirm PG connection; see [`database-migrations.md`](database-migrations.md) |
| TLS / listen URL rejected | Production binds need `https://`; Dev may use loopback + `AllowInsecureLoopback` |
| Master key error | `Development` provider forbidden outside Development; `OsKeyStore` needs `MFC__Security__MasterKeyBase64` |

## Capture fails

| Code / status | Meaning | Action |
|---------------|---------|--------|
| `snapshot_unstable` / Aborted | Config changed across stable-read attempts | Retry; reduce concurrent admin changes on device |
| `snapshot_too_large` / ResourceExhausted | Raw/canonical payload over limit | Narrow discovery / raise only via product change |
| `dependency` / Unavailable | Transport/adapter fault | Check API-SSL reachability, trust mode, lab certs |
| `failed` / FailedPrecondition | Validation / missing profile | Ensure `UpdateDeviceConnection` completed |

Fault-injection matrix: `FullyQualifiedName~FaultInjection`.

## Desktop empty / stale tree

1. Confirm Controller health and `Desktop:ControllerEndpoint`.
2. Refresh inventory (single-flight cache may keep last good tree on error).
3. Desktop never talks to RouterOS directly — fix Controller/device path, not Desktop credentials.
4. To register devices: Inventory **Add router** (CreateSite → CreateNode → RegisterDevice → UpdateDeviceConnection) — [`connection-profiles.md`](connection-profiles.md).

## Diff looks wrong

- Diff is server-authoritative; Desktop must not recompute.
- Confirm both captures completed and belong to the same device.
- VRRP pair members a and b are different devices: capture each separately; Desktop Compare will not treat a-against-b as a pair diff (`SNAPSHOTS_FROM_DIFFERENT_DEVICES`).
- Observation-only changes appear under observation domain filters.

## CHR lab

- No live host → always-on in-process suites still pass.
- Provision scripts are outside product adapter (`testlab/chr/scripts/`).
- See [`chr-lab.md`](chr-lab.md) for isolation and reset.
