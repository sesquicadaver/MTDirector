# PLAN-28 — Desktop residual field / control AutomationProperties tranche

**Date:** 2026-09-15 (inventory **DONE** 2026-09-15; **COMPLETE** 2026-09-15)  
**Status:** **PLAN-28 COMPLETE** — Inventory **DONE** (W7-244); seed **W7-245 (#896) DONE**; **DESK-A11Y-FIELD-01 W7-246 (#898) DONE**; seed **W7-247 (#899) DONE**; **DESK-A11Y-CTRL-01 W7-248 (#903) DONE**; seed **W7-249 (#904) DONE**; successor **PLAN-29 COMPLETE**; successor **PLAN-30 COMPLETE**; successor **PLAN-31** inventory **W7-262 (#931) OPEN**; **§3.C NEXT = W7-397 (#1200)**  
**PLAN issue / queue:** [W7-244 / PLAN-28 #895](https://github.com/sesquicadaver/MTDirector/issues/895) **DONE**  
**Predecessor:** PLAN-27 Desktop Snapshot/Node/Drift/Audit button AutomationProperties residual **COMPLETE**; button-name waves PLAN-16…27  
**Successor:** [`plan-29-desktop-connection-health-reconnect.md`](plan-29-desktop-connection-health-reconnect.md) (Desktop connection health / reconnect after Controller stop; AUDIT §18 residual)  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb residual non-button Desktop `AutomationProperties.Name` gaps after all ~60 Buttons are named. Evidence baseline (2026-09-15 `MainWindow.axaml` @ `c3b3461`): TextBox **21** missing Name, ComboBox **18**, CheckBox **3**, TabItem **7** (ListBox hosts deferred). Button Names from PLAN-16…27 remain locked — **do not regress**.

## Principles

1. Operator-facing fields and selectors expose AutomationProperties.Name matching label / PlaceholderText / Content / Header intent.  
2. Living Spec locks Name without changing bindings or Commands.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-27 DESK-A11Y-SNAP/PANEL rows — that tranche is **COMPLETE**.

## Out of scope (do not seed)

- Re-opening PLAN-27 SNAP/PANEL product rows  
- New Snapshot / Drift / Audit / Policy RPCs  
- Replacing PLAN-16…27 Button Name locks  
- Naming every ListBox item host (deferred; inventory does not seed list containers)  
- Read-only Drift SemanticDiff / Audit Payload JSON TextBoxes (display surfaces; defer unless CTRL follow-up expands)

## Inventory evidence (2026-09-15 `MainWindow.axaml`)

| Surface | Named | Still missing Name (concrete) |
|---------|-------|-------------------------------|
| Desktop Buttons (~60) | yes (PLAN-16…27) | — |
| Add-router Inventory fields | yes (PLAN-25 / prior) | — |
| Incident assessment TextBoxes | yes (PLAN-16…18) | — |
| Zones TextBoxes | **yes** (W7-246 FIELD-01) | Named: `Zones.NewZoneKey` / `NewZoneName` / `NewZoneDescription`; `EditZoneName` / `EditZoneDescription`; `BindingValuesText` (placeholders `key`, `name`, `description (optional)`, `description (empty clears)`, `values (comma-separated)`) |
| Zones BindingKinds ComboBox | **yes** (W7-248 CTRL-01) | Named: `binding kind` (`Zones.BindingKinds`) |
| Policies draft / authoring TextBoxes | **yes** (W7-246 FIELD-01) | Named: `Policies.RevisionIdText`; draft name (`draft name (CompanyBaseline)` / `DraftNameText`); safety device + controller CIDR; rule description; address name + entries; service name + TCP port; contract disposition; compose node; baseline revision UUID; capability hash |
| Policies selector ComboBoxes | **yes** (W7-248 CTRL-01) | Named: `Families` / `Chains` / `Stages` / `Effects`; `address family`; `contract family` / `contract chain` / `reject mode`; `diff baseline catalog` |
| Snapshot CheckBox / Captures ComboBox | **yes** (W7-248 CTRL-01) | Named: `Technical`; `Captures` |
| Semantic diff ComboBoxes / CheckBoxes | **yes** (W7-248 CTRL-01) | Named: `Base capture` / `Target capture`; `Configuration only` / `Observations only` |
| Panel TabItems | **yes** (W7-248 CTRL-01) | Named: Snapshot, Configuration, Observations, Semantic diff, Onboarding, Deploy, Incident |
| Drift / Audit read-only TextBoxes | **no** (deferred) | Semantic diff text; Audit payload JSON |
| ListBox hosts | **no** (deferred) | Zones / Snapshot / Policies / Drift / Audit lists |

## Ranked Desktop residual field/control a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-FIELD-01** | Zones / Policies draft TextBox Names | Zones New/Edit/Binding values TextBoxes; Policies revision/draft/safety/rule/object/compose/compile TextBoxes | **W7-246 (#898) DONE**; seed **W7-245 (#896) DONE** |
| 2 | **DESK-A11Y-CTRL-01** | Snapshot/Diff ComboBox & CheckBox + TabItem (+ Zones/Policies selector ComboBoxes) | Snapshot `Technical` + Captures; Diff Base/Target + config/obs-only CheckBoxes; panel TabItem Headers; Zones BindingKinds; Policies Families/Chains/Stages/Effects/catalog selectors | seed **W7-247 (#899) DONE**; implement **W7-248 (#903) DONE** (`DesktopSnapshotDiffControlAutomationLivingSpecTests`); follow-up **W7-249 (#904) DONE** |

Inventory (**W7-244 DONE**) locked ranking and opened FIELD implement (**W7-246**) + CTRL seed (**W7-247**). Seed **W7-245 DONE** advanced §3.C NEXT to **W7-246**. **W7-246 DONE** advanced NEXT to **W7-247**. Seed **W7-247 DONE** advanced NEXT to **W7-248** and opened **W7-249**. **W7-248 DONE** advanced NEXT to **W7-249**. **W7-249 DONE** closed PLAN-28 and seeded **PLAN-29**.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-27 CLOSED)

PLAN-27 ranks 1…2 (**DESK-A11Y-SNAP-01**, **DESK-A11Y-PANEL-01**) are **DONE**. All primary Desktop Buttons expose AutomationProperties.Name. No further PLAN-27 product rows.

## Residual notes (COMPLETE)

- All ranked FIELD/CTRL remediations 1…2 closed on `main`.  
- Deferred ListBox hosts and Drift/Audit read-only JSON TextBoxes are seeded as **PLAN-31** (`plan-31-desktop-residual-listbox-readonly-a11y.md`).  
- No further PLAN-28 product rows — **PLAN-29 COMPLETE**; **PLAN-30 COMPLETE**; continuous queue advances to **PLAN-31** (Desktop residual ListBox / Drift–Audit read-only a11y).  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates.

## §3.C ordering

1. **PLAN-27 COMPLETE** (W7-242 DESK-A11Y-PANEL-01; seed **W7-243 DONE**).  
2. **W7-244 DONE** — PLAN-28 inventory; opened **W7-246** / **W7-247**.  
3. **W7-245 DONE** — seed advanced NEXT to **DESK-A11Y-FIELD-01** (**W7-246**).  
4. **W7-246 DONE** — DESK-A11Y-FIELD-01 Names locked (`DesktopZonesPoliciesFieldAutomationLivingSpecTests`).  
5. **W7-247 DONE** — seed advanced NEXT to **DESK-A11Y-CTRL-01** (**W7-248**); follow-up **W7-249** (PLAN-28 COMPLETE).  
6. **W7-248 DONE** — DESK-A11Y-CTRL-01 Names locked (`DesktopSnapshotDiffControlAutomationLivingSpecTests`); §3.C NEXT → **W7-249**.  
7. **W7-249 DONE** — PLAN-28 COMPLETE; seeded PLAN-29 (**W7-250** / **W7-251**).

## §3.C NEXT

**PLAN-28 COMPLETE.** Successor **PLAN-29 COMPLETE**; successor **PLAN-30 COMPLETE**; successor **PLAN-31** inventory **OPEN** (W7-262). **§3.C NEXT = W7-397 (#1200)** — PLAN-31 inventory.
