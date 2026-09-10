# PLAN-24 — Desktop Onboarding/Deployment AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **DONE** (W7-199); **DESK-A11Y-OPS-01 DONE** (W7-200); seed **W7-201 DONE**; **DESK-A11Y-OPS-02 DONE** (W7-202); **PLAN-24 COMPLETE**  
**PLAN issue / queue:** [W7-199 / PLAN-24 #800](https://github.com/sesquicadaver/MTDirector/issues/800)  
**Predecessor:** PLAN-23 Policies catalog/object AutomationProperties **COMPLETE**; product seed **W7-198 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Onboarding/Deployment residual action buttons expose `AutomationProperties.Name` matching Content (DESK-A11Y-OPS-01/02). Policies/shell/Incident a11y rows remain locked by PLAN-16…23.

## Principles

1. Primary Onboarding/Deployment actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Replacing PLAN-16…23 Policies / shell / Incident locks  
- New Onboarding/Deployment RPCs  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Onboarding/Deployment primary actions lack accessible names after Policies catalog/object lock |
| Risk | XAML AutomationProperties only; keep Command bindings; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-23 COMPLETE; inventory locks **DESK-A11Y-OPS-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Onboarding Validate prerequisites / Create plan / Start / Rollback / Recovery status | Content + AutomationProperties.Name | **DESK-A11Y-OPS-01 DONE** |
| Deployment Create plan / Start / Rollback / Recovery status | Content + AutomationProperties.Name | **DESK-A11Y-OPS-01 DONE** |
| Regression lock | ops Names + Policies/shell/Incident Names matrix | **DESK-A11Y-OPS-02 DONE** |

## Ranked Desktop Onboarding/Deployment a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-OPS-01** | Onboarding/Deployment primary actions lack AutomationProperties.Name | `MainWindow.axaml` Operations panels | **W7-200 DONE** (#803); seeded by inventory **W7-199 DONE** (#800) |
| 2 | **DESK-A11Y-OPS-02** | Regression lock: ops Names + Policies/shell/Incident Names matrix | Living Spec matrix | **W7-202 DONE** (#807); seeded by **W7-201 DONE** (#804); **PLAN-24 COMPLETE** |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**PLAN-24 COMPLETE.** Seeded PLAN-25 queue (W7-203/204). **§3.C NEXT = W7-203 (#809)** — Seed next product tranche after PLAN-24 → PLAN-25.
