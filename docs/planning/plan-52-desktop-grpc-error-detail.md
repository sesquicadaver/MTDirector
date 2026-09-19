# PLAN-52 — Desktop gRPC ErrorDetail operator mapping after unary deadlines

**Date:** 2026-09-19 (inventory **DONE** @ `7ee69220`)  
**Status:** **PLAN-52 COMPLETE** — Inventory **DONE** (W7-352); seed **W7-353 (#1112) DONE**; implement **W7-354 (#1114) DONE**; COMPLETE seed **W7-355 (#1115) DONE**; successor **PLAN-53** inventory **W7-356 (#1119) DONE**; seed **W7-357 (#1120) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-352 / PLAN-52 #1111](https://github.com/sesquicadaver/MTDirector/issues/1111) **DONE**  
**Predecessor:** PLAN-51 Desktop gRPC unary call deadline **COMPLETE** (DESK-GRPC-DEADLINE-01)  
**Normative files:** `GrpcApplicationErrorMapper`, Desktop ViewModels that catch `RpcException`, operator docs  
**Normative prior locks:** unary `Desktop:UnaryCallTimeoutSeconds` (30s, fail-closed); Health `CancelAfter`; Watch streams unbounded; MSGSIZE / BODY / KEEPALIVE / MINRATE; HTTP health; metrics; tracing; log correlation; OTel resource — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-51 shipped unary deadlines: Controller already emits structured `ErrorDetail` trailers (`mfc-error-detail-bin`: code, retryable, correlation_id, sanitized_detail). Desktop operator surfaces keep only `ex.Status.Detail`, so a failed unary call — including `DeadlineExceeded` — drops the correlation id and stable error code. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. Operators should see the structured fault the Controller already returns (code + correlation id), with a non-empty fallback when `Status.Detail` is empty.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open PLAN-51 deadline / PLAN-47…50 transport polish.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening DESK-GRPC-DEADLINE-01 / MSGSIZE / BODY / KEEPALIVE / MINRATE  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  

## Inventory evidence (W7-352 @ `main` `7ee69220`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `GrpcApplicationErrorMapper` | Writes `mfc-error-detail-bin` (`ErrorDetail`: code, retryable, correlation_id, sanitized_detail) | Structured fault exists |
| Desktop ViewModels | **14** `ErrorText = ex.Status.Detail` | Trailers ignored |
| `src/Mfc.Desktop` | **0** references to `mfc-error-detail` | Confirmed |
| Unary deadline | `DesktopGrpcUnaryCall` default 30s | Shipped; do not regress |

### ViewModel `ErrorText` sites (14)

| ViewModel | Assignments |
|-----------|-------------|
| `AddRouterWizardViewModel` | 1 |
| `AuditViewModel` | 1 |
| `DeploymentViewModel` | 1 |
| `DriftViewModel` | 2 |
| `IncidentViewModel` | 2 |
| `NodeDetailViewModel` | 2 |
| `OnboardingViewModel` | 1 |
| `PoliciesViewModel` | 1 |
| `RoutingAssuranceViewModel` | 1 |
| `SnapshotViewerViewModel` | 1 |
| `ZonesViewModel` | 1 |
| **Total** | **14** |

`ControllerConnectionService` sets connection state from `Status.Detail` on auth failure (2 sites). Those are not `ErrorText` and stay out of DESK-RPC-FAULT-01.

**Ranking decision:** Prefer **ONE atomic row** (**DESK-RPC-FAULT-01**):

- Shared helper used by ViewModels maps `mfc-error-detail-bin` into operator `ErrorText` (code + correlation id)
- Non-empty fallback when `Status.Detail` is empty (client `DeadlineExceeded` often has no trailer)
- Do **not** regress unary deadline / Watch unbounded streams / transport locks
- Living Spec + operator docs

Splitting per-ViewModel ranks would be vanity.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-RPC-FAULT-01** | Map `ErrorDetail` trailers into operator `ErrorText` (code, correlation id, non-empty fallback) + Living Spec | **14** ViewModel sites; **0** trailer reads @ `7ee69220` | after inventory **W7-352 DONE**; seed **W7-353 (#1112) DONE**; implement **W7-354 (#1114) DONE**; COMPLETE **W7-355 (#1115) DONE** |

Inventory (**W7-352 DONE**) confirmed sole rank. Seed **W7-353** advances NEXT to the FAULT implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-51 CLOSED)

PLAN-51 sole ranked row (**DESK-GRPC-DEADLINE-01**) is **DONE**. No further PLAN-51 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  

## §3.C ordering

1. **PLAN-51 COMPLETE** (W7-350 DESK-GRPC-DEADLINE-01; seed **W7-351 DONE**).  
2. **W7-352 DONE** — PLAN-52 inventory; opened **W7-354 (#1114)** DESK-RPC-FAULT-01 implement + **W7-355 (#1115)** COMPLETE follow-up.  
3. **W7-353 (#1112) DONE** — seed advanced NEXT to DESK-RPC-FAULT-01; keep COMPLETE **W7-355** open.  
4. **W7-354 (#1114) DONE** — DESK-RPC-FAULT-01 maps `mfc-error-detail-bin` into operator `ErrorText`.  
5. **W7-355 (#1115) DONE** — PLAN-52 COMPLETE → seed PLAN-53 inventory.

## Delivery notes (W7-354)

`DesktopRpcFaultText.Format` reads `mfc-error-detail-bin` and sets ViewModel `ErrorText` to `{code} (correlation {id}): {detail}` (`, retryable` when the trailer says so). Missing or unreadable trailers keep `Status.Detail`. Empty detail falls back to the status code (`DeadlineExceeded`). Unary `DesktopGrpcUnaryCall` deadlines are unchanged. Watch streams stay unbounded.

## §3.C NEXT

**§3.C NEXT = W7-376 (#1159)** — Seed first PLAN-53 atomic row after inventory → CTRL-ERRDETAIL-LOG-01.
