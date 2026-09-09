# PLAN-14 — Desktop Avalonia PlaceholderText / Incident surface Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **OPEN** (W7-149); seeded by **W7-148 OPEN** after **PLAN-13 COMPLETE**  
**PLAN issue / queue:** [W7-149 / PLAN-14 #700](https://github.com/sesquicadaver/MTDirector/issues/700)  
**Predecessor:** PLAN-13 Desktop layout density **COMPLETE** (DESK-LAYOUT-00…10); product seed **W7-148**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Avalonia build warns `AVLN5001`: `TextBox.Watermark` is obsolete — use `PlaceholderText`. Incident Operations tab still uses `Watermark=` on ingest/bind fields.

## Principles

1. Prefer current Avalonia APIs (`PlaceholderText`) over obsolete `Watermark`.  
2. Living Spec locks Incident field placeholders and absence of `Watermark=` in Desktop XAML.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Visual redesign / new Fluent theme  
- New Incident mutate/deploy RPCs beyond existing ingest/bind  
- Replacing completed PLAN-13 DESK-LAYOUT-* Living Specs  

## Ranked Desktop Avalonia / Incident surface tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-PLACEHOLDER-01** | Obsolete `Watermark=` on Incident TextBoxes | `MainWindow.axaml` Operations → Incident | seed after PLAN-14 inventory |
| 2 | **DESK-PLACEHOLDER-02** | Repo-wide Desktop XAML Watermark residue scan Living Spec | all `src/Mfc.Desktop/**/*.axaml` | seed after DESK-PLACEHOLDER-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-148 (#699)** — Seed next product tranche after PLAN-13 → PLAN-14 (this inventory is W7-149 OPEN).
