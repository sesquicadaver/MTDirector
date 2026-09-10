# PLAN-20 — Desktop Policies lifecycle-action AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **DONE** (W7-179); seeded by **W7-178 DONE**; **DESK-A11Y-POLICY-01 DONE** (W7-180); next seed **W7-181 OPEN**  
**PLAN issue / queue:** [W7-179 / PLAN-20 #760](https://github.com/sesquicadaver/MTDirector/issues/760)  
**Predecessor:** PLAN-19 shell Connect/Disconnect AutomationProperties **COMPLETE**; product seed **W7-178 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Policies lifecycle buttons (Validate / Submit for review / Approve / Bind / Deploy) have Content text but no `AutomationProperties.Name` (shell Connect/Disconnect + Incident actions already locked in PLAN-16…19).

## Principles

1. Primary Policies lifecycle actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name on lifecycle buttons without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Full Policies chrome a11y pass beyond lifecycle row  
- New Policies RPCs  
- Replacing PLAN-16…19 shell / Incident locks  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Policies lifecycle actions lack accessible names before regression matrix |
| Risk | XAML AutomationProperties only; keep Command bindings; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-19 COMPLETE; inventory locks **DESK-A11Y-POLICY-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Validate button | Content + AutomationProperties.Name="Validate" | **DESK-A11Y-POLICY-01 DONE** |
| Submit for review button | Content + AutomationProperties.Name="Submit for review" | **DESK-A11Y-POLICY-01 DONE** |
| Approve button | Content + AutomationProperties.Name="Approve" | **DESK-A11Y-POLICY-01 DONE** |
| Bind button | Content + AutomationProperties.Name="Bind" | **DESK-A11Y-POLICY-01 DONE** |
| Deploy button | Content + AutomationProperties.Name="Deploy" | **DESK-A11Y-POLICY-01 DONE** |
| Regression lock | Policies lifecycle Names + Connect/Disconnect + Incident action Names matrix | DESK-A11Y-POLICY-02 |

## Ranked Desktop Policies lifecycle-action a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-POLICY-01** | Policies Validate/Submit/Approve/Bind/Deploy lack AutomationProperties.Name | `MainWindow.axaml` Policies panel | **W7-180 DONE** (#763); seeded by inventory **W7-179 DONE** (#760) |
| 2 | **DESK-A11Y-POLICY-02** | Regression lock: Policies lifecycle Names + Connect/Disconnect + Incident action Names matrix | Living Spec matrix | seeded by **W7-181 OPEN** (#764) after DESK-A11Y-POLICY-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-181 (#764)** — Seed next PLAN-20 row after DESK-A11Y-POLICY-01 → DESK-A11Y-POLICY-02.
