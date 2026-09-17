# PLAN-48 — Controller Kestrel request-body / HTTP2 limits after gRPC message-size

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-336); seed **W7-337 (#1080) OPEN**; predecessor **PLAN-47 COMPLETE**  
**PLAN issue / queue:** [W7-336 / PLAN-48 #1079](https://github.com/sesquicadaver/MTDirector/issues/1079) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-47 Controller gRPC message-size / transport limits **COMPLETE** (CTRL-GRPC-MSGSIZE-01)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs) (`ConfigureKestrel`), [`GrpcTransportLimits.cs`](../../src/Mfc.Contracts/GrpcTransportLimits.cs), [`installation.md`](../operations/installation.md) / [`controller-configuration.md`](../operations/controller-configuration.md)  
**Normative prior locks:** HTTP health; opt-in `/metrics`; opt-in tracing; log correlation; OTel resource identity; gRPC MaxReceive/SendMessageSize (256 MiB); gRPC health; QG-SIGN-01/02; PLAN-32…47 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-47 shipped shared gRPC message-size ceilings: Controller `ConfigureKestrel` still leaves ASP.NET Core **default MaxRequestBodySize (~30 MiB)** untouched, so large snapshot/diff RPCs can fail at the HTTP host layer despite `GrpcTransportLimits.MaxMessageBytes` = 256 MiB. Observability PLAN-42…46 remains saturating; Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. Installed Controller should have **minimal correct Kestrel request-body / HTTP2 limits** aligned with `GrpcTransportLimits.MaxMessageBytes` (fail-closed finite ceiling).  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-47 MSGSIZE polish — that wave is **COMPLETE**.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer real transport reliability over systemd Type=notify vanity polish.

## Out of scope (do not seed)

- Re-opening PLAN-47 CTRL-GRPC-MSGSIZE-01  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full gRPC load-balancing / multi-instance HA productization  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-47 COMPLETE / MSGSIZE shipped)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` `ConfigureKestrel` | Protocols + mTLS only | No `Limits.MaxRequestBodySize` |
| ASP.NET Core default | MaxRequestBodySize ≈ 30 MiB | Below gRPC 256 MiB ceiling |
| `GrpcTransportLimits` | MaxMessageBytes = 256 MiB on AddGrpc + Desktop | Host body limit may reject earlier |
| Glob / rg | `MaxRequestBodySize` absent under Controller @ MSGSIZE ship | Confirmed |

## Ranked Controller Kestrel body / HTTP2 tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-KESTREL-BODY-01** | Author minimal correct Kestrel MaxRequestBodySize (and related HTTP/2 limits if required) aligned with `GrpcTransportLimits.MaxMessageBytes` + docs/Living Spec; keep MSI/AppImage and Type=notify locked | Default ~30 MiB @ PLAN-47 COMPLETE | after inventory **W7-336**; seed **W7-337 (#1080)** |

Inventory (**W7-336**) may refine ranking and open implement issues; seed **W7-337** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-47 CLOSED)

PLAN-47 sole ranked row (**CTRL-GRPC-MSGSIZE-01**) is **DONE**. No further PLAN-47 product rows.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-47 COMPLETE** (W7-334 CTRL-GRPC-MSGSIZE-01; seed **W7-335 DONE**).  
2. **W7-336 OPEN** — PLAN-48 inventory → open first Kestrel-body implement + follow-up seeds.  
3. **W7-337 OPEN** — seed first PLAN-48 implement after inventory.  
4. Execute ranked CTRL-KESTREL-BODY row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-336 (#1079)** — PLAN-48 Inventory Controller Kestrel request-body / HTTP2 limits after PLAN-47.
