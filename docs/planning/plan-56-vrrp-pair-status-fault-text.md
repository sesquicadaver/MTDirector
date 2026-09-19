# PLAN-56 — VRRP pair status fault text after capture progress fault correlation

**Date:** 2026-09-19 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-368); seed **W7-369 (#1144) OPEN**; predecessor **PLAN-55 COMPLETE**  
**PLAN issue / queue:** [W7-368 / PLAN-56 #1143](https://github.com/sesquicadaver/MTDirector/issues/1143) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-55 Capture progress fault correlation **COMPLETE** (SNAP-FAULT-CORR-01)  
**Normative files:** `NodeDetailViewModel`, `DesktopRpcFaultText`, operator docs  
**Normative prior locks:** SNAP-FAULT-CORR-01; DESK-CONN-FAULT-01; CTRL-ERRDETAIL-LOG-01 (event 5301); DESK-RPC-FAULT-01; unary deadline; Watch streams unbounded; MSGSIZE / BODY / KEEPALIVE / MINRATE; HTTP health; metrics; tracing; log↔trace correlation — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-55 joined capture progress to the RPC fault correlation id: VRRP pair status still drops it. `NodeDetailViewModel.ValidateVrrpPairInternalAsync` sets `ErrorText` from `DesktopRpcFaultText.Format` on `RpcException`, then sets `VrrpPairStatusText` to the static sentence `VRRP pair consistency failed.`. The Watch loop writes `{member}: {stage}` and never reads `ErrorDetail`. Operators watching the pair status line cannot join a failed node capture to journald even though ErrorText already has the code and correlation id. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. The VRRP pair status line operators watch should show the same code and correlation id already on `ErrorText`.  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not re-open SNAP-FAULT-CORR-01, DESK-CONN-FAULT-01, CTRL-ERRDETAIL-LOG-01, DESK-RPC-FAULT-01, or PLAN-47…51 transport / deadline polish.  
5. Avoid vanity Desktop a11y and systemd Type=notify.

## Out of scope (do not seed)

- Re-opening SNAP-FAULT-CORR-01 / DESK-CONN-FAULT-01 / CTRL-ERRDETAIL-LOG-01 / DESK-RPC-FAULT-01 / DESK-GRPC-DEADLINE-01 / MSGSIZE / BODY / KEEPALIVE / MINRATE  
- Native MSI / AppImage (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- systemd `Type=notify` / `WatchdogSec`  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Changing the `mfc-error-detail-bin` trailer contract or the event 5301 log line  
- Onboarding / Deployment progress (`ErrorCode` only; proto has no `ErrorDetail`)  
- Health-timeout and TLS connection text  
- Generic capture `catch` rethrows and `Unwrap` (no pair-status line)

## Inventory evidence (seed baseline @ `f5560b4c`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `ValidateVrrpPairInternalAsync` `RpcException` | `ErrorText = DesktopRpcFaultText.Format(ex)` | Correlation id is on ErrorText |
| Same catch | `VrrpPairStatusText = "VRRP pair consistency failed."` | Status line drops code and correlation id |
| Same method `Exception` catch | Same static status sentence | Non-RPC failures also hide the message |
| Watch loop | `{memberName}: {progress.Stage}` | `ErrorDetail` (shared capture id after SNAP-FAULT-CORR-01) is not shown |
| `NodeDetailViewModel` | **0** `correlation` mentions | Pair status cannot be joined to journald |

## Ranked tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-VRRP-FAULT-01** | Show the RPC fault code and correlation id on the VRRP pair status line + Living Spec | **2** static `VRRP pair consistency failed.` assignments; **0** `correlation` mentions in `NodeDetailViewModel` @ `f5560b4c` | after inventory **W7-368**; seed **W7-369 (#1144)** |

Inventory (**W7-368**) may refine ranking and open the implement issue; seed **W7-369** advances NEXT to that implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-55 CLOSED)

PLAN-55 sole ranked row (**SNAP-FAULT-CORR-01**) is **DONE**. No further PLAN-55 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl / nested ListBox a11y — deferred vanity  
- Native MSI / AppImage — W7-22 lock  
- systemd Type=notify — deferred packaging polish  
- Onboarding / Deployment progress is an `ErrorCode` string, not an `ErrorDetail` — adding a correlation id would change the progress proto; smaller than the pair-status drop, which already has the id on `ErrorText`  
- Health-timeout and TLS connection text are not `ErrorDetail` trailers  
- Generic capture `catch` rethrows — no `ToRpcException`, so no event 5301  
- `Unwrap` mints its own id — no capture-progress publish

## §3.C ordering

1. **PLAN-55 COMPLETE** (W7-366 SNAP-FAULT-CORR-01; seed **W7-367 DONE**).  
2. **W7-368 OPEN** — PLAN-56 inventory (**§3.C NEXT**).  
3. **W7-369 OPEN** — seed first PLAN-56 implement after inventory.  
4. Execute ranked DESK-VRRP-FAULT-01 atomically.

## §3.C NEXT

**§3.C NEXT = W7-368 (#1143)** — PLAN-56 Inventory VRRP pair status fault text.
