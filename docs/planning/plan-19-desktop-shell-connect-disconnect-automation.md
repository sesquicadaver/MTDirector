# PLAN-19 — Desktop shell Connect/Disconnect AutomationProperties Living Spec product tranche

**Date:** 2026-09-09  
**Status:** Inventory **DONE** (W7-174); **DESK-A11Y-CONN-01 DONE** (W7-175); seed **W7-176 DONE**; **DESK-A11Y-CONN-02 DONE** (W7-177); **PLAN-19 COMPLETE**  
**PLAN issue / queue:** [W7-174 / PLAN-19 #750](https://github.com/sesquicadaver/MTDirector/issues/750)  
**Predecessor:** PLAN-18 Incident ingest-action AutomationProperties **COMPLETE**; product seed **W7-173 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Shell chrome Connect/Disconnect buttons expose `AutomationProperties.Name` matching Content (DESK-A11Y-CONN-01/02). Incident actions remain locked by PLAN-16…18.

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
| Connect button | Content + AutomationProperties.Name="Connect" | **DESK-A11Y-CONN-01 DONE** |
| Disconnect button | Content + AutomationProperties.Name="Disconnect" | **DESK-A11Y-CONN-01 DONE** |
| Regression lock | Connect/Disconnect Names + Incident action Names matrix | **DESK-A11Y-CONN-02 DONE** |

## Ranked Desktop shell connection-action a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-CONN-01** | Connect/Disconnect buttons lack AutomationProperties.Name | `MainWindow.axaml` shell chrome | **W7-175 DONE** (#753); seeded by inventory **W7-174 DONE** (#750) |
| 2 | **DESK-A11Y-CONN-02** | Regression lock: Connect/Disconnect Names + Incident action Names matrix | Living Spec matrix | **W7-177 DONE** (#757); seeded by **W7-176 DONE** (#754); **PLAN-19 COMPLETE** |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**PLAN-19 COMPLETE.** Seeded PLAN-20 queue (W7-178/179). **W7-178 DONE.** **W7-179 DONE** (PLAN-20 inventory). **§3.C NEXT = W7-187 (#777)** — DESK-A11Y-POLICY-01 Policies lifecycle AutomationProperties.Name.
