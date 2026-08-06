# Database migrations (operations)

Canonical developer workflow: [../development/database-migrations.md](../development/database-migrations.md).

## Production rules

1. Forward-only EF Core migrations in `Mfc.Infrastructure`.
2. Apply with `Mfc.Controller --migrate-only` (process exits; gRPC does not start).
3. Normal Controller startup **fails** if mandatory migrations are pending.
4. Schema rollback = restore from backup, not automatic `Down()`.
5. Destructive migrations require a new ADR.

## Verification

- Empty database: migration applies bootstrap tables.
- Re-run migrate: no schema drift.
- Integration coverage: `tests/Mfc.IntegrationTests/Persistence`.
