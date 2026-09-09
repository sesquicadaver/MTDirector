# PLAN-19 — Desktop shell Connect/Disconnect AutomationProperties Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **DONE** (W7-174); seeded by **W7-173 DONE**; first implement **DESK-A11Y-CONN-01 OPEN** (W7-175)  
**PLAN issue / queue:** [W7-174 / PLAN-19 #750](https://github.com/sesquicadaver/MTDirector/issues/750)  
**Predecessor:** PLAN-18 Incident ingest-action AutomationProperties **COMPLETE**; product seed **W7-173 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Shell chrome Connect/Disconnect buttons have Content text but no `AutomationProperties.Name` (Incident actions already locked in PLAN-16…18).

## Principles

1. Primary shell connection actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name on Connect/Disconnect without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Full toolbar/chrome a11y pass beyond Connect/Disconnect  
- New connection RPCs  
- Replacing PLAN-16…18 Incident locks  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Connect/Disconnect lack accessible names before regression matrix |
| Risk | XAML AutomationProperties only; keep Command bindings; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-18 COMPLETE; inventory locks **DESK-A11Y-CONN-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Connect button | Content="Connect"; no AutomationProperties.Name | DESK-A11Y-CONN-01 |
| Disconnect button | Content="Disconnect"; no AutomationProperties.Name | DESK-A11Y-CONN-01 |
| Regression lock | Connect/Disconnect Names + Incident action Names matrix | DESK-A11Y-CONN-02 |

## Ranked Desktop shell connection-action a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-CONN-01** | Connect/Disconnect buttons lack AutomationProperties.Name | `MainWindow.axaml` shell chrome | **W7-175 OPEN** (#753); seeded by inventory **W7-174 DONE** (#750) |
| 2 | **DESK-A11Y-CONN-02** | Regression lock: Connect/Disconnect Names + Incident action Names matrix | Living Spec matrix | seeded by **W7-176 OPEN** (#754) after DESK-A11Y-CONN-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-175 (#753)** — DESK-A11Y-CONN-01 Connect/Disconnect AutomationProperties.Name Living Spec.
