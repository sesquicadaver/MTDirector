# PLAN-10 — Desktop shell chrome & Policies authoring depth Living Spec product tranche

**Date:** 2026-09-08  
**PLAN issue / queue:** [W7-105 / PLAN-10 #608](https://github.com/sesquicadaver/MTDirector/issues/608)  
**Predecessor:** PLAN-09 Desktop connection-status COMPLETE (DESK-CONN…DESK-AUTH); product seed W7-104  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-05…09 closed host-aligned panels, secondary operator surfaces, and connection-status Living Specs. PLAN-10 keeps `/autopilot` from idling by inventoring **Desktop shell chrome and Policies authoring depth** rows: Shell module navigation / hotkeys / status chrome, Policies Diff execute path, and Policies Move up/down reorder that today exist in UI (and partial unit coverage) but lack dedicated `Desktop*LivingSpecTests` beyond MVP workflow / DESK-POLICY-01 presence checks.

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles  
- EndpointPresence / ResponseFeedback / Incident deploy-overlay Desktop UI (Application-only / scope lock)  
- Replacing PLAN-07/08/09 completed DESK-* Living Specs

## Evidence baseline (shell chrome & authoring depth)

| Surface | Desktop today | Gap |
|---------|---------------|-----|
| Shell modules / hotkeys / status | `DesktopShellLivingSpecTests` + `DesktopMvpWorkflowsLivingSpecTests` Ac12 + DESK-CONN status | **DESK-SHELL-01 DONE** |
| Policies Diff | `DesktopPoliciesDiffLivingSpecTests` + DESK-POLICY-01 presence + `PolicyDesktopServiceTests` | **DESK-DIFF-01 DONE** |
| Policies Move up/down reorder | W6-09 product; unit tests | PLAN-10 |

## Ranked Desktop shell chrome & Policies authoring depth tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-SHELL-01** | Shell SelectModule / HotKeys / chrome lacks dedicated Desktop Living Spec depth | `ShellViewModel` (`SelectModuleCommand`, `HotKeysText`, `Modules`, `StatusText`); `MainWindow.axaml` key bindings; `DesktopShellLivingSpecTests` | **W7-106 DONE** (#611) |
| 2 | **DESK-DIFF-01** | Policies Diff execute path lacks dedicated Desktop Living Spec depth | `PoliciesViewModel.DiffCommand`; `IPolicyPanelService.DiffAsync`; `DesktopPoliciesDiffLivingSpecTests` | **W7-108 DONE** (#614); seeded by **W7-107 DONE** (#612) |
| 3 | **DESK-REORDER-01** | Policies Move up/down reorder lacks dedicated Desktop Living Spec depth | `PoliciesViewModel.MoveRuleUpCommand` / `MoveRuleDownCommand`; `ReorderRulesInStageAsync` | seeded after DESK-DIFF-01 (**W7-109 OPEN** #616 → implement **W7-110** #617) |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**§3.C NEXT = W7-109 (#616)** — Seed next PLAN-10 row after DESK-DIFF-01 → DESK-REORDER-01.
