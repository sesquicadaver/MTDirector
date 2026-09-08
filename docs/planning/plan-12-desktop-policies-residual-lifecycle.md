# PLAN-12 — Desktop Policies residual lifecycle Living Spec product tranche

**Date:** 2026-09-08  
**PLAN issue / queue:** [W7-119 / PLAN-12 #638](https://github.com/sesquicadaver/MTDirector/issues/638)  
**Predecessor:** PLAN-11 Desktop Policies review-compose lifecycle COMPLETE (DESK-SUBMIT…DESK-GATE); product seed W7-118  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-05…11 closed host-aligned panels through review-compose gates. PLAN-12 keeps `/autopilot` from idling by inventoring **Desktop Policies residual lifecycle** rows that still only have presence/MVP checks: AcknowledgeWarning, Create draft / Load revision, and Catalog refresh.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles, Policies “Save and Deploy” outside MVP scope lock  
- Replacing DESK-POLICY-01 / DESK-POLICY-02 / DESK-SUBMIT-01 / DESK-COMPOSE-01 / DESK-GATE-01 completed Living Specs  
- Controller-only policy proto contract rows (already PLAN-04 / CT-*)

## Evidence baseline (residual lifecycle)

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| AcknowledgeWarning | MVP / DESK-POLICY-01 presence; `AcknowledgeWarningCommand` | PLAN-12 |
| Create draft / Load revision | `CreateDraftCommand` / `LoadCommand`; panel Create/Load | PLAN-12 |
| Catalog refresh | `RefreshCatalogCommand` / `ListPolicies` catalog surface | PLAN-12 |

## Ranked Desktop Policies residual lifecycle tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-ACK-01** | AcknowledgeWarning execute path lacks dedicated Desktop Living Spec depth | `PoliciesViewModel.AcknowledgeWarningCommand`; `IPolicyPanelService.AcknowledgeWarningAsync`; `IPolicyServiceClient.AcknowledgeWarningAsync` | **W7-120 OPEN** (#641); seeded by inventory |
| 2 | **DESK-DRAFT-01** | Create draft / Load revision lacks dedicated Desktop Living Spec depth | `CreateDraftCommand` / `LoadCommand`; `CreateDraftAsync` / `LoadRevisionAsync` | seeded after DESK-ACK-01 (**W7-121 OPEN** #642 → implement **W7-122** #643) |
| 3 | **DESK-CATALOG-01** | Catalog refresh / list path lacks dedicated Desktop Living Spec depth | `RefreshCatalogCommand`; `ListPolicies` / catalog reload surface | seeded after DESK-DRAFT-01 (**W7-123 OPEN** #644 → implement **W7-124** #645) |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**§3.C NEXT = W7-120 (#641)** — DESK-ACK-01 Desktop Policies AcknowledgeWarning Living Spec depth.
