# Recovery

## Database

1. Stop Controller instances that write to the affected database.
2. Restore PostgreSQL from the last known-good backup (PITR if configured).
3. Confirm `__EFMigrationsHistory` matches the deployed binary’s migrations.
4. Start Controller (schema guard must pass). Do **not** run experimental `Down()` migrations.

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
