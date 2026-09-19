# PLAN-52 — Desktop gRPC ErrorDetail operator mapping after unary deadlines

**Date:** 2026-09-19 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-352); seed **W7-353 (#1112) OPEN**; predecessor **PLAN-51 COMPLETE**  
**PLAN issue / queue:** [W7-352 / PLAN-52 #1111](https://github.com/sesquicadaver/MTDirector/issues/1111) **OPEN** (**§3.C NEXT**)  
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

## Inventory evidence (seed baseline @ `087d1a47`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `GrpcApplicationErrorMapper` | Writes `mfc-error-detail-bin` | Structured fault exists |
| Desktop ViewModels | **14** `ErrorText = ex.Status.Detail` | Trailers ignored |
| `src/Mfc.Desktop` | **0** references to `mfc-error-detail` | Confirmed |
| Unary deadline | `DesktopGrpcUnaryCall` default 30s | Shipped; do not regress |

## Ranked tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-RPC-FAULT-01** | Map `ErrorDetail` trailers into operator `ErrorText` (code, correlation id, non-empty fallback) + Living Spec | 14 ViewModel sites; 0 trailer reads @ `087d1a47` | after inventory **W7-352**; seed **W7-353 (#1112)** |

Inventory (**W7-352**) may refine ranking and open the implement issue; seed **W7-353** advances NEXT to that implement after inventory DONE.

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
2. **W7-352 OPEN** — PLAN-52 inventory (**§3.C NEXT**).  
3. **W7-353 OPEN** — seed first PLAN-52 implement after inventory.  
4. Execute ranked DESK-RPC-FAULT-01 atomically.

## §3.C NEXT

**§3.C NEXT = W7-352 (#1111)** — PLAN-52 Inventory Desktop gRPC ErrorDetail operator mapping.
