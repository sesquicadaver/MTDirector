# ADR 0004: PostgreSQL as source of truth

- **Status:** Accepted
- **Date:** 2026-08-06
- **Deciders:** Architecture (Bootstrap Plan / MVP Technical Specification)

## Context

Controller needs durable inventory, snapshots, policies, deployments, audit, and secrets. Embedded databases or Desktop-local stores would fragment truth and weaken audit/concurrency guarantees.

## Decision

**PostgreSQL is the only supported production database:**

- EF Core + Npgsql in `Mfc.Infrastructure`; forward-only migrations.
- Controller does **not** auto-migrate on normal startup; operators run `Mfc.Controller --migrate-only`.
- SQLite (and other engines) are forbidden as production stand-ins, including in tests.
- Timestamps are UTC; `encrypted_secrets` has no plaintext column; `audit_events` is append-only at the application boundary.

## Consequences

- **Positive:** One operational model; strong constraints/indexes; Testcontainers/PG18 integration tests.
- **Negative:** Local and CI require Docker/PostgreSQL; schema changes need migrations + tests.
- **Follow-up:** Domain tables land in M1+; destructive migrations require a new ADR.
