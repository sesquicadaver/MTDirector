# PLAN-07 — Core MVP Desktop operator-surface Living Spec product tranche

**Date:** 2026-09-07  
**PLAN issue / queue:** [W7-78 / PLAN-07 #554](https://github.com/sesquicadaver/MTDirector/issues/554)  
**Predecessor:** PLAN-06 Incident Desktop COMPLETE (W7-73…W7-76); product seed W7-77  
**Status:** **COMPLETE** (DESK-POLICY…DESK-INVENTORY DONE)
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C

PLAN-05 closed dedicated Desktop Living Specs for Audit → Routing and deferred **Incident** to PLAN-06 (now **COMPLETE**). PLAN-05 also named Policies / Onboarding / Deploy / Snapshot as **MVP baseline** covered only by `DesktopMvpWorkflowsLivingSpecTests`, not host-aligned `Desktop*LivingSpecTests`. PLAN-07 keeps `/autopilot` from idling by inventoring **Core MVP Desktop operator-surface** rows: Policies → Deploy → Onboarding → Snapshot → Inventory against existing GrpcHost contracts (CT / Policy / Deployment / Onboarding / Snapshot / Inventory hosts).

## Out of scope (do not seed)

- Lab / CHR / physical CRS / `WriteEnabled` flip — ops-parallel, not §3 stop-gates  
- Anti-goals: local Desktop `SemanticDiffEngine`, auto-fix drift, fake VRRP roles  
- EndpointPresence / ResponseFeedback / Incident deploy-overlay Desktop UI (Application-only / scope lock)  
- Replacing existing MVP workflow Living Specs (Desktop **adds** host-aligned DESK-* coverage)

## Evidence baseline (host vs Desktop)

| Surface | Host / Application | Desktop today | Gap |
|---------|--------------------|---------------|-----|
| Audit / Drift / Zones / Routing | GrpcHost + `Desktop*LivingSpecTests` | PLAN-05 **DONE** | baseline |
| Incident Ingest/Bind | GrpcHost + Desktop Living Specs | PLAN-06 **DONE** | baseline |
| PolicyService | `PolicyGrpcHostTests` | `DesktopPoliciesLivingSpecTests` (DESK-POLICY-01) | PLAN-07 **DONE** row |
| DeploymentService | `DeploymentGrpcHostTests` / CT-DEPLOY-01 | `DesktopDeploymentLivingSpecTests` (DESK-DEPLOY-01) | PLAN-07 **DONE** row |
| OnboardingService | `OnboardingGrpcHostTests` | `DesktopOnboardingLivingSpecTests` (DESK-ONBOARD-01) | PLAN-07 **DONE** row |
| SnapshotService | `SnapshotGrpcHostTests` | `DesktopSnapshotLivingSpecTests` (DESK-SNAPSHOT-01) | PLAN-07 **DONE** row |
| InventoryService | `InventoryGrpcHostTests` | `DesktopInventoryLivingSpecTests` (DESK-INVENTORY-01) | PLAN-07 **DONE** row |

## Ranked Core MVP Desktop operator-surface tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-POLICY-01** | Policies panel lacks host-aligned Desktop Living Spec vs PolicyGrpcHost | `PoliciesViewModel`, `IPolicyServiceClient` / `GrpcPolicyServiceClient`; host: `PolicyGrpcHostTests` | **W7-79 DONE** (#556) |
| 2 | **DESK-DEPLOY-01** | Deployment operator path lacks dedicated Desktop Living Spec vs DeploymentGrpcHost | `DeploymentViewModel`, `GrpcDeploymentServiceClient`; host: `DeploymentGrpcHostTests` | **W7-81 DONE** (#560) |
| 3 | **DESK-ONBOARD-01** | Onboarding Start/Watch/Rollback lacks dedicated Desktop Living Spec vs OnboardingGrpcHost | `OnboardingViewModel`, `GrpcOnboardingServiceClient`; host: `OnboardingGrpcHostTests` | **W7-83 DONE** (#564) |
| 4 | **DESK-SNAPSHOT-01** | Snapshot capture/compare lacks dedicated Desktop Living Spec vs SnapshotGrpcHost | `SnapshotViewerViewModel` / `SnapshotDiffViewModel`; host: `SnapshotGrpcHostTests` | **W7-85 DONE** (#568) |
| 5 | **DESK-INVENTORY-01** | Inventory tree / Add router / Node lacks dedicated Desktop Living Spec vs InventoryGrpcHost | `InventoryTreeViewModel`, `AddRouterWizardViewModel`, `NodeDetailViewModel`; host: `InventoryGrpcHostTests` | **W7-87 DONE** (#572) |

## Dual track (unchanged)

Product §3.C never waits on lab. Physical CRS / live CHR / `WriteEnabled` stay ops-parallel ([`known-limitations.md`](../release/known-limitations.md)).

## Successor

After PLAN-07 **COMPLETE**, §3.C product seed **W7-88 DONE** advanced to **PLAN-08** inventory (**W7-89 DONE**); first atomic row **DESK-NODE-01** (**W7-90 OPEN**).

## §3.C NEXT

**§3.C NEXT = W7-201 (#804)** — DESK-CONN-01 Desktop Connect/Disconnect Living Spec depth (PLAN-09).
