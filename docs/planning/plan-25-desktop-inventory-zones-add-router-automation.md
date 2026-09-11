# PLAN-25 — Desktop Inventory / Zones / Add-router AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **OPEN** (W7-204); seeded by **W7-203 DONE** after **PLAN-24 COMPLETE**  
**PLAN issue / queue:** [W7-204 / PLAN-25 #810](https://github.com/sesquicadaver/MTDirector/issues/810)  
**Predecessor:** PLAN-24 Onboarding/Deployment AutomationProperties **COMPLETE**; product seed **W7-203 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Inventory / Add-router / Zones / Snapshot / Drift / Audit residual action buttons have Content text but no `AutomationProperties.Name` (Onboarding/Deployment + Policies a11y rows already locked in PLAN-16…24).

## Principles

1. Primary Inventory / Zones / Add-router actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Replacing PLAN-16…24 Policies / shell / Incident / Onboarding/Deployment locks  
- New Inventory / Zones RPCs  

## Ranked Desktop Inventory/Zones/Add-router a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-INV-01** | Inventory / Add-router / Zones primary actions lack AutomationProperties.Name | `MainWindow.axaml` Inventory/Zones panels | seed after PLAN-25 inventory |
| 2 | **DESK-A11Y-INV-02** | Regression lock: inv/zones Names + ops + Policies/shell/Incident Names matrix | Living Spec matrix | seed after DESK-A11Y-INV-01 |

## Dual track

Product §3 never waits on GNS3.

## Successor

**PLAN-26** (code-audit remediation @ `11cb746`) is queued: seed **W7-205 (#814)**, inventory **W7-206 (#815)** — see [`plan-26-code-audit-remediation-11cb746.md`](plan-26-code-audit-remediation-11cb746.md).

## §3.C NEXT

**§3.C NEXT = W7-204 (#810)** — PLAN-25 Inventory next Desktop Living Spec product tranche after PLAN-24.
