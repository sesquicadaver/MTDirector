# PLAN-47 — Controller gRPC message-size / transport limits after OTel resource identity

**Date:** 2026-09-17 (seeded; inventory **OPEN**)  
**Status:** Inventory **OPEN** (W7-332); seed **W7-333 (#1072) OPEN**; predecessor **PLAN-46 COMPLETE**  
**PLAN issue / queue:** [W7-332 / PLAN-47 #1071](https://github.com/sesquicadaver/MTDirector/issues/1071) **OPEN** (**§3.C NEXT**)  
**Predecessor:** PLAN-46 Controller OpenTelemetry resource identity **COMPLETE** (CTRL-HTTP-OTEL-RESOURCE-01)  
**Normative files:** [`Program.cs`](../../src/Mfc.Controller/Program.cs), [`ControllerConnectionService.cs`](../../src/Mfc.Desktop/Services/ControllerConnectionService.cs), [`installation.md`](../operations/installation.md) / controller-configuration  
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

## Inventory evidence (seed baseline 2026-09-17 `main` @ PLAN-46 COMPLETE / `0a87280e`)

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| `Program.cs` | `AddGrpc()` with no MaxReceive/SendMessageSize | Default ~4 MiB server limit |
| Desktop `ControllerConnectionService` | `GrpcChannel.ForAddress` without size options | Default ~4 MiB client limit |
| Domain | `RawSnapshotLimits.MaxSnapshotBytes` = 256 MiB; section paging exists | Transport may fail before domain limits |
| Glob / rg | MaxReceiveMessageSize absent under Controller/Desktop gRPC path @ `0a87280e` | Confirmed |

## Ranked Controller gRPC transport tranche (seed baseline)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **CTRL-GRPC-MSGSIZE-01** | Author minimal correct MaxReceive/SendMessageSize (Controller + Desktop) aligned with product bounds + docs/Living Spec; keep MSI/AppImage and Type=notify locked | Default 4 MiB @ `0a87280e` | after inventory **W7-332**; seed **W7-333 (#1072)** |

Inventory (**W7-332**) may refine ranking and open implement issues; seed **W7-333** advances NEXT to the first implement after inventory DONE.

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
2. **W7-332 OPEN** — PLAN-47 inventory → open first message-size implement + follow-up seeds.  
3. **W7-333 OPEN** — seed first PLAN-47 implement after inventory.  
4. Execute ranked CTRL-GRPC-MSGSIZE row(s) atomically.

## §3.C NEXT

**§3.C NEXT = W7-332 (#1071)** — PLAN-47 Inventory Controller gRPC message-size / transport limits after PLAN-46.
