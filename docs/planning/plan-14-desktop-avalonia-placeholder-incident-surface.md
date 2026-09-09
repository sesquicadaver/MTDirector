# PLAN-14 — Desktop Avalonia PlaceholderText / Incident surface Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **DONE** (W7-149); seeded by **W7-148 DONE**; first implement **DESK-PLACEHOLDER-01 OPEN** (W7-150)  
**PLAN issue / queue:** [W7-149 / PLAN-14 #700](https://github.com/sesquicadaver/MTDirector/issues/700)  
**Predecessor:** PLAN-13 Desktop layout density **COMPLETE** (DESK-LAYOUT-00…10); product seed **W7-148 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Avalonia build warns `AVLN5001`: `TextBox.Watermark` is obsolete — use `PlaceholderText`. Incident Operations tab still uses `Watermark=` on ingest/bind fields; most other Desktop fields already use `PlaceholderText`.

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
| Incident ingest/bind TextBoxes | `Watermark=` on Incident Operations fields (AVLN5001) | DESK-PLACEHOLDER-01 |
| Repo-wide Desktop XAML | Mostly `PlaceholderText`; Incident residual | DESK-PLACEHOLDER-02 |

## Ranked Desktop Avalonia / Incident surface tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-PLACEHOLDER-01** | Obsolete `Watermark=` on Incident TextBoxes | `MainWindow.axaml` Operations → Incident | **W7-150 OPEN** (#703); seeded by inventory **W7-149 DONE** (#700) |
| 2 | **DESK-PLACEHOLDER-02** | Repo-wide Desktop XAML Watermark residue scan Living Spec | all `src/Mfc.Desktop/**/*.axaml` | seeded by **W7-151 OPEN** (#704) after DESK-PLACEHOLDER-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-150 (#703)** — DESK-PLACEHOLDER-01 Incident TextBox Watermark→PlaceholderText Living Spec.
