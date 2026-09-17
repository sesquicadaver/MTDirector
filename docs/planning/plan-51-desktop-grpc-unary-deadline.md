# PLAN-51 — Desktop gRPC unary call deadline / timeout after transport saturates

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-348); seed **W7-349 (#1104) OPEN**; predecessor **PLAN-50 COMPLETE**  
**PLAN issue / queue:** [W7-348 / PLAN-51 #1103](https://github.com/sesquicadaver/MTDirector/issues/1103) **OPEN** (**§3.C NEXT** after W7-347)  
**Predecessor:** PLAN-50 Controller Kestrel min request/response data-rate **COMPLETE** (CTRL-KESTREL-MINRATE-01)  
**Normative files:** Desktop gRPC call sites (`src/Mfc.Desktop/Services/`, ViewModels / panel services), connection options, operator docs  
**Normative prior locks:** HTTP health; opt-in `/metrics`; opt-in tracing; log correlation; OTel resource identity; gRPC MaxReceive/SendMessageSize (256 MiB); Kestrel MaxRequestBodySize (256 MiB); HTTP/2 KeepAlivePing 60s/30s; Kestrel MinRequest/ResponseDataRate null; gRPC health; QG-SIGN-01/02; PLAN-32…50 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-50 shipped null Kestrel min data-rates: **transport (MSGSIZE + BODY + KEEPALIVE + MINRATE) is saturating**. Desktop unary gRPC calls generally lack `CallOptions.Deadline` / bounded timeouts — only Health uses `CancelAfter(HealthCheckTimeoutSeconds)`. A hung Controller can hang operator UI indefinitely. Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. Desktop unary RPCs should have a **minimal correct deadline/timeout policy** (fail-closed bounded wait; cancelable).  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-50 MINRATE / KEEPALIVE / MSGSIZE / BODY polish — those waves are **COMPLETE**.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer real operator reliability over systemd Type=notify vanity polish.

## Out of scope (do not seed)

- Re-opening PLAN-50 CTRL-KESTREL-MINRATE-01 / PLAN-49 KEEPALIVE / PLAN-48 BODY / PLAN-47 MSGSIZE  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full gRPC load-balancing / multi-instance HA productization  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-50 COMPLETE / MINRATE shipped)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Desktop Health | `CancelAfter(HealthCheckTimeoutSeconds)` | Bounded |
| Desktop unary RPCs | No widespread `CallOptions.Deadline` | Hung Controller hangs UI |
| Transport stack | MSGSIZE + BODY + KEEPALIVE + MINRATE | Saturating |
| Glob / rg | `Deadline` / `WithDeadline` absent under Desktop Services (except RosSession elsewhere) | Confirmed at seed |

## Ranked Desktop gRPC deadline tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-GRPC-DEADLINE-01** | Author minimal correct unary deadline/timeout policy + docs/Living Spec; keep MSI/AppImage and Type=notify locked | No Deadline on Desktop unary path @ PLAN-50 COMPLETE | after inventory **W7-348**; seed **W7-349 (#1104)** |

Inventory (**W7-348**) may refine ranking and open implement issues; seed **W7-349** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-50 CLOSED)

PLAN-50 sole ranked row (**CTRL-KESTREL-MINRATE-01**) is **DONE**. No further PLAN-50 product rows. Transport size+keepalive+min-rate tranche is saturating.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-50 COMPLETE** (W7-346 CTRL-KESTREL-MINRATE-01; seed **W7-347**).  
2. **W7-348 OPEN** — PLAN-51 inventory → open first deadline implement + follow-up seeds.  
3. **W7-349 OPEN** — seed first PLAN-51 implement after inventory.  
4. Execute ranked DESK-GRPC-DEADLINE row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-348 (#1103)** — PLAN-51 Inventory Desktop gRPC unary call deadline / timeout after PLAN-50 (set by W7-347 COMPLETE seed).
