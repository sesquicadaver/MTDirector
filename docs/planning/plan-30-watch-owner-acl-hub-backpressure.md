# PLAN-30 — Watch operation-owner ACL / hub slow-subscriber backpressure

**Date:** 2026-09-15 (inventory **DONE** 2026-09-15)  
**Status:** **PLAN-30 COMPLETE** — Inventory **DONE** (W7-256); seed **W7-257 (#920) DONE**; implement **W7-258 (#922) DONE**; seed **W7-259 (#923) DONE**; implement **W7-260 (#927) DONE**; seed **W7-261 (#928) DONE**; successor **PLAN-31** inventory **W7-262 (#931) DONE**; seed **W7-263 (#932) DONE**; implement **W7-264 (#934) DONE**; seed **W7-265 (#935) DONE**; implement **W7-266 (#939) DONE**; COMPLETE seed **W7-267 (#940) DONE**; **PLAN-31 COMPLETE**; successor **PLAN-32** inventory **W7-268 (#943) DONE**; seed **W7-269 (#944) DONE**; implement **W7-270 (#946) DONE**; seed **W7-271 (#947) DONE**; implement **W7-272 (#951) DONE**; COMPLETE **W7-273 (#952) DONE**; **PLAN-32 COMPLETE**; successor **PLAN-33** inventory **W7-274 (#955) OPEN** (**§3.C NEXT**)
**PLAN issue / queue:** [W7-256 / PLAN-30 #919](https://github.com/sesquicadaver/MTDirector/issues/919) **DONE**  
**Predecessor:** PLAN-29 Desktop connection health / reconnect **COMPLETE**; AUDIT-INT-01 deferred Watch ownership / hub backpressure from audit `11cb746` §19  
**Successor:** [`plan-31-desktop-residual-listbox-readonly-a11y.md`](plan-31-desktop-residual-listbox-readonly-a11y.md) (Desktop residual ListBox / Drift–Audit read-only a11y; PLAN-28 deferred)  
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
- PLAN-28 deferred ListBox hosts / Drift–Audit read-only JSON TextBoxes (seeded as **PLAN-31**)  
- Replacing AUDIT-INT-01 Read permission / terminal-prune Living Spec locks  
- Re-opening PLAN-26 ranked AUDIT-RULE…AUDIT-INT product rows

## Inventory evidence (2026-09-15 `main` @ `892a073`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| WatchCapture | `EnsureWatchAuthorizedAsync` → `SnapshotRead` only (`SnapshotGrpcService` ~383–398); then `_progressHub.WatchAsync(operationId)` | No operation-owner check; any allowlisted Read actor with an ID can Watch |
| WatchDeployment / WatchOnboarding | Same pattern with `DeploymentRead` / `OnboardingRead` (`DeploymentGrpcService` ~242–256; `OnboardingGrpcService` ~230–243) | Same ownership gap across all three Watch RPCs |
| Hub owner retention | `CaptureProgressHub.Begin(deviceId)` stores device only; `DeploymentProgressHub.Ensure` / `OnboardingProgressHub.Ensure` take `operationId` only | No owner actor on `OperationStream` for Watch compare |
| Durable creator | `DeploymentOperation.CreatedBy` exists in domain/persistence | Watch path does **not** consult CreatedBy (or equivalent) |
| Hub retention | Prune after terminal + last reader (`TryPrune`) | OK vs AUDIT-INT-01 — do not regress |
| Subscriber channels | `Channel.CreateUnbounded` in Capture (~148), Deployment (~118), Onboarding (~118) hubs | Slow subscriber memory growth while live |
| History buffer | `_history.Add` on every `Publish` with no live cap | Unbounded replay buffer while operation retained |
| Publish | `TryWrite` to all subscribers | Dropped writes on unbounded channels are rare; memory, not backpressure, is the failure mode |

## Ranked Watch owner/backpressure tranche

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **WATCH-OWN-01** | Bind Watch to operation owner (beyond Read permission) | `EnsureWatchAuthorizedAsync` permission-only; hubs lack owner; audit §19 ownership | implement **W7-258 (#922) DONE**; seed **W7-257 (#920) DONE** |
| 2 | **WATCH-BP-01** | Bounded hub subscriber channels / slow-subscriber backpressure + live `_history` cap | was `CreateUnbounded` ×3; unbounded `_history` | implement **W7-260 (#927) DONE**; seed **W7-259 (#923) DONE**; follow-up **W7-261 (#928) DONE** |

Inventory (**W7-256 DONE**) locked ranking and opened OWN implement (**W7-258**) + BP seed (**W7-259**). Seed **W7-257 DONE** advanced §3.C NEXT to the first implement after inventory DONE. No third vanity rank — history bound stays inside **WATCH-BP-01**; AUDIT-INT-01 Read/prune locks remain the regression corpus. Seed IDs **WATCH-OWN-01** / **WATCH-BP-01** are the canonical atomic row names (owner ACL / hub backpressure).

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-29 CLOSED)

PLAN-29 ranks 1…2 (**DESK-CONN-HEALTH-01**, **DESK-CONN-RECONNECT-01**) are **DONE**. No further PLAN-29 product rows.

## Residual notes (COMPLETE)

- PLAN-30 ranks 1…2 (**WATCH-OWN-01**, **WATCH-BP-01**) are **DONE**. No further PLAN-30 product rows.  
- PLAN-28 deferred ListBox hosts and Drift/Audit read-only JSON TextBoxes are seeded as **PLAN-31**.  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates.

## §3.C ordering

1. **PLAN-29 COMPLETE** (W7-254 DESK-CONN-RECONNECT-01; seed **W7-255 DONE**).  
2. **W7-256 DONE** — PLAN-30 inventory; opened **W7-258** / **W7-259**.  
3. **W7-257 DONE** — seed advanced NEXT to **WATCH-OWN-01** (**W7-258**).  
4. **W7-258 DONE** — WATCH-OWN-01 owner ACL beyond Read.
5. **W7-259 DONE** — seed advanced NEXT to **WATCH-BP-01** (**W7-260**); opened **W7-261** PLAN-30 COMPLETE follow-up.
6. **W7-260 DONE** — WATCH-BP-01 bounded ProgressHub channels + live `_history` cap (capacity 64, disconnect-on-full, history 256).
7. **W7-261 DONE** — PLAN-30 COMPLETE; seeded PLAN-31 (**W7-262** / **W7-263**).

## §3.C NEXT

**PLAN-30 COMPLETE.** Successor **PLAN-31** inventory **DONE** (W7-262). **§3.C NEXT = W7-356 (#1119)** — Seed next PLAN-31 row after DESK-A11Y-LIST-01 → DESK-A11Y-RO-01.
