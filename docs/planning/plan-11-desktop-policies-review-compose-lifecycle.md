# PLAN-11 — Desktop Policies review-compose lifecycle Living Spec product tranche

**Date:** 2026-09-08  
**PLAN issue / queue:** [W7-112 / PLAN-11 #625](https://github.com/sesquicadaver/MTDirector/issues/625)  
**Predecessor:** PLAN-10 Desktop shell chrome & Policies authoring depth COMPLETE (DESK-SHELL…DESK-REORDER); product seed W7-111  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-05…10 closed host-aligned panels, secondary surfaces, connection-status, shell chrome, Diff, and Move up/down Living Specs. PLAN-11 keeps `/autopilot` from idling by inventoring **Desktop Policies review/compose lifecycle** rows: SubmitForReview, ComposeEffective + RecordAnalysisRun, and Approve/Bind/Compile gate paths that today exist in UI (and presence checks in DESK-POLICY-01 / MVP workflows) but lack dedicated `Desktop*LivingSpecTests` depth for the execute path.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles, Policies “Save and Deploy” outside MVP scope lock  
- Replacing DESK-POLICY-01 / DESK-POLICY-02 / DESK-DIFF-01 / DESK-REORDER-01 completed Living Specs  
- Controller-only policy proto contract rows (already PLAN-04 / CT-*)

## Evidence baseline (review-compose lifecycle)

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| SubmitForReview | `DesktopPoliciesSubmitLivingSpecTests` + DESK-POLICY-01 presence | **DESK-SUBMIT-01 DONE** |
| ComposeEffective + RecordAnalysisRun | presence / MVP Ac5; `ComposeCommand` / `RecordAnalysisCommand` | PLAN-11 (**DESK-COMPOSE-01** next) |
| Approve + Bind + CompileNodeFilterArtifacts | presence / MVP Ac5; `ApproveCommand` / `BindCommand` / `CompileCommand` | PLAN-11 |

## Ranked Desktop Policies review-compose lifecycle tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-SUBMIT-01** | SubmitForReview execute path lacks dedicated Desktop Living Spec depth | `PoliciesViewModel.SubmitCommand`; `IPolicyPanelService.SubmitForReviewAsync`; `DesktopPoliciesSubmitLivingSpecTests` | **W7-114 DONE** (#628) |
| 2 | **DESK-COMPOSE-01** | ComposeEffective + RecordAnalysisRun lacks dedicated Desktop Living Spec depth | `ComposeCommand` / `RecordAnalysisCommand`; `ComposeAsync` / `RecordAnalysisRunAsync` | **W7-115 OPEN** (#629); seeded by **W7-113 DONE** (#626) |
| 3 | **DESK-GATE-01** | Approve / Bind / Compile gate path lacks dedicated Desktop Living Spec depth | `ApproveCommand` / `BindCommand` / `CompileCommand`; `ApproveAsync` / `BindAsync` / `CompileNodeFilterArtifactsAsync` | after DESK-COMPOSE-01 (**W7-117 OPEN** #632 → implement **W7-116** #630) |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**§3.C NEXT = W7-115 (#629)** — DESK-COMPOSE-01 Desktop Policies Compose+RecordAnalysis Living Spec depth.
