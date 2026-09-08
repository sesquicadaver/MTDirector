# PLAN-08 — Desktop secondary operator-surface Living Spec product tranche

**Date:** 2026-09-08  
**PLAN issue / queue:** [W7-89 / PLAN-08 #577](https://github.com/sesquicadaver/MTDirector/issues/577)  
**Predecessor:** PLAN-07 Core MVP Desktop COMPLETE (DESK-POLICY…DESK-INVENTORY); product seed W7-88  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-05…07 closed host-aligned Desktop Living Specs for Audit → Routing, Incident Ingest/Bind, and Core MVP Policies → Inventory. PLAN-08 keeps `/autopilot` from idling by inventoring **Desktop secondary operator-surface** rows: Node VRRP pair depth, neighbor candidate apply, device probe, and Policy safety analysis execute paths that exist in UI/clients but lack dedicated `Desktop*LivingSpecTests` beyond presence checks in DESK-INVENTORY-01 / DESK-POLICY-01.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles  
- EndpointPresence / ResponseFeedback / Incident deploy-overlay Desktop UI (Application-only / scope lock)  
- Replacing PLAN-07 Core MVP Living Specs (Desktop **adds** secondary depth coverage)

## Evidence baseline (secondary vs Core MVP)

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Inventory tree / Add router submit | `DesktopInventoryLivingSpecTests` | Core MVP **DONE** |
| Node VRRP validate / capture-all | command presence only in DESK-INVENTORY-01 Ac2/Ac5 | PLAN-08 |
| Neighbor load / apply candidate | presence in DESK-INVENTORY-01 / MVP workflows | PLAN-08 |
| Device connection probe | presence in DESK-INVENTORY-01 | PLAN-08 |
| Policy safety analysis refresh | presence in DESK-POLICY-01 Ac2/Ac5 | PLAN-08 |

## Ranked Desktop secondary operator-surface tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-NODE-01** | Node VRRP pair validate/capture lacks dedicated Desktop Living Spec depth vs InventoryGrpcHost | `NodeDetailViewModel` (`ValidateVrrpPairCommand`, `CaptureAllMembersAndValidateCommand`); host: `InventoryGrpcHostTests` / `ValidateVrrpPairConsistency` | **W7-90 DONE** (#578) |
| 2 | **DESK-NBR-01** | Neighbor candidate load/apply lacks dedicated Desktop Living Spec depth | `AddRouterWizardViewModel` (`LoadNeighborsCommand`, `ApplyNeighborCandidateCommand`); `ListNeighborCandidates` | **W7-92 OPEN** (#582) |
| 3 | **DESK-PROBE-01** | ValidateDeviceConnection probe path lacks dedicated Desktop Living Spec depth | `AddRouterWizardViewModel.ProbeCommand`; `ValidateDeviceConnection` | seed after DESK-NBR-01 |
| 4 | **DESK-POLICY-02** | Policy safety analysis execute path lacks dedicated Desktop Living Spec depth | `PoliciesViewModel.RefreshSafetyAnalysisCommand`; `GetDevicePolicySafetyAnalysis` | seed after DESK-PROBE-01 |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**§3.C NEXT = W7-91 (#580)** — Seed next PLAN-08 row after DESK-NODE-01 → DESK-NBR-01.
