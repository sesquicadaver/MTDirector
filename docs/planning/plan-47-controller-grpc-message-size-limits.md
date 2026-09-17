# PLAN-47 — Controller gRPC message-size / transport limits after OTel resource identity

**Date:** 2026-09-17 (inventory **DONE** @ `d107b57d`)  
**Status:** **PLAN-47 COMPLETE** — Inventory **DONE** (W7-332); seed **W7-333 (#1072) DONE**; implement **W7-334 (#1074) DONE**; COMPLETE seed **W7-335 (#1076) DONE**; successor **PLAN-48** inventory **W7-336 (#1079) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-332 / PLAN-47 #1071](https://github.com/sesquicadaver/MTDirector/issues/1071) **DONE**  
**Predecessor:** PLAN-46 Controller OpenTelemetry resource identity **COMPLETE** (CTRL-HTTP-OTEL-RESOURCE-01)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs), [`ControllerConnectionService.cs`](../../src/Mfc.Desktop/Services/ControllerConnectionService.cs), [`installation.md`](../operations/installation.md) / [`controller-configuration.md`](../operations/controller-configuration.md)  
**Normative prior locks:** HTTP health; opt-in `/metrics`; opt-in tracing; log correlation; OTel resource identity; gRPC health; QG-SIGN-01/02; PLAN-32…46 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-46 shipped OTel resource identity: operator observability (health→metrics→tracing→correlation→resource) is **saturating**. systemd Type=notify and Desktop a11y remain deferred vanity. Evidence shows Controller/Desktop gRPC stacks still rely on **default ~4 MiB** message limits while product snapshot domain allows much larger raw payloads — risking opaque transport failures on section/diff/operator RPCs.

## Principles

1. Installed Controller + Desktop should have **minimal correct gRPC message-size / transport limits** aligned with product snapshot/diff bounds (or documented fail-closed ceilings).  
2. Do not invent AppImage/MSI (W7-22).  
3. Lab / CHR / `WriteEnabled` are **not** stop-gates.  
4. Do not invent further PLAN-46 RESOURCE / observability polish — that wave is **COMPLETE** / saturating.  
5. Avoid vanity Desktop a11y (nested ListBox / unnamed TabControl).  
6. Prefer real transport reliability over systemd Type=notify vanity polish.

## Out of scope (do not seed)

- Re-opening PLAN-46 CTRL-HTTP-OTEL-RESOURCE-01  
- Native MSI / AppImage / changing `--self-contained false` (W7-22)  
- Nested ListBox / TabControl a11y vanity  
- Ops / CRS / physical lab live runners as §3 stop-gates  
- Full gRPC load-balancing / multi-instance HA productization  
- systemd `Type=notify` / `WatchdogSec` packaging polish (explicitly deferred / saturating)

## Inventory evidence (W7-332 @ `main` `d107b57d`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | `AddGrpc()` with no MaxReceive/SendMessageSize | Default ~4 MiB server limit |
| Desktop `ControllerConnectionService` | `GrpcChannel.ForAddress` without size options | Default ~4 MiB client limit |
| Domain | `RawSnapshotLimits.MaxSnapshotBytes` = 256 MiB (268435456); section paging exists | Transport may fail before domain limits |
| Glob / rg | `MaxReceiveMessageSize` / `MaxSendMessageSize` absent under Controller/Desktop gRPC path @ `d107b57d` | Confirmed |

**Ranking decision:** Prefer **ONE atomic row** (**CTRL-GRPC-MSGSIZE-01**) covering minimal correct MaxReceive/SendMessageSize on both sides:

- Align configured sizes with `RawSnapshotLimits.MaxSnapshotBytes` (256 MiB = 268435456) via a shared fail-closed constant (Contracts and/or Controller options referenced by Desktop)
- Do **not** set unlimited; keep a finite ceiling
- Keep MSI/AppImage locked; do not regress health/metrics/tracing/correlation/resource
- Docs (`installation.md` / `controller-configuration.md`) + Living Spec

Splitting server vs client into two ranks would be vanity (both must match to be useful); Type=notify and Desktop a11y remain deferred adjacent residuals.

## Ranked Controller gRPC transport tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-GRPC-MSGSIZE-01** | Author minimal correct MaxReceive/SendMessageSize (Controller + Desktop) aligned with product bounds + shared constant + docs/Living Spec; keep MSI/AppImage and Type=notify locked | Default 4 MiB @ `d107b57d` | implement **W7-334 (#1074) DONE**; COMPLETE **W7-335 (#1076) DONE**; seed **W7-333 (#1072) DONE** |

Inventory (**W7-332 DONE**) confirmed sole rank. Seed **W7-333** advances NEXT to MSGSIZE-01 implement after inventory.

## Dual track

Product §3 never waits on GNS3.

## Residual notes (PLAN-46 CLOSED)

PLAN-46 sole ranked row (**CTRL-HTTP-OTEL-RESOURCE-01**) is **DONE**. No further PLAN-46 product rows. Observability tranche PLAN-42…46 is saturating.

## Adjacent residuals (not seeded here)

- Unnamed TabControl containers — deferred vanity  
- Nested ListBox item-template hosts — deferred vanity  
- Native MSI / AppImage / self-contained publish default — W7-22 lock  
- systemd Type=notify/WatchdogSec — deferred packaging polish  
- Ops residuals (CRS / physical lab / live CHR) remain parallel, not §3 stop-gates

## §3.C ordering

1. **PLAN-46 COMPLETE** (W7-330 CTRL-HTTP-OTEL-RESOURCE-01; seed **W7-331 DONE**).  
2. **W7-332 DONE** — PLAN-47 inventory; opened **W7-334 (#1074)** CTRL-GRPC-MSGSIZE-01 implement.  
3. **W7-333 DONE** — seed advanced NEXT to CTRL-GRPC-MSGSIZE-01; opened COMPLETE **W7-335 (#1076)**.  
4. **W7-334 DONE** — sole CTRL-GRPC-MSGSIZE-01 shipped.
5. **W7-335 DONE** — PLAN-47 COMPLETE; seeded PLAN-48 inventory **W7-336**.

## Delivery notes (W7-334)

Shared `Mfc.Contracts.GrpcTransportLimits.MaxMessageBytes` = **256 MiB** (268435456), aligned with `RawSnapshotLimits.MaxSnapshotBytes`. Controller `AddGrpc` and Desktop `GrpcChannelOptions` both set MaxReceive/SendMessageSize to that constant (finite fail-closed; never unlimited). Docs: `installation.md` §9 + `controller-configuration.md`. Health/metrics/tracing/correlation/resource unchanged.

## Adjacent residual seeded as PLAN-48

- Controller Kestrel MaxRequestBodySize / HTTP2 host limits — seeded as **PLAN-48** [`plan-48-controller-kestrel-request-body-limits.md`](plan-48-controller-kestrel-request-body-limits.md)

## §3.C NEXT

**§3.C NEXT = W7-336 (#1079)** — PLAN-48 Inventory Controller Kestrel request-body / HTTP2 limits after PLAN-47.
