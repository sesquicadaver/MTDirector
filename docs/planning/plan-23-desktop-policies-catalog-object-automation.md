# PLAN-23 — Desktop Policies catalog/object AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **DONE** (W7-194); **DESK-A11Y-POLICY-OBJ-01 DONE** (W7-195); seed **W7-196 DONE**; **DESK-A11Y-POLICY-OBJ-02 DONE** (W7-197); **PLAN-23 COMPLETE**  
**PLAN issue / queue:** [W7-194 / PLAN-23 #790](https://github.com/sesquicadaver/MTDirector/issues/790)  
**Predecessor:** PLAN-22 Policies acknowledge/record-analysis AutomationProperties **COMPLETE**; product seed **W7-193 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Policies catalog/object residual buttons (Refresh catalog / Upsert address / Upsert service / Replace contracts) expose `AutomationProperties.Name` matching Content (DESK-A11Y-POLICY-OBJ-01/02). Ack/record + authoring residual + lifecycle + shell/Incident rows remain locked by PLAN-16…22.

## Principles

1. Residual Policies catalog/object actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Replacing PLAN-16…22 authoring / ack / lifecycle / shell / Incident locks  
- New Policies RPCs  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Catalog/object actions lack accessible names after ack/record lock |
| Risk | XAML AutomationProperties only; keep Command bindings; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-22 COMPLETE; inventory locks **DESK-A11Y-POLICY-OBJ-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Refresh catalog | Content + AutomationProperties.Name | **DESK-A11Y-POLICY-OBJ-01 DONE** |
| Upsert address / Upsert service | Content + AutomationProperties.Name | **DESK-A11Y-POLICY-OBJ-01 DONE** |
| Replace contracts | Content + AutomationProperties.Name | **DESK-A11Y-POLICY-OBJ-01 DONE** |
| Regression lock | catalog/object Names + ack/record + authoring residual + lifecycle + shell/Incident Names matrix | **DESK-A11Y-POLICY-OBJ-02 DONE** |

## Ranked Desktop Policies catalog/object a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-POLICY-OBJ-01** | Policies Refresh catalog / Upsert address/service / Replace contracts lack AutomationProperties.Name | `MainWindow.axaml` Policies panel | **W7-195 DONE** (#793); seeded by inventory **W7-194 DONE** (#790) |
| 2 | **DESK-A11Y-POLICY-OBJ-02** | Regression lock: catalog/object Names + ack/record + authoring residual + lifecycle + shell/Incident Names matrix | Living Spec matrix | **W7-197 DONE** (#797); seeded by **W7-196 DONE** (#794); **PLAN-23 COMPLETE** |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**PLAN-23 COMPLETE.** Seeded PLAN-24 queue (W7-198/199). **W7-198 DONE.** **W7-199 DONE** (PLAN-24 inventory). **§3.C NEXT = W7-203 (#809)** — DESK-A11Y-OPS-01.
