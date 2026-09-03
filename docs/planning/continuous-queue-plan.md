# Continuous product queue (PLAN-02)

**Date:** 2026-08-31  
**Baseline:** `main` @ `877a529` (W2.2 Routing assurance next-hop/subject fields, [#338](https://github.com/sesquicadaver/MTDirector/pull/338))  
**PLAN issue:** [PLAN-02 #339](https://github.com/sesquicadaver/MTDirector/issues/339)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

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
| gRPC actor ↔ authenticated principal | Controller authn | **W7-02 OPEN** |

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
W7-02 Bind gRPC actor to authenticated principal **OPEN**
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
| 32 | W7-02 | [#402](https://github.com/sesquicadaver/MTDirector/issues/402) | Bind gRPC actor to authenticated principal | **OPEN** |

**§3.C NEXT = W7-02 (#402)**. W7-01 **DONE**. CRS/physical lab runner remains ops-parallel ([`known-limitations.md`](../release/known-limitations.md)), not a product §3 stop-gate.

## Anti-goals (unchanged)

- Local SemanticDiffEngine on Desktop
- `WriteEnabled=true` “so the UI works”
- Auto-fix drift / Save and Deploy
- Fake VRRP role labels
- Treating GNS3 phase N as a Desktop/Contracts stop-gate

## DoD per product PR

Issue AC; Living Spec row; CHANGELOG; CI Linux validate + Windows Desktop; no `pass` / `NotImplemented`; Domain/App ↛ RouterOS; this plan + alignment + ROADMAP NEXT advanced in the same PR.
