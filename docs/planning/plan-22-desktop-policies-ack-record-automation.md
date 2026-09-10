# PLAN-22 — Desktop Policies acknowledge/record-analysis AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **OPEN** (W7-189); seeded by **W7-188 OPEN** after **PLAN-21 COMPLETE**  
**PLAN issue / queue:** [W7-189 / PLAN-22 #780](https://github.com/sesquicadaver/MTDirector/issues/780)  
**Predecessor:** PLAN-21 Policies authoring residual AutomationProperties **COMPLETE**; product seed **W7-188**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Policies Record analysis / Acknowledge warning buttons have Content text but no `AutomationProperties.Name` (authoring residual + lifecycle rows already locked in PLAN-20/21).

## Principles

1. Residual Policies review-ack actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Replacing PLAN-16…21 authoring / lifecycle / shell / Incident locks  
- New Policies RPCs  

## Ranked Desktop Policies acknowledge/record-analysis a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-POLICY-ACK-01** | Policies Record analysis / Acknowledge warning lack AutomationProperties.Name | `MainWindow.axaml` Policies panel | seed after PLAN-22 inventory |
| 2 | **DESK-A11Y-POLICY-ACK-02** | Regression lock: ack/record Names + authoring residual + lifecycle + shell/Incident Names matrix | Living Spec matrix | seed after DESK-A11Y-POLICY-ACK-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-188 (#779)** — Seed next product tranche after PLAN-21 → PLAN-22 (this inventory is W7-189 OPEN).
