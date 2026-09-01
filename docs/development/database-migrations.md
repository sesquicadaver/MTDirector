# Database migrations

PostgreSQL is the only supported production database. SQLite is forbidden.

## Applied schema milestones

| Key (`schema_metadata`) | Value | Migration |
|-------------------------|-------|-----------|
| `bootstrap.schema` | `m0-07` | `InitialBootstrap` |
| `inventory.snapshot.schema` | `m1-03` | `InventorySnapshotSchema` |
| `snapshot.persist.schema` | `m1-23` | `SnapshotCaptureSectionsM123` |
| `policy.lifecycle.schema` | `m2-01` | `PolicyLifecycleSchema` |
| `policy.zone_bindings.schema` | `m2-05` | `ZoneBindingsSchemaM205` |
| `policy.approval.schema` | `m2-17` | `PolicyApprovalBindingSchemaM217` |
| `compiler.filter_artifacts.schema` | `m3-07` | `FilterArtifactsSchemaM307` |
| `onboarding.schema` | `m5-01` | `OnboardingSchemaM501` |
| `deployment.schema` | `m4-01` | `DeploymentSchemaM401` |

M1 inventory/snapshot tables follow Vertical Slice §8 (`sites`, `nodes`, `devices`, `device_connection_profiles`, `capture_operations`, `snapshot_captures`, `snapshot_payloads`) plus Canonical Spec §28.2 `snapshot_capture_sections` (M1-23). Topology tables from the early issue draft are **not** persisted in M1.

M2-01 adds document-centric `policies` / `policy_revisions` (Policy Model §66): Brotli-compressed MFC-CJ1 payload, content hash over uncompressed bytes, application DbContext blocks delete and approved payload mutation.

M2-05 adds desired catalog tables `zone_definitions` / `node_zone_bindings` (Policy Model §§20–21): unique `(owner_scope, owner_id, key)` and `(node_id, zone_id)`, RowVersion optimistic concurrency; `PolicyDocument.zone_definitions` remains empty until composition.

M2-17 adds append-only `policy_analysis_runs`, `warning_acknowledgments`, `policy_approvals` and mutable `policy_bindings` (Policy Model §§10, §66–§67). Findings and test outcomes live in the immutable run JSON payload (dedicated `policy_findings` / `policy_test_results` tables deferred). DbContext blocks UPDATE/DELETE of runs, acknowledgments, and approval votes; binding identity/hashes stay frozen while state/row_version may change. Completing approval freezes `ApprovedAnalysisRunId` / `ApprovedBundleHash` on `policy_revisions`. Filtered unique indexes enforce at most one ACTIVE company baseline, one ACTIVE site/node overlay, and one ACTIVE binding per EXCEPTION policy. A PostgreSQL trigger (`mfc_enforce_exception_binding_cap`) enforces the 256 ACTIVE EXCEPTION cap per `ScopeId`. Completing approval votes and COMPANY/SITE/NODE binding replacement persist in one transaction.

M3-07 adds append-only content-addressed `filter_artifacts` keyed by `resource_hash` (Compiler Spec §6): Brotli-compressed MFC-CJ1 filter artifact body, provenance columns (logical/device-resolved/analysis/capability/profile hashes), DbContext blocks UPDATE/DELETE.

M5-01 adds `ManagementState` on `nodes`/`devices` (default UNMANAGED) and append-only `onboarding_plans` / `onboarding_device_plans` / `onboarding_anchor_placements`, mutable `onboarding_operations` (filtered unique nonterminal per `NodeId`) and write-ahead `onboarding_steps`. DbContext blocks plan mutation, terminal-operation identity changes, and verified/failed step identity changes.

M4-01 adds append-only `deployment_plans` / `deployment_device_plans`, mutable `deployment_operations` (filtered unique nonterminal per `NodeId`; terminal includes `NO_CHANGES`), `deployment_device_states`, unique `deployment_locks` (expired rows are not deleted), and write-ahead `deployment_steps`. DbContext blocks plan mutation, terminal-operation/device-state/step identity changes, and lock identity mutation (heartbeat/expiry may change).

W6-08 adds nullable `devices.LastObservedReachability` (`ObservedReachability` enum) so Unreachable/Reachable from DiscoverDevice survives Controller restart (check `ck_devices_last_observed_reachability`).

M7.1-02 adds upsertable `routing_assurance_states` keyed by `DeviceId` (FK → `devices`): distinct `ConfigurationHash` / `OperationalHash`, jsonb configuration and operational snapshots, and jsonb arrays for `RouteExpectations` / `RouteFindings` / `ResolutionTraces` (M7.1-03 traces; M7.1-06 expectation evaluation).

## Local PostgreSQL

```bash
docker compose -f testlab/postgres/compose.yml up -d
```

Default development connection (also in `appsettings.Development.json`):

```text
Host=127.0.0.1;Port=5432;Database=mfc;Username=mfc;Password=mfc_dev_only_change_me
```

Override with `MFC__Database__ConnectionString` for non-default setups. Never commit production passwords.

## Apply migrations

Controller does **not** migrate on normal startup.

```bash
dotnet run --project src/Mfc.Controller -- --environment Development --migrate-only
```

After success the process exits without starting gRPC.

## Startup schema guard

If mandatory migrations are pending, Controller fails to start with a clear error. Fix by running `--migrate-only`.

## Creating a new migration

```bash
dotnet tool restore
dotnet ef migrations add <Name> \
  --project src/Mfc.Infrastructure \
  --startup-project src/Mfc.Controller \
  --output-dir Persistence/Migrations
```

Commit the migration with the code change. Integration tests must cover empty-database apply and idempotent re-check.
