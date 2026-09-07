# PLAN-05 — Desktop operator-surface Living Spec product tranche

**Date:** 2026-09-07  
**PLAN issue / queue:** [W7-66 / PLAN-05 #530](https://github.com/sesquicadaver/MTDirector/issues/530)  
**Predecessor:** PLAN-04 contract-test host coverage COMPLETE (W7-59…W7-64); product seed W7-65  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-04 closed Controller **GrpcHost** gaps for Deployment → Incident. PLAN-05 keeps `/autopilot` from idling by seeding **Desktop operator-surface Living Spec** rows: ViewModels / gRPC clients that already call host-covered services but lack (or only partially have) Desktop Living Specs comparable to connection-status / routing-assurance Desktop locks.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles  
- Replacing PLAN-04 GrpcHost tests (Desktop **adds** operator-surface coverage)  
- Incident Desktop panel (no `IncidentViewModel` yet) — stays Application/gRPC-only until a later tranche

## Evidence baseline (Desktop vs host)

| Operator surface | Desktop client / VM | Host contract | Desktop Living Spec |
|------------------|---------------------|---------------|---------------------|
| Connection / mTLS actor | `DesktopGrpcActorResolver` | Controller authn | W7-05 / W7-08 / W7-12 **DONE** |
| Routing assurance | `RoutingAssuranceViewModel` | `RoutingAssuranceGrpcHostTests` | `DesktopRoutingAssuranceLivingSpecTests` (partial) |
| Audit | `AuditViewModel` / `GrpcAuditServiceClient` | `AuditGrpcHostTests` | **missing Desktop Living Spec** |
| Drift | `DriftViewModel` / `GrpcDriftServiceClient` | `DriftGrpcHostTests` | **missing Desktop Living Spec** |
| Zones | `ZonesViewModel` / `GrpcZoneServiceClient` | `ZoneGrpcHostTests` | **missing Desktop Living Spec** |
| Policies / Onboarding / Deploy / Snapshot | existing VMs | PLAN-04 / earlier host tests | Desktop MVP workflows + panel Living Specs (baseline) |

## Ranked Desktop operator-surface tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-AUDIT-01** | Audit panel lacks Desktop Living Spec against host ListAuditEvents contract | `AuditViewModel`, `GrpcAuditServiceClient`; host: `AuditGrpcHostTests` | **W7-67 OPEN** (#532) |
| 2 | **DESK-DRIFT-01** | Drift panel lacks Desktop Living Spec against List/Get DriftEvents | `DriftViewModel`, `GrpcDriftServiceClient`; host: `DriftGrpcHostTests` | seed after DESK-AUDIT-01 |
| 3 | **DESK-ZONE-01** | Zones panel lacks Desktop Living Spec against Zone CRUD/resolve host contract | `ZonesViewModel`, `GrpcZoneServiceClient`; host: `ZoneGrpcHostTests` | seed after DESK-DRIFT-01 |
| 4 | **DESK-ROUTING-01** | Routing assurance Desktop Living Spec is partial vs CT-ROUTING-01 host detail | `DesktopRoutingAssuranceLivingSpecTests` + `RoutingAssuranceViewModel` | seed after DESK-ZONE-01 |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**§3.C NEXT = W7-67 (#532)** — DESK-AUDIT-01 Desktop Audit panel Living Spec vs AuditGrpcHost.
