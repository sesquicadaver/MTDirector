# PLAN-50 — Controller Kestrel min request/response data-rate after HTTP/2 keepalive

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-344); seed **W7-345 (#1096) OPEN**; predecessor **PLAN-49 COMPLETE**  
**PLAN issue / queue:** [W7-344 / PLAN-50 #1095](https://github.com/sesquicadaver/MTDirector/issues/1095) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-49 Controller/Desktop gRPC HTTP/2 keepalive **COMPLETE** (CTRL-GRPC-KEEPALIVE-01)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs) (`ConfigureKestrel` / `Limits.MinRequestBodyDataRate` / `MinResponseDataRate`), [`installation.md`](../operations/installation.md) / [`controller-configuration.md`](../operations/controller-configuration.md)  
**Normative prior locks:** HTTP health; opt-in `/metrics`; opt-in tracing; log correlation; OTel resource identity; gRPC MaxReceive/SendMessageSize (256 MiB); Kestrel MaxRequestBodySize (256 MiB); HTTP/2 KeepAlivePing 60s/30s; gRPC health; QG-SIGN-01/02; PLAN-32…49 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-49 shipped finite HTTP/2 keepalive: size ceilings + PING are complete, but Controller Kestrel still enforces ASP.NET Core default **MinRequestBodyDataRate / MinResponseDataRate (240 B/s + 5s grace)**. Quiet Capture/Deployment/Onboarding **Watch** server-streams can stall longer than grace; HTTP/2 PING does **not** count as HTTP response-body bytes, so keepalive alone does not clear this host-layer kill. Observability PLAN-42…46 + MSGSIZE/BODY/KEEPALIVE remain saturating for size/ping; Type=notify and Desktop a11y remain deferred vanity.

## Principles

1. Installed Controller should have **minimal correct Kestrel min data-rate policy** for long-lived gRPC Watch streams (prefer `null` = no minimum, documented fail-closed intent).  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-49 KEEPALIVE / MSGSIZE / BODY polish — those waves are **COMPLETE**.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer real transport reliability over systemd Type=notify vanity polish.

## Out of scope (do not seed)

- Re-opening PLAN-49 CTRL-GRPC-KEEPALIVE-01 / PLAN-48 BODY / PLAN-47 MSGSIZE  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full gRPC load-balancing / multi-instance HA productization  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-49 COMPLETE / KEEPALIVE shipped)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Controller `ConfigureKestrel` | BODY + Http2 KeepAlivePing set | No MinRequest/ResponseDataRate override |
| ASP.NET Core defaults | MinRequestBodyDataRate / MinResponseDataRate = 240 B/s + 5s grace | Quiet Watch can trip after grace |
| HTTP/2 PING | Framing-layer keepalive | Does not satisfy response-body data-rate |
| Watch hubs | Long-lived Capture/Deployment/Onboarding streams | Quiet-progress drop risk |
| Glob / rg | MinResponseDataRate / MinRequestBodyDataRate absent under Controller @ post-KEEPALIVE | Confirmed |

## Ranked Controller Kestrel min-rate tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-KESTREL-MINRATE-01** | Author minimal correct Kestrel MinRequestBodyDataRate / MinResponseDataRate (null/disable or otherwise Watch-safe) + docs/Living Spec; keep MSI/AppImage and Type=notify locked | Default 240 B/s + 5s grace @ PLAN-49 COMPLETE | after inventory **W7-344**; seed **W7-345 (#1096)** |

Inventory (**W7-344**) may refine ranking and open implement issues; seed **W7-345** advances NEXT to the first implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-49 CLOSED)

PLAN-49 sole ranked row (**CTRL-GRPC-KEEPALIVE-01**) is **DONE**. No further PLAN-49 product rows. Transport size+keepalive tranche (MSGSIZE + BODY + KEEPALIVE) is saturating for those layers.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-49 COMPLETE** (W7-342 CTRL-GRPC-KEEPALIVE-01; seed **W7-343 DONE**).  
2. **W7-344 OPEN** — PLAN-50 inventory → open first min-rate implement + follow-up seeds.  
3. **W7-345 OPEN** — seed first PLAN-50 implement after inventory.  
4. Execute ranked CTRL-KESTREL-MINRATE row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-344 (#1095)** — PLAN-50 Inventory Controller Kestrel min request/response data-rate after PLAN-49.
