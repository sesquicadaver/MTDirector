# PLAN-53 — Controller fault-correlation logging after Desktop ErrorDetail mapping

**Date:** 2026-09-19 (inventory **DONE** @ `dbbe0733`)  
**Status:** **PLAN-53 COMPLETE** — Inventory **DONE** (W7-356); seed **W7-357 (#1120) DONE**; implement **W7-358 (#1122) DONE**; COMPLETE seed **W7-359 (#1123) DONE**; successor **PLAN-54** inventory **W7-360 (#1127) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-356 / PLAN-53 #1119](https://github.com/sesquicadaver/MTDirector/issues/1119) **DONE**  
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
- Changing the `mfc-error-detail-bin` trailer contract consumed by `DesktopRpcFaultText`

## Inventory evidence (W7-356 @ `main` `dbbe0733`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `GrpcApplicationErrorMapper` | `CorrelationId = ProtoUuid.FromGuid(correlationId ?? Guid.NewGuid())` | Id minted when callers omit it |
| Mapper logging | **0** `ILogger` references | Id never logged |
| `ToRpcException` callers | **43** calls; **0** pass `correlationId` | Every fault id is new and unlogged |
| Desktop `ErrorText` | `DesktopRpcFaultText` shows `correlation {id}` | Operators cannot join journald |
| PLAN-45 JSON logs | `RedactingJsonConsoleLoggerProvider` emits Activity `traceId` / `spanId` | Different identifier; do not regress |

### `ToRpcException` call sites (43)

| File | Calls |
|------|------:|
| `DeploymentGrpcService` | 10 |
| `OnboardingGrpcService` | 9 |
| `SnapshotGrpcService` | 7 |
| `IncidentGrpcService` | 6 |
| `PolicyGrpcService` | 3 |
| `GrpcRequestActorResolver` | 3 |
| `AuditGrpcService` | 1 |
| `DriftGrpcService` | 1 |
| `InventoryGrpcService` | 1 |
| `RoutingAssuranceGrpcService` | 1 |
| `ZoneGrpcService` | 1 |
| **Total** | **43** |

The mapper is a `static` class. Existing Controller logging is constructor-injected `ILogger<T>` plus `RedactingJsonConsoleLoggerProvider` (JSON console / journald). No second logging stack.

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-ERRDETAIL-LOG-01**):

- One structured log at the mapper: fault **code**, gRPC **status**, **correlation id**, **retryable**
- Use the existing logger (`ILogger` injection into the mapper, or the same `ILogger<T>` / `LoggerMessage` pattern). Do not invent a new logging stack
- Do **not** change the `ErrorDetail` trailer contract (`mfc-error-detail-bin`) consumed by `DesktopRpcFaultText`
- Do **not** regress DESK-RPC-FAULT-01, unary deadlines, MSGSIZE / BODY / KEEPALIVE / MINRATE, health, metrics, or tracing
- Living Spec + operator docs

Splitting per-service ranks would be vanity. Passing a caller-supplied correlation id is unnecessary while every caller omits it — logging the id the mapper already mints joins journald to Desktop `ErrorText`.

## Ranked tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-ERRDETAIL-LOG-01** | Structured log of fault code, gRPC status, correlation id, and retryable at the mapper + Living Spec | **43** callers omit `correlationId`; **0** `ILogger` @ `dbbe0733` | after inventory **W7-356 DONE**; seed **W7-357 (#1120) DONE**; implement **W7-358 (#1122) DONE**; COMPLETE **W7-359 (#1123) OPEN** (**§3.C NEXT**) |

Inventory (**W7-356 DONE**) confirmed sole rank. Seed **W7-357** advances NEXT to the LOG implement after inventory DONE.

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
2. **W7-356 DONE** — PLAN-53 inventory; opened **W7-358 (#1122)** CTRL-ERRDETAIL-LOG-01 implement + **W7-359 (#1123)** COMPLETE follow-up.  
3. **W7-357 (#1120) DONE** — seed advanced NEXT to CTRL-ERRDETAIL-LOG-01; keep COMPLETE **W7-359** open.  
4. **W7-358 (#1122) DONE** — CTRL-ERRDETAIL-LOG-01 logs code, status, correlation id, retryable on the existing JSON logger.  
5. **W7-359 (#1123) DONE** — PLAN-53 COMPLETE → seed PLAN-54 inventory.

## Delivery notes (W7-358)

`GrpcApplicationErrorMapper` takes `ILogger<GrpcApplicationErrorMapper>` and `Program.BuildHost` binds it for the existing static `ToRpcException` call sites. Each fault emits LoggerMessage event **5301** (Warning) on `RedactingJsonConsoleLoggerProvider`:

`gRPC application fault code={Code} status={Status} correlation_id={CorrelationId} retryable={Retryable}`

`correlation_id` is the same `Guid` (`D`) already written into `mfc-error-detail-bin` and shown by `DesktopRpcFaultText`. Activity `traceId`/`spanId` (PLAN-45) are unchanged and are a different identifier. The trailer contract is unchanged.

## §3.C NEXT

**§3.C NEXT = W7-374 (#1154)** — PLAN-54 Inventory Desktop connection-status fault text.
