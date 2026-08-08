# Architecture overview

MTDirector (MikroTik Firewall Controller) is a **modular monolith**: one deployable Controller process, one Desktop client, and clear assembly boundaries.

```text
Avalonia Desktop  --gRPC/mTLS-->  ASP.NET Core Controller
                                        |
                         +--------------+--------------+
                         |              |              |
                      Domain      Infrastructure    RouterOs
                   (pure model)   (PostgreSQL/EF)  (API-SSL only)
```

Normative detail lives in `TOR-1.md` and the MVP specifications. This folder records **decisions** (ADRs), not a restatement of the full ТЗ.

## Domain progress (M1)

| Slice | Status | Location |
|-------|--------|----------|
| Inventory aggregates | Done (M1-01) | `src/Mfc.Domain/Inventory/` |
| Snapshot / capability VOs | Done (M1-02) | `src/Mfc.Domain/Snapshots/`, `src/Mfc.Domain/Capabilities/` |
| Persistence schema | Done (M1-03) | `sites`/`nodes`/`devices`, `capture_operations`, `snapshot_*` |
| Secure connection profiles | Done (M1-04) | AES-256-GCM envelope secrets + INTERNAL_CA/SPKI trust |

## ADRs

| ID | Title | Status |
|----|-------|--------|
| [0001](adr/0001-modular-monolith.md) | Modular monolith | Accepted |
| [0002](adr/0002-routeros-api-ssl.md) | RouterOS API-SSL transport | Accepted |
| [0003](adr/0003-node-deployment-atomicity.md) | Node deployment atomicity | Accepted |
| [0004](adr/0004-postgresql-source-of-truth.md) | PostgreSQL as source of truth | Accepted |
| [0005](adr/0005-no-direct-desktop-routeros-access.md) | No Desktop→RouterOS access | Accepted |

## Related development docs

- [Local environment](../development/local-environment.md)
- [Testing](../development/testing.md)
- [Database migrations](../development/database-migrations.md)
- [CHR lab isolation](../development/chr-lab.md)
- [CI](../development/ci.md)
