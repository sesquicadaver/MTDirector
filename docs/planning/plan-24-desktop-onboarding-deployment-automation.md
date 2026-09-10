# PLAN-24 — Desktop Onboarding/Deployment AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **OPEN** (W7-199); seeded by **W7-198 OPEN** after **PLAN-23 COMPLETE**  
**PLAN issue / queue:** [W7-199 / PLAN-24 #800](https://github.com/sesquicadaver/MTDirector/issues/800)  
**Predecessor:** PLAN-23 Policies catalog/object AutomationProperties **COMPLETE**; product seed **W7-198**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Onboarding/Deployment residual action buttons have Content text but no `AutomationProperties.Name` (Policies a11y rows already locked in PLAN-16…23).

## Principles

1. Primary Onboarding/Deployment actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Replacing PLAN-16…23 Policies / shell / Incident locks  
- New Onboarding/Deployment RPCs  

## Ranked Desktop Onboarding/Deployment a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-OPS-01** | Onboarding/Deployment primary actions lack AutomationProperties.Name | `MainWindow.axaml` Operations panels | seed after PLAN-24 inventory |
| 2 | **DESK-A11Y-OPS-02** | Regression lock: ops Names + Policies/shell/Incident Names matrix | Living Spec matrix | seed after DESK-A11Y-OPS-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-198 (#799)** — Seed next product tranche after PLAN-23 → PLAN-24 (this inventory is W7-199 OPEN).
