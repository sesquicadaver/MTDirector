# PLAN-06 — Incident Desktop operator-surface Living Spec product tranche

**Date:** 2026-09-07  
**PLAN issue / queue:** [W7-72 / PLAN-06 #542](https://github.com/sesquicadaver/MTDirector/issues/542)  
**Predecessor:** PLAN-05 Desktop operator-surface COMPLETE (W7-66…W7-70); product seed W7-71  
**Status:** **COMPLETE** (DESK-INCIDENT-01…04 DONE)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-05 closed Desktop Living Specs for Audit → Routing against host-covered services, and explicitly deferred **Incident Desktop** (`IncidentViewModel` absent). PLAN-04 already closed `IncidentService` GrpcHost (`IncidentGrpcHostTests` / CT-INCIDENT-01). PLAN-06 inventored and delivered **Incident Desktop operator-surface Living Spec** rows: Contracts-only ViewModel / gRPC client / MainWindow bindings aligned with the SEC-06 scoped wire surface (`IngestIncidentSignal` + `BindIncidentResponseAssessment`), plus fail-closed lock against deploy/overlay/feedback Desktop RPCs.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles  
- Incident **deploy / overlay / feedback** RPCs on Desktop wire (proto: Application-only until separately queued)  
- SIEM/SOAR product UI or multi-tenant incident console (MVP/M7 scope lock)  
- Replacing CT-INCIDENT-01 GrpcHost tests (Desktop **adds** operator-surface coverage)

## Evidence baseline (host vs Desktop)

| Surface | Host / Application | Desktop today | Gap |
|---------|--------------------|---------------|-----|
| `IncidentService` Ingest + BindAssessment | `IncidentGrpcHostTests`, `IncidentProtoContractTests` | ViewModel+client + Operations panel + Bind UX + fail-closed lock | **CLOSED** (PLAN-06) |
| Deny overlay deploy / expire / feedback | Application use cases (SEC-10…) | not on Desktop wire (`DesktopIncidentFailClosedLivingSpecTests`) | locked |
| Audit / Drift / Zones / Routing panels | GrpcHost + Desktop Living Specs | PLAN-05 **DONE** | baseline |

## Ranked Incident Desktop operator-surface tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-INCIDENT-01** | No Desktop Incident ViewModel / Contracts client Living Spec vs host Ingest+Bind | `IncidentViewModel` + `IIncidentServiceClient` / `GrpcIncidentServiceClient`; host: `IncidentGrpcHostTests` | **W7-73 DONE** (#544) |
| 2 | **DESK-INCIDENT-02** | MainWindow lacks Incident operator panel bindings / empty-state Living Spec | `MainWindow.axaml` Operations → Incident tab + status/error/empty-state | **W7-74 DONE** (#546) |
| 3 | **DESK-INCIDENT-03** | Bind assessment operator path lacks Desktop Living Spec (selection → Bind RPC) | `BindIncidentResponseAssessment` client + ViewModel command + MainWindow bind form | **W7-75 DONE** (#548) |
| 4 | **DESK-INCIDENT-04** | Desktop must stay fail-closed: no deploy/overlay/write RPCs beyond SEC-06 wire | `DesktopIncidentFailClosedLivingSpecTests` vs `incident.proto` / host wire | **W7-76 DONE** (#550) |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## Successor

After PLAN-06 **COMPLETE**, §3.C product seed **W7-77 DONE** advanced to **PLAN-07** inventory (**W7-78 DONE**); first atomic row **DESK-POLICY-01** (**W7-79 DONE**); NEXT seed W7-80.

## §3.C NEXT

**§3.C NEXT = W7-175 (#753)** — Seed next PLAN-07 row after DESK-POLICY-01 → DESK-DEPLOY-01.
