# PLAN-31 — Desktop residual ListBox / Drift–Audit read-only a11y

**Date:** 2026-09-15  
**Status:** Inventory **OPEN** (W7-262); seeded by **W7-261 DONE** after **PLAN-30 COMPLETE**  
**PLAN issue / queue:** [W7-262 / PLAN-31 #931](https://github.com/sesquicadaver/MTDirector/issues/931)  
**Predecessor:** PLAN-30 Watch operation-owner ACL / hub backpressure **COMPLETE**; PLAN-28 deferred ListBox hosts / Drift–Audit read-only JSON TextBoxes  
**Normative files:** [`MainWindow.axaml`](../../src/Mfc.Desktop/MainWindow.axaml)  
**Normative prior tranche:** [`plan-28-desktop-residual-field-control-automation.md`](plan-28-desktop-residual-field-control-automation.md) (deferred ListBox / Drift–Audit JSON TextBoxes)  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest remaining **product** continuous-queue a11y gap after PLAN-30 closed Watch OWN+BP: PLAN-28 intentionally deferred **ListBox host** `AutomationProperties.Name` and **Drift/Audit read-only** JSON TextBox Names. FIELD/CTRL (PLAN-28), HEALTH/RECONNECT (PLAN-29), and OWN/BP (PLAN-30) Living Spec locks remain — **do not regress**.

## Principles

1. ListBox hosts used for operator selection/browse must expose stable `AutomationProperties.Name` (control-level; item templates may stay out of scope until inventory refines).  
2. Drift SemanticDiff and Audit PayloadJson read-only TextBoxes must be nameable to AT / UI Automation without inventing editable fields.  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-30 WATCH-OWN/BP rows — that tranche is **COMPLETE**.

## Out of scope (do not seed)

- Re-opening PLAN-30 WATCH-OWN / WATCH-BP product rows  
- Re-opening PLAN-28 DESK-A11Y-FIELD / DESK-A11Y-CTRL product rows  
- Replacing PLAN-29 HEALTH/RECONNECT Living Spec locks  
- Ops / CRS / physical lab residuals

## Inventory evidence (seed baseline after PLAN-30; `main` @ `3c2dea7`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| ListBox hosts in `MainWindow.axaml` | **43** hosts (`Modules`, Zones, Node, RoutingAssurance, Snapshot, Diff, Policies, Onboarding, Deployment, Drift, Audit) | **0** host-level `AutomationProperties.Name` |
| Drift read-only TextBox | `Text="{Binding Drift.SemanticDiffText}"` + `IsReadOnly="True"` | No `AutomationProperties.Name` |
| Audit read-only TextBox | `Text="{Binding Audit.SelectedEvent.PayloadJson}"` + `IsReadOnly="True"` | No `AutomationProperties.Name` |
| PLAN-28 FIELD/CTRL | TextBox / ComboBox / CheckBox / TabItem Names locked | Do not regress |

## Ranked Desktop residual ListBox / read-only a11y tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-A11Y-LIST-01** | ListBox host `AutomationProperties.Name` (operator browse/select surfaces) | 43 unnamed ListBox hosts in `MainWindow.axaml` | queued after inventory **W7-262**; seed **W7-263 (#932)** |
| 2 | **DESK-A11Y-RO-01** | Drift SemanticDiff + Audit PayloadJson read-only TextBox Names | 2 `IsReadOnly` TextBoxes without Name | after LIST (inventory may refine / split / add regression)

Inventory (**W7-262**) may refine ranking, split by panel, add a regression lock row, and open implement issues; seed **W7-263** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-30 CLOSED)

PLAN-30 ranks 1…2 (**WATCH-OWN-01**, **WATCH-BP-01**) are **DONE**. No further PLAN-30 product rows.

## Adjacent residuals (not seeded here)

- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates.

## §3.C ordering

1. **PLAN-30 COMPLETE** (W7-260 WATCH-BP-01; seed **W7-261 DONE**).  
2. **W7-262 OPEN** — PLAN-31 inventory → open first ListBox/read-only implement + follow-up seeds.  
3. **W7-263 OPEN** — seed first PLAN-31 implement after inventory.  
4. Execute ranked DESK-A11Y-LIST / DESK-A11Y-RO rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-262 (#931)** — PLAN-31 Inventory Desktop residual ListBox / Drift–Audit read-only a11y after PLAN-30.
