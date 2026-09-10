# PLAN-21 — Desktop Policies authoring residual AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **OPEN** (W7-184); seeded by **W7-183 OPEN** after **PLAN-20 COMPLETE**  
**PLAN issue / queue:** [W7-184 / PLAN-21 #770](https://github.com/sesquicadaver/MTDirector/issues/770)  
**Predecessor:** PLAN-20 Policies lifecycle-action AutomationProperties **COMPLETE**; product seed **W7-183**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Policies authoring residual buttons (Create draft / Load / rule CRUD-move / Compose / Diff / Analyze safety / Compile) have Content text but no `AutomationProperties.Name` (lifecycle row already locked in PLAN-20).

## Principles

1. Primary Policies authoring residual actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Replacing PLAN-16…20 lifecycle / shell / Incident locks  
- New Policies RPCs  

## Ranked Desktop Policies authoring residual a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-POLICY-EDIT-01** | Policies Create draft / Load / rule CRUD-move / Diff / Compose lack AutomationProperties.Name | `MainWindow.axaml` Policies panel | seed after PLAN-21 inventory |
| 2 | **DESK-A11Y-POLICY-EDIT-02** | Regression lock: authoring residual Names + lifecycle + shell/Incident Names matrix | Living Spec matrix | seed after DESK-A11Y-POLICY-EDIT-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-183 (#769)** — Seed next product tranche after PLAN-20 → PLAN-21 (this inventory is W7-184 OPEN).
