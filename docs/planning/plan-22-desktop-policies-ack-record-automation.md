# PLAN-22 — Desktop Policies acknowledge/record-analysis AutomationProperties Living Spec product tranche

**Date:** 2026-09-10  
**Status:** Inventory **DONE** (W7-189); seeded by **W7-188 DONE**; first implement **DESK-A11Y-POLICY-ACK-01 OPEN** (W7-190)  
**PLAN issue / queue:** [W7-189 / PLAN-22 #780](https://github.com/sesquicadaver/MTDirector/issues/780)  
**Predecessor:** PLAN-21 Policies authoring residual AutomationProperties **COMPLETE**; product seed **W7-188 DONE**  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Policies Record analysis / Acknowledge warning buttons have Content text but no `AutomationProperties.Name` (authoring residual + lifecycle rows already locked in PLAN-20/21).

## Principles

1. Residual Policies review-ack actions expose AutomationProperties.Name matching Content / operator intent.  
2. Living Spec locks Name without changing Command bindings.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.

## Out of scope (do not seed)

- Replacing PLAN-16…21 authoring / lifecycle / shell / Incident locks  
- New Policies RPCs  

## Decision drivers

| Driver | Choice |
|--------|--------|
| Worst pain first | Record analysis / Acknowledge warning lack accessible names after authoring residual lock |
| Risk | XAML AutomationProperties only; keep Command bindings; Desktop build + Living Spec |
| Queue fit | Seed **after** PLAN-21 COMPLETE; inventory locks **DESK-A11Y-POLICY-ACK-01** as first implement |

## Evidence baseline

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Record analysis | Content text; no AutomationProperties.Name | DESK-A11Y-POLICY-ACK-01 |
| Acknowledge warning | Content text; no AutomationProperties.Name | DESK-A11Y-POLICY-ACK-01 |
| Regression lock | ack/record Names + authoring residual + lifecycle + shell/Incident Names matrix | DESK-A11Y-POLICY-ACK-02 |

## Ranked Desktop Policies acknowledge/record-analysis a11y tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-POLICY-ACK-01** | Policies Record analysis / Acknowledge warning lack AutomationProperties.Name | `MainWindow.axaml` Policies panel | **W7-190 OPEN** (#783); seeded by inventory **W7-189 DONE** (#780) |
| 2 | **DESK-A11Y-POLICY-ACK-02** | Regression lock: ack/record Names + authoring residual + lifecycle + shell/Incident Names matrix | Living Spec matrix | seeded by **W7-191 OPEN** (#784) after DESK-A11Y-POLICY-ACK-01 |

## Dual track

Product §3 never waits on GNS3.

## §3.C NEXT

**§3.C NEXT = W7-190 (#783)** — DESK-A11Y-POLICY-ACK-01 Policies Record analysis / Acknowledge warning AutomationProperties.Name Living Spec.
