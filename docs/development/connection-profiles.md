# Connection profiles (read-path)

Controller stores RouterOS API-SSL credentials as encrypted connection profiles. Desktop and Application never receive plaintext passwords.

## Trust modes

| Mode | Use |
|------|-----|
| `INTERNAL_CA` | Lab/prod when devices present certs from an operator-managed CA (`CaProfileRef`) |
| `SPKI` | Pin leaf/public key SHA-256 (`PinnedSpkiSha256`) when CA distribution is unavailable |

See ADR [`0002-routeros-api-ssl.md`](../architecture/adr/0002-routeros-api-ssl.md).

## Lifecycle (gRPC)

1. `RegisterDevice` — management host/port only.
2. `UpdateDeviceConnection` — username + password bytes + trust fields (idempotent, audited).
3. `ValidateDeviceConnection` — Controller-side probe (Issue Set `DiscoverDevice` alias).
4. `GetDeviceConnection` / summaries — **Desktop-safe**: username, trust mode, timeouts; **no password / ciphertext**.

Secrets use AES-256-GCM envelopes under `Security:MasterKeyProvider` (`Development` only in Development environment).

## Desktop rules

- Desktop talks only to Controller Contracts (`mfc.v1`).
- No RouterOS host credentials in Desktop settings or logs (ADR 0005).
- Operator pastes credentials once into Controller via gRPC; Desktop never reloads them.

## Synthetic lab credentials

CHR provisioning scripts under `testlab/chr/scripts/` generate ephemeral credentials outside `Mfc.RouterOs`. Never commit passwords or PEMs under `testlab/chr/`.
