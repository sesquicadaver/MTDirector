# PLAN-14 — Desktop Avalonia PlaceholderText / Incident surface Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **DONE** (W7-149); **DESK-PLACEHOLDER-01 DONE** (W7-150); seed **W7-151 DONE**; **DESK-PLACEHOLDER-02 DONE** (W7-152); **PLAN-14 COMPLETE**  
**PLAN issue / queue:** [W7-149 / PLAN-14 #700](https://github.com/sesquicadaver/MTDirector/issues/700)  
**Predecessor:** PLAN-13 Desktop layout density **COMPLETE** (DESK-LAYOUT-00…10); product seed **W7-148 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml), all `src/Mfc.Desktop/**/*.axaml`  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Avalonia build warns `AVLN5001`: `TextBox.Watermark` is obsolete — use `PlaceholderText`. Incident Operations tab previously used `Watermark=`; migrated and locked.

## Principles

1. Prefer current Avalonia APIs (`PlaceholderText`) over obsolete `Watermark`.  
2. Living Spec locks Incident field placeholders and absence of `Watermark=` in Desktop XAML.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Visual redesign / new Fluent theme  
- New Incident mutate/deploy RPCs beyond existing ingest/bind  
- Replacing completed PLAN-13 DESK-LAYOUT-* Living Specs  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Incident Operations `Watermark=` (AVLN5001) before repo-wide residue scan |
| Risk | XAML-only attribute rename; keep bindings; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-13 COMPLETE; inventory locks **DESK-PLACEHOLDER-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Incident ingest/bind TextBoxes | `PlaceholderText=` (no `Watermark=`) | **DESK-PLACEHOLDER-01 DONE** |
| Repo-wide Desktop XAML | No `Watermark=` under `src/Mfc.Desktop/**/*.axaml` | **DESK-PLACEHOLDER-02 DONE** |

## Ranked Desktop Avalonia / Incident surface tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-PLACEHOLDER-01** | Obsolete `Watermark=` on Incident TextBoxes | `MainWindow.axaml` Operations → Incident | **W7-150 DONE** (#703); seeded by inventory **W7-149 DONE** (#700) |
| 2 | **DESK-PLACEHOLDER-02** | Repo-wide Desktop XAML Watermark residue scan Living Spec | all `src/Mfc.Desktop/**/*.axaml` | **W7-152 DONE** (#707); seeded by **W7-151 DONE** (#704); **PLAN-14 COMPLETE** |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**PLAN-14 COMPLETE.** PLAN-15 **COMPLETE**. **§3.C NEXT = W7-195 (#793)** — Seed next product tranche after PLAN-15 → PLAN-16.
