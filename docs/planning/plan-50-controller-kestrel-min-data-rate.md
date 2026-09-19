# PLAN-50 — Controller Kestrel min request/response data-rate after HTTP/2 keepalive

**Date:** 2026-09-17 (inventory **DONE** @ `be2ac7a8`)  
**Status:** **PLAN-50 COMPLETE** — Inventory **DONE** (W7-344); seed **W7-345 (#1096) DONE**; implement **W7-346 (#1098) DONE**; COMPLETE seed **W7-347 (#1099) DONE**; successor **PLAN-51** inventory **W7-348 (#1103) DONE**; seed **W7-349 (#1104) DONE**; implement **W7-350 (#1106) DONE**; COMPLETE **W7-351 (#1107) DONE**; successor **PLAN-52** inventory **W7-352 (#1111) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-344 / PLAN-50 #1095](https://github.com/sesquicadaver/MTDirector/issues/1095) **DONE**  
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

## Inventory evidence (W7-344 @ `main` `be2ac7a8`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Controller `ConfigureKestrel` | BODY + Http2 KeepAlivePing set | No MinRequest/ResponseDataRate override |
| ASP.NET Core defaults | MinRequestBodyDataRate / MinResponseDataRate = 240 B/s + 5s grace | Quiet Watch can trip after grace |
| HTTP/2 PING | Framing-layer keepalive | Does not satisfy response-body data-rate |
| Watch hubs | Long-lived Capture/Deployment/Onboarding streams | Quiet-progress drop risk |
| Glob / rg | MinResponseDataRate / MinRequestBodyDataRate absent under Controller @ `be2ac7a8` | Confirmed |

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-KESTREL-MINRATE-01**) covering minimal correct Kestrel min data-rate policy:

- Set `Limits.MinRequestBodyDataRate = null` and `Limits.MinResponseDataRate = null` (disabled — documented fail-closed choice for quiet Watch longevity; not Watch-safe finite intervals that still risk false kills)
- Do **not** regress MSGSIZE / BODY / KEEPALIVE / health / metrics / tracing / correlation / resource
- Keep MSI/AppImage locked; Type=notify remains deferred vanity
- Docs (`installation.md` / `controller-configuration.md`) + Living Spec locking the null/disabled policy

Splitting request vs response min-rate into two ranks would be vanity; Type=notify and Desktop a11y remain deferred adjacent residuals.

## Ranked Controller Kestrel min-rate tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-KESTREL-MINRATE-01** | Author minimal correct Kestrel MinRequestBodyDataRate / MinResponseDataRate (**null**/disabled) + docs/Living Spec; keep MSI/AppImage and Type=notify locked | Default 240 B/s + 5s grace @ `be2ac7a8` | after inventory **W7-344 DONE**; seed **W7-345 (#1096) OPEN**; implement **W7-346 (#1098) OPEN**; COMPLETE **W7-347 (#1099) OPEN** |

Inventory (**W7-344 DONE**) confirmed sole rank. Seed **W7-345** advances NEXT to the MINRATE implement after inventory DONE.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-49 CLOSED)

PLAN-49 sole ranked row (**CTRL-GRPC-KEEPALIVE-01**) is **DONE**. No further PLAN-49 product rows. Transport size+keepalive tranche (MSGSIZE + BODY + KEEPALIVE) is saturating for those layers; min-rate remains the host-layer quiet-stream gap.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates  
- After MINRATE, transport (size + ping + min-rate) is **saturating** — successor PLAN-51 should pick a non-vanity non-transport gap (evidence: Desktop unary RPCs lack gRPC `Deadline` / CallOptions timeouts except Health `CancelAfter`)

## §3.C ordering

1. **PLAN-49 COMPLETE** (W7-342 CTRL-GRPC-KEEPALIVE-01; seed **W7-343 DONE**).  
2. **W7-344 DONE** — PLAN-50 inventory; opened **W7-346 (#1098)** CTRL-KESTREL-MINRATE-01 implement + **W7-347 (#1099)** COMPLETE follow-up.  
3. **W7-345 DONE** — seed advanced NEXT to CTRL-KESTREL-MINRATE-01; opened COMPLETE **W7-347 (#1099)**.  
4. **W7-346 DONE** — CTRL-KESTREL-MINRATE-01 null/disabled min data rates.
5. **W7-347 DONE** — PLAN-50 COMPLETE; seeded PLAN-51 inventory **W7-348**.

## Delivery notes (W7-346)

CTRL-KESTREL-MINRATE-01 shipped: `ConfigureKestrel` sets `Limits.MinRequestBodyDataRate = null` and `Limits.MinResponseDataRate = null` (disabled). Documented fail-closed choice for quiet Watch longevity — ASP.NET defaults (240 B/s + 5s grace) would kill quiet Capture/Deployment/Onboarding server-streams; HTTP/2 PING does not count as response-body bytes. MSGSIZE / BODY / KEEPALIVE unchanged.

## Adjacent residual seeded as PLAN-51

- Desktop gRPC unary call deadline / timeout policy — seeded as **PLAN-51** [`plan-51-desktop-grpc-unary-deadline.md`](plan-51-desktop-grpc-unary-deadline.md)

## §3.C NEXT

**§3.C NEXT = W7-387 (#1179)** — PLAN-51 Inventory Desktop gRPC unary call deadline / timeout after PLAN-50.
