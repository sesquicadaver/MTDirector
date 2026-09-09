# PLAN-17 — Desktop Incident bind-action AutomationProperties Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **OPEN** (W7-164); seeded by **W7-163 OPEN** after **PLAN-16 COMPLETE**  
**PLAN issue / queue:** [W7-164 / PLAN-17 #730](https://github.com/sesquicadaver/MTDirector/issues/730)  
**Predecessor:** PLAN-16 Incident AutomationProperties accessible-name **COMPLETE**; product seed **W7-163**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Incident Operations "Bind assessment" button has Content text but no `AutomationProperties.Name` (fields already locked in PLAN-16).

## Principles

1. Primary Incident actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name on Bind assessment without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Full toolbar/chrome a11y pass  
- New Incident RPCs  
- Replacing PLAN-16 field Name locks  

## Ranked Desktop Incident bind-action a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-ACTION-01** | Bind assessment button lacks AutomationProperties.Name | `MainWindow.axaml` Operations → Incident | seed after PLAN-17 inventory |
| 2 | **DESK-A11Y-ACTION-02** | Regression lock: Bind Name + Incident field Names matrix | Living Spec matrix | seed after DESK-A11Y-ACTION-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-163 (#729)** — Seed next product tranche after PLAN-16 → PLAN-17 (this inventory is W7-164 OPEN).
