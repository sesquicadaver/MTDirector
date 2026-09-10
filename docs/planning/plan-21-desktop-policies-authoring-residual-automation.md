# PLAN-21 — Desktop Policies authoring residual AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **DONE** (W7-184); **DESK-A11Y-POLICY-EDIT-01 DONE** (W7-185); seed **W7-186 DONE**; **DESK-A11Y-POLICY-EDIT-02 DONE** (W7-187); **PLAN-21 COMPLETE**  
**PLAN issue / queue:** [W7-184 / PLAN-21 #770](https://github.com/sesquicadaver/MTDirector/issues/770)  
**Predecessor:** PLAN-20 Policies lifecycle-action AutomationProperties **COMPLETE**; product seed **W7-183 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Policies authoring residual buttons (Create draft / Load / rule CRUD-move / Compose / Diff / Analyze safety / Compile) expose `AutomationProperties.Name` matching Content (DESK-A11Y-POLICY-EDIT-01/02). Lifecycle + shell/Incident rows remain locked by PLAN-16…20.

## Principles

1. Primary Policies authoring residual actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Replacing PLAN-16…20 lifecycle / shell / Incident locks  
- New Policies RPCs  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Authoring residual actions lack accessible names after lifecycle row lock |
| Risk | XAML AutomationProperties only; keep Command bindings; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-20 COMPLETE; inventory locks **DESK-A11Y-POLICY-EDIT-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Create draft / Load | Content + AutomationProperties.Name | **DESK-A11Y-POLICY-EDIT-01 DONE** |
| Rule Add/Update/Delete/Move up/down | Content + AutomationProperties.Name | **DESK-A11Y-POLICY-EDIT-01 DONE** |
| Compose / Diff / Analyze safety / Compile | Content + AutomationProperties.Name | **DESK-A11Y-POLICY-EDIT-01 DONE** |
| Regression lock | authoring residual Names + lifecycle + shell/Incident Names matrix | **DESK-A11Y-POLICY-EDIT-02 DONE** |

## Ranked Desktop Policies authoring residual a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-POLICY-EDIT-01** | Policies Create draft / Load / rule CRUD-move / Diff / Compose / Analyze safety / Compile lack AutomationProperties.Name | `MainWindow.axaml` Policies panel | **W7-185 DONE** (#773); seeded by inventory **W7-184 DONE** (#770) |
| 2 | **DESK-A11Y-POLICY-EDIT-02** | Regression lock: authoring residual Names + lifecycle + shell/Incident Names matrix | Living Spec matrix | **W7-187 DONE** (#777); seeded by **W7-186 DONE** (#774); **PLAN-21 COMPLETE** |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**PLAN-21 COMPLETE.** Seeded PLAN-22 queue (W7-188/189). **W7-188 DONE.** **W7-189 DONE** (PLAN-22 inventory). **§3.C NEXT = W7-202 (#807)** — DESK-A11Y-POLICY-ACK-01.
