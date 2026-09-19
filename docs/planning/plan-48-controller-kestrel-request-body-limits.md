# PLAN-48 — Controller Kestrel request-body / HTTP2 limits after gRPC message-size

**Date:** 2026-09-17 (inventory **DONE** @ `319d35bd`)  
**Status:** **PLAN-48 COMPLETE** — Inventory **DONE** (W7-336); seed **W7-337 (#1080) DONE**; implement **W7-338 (#1082) DONE**; COMPLETE seed **W7-339 (#1084) DONE**; successor **PLAN-49** inventory **W7-340 (#1087) DONE**; seed **W7-341 (#1088) DONE**; implement **W7-342 (#1090) DONE**; COMPLETE **W7-343 (#1092) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-336 / PLAN-48 #1079](https://github.com/sesquicadaver/MTDirector/issues/1079) **DONE**  
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

## Inventory evidence (W7-336 @ `main` `319d35bd`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` `ConfigureKestrel` | Protocols + mTLS only | No `Limits.MaxRequestBodySize` |
| ASP.NET Core default | MaxRequestBodySize ≈ 30 MiB (30000000) | Below gRPC 256 MiB ceiling |
| `GrpcTransportLimits` | MaxMessageBytes = 256 MiB on AddGrpc + Desktop | Host body limit may reject earlier |
| Glob / rg | `MaxRequestBodySize` absent under Controller @ `319d35bd` | Confirmed |

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-KESTREL-BODY-01**) covering minimal correct Kestrel `Limits.MaxRequestBodySize`:

- Align with `GrpcTransportLimits.MaxMessageBytes` (256 MiB = 268435456) — same fail-closed constant as MSGSIZE
- Do **not** set unlimited / `null`; keep a finite ceiling
- Related HTTP/2 frame limits are **not** required for this gap (messages stream across frames; body ceiling is the host reject)
- Keep MSI/AppImage locked; do not regress MSGSIZE/health/metrics/tracing/correlation/resource
- Docs (`installation.md` / `controller-configuration.md`) + Living Spec

Splitting MaxRequestBodySize vs HTTP/2 frame polish into two ranks would be vanity; Type=notify and Desktop a11y remain deferred adjacent residuals.

## Ranked Controller Kestrel body / HTTP2 tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-KESTREL-BODY-01** | Author minimal correct Kestrel MaxRequestBodySize aligned with `GrpcTransportLimits.MaxMessageBytes` + docs/Living Spec; keep MSI/AppImage and Type=notify locked | Default ~30 MiB @ `319d35bd` | implement **W7-338 (#1082) DONE**; COMPLETE **W7-339 (#1084) DONE**; seed **W7-337 (#1080) DONE** |

Inventory (**W7-336 DONE**) confirmed sole rank. Seed **W7-337** advances NEXT to BODY-01 implement after inventory.

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
2. **W7-336 DONE** — PLAN-48 inventory; opened **W7-338 (#1082)** CTRL-KESTREL-BODY-01 implement.  
3. **W7-337 DONE** — seed advanced NEXT to CTRL-KESTREL-BODY-01; opened COMPLETE **W7-339 (#1084)**.  
4. **W7-338 DONE** — sole CTRL-KESTREL-BODY-01 shipped.
5. **W7-339 DONE** — PLAN-48 COMPLETE; seeded PLAN-49 inventory **W7-340**.

## Delivery notes (W7-338)

Controller `ConfigureKestrel` sets `Limits.MaxRequestBodySize = GrpcTransportLimits.MaxMessageBytes` (**256 MiB** / **268435456**). Finite fail-closed; never unlimited/`null`. Docs: `installation.md` §10 + `controller-configuration.md`. MSGSIZE/health/metrics/tracing/correlation/resource unchanged.

## Adjacent residual seeded as PLAN-49

- Controller/Desktop gRPC HTTP/2 keepalive for long-lived Watch streams — seeded as **PLAN-49** [`plan-49-controller-grpc-http2-keepalive.md`](plan-49-controller-grpc-http2-keepalive.md)

## §3.C NEXT

**§3.C NEXT = W7-349 (#1104)** — PLAN-50 Inventory Controller Kestrel min request/response data-rate after PLAN-49.
