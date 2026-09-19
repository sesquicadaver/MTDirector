# PLAN-53 — Controller fault-correlation logging after Desktop ErrorDetail mapping

**Date:** 2026-09-19 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-356); seed **W7-357 (#1120) OPEN**; predecessor **PLAN-52 COMPLETE**  
**PLAN issue / queue:** [W7-356 / PLAN-53 #1119](https://github.com/sesquicadaver/MTDirector/issues/1119) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-52 Desktop gRPC ErrorDetail operator mapping **COMPLETE** (DESK-RPC-FAULT-01)  
**Normative files:** `GrpcApplicationErrorMapper`, Controller JSON logging, operator docs  
**Normative prior locks:** DESK-RPC-FAULT-01 (`DesktopRpcFaultText`); unary deadline; Watch streams unbounded; MSGSIZE / BODY / KEEPALIVE / MINRATE; HTTP health; metrics; tracing; log↔trace correlation; OTel resource — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-52 showed Controller `ErrorDetail` on the operator screen: the correlation id operators can now copy is minted inside `GrpcApplicationErrorMapper` and is **not** written to Controller logs. Journald cannot be joined to `ErrorText`. Activity trace ids (PLAN-45) are a different identifier. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. The correlation id already shown in Desktop `ErrorText` should be greppable in Controller logs (code, status, correlation id, retryable) without a new logging stack.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open DESK-RPC-FAULT-01 or PLAN-47…51 transport / deadline polish.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening DESK-RPC-FAULT-01 / DESK-GRPC-DEADLINE-01 / MSGSIZE / BODY / KEEPALIVE / MINRATE  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  

## Inventory evidence (seed baseline @ `59488058`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `GrpcApplicationErrorMapper` | `CorrelationId = ProtoUuid.FromGuid(correlationId ?? Guid.NewGuid())` | Id minted |
| Mapper logging | **0** `ILogger` references | Id never logged |
| `ToRpcException` callers | **43** calls; **0** pass `correlationId` | Every fault id is new and unlogged |
| Desktop `ErrorText` | `DesktopRpcFaultText` shows that id | Operators cannot join journald |
| PLAN-45 JSON logs | Activity `TraceId` / `SpanId` | Different identifier; do not regress |

## Ranked tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-ERRDETAIL-LOG-01** | Structured log of fault code, gRPC status, correlation id, and retryable at the mapper + Living Spec | mapper has no logger; 43 callers omit correlation id @ `59488058` | after inventory **W7-356**; seed **W7-357 (#1120)** |

Inventory (**W7-356**) may refine ranking and open the implement issue; seed **W7-357** advances NEXT to that implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-52 CLOSED)

PLAN-52 sole ranked row (**DESK-RPC-FAULT-01**) is **DONE**. No further PLAN-52 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- `ControllerConnectionService` auth text still uses `Status.Detail` (not operator `ErrorText`) — smaller than the unlogged server id  

## §3.C ordering

1. **PLAN-52 COMPLETE** (W7-354 DESK-RPC-FAULT-01; seed **W7-355 DONE**).  
2. **W7-356 OPEN** — PLAN-53 inventory (**§3.C NEXT**).  
3. **W7-357 OPEN** — seed first PLAN-53 implement after inventory.  
4. Execute ranked CTRL-ERRDETAIL-LOG-01 atomically.

## §3.C NEXT

**§3.C NEXT = W7-356 (#1119)** — PLAN-53 Inventory Controller fault-correlation logging.
