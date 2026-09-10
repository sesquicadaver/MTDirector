# PLAN-17 — Desktop Incident bind-action AutomationProperties Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **DONE** (W7-164); **DESK-A11Y-ACTION-01 DONE** (W7-165); seed **W7-166 DONE**; **DESK-A11Y-ACTION-02 DONE** (W7-167); **PLAN-17 COMPLETE**  
**PLAN issue / queue:** [W7-164 / PLAN-17 #730](https://github.com/sesquicadaver/MTDirector/issues/730)  
**Predecessor:** PLAN-16 Incident AutomationProperties accessible-name **COMPLETE**; product seed **W7-163 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Incident Operations "Bind assessment" button exposes `AutomationProperties.Name` matching Content (DESK-A11Y-ACTION-01/02). Field Names remain locked by PLAN-16.

## Principles

1. Primary Incident actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name on Bind assessment without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Full toolbar/chrome a11y pass  
- New Incident RPCs  
- Replacing PLAN-16 field Name locks  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Bind assessment lacks accessible name before regression matrix |
| Risk | XAML AutomationProperties only; keep Command binding; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-16 COMPLETE; inventory locks **DESK-A11Y-ACTION-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Bind assessment button | Content + AutomationProperties.Name="Bind assessment" | **DESK-A11Y-ACTION-01 DONE** |
| Regression lock | Bind Name + Incident field Names matrix | **DESK-A11Y-ACTION-02 DONE** |

## Ranked Desktop Incident bind-action a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-ACTION-01** | Bind assessment button lacks AutomationProperties.Name | `MainWindow.axaml` Operations → Incident | **W7-165 DONE** (#733); seeded by inventory **W7-164 DONE** (#730) |
| 2 | **DESK-A11Y-ACTION-02** | Regression lock: Bind Name + Incident field Names matrix | Living Spec matrix | **W7-167 DONE** (#737); seeded by **W7-166 DONE** (#734); **PLAN-17 COMPLETE** |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**PLAN-17 COMPLETE.** PLAN-18 **COMPLETE**. Seeded PLAN-19 (W7-173/174). **§3.C NEXT = W7-181 (#764)** — Seed next product tranche after PLAN-18 → PLAN-19.
