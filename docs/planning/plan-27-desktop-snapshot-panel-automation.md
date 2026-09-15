# PLAN-27 — Desktop Snapshot / Node / Drift / Audit AutomationProperties residual tranche

**Date:** 2026-09-15 (inventory **DONE** 2026-09-15; **COMPLETE** 2026-09-15)  
**Status:** **PLAN-27 COMPLETE** — Inventory **DONE** (W7-238); seed **W7-239 (#884) DONE**; **DESK-A11Y-SNAP-01 W7-240 (#886) DONE**; seed **W7-241 (#887) DONE**; **DESK-A11Y-PANEL-01 W7-242 (#891) DONE**; seed **W7-243 (#892) DONE**; successor **PLAN-28 COMPLETE**; **PLAN-29 COMPLETE**; successor **PLAN-30** inventory **W7-256 (#919) DONE**; **§3.C NEXT = W7-276 (#958)**
**PLAN issue / queue:** [W7-238 / PLAN-27 #883](https://github.com/sesquicadaver/MTDirector/issues/883) **DONE**  
**Predecessor:** PLAN-26 code-audit remediation (`11cb746`) **COMPLETE**; deferred residuals from PLAN-25  
**Successor:** [`plan-28-desktop-residual-field-control-automation.md`](plan-28-desktop-residual-field-control-automation.md) **COMPLETE**; next [`plan-29-desktop-connection-health-reconnect.md`](plan-29-desktop-connection-health-reconnect.md)  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb PLAN-25 deferred Snapshot / Node / Drift / Audit button `AutomationProperties.Name` gaps (**DESK-A11Y-SNAP-01**, **DESK-A11Y-PANEL-01**) into a ranked Living Spec tranche. Inventory / Zones / Policies / shell / Incident / Onboarding / Deployment Names from PLAN-16…26 remain locked — **do not regress**.

## Principles

1. Primary Snapshot / Node / Drift / Audit actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-26 AUDIT-* rows — that tranche is **COMPLETE**.

## Out of scope (do not seed)

- Re-opening PLAN-26 AUDIT-RULE…AUDIT-INT product rows  
- New Snapshot / Drift / Audit RPCs  
- Replacing PLAN-16…26 Policies / shell / Incident / Onboarding / Deployment / Inventory / Zones locks  

## Inventory evidence (2026-09-15 `MainWindow.axaml`)

| Surface | Named | Still missing Name |
|---------|-------|--------------------|
| Shell Connect/Disconnect | yes | — |
| Inventory / Zones / Add-router | yes (PLAN-25) | — |
| Policies / Onboarding / Deployment / Incident | yes (PLAN-16…24) | — |
| Snapshots — Snapshot tab | yes (W7-240) | — (`Reload`, `Capture`, `Copy sanitized`) |
| Snapshots — Semantic diff | yes (W7-240) | — (`Compare`, `Reload captures`) |
| Node | yes (W7-242) | — (`Refresh`, `Validate (last captures)`, `Capture all members + validate`) |
| Node — Routing assurance | yes (W7-242) | — (panel `Refresh`) |
| Drift | yes (W7-242) | — (`Refresh`) |
| Audit | yes (W7-242) | — (`Refresh`) |

## Ranked Desktop Snapshot/Panel a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-SNAP-01** | Snapshot Capture/Reload/Compare/Copy Names | Snapshots tabs (`Reload`, `Capture`, `Copy sanitized`, `Compare`, `Reload captures`) | **W7-240 (#886) DONE**; seed **W7-239 (#884) DONE** |
| 2 | **DESK-A11Y-PANEL-01** | Node VRRP validate + Drift/Audit Refresh Names | Node (`Refresh`, VRRP validate pair), RoutingAssurance `Refresh`, Drift/Audit `Refresh` | **W7-242 (#891) DONE**; seed **W7-241 (#887) DONE**; follow-up seed **W7-243 (#892) DONE** |

Inventory (**W7-238 DONE**) locked ranking and opened SNAP implement + PANEL seed. Seed **W7-239 DONE** advanced §3.C NEXT to **W7-240**. **W7-240 DONE** + seed **W7-241 DONE** opened **W7-242** PANEL-01. **W7-242 DONE** + seed **W7-243 DONE** closed PLAN-27 and seeded **PLAN-28**.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-26 CLOSED)

PLAN-26 ranks 1…14 (**AUDIT-RULE-01** … **AUDIT-INT-01**) are **DONE**. Disconnected DI/callers from audit `11cb746` were absorbed into AUDIT-DEP-* / AUDIT-INT-01 acceptance notes — not vanity deletes. No further PLAN-26 product rows.

## Residual notes (COMPLETE)

- All ranked SNAP/PANEL remediations 1…2 closed on `main`.  
- All ~60 Desktop Buttons expose AutomationProperties.Name.  
- No further PLAN-27 product rows — continuous queue advances to **PLAN-28** (residual field/control AutomationProperties).  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates.

## §3.C ordering

1. **PLAN-26 COMPLETE** (W7-236 AUDIT-INT-01; seed **W7-237 DONE**).  
2. **W7-238 DONE** — PLAN-27 inventory; opened **W7-240** / **W7-241**.  
3. **W7-239 DONE** — seed advanced NEXT to **DESK-A11Y-SNAP-01** (**W7-240**).  
4. **W7-240 DONE** — DESK-A11Y-SNAP-01 Names locked (`DesktopSnapshotAutomationLivingSpecTests`).  
5. **W7-241 DONE** — seed opened **W7-242** PANEL-01 + **W7-243** PLAN-27 COMPLETE follow-up.  
6. **W7-242 DONE** — DESK-A11Y-PANEL-01 Names locked (`DesktopPanelAutomationLivingSpecTests`).  
7. **W7-243 DONE** — PLAN-27 COMPLETE; seeded PLAN-28 (**W7-244** / **W7-245**).

## §3.C NEXT

**PLAN-27 COMPLETE.** Successor **PLAN-28 COMPLETE**; **PLAN-29 COMPLETE**; successor **PLAN-30** inventory **DONE** (W7-256). **§3.C NEXT = W7-276 (#958)** — WATCH-BP-01.
