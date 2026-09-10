# PLAN-23 — Desktop Policies catalog/object AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **DONE** (W7-194); seeded by **W7-193 DONE**; first implement **DESK-A11Y-POLICY-OBJ-01 OPEN** (W7-195)  
**PLAN issue / queue:** [W7-194 / PLAN-23 #790](https://github.com/sesquicadaver/MTDirector/issues/790)  
**Predecessor:** PLAN-22 Policies acknowledge/record-analysis AutomationProperties **COMPLETE**; product seed **W7-193 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Policies catalog/object residual buttons (Refresh catalog / Upsert address / Upsert service / Replace contracts) have Content text but no `AutomationProperties.Name` (ack/record + authoring residual + lifecycle rows already locked in PLAN-20…22).

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
| Refresh catalog | Content text; no AutomationProperties.Name | DESK-A11Y-POLICY-OBJ-01 |
| Upsert address / Upsert service | Content text; no AutomationProperties.Name | DESK-A11Y-POLICY-OBJ-01 |
| Replace contracts | Content text; no AutomationProperties.Name | DESK-A11Y-POLICY-OBJ-01 |
| Regression lock | catalog/object Names + ack/record + authoring residual + lifecycle + shell/Incident Names matrix | DESK-A11Y-POLICY-OBJ-02 |

## Ranked Desktop Policies catalog/object a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-POLICY-OBJ-01** | Policies Refresh catalog / Upsert address/service / Replace contracts lack AutomationProperties.Name | `MainWindow.axaml` Policies panel | **W7-195 OPEN** (#793); seeded by inventory **W7-194 DONE** (#790) |
| 2 | **DESK-A11Y-POLICY-OBJ-02** | Regression lock: catalog/object Names + ack/record + authoring residual + lifecycle + shell/Incident Names matrix | Living Spec matrix | seeded by **W7-196 OPEN** (#794) after DESK-A11Y-POLICY-OBJ-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-195 (#793)** — DESK-A11Y-POLICY-OBJ-01 Policies catalog/object AutomationProperties.Name Living Spec.
