# PLAN-05 — Desktop operator-surface Living Spec product tranche

**Date:** 2026-09-07  
**PLAN issue / queue:** [W7-66 / PLAN-05 #530](https://github.com/sesquicadaver/MTDirector/issues/530)  
**Predecessor:** PLAN-04 contract-test host coverage COMPLETE (W7-59…W7-64); product seed W7-65  
**Status:** **COMPLETE** (DESK-AUDIT…DESK-ROUTING DONE)  
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
| Routing assurance | `RoutingAssuranceViewModel` | `RoutingAssuranceGrpcHostTests` | `DesktopRoutingAssuranceLivingSpecTests` (M7.1-10 + DESK-ROUTING-01) |
| Audit | `AuditViewModel` / `GrpcAuditServiceClient` | `AuditGrpcHostTests` | `DesktopAuditLivingSpecTests` (W7-67) |
| Drift | `DriftViewModel` / `GrpcDriftServiceClient` | `DriftGrpcHostTests` | `DesktopDriftLivingSpecTests` (W7-68) |
| Zones | `ZonesViewModel` / `GrpcZoneServiceClient` | `ZoneGrpcHostTests` | `DesktopZonesLivingSpecTests` (W7-69) |
| Policies / Onboarding / Deploy / Snapshot | existing VMs | PLAN-04 / earlier host tests | Desktop MVP workflows + panel Living Specs (baseline) |

## Ranked Desktop operator-surface tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-AUDIT-01** | Audit panel lacks Desktop Living Spec against host ListAuditEvents contract | `AuditViewModel`, `GrpcAuditServiceClient`; host: `AuditGrpcHostTests` | **W7-67 DONE** (#532) |
| 2 | **DESK-DRIFT-01** | Drift panel lacks Desktop Living Spec against List/Get DriftEvents | `DriftViewModel`, `GrpcDriftServiceClient`; host: `DriftGrpcHostTests` | **W7-68 DONE** (#534) |
| 3 | **DESK-ZONE-01** | Zones panel lacks Desktop Living Spec against Zone CRUD/resolve host contract | `ZonesViewModel`, `GrpcZoneServiceClient`; host: `ZoneGrpcHostTests` | **W7-69 DONE** (#536) |
| 4 | **DESK-ROUTING-01** | Routing assurance Desktop Living Spec is partial vs CT-ROUTING-01 host detail | `DesktopRoutingAssuranceLivingSpecTests` + `RoutingAssuranceViewModel` | **W7-70 DONE** (#538) |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## Successor

PLAN-06 Incident Desktop operator-surface inventory: [`plan-06-incident-desktop-operator-surface.md`](plan-06-incident-desktop-operator-surface.md).

## §3.C NEXT

**§3.C NEXT = W7-197 (#797)** — PLAN-07 Inventory next product Living Spec tranche after PLAN-06 (PLAN-06).
