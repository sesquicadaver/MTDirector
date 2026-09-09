# PLAN-15 — Desktop Incident mfc-field style hygiene Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **OPEN** (W7-154); seeded by **W7-153 OPEN** after **PLAN-14 COMPLETE**  
**PLAN issue / queue:** [W7-154 / PLAN-15 #710](https://github.com/sesquicadaver/MTDirector/issues/710)  
**Predecessor:** PLAN-14 Avalonia PlaceholderText / Incident surface **COMPLETE**; product seed **W7-153**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Incident Operations TextBoxes use `PlaceholderText` but lack `Classes="mfc-field"` applied elsewhere on Desktop forms (Add router, Zones, Policies).

## Principles

1. Incident fields share the same visual/input chrome tokens as other Desktop forms.  
2. Living Spec locks `mfc-field` on Incident TextBoxes.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- New Incident RPCs / bind semantics  
- Global Fluent theme rewrite  
- Replacing PLAN-14 PlaceholderText locks  

## Ranked Desktop Incident field-style tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-FIELD-01** | Incident TextBoxes missing `Classes="mfc-field"` | `MainWindow.axaml` Operations → Incident | seed after PLAN-15 inventory |
| 2 | **DESK-FIELD-02** | Regression lock: Incident fields retain PlaceholderText + mfc-field | Living Spec matrix | seed after DESK-FIELD-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-153 (#709)** — Seed next product tranche after PLAN-14 → PLAN-15 (this inventory is W7-154 OPEN).
