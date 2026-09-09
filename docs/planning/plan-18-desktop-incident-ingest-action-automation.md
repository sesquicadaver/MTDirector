# PLAN-18 — Desktop Incident ingest-action AutomationProperties Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **OPEN** (W7-169); seeded by **W7-168 DONE** after **PLAN-17 COMPLETE**  
**PLAN issue / queue:** [W7-169 / PLAN-18 #740](https://github.com/sesquicadaver/MTDirector/issues/740)  
**Predecessor:** PLAN-17 Incident bind-action AutomationProperties **COMPLETE**; product seed **W7-168 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Incident Operations "Ingest signal" button has Content text but no `AutomationProperties.Name` (Bind assessment + fields already locked in PLAN-16/17).

## Principles

1. Primary Incident actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name on Ingest signal without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Full toolbar/chrome a11y pass  
- New Incident RPCs  
- Replacing PLAN-16/17 field / Bind Name locks  

## Ranked Desktop Incident ingest-action a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-INGEST-01** | Ingest signal button lacks AutomationProperties.Name | `MainWindow.axaml` Operations → Incident | seed after PLAN-18 inventory |
| 2 | **DESK-A11Y-INGEST-02** | Regression lock: Ingest Name + Bind Name + Incident field Names matrix | Living Spec matrix | seed after DESK-A11Y-INGEST-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-169 (#740)** — PLAN-18 Inventory Desktop Incident ingest-action AutomationProperties Living Spec product tranche.
