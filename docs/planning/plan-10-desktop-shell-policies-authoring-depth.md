# PLAN-10 — Desktop shell chrome & Policies authoring depth Living Spec product tranche

**Date:** 2026-09-08  
**PLAN issue / queue:** [W7-105 / PLAN-10 #608](https://github.com/sesquicadaver/MTDirector/issues/608)  
**Status:** **COMPLETE** (DESK-SHELL-01…DESK-REORDER-01)  
**Predecessor:** PLAN-09 Desktop connection-status COMPLETE (DESK-CONN…DESK-AUTH); product seed W7-104  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-05…09 closed host-aligned panels, secondary operator surfaces, and connection-status Living Specs. PLAN-10 inventoried **Desktop shell chrome and Policies authoring depth** rows and delivered dedicated `Desktop*LivingSpecTests` for Shell navigation/hotkeys, Policies Diff, and Policies Move up/down reorder.

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
| Policies Move up/down reorder | `DesktopPoliciesReorderLivingSpecTests` + W6-09 / `PoliciesViewModelTests` | **DESK-REORDER-01 DONE** |

## Ranked Desktop shell chrome & Policies authoring depth tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-SHELL-01** | Shell SelectModule / HotKeys / chrome lacks dedicated Desktop Living Spec depth | `ShellViewModel` (`SelectModuleCommand`, `HotKeysText`, `Modules`, `StatusText`); `MainWindow.axaml` key bindings; `DesktopShellLivingSpecTests` | **W7-106 DONE** (#611) |
| 2 | **DESK-DIFF-01** | Policies Diff execute path lacks dedicated Desktop Living Spec depth | `PoliciesViewModel.DiffCommand`; `IPolicyPanelService.DiffAsync`; `DesktopPoliciesDiffLivingSpecTests` | **W7-108 DONE** (#614); seeded by **W7-107 DONE** (#612) |
| 3 | **DESK-REORDER-01** | Policies Move up/down reorder lacks dedicated Desktop Living Spec depth | `PoliciesViewModel.MoveRuleUpCommand` / `MoveRuleDownCommand`; `ReorderRulesInStageAsync`; `DesktopPoliciesReorderLivingSpecTests` | **W7-110 DONE** (#617); seeded by **W7-109 DONE** (#616) |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## §3.C NEXT

**PLAN-10 COMPLETE.**

See PLAN-11: [`plan-11-desktop-policies-review-compose-lifecycle.md`](plan-11-desktop-policies-review-compose-lifecycle.md).

**§3.C NEXT = W7-116 (#630)** — DESK-COMPOSE-01 Desktop Policies Compose+RecordAnalysis Living Spec depth (PLAN-11).

**Successor:** PLAN-11 inventory **DONE** (W7-112); first atomic row **DESK-SUBMIT-01** (W7-114).
