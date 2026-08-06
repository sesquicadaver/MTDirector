# ADR 0005: No direct Desktop→RouterOS access

- **Status:** Accepted
- **Date:** 2026-08-06
- **Deciders:** Architecture (Bootstrap Plan / TOR-1)

## Context

A Desktop GUI that talks directly to routers would bypass Controller policy, audit, RBAC, and secret handling. It would also force RouterOS credentials onto operator workstations.

## Decision

**Desktop must not access RouterOS or PostgreSQL directly:**

- `Mfc.Desktop` may reference only `Mfc.Contracts` (plus UI/client packages).
- All device and persistence operations go Desktop → gRPC → Controller → RouterOs / Infrastructure.
- Architecture tests fail the build if Desktop references Domain, Application, Infrastructure, or RouterOs.

## Consequences

- **Positive:** Single control plane; secrets stay server-side; clear security boundary.
- **Negative:** Offline Desktop features limited; Controller availability required for management actions.
- **Follow-up:** Keep Desktop connection shell health-only until authenticated gRPC APIs land in M1+.
