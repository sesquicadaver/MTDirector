# PLAN-19 — Desktop shell Connect/Disconnect AutomationProperties Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **OPEN** (W7-174); seeded by **W7-173 DONE** after **PLAN-18 COMPLETE**  
**PLAN issue / queue:** [W7-174 / PLAN-19 #750](https://github.com/sesquicadaver/MTDirector/issues/750)  
**Predecessor:** PLAN-18 Incident ingest-action AutomationProperties **COMPLETE**; product seed **W7-173 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Shell chrome Connect/Disconnect buttons have Content text but no `AutomationProperties.Name` (Incident actions already locked in PLAN-16…18).

## Principles

1. Primary shell connection actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name on Connect/Disconnect without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Full toolbar/chrome a11y pass beyond Connect/Disconnect  
- New connection RPCs  
- Replacing PLAN-16…18 Incident locks  

## Ranked Desktop shell connection-action a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-CONN-01** | Connect/Disconnect buttons lack AutomationProperties.Name | `MainWindow.axaml` shell chrome | seed after PLAN-19 inventory |
| 2 | **DESK-A11Y-CONN-02** | Regression lock: Connect/Disconnect Names + Incident action Names matrix | Living Spec matrix | seed after DESK-A11Y-CONN-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-174 (#750)** — PLAN-19 Inventory Desktop shell Connect/Disconnect AutomationProperties Living Spec product tranche.
