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

- Snapshot / Drift / Audit / Node VRRP button Names (**DESK-A11Y-SNAP-01** / **DESK-A11Y-PANEL-01**) — deferred residuals absorbed by **PLAN-27** after PLAN-26 COMPLETE; not §3 stop-gates  
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
| Snapshots | no | **PLAN-27** **DESK-A11Y-SNAP-01** |
| Node / Drift / Audit | yes (W7-242) | — |

## Ranked Desktop Inventory/Zones a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-INV-01** | Inventory + Zones primary actions lack AutomationProperties.Name | `MainWindow.axaml` Inventory/Zones | **W7-207 DONE** (#817) |
| 2 | **DESK-A11Y-INV-02** | Regression: inv/zones Names + ops + Policies/shell/Incident Names matrix | Living Spec matrix | **W7-209 DONE** (#821); seeded by **W7-208 DONE** (#818); **PLAN-25 COMPLETE** |
| 3 | **DESK-A11Y-SNAP-01** | Snapshot Capture/Reload/Compare/Copy Names | Snapshots tabs | **PLAN-27** **W7-240 (#886) DONE** (inventory W7-238 DONE; seed W7-239 DONE) |
| 4 | **DESK-A11Y-PANEL-01** | Node VRRP validate + Drift/Audit Refresh Names | Node/Drift/Audit | **PLAN-27** **W7-242 (#891) DONE**; seed W7-241 DONE; NEXT **W7-243** |

## Dual track

Product §3 never waits on GNS3.

## Successor

**PLAN-26 COMPLETE** ([`plan-26-code-audit-remediation-11cb746.md`](plan-26-code-audit-remediation-11cb746.md)). Deferred SNAP/PANEL residuals continue in **PLAN-27**: [`plan-27-desktop-snapshot-panel-automation.md`](plan-27-desktop-snapshot-panel-automation.md) (inventory **W7-238 DONE**; seed **W7-239 DONE**; SNAP-01 **W7-240 DONE**; seed **W7-241 DONE**; PANEL-01 **W7-242 DONE**; seed **W7-243 DONE**; **PLAN-27 COMPLETE**; successor **PLAN-28** NEXT **W7-244**).

## §3.C NEXT

**PLAN-25 COMPLETE.** **PLAN-26 COMPLETE.** **PLAN-27 COMPLETE.** **PLAN-28 COMPLETE.** **PLAN-29 COMPLETE.** Successor **PLAN-30** inventory **DONE** (W7-256). **§3.C NEXT = W7-337 (#1080)** — DESK-A11Y-RO-01 seed.
