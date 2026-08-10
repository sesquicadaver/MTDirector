# Architecture overview

MTDirector (MikroTik Firewall Controller) is a **modular monolith**: one deployable Controller process, one Desktop client, and clear assembly boundaries.

```text
Avalonia Desktop  --gRPC/mTLS-->  ASP.NET Core Controller
                                        |
                         +--------------+--------------+
                         |              |              |
                      Domain      Infrastructure    RouterOs
                   (pure model)   (PostgreSQL/EF)  (API-SSL only)
```

Normative detail lives in `TOR-1.md` and the MVP specifications. This folder records **decisions** (ADRs), not a restatement of the full ТЗ.

## Domain progress (M1)

| Slice | Status | Location |
|-------|--------|----------|
| Inventory aggregates | Done (M1-01) | `src/Mfc.Domain/Inventory/` |
| Snapshot / capability VOs | Done (M1-02) | `src/Mfc.Domain/Snapshots/`, `src/Mfc.Domain/Capabilities/` |
| Persistence schema | Done (M1-03) | `sites`/`nodes`/`devices`, `capture_operations`, `snapshot_*` |
| Secure connection profiles | Done (M1-04) | AES-256-GCM envelope secrets + INTERNAL_CA/SPKI trust |
| Application ports / use cases | Done (M1-05) | `src/Mfc.Application/Inventory/`, `Snapshots/` + narrow ports |
| RouterOS word-length codec | Done (M1-06) | `src/Mfc.RouterOs/Protocol/ApiWordLengthCodec.cs` |
| RouterOS sentence codec | Done (M1-07) | `ApiSentenceParser` / `ApiSentenceEncoder` |
| Tagged API session | Done (M1-08) | `src/Mfc.RouterOs/Session/RosSession.cs` |
| Authenticated API-SSL | Done (M1-09) | `src/Mfc.RouterOs/Transport/AuthenticatedRosConnection.cs` |
| Typed read executor | Done (M1-10) | `src/Mfc.RouterOs/Commands/RosReadCommandExecutor.cs` |
| System/service discovery | Done (M1-11) | `src/Mfc.RouterOs/Discovery/SystemServiceDiscovery.cs` |
| Interface/address discovery | Done (M1-12) | `src/Mfc.RouterOs/Discovery/InterfaceAddressDiscovery.cs` |
| Firewall filter discovery | Done (M1-13) | `src/Mfc.RouterOs/Discovery/FirewallFilterDiscovery.cs` |
| Routing/dependency discovery | Done (M1-14) | `src/Mfc.RouterOs/Discovery/RoutingDependencyDiscovery.cs` |
| VRRP discovery | Done (M1-15) | `src/Mfc.RouterOs/Discovery/VrrpDiscovery.cs` |
| Bridge/VLAN/switch discovery | Done (M1-16) | `src/Mfc.RouterOs/Discovery/BridgeSwitchDiscovery.cs` |
| Packet-path allowlist | Done (N1-01) | `src/Mfc.RouterOs/Commands/PacketPathAllowlist.cs` |
| Capability profile | Done (M1-17) | `src/Mfc.RouterOs/Capabilities/CapabilityProfileEvaluator.cs` |
| Packet-path topology graph | Done (N1-02) | `src/Mfc.RouterOs/Discovery/PacketPathTopologyDiscovery.cs` |
| Packet-path class | Done (N1-03) | `src/Mfc.RouterOs/Discovery/PacketPathClassifier.cs` |
| Node topology validation | Done (M1-18) | `src/Mfc.Domain/Topology/NodeTopologyValidator.cs` |
| Stable-read coordinator | Done (M1-19) | `src/Mfc.RouterOs/Snapshot/StableReadCoordinator.cs` |
| Raw snapshot assembly | Done (M1-20) | `src/Mfc.RouterOs/Snapshot/RawSnapshotAssembler.cs` |
| Canonicalization primitives | Done (M1-21) | `src/Mfc.Domain/Canonicalization/` |
| Menu-specific canonical snapshots | Done (M1-22) | `src/Mfc.RouterOs/Snapshot/DiscoveryCanonicalProjector.cs` |
| Persist canonical snapshots | Done (M1-23) | `EfSnapshotStore`, `snapshot_capture_sections`, Brotli content-addressed payloads |
| Semantic snapshot diff | Done (M1-24) | `src/Mfc.Domain/Diff/`, `CompareSnapshotsUseCase`, `LoadCanonicalSectionsAsync` |
| Inventory/discovery gRPC | Done (M1-25) | `Protos/mfc/v1/inventory.proto`, `InventoryGrpcService` (VS §9.2; ValidateDeviceConnection ← DiscoverDeviceUseCase) |
| Snapshot/diff gRPC | Done (M1-26) | `Protos/mfc/v1/snapshots.proto`, `SnapshotGrpcService` (VS §9.3; DiffEntry = Canonical Spec §30; Issue Set CaptureSnapshot→StartCapture) |
| Desktop inventory tree | Done (M1-27) | Avalonia Site→Node→Device tree; `ListNodes` RPC; Contracts-only Desktop client; cached-on-error refresh |
| Desktop snapshot viewer | Done (M1-28) | Read-only Avalonia viewer; `SnapshotSummary.sections` statuses; config/obs split; sanitized export |
| Desktop semantic diff viewer | Done (M1-29) | Avalonia CompareSnapshots UI; section-grouped DiffEntry rows; no local recompute |
| Standalone CHR acceptance | Done (M1-30) | In-process vertical-slice suite + live CHR TLS gate + lab provision script |
| Multi-WAN CHR acceptance | Done (M1-31) | Failover/balanced routing+NAT+mangle slice; config≠obs route diffs; lab provision |
| VRRP CHR acceptance | Done (M1-32) | Active/passive + split-master; per-VRID roles; role≠config hash; topology blockers; lab provision |
| Fault-injection suite | Done (M1-33) | Protocol/session/stable-read matrix + snapshot capture faults; typed codes; no orphan completes |
| M1 acceptance package | Done (M1-34) | Operator docs + acceptance report; **M1 CLOSED** |
| Policy document lifecycle | Done (M2-01) | `Policy` / `PolicyRevision`, MFC-CJ1 hash, Brotli `policy_revisions` |
| Policy Pipeline v1 + chain contracts | Done (M2-02) | Fixed stages + owner/effect matrix; company-only `ChainContract` |
| Remaining delivery order | See ROADMAP v0.2 §3 | Linear queue continues at M2-03 (#50) |

## ADRs

| ID | Title | Status |
|----|-------|--------|
| [0001](adr/0001-modular-monolith.md) | Modular monolith | Accepted |
| [0002](adr/0002-routeros-api-ssl.md) | RouterOS API-SSL transport | Accepted |
| [0003](adr/0003-node-deployment-atomicity.md) | Node deployment atomicity | Accepted |
| [0004](adr/0004-postgresql-source-of-truth.md) | PostgreSQL as source of truth | Accepted |
| [0005](adr/0005-no-direct-desktop-routeros-access.md) | No Desktop→RouterOS access | Accepted |

## Related development docs

- [Local environment](../development/local-environment.md)
- [Testing](../development/testing.md)
- [Database migrations](../development/database-migrations.md)
- [CHR lab isolation](../development/chr-lab.md)
- [CI](../development/ci.md)
