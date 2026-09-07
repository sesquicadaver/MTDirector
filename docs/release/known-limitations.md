# Known limitations (MVP CLOSED + M7 CLOSED)

These limitations match the normative MVP scope lock (TOR-2 / ROADMAP §1). They are intentional residuals, not defects against M6-09 / N1-07 / M7 DoD.

## Closed milestones

- Intentional residual (W7-43 Living Spec lock): **N1-07 (#109)** — E2E/drift acceptance for container/VLAN/VETH/HW path classes is DONE (`PathClassE2EDriftLivingSpecTests`). Spine complete: `M6(+N1-07) → MVP CLOSED`.
- Intentional residual (W7-44 Living Spec lock): §3.C continuous residual tranche after N1-07 Living Spec is seeded as **W7-45** — **M7.1…M7.4** Post-MVP CLOSED; release tag **`v0.2.0`**.
- Intentional residual (W7-45 Living Spec lock): **M7.1…M7.4 (#110–#136)** — Post-MVP routing assurance, endpoint mobility, external correlation, and incident enforcement are DONE. **M7.4 CLOSED**; Post-MVP M7 = **0** open. Release tag **`v0.2.0`** (2026-08-24).
- Intentional residual (W7-46 Living Spec lock): §3.C continuous residual tranche after M7 CLOSED is seeded as **W7-47** — **SEC-07…SEC-15 DONE** share `IUnitOfWork` for entity+idempotency+audit co-writes.
- Intentional residual (W7-48 Living Spec lock): §3.C continuous residual tranche after SEC-07…15 is seeded as **W7-49** — known-limitations intentional residual bullets are **fully Living-Spec locked** (corpus COMPLETE); next product work seeds via PLAN tranche, not idle.
- Intentional residual (W7-49 Living Spec lock): known-limitations intentional residual bullets are **fully Living-Spec locked** (corpus COMPLETE); CRS/physical lab runner remains **ops-parallel**, not a §3 stop-gate.
- Intentional residual (W7-50 Living Spec lock): §3.C product tranche after residual corpus is seeded as **W7-51** — **PLAN-03** quality-gate inventory (import-graph / docs smoke / anti-stub); not idle; not a lab stop-gate.
- Intentional residual (W7-51 Living Spec lock): **PLAN-03** quality-gate inventory is documented in [`plan-03-quality-gates.md`](../planning/plan-03-quality-gates.md); first atomic row **QG-IMPORT-01** (W7-52).
- Intentional residual (W7-52 Living Spec lock): **QG-IMPORT-01** — production `Mfc.*` assembly import graph is a DAG (`ProductionImportGraph` + `QgImport01ImportGraphCycleLivingSpecTests`); complements NetArch pairwise boundaries.
- Intentional residual (W7-53 Living Spec lock): **QG-DOCS-01** — weekly docs smoke (`docs-smoke.md` + `QgDocs01WeeklyDocsSmokeLivingSpecTests`) keeps README / docs index aligned with ROADMAP §3.C NEXT.
- Intentional residual (W7-54 Living Spec lock): **QG-ANTISTUB-01** — `AntiStubScanner` + `QgAntistub01AntiStubLivingSpecTests` fail closed on `NotImplementedException`, stub TODOs, and xUnit `Skip` in repo sources.
- Intentional residual (W7-55 Living Spec lock): **QG-LIVESPEC-MATRIX-01** — DONE PLAN-03 `QG-*` rows require matching `## Living Specification — QG-…` sections in `testing.md` (`QgLivespecMatrix01LivingSpecTests`).
- Intentional residual (W7-56 Living Spec lock): **QG-SIGN-01** — MVP signing remains cleartext `SHA256SUMS` + documented attestation; crypto GPG/Sigstore stays a future CI gate (`QgSign01ReleaseSigningLivingSpecTests`).
- Intentional residual (W7-57 Living Spec lock): §3.C product tranche after PLAN-03 quality gates is seeded as **W7-58** — **PLAN-04** contract-test / API Living Spec inventory; not idle; not a lab stop-gate.
- Intentional residual (W7-58 Living Spec lock): **PLAN-04** contract-test inventory is documented in [`plan-04-contract-tests.md`](../planning/plan-04-contract-tests.md); first atomic row **CT-DEPLOY-01** (W7-59).
- Intentional residual (W7-59 Living Spec lock): **CT-DEPLOY-01** — `DeploymentService` has Controller GrpcHost contract coverage (`DeploymentGrpcHostTests` + `ScriptedDeploymentRuntime`).
- Intentional residual (W7-60 Living Spec lock): **CT-ZONE-01** — `ZoneService` has Controller GrpcHost contract coverage (`ZoneGrpcHostTests`).
- Intentional residual (W7-61 Living Spec lock): **CT-DRIFT-01** — `DriftService` has Controller GrpcHost contract coverage (`DriftGrpcHostTests`).
- Intentional residual (W7-62 Living Spec lock): **CT-AUDIT-01** — `AuditService` has Controller GrpcHost contract coverage (`AuditGrpcHostTests`).
- Intentional residual (W7-63 Living Spec lock): **CT-ROUTING-01** — `RoutingAssuranceService` has ProtoContract + Controller GrpcHost coverage (`RoutingAssuranceProtoContractTests`, `RoutingAssuranceGrpcHostTests`).
- Intentional residual (W7-64 Living Spec lock): **CT-INCIDENT-01** — `IncidentService` has ProtoContract + Controller GrpcHost coverage (`IncidentProtoContractTests`, `IncidentGrpcHostTests`).
- Intentional residual (W7-65 Living Spec lock): §3.C product tranche after PLAN-04 contract-test host coverage is seeded as **W7-66** — **PLAN-05** Desktop operator-surface Living Spec inventory; not idle; not a lab stop-gate.
- Intentional residual (W7-66 Living Spec lock): **PLAN-05** Desktop operator-surface inventory is documented in [`plan-05-desktop-operator-surface.md`](../planning/plan-05-desktop-operator-surface.md); first atomic row **DESK-AUDIT-01** (W7-67).
- Intentional residual (W7-67 Living Spec lock): **DESK-AUDIT-01** — Desktop Audit panel Living Spec vs AuditGrpcHost (`DesktopAuditLivingSpecTests`).
- Intentional residual (W7-68 Living Spec lock): **DESK-DRIFT-01** — Desktop Drift panel Living Spec vs DriftGrpcHost (`DesktopDriftLivingSpecTests`).
- Intentional residual (W7-69 Living Spec lock): **DESK-ZONE-01** — Desktop Zones panel Living Spec vs ZoneGrpcHost (`DesktopZonesLivingSpecTests`).
- Intentional residual (W7-70 Living Spec lock): **DESK-ROUTING-01** — Desktop Routing assurance Living Spec deepened vs RoutingAssuranceGrpcHost (`DesktopRoutingAssuranceLivingSpecTests` Ac10–13); PLAN-05 **COMPLETE**.
- Intentional residual (W7-71 Living Spec lock): §3.C product tranche after PLAN-05 Desktop operator-surface is seeded as **W7-72** — **PLAN-06** Incident Desktop operator-surface Living Spec inventory; not idle; not a lab stop-gate.
- Intentional residual (W7-72 Living Spec lock): **PLAN-06** Incident Desktop operator-surface inventory is documented in [`plan-06-incident-desktop-operator-surface.md`](../planning/plan-06-incident-desktop-operator-surface.md); first atomic row **DESK-INCIDENT-01** (W7-73).
- Intentional residual (W7-73 Living Spec lock): **DESK-INCIDENT-01** — Desktop Incident ViewModel + client Living Spec vs IncidentGrpcHost (`DesktopIncidentLivingSpecTests`); MainWindow panel deferred to **DESK-INCIDENT-02** (W7-74).
- Intentional residual (W7-74 Living Spec lock): **DESK-INCIDENT-02** — Desktop Incident MainWindow Operations → Incident panel Living Spec (`DesktopIncidentPanelLivingSpecTests`); Bind assessment deferred to **DESK-INCIDENT-03** (W7-75).
- Intentional residual (W7-75 Living Spec lock): **DESK-INCIDENT-03** — Desktop Incident Bind assessment Living Spec (`DesktopIncidentBindLivingSpecTests`); fail-closed deploy/overlay lock deferred to **DESK-INCIDENT-04** (W7-76).
- Intentional residual (W7-76 Living Spec lock): **DESK-INCIDENT-04** — Desktop Incident fail-closed no deploy/overlay Living Spec (`DesktopIncidentFailClosedLivingSpecTests`); **PLAN-06 COMPLETE**; next product seed W7-77.
- Intentional residual (W7-77 Living Spec lock): §3.C product tranche after PLAN-06 Incident Desktop operator-surface is seeded as **W7-78** — **PLAN-07** inventory next product Living Spec tranche after PLAN-06; not idle; not a lab stop-gate.
- Intentional residual (W7-78 Living Spec lock): **PLAN-07** Core MVP Desktop operator-surface inventory is documented in [`plan-07-core-mvp-desktop-operator-surface.md`](../planning/plan-07-core-mvp-desktop-operator-surface.md); first atomic row **DESK-POLICY-01** (W7-79).
- Intentional residual (W7-79 Living Spec lock): **DESK-POLICY-01** — Desktop Policies panel Living Spec (`DesktopPoliciesLivingSpecTests`) vs PolicyGrpcHost; next PLAN-07 seed W7-80 → DESK-DEPLOY-01.
- Intentional residual (W7-80 Living Spec lock): §3.C product tranche after DESK-POLICY-01 is seeded as **W7-81** — **DESK-DEPLOY-01** Desktop Deployment Living Spec vs DeploymentGrpcHost; not idle; not a lab stop-gate.
- Intentional residual (W7-81 Living Spec lock): **DESK-DEPLOY-01** — Desktop Deployment Living Spec (`DesktopDeploymentLivingSpecTests`) vs DeploymentGrpcHost; next PLAN-07 seed W7-82 → DESK-ONBOARD-01.
- Intentional residual (W7-82 Living Spec lock): §3.C product tranche after DESK-DEPLOY-01 is seeded as **W7-83** — **DESK-ONBOARD-01** Desktop Onboarding Living Spec vs OnboardingGrpcHost; not idle; not a lab stop-gate.
- Intentional residual (W7-83 Living Spec lock): **DESK-ONBOARD-01** — Desktop Onboarding Living Spec (`DesktopOnboardingLivingSpecTests`) vs OnboardingGrpcHost; next PLAN-07 seed W7-84 → DESK-SNAPSHOT-01.
- Intentional residual (W7-84 Living Spec lock): §3.C product tranche after DESK-ONBOARD-01 is seeded as **W7-85** — **DESK-SNAPSHOT-01** Desktop Snapshot Living Spec vs SnapshotGrpcHost; not idle; not a lab stop-gate.

## Production wiring (P2 pilot)

- Intentional residual (W7-39 Living Spec lock): **Read path (P2-04…P2-06)** — **DONE**. Enable via `Mfc:RouterOs:Enabled=true`; default remains fail-closed (`ProbeOnlyRouterOsReadPort` / `NotConfiguredSnapshotCapturePort`). Pilot checklist: [`pilot-runbook.md`](../operations/pilot-runbook.md).
- Intentional residual (W7-40 Living Spec lock): §3.C continuous residual tranche after RouterOs fail-closed is seeded as **W7-41** — enable write path via **`Mfc:RouterOs:WriteEnabled=true`** (P2-11 checklist).
- Intentional residual (W7-41 Living Spec lock): Onboarding/deploy/watchdog-residue: **P2-07…P2-10 DONE** in code; enable via **`Mfc:RouterOs:WriteEnabled=true`**. Operator checklist: [`pilot-runbook.md`](../operations/pilot-runbook.md) (P2-11).
- Intentional residual (W7-42 Living Spec lock): §3.C continuous residual tranche after WriteEnabled is seeded as **W7-43** — **N1-07** path-class E2E/drift acceptance is DONE (`PathClassE2EDriftLivingSpecTests`). **§3.C NEXT = W7-43 (#484)** after W7-42.

## Desktop inventory registration

- Intentional residual (W7-35 Living Spec lock): Inventory **Add router** wizard is **DONE** ([#309](https://github.com/sesquicadaver/MTDirector/pull/309)): Site→Node→Device + `UpdateDeviceConnection` from Desktop.
- Intentional residual (W7-37 Living Spec lock): gRPC remains available for automation.
- Intentional residual (W7-38 Living Spec lock): §3.C continuous residual tranche after gRPC automation is seeded as **W7-39** — RouterOs Enabled default remains fail-closed (`ProbeOnlyRouterOsReadPort` / `NotConfiguredSnapshotCapturePort`).
- Intentional residual (W7-36 Living Spec lock): §3.C continuous residual tranche after Inventory Add router is seeded as **W7-37** — gRPC remains available for automation.
- Intentional residual (W7-34 Living Spec lock): Closing the Desktop window **does not** stop Controller — stop the Controller process separately (separate OS processes).

## Live lab residuals (optional)

- Intentional residual (W7-27 Living Spec lock): Live CHR matrix is **OFF**. Scripted E2E Living Specs (M6-05…M6-07 + N1-07 + M7.1-11 + M7.2-04 + M7.4-06) are the DoD substitute.
- Intentional residual (W7-29 Living Spec lock): Live physical CRS hardware exercise is **OFF**. Scripted CRS fixture + `VrrpCrsE2ELivingSpecTests` AC11 are the DoD substitute. Physical CRS is **ops residual**, not a §3 stop-gate.
- Intentional residual (W7-28 Living Spec lock): Golden live CHR hashes remain env-gated until an isolated runner exists.

## Packaging / signing residuals

- Intentional residual (W7-22 Living Spec lock): Desktop “installer” for MVP is a **zip/tar publish directory** (Avalonia), not MSI/setup.exe.
- Intentional residual (W7-23 Living Spec lock): Artifact “signing” for MVP is **cleartext `SHA256SUMS` + documented attestation**; cryptographic GPG/Sigstore is a CI signing gate (see [`RELEASE_SIGNING.md`](RELEASE_SIGNING.md)).
- Intentional residual (W7-24 Living Spec lock): CycloneDX CLI is optional; SBOM script falls back to CycloneDX-lite metadata + package inventory.

## Product scope lock (out of MVP / M7)

- Intentional residual (W7-25 Living Spec lock): No NAT / RAW / Mangle / routing / VRRP / bridge / VLAN **writes** beyond managed filter/onboarding/deploy allowlists.
- Intentional residual (W7-26 Living Spec lock): No campaigns, auto-deploy, auto-fix drift, web/mobile UI, multi-tenant, microservices/Redis/K8s, multi-vendor, SIEM/SOAR in Controller.
- Intentional residual (W7-21 Living Spec lock): `IResponseFeedbackDeliveryPort` defaults to **not configured** until an external analytics complex is wired.
- Intentional residual (W7-47 Living Spec lock): **SEC-07…SEC-15 DONE:** Zone/policy (SEC-07), `UpdateConnectionProfileUseCase` + `DeploymentWorkflowUseCases` (SEC-08), `OnboardingWorkflowUseCases` (SEC-09), `ExpireIncidentDenyOverlayBindingUseCase` (SEC-10), `DetectManagedDriftUseCase` + `EmitResponseFeedbackUseCase` store+audit (SEC-11), `CaptureSnapshotUseCase` persist+audit (SEC-12), `UpsertDeviceHashStateUseCase` (SEC-13), `OpenEndpointPresenceUseCase` multi-store writes (SEC-14), and `UpsertRoutingAssuranceStateUseCase` (SEC-15) share `IUnitOfWork` for entity+idempotency+audit co-writes. Intentional residual (W7-20 Living Spec lock): Feedback **delivery** and RouterOS capture remain outside the DB boundary. Intentional residual (W7-17 Living Spec lock): **resolve-only zone updates** (no idempotency/audit triple); Intentional residual (W7-18 Living Spec lock): Start* pre-runtime `AddOperationAsync` stays outside UoW; Intentional residual (W7-19 Living Spec lock): orchestrator-only audit append after nested use cases (e.g. incident overlay deploy) stays outside UoW.

## Operational notes

- Intentional residual (W7-30 Living Spec lock): Controller does not migrate on normal startup; use `--migrate-only` or the EF migrations bundle.
- Intentional residual (W7-31 Living Spec lock): Development master-key provider is forbidden outside Development.
- Intentional residual (W7-32 Living Spec lock): GitHub-hosted CI may be billing-limited; local gates in [`release-gates.md`](release-gates.md) remain authoritative for acceptance.
- Intentional residual (W7-33 Living Spec lock): §3.C continuous residual tranche after CI billing is seeded as **W7-34** — Closing the Desktop window **does not** stop Controller (separate OS processes).
