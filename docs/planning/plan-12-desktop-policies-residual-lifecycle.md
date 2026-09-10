# PLAN-12 — Desktop Policies residual lifecycle Living Spec product tranche

**Date:** 2026-09-08  
**PLAN issue / queue:** [W7-119 / PLAN-12 #638](https://github.com/sesquicadaver/MTDirector/issues/638)  
**Predecessor:** PLAN-11 Desktop Policies review-compose lifecycle COMPLETE (DESK-SUBMIT…DESK-GATE); product seed W7-118  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-05…11 closed host-aligned panels through review-compose gates. PLAN-12 inventores **Desktop Policies residual lifecycle** rows that still only had presence/MVP checks: AcknowledgeWarning, Create draft / Load revision, and Catalog refresh.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles, Policies “Save and Deploy” outside MVP scope lock  
- Replacing DESK-POLICY-01 / DESK-POLICY-02 / DESK-SUBMIT-01 / DESK-COMPOSE-01 / DESK-GATE-01 / DESK-ACK-01 / DESK-DRAFT-01 completed Living Specs  
- Controller-only policy proto contract rows (already PLAN-04 / CT-*)

## Evidence baseline (residual lifecycle)

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| AcknowledgeWarning | `DesktopPoliciesAcknowledgeLivingSpecTests` + MVP / DESK-POLICY-01 presence | **DESK-ACK-01 DONE** |
| Create draft / Load revision | `DesktopPoliciesDraftLivingSpecTests` + presence / MVP | **DESK-DRAFT-01 DONE** |
| Catalog refresh | `DesktopPoliciesCatalogLivingSpecTests` + `RefreshCatalogCommand` / `ListPolicies` | **DESK-CATALOG-01 DONE**; **PLAN-12 COMPLETE** |

## Ranked Desktop Policies residual lifecycle tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-ACK-01** | AcknowledgeWarning execute path lacks dedicated Desktop Living Spec depth | `AcknowledgeWarningCommand`; `AcknowledgeWarningAsync`; `DesktopPoliciesAcknowledgeLivingSpecTests` | **W7-120 DONE** (#641) |
| 2 | **DESK-DRAFT-01** | Create draft / Load revision lacks dedicated Desktop Living Spec depth | `CreateDraftCommand` / `LoadCommand`; `CreateDraftAsync` / `LoadRevisionAsync`; `DesktopPoliciesDraftLivingSpecTests` | **W7-122 DONE** (#643); seeded by **W7-121 DONE** (#642) |
| 3 | **DESK-CATALOG-01** | Catalog refresh / list path lacks dedicated Desktop Living Spec depth | `RefreshCatalogCommand`; `ListPolicies` / catalog reload surface | **W7-124 DONE** (#645); seeded by **W7-123 DONE** (#644); **PLAN-12 COMPLETE** |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**PLAN-12 COMPLETE.** **W7-125 DONE** (#653). PLAN-13 inventory **DONE** (W7-126). **W7-127 DONE** (DESK-LAYOUT-00). **W7-128 DONE**. **W7-129 DONE** (DESK-LAYOUT-01). **W7-130 DONE**. **W7-131 DONE** (DESK-LAYOUT-02). **W7-132 DONE**. **W7-133 DONE** (DESK-LAYOUT-03). **PLAN-12 COMPLETE.** PLAN-13 **COMPLETE**. **§3.C NEXT = W7-199 (#800)** — Seed next product tranche after PLAN-13 → PLAN-14.
