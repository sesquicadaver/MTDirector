# PLAN-25 — Desktop Inventory / Zones / Add-router AutomationProperties Living Spec product tranche

**Date:** 2026-09-10 (inventory **DONE** 2026-09-11)  
**Status:** Inventory **DONE** (W7-204); **DESK-A11Y-INV-01 DONE** (W7-207); seed **W7-208 DONE**; next **W7-209 (#821)** DESK-A11Y-INV-02  
**PLAN issue / queue:** [W7-204 / PLAN-25 #810](https://github.com/sesquicadaver/MTDirector/issues/810) **DONE**  
**Predecessor:** PLAN-24 Onboarding/Deployment AutomationProperties **COMPLETE**; product seed **W7-203 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Inventory / Zones primary action buttons expose `AutomationProperties.Name` matching Content / operator intent (DESK-A11Y-INV-01). Add-router wizard Names from [#813](https://github.com/sesquicadaver/MTDirector/pull/813) remain locked (`Create / register`, `Add router Probe`, neighbor/site/node fields) — **do not regress**.

## Principles

1. Primary Inventory / Zones actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed in INV-01)

- Replacing PLAN-16…24 Policies / shell / Incident / Onboarding/Deployment locks  
- New Inventory / Zones RPCs  
- Snapshot / Drift / Audit / Node VRRP button Names (ranked below; seed after INV-02 or follow-on PLAN row)

## Inventory evidence (2026-09-11 `MainWindow.axaml`)

| Surface | Named | Still missing Name |
|---------|-------|--------------------|
| Shell Connect/Disconnect | yes | — |
| Add-router wizard | yes (#813) | — |
| Policies / Onboarding / Deployment / Incident | yes (PLAN-16…24) | — |
| Inventory tree / detail | Content + AutomationProperties.Name (`Refresh`, `Inventory Probe`) | — |
| Zones | Content + AutomationProperties.Name (`Resolve node`, `Resolve device`, `Create`, `Delete`, `Update zone`, `Upsert binding`, `Delete binding`, `Refresh`) | — |
| Snapshots | no | `Reload`, `Capture`, `Copy sanitized`, `Compare`, `Reload captures`, `Refresh` |
| Node / Drift / Audit | no | `Validate (last captures)`, `Capture all members + validate`, `Refresh` |

## Ranked Desktop Inventory/Zones a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-INV-01** | Inventory + Zones primary actions lack AutomationProperties.Name | `MainWindow.axaml` Inventory/Zones | **W7-207 DONE** (#817) |
| 2 | **DESK-A11Y-INV-02** | Regression: inv/zones Names + ops + Policies/shell/Incident Names matrix | Living Spec matrix | **W7-209 OPEN** (#821); seeded by **W7-208 DONE** (#818) |
| 3 | **DESK-A11Y-SNAP-01** | Snapshot Capture/Reload/Compare/Copy Names | Snapshots tabs | seed after INV-02 / PLAN-25 close |
| 4 | **DESK-A11Y-PANEL-01** | Node VRRP validate + Drift/Audit Refresh Names | Node/Drift/Audit | seed after SNAP-01 |

## Dual track

Product §3 never waits on GNS3.

## Successor

**PLAN-26** (code-audit remediation @ `11cb746`) remains queued: seed **W7-205 (#814)**, inventory **W7-206 (#815)** — after PLAN-25 COMPLETE (INV-02 + optional SNAP/PANEL or explicit close). See [`plan-26-code-audit-remediation-11cb746.md`](plan-26-code-audit-remediation-11cb746.md).

## §3.C NEXT

**§3.C NEXT = W7-209 (#821)** — DESK-A11Y-INV-02 Inventory/Zones Names + ops + Policies/shell/Incident Names regression Living Spec.
