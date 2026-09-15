# PLAN-28 — Desktop residual field / control AutomationProperties tranche

**Date:** 2026-09-15  
**Status:** Inventory **OPEN** (W7-244); seeded by **W7-243 DONE** after **PLAN-27 COMPLETE**  
**PLAN issue / queue:** [W7-244 / PLAN-28 #895](https://github.com/sesquicadaver/MTDirector/issues/895)  
**Predecessor:** PLAN-27 Desktop Snapshot/Node/Drift/Audit button AutomationProperties residual **COMPLETE**; button-name waves PLAN-16…27  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb residual non-button Desktop `AutomationProperties.Name` gaps after all ~60 Buttons are named. Evidence baseline (2026-09-15 `MainWindow.axaml`): TextBox ~21 missing Name, ComboBox ~18, CheckBox ~3, TabItems, plus Policies draft / Zones / Snapshot-Diff selectors still without Names. Button Names from PLAN-16…27 remain locked — **do not regress**.

## Principles

1. Operator-facing fields and selectors expose AutomationProperties.Name matching label / intent.  
2. Living Spec locks Name without changing bindings or Commands.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-27 DESK-A11Y-SNAP/PANEL rows — that tranche is **COMPLETE**.

## Out of scope (do not seed)

- Re-opening PLAN-27 SNAP/PANEL product rows  
- New Snapshot / Drift / Audit / Policy RPCs  
- Replacing PLAN-16…27 Button Name locks  
- Naming every ListBox item host (inventory may defer list containers)

## Inventory evidence (seed baseline after PLAN-27)

| Surface | Named | Still missing Name (coarse) |
|---------|-------|-----------------------------|
| Desktop Buttons (~60) | yes (PLAN-16…27) | — |
| Zones TextBoxes | partial | residual TextBox Names |
| Snapshot / Semantic-diff ComboBoxes & CheckBoxes | partial | residual ComboBox / CheckBox Names |
| Policies draft / authoring fields | partial | residual TextBox / ComboBox Names |
| TabItems (panel chrome) | no | TabItem Names |
| Other fields | mixed | inventory refines |

## Ranked Desktop residual field/control a11y tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-FIELD-01** | Zones / Policies draft TextBox Names | Zones + Policies authoring TextBoxes missing Name | queued after inventory **W7-244**; seed **W7-245 (#896)** |
| 2 | **DESK-A11Y-CTRL-01** | Snapshot/Diff ComboBox & CheckBox + TabItem Names | Snapshot/Diff selectors, CheckBoxes, TabItems | after FIELD (inventory may refine / split / add regression)

Inventory (**W7-244**) may refine ranking, split surfaces, add a regression lock row, and open implement issues; seed **W7-245** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-27 CLOSED)

PLAN-27 ranks 1…2 (**DESK-A11Y-SNAP-01**, **DESK-A11Y-PANEL-01**) are **DONE**. All primary Desktop Buttons expose AutomationProperties.Name. No further PLAN-27 product rows.

## §3.C ordering

1. **PLAN-27 COMPLETE** (W7-242 DESK-A11Y-PANEL-01; seed **W7-243 DONE**).  
2. **W7-244 OPEN** — PLAN-28 inventory → open first field/control implement + follow-up seeds.  
3. **W7-245 OPEN** — seed first PLAN-28 implement after inventory.  
4. Execute ranked FIELD/CTRL rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-244 (#895)** — PLAN-28 Inventory Desktop residual field/control AutomationProperties tranche after PLAN-27.
