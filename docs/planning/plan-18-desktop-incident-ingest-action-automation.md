# PLAN-18 — Desktop Incident ingest-action AutomationProperties Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **DONE** (W7-169); **DESK-A11Y-INGEST-01 DONE** (W7-170); seed **W7-171 DONE**; **DESK-A11Y-INGEST-02 DONE** (W7-172); **PLAN-18 COMPLETE**  
**PLAN issue / queue:** [W7-169 / PLAN-18 #740](https://github.com/sesquicadaver/MTDirector/issues/740)  
**Predecessor:** PLAN-17 Incident bind-action AutomationProperties **COMPLETE**; product seed **W7-168 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Incident Operations "Ingest signal" button exposes `AutomationProperties.Name` matching Content (DESK-A11Y-INGEST-01/02). Bind assessment + field Names remain locked by PLAN-16/17.

## Principles

1. Primary Incident actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name on Ingest signal without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Full toolbar/chrome a11y pass  
- New Incident RPCs  
- Replacing PLAN-16/17 field / Bind Name locks  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Ingest signal lacks accessible name before regression matrix |
| Risk | XAML AutomationProperties only; keep Command binding; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-17 COMPLETE; inventory locks **DESK-A11Y-INGEST-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Ingest signal button | Content + AutomationProperties.Name="Ingest signal" | **DESK-A11Y-INGEST-01 DONE** |
| Regression lock | Ingest Name + Bind Name + Incident field Names matrix | **DESK-A11Y-INGEST-02 DONE** |

## Ranked Desktop Incident ingest-action a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-INGEST-01** | Ingest signal button lacks AutomationProperties.Name | `MainWindow.axaml` Operations → Incident | **W7-170 DONE** (#743); seeded by inventory **W7-169 DONE** (#740) |
| 2 | **DESK-A11Y-INGEST-02** | Regression lock: Ingest Name + Bind Name + Incident field Names matrix | Living Spec matrix | **W7-172 DONE** (#747); seeded by **W7-171 DONE** (#744); **PLAN-18 COMPLETE** |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**PLAN-18 COMPLETE.** PLAN-19 **COMPLETE**. Seeded PLAN-20 (W7-178/179). **§3.C NEXT = W7-184 (#770)** — Seed next product tranche after PLAN-19 → PLAN-20.
