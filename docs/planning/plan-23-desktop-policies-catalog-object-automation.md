# PLAN-23 — Desktop Policies catalog/object AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **OPEN** (W7-194); seeded by **W7-193 OPEN** after **PLAN-22 COMPLETE**  
**PLAN issue / queue:** [W7-194 / PLAN-23 #790](https://github.com/sesquicadaver/MTDirector/issues/790)  
**Predecessor:** PLAN-22 Policies acknowledge/record-analysis AutomationProperties **COMPLETE**; product seed **W7-193**  
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

## Ranked Desktop Policies catalog/object a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-POLICY-OBJ-01** | Policies Refresh catalog / Upsert address/service / Replace contracts lack AutomationProperties.Name | `MainWindow.axaml` Policies panel | seed after PLAN-23 inventory |
| 2 | **DESK-A11Y-POLICY-OBJ-02** | Regression lock: catalog/object Names + ack/record + authoring residual + lifecycle + shell/Incident Names matrix | Living Spec matrix | seed after DESK-A11Y-POLICY-OBJ-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-193 (#789)** — Seed next product tranche after PLAN-22 → PLAN-23 (this inventory is W7-194 OPEN).
