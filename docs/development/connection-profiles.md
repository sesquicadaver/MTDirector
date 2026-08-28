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

Optional seed suggest (#314): select a registered **Device** in the tree → **Load MikroTik neighbors** (`ListNeighborCandidates`) → **Apply selected neighbor** to pre-fill host/port/display name → enter credentials → Submit as usual. Never auto-registers; Controller reads allowlisted `/ip/neighbor` on the seed only (not Desktop MNDP / LAN scan).

Equivalent gRPC:

1. `RegisterDevice` — management host/port only (after Site/Node exist).
2. `UpdateDeviceConnection` — username + password bytes + trust fields (idempotent, audited).
3. `ValidateDeviceConnection` — Controller-side probe (Issue Set `DiscoverDevice` alias).
4. `ListNeighborCandidates` — on-demand MikroTik candidates from seed `seed_device_id` (`discovery.read`).
5. Connection summaries — **Desktop-safe**: username, trust mode, timeouts; **no password / ciphertext**.

Secrets use AES-256-GCM envelopes under `Security:MasterKeyProvider` (`Development` only in Development environment).

## Desktop rules

- Desktop talks only to Controller Contracts (`mfc.v1`).
- No RouterOS host credentials in Desktop settings or logs (ADR 0005).
- Operator enters credentials once via Inventory **Add router** wizard (`UpdateDeviceConnection`); password is cleared from the form after success and never reloaded from Controller.
- Prefer selecting an existing Site/Node in the tree so pickers pre-fill.
- Neighbor suggest requires selecting an existing Device (seed) with a connection profile.
- Living Spec: `Ac2bInventoryAddRouterWizard…` + `NeighborCandidatesLivingSpecTests` + `AddRouterWizardViewModelTests` ([`testing.md`](testing.md) M6-04).

## Synthetic lab credentials

CHR provisioning scripts under `testlab/chr/scripts/` generate ephemeral credentials outside `Mfc.RouterOs`. Never commit passwords or PEMs under `testlab/chr/`.
