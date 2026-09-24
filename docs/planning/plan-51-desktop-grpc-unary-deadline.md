# PLAN-51 — Desktop gRPC unary call deadline / timeout after transport saturates

**Date:** 2026-09-17 (inventory **DONE** @ `9001354c`)  
**Status:** **PLAN-51 COMPLETE** — Inventory **DONE** (W7-348); seed **W7-349 (#1104) DONE**; implement **W7-350 (#1106) DONE**; COMPLETE seed **W7-351 (#1107) DONE**; successor **PLAN-52** inventory **W7-352 (#1111) DONE**; seed **W7-353 (#1112) DONE**; implement **W7-354 (#1114) DONE**; COMPLETE seed **W7-355 (#1115) DONE**; successor **PLAN-53** inventory **W7-356 (#1119) OPEN** (**§3.C NEXT**)  
**PLAN issue / queue:** [W7-348 / PLAN-51 #1103](https://github.com/sesquicadaver/MTDirector/issues/1103) **DONE**  
**Predecessor:** PLAN-50 Controller Kestrel min request/response data-rate **COMPLETE** (CTRL-KESTREL-MINRATE-01)  
**Normative files:** Desktop gRPC call sites (`src/Mfc.Desktop/Services/Grpc*Client.cs`), `DesktopOptions`, shared unary call helper, connection / installation / development docs  
**Normative prior locks:** HTTP health; opt-in `/metrics`; opt-in tracing; log correlation; OTel resource identity; gRPC MaxReceive/SendMessageSize (256 MiB); Kestrel MaxRequestBodySize (256 MiB); HTTP/2 KeepAlivePing 60s/30s; Kestrel MinRequest/ResponseDataRate null; gRPC health; QG-SIGN-01/02; PLAN-32…50 — **do not regress**  
**Normative execution order:** [`ROADMAP.md`](../../ROADMAP.md) §3.C  

Absorb the highest-value **non-packaging / non-vanity** continuous-queue gap after PLAN-50 shipped null Kestrel min data-rates: **transport (MSGSIZE + BODY + KEEPALIVE + MINRATE) is saturating**. Desktop unary gRPC calls lack `CallOptions.Deadline` / bounded timeouts — only Health uses `CancelAfter(HealthCheckTimeoutSeconds)`. A hung Controller can hang operator UI indefinitely. Type=notify and Desktop a11y remain deferred vanity.

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
- Deadlines on long-lived Capture / Deployment / Onboarding **Watch** streams

## Inventory evidence (W7-348 @ `main` `9001354c`)

### Transport / Health baseline

| Surface | Current behavior | Gap |
|---------|------------------|-----|
| Desktop Health | `CancelAfter(HealthCheckTimeoutSeconds)` (default **5s**) in `ControllerConnectionService` | Bounded (keep / fold consistently) |
| Transport stack | MSGSIZE + BODY + KEEPALIVE + MINRATE | Saturating — do not re-open |
| Glob / rg | `Deadline` / `WithDeadline` / `CallOptions` absent under `Grpc*Client.cs` | Confirmed @ `9001354c` |

### Unary call sites lacking Deadline (63 unique client RPC methods)

All pass `ActorHeaders()` + `cancellationToken` only — **no** `deadline:` / `CallOptions.Deadline`.

| Client | Unary methods without Deadline (count) |
|--------|----------------------------------------|
| `GrpcAuditServiceClient` | ListAuditEventsAsync (**1**) |
| `GrpcDeploymentServiceClient` | CreatePlanAsync, CreatePlanFromSealedArtifactsAsync, StartAsync, RollbackAsync, GetRecoveryStatusAsync (**5**) — **Watch excluded** |
| `GrpcDriftServiceClient` | ListDeviceDriftEventsAsync, GetDriftEventAsync (**2**) |
| `GrpcIncidentServiceClient` | IngestIncidentSignalAsync, BindIncidentResponseAssessmentAsync (**2**) |
| `GrpcInventoryTreeClient` | ListSitesAsync, ListNodesAsync, GetNodeAsync, GetNodeWorkflowAsync, CreateSiteAsync, CreateNodeAsync, RegisterDeviceAsync, UpdateDeviceConnectionAsync, ValidateDeviceConnectionAsync, ListNeighborCandidatesAsync, ValidateVrrpPairConsistencyAsync (**11**) |
| `GrpcOnboardingServiceClient` | ValidatePrerequisitesAsync, CreatePlanAsync, StartAsync, RollbackAsync, GetRecoveryStatusAsync (**5**) — **Watch excluded** |
| `GrpcPolicyServiceClient` | CreateDraftPolicyAsync … GetDevicePolicySafetyAnalysisAsync (**22**) |
| `GrpcRoutingAssuranceServiceClient` | GetDeviceRoutingAssuranceStateAsync (**1**) |
| `GrpcSnapshotViewerClient` | StartCaptureAsync, ListCapturesAsync, GetSnapshotSummaryAsync, GetSnapshotSectionAsync, CompareSnapshotsAsync (**5**) — **WatchCapture excluded** |
| `GrpcZoneServiceClient` | ListZoneDefinitionsAsync … ResolveZonesForDeviceAsync (**9**) |
| **Total** | **63** unary RPCs lack Deadline |

### Streaming Watch sites (must remain without forced unary Deadline)

| Client | Streaming RPC | Policy |
|--------|---------------|--------|
| `GrpcDeploymentServiceClient` | `Watch` | Long-lived — **no** unary deadline |
| `GrpcOnboardingServiceClient` | `Watch` | Long-lived — **no** unary deadline |
| `GrpcSnapshotViewerClient` | `WatchCapture` | Long-lived — **no** unary deadline |

**Ranking decision:** Prefer **ONE atomic row** (**DESK-GRPC-DEADLINE-01**) covering minimal correct unary deadline policy:

- Add `DesktopOptions.UnaryCallTimeoutSeconds` (finite default; fail-closed when ≤0)
- Shared helper applying `CallOptions` with `Deadline` on **unary** `Grpc*Client` RPCs only
- Keep Health `CancelAfter(HealthCheckTimeoutSeconds)` consistent (probe-scoped; distinct knob OK)
- Do **not** force deadlines on Watch/Capture streaming RPCs
- Do **not** regress MSGSIZE / BODY / KEEPALIVE / MINRATE
- Docs (`connection-profiles.md` / installation / development) + Living Spec locking option + helper / call-site coverage

Splitting per-service deadline ranks would be vanity; Type=notify and Desktop a11y remain deferred adjacent residuals.

## Ranked Desktop gRPC deadline tranche (inventory lock)

| Rank | ID | Gap | Evidence | Queue |
|------|----|-----|----------|-------|
| 1 | **DESK-GRPC-DEADLINE-01** | Author minimal correct unary deadline/timeout policy + docs/Living Spec; keep MSI/AppImage and Type=notify locked; exclude Watch streams | **63** unary `Grpc*Client` methods lack Deadline @ `9001354c`; Health alone bounded | after inventory **W7-348 DONE**; seed **W7-349 (#1104) DONE**; implement **W7-350 (#1106) DONE**; COMPLETE **W7-351 (#1107) DONE** |

Inventory (**W7-348 DONE**) confirmed sole rank. Seed **W7-349** advances NEXT to the DEADLINE implement after inventory DONE.

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

1. **PLAN-50 COMPLETE** (W7-346 CTRL-KESTREL-MINRATE-01; seed **W7-347 DONE**).  
2. **W7-348 DONE** — PLAN-51 inventory; opened **W7-350 (#1106)** DESK-GRPC-DEADLINE-01 implement + **W7-351 (#1107)** COMPLETE follow-up.  
3. **W7-349 (#1104) DONE** — seed advanced NEXT to DESK-GRPC-DEADLINE-01; keep COMPLETE **W7-351** open.  
4. **W7-350 (#1106) DONE** — DESK-GRPC-DEADLINE-01 unary deadline policy.  
5. **W7-351 (#1107) DONE** — PLAN-51 COMPLETE → seed PLAN-52 inventory.

## Delivery notes (W7-350)

`DesktopOptions.UnaryCallTimeoutSeconds` defaults to **30**. `DesktopGrpcUnaryCall.For` builds `CallOptions` with that deadline (fail-closed when ≤0) on unary `Grpc*Client` RPCs. Health stays on `CancelAfter(HealthCheckTimeoutSeconds)`. Watch / WatchCapture are not deadline-bounded. Transport MSGSIZE / BODY / KEEPALIVE / MINRATE are unchanged.

## §3.C NEXT

**§3.C NEXT = none** — PLAN-52 Inventory Desktop gRPC ErrorDetail operator mapping after PLAN-51.
