# MTDirector documentation index

**Release:** `v0.2.0` (2026-08-24) — MVP + Post-MVP M7 CLOSED (issue-queue).  
**§3.C NEXT = W7-431 (#1256)** — PLAN-63 seed → EVID-LIVE-01 (OWN-HB-01 DONE); wave B Layer C deferred ([`ROADMAP.md`](../ROADMAP.md) §3.C).  
**Pilot:** P2 read + write **code rows** CLOSED; production-safe write **NOT PROVEN** — [`operations/pilot-runbook.md`](operations/pilot-runbook.md), [PLAN-62](planning/plan-62-audit-remediation-acd0759.md), [PLAN-63](planning/plan-63-reaudit-residuals-wave-a.md).  
**Alignment P0–P2:** W1–W4 / W2.1–W2.2 **DONE**. CRS/physical lab runner stays ops (not a §3 stop-gate).

## Planning and tracking

| Document | Purpose |
|----------|---------|
| [`ROADMAP.md`](../ROADMAP.md) | Linear atomic task queue (normative execution order) |
| [`planning/continuous-queue-plan.md`](planning/continuous-queue-plan.md) | Continuous §3.C process + PLAN-02…PLAN-63 index |
| [`planning/plan-03-quality-gates.md`](planning/plan-03-quality-gates.md) | PLAN-03: quality-gate tranche (import-graph / docs smoke / anti-stub) |
| [`planning/plan-04-contract-tests.md`](planning/plan-04-contract-tests.md) | PLAN-04: contract-test / GrpcHost Living Spec tranche |
| [`planning/plan-05-desktop-operator-surface.md`](planning/plan-05-desktop-operator-surface.md) · [`plan-06-incident-desktop-operator-surface.md`](planning/plan-06-incident-desktop-operator-surface.md) · [`plan-07-core-mvp-desktop-operator-surface.md`](planning/plan-07-core-mvp-desktop-operator-surface.md) · [`plan-08-desktop-secondary-operator-surface.md`](planning/plan-08-desktop-secondary-operator-surface.md) · [`plan-09-desktop-connection-status-operator-surface.md`](planning/plan-09-desktop-connection-status-operator-surface.md) · [`plan-10-desktop-shell-policies-authoring-depth.md`](planning/plan-10-desktop-shell-policies-authoring-depth.md) · [`plan-11-desktop-policies-review-compose-lifecycle.md`](planning/plan-11-desktop-policies-review-compose-lifecycle.md) · [`plan-12-desktop-policies-residual-lifecycle.md`](planning/plan-12-desktop-policies-residual-lifecycle.md) · [`plan-13-desktop-layout-density.md`](planning/plan-13-desktop-layout-density.md) · [`plan-14-desktop-avalonia-placeholder-incident-surface.md`](planning/plan-14-desktop-avalonia-placeholder-incident-surface.md) · [`plan-15-desktop-incident-mfc-field-style-hygiene.md`](planning/plan-15-desktop-incident-mfc-field-style-hygiene.md) · [`plan-16-desktop-incident-automation-properties.md`](planning/plan-16-desktop-incident-automation-properties.md) · [`plan-17-desktop-incident-bind-action-automation.md`](planning/plan-17-desktop-incident-bind-action-automation.md) · [`plan-18-desktop-incident-ingest-action-automation.md`](planning/plan-18-desktop-incident-ingest-action-automation.md) · [`plan-19-desktop-shell-connect-disconnect-automation.md`](planning/plan-19-desktop-shell-connect-disconnect-automation.md) · [`plan-20-desktop-policies-lifecycle-action-automation.md`](planning/plan-20-desktop-policies-lifecycle-action-automation.md) · [`plan-21-desktop-policies-authoring-residual-automation.md`](planning/plan-21-desktop-policies-authoring-residual-automation.md) · [`plan-22-desktop-policies-ack-record-automation.md`](planning/plan-22-desktop-policies-ack-record-automation.md) · [`plan-23-desktop-policies-catalog-object-automation.md`](planning/plan-23-desktop-policies-catalog-object-automation.md) · [`plan-24-desktop-onboarding-deployment-automation.md`](planning/plan-24-desktop-onboarding-deployment-automation.md) · [`plan-25-desktop-inventory-zones-add-router-automation.md`](planning/plan-25-desktop-inventory-zones-add-router-automation.md) · [`plan-26-code-audit-remediation-11cb746.md`](planning/plan-26-code-audit-remediation-11cb746.md) · [`plan-27-desktop-snapshot-panel-automation.md`](planning/plan-27-desktop-snapshot-panel-automation.md) · [`plan-28-desktop-residual-field-control-automation.md`](planning/plan-28-desktop-residual-field-control-automation.md) · [`plan-29-desktop-connection-health-reconnect.md`](planning/plan-29-desktop-connection-health-reconnect.md) | PLAN-05…29 Desktop / incident / a11y (**COMPLETE**) |
| [`planning/plan-30-watch-owner-acl-hub-backpressure.md`](planning/plan-30-watch-owner-acl-hub-backpressure.md) · [`plan-31-desktop-residual-listbox-readonly-a11y.md`](planning/plan-31-desktop-residual-listbox-readonly-a11y.md) · [`plan-32-controller-host-process-packaging.md`](planning/plan-32-controller-host-process-packaging.md) · [`plan-33-desktop-inventory-treeview-a11y.md`](planning/plan-33-desktop-inventory-treeview-a11y.md) · [`plan-34-desktop-operator-launch-packaging.md`](planning/plan-34-desktop-operator-launch-packaging.md) · [`plan-35-desktop-launch-template-publish-bundling.md`](planning/plan-35-desktop-launch-template-publish-bundling.md) · [`plan-36-controller-host-template-publish-bundling.md`](planning/plan-36-controller-host-template-publish-bundling.md) · [`plan-37-controller-host-env-sample-packaging.md`](planning/plan-37-controller-host-env-sample-packaging.md) · [`plan-38-controller-host-sysusers-tmpfiles-packaging.md`](planning/plan-38-controller-host-sysusers-tmpfiles-packaging.md) · [`plan-39-controller-host-operator-doc-packaging.md`](planning/plan-39-controller-host-operator-doc-packaging.md) · [`plan-40-controller-host-journald-syslog-identity.md`](planning/plan-40-controller-host-journald-syslog-identity.md) · [`plan-41-release-signing-crypto-gpg-sigstore.md`](planning/plan-41-release-signing-crypto-gpg-sigstore.md) · [`plan-42-controller-http-health-probes.md`](planning/plan-42-controller-http-health-probes.md) · [`plan-43-controller-http-metrics-otel.md`](planning/plan-43-controller-http-metrics-otel.md) · [`plan-44-controller-otel-tracing.md`](planning/plan-44-controller-otel-tracing.md) · [`plan-45-controller-log-trace-correlation.md`](planning/plan-45-controller-log-trace-correlation.md) · [`plan-46-controller-otel-resource-identity.md`](planning/plan-46-controller-otel-resource-identity.md) · [`plan-47-controller-grpc-message-size-limits.md`](planning/plan-47-controller-grpc-message-size-limits.md) · [`plan-48-controller-kestrel-request-body-limits.md`](planning/plan-48-controller-kestrel-request-body-limits.md) · [`plan-49-controller-grpc-http2-keepalive.md`](planning/plan-49-controller-grpc-http2-keepalive.md) · [`plan-50-controller-kestrel-min-data-rate.md`](planning/plan-50-controller-kestrel-min-data-rate.md) · [`plan-51-desktop-grpc-unary-deadline.md`](planning/plan-51-desktop-grpc-unary-deadline.md) · [`plan-52-desktop-grpc-error-detail.md`](planning/plan-52-desktop-grpc-error-detail.md) · [`plan-53-controller-fault-correlation-log.md`](planning/plan-53-controller-fault-correlation-log.md) · [`plan-54-desktop-connection-status-fault-text.md`](planning/plan-54-desktop-connection-status-fault-text.md) · [`plan-55-capture-progress-fault-correlation.md`](planning/plan-55-capture-progress-fault-correlation.md) · [`plan-56-vrrp-pair-status-fault-text.md`](planning/plan-56-vrrp-pair-status-fault-text.md) · [`plan-57-vrrp-capture-progress-fault-text.md`](planning/plan-57-vrrp-capture-progress-fault-text.md) · [`plan-58-desktop-panel-status-fault-text.md`](planning/plan-58-desktop-panel-status-fault-text.md) · [`plan-59-snapshot-failed-errortext-correlation.md`](planning/plan-59-snapshot-failed-errortext-correlation.md) · [`plan-60-desktop-service-rpc-fault-text.md`](planning/plan-60-desktop-service-rpc-fault-text.md) · [`plan-61-desktop-connection-disconnected-fault-text.md`](planning/plan-61-desktop-connection-disconnected-fault-text.md) · [`plan-62-audit-remediation-acd0759.md`](planning/plan-62-audit-remediation-acd0759.md) | PLAN-30…62 packaging / OTEL / fault-text / audit remediation (**COMPLETE**; **§3.C NEXT = none**) |
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
