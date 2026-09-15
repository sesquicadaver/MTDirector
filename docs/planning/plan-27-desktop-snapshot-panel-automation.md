# PLAN-27 — Desktop Snapshot / Node / Drift / Audit AutomationProperties residual tranche

**Date:** 2026-09-15  
**Status:** Inventory **OPEN** (W7-238); seeded by **W7-237 DONE** after **PLAN-26 COMPLETE**  
**PLAN issue / queue:** [W7-238 / PLAN-27 #883](https://github.com/sesquicadaver/MTDirector/issues/883)  
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

## Inventory evidence (deferred from PLAN-25)

| Surface | Named | Still missing Name |
|---------|-------|--------------------|
| Snapshots Capture / Reload / Compare / Copy | no | **DESK-A11Y-SNAP-01** |
| Node VRRP validate + Drift/Audit Refresh | no | **DESK-A11Y-PANEL-01** |

## Ranked Desktop Snapshot/Panel a11y tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-SNAP-01** | Snapshot Capture/Reload/Compare/Copy Names | Snapshots tabs | queued after inventory **W7-238**; seed **W7-239 (#884)** |
| 2 | **DESK-A11Y-PANEL-01** | Node VRRP validate + Drift/Audit Refresh Names | Node/Drift/Audit | after SNAP (inventory may refine seed/regression rows) |

Inventory (**W7-238**) may refine ranking, add a regression lock row, and open implement issues; seed **W7-239** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-26 CLOSED)

PLAN-26 ranks 1…14 (**AUDIT-RULE-01** … **AUDIT-INT-01**) are **DONE**. Disconnected DI/callers from audit `11cb746` were absorbed into AUDIT-DEP-* / AUDIT-INT-01 acceptance notes — not vanity deletes. No further PLAN-26 product rows.

## §3.C ordering

1. **PLAN-26 COMPLETE** (W7-236 AUDIT-INT-01; seed **W7-237 DONE**).  
2. **W7-238 OPEN** — PLAN-27 inventory → open DESK-A11Y-SNAP-01 + follow-up seeds.  
3. **W7-239 OPEN** — seed first PLAN-27 implement after inventory.  
4. Execute ranked SNAP/PANEL rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-238 (#883)** — PLAN-27 Inventory Desktop Snapshot/Node/Drift/Audit AutomationProperties residual tranche after PLAN-26.
