# Continuous product queue (PLAN-02)

**Date:** 2026-08-31  
**Baseline:** `main` @ `877a529` (W2.2 Routing assurance next-hop/subject fields, [#338](https://github.com/sesquicadaver/MTDirector/pull/338))  
**PLAN issue:** [PLAN-02 #339](https://github.com/sesquicadaver/MTDirector/issues/339)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  
**PLAN-03 (quality gates):** [`plan-03-quality-gates.md`](plan-03-quality-gates.md)  
**PLAN-04 (contract tests):** [`plan-04-contract-tests.md`](plan-04-contract-tests.md)  
**PLAN-05 (Desktop operator-surface):** [`plan-05-desktop-operator-surface.md`](plan-05-desktop-operator-surface.md) **COMPLETE**  
**PLAN-06 (Incident Desktop operator-surface):** [`plan-06-incident-desktop-operator-surface.md`](plan-06-incident-desktop-operator-surface.md) **COMPLETE**  
**PLAN-07 (Core MVP Desktop operator-surface):** [`plan-07-core-mvp-desktop-operator-surface.md`](plan-07-core-mvp-desktop-operator-surface.md) **COMPLETE**
**PLAN-08 (Desktop secondary operator-surface):** [`plan-08-desktop-secondary-operator-surface.md`](plan-08-desktop-secondary-operator-surface.md) **COMPLETE**  
**PLAN-09 (Desktop connection-status operator-surface):** [`plan-09-desktop-connection-status-operator-surface.md`](plan-09-desktop-connection-status-operator-surface.md) **COMPLETE**

This is the in-repo plan (`.omx/plans/` is gitignored). It replaces the idle state **NEXT = none**.

## Why the previous queue stopped work

| Stopper | Evidence | Effect |
|---------|----------|--------|
| Empty `ROADMAP.md` §3 | `NEXT = none` after P2-11 + post-queue UX | `/autopilot` reports and **idles** |
| W5 labelled “PLAN only” | [`desktop-ui-backend-alignment.md`](../development/desktop-ui-backend-alignment.md) | P3 work waits for a PLAN that was never opened |
| Lab phase gates (GNS3 / CHR / `WriteEnabled`) | `~/gns3-lab` (outside git); [`known-limitations.md`](../release/known-limitations.md) live CHR **OFF** | Operators treat phase N as a product stop — **not a MUST in this repo** |
| ROADMAP §6 “no skip predecessors” | Correct **inside** the product line | Misread as “wait for lab phase close” |

**Rule (PLAN-02):** closing a delivery wave without seeding the next §3 row in the same cycle is forbidden. Lab/CHR/`WriteEnabled` run **in parallel** and are **never** predecessors of Desktop/Contracts PRs.

## Readiness (evidence vs inference)

### Code / normative milestones (evidence)

| Layer | Status | Notes |
|-------|--------|-------|
| MVP M0–M6 + N1 | **100% CLOSED** | 109/109 |
| Post-MVP M7.1–M7.4 | **100% CLOSED** | `v0.2.0` (2026-08-24) |
| P2 read P2-04…P2-06 | **CLOSED** | `Mfc:RouterOs:Enabled` fail-closed default |
| P2 write P2-07…P2-11 | **CLOSED** | `WriteEnabled` fail-closed default — **do not** flip to wake UI |
| Desktop alignment W1.1–W4.4, W2.1–W2.2 | **DONE** | P0–P2 UI ↔ existing RPC |
| Product issues in `ISSUES.md` §2.2 | **139 DONE** | Tracker aligned TRACKER-01 |

### Operational / lab (evidence)

| Item | Status |
|------|--------|
| Live CHR / physical CRS in CI | **OFF** (scripted Living Specs are DoD) |
| Default `WriteEnabled` | **false** (correct fail-closed) |
| GNS3 lab (`~/gns3-lab`) | Outside this git repo; day-2 / write unlock is **ops**, not §3 |

### Remaining product glue (evidence — not W5)

| Gap | Where | Queue ID |
|-----|-------|----------|
| Rollback Start without Watch | `DeploymentViewModel.RollbackAsync` vs `StartAndWatchAsync` | **CONT-01 DONE** |
| Neighbor apply ignores VRRP member b | `AddRouterWizardViewModel.ApplyNeighborCandidate` | **CONT-02 DONE** |
| Onboarding Rollback without Watch / hub stops at Committed | `OnboardingViewModel` + `OnboardingProgressHub` | **W6-04 DONE** |
| GetNode Reachability always Unknown | `ViewMapper` / DiscoverDevice LastSupportState | **W6-05 DONE** |
| Policies Diff flattened to SummaryLine only | `PolicyPanelService.DiffAsync` / DiffLines | **W6-06 DONE** |
| Diff baseline UUID paste ritual | `PoliciesViewModel.DiffBaselineRevisionIdText` | **W6-07 DONE** |
| Unreachable lost on Controller restart | `IDeviceReachabilityObservationStore` (process-local) | **W6-08 DONE** |
| ReorderRules only via UUID paste | `PoliciesViewModel.ReorderRuleIdsText` | **W6-09 DONE** |
| System actor spoofable via `x-mfc-actor` | `SystemActorAuthorizationBoundary` + gRPC ResolveActor | **SEC-01 DONE** |
| AnchorOnly empty deploy materializer in production | `AnchorOnlyDeploymentArtifactMaterializer` | **SEC-02 DONE** |
| Audit hash uses predecessor length only | `EfAuditEventWriter` | **SEC-03 DONE** |
| INTERNAL_CA empty CA store + RevocationMode.NoCheck | `IRouterOsTrustedCaStore` / `ApiSslCertificateValidator` | **SEC-04 DONE** |
| Non-atomic mutation vs idempotency/audit | write path | **SEC-05 DONE** |
| No Incident gRPC surface | Contracts / Controller | **SEC-06 DONE** |
| Partial UoW on mutations | Application write paths | **SEC-07 DONE** |
| Deploy/profile UoW residual | Application write paths | **SEC-08 DONE** |
| Onboarding UoW residual | Application write paths | **SEC-09 DONE** |
| Incident overlay expire UoW | Application write paths | **SEC-10 DONE** |
| Drift detect + response-feedback UoW | Application write paths | **SEC-11 DONE** |
| CaptureSnapshot persist+audit UoW | Application write paths | **SEC-12 DONE** |
| Device hash-state upsert UoW | Application write paths | **SEC-13 DONE** |
| Endpoint presence multi-store UoW | Application write paths | **SEC-14 DONE** |
| Routing assurance state upsert UoW | Application write paths | **SEC-15 DONE** |
| Desktop MikroTik/Winbox display labels | Desktop presentation | **W7-01 DONE** |
| gRPC actor ↔ authenticated principal | Controller authn | **W7-02 DONE** |
| Controller Kestrel mTLS client certificates | Controller TLS | **W7-03 DONE** |
| Validate mTLS client certs against TrustedCa | Controller TLS | **W7-04 DONE** |
| Bind Desktop actor from client cert CN | Desktop authn | **W7-05 DONE** |
| Map mTLS client cert to HttpContext.User | Controller authn | **W7-06 DONE** |
| Prefer HttpContext.User over gRPC peer identity | Controller authn | **W7-07 DONE** |
| Desktop status shows resolved mTLS actor | Desktop UX | **W7-08 DONE** |
| Production mTLS operator checklist | Ops docs | **W7-09 DONE** |
| Log redacted client-cert thumbprint on mTLS principal map | Controller observability | **W7-10 DONE** |
| Harden SessionFaultInjection timeout pending-clear | RouterOS session tests | **W7-11 DONE** |
| Desktop AuthenticationFailed status Living Spec | Desktop UX tests | **W7-12 DONE** |
| mTLS map log includes request TraceIdentifier | Controller observability | **W7-13 DONE** |
| SECURITY.md documents TraceIdentifier on mTLS map log | Security docs | **W7-14 DONE** |
| Pilot runbook TraceIdentifier for mTLS Connect correlation | Ops docs | **W7-15 DONE** |
| controller-configuration.md TraceIdentifier cross-link | Ops docs | **W7-16 DONE** |
| Lock resolve-only zone UoW residual Living Spec | Docs / SEC residual | **W7-17 DONE** |
| Lock Start* AddOperationAsync outside-UoW residual Living Spec | Docs / SEC residual | **W7-18 DONE** |
| Lock orchestrator-only audit residual Living Spec | Docs / SEC residual | **W7-19 DONE** |
| Lock Feedback delivery / RouterOS capture outside-DB residual Living Spec | Docs / SEC residual | **W7-20 DONE** |
| Lock IResponseFeedbackDeliveryPort not-configured residual Living Spec | Docs / SEC residual | **W7-21 DONE** |
| Lock Desktop zip/tar installer packaging residual Living Spec | Docs / packaging residual | **W7-22 DONE** |
| Lock SHA256SUMS attestation packaging residual Living Spec | Docs / packaging residual | **W7-23 DONE** |
| Lock CycloneDX-lite SBOM packaging residual Living Spec | Docs / packaging residual | **W7-24 DONE** |
| Lock MVP scope-lock no-NAT/RAW writes residual Living Spec | Docs / scope residual | **W7-25 DONE** |
| Lock MVP scope-lock no-campaigns/auto-deploy residual Living Spec | Docs / scope residual | **W7-26 DONE** |
| Lock Live CHR matrix OFF residual Living Spec | Docs / lab residual | **W7-27 DONE** |
| Lock Golden live CHR hashes env-gated residual Living Spec | Docs / lab residual | **W7-28 DONE** |
| Lock Live physical CRS hardware OFF ops residual Living Spec | Docs / lab residual | **W7-29 DONE** |
| Lock Controller migrate-only startup residual Living Spec | Docs / ops residual | **W7-30 DONE** |
| Lock Development master-key provider residual Living Spec | Docs / ops residual | **W7-31 DONE** |
| Lock GitHub-hosted CI billing-limited residual Living Spec | Docs / ops residual | **W7-32 DONE** |
| Seed next continuous residual after CI billing Living Spec | Docs / queue seed | **W7-33 DONE** |
| Lock Desktop window does not stop Controller residual Living Spec | Docs / Desktop residual | **W7-34 DONE** |
| Lock Inventory Add router wizard DONE residual Living Spec | Docs / Desktop residual | **W7-35 DONE** |
| Seed next continuous residual after Inventory Add router Living Spec | Docs / queue seed | **W7-36 DONE** |
| Lock gRPC remains available for automation residual Living Spec | Docs / Desktop residual | **W7-37 DONE** |
| Seed next continuous residual after gRPC automation Living Spec | Docs / queue seed | **W7-38 DONE** |
| Lock RouterOs Enabled default fail-closed residual Living Spec | Docs / P2 residual | **W7-39 DONE** |
| Seed next continuous residual after RouterOs fail-closed Living Spec | Docs / queue seed | **W7-40 DONE** |
| Lock RouterOs WriteEnabled operator residual Living Spec | Docs / P2 residual | **W7-41 DONE** |
| Seed next continuous residual after WriteEnabled Living Spec | Docs / queue seed | **W7-42 DONE** |
| Lock N1-07 path-class E2E DONE residual Living Spec | Docs / MVP residual | **W7-43 DONE** |
| Seed next continuous residual after N1-07 Living Spec | Docs / queue seed | **W7-44 DONE** |
| Lock M7.1…M7.4 CLOSED residual Living Spec | Docs / M7 residual | **W7-45 DONE** |
| Seed next continuous residual after M7 CLOSED Living Spec | Docs / queue seed | **W7-46 DONE** |
| Lock SEC-07…SEC-15 DONE residual Living Spec | Docs / SEC residual | **W7-47 DONE** |
| Seed next continuous residual after SEC-07…15 Living Spec | Docs / queue seed | **W7-48 DONE** |
| Lock known-limitations residual Living Spec corpus COMPLETE | Docs / residual corpus | **W7-49 DONE** |
| Seed next product tranche after residual corpus Living Spec | Docs / product seed | **W7-50 DONE** |
| PLAN-03 — Inventory next quality-gate product tranche | Docs / PLAN-03 | **W7-51 DONE** |
| QG-IMPORT-01 — Import graph + cycle anomaly Living Spec gate | Docs / quality gate | **W7-52 DONE** |
| QG-DOCS-01 — Weekly docs smoke Living Spec gate | Docs / quality gate | **W7-53 DONE** |
| QG-ANTISTUB-01 — Anti-stub CI Living Spec gate | Docs / quality gate | **W7-54 DONE** |
| QG-LIVESPEC-MATRIX-01 — Living Spec matrix PR gate | Docs / quality gate | **W7-55 DONE** |
| QG-SIGN-01 — Release signing checklist Living Spec gate | Docs / quality gate | **W7-56 DONE** |
| Seed next product tranche after PLAN-03 quality gates | Docs / product seed | **W7-57 DONE** |
| PLAN-04 — Inventory next contract-test / API Living Spec product tranche | Docs / PLAN-04 | **W7-58 DONE** |
| CT-DEPLOY-01 — DeploymentService GrpcHost contract Living Spec | Docs / contract test | **W7-59 DONE** |
| CT-ZONE-01 — ZoneService GrpcHost contract Living Spec | Docs / contract test | **W7-60 DONE** |
| CT-DRIFT-01 — DriftService GrpcHost contract Living Spec | Docs / contract test | **W7-61 DONE** |
| CT-AUDIT-01 — AuditService GrpcHost contract Living Spec | Docs / contract test | **W7-62 DONE** |
| CT-ROUTING-01 — RoutingAssuranceService ProtoContract + GrpcHost Living Spec | Docs / contract test | **W7-63 DONE** |
| CT-INCIDENT-01 — IncidentService ProtoContract + GrpcHost Living Spec | Docs / contract test | **W7-64 DONE** |
| Seed next product tranche after PLAN-04 → PLAN-05 Desktop operator-surface | Docs / product seed | **W7-65 DONE** |
| PLAN-05 — Inventory next Desktop operator-surface Living Spec product tranche | Docs / PLAN-05 | **W7-66 DONE** |
| DESK-AUDIT-01 — Desktop Audit panel Living Spec vs AuditGrpcHost | Docs / Desktop Living Spec | **W7-67 DONE** |
| DESK-DRIFT-01 — Desktop Drift panel Living Spec vs DriftGrpcHost | Docs / Desktop Living Spec | **W7-68 DONE** |
| DESK-ZONE-01 — Desktop Zones panel Living Spec vs ZoneGrpcHost | Docs / Desktop Living Spec | **W7-69 DONE** |
| DESK-ROUTING-01 — Desktop Routing assurance Living Spec vs RoutingAssuranceGrpcHost | Docs / Desktop Living Spec | **W7-70 DONE** |
| Seed next product tranche after PLAN-05 Desktop operator-surface | Docs / product seed | **W7-71 DONE** |
| PLAN-06 — Inventory next Incident Desktop operator-surface Living Spec product tranche | Docs / PLAN-06 | **W7-72 DONE** |
| DESK-INCIDENT-01 — Desktop Incident ViewModel + client Living Spec vs IncidentGrpcHost | Docs / Desktop Living Spec | **W7-73 DONE** |
| DESK-INCIDENT-02 — Desktop Incident MainWindow panel Living Spec | Docs / Desktop Living Spec | **W7-74 DONE** |
| DESK-INCIDENT-03 — Desktop Incident Bind assessment Living Spec | Docs / Desktop Living Spec | **W7-75 DONE** |
| DESK-INCIDENT-04 — Desktop Incident fail-closed no deploy/overlay Living Spec | Docs / Desktop Living Spec | **W7-76 DONE** |
| Seed next product tranche after PLAN-06 Incident Desktop operator-surface | Docs / product seed | **W7-77 DONE** |
| PLAN-07 — Inventory next Core MVP Desktop operator-surface Living Spec product tranche | Docs / PLAN-07 | **W7-78 DONE** |
| DESK-POLICY-01 — Desktop Policies panel Living Spec vs PolicyGrpcHost | Docs / Desktop Living Spec | **W7-79 DONE** |
| Seed next PLAN-07 row after DESK-POLICY-01 → DESK-DEPLOY-01 | Docs / PLAN-07 | **W7-80 DONE** |
| DESK-DEPLOY-01 — Desktop Deployment Living Spec vs DeploymentGrpcHost | Docs / Desktop Living Spec | **W7-81 DONE** |
| Seed next PLAN-07 row after DESK-DEPLOY-01 → DESK-ONBOARD-01 | Docs / PLAN-07 | **W7-82 DONE** |
| DESK-ONBOARD-01 — Desktop Onboarding Living Spec vs OnboardingGrpcHost | Docs / Desktop Living Spec | **W7-83 DONE** |
| Seed next PLAN-07 row after DESK-ONBOARD-01 → DESK-SNAPSHOT-01 | Docs / PLAN-07 | **W7-84 DONE** |
| DESK-SNAPSHOT-01 — Desktop Snapshot Living Spec vs SnapshotGrpcHost | Docs / Desktop Living Spec | **W7-85 DONE** |
| Seed next PLAN-07 row after DESK-SNAPSHOT-01 → DESK-INVENTORY-01 | Docs / PLAN-07 | **W7-86 DONE** |
| DESK-INVENTORY-01 — Desktop Inventory Living Spec vs InventoryGrpcHost | Docs / Desktop Living Spec | **W7-87 DONE** |
| Seed next product tranche after PLAN-07 Core MVP Desktop | Docs / product seed | **W7-88 DONE** |
| PLAN-08 — Inventory next Desktop secondary operator-surface Living Spec product tranche | Docs / PLAN-08 | **W7-89 DONE** |
| DESK-NODE-01 — Desktop Node VRRP pair Living Spec vs InventoryGrpcHost | Docs / Desktop Living Spec | **W7-90 DONE** |
| Seed next PLAN-08 row after DESK-NODE-01 → DESK-NBR-01 | Docs / PLAN-08 | **W7-91 DONE** |
| DESK-NBR-01 — Desktop Neighbor candidates Living Spec depth | Docs / Desktop Living Spec | **W7-92 DONE** |
| Seed next PLAN-08 row after DESK-NBR-01 → DESK-PROBE-01 | Docs / PLAN-08 | **W7-93 DONE** |
| DESK-PROBE-01 — Desktop ValidateDeviceConnection probe Living Spec depth | Docs / Desktop Living Spec | **W7-94 DONE** |
| Seed next PLAN-08 row after DESK-PROBE-01 → DESK-POLICY-02 | Docs / PLAN-08 | **W7-95 DONE** |
| DESK-POLICY-02 — Desktop Policy safety analysis Living Spec depth | Docs / Desktop Living Spec | **W7-96 DONE** |
| Seed next product tranche after PLAN-08 → PLAN-09 | Docs / product seed | **W7-97 DONE** |
| PLAN-09 — Inventory next Desktop connection-status operator-surface Living Spec product tranche | Docs / PLAN-09 | **W7-98 DONE** |
| DESK-CONN-01 — Desktop Connect/Disconnect Living Spec depth | Docs / Desktop Living Spec | **W7-99 DONE** |
| Seed next PLAN-09 row after DESK-CONN-01 → DESK-MTLS-01 | Docs / PLAN-09 | **W7-100 DONE** |
| DESK-MTLS-01 — Desktop mTLS actor status Living Spec depth | Docs / Desktop Living Spec | **W7-101 DONE** |
| Seed next PLAN-09 row after DESK-MTLS-01 → DESK-AUTH-01 | Docs / PLAN-09 | **W7-102 DONE** |
| DESK-AUTH-01 — Desktop AuthenticationFailed/TlsError Living Spec depth | Docs / Desktop Living Spec | **W7-103 DONE** |
| Seed next product tranche after PLAN-09 → PLAN-10 | Docs / product seed | **W7-104 DONE** |
| PLAN-10 — Inventory next product Living Spec tranche after PLAN-09 | Docs / PLAN-10 | **W7-105 OPEN** |

### P3 / new Contracts (evidence)

| Gap | Queue ID |
|-----|----------|
| No `ListPolicies` in `policy.proto` (catalog browse) | **W5-01 DONE** |
| No Desktop RPC for ManagementPath / FastTrack | **W5-02 DONE** |
| Deployment `semantic_diff_entries` is `repeated string` | **W5-03 DONE** |
| CRS / physical lab runner | **Not §3** — residual in known-limitations; ops parallel |

### Deferred / not this tranche (evidence)

- `StartCapture` `node_id` — **W6-03 DONE** (#356); device_id path unchanged
- Policies “Save and Deploy” — MVP scope lock
- Local Desktop `SemanticDiffEngine` — anti-goal
- Auto-fix drift — anti-goal
- Fake VRRP Master/Backup labels without capture facts — anti-goal

## Dual track (product never waits on lab)

```text
Product §3.C (linear, one NEXT)     Ops / lab (parallel, never blocks §3)
─────────────────────────────────   ─────────────────────────────────────
PLAN-02 docs **DONE** (#345)        GNS3 day-2 fixture (out of git)
CONT-01 Rollback Watch **DONE**      Isolated WriteEnabled=true lab
CONT-02 Neighbor → member b **DONE** Live CHR when an isolated runner exists
W5-01 ListPolicies **DONE**
W5-02 ManagementPath / FastTrack **DONE**
W5-03 Typed deploy policy diff **DONE**
W6-01 Operator-readable Diff/Snapshot **DONE**
W6-02 VRRP pair consistency **DONE**
W6-03 StartCapture node_id **DONE**
W6-04 Onboarding Rollback Watch **DONE**
W6-05 GetNode Reachability **DONE**
W6-06 Policies typed Diff rows **DONE**
W6-07 Diff baseline catalog **DONE**
W6-08 Durable Unreachable **DONE**
W6-09 Policies Move up/down reorder **DONE**
SEC-01 Reject system actor gRPC spoof **DONE**
SEC-02 Deploy artifact materializer **DONE**
SEC-03 Audit hash chain **DONE**
SEC-04 INTERNAL_CA trusted CA store **DONE**
SEC-05 Atomic mutation/idempotency/audit **DONE**
SEC-06 Incident assessment gRPC **DONE**
SEC-07 Extend atomic mutation boundary **DONE**
SEC-08 Connection profile + deployment UoW **DONE**
SEC-09 Onboarding workflow UoW **DONE**
SEC-10 Incident overlay expire UoW **DONE**
SEC-11 Drift detect + response-feedback UoW **DONE**
SEC-12 CaptureSnapshot persist+audit UoW **DONE**
SEC-13 UpsertDeviceHashState UoW **DONE**
SEC-14 OpenEndpointPresence UoW **DONE**
SEC-15 UpsertRoutingAssuranceState UoW **DONE**
W7-01 Desktop MikroTik/Winbox display labels **DONE**
W7-02 Bind gRPC actor to authenticated principal **DONE**
W7-03 Controller Kestrel mTLS client certificates **DONE**
W7-04 Validate mTLS client certs against TrustedCa **DONE**
W7-05 Bind Desktop actor from client cert CN **DONE**
W7-06 Map mTLS client cert to HttpContext.User **DONE**
W7-07 Prefer HttpContext.User over gRPC peer identity for actor **DONE**
W7-08 Desktop status shows resolved mTLS actor **DONE**
W7-09 Production mTLS operator checklist **DONE**
W7-10 Log redacted client-cert thumbprint on mTLS principal map **DONE**
W7-11 Harden SessionFaultInjection timeout pending-clear **DONE**
W7-12 Desktop AuthenticationFailed status Living Spec **DONE**
W7-13 mTLS map log includes request TraceIdentifier **DONE**
W7-14 SECURITY.md documents TraceIdentifier on mTLS map log **DONE**
W7-15 Pilot runbook TraceIdentifier for mTLS Connect correlation **DONE**
W7-16 controller-configuration.md TraceIdentifier cross-link **DONE**
W7-17 Lock resolve-only zone UoW residual Living Spec **DONE**
W7-18 Lock Start* AddOperationAsync outside-UoW residual Living Spec **DONE**
W7-19 Lock orchestrator-only audit residual Living Spec **DONE**
W7-20 Lock Feedback delivery / RouterOS capture outside-DB residual Living Spec **DONE**
W7-21 Lock IResponseFeedbackDeliveryPort not-configured residual Living Spec **DONE**
W7-22 Lock Desktop zip/tar installer packaging residual Living Spec **DONE**
W7-23 Lock SHA256SUMS attestation packaging residual Living Spec **DONE**
W7-24 Lock CycloneDX-lite SBOM packaging residual Living Spec **DONE**
W7-25 Lock MVP scope-lock no-NAT/RAW writes residual Living Spec **DONE**
W7-26 Lock MVP scope-lock no-campaigns/auto-deploy residual Living Spec **DONE**
W7-27 Lock Live CHR matrix OFF residual Living Spec **DONE**
W7-28 Lock Golden live CHR hashes env-gated residual Living Spec **DONE**
W7-29 Lock Live physical CRS hardware OFF ops residual Living Spec **DONE**
W7-30 Lock Controller migrate-only startup residual Living Spec **DONE**
W7-31 Lock Development master-key provider residual Living Spec **DONE**
W7-32 Lock GitHub-hosted CI billing-limited residual Living Spec **DONE**
W7-33 Seed next continuous residual after CI billing Living Spec **DONE**
W7-34 Lock Desktop window does not stop Controller residual Living Spec **DONE**
W7-35 Lock Inventory Add router wizard DONE residual Living Spec **DONE**
W7-36 Seed next continuous residual after Inventory Add router Living Spec **DONE**
W7-37 Lock gRPC remains available for automation residual Living Spec **DONE**
W7-38 Seed next continuous residual after gRPC automation Living Spec **DONE**
W7-39 Lock RouterOs Enabled default fail-closed residual Living Spec **DONE**
W7-40 Seed next continuous residual after RouterOs fail-closed Living Spec **DONE**
W7-41 Lock RouterOs WriteEnabled operator residual Living Spec **DONE**
W7-42 Seed next continuous residual after WriteEnabled Living Spec **DONE**
W7-43 Lock N1-07 path-class E2E DONE residual Living Spec **DONE**
W7-44 Seed next continuous residual after N1-07 Living Spec **DONE**
W7-45 Lock M7.1…M7.4 CLOSED residual Living Spec **DONE**
W7-46 Seed next continuous residual after M7 CLOSED Living Spec **DONE**
W7-47 Lock SEC-07…SEC-15 DONE residual Living Spec **DONE**
W7-48 Seed next continuous residual after SEC-07…15 Living Spec **DONE**
W7-49 Lock known-limitations residual Living Spec corpus COMPLETE **DONE**
W7-50 Seed next product tranche after residual corpus Living Spec **DONE**
W7-51 PLAN-03 — Inventory next quality-gate product tranche **DONE**
W7-52 QG-IMPORT-01 — Import graph + cycle anomaly Living Spec gate **DONE**
W7-53 QG-DOCS-01 — Weekly docs smoke Living Spec gate **DONE**
W7-54 QG-ANTISTUB-01 — Anti-stub CI Living Spec gate **DONE**
W7-55 QG-LIVESPEC-MATRIX-01 — Living Spec matrix PR gate **DONE**
W7-56 QG-SIGN-01 — Release signing checklist Living Spec gate **DONE**
W7-57 Seed next product tranche after PLAN-03 quality gates **DONE**
W7-58 PLAN-04 — Inventory next contract-test / API Living Spec product tranche **DONE**
W7-59 CT-DEPLOY-01 — DeploymentService GrpcHost contract Living Spec **DONE**
W7-60 CT-ZONE-01 — ZoneService GrpcHost contract Living Spec **DONE**
W7-61 CT-DRIFT-01 — DriftService GrpcHost contract Living Spec **DONE**
W7-62 CT-AUDIT-01 — AuditService GrpcHost contract Living Spec **DONE**
W7-63 CT-ROUTING-01 — RoutingAssuranceService ProtoContract + GrpcHost Living Spec **DONE**
W7-64 CT-INCIDENT-01 — IncidentService ProtoContract + GrpcHost Living Spec **DONE**
W7-65 Seed next product tranche after PLAN-04 → PLAN-05 Desktop operator-surface **DONE**
W7-66 PLAN-05 — Inventory next Desktop operator-surface Living Spec product tranche **DONE**
W7-67 DESK-AUDIT-01 — Desktop Audit panel Living Spec vs AuditGrpcHost **DONE**
W7-68 DESK-DRIFT-01 — Desktop Drift panel Living Spec vs DriftGrpcHost **DONE**
W7-69 DESK-ZONE-01 — Desktop Zones panel Living Spec vs ZoneGrpcHost **DONE**
W7-70 DESK-ROUTING-01 — Desktop Routing assurance Living Spec vs RoutingAssuranceGrpcHost **DONE**
W7-71 Seed next product tranche after PLAN-05 Desktop operator-surface **DONE**
W7-72 PLAN-06 — Inventory next Incident Desktop operator-surface Living Spec product tranche **DONE**
W7-73 DESK-INCIDENT-01 — Desktop Incident ViewModel + client Living Spec vs IncidentGrpcHost **DONE**
W7-74 DESK-INCIDENT-02 — Desktop Incident MainWindow panel Living Spec **DONE**
W7-75 DESK-INCIDENT-03 — Desktop Incident Bind assessment Living Spec **DONE**
W7-76 DESK-INCIDENT-04 — Desktop Incident fail-closed no deploy/overlay Living Spec **DONE**
W7-77 Seed next product tranche after PLAN-06 Incident Desktop operator-surface **DONE**
W7-78 PLAN-07 — Inventory next Core MVP Desktop operator-surface Living Spec product tranche **DONE**
W7-79 DESK-POLICY-01 — Desktop Policies panel Living Spec vs PolicyGrpcHost **DONE**
W7-80 Seed next PLAN-07 row after DESK-POLICY-01 → DESK-DEPLOY-01 **DONE**
W7-81 DESK-DEPLOY-01 — Desktop Deployment Living Spec vs DeploymentGrpcHost **DONE**
W7-82 Seed next PLAN-07 row after DESK-DEPLOY-01 → DESK-ONBOARD-01 **DONE**
W7-83 DESK-ONBOARD-01 — Desktop Onboarding Living Spec vs OnboardingGrpcHost **DONE**
W7-84 Seed next PLAN-07 row after DESK-ONBOARD-01 → DESK-SNAPSHOT-01 **DONE**
W7-85 DESK-SNAPSHOT-01 — Desktop Snapshot Living Spec vs SnapshotGrpcHost **DONE**
W7-86 Seed next PLAN-07 row after DESK-SNAPSHOT-01 → DESK-INVENTORY-01 **DONE**
W7-87 DESK-INVENTORY-01 — Desktop Inventory Living Spec vs InventoryGrpcHost **DONE**
W7-88 Seed next product tranche after PLAN-07 Core MVP Desktop **DONE**
W7-89 PLAN-08 — Inventory next Desktop secondary operator-surface Living Spec product tranche **DONE**
W7-90 DESK-NODE-01 — Desktop Node VRRP pair Living Spec vs InventoryGrpcHost **DONE**
W7-91 Seed next PLAN-08 row after DESK-NODE-01 → DESK-NBR-01 **DONE**
W7-92 DESK-NBR-01 — Desktop Neighbor candidates Living Spec depth **DONE**
W7-93 Seed next PLAN-08 row after DESK-NBR-01 → DESK-PROBE-01 **DONE**
W7-94 DESK-PROBE-01 — Desktop ValidateDeviceConnection probe Living Spec depth **DONE**
W7-95 Seed next PLAN-08 row after DESK-PROBE-01 → DESK-POLICY-02 **DONE**
W7-96 DESK-POLICY-02 — Desktop Policy safety analysis Living Spec depth **DONE**
W7-97 Seed next product tranche after PLAN-08 → PLAN-09 **DONE**
W7-98 PLAN-09 — Inventory next Desktop connection-status operator-surface Living Spec product tranche **DONE**
W7-99 DESK-CONN-01 — Desktop Connect/Disconnect Living Spec depth **DONE**
W7-100 Seed next PLAN-09 row after DESK-CONN-01 → DESK-MTLS-01 **DONE**
W7-101 DESK-MTLS-01 — Desktop mTLS actor status Living Spec depth **DONE**
W7-102 Seed next PLAN-09 row after DESK-MTLS-01 → DESK-AUTH-01 **DONE**
W7-103 DESK-AUTH-01 — Desktop AuthenticationFailed/TlsError Living Spec depth **DONE**
W7-104 Seed next product tranche after PLAN-09 → PLAN-10 **DONE**
W7-105 PLAN-10 — Inventory next product Living Spec tranche after PLAN-09 **OPEN**
residual ops: CRS / physical lab runner (not §3 stop-gate)
```

`/autopilot` always takes **§3 NEXT**. It does not wait for lab phase transitions.

## Linear queue (one PR each)

| Order | ID | GitHub | Scope | Status |
|------:|----|-------:|-------|--------|
| 1 | PLAN-02 | [#339](https://github.com/sesquicadaver/MTDirector/issues/339) | Seed §3.C + process | **DONE** ([#345](https://github.com/sesquicadaver/MTDirector/pull/345)) |
| 2 | CONT-01 | [#340](https://github.com/sesquicadaver/MTDirector/issues/340) | Rollback + Watch (existing RPC) | **DONE** |
| 3 | CONT-02 | [#341](https://github.com/sesquicadaver/MTDirector/issues/341) | Neighbor apply → VRRP member b | **DONE** |
| 4 | W5-01 | [#342](https://github.com/sesquicadaver/MTDirector/issues/342) | `ListPolicies` catalog browse | **DONE** |
| 5 | W5-02 | [#343](https://github.com/sesquicadaver/MTDirector/issues/343) | ManagementPath / FastTrack Desktop | **DONE** |
| 6 | W5-03 | [#344](https://github.com/sesquicadaver/MTDirector/issues/344) | Typed deployment semantic policy diff | **DONE** |
| 7 | W6-01 | [#352](https://github.com/sesquicadaver/MTDirector/issues/352) | Operator-readable snapshot/diff + VRRP surface | **DONE** |
| 8 | W6-02 | [#354](https://github.com/sesquicadaver/MTDirector/issues/354) | VRRP pair consistency (config + logical FW) | **DONE** |
| 9 | W6-03 | [#356](https://github.com/sesquicadaver/MTDirector/issues/356) | StartCapture node_id fan-out | **DONE** |
| 10 | W6-04 | [#358](https://github.com/sesquicadaver/MTDirector/issues/358) | Onboarding Rollback + Watch (hub + Desktop) | **DONE** |
| 11 | W6-05 | [#360](https://github.com/sesquicadaver/MTDirector/issues/360) | GetNode Reachability from probe | **DONE** |
| 12 | W6-06 | [#362](https://github.com/sesquicadaver/MTDirector/issues/362) | Policies typed Diff rows | **DONE** |
| 13 | W6-07 | [#364](https://github.com/sesquicadaver/MTDirector/issues/364) | Diff baseline from catalog picker | **DONE** |
| 14 | W6-08 | [#366](https://github.com/sesquicadaver/MTDirector/issues/366) | Durable GetNode Unreachable | **DONE** |
| 15 | W6-09 | [#369](https://github.com/sesquicadaver/MTDirector/issues/369) | Policies Reorder via Move up/down | **DONE** |
| 16 | SEC-01 | [#371](https://github.com/sesquicadaver/MTDirector/issues/371) | Reject system actor via gRPC metadata | **DONE** |
| 17 | SEC-02 | [#372](https://github.com/sesquicadaver/MTDirector/issues/372) | Deploy artifact materializer + observed hash | **DONE** |
| 18 | SEC-03 | [#373](https://github.com/sesquicadaver/MTDirector/issues/373) | Audit hash chain includes predecessor bytes | **DONE** |
| 19 | SEC-04 | [#377](https://github.com/sesquicadaver/MTDirector/issues/377) | INTERNAL_CA directory trusted CA store + revocation | **DONE** |
| 20 | SEC-05 | [#378](https://github.com/sesquicadaver/MTDirector/issues/378) | Atomic mutation + idempotency + audit boundary | **DONE** |
| 21 | SEC-06 | [#380](https://github.com/sesquicadaver/MTDirector/issues/380) | Incident assessment gRPC surface | **DONE** |
| 22 | SEC-07 | [#383](https://github.com/sesquicadaver/MTDirector/issues/383) | Extend atomic mutation boundary | **DONE** |
| 23 | SEC-08 | [#385](https://github.com/sesquicadaver/MTDirector/issues/385) | Connection profile + deployment UoW | **DONE** |
| 24 | SEC-09 | [#387](https://github.com/sesquicadaver/MTDirector/issues/387) | Onboarding workflow UoW | **DONE** |
| 25 | SEC-10 | [#389](https://github.com/sesquicadaver/MTDirector/issues/389) | Incident overlay expire UoW | **DONE** |
| 26 | SEC-11 | [#391](https://github.com/sesquicadaver/MTDirector/issues/391) | Drift detect + response-feedback UoW | **DONE** |
| 27 | SEC-12 | [#392](https://github.com/sesquicadaver/MTDirector/issues/392) | CaptureSnapshot persist+audit UoW | **DONE** |
| 28 | SEC-13 | [#394](https://github.com/sesquicadaver/MTDirector/issues/394) | UpsertDeviceHashState UoW | **DONE** |
| 29 | SEC-14 | [#396](https://github.com/sesquicadaver/MTDirector/issues/396) | OpenEndpointPresence UoW | **DONE** |
| 30 | SEC-15 | [#398](https://github.com/sesquicadaver/MTDirector/issues/398) | UpsertRoutingAssuranceState UoW | **DONE** |
| 31 | W7-01 | [#401](https://github.com/sesquicadaver/MTDirector/issues/401) | Desktop MikroTik/Winbox display labels | **DONE** |
| 32 | W7-02 | [#402](https://github.com/sesquicadaver/MTDirector/issues/402) | Bind gRPC actor to authenticated principal | **DONE** |
| 33 | W7-03 | [#404](https://github.com/sesquicadaver/MTDirector/issues/404) | Controller Kestrel mTLS client certificates | **DONE** |
| 34 | W7-04 | [#406](https://github.com/sesquicadaver/MTDirector/issues/406) | Validate mTLS client certs against TrustedCa | **DONE** |
| 35 | W7-05 | [#409](https://github.com/sesquicadaver/MTDirector/issues/409) | Bind Desktop actor from client cert CN | **DONE** |
| 36 | W7-06 | [#411](https://github.com/sesquicadaver/MTDirector/issues/411) | Map mTLS client cert to HttpContext.User | **DONE** |
| 37 | W7-07 | [#413](https://github.com/sesquicadaver/MTDirector/issues/413) | Prefer HttpContext.User over gRPC peer identity for actor | **DONE** |
| 38 | W7-08 | [#415](https://github.com/sesquicadaver/MTDirector/issues/415) | Desktop status shows resolved mTLS actor | **DONE** |
| 39 | W7-09 | [#417](https://github.com/sesquicadaver/MTDirector/issues/417) | Production mTLS operator checklist | **DONE** |
| 40 | W7-10 | [#419](https://github.com/sesquicadaver/MTDirector/issues/419) | Log redacted client-cert thumbprint on mTLS principal map | **DONE** |
| 41 | W7-11 | [#421](https://github.com/sesquicadaver/MTDirector/issues/421) | Harden SessionFaultInjection timeout pending-clear assertion | **DONE** |
| 42 | W7-12 | [#423](https://github.com/sesquicadaver/MTDirector/issues/423) | Desktop status Living Spec covers AuthenticationFailed | **DONE** |
| 43 | W7-13 | [#425](https://github.com/sesquicadaver/MTDirector/issues/425) | mTLS map log includes request TraceIdentifier | **DONE** |
| 44 | W7-14 | [#427](https://github.com/sesquicadaver/MTDirector/issues/427) | SECURITY.md documents TraceIdentifier on mTLS map log | **DONE** |
| 45 | W7-15 | [#429](https://github.com/sesquicadaver/MTDirector/issues/429) | Pilot runbook TraceIdentifier for mTLS Connect correlation | **DONE** |
| 46 | W7-16 | [#431](https://github.com/sesquicadaver/MTDirector/issues/431) | controller-configuration.md TraceIdentifier cross-link | **DONE** |
| 47 | W7-17 | [#433](https://github.com/sesquicadaver/MTDirector/issues/433) | Lock resolve-only zone UoW residual Living Spec | **DONE** |
| 48 | W7-18 | [#435](https://github.com/sesquicadaver/MTDirector/issues/435) | Lock Start* AddOperationAsync outside-UoW residual Living Spec | **DONE** |
| 49 | W7-19 | [#437](https://github.com/sesquicadaver/MTDirector/issues/437) | Lock orchestrator-only audit residual Living Spec | **DONE** |
| 50 | W7-20 | [#439](https://github.com/sesquicadaver/MTDirector/issues/439) | Lock Feedback delivery / RouterOS capture outside-DB residual Living Spec | **DONE** |
| 51 | W7-21 | [#441](https://github.com/sesquicadaver/MTDirector/issues/441) | Lock IResponseFeedbackDeliveryPort not-configured residual Living Spec | **DONE** |
| 52 | W7-22 | [#443](https://github.com/sesquicadaver/MTDirector/issues/443) | Lock Desktop zip/tar installer packaging residual Living Spec | **DONE** |
| 53 | W7-23 | [#445](https://github.com/sesquicadaver/MTDirector/issues/445) | Lock SHA256SUMS attestation packaging residual Living Spec | **DONE** |
| 54 | W7-24 | [#447](https://github.com/sesquicadaver/MTDirector/issues/447) | Lock CycloneDX-lite SBOM packaging residual Living Spec | **DONE** |
| 55 | W7-25 | [#449](https://github.com/sesquicadaver/MTDirector/issues/449) | Lock MVP scope-lock no-NAT/RAW writes residual Living Spec | **DONE** |
| 56 | W7-26 | [#451](https://github.com/sesquicadaver/MTDirector/issues/451) | Lock MVP scope-lock no-campaigns/auto-deploy residual Living Spec | **DONE** |
| 57 | W7-27 | [#453](https://github.com/sesquicadaver/MTDirector/issues/453) | Lock Live CHR matrix OFF residual Living Spec | **DONE** |
| 58 | W7-28 | [#455](https://github.com/sesquicadaver/MTDirector/issues/455) | Lock Golden live CHR hashes env-gated residual Living Spec | **DONE** |
| 59 | W7-29 | [#457](https://github.com/sesquicadaver/MTDirector/issues/457) | Lock Live physical CRS hardware OFF ops residual Living Spec | **DONE** |
| 60 | W7-30 | [#459](https://github.com/sesquicadaver/MTDirector/issues/459) | Lock Controller migrate-only startup residual Living Spec | **DONE** |
| 61 | W7-31 | [#460](https://github.com/sesquicadaver/MTDirector/issues/460) | Lock Development master-key provider residual Living Spec | **DONE** |
| 62 | W7-32 | [#462](https://github.com/sesquicadaver/MTDirector/issues/462) | Lock GitHub-hosted CI billing-limited residual Living Spec | **DONE** |
| 63 | W7-33 | [#464](https://github.com/sesquicadaver/MTDirector/issues/464) | Seed next continuous residual after CI billing Living Spec | **DONE** |
| 64 | W7-34 | [#466](https://github.com/sesquicadaver/MTDirector/issues/466) | Lock Desktop window does not stop Controller residual Living Spec | **DONE** |
| 65 | W7-35 | [#468](https://github.com/sesquicadaver/MTDirector/issues/468) | Lock Inventory Add router wizard DONE residual Living Spec | **DONE** |
| 66 | W7-36 | [#470](https://github.com/sesquicadaver/MTDirector/issues/470) | Seed next continuous residual after Inventory Add router Living Spec | **DONE** |
| 67 | W7-37 | [#472](https://github.com/sesquicadaver/MTDirector/issues/472) | Lock gRPC remains available for automation residual Living Spec | **DONE** |
| 68 | W7-38 | [#474](https://github.com/sesquicadaver/MTDirector/issues/474) | Seed next continuous residual after gRPC automation Living Spec | **DONE** |
| 69 | W7-39 | [#476](https://github.com/sesquicadaver/MTDirector/issues/476) | Lock RouterOs Enabled default fail-closed residual Living Spec | **DONE** |
| 70 | W7-40 | [#478](https://github.com/sesquicadaver/MTDirector/issues/478) | Seed next continuous residual after RouterOs fail-closed Living Spec | **DONE** |
| 71 | W7-41 | [#480](https://github.com/sesquicadaver/MTDirector/issues/480) | Lock RouterOs WriteEnabled operator residual Living Spec | **DONE** |
| 72 | W7-42 | [#482](https://github.com/sesquicadaver/MTDirector/issues/482) | Seed next continuous residual after WriteEnabled Living Spec | **DONE** |
| 73 | W7-43 | [#484](https://github.com/sesquicadaver/MTDirector/issues/484) | Lock N1-07 path-class E2E DONE residual Living Spec | **DONE** |
| 74 | W7-44 | [#486](https://github.com/sesquicadaver/MTDirector/issues/486) | Seed next continuous residual after N1-07 Living Spec | **DONE** |
| 75 | W7-45 | [#488](https://github.com/sesquicadaver/MTDirector/issues/488) | Lock M7.1…M7.4 CLOSED residual Living Spec | **DONE** |
| 76 | W7-46 | [#490](https://github.com/sesquicadaver/MTDirector/issues/490) | Seed next continuous residual after M7 CLOSED Living Spec | **DONE** |
| 77 | W7-47 | [#492](https://github.com/sesquicadaver/MTDirector/issues/492) | Lock SEC-07…SEC-15 DONE residual Living Spec | **DONE** |
| 78 | W7-48 | [#494](https://github.com/sesquicadaver/MTDirector/issues/494) | Seed next continuous residual after SEC-07…15 Living Spec | **DONE** |
| 79 | W7-49 | [#496](https://github.com/sesquicadaver/MTDirector/issues/496) | Lock known-limitations residual Living Spec corpus COMPLETE | **DONE** |
| 80 | W7-50 | [#498](https://github.com/sesquicadaver/MTDirector/issues/498) | Seed next product tranche after residual corpus Living Spec | **DONE** |
| 81 | W7-51 | [#500](https://github.com/sesquicadaver/MTDirector/issues/500) | PLAN-03 — Inventory next quality-gate product tranche | **DONE** |
| 82 | W7-52 | [#502](https://github.com/sesquicadaver/MTDirector/issues/502) | QG-IMPORT-01 — Import graph + cycle anomaly Living Spec gate | **DONE** |
| 83 | W7-53 | [#504](https://github.com/sesquicadaver/MTDirector/issues/504) | QG-DOCS-01 — Weekly docs smoke Living Spec gate | **DONE** |
| 84 | W7-54 | [#506](https://github.com/sesquicadaver/MTDirector/issues/506) | QG-ANTISTUB-01 — Anti-stub CI Living Spec gate | **DONE** |
| 85 | W7-55 | [#508](https://github.com/sesquicadaver/MTDirector/issues/508) | QG-LIVESPEC-MATRIX-01 — Living Spec matrix PR gate | **DONE** |
| 86 | W7-56 | [#510](https://github.com/sesquicadaver/MTDirector/issues/510) | QG-SIGN-01 — Release signing checklist Living Spec gate | **DONE** |
| 87 | W7-57 | [#512](https://github.com/sesquicadaver/MTDirector/issues/512) | Seed next product tranche after PLAN-03 quality gates | **DONE** |
| 88 | W7-58 | [#514](https://github.com/sesquicadaver/MTDirector/issues/514) | PLAN-04 — Inventory next contract-test / API Living Spec product tranche | **DONE** |
| 89 | W7-59 | [#516](https://github.com/sesquicadaver/MTDirector/issues/516) | CT-DEPLOY-01 — DeploymentService GrpcHost contract Living Spec | **DONE** |
| 90 | W7-60 | [#518](https://github.com/sesquicadaver/MTDirector/issues/518) | CT-ZONE-01 — ZoneService GrpcHost contract Living Spec | **DONE** |
| 91 | W7-61 | [#520](https://github.com/sesquicadaver/MTDirector/issues/520) | CT-DRIFT-01 — DriftService GrpcHost contract Living Spec | **DONE** |
| 92 | W7-62 | [#522](https://github.com/sesquicadaver/MTDirector/issues/522) | CT-AUDIT-01 — AuditService GrpcHost contract Living Spec | **DONE** |
| 93 | W7-63 | [#524](https://github.com/sesquicadaver/MTDirector/issues/524) | CT-ROUTING-01 — RoutingAssuranceService ProtoContract + GrpcHost Living Spec | **DONE** |
| 94 | W7-64 | [#526](https://github.com/sesquicadaver/MTDirector/issues/526) | CT-INCIDENT-01 — IncidentService ProtoContract + GrpcHost Living Spec | **DONE** |
| 95 | W7-65 | [#528](https://github.com/sesquicadaver/MTDirector/issues/528) | Seed next product tranche after PLAN-04 → PLAN-05 Desktop operator-surface | **DONE** |
| 96 | W7-66 | [#530](https://github.com/sesquicadaver/MTDirector/issues/530) | PLAN-05 — Inventory next Desktop operator-surface Living Spec product tranche | **DONE** |
| 97 | W7-67 | [#532](https://github.com/sesquicadaver/MTDirector/issues/532) | DESK-AUDIT-01 — Desktop Audit panel Living Spec vs AuditGrpcHost | **DONE** |
| 98 | W7-68 | [#534](https://github.com/sesquicadaver/MTDirector/issues/534) | DESK-DRIFT-01 — Desktop Drift panel Living Spec vs DriftGrpcHost | **DONE** |
| 99 | W7-69 | [#536](https://github.com/sesquicadaver/MTDirector/issues/536) | DESK-ZONE-01 — Desktop Zones panel Living Spec vs ZoneGrpcHost | **DONE** |
| 100 | W7-70 | [#538](https://github.com/sesquicadaver/MTDirector/issues/538) | DESK-ROUTING-01 — Desktop Routing assurance Living Spec vs RoutingAssuranceGrpcHost | **DONE** |
| 101 | W7-71 | [#540](https://github.com/sesquicadaver/MTDirector/issues/540) | Seed next product tranche after PLAN-05 Desktop operator-surface | **DONE** |
| 102 | W7-72 | [#542](https://github.com/sesquicadaver/MTDirector/issues/542) | PLAN-06 — Inventory next Incident Desktop operator-surface Living Spec product tranche | **DONE** |
| 103 | W7-73 | [#544](https://github.com/sesquicadaver/MTDirector/issues/544) | DESK-INCIDENT-01 — Desktop Incident ViewModel + client Living Spec vs IncidentGrpcHost | **DONE** |
| 104 | W7-74 | [#546](https://github.com/sesquicadaver/MTDirector/issues/546) | DESK-INCIDENT-02 — Desktop Incident MainWindow panel Living Spec | **DONE** |
| 105 | W7-75 | [#548](https://github.com/sesquicadaver/MTDirector/issues/548) | DESK-INCIDENT-03 — Desktop Incident Bind assessment Living Spec | **DONE** |
| 106 | W7-76 | [#550](https://github.com/sesquicadaver/MTDirector/issues/550) | DESK-INCIDENT-04 — Desktop Incident fail-closed no deploy/overlay Living Spec | **DONE** |
| 107 | W7-77 | [#552](https://github.com/sesquicadaver/MTDirector/issues/552) | Seed next product tranche after PLAN-06 Incident Desktop operator-surface | **DONE** |
| 108 | W7-78 | [#554](https://github.com/sesquicadaver/MTDirector/issues/554) | PLAN-07 — Inventory next Core MVP Desktop operator-surface Living Spec product tranche | **DONE** |
| 109 | W7-79 | [#556](https://github.com/sesquicadaver/MTDirector/issues/556) | DESK-POLICY-01 — Desktop Policies panel Living Spec vs PolicyGrpcHost | **DONE** |
| 110 | W7-80 | [#558](https://github.com/sesquicadaver/MTDirector/issues/558) | Seed next PLAN-07 row after DESK-POLICY-01 → DESK-DEPLOY-01 | **DONE** |
| 111 | W7-81 | [#560](https://github.com/sesquicadaver/MTDirector/issues/560) | DESK-DEPLOY-01 — Desktop Deployment Living Spec vs DeploymentGrpcHost | **DONE** |
| 112 | W7-82 | [#562](https://github.com/sesquicadaver/MTDirector/issues/562) | Seed next PLAN-07 row after DESK-DEPLOY-01 → DESK-ONBOARD-01 | **DONE** |
| 113 | W7-83 | [#564](https://github.com/sesquicadaver/MTDirector/issues/564) | DESK-ONBOARD-01 — Desktop Onboarding Living Spec vs OnboardingGrpcHost | **DONE** |
| 114 | W7-84 | [#566](https://github.com/sesquicadaver/MTDirector/issues/566) | Seed next PLAN-07 row after DESK-ONBOARD-01 → DESK-SNAPSHOT-01 | **DONE** |
| 115 | W7-85 | [#568](https://github.com/sesquicadaver/MTDirector/issues/568) | DESK-SNAPSHOT-01 — Desktop Snapshot Living Spec vs SnapshotGrpcHost | **DONE** |
| 116 | W7-86 | [#570](https://github.com/sesquicadaver/MTDirector/issues/570) | Seed next PLAN-07 row after DESK-SNAPSHOT-01 → DESK-INVENTORY-01 | **DONE** |
| 117 | W7-87 | [#572](https://github.com/sesquicadaver/MTDirector/issues/572) | DESK-INVENTORY-01 — Desktop Inventory Living Spec vs InventoryGrpcHost | **DONE** |
| 118 | W7-88 | [#574](https://github.com/sesquicadaver/MTDirector/issues/574) | Seed next product tranche after PLAN-07 Core MVP Desktop | **DONE** |
| 119 | W7-89 | [#577](https://github.com/sesquicadaver/MTDirector/issues/577) | PLAN-08 — Inventory next Desktop secondary operator-surface Living Spec product tranche | **DONE** |
| 120 | W7-90 | [#578](https://github.com/sesquicadaver/MTDirector/issues/578) | DESK-NODE-01 — Desktop Node VRRP pair Living Spec vs InventoryGrpcHost | **DONE** |
| 121 | W7-91 | [#580](https://github.com/sesquicadaver/MTDirector/issues/580) | Seed next PLAN-08 row after DESK-NODE-01 → DESK-NBR-01 | **DONE** |
| 122 | W7-92 | [#582](https://github.com/sesquicadaver/MTDirector/issues/582) | DESK-NBR-01 — Desktop Neighbor candidates Living Spec depth | **DONE** |
| 123 | W7-93 | [#585](https://github.com/sesquicadaver/MTDirector/issues/585) | Seed next PLAN-08 row after DESK-NBR-01 → DESK-PROBE-01 | **DONE** |
| 124 | W7-94 | [#586](https://github.com/sesquicadaver/MTDirector/issues/586) | DESK-PROBE-01 — Desktop ValidateDeviceConnection probe Living Spec depth | **DONE** |
| 125 | W7-95 | [#589](https://github.com/sesquicadaver/MTDirector/issues/589) | Seed next PLAN-08 row after DESK-PROBE-01 → DESK-POLICY-02 | **DONE** |
| 126 | W7-96 | [#590](https://github.com/sesquicadaver/MTDirector/issues/590) | DESK-POLICY-02 — Desktop Policy safety analysis Living Spec depth | **DONE** |
| 127 | W7-97 | [#593](https://github.com/sesquicadaver/MTDirector/issues/593) | Seed next product tranche after PLAN-08 → PLAN-09 | **DONE** |
| 128 | W7-98 | [#594](https://github.com/sesquicadaver/MTDirector/issues/594) | PLAN-09 — Inventory next Desktop connection-status operator-surface Living Spec product tranche | **DONE** |
| 129 | W7-99 | [#597](https://github.com/sesquicadaver/MTDirector/issues/597) | DESK-CONN-01 — Desktop Connect/Disconnect Living Spec depth | **DONE** |
| 130 | W7-100 | [#598](https://github.com/sesquicadaver/MTDirector/issues/598) | Seed next PLAN-09 row after DESK-CONN-01 → DESK-MTLS-01 | **DONE** |
| 131 | W7-101 | [#600](https://github.com/sesquicadaver/MTDirector/issues/600) | DESK-MTLS-01 — Desktop mTLS actor status Living Spec depth | **DONE** |
| 132 | W7-102 | [#603](https://github.com/sesquicadaver/MTDirector/issues/603) | Seed next PLAN-09 row after DESK-MTLS-01 → DESK-AUTH-01 | **DONE** |
| 133 | W7-103 | [#604](https://github.com/sesquicadaver/MTDirector/issues/604) | DESK-AUTH-01 — Desktop AuthenticationFailed/TlsError Living Spec depth | **DONE** |
| 134 | W7-104 | [#607](https://github.com/sesquicadaver/MTDirector/issues/607) | Seed next product tranche after PLAN-09 → PLAN-10 | **DONE** |
| 135 | W7-105 | [#608](https://github.com/sesquicadaver/MTDirector/issues/608) | PLAN-10 — Inventory next product Living Spec tranche after PLAN-09 | **OPEN** |

**§3.C NEXT = W7-105 (#608)**. W7-104 **DONE**; PLAN-09 **COMPLETE**; PLAN-08 **COMPLETE**; PLAN-09 inventory **DONE**; PLAN-07 **COMPLETE**; PLAN-05 **COMPLETE**; PLAN-06 **COMPLETE**. CRS/physical lab runner remains ops-parallel ([`known-limitations.md`](../release/known-limitations.md)), not a product §3 stop-gate.

## Anti-goals (unchanged)

- Local SemanticDiffEngine on Desktop
- `WriteEnabled=true` “so the UI works”
- Auto-fix drift / Save and Deploy
- Fake VRRP role labels
- Treating GNS3 phase N as a Desktop/Contracts stop-gate

## DoD per product PR

Issue AC; Living Spec row; CHANGELOG; CI Linux validate + Windows Desktop; no `pass` / `NotImplemented`; Domain/App ↛ RouterOS; this plan + alignment + ROADMAP NEXT advanced in the same PR.
