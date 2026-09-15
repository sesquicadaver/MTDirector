# PLAN-27 — Desktop Snapshot / Node / Drift / Audit AutomationProperties residual tranche

**Date:** 2026-09-15 (inventory **DONE** 2026-09-15)  
**Status:** Inventory **DONE** (W7-238); seed **W7-239 (#884) DONE**; **DESK-A11Y-SNAP-01 W7-240 (#886) DONE**; seed **W7-241 (#887) DONE**; **DESK-A11Y-PANEL-01 W7-242 (#891) DONE**; **§3.C NEXT = W7-243 (#892)** → PLAN-27 COMPLETE seed  
**PLAN issue / queue:** [W7-238 / PLAN-27 #883](https://github.com/sesquicadaver/MTDirector/issues/883) **DONE**  
**Predecessor:** PLAN-26 code-audit remediation (`11cb746`) **COMPLETE**; deferred residuals from PLAN-25  
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
| 2 | **DESK-A11Y-PANEL-01** | Node VRRP validate + Drift/Audit Refresh Names | Node (`Refresh`, VRRP validate pair), RoutingAssurance `Refresh`, Drift/Audit `Refresh` | **W7-242 (#891) DONE**; seed **W7-241 (#887) DONE**; follow-up **W7-243 (#892)** (**§3.C NEXT**) → PLAN-27 COMPLETE |

Inventory (**W7-238 DONE**) locked ranking and opened SNAP implement + PANEL seed. Seed **W7-239 DONE** advanced §3.C NEXT to **W7-240**. **W7-240 DONE** + seed **W7-241 DONE** opened **W7-242** PANEL-01. **W7-242 DONE** advances §3.C NEXT to **W7-243** PLAN-27 COMPLETE seed.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-26 CLOSED)

PLAN-26 ranks 1…14 (**AUDIT-RULE-01** … **AUDIT-INT-01**) are **DONE**. Disconnected DI/callers from audit `11cb746` were absorbed into AUDIT-DEP-* / AUDIT-INT-01 acceptance notes — not vanity deletes. No further PLAN-26 product rows.

## §3.C ordering

1. **PLAN-26 COMPLETE** (W7-236 AUDIT-INT-01; seed **W7-237 DONE**).  
2. **W7-238 DONE** — PLAN-27 inventory; opened **W7-240** / **W7-241**.  
3. **W7-239 DONE** — seed advanced NEXT to **DESK-A11Y-SNAP-01** (**W7-240**).  
4. **W7-240 DONE** — DESK-A11Y-SNAP-01 Names locked (`DesktopSnapshotAutomationLivingSpecTests`).  
5. **W7-241 DONE** — seed opened **W7-242** PANEL-01 + **W7-243** PLAN-27 COMPLETE follow-up.  
6. **W7-242 DONE** — DESK-A11Y-PANEL-01 Names locked (`DesktopPanelAutomationLivingSpecTests`).
7. Execute **W7-243** PLAN-27 COMPLETE seed (no further PLAN-27 product rows unless continuous-queue seeds a successor tranche).

## §3.C NEXT

**§3.C NEXT = W7-243 (#892)** — Seed next after DESK-A11Y-PANEL-01 (PLAN-27 COMPLETE).
