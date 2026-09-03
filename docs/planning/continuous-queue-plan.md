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
residual ops: CRS / physical lab runner (not §3)
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

**§3.C NEXT = residual (CRS lab ops)**. SEC tranche closed. CRS/physical lab runner remains ops-parallel ([`known-limitations.md`](../release/known-limitations.md)), not a product §3 row.

## Anti-goals (unchanged)

- Local SemanticDiffEngine on Desktop
- `WriteEnabled=true` “so the UI works”
- Auto-fix drift / Save and Deploy
- Fake VRRP role labels
- Treating GNS3 phase N as a Desktop/Contracts stop-gate

## DoD per product PR

Issue AC; Living Spec row; CHANGELOG; CI Linux validate + Windows Desktop; no `pass` / `NotImplemented`; Domain/App ↛ RouterOS; this plan + alignment + ROADMAP NEXT advanced in the same PR.
