# CHR testlab (M0-09)

Isolated Cloud Hosted Router lab skeleton for future `Mfc.RouterOs.IntegrationTests`.

## Hard rules

- **Never commit** CHR disk images, MikroTik license files, VM snapshots, production exports, or private keys.
- Runner must have **no route to production**, no production DNS, and no production PKI.
- Each test environment generates its **own test CA** and **ephemeral credentials** (never reuse across jobs).
- After each job: wipe credentials, restore clean snapshots, destroy ephemeral artifacts.

## Layout

```text
testlab/chr/
  manifest.example.json   # copy → manifest.local.json (gitignored)
  fixtures/               # synthetic RouterOS exports only
  topologies/             # one directory per topology contract
  private/                # local-only images/keys (gitignored)
  README.md
```

## Topologies

| Directory | Purpose |
|-----------|---------|
| `standalone` | Single managed router |
| `multi-wan-failover` | Dual WAN, failover preference |
| `multi-wan-balanced` | Dual WAN, balanced |
| `vrrp-active-passive` | VRRP pair, stable master |
| `vrrp-split-master` | Split-brain / dual-master scenario |

Each topology directory contains `topology.json` with management/WAN addressing, reset, cleanup, and expected snapshot hash placeholders.

## Local setup

1. Obtain a legal CHR image offline (not from this repository).
2. Place it under `testlab/chr/private/` (gitignored).
3. `cp manifest.example.json manifest.local.json` and set `imageSha256` / `imagePath`.
4. Follow topology `reset` / `cleanup` procedures before and after runs.
5. For standalone M1-30: run `scripts/provision-standalone.sh`, then set `MFC_CHR_STANDALONE_HOST`.

See also [`docs/development/chr-lab.md`](../../docs/development/chr-lab.md).
