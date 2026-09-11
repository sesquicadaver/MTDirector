# PLAN-25 — Desktop Inventory / Zones / Add-router AutomationProperties Living Spec product tranche

**Date:** 2026-09-10 (inventory **DONE** 2026-09-11; **COMPLETE** 2026-09-11)  
**Status:** Inventory **DONE** (W7-204); **DESK-A11Y-INV-01 DONE** (W7-207); seed **W7-208 DONE**; **DESK-A11Y-INV-02 DONE** (W7-209); **PLAN-25 COMPLETE**  
**PLAN issue / queue:** [W7-204 / PLAN-25 #810](https://github.com/sesquicadaver/MTDirector/issues/810) **DONE**  
**Predecessor:** PLAN-24 Onboarding/Deployment AutomationProperties **COMPLETE**; product seed **W7-203 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Inventory / Zones primary action buttons expose `AutomationProperties.Name` matching Content / operator intent (DESK-A11Y-INV-01/02). Add-router wizard Names from [#813](https://github.com/sesquicadaver/MTDirector/pull/813) and Policies/shell/Incident / Onboarding/Deployment Names from PLAN-16…24 remain locked — **do not regress**.

## Principles

1. Primary Inventory / Zones actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (closed with PLAN-25; deferred residuals)

- Snapshot / Drift / Audit / Node VRRP button Names (**DESK-A11Y-SNAP-01** / **DESK-A11Y-PANEL-01**) — deferred residuals after PLAN-25 COMPLETE; not §3 stop-gates  
- New Inventory / Zones RPCs  
- Replacing PLAN-16…24 Policies / shell / Incident / Onboarding/Deployment locks  

## Inventory evidence (2026-09-11 `MainWindow.axaml`)

| Surface | Named | Still missing Name |
|---------|-------|--------------------|
| Shell Connect/Disconnect | yes | — |
| Add-router wizard | yes (#813) | — |
| Policies / Onboarding / Deployment / Incident | yes (PLAN-16…24) | — |
| Inventory tree / detail | Content + AutomationProperties.Name (`Refresh`, `Inventory Probe`) | — |
| Zones | Content + AutomationProperties.Name (`Resolve node`, `Resolve device`, `Create`, `Delete`, `Update zone`, `Upsert binding`, `Delete binding`, `Refresh`) | — |
| Snapshots | no | deferred **DESK-A11Y-SNAP-01** |
| Node / Drift / Audit | no | deferred **DESK-A11Y-PANEL-01** |

## Ranked Desktop Inventory/Zones a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-INV-01** | Inventory + Zones primary actions lack AutomationProperties.Name | `MainWindow.axaml` Inventory/Zones | **W7-207 DONE** (#817) |
| 2 | **DESK-A11Y-INV-02** | Regression: inv/zones Names + ops + Policies/shell/Incident Names matrix | Living Spec matrix | **W7-209 DONE** (#821); seeded by **W7-208 DONE** (#818); **PLAN-25 COMPLETE** |
| 3 | **DESK-A11Y-SNAP-01** | Snapshot Capture/Reload/Compare/Copy Names | Snapshots tabs | deferred residual after PLAN-25 COMPLETE |
| 4 | **DESK-A11Y-PANEL-01** | Node VRRP validate + Drift/Audit Refresh Names | Node/Drift/Audit | deferred residual after PLAN-25 COMPLETE |

## Dual track

Product §3 never waits on GNS3.

## Successor

**PLAN-26** (code-audit remediation @ `11cb746`) is next: seed **W7-205 (#814)**, inventory **W7-206 (#815)**. See [`plan-26-code-audit-remediation-11cb746.md`](plan-26-code-audit-remediation-11cb746.md).

## §3.C NEXT

**PLAN-25 COMPLETE.** **§3.C NEXT = W7-205 (#814)** — Seed next product tranche after PLAN-25 → PLAN-26.
