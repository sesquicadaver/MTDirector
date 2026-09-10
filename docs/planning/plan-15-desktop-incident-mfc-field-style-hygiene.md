# PLAN-15 — Desktop Incident mfc-field style hygiene Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **DONE** (W7-154); **DESK-FIELD-01 DONE** (W7-155); seed **W7-156 DONE**; **DESK-FIELD-02 DONE** (W7-157); **PLAN-15 COMPLETE**  
**PLAN issue / queue:** [W7-154 / PLAN-15 #710](https://github.com/sesquicadaver/MTDirector/issues/710)  
**Predecessor:** PLAN-14 Avalonia PlaceholderText / Incident surface **COMPLETE**; product seed **W7-153 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Incident Operations TextBoxes use `PlaceholderText` and `Classes="mfc-field"` aligned with other Desktop forms.

## Principles

1. Incident fields share the same visual/input chrome tokens as other Desktop forms.  
2. Living Spec locks `mfc-field` on Incident TextBoxes and regression matrix with PlaceholderText.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- New Incident RPCs / bind semantics  
- Global Fluent theme rewrite  
- Replacing PLAN-14 PlaceholderText locks  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Incident Operations missing `mfc-field` before regression matrix |
| Risk | XAML Classes only; keep bindings/PlaceholderText; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-14 COMPLETE; inventory locks **DESK-FIELD-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Incident ingest/bind TextBoxes | `PlaceholderText=` + `Classes="mfc-field"` | **DESK-FIELD-01 DONE** |
| Regression lock | PlaceholderText + mfc-field matrix | **DESK-FIELD-02 DONE** |

## Ranked Desktop Incident field-style tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-FIELD-01** | Incident TextBoxes missing `Classes="mfc-field"` | `MainWindow.axaml` Operations → Incident | **W7-155 DONE** (#713); seeded by inventory **W7-154 DONE** (#710) |
| 2 | **DESK-FIELD-02** | Regression lock: Incident fields retain PlaceholderText + mfc-field | Living Spec matrix | **W7-157 DONE** (#717); seeded by **W7-156 DONE** (#714); **PLAN-15 COMPLETE** |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**PLAN-15 COMPLETE.** PLAN-16 inventory **DONE** (W7-159). **PLAN-15 COMPLETE.** PLAN-16 **COMPLETE**. **§3.C NEXT = W7-185 (#773)** — Seed next product tranche after PLAN-16 → PLAN-17.
