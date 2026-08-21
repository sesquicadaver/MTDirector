# Recovery

Part of the MVP operations set ([`operations-manual.md`](operations-manual.md), [`../release/mvp-acceptance.md`](../release/mvp-acceptance.md)).

## Database

1. Stop Controller instances that write to the affected database.
2. Restore PostgreSQL from the last known-good backup (PITR if configured).
3. Confirm `__EFMigrationsHistory` matches the deployed binary’s migrations (or re-apply via EF migrations bundle from `scripts/release/create-migration-bundle.sh`).
4. Start Controller (schema guard must pass). Do **not** run experimental `Down()` migrations.
5. Acceptance coverage: Integration `SecurityBackupRestoreAcceptanceTests` (M6-08 AC11–14) runs in-container `pg_dump`/`pg_restore` against Testcontainers Postgres and checks snapshot hashes, active artifact refs, and nonterminal recovery.

## Controller process

1. Inspect JSON logs (secrets should already be redacted).
2. Validate `Mfc` configuration (listen URL, TLS, connection string, master-key provider).
3. If startup cites pending migrations, run `--migrate-only` in a controlled window, then restart.

## CHR / RouterOS lab

1. Follow topology `cleanup` then `reset` in `testlab/chr/topologies/*/topology.json`.
2. Wipe ephemeral credentials under `testlab/chr/private/run-*` (gitignored).
3. Never restore lab VMs from production exports.

## Secrets

If a master key or RouterOS credential may be exposed: rotate the master-key provider material, re-wrap DEKs as implemented in later milestones, and revoke device accounts used by Controller.
