# MTDirector documentation index

**Release:** `v0.2.0` (2026-08-24) — MVP + Post-MVP M7 CLOSED.  
**Pilot:** P2 read + write path CLOSED — [`operations/pilot-runbook.md`](operations/pilot-runbook.md).  
**Next delivery (§3):** **§3.C NEXT = W7-02 (#402)** — W7-01 ([#401](https://github.com/sesquicadaver/MTDirector/issues/401)) **DONE**. CRS/physical lab runner stays ops.  
**Alignment P0–P2:** W1–W4 / W2.1–W2.2 **DONE** (`877a529`).

## Planning and tracking

| Document | Purpose |
|----------|---------|
| [`ROADMAP.md`](../ROADMAP.md) | Linear atomic task queue (normative execution order) |
| [`planning/continuous-queue-plan.md`](planning/continuous-queue-plan.md) | PLAN-02: continuous §3.C (no phase-stop idle) |
| [`ISSUES.md`](../ISSUES.md) | Logical ID → GitHub issue mapping |
| [`CHANGELOG.md`](../CHANGELOG.md) | Release history |

## Normative specifications

Authoritative ТЗ and Issue Sets live in the repository root and are indexed in [`specs/README.md`](specs/README.md). Do not duplicate normative MUST/SHALL text under `docs/` — link instead.

## Architecture

| Document | Purpose |
|----------|---------|
| [`architecture/overview.md`](architecture/overview.md) | Module map + ADR index |
| [`architecture/adr/README.md`](architecture/adr/README.md) | Architecture Decision Records |

## Development

| Document | Purpose |
|----------|---------|
| [`development/local-environment.md`](development/local-environment.md) | Workstation bootstrap |
| [`development/testing.md`](development/testing.md) | Living Specification matrices (ТЗ → module → tests) |
| [`development/desktop-ui-backend-alignment.md`](development/desktop-ui-backend-alignment.md) | Desktop UI ↔ Controller data alignment (P0–P3); W6-01…W6-03 **DONE**; residual CRS lab ops |
| [`development/ci.md`](development/ci.md) | CI workflow and gates |
| [`development/git-workflow.md`](development/git-workflow.md) | Branch/PR process |
| [`development/database-migrations.md`](development/database-migrations.md) | EF migrations |
| [`development/connection-profiles.md`](development/connection-profiles.md) | Connection profiles + Desktop Add router |
| [`development/snapshots-and-diff.md`](development/snapshots-and-diff.md) | Snapshot capture / semantic diff operator notes |
| [`development/chr-lab.md`](development/chr-lab.md) | CHR lab isolation |
| [`development/troubleshooting-read-path.md`](development/troubleshooting-read-path.md) | Read-path diagnostics |
| [`development/m1-vertical-slice-acceptance.md`](development/m1-vertical-slice-acceptance.md) | M1 acceptance report |
| [`development/support-manifest.md`](development/support-manifest.md) | Hardware / RouterOS support matrix |

## Operations

| Document | Purpose |
|----------|---------|
| [`operations/installation.md`](operations/installation.md) | Controller + Desktop install (short) |
| [`operations/controller-configuration.md`](operations/controller-configuration.md) | `Mfc` configuration keys |
| [`operations/pilot-runbook.md`](operations/pilot-runbook.md) | Lab/production RouterOS read + write pilot |
| [`operations/prerequisite-checklist.md`](operations/prerequisite-checklist.md) | RouterOS device gates |
| [`operations/operations-manual.md`](operations/operations-manual.md) | Day-2 operator guide |
| [`operations/recovery.md`](operations/recovery.md) | Backup / restore / crash recovery |
| [`operations/database-migrations.md`](operations/database-migrations.md) | Production migration bundle |

## Release and acceptance

| Document | Purpose |
|----------|---------|
| [`release/mvp-acceptance.md`](release/mvp-acceptance.md) | M6-09 acceptance package |
| [`release/release-gates.md`](release/release-gates.md) | Pre-release checklist |
| [`release/known-limitations.md`](release/known-limitations.md) | Intentional scope residuals |
| [`release/readiness.md`](release/readiness.md) | Project readiness assessment (milestones + pilot status) |
| [`release/packaging.md`](release/packaging.md) | Artifact packaging |
| [`release/RELEASE_SIGNING.md`](release/RELEASE_SIGNING.md) | Signing / attestation policy |

## Test lab

| Path | Purpose |
|------|---------|
| [`testlab/postgres/compose.yml`](../testlab/postgres/compose.yml) | Local PostgreSQL |
| [`testlab/chr/README.md`](../testlab/chr/README.md) | CHR acceptance lab |

## HOWTO

| Document | Purpose |
|----------|---------|
| [`howto/build-and-run.md`](howto/build-and-run.md) | Build, package, and run Controller + Desktop on Linux / Windows |
