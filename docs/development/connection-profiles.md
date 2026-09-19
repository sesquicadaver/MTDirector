# Connection profiles

Controller stores RouterOS API-SSL credentials as encrypted connection profiles. Desktop and Application never receive plaintext passwords after `UpdateDeviceConnection`.

## Trust modes

| Mode | Use |
|------|-----|
| `INTERNAL_CA` | Lab/prod when devices present certs from an operator-managed CA (`CaProfileRef`) |
| `SPKI` | Pin leaf/public key SHA-256 (`PinnedSpkiSha256`) when CA distribution is unavailable |

See ADR [`0002-routeros-api-ssl.md`](../architecture/adr/0002-routeros-api-ssl.md).

## Lifecycle

Preferred Desktop path: Inventory **Add router** wizard → CreateSite (optional) → CreateNode (optional) → `RegisterDevice` → `UpdateDeviceConnection`.

Optional seed suggest (#314): select a registered **Device** in the tree → **Load MikroTik neighbors** (`ListNeighborCandidates`) → **Apply selected neighbor** to pre-fill host/port/display name → enter credentials → Submit as usual. Never auto-registers; Controller reads allowlisted `/ip/neighbor` on the seed only (not Desktop MNDP / LAN scan). Multi-homed MNDP rows for the same `identity` (mgmt + LAN + VRRP VIP) collapse to **one** candidate; the filter prefers an address in the seed device’s IPv4 `/24` when present.

Equivalent gRPC:

1. `RegisterDevice` — management host/port only (after Site/Node exist).
2. `UpdateDeviceConnection` — username + password bytes + trust fields (idempotent, audited).
3. `ValidateDeviceConnection` — Controller-side probe (Issue Set `DiscoverDevice` alias). Desktop **Probe** (W3.2) on Inventory device detail and Add router.
4. `ListNeighborCandidates` — on-demand MikroTik candidates from seed `seed_device_id` (`discovery.read`).
5. Connection summaries — **Desktop-safe**: username, trust mode, timeouts; **no password / ciphertext**.

Secrets use AES-256-GCM envelopes under `Security:MasterKeyProvider` (`Development` only in Development environment).

## Desktop rules

- Desktop talks only to Controller Contracts (`mfc.v1`).
- Unary Controller RPCs use `Desktop:UnaryCallTimeoutSeconds` (default **30**). `DesktopGrpcUnaryCall` rejects a non-positive value (fail-closed) and sets `CallOptions.Deadline`. Long-lived **Watch** streams (Capture, Deployment, Onboarding) are not deadline-bounded. Health probes stay on `HealthCheckTimeoutSeconds` (default **5**).
- Failed unary calls map Controller `mfc-error-detail-bin` into operator `ErrorText` via `DesktopRpcFaultText` (DESK-RPC-FAULT-01: code, correlation id, retryable when set). If the trailer is missing, `Status.Detail` is shown; an empty detail falls back to the status code (including `DeadlineExceeded`).
- Connect and reconnect `AuthenticationFailed` shell status (`LastError` → shell `ErrorText`) uses the same `DesktopRpcFaultText.Format` helper (DESK-CONN-FAULT-01). The correlation id is present when `mfc-error-detail-bin` is; an empty detail is never a blank status.
- VRRP pair consistency `RpcException`s put that same `DesktopRpcFaultText.Format` text on `VrrpPairStatusText` (DESK-VRRP-FAULT-01), so the pair status line matches `ErrorText` and can be joined to journald event 5301. Non-RPC failures keep the static status sentence, except an incomplete VRRP member capture: that path reuses `SnapshotViewerViewModel.FormatCaptureProgress` (DESK-VRRP-PROG-01) so the pair status and `ErrorText` include the same `(correlation {id})` suffix Snapshots already show.
- Drift load, GetDriftEvent, Audit load, Incident ingest, Incident assessment bind, Routing assurance load, and GetNodeWorkflow `RpcException`s put that same text on the panel status or readiness line (DESK-PANEL-FAULT-01). Non-RPC failures keep the static sentence.
- No RouterOS host credentials in Desktop settings or logs (ADR 0005).
- Operator enters credentials once via Inventory **Add router** wizard (`UpdateDeviceConnection`); password is cleared from the form after success and never reloaded from Controller.
- Prefer selecting an existing Site/Node in the tree so pickers pre-fill.
- Neighbor suggest requires selecting an existing Device (seed) with a connection profile.
- **Probe** (`ValidateDeviceConnection`) uses the selected Device, or the last device registered in this wizard session; shows observed identity, support state, and `routeros_mutated`.
- Living Spec: `Ac2bInventoryAddRouterWizard…` + `Ac2cInventoryAndAddRouterProbeValidateDeviceConnection` + `NeighborCandidatesLivingSpecTests` + `AddRouterWizardViewModelTests` ([`testing.md`](testing.md) M6-04).

## Synthetic lab credentials

CHR provisioning scripts under `testlab/chr/scripts/` generate ephemeral credentials outside `Mfc.RouterOs`. Never commit passwords or PEMs under `testlab/chr/`.
