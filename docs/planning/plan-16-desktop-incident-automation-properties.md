# PLAN-16 — Desktop Incident AutomationProperties accessible-name Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **DONE** (W7-159); seeded by **W7-158 DONE**; first implement **DESK-A11Y-01 OPEN** (W7-160)  
**PLAN issue / queue:** [W7-159 / PLAN-16 #720](https://github.com/sesquicadaver/MTDirector/issues/720)  
**Predecessor:** PLAN-15 Incident mfc-field style hygiene **COMPLETE**; product seed **W7-158 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Desktop XAML currently has no `AutomationProperties.Name` usages. Incident Operations TextBoxes rely on adjacent `TextBlock` labels only — screen-reader / automation clients do not get an accessible name on the inputs.

## Principles

1. Interactive Incident fields expose `AutomationProperties.Name` matching their operator labels.  
2. Living Spec locks Name presence on Incident TextBoxes without changing bind semantics.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Full Desktop a11y audit of every control  
- New Incident RPCs  
- Replacing PLAN-14/15 PlaceholderText / mfc-field locks  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Incident Operations inputs lack accessible names before regression matrix |
| Risk | XAML AutomationProperties only; keep bindings/PlaceholderText/mfc-field; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-15 COMPLETE; inventory locks **DESK-A11Y-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Incident ingest/bind TextBoxes | Labels via adjacent TextBlock only; no AutomationProperties.Name | DESK-A11Y-01 |
| Regression lock | Names + PlaceholderText + mfc-field matrix | DESK-A11Y-02 |

## Ranked Desktop Incident a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-01** | Incident TextBoxes lack AutomationProperties.Name | `MainWindow.axaml` Operations → Incident | **W7-160 OPEN** (#723); seeded by inventory **W7-159 DONE** (#720) |
| 2 | **DESK-A11Y-02** | Regression lock: Incident Names + PlaceholderText + mfc-field | Living Spec matrix | seeded by **W7-161 OPEN** (#724) after DESK-A11Y-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-160 (#723)** — DESK-A11Y-01 Incident TextBox AutomationProperties.Name Living Spec.
