# MTDirector documentation index

**Release:** `v0.2.0` (2026-08-24) — MVP + Post-MVP M7 CLOSED.  
**Pilot:** P2 read + write path CLOSED — [`operations/pilot-runbook.md`](operations/pilot-runbook.md).  
**Next delivery (§3):** **§3.C NEXT = W7-212 (#829)** (AUDIT-CTX-01 W7-212; queued: [#829](https://github.com/sesquicadaver/MTDirector/issues/829)/[#830](https://github.com/sesquicadaver/MTDirector/issues/830)) — W7-113 ([#626](https://github.com/sesquicadaver/MTDirector/issues/626)) **DONE**; W7-114 ([#628](https://github.com/sesquicadaver/MTDirector/issues/628)) **DONE**; W7-112 ([#625](https://github.com/sesquicadaver/MTDirector/issues/625)) **DONE**; W7-111 ([#620](https://github.com/sesquicadaver/MTDirector/issues/620)) **DONE**; W7-110 ([#617](https://github.com/sesquicadaver/MTDirector/issues/617)) **DONE**; PLAN-10 **COMPLETE**; W7-109 ([#616](https://github.com/sesquicadaver/MTDirector/issues/616)) **DONE**; W7-108 ([#614](https://github.com/sesquicadaver/MTDirector/issues/614)) **DONE**; W7-107 ([#612](https://github.com/sesquicadaver/MTDirector/issues/612)) **DONE**; W7-106 ([#611](https://github.com/sesquicadaver/MTDirector/issues/611)) **DONE**; W7-105 ([#608](https://github.com/sesquicadaver/MTDirector/issues/608)) **DONE**; PLAN-10 inventory ([`plan-10-desktop-shell-policies-authoring-depth.md`](planning/plan-10-desktop-shell-policies-authoring-depth.md)); PLAN-11 ([`plan-11-desktop-policies-review-compose-lifecycle.md`](planning/plan-11-desktop-policies-review-compose-lifecycle.md)); PLAN-12 ([`plan-12-desktop-policies-residual-lifecycle.md`](planning/plan-12-desktop-policies-residual-lifecycle.md)); PLAN-13 ([`plan-13-desktop-layout-density.md`](planning/plan-13-desktop-layout-density.md)); PLAN-09 **COMPLETE** ([`plan-09-desktop-connection-status-operator-surface.md`](planning/plan-09-desktop-connection-status-operator-surface.md)); PLAN-08 **COMPLETE** ([`plan-08-desktop-secondary-operator-surface.md`](planning/plan-08-desktop-secondary-operator-surface.md)); PLAN-07 **COMPLETE**; PLAN-06 **COMPLETE**; PLAN-05 **COMPLETE**. CRS/physical lab runner stays ops.  
**Alignment P0–P2:** W1–W4 / W2.1–W2.2 **DONE** (`877a529`).

## Planning and tracking

| Document | Purpose |
|----------|---------|
| [`ROADMAP.md`](../ROADMAP.md) | Linear atomic task queue (normative execution order) |
| [`planning/continuous-queue-plan.md`](planning/continuous-queue-plan.md) | PLAN-02 continuous §3.C + PLAN-03…PLAN-08 |
| [`planning/plan-03-quality-gates.md`](planning/plan-03-quality-gates.md) | PLAN-03: quality-gate tranche (import-graph / docs smoke / anti-stub) |
| [`planning/plan-04-contract-tests.md`](planning/plan-04-contract-tests.md) | PLAN-04: contract-test / GrpcHost Living Spec tranche |
| [`planning/plan-05-desktop-operator-surface.md`](planning/plan-05-desktop-operator-surface.md) | PLAN-05: Desktop operator-surface Living Spec tranche (**COMPLETE**) |
| [`planning/plan-06-incident-desktop-operator-surface.md`](planning/plan-06-incident-desktop-operator-surface.md) | PLAN-06: Incident Desktop operator-surface Living Spec tranche (**COMPLETE**) |
| [`planning/plan-07-core-mvp-desktop-operator-surface.md`](planning/plan-07-core-mvp-desktop-operator-surface.md) | PLAN-07: Core MVP Desktop operator-surface Living Spec tranche (**COMPLETE**) |
| [`planning/plan-08-desktop-secondary-operator-surface.md`](planning/plan-08-desktop-secondary-operator-surface.md) | PLAN-08: Desktop secondary operator-surface Living Spec tranche |
| [`planning/plan-12-desktop-policies-residual-lifecycle.md`](planning/plan-12-desktop-policies-residual-lifecycle.md) | PLAN-12: Desktop Policies residual lifecycle (**COMPLETE**) |
| [`planning/plan-13-desktop-layout-density.md`](planning/plan-13-desktop-layout-density.md) | PLAN-13: Desktop layout density Living Spec tranche (**COMPLETE**) |
| [`planning/plan-14-desktop-avalonia-placeholder-incident-surface.md`](planning/plan-14-desktop-avalonia-placeholder-incident-surface.md) | PLAN-14: Desktop Avalonia PlaceholderText / Incident surface (**COMPLETE**) |
| [`planning/plan-15-desktop-incident-mfc-field-style-hygiene.md`](planning/plan-15-desktop-incident-mfc-field-style-hygiene.md) | PLAN-15: Desktop Incident mfc-field style hygiene (**COMPLETE**) |
| [`planning/plan-16-desktop-incident-automation-properties.md`](planning/plan-16-desktop-incident-automation-properties.md) | PLAN-16: Desktop Incident AutomationProperties accessible-name (**COMPLETE**) |
| [`planning/plan-17-desktop-incident-bind-action-automation.md`](planning/plan-17-desktop-incident-bind-action-automation.md) | PLAN-17: Desktop Incident bind-action AutomationProperties (**COMPLETE**) |
| [`planning/plan-18-desktop-incident-ingest-action-automation.md`](planning/plan-18-desktop-incident-ingest-action-automation.md) | PLAN-18: Desktop Incident ingest-action AutomationProperties (**COMPLETE**) |
| [`planning/plan-19-desktop-shell-connect-disconnect-automation.md`](planning/plan-19-desktop-shell-connect-disconnect-automation.md) | PLAN-19: Desktop shell Connect/Disconnect AutomationProperties (**COMPLETE**) |
| [`planning/plan-20-desktop-policies-lifecycle-action-automation.md`](planning/plan-20-desktop-policies-lifecycle-action-automation.md) | PLAN-20: Desktop Policies lifecycle-action AutomationProperties (**COMPLETE**) |
| [`planning/plan-21-desktop-policies-authoring-residual-automation.md`](planning/plan-21-desktop-policies-authoring-residual-automation.md) | PLAN-21: Desktop Policies authoring residual AutomationProperties (**COMPLETE**) |
| [`planning/plan-22-desktop-policies-ack-record-automation.md`](planning/plan-22-desktop-policies-ack-record-automation.md) | PLAN-22: Desktop Policies acknowledge/record-analysis AutomationProperties (**COMPLETE**) |
| [`planning/plan-23-desktop-policies-catalog-object-automation.md`](planning/plan-23-desktop-policies-catalog-object-automation.md) | PLAN-23: Desktop Policies catalog/object AutomationProperties (**COMPLETE**) |
| [`planning/plan-24-desktop-onboarding-deployment-automation.md`](planning/plan-24-desktop-onboarding-deployment-automation.md) | PLAN-24: Desktop Onboarding/Deployment AutomationProperties (**COMPLETE**) |
| [`planning/plan-25-desktop-inventory-zones-add-router-automation.md`](planning/plan-25-desktop-inventory-zones-add-router-automation.md) | PLAN-25: Desktop Inventory/Zones AutomationProperties (**COMPLETE**) |
| [`planning/plan-26-code-audit-remediation-11cb746.md`](planning/plan-26-code-audit-remediation-11cb746.md) | PLAN-26: Code-audit remediation @ `11cb746` (inventory DONE; NEXT AUDIT-RULE-01) |
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
| [`development/docs-smoke.md`](development/docs-smoke.md) | QG-DOCS-01 weekly docs smoke checklist |
| [`development/livespec-matrix-gate.md`](development/livespec-matrix-gate.md) | QG-LIVESPEC-MATRIX-01 Living Spec matrix PR gate checklist |
| [`development/signing-gate.md`](development/signing-gate.md) | QG-SIGN-01 release signing residual checklist |
| [`development/desktop-ui-backend-alignment.md`](development/desktop-ui-backend-alignment.md) | Desktop UI ↔ Controller data alignment (P0–P3); W6-01…W6-03 **DONE**; residual CRS lab ops |
| [`development/desktop-layout.md`](development/desktop-layout.md) | Desktop layout density tokens (PLAN-13 / DESK-LAYOUT-00) |
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
