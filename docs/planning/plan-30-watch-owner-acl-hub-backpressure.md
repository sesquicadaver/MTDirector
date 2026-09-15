# PLAN-30 — Watch operation-owner ACL / hub slow-subscriber backpressure

**Date:** 2026-09-15  
**Status:** Inventory **OPEN** (W7-256); seeded by **W7-255 DONE** after **PLAN-29 COMPLETE**  
**PLAN issue / queue:** [W7-256 / PLAN-30 #919](https://github.com/sesquicadaver/MTDirector/issues/919)  
**Predecessor:** PLAN-29 Desktop connection health / reconnect **COMPLETE**; AUDIT-INT-01 deferred Watch ownership / hub backpressure from audit `11cb746` §19  
**Normative files:** [`SnapshotGrpcService.cs`](../../src/Mfc.Controller/Grpc/SnapshotGrpcService.cs), [`DeploymentGrpcService.cs`](../../src/Mfc.Controller/Grpc/DeploymentGrpcService.cs), [`OnboardingGrpcService.cs`](../../src/Mfc.Controller/Grpc/OnboardingGrpcService.cs), [`CaptureProgressHub.cs`](../../src/Mfc.Controller/Grpc/CaptureProgressHub.cs), [`DeploymentProgressHub.cs`](../../src/Mfc.Controller/Grpc/DeploymentProgressHub.cs), [`OnboardingProgressHub.cs`](../../src/Mfc.Controller/Grpc/OnboardingProgressHub.cs)  
**Normative audit:** [`docs/audits/MTDirector-audit-11cb746-20260911.md`](../audits/MTDirector-audit-11cb746-20260911.md) §19  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the **product-critical** AUDIT §19 residuals left after AUDIT-INT-01: Watch RPCs require Read permissions but do **not** bind the caller to the operation owner; progress hubs prune after terminal + last reader but still fan out on **unbounded** channels while live. PLAN-29 HEALTH/RECONNECT Living Spec locks remain — **do not regress**. AUDIT-INT-01 Read gates + terminal prune remain — **do not regress**.

## Principles

1. Watch of an in-flight operation must be limited to the owning actor (or an explicit equivalent), not merely any operator with the Read permission and an operation ID.  
2. Hub publish must not grow without bound for slow subscribers (bounded channels / drop / disconnect policy).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-29 DESK-CONN-HEALTH/RECONNECT rows — that tranche is **COMPLETE**.

## Out of scope (do not seed)

- Re-opening PLAN-29 DESK-CONN-HEALTH/RECONNECT product rows  
- PLAN-28 deferred ListBox hosts / Drift–Audit read-only JSON TextBoxes (remain deferred a11y)  
- Replacing AUDIT-INT-01 Read permission / terminal-prune Living Spec locks  
- Re-opening PLAN-26 ranked AUDIT-RULE…AUDIT-INT product rows

## Inventory evidence (seed baseline after PLAN-29)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| WatchCapture / WatchDeployment / WatchOnboarding | `EnsureWatchAuthorizedAsync` + `SnapshotRead` / `DeploymentRead` / `OnboardingRead` | No operation-owner check; any allowlisted Read actor with an ID can Watch |
| Hub retention | Prune after terminal + last reader (`TryPrune`) | OK vs AUDIT-INT-01 |
| Subscriber channels | `Channel.CreateUnbounded` in Capture/Deployment/Onboarding hubs | Slow subscriber + unbounded `_history` while live |
| History buffer | `_history` list grows until prune | Unbounded replay while operation is retained |
| Publish | `TryWrite` to all subscribers | Dropped writes on unbounded channels are rare; memory, not backpressure, is the failure mode |

## Ranked Watch owner/backpressure tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **WATCH-OWN-01** | Bind Watch to operation owner (beyond Read permission) | `EnsureWatchAuthorizedAsync` in Snapshot/Deployment/Onboarding gRPC; audit §19 ownership | queued after inventory **W7-256**; seed **W7-257 (#920)** |
| 2 | **WATCH-BP-01** | Bounded hub subscriber channels / slow-subscriber backpressure | `CreateUnbounded` in three ProgressHubs; unbounded `_history` | after OWN (inventory may refine / split / add regression)

Inventory (**W7-256**) may refine ranking, split owner vs backpressure vs replay cap, add a regression lock row, and open implement issues; seed **W7-257** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-29 CLOSED)

PLAN-29 ranks 1…2 (**DESK-CONN-HEALTH-01**, **DESK-CONN-RECONNECT-01**) are **DONE**. No further PLAN-29 product rows.

## Adjacent residuals (not seeded here)

- PLAN-28 deferred ListBox hosts and Drift/Audit read-only JSON TextBoxes (intentional a11y residuals, not §3 stop-gates).  

## §3.C ordering

1. **PLAN-29 COMPLETE** (W7-254 DESK-CONN-RECONNECT-01; seed **W7-255 DONE**).  
2. **W7-256 OPEN** — PLAN-30 inventory → open first owner/backpressure implement + follow-up seeds.  
3. **W7-257 OPEN** — seed first PLAN-30 implement after inventory.  
4. Execute ranked WATCH-OWN / WATCH-BP rows atomically.

## §3.C NEXT

**§3.C NEXT = W7-256 (#919)** — PLAN-30 Inventory Watch operation-owner ACL / hub slow-subscriber backpressure (AUDIT §19 residual) after PLAN-29.
