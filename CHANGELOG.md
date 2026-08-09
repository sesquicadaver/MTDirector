# Changelog

All notable changes to MTDirector (MikroTik Firewall Controller) are documented in this file.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
Versioning follows [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Repository governance baseline (`.gitignore`, `.gitattributes`, contributing and security docs, PR/issue templates, CODEOWNERS).
- Normative specifications, ROADMAP, and GitHub issue tracker for M0–M7.
- Pinned .NET 10 SDK (`global.json` 10.0.302), Central Package Management, deterministic build props, `.editorconfig`, and NuGet.config.
- Solution skeleton `MikroTikFirewallController.sln` with normative `Mfc.*` assemblies and project-reference boundaries.
- Architecture boundary tests (M0-04) that fail the build when assembly dependency rules are violated.
- Health-only Controller host with gRPC health checks, TLS/loopback validation, JSON logging, and graceful shutdown (M0-05).
- Desktop connection shell with off-UI-thread gRPC health client and connection state display (M0-06).
- PostgreSQL bootstrap persistence with forward-only migrations, `--migrate-only`, schema guard, and append-only audit table (M0-07).
- Deterministic GitHub Actions CI (Linux validate + Windows Desktop build) with locked restore, format, coverage gates, and vulnerability scan (M0-08).
- Isolated CHR testlab skeleton with topology contracts, synthetic fixtures, and lab isolation docs (M0-09).
- Initial Accepted ADRs (0001–0005) and reproducible development/operations documentation (M0-10).
- Inventory domain model: Site/Node/Device aggregates plus Uplink, ZoneBinding, VrrpGroup/VrrpMember value model with typed management endpoints (M1-01).
- Snapshot and capability domain types: typed SHA-256 digests, CapabilityProfile, RouterOsVersion, TopologyObservation, SnapshotMetadata (M1-02).
- Inventory/snapshot PostgreSQL schema: sites/nodes/devices, connection profiles, capture operations, content-addressed payloads, immutable completed captures (M1-03; Vertical Slice §8).
- Secure RouterOS connection profiles with AES-256-GCM envelope secrets, INTERNAL_CA/SPKI trust, pin-change audit, and Desktop-safe views (M1-04).
- ROADMAP v0.2: audit of DONE vs remaining work and a single linear queue of 122 unimplemented atomic tasks (95 to MVP, 27 post-MVP).
- Read-only application ports and use cases: inventory/snapshot orchestration, RouterOS/persistence ports, typed errors, authorization boundary, hand-written fakes (M1-05).
- RouterOS API word-length codec with canonical encode/decode, fragmented prefix support, and configurable max word size (M1-06).
- RouterOS API sentence streaming parser/encoder with order-preserving attributes, duplicate policy, and pooled leases (M1-07).
- Asynchronous tagged RouterOS API session: single read loop, serialized writes, bounded pending map, /cancel, stress-tested routing (M1-08).
- Authenticated RouterOS API-SSL connection with INTERNAL_CA/SPKI validation, modern /login, and local test-CA coverage (M1-09).
- Typed allowlisted RouterOS read executor: compile-time command catalogue, explicit `.proplist`, static query profiles, trap/fatal mapping, sensitive redaction, architecture block on Write namespaces (M1-10).
- System and service discovery: identity/resource/packages/clock/API-SSL via typed reads, uptime excluded from configuration hash material, sanitized fixture + CHR smoke (M1-11).
- Interface and address discovery: IPv4/IPv6 separation, CIDR normalization, dynamic/static split, deterministic interface-list include/exclude resolution with cycle findings (M1-12).
- Firewall filter and address-list discovery: ordered IPv4/IPv6 filters, fwc: marker recognition, FastTrack action fields, dynamic list digests, counters excluded from config hash (M1-13).
- Routing and firewall-dependency discovery: tables/rules/static routes, default-route observations, ordered NAT/RAW/Mangle, rp-filter, PCC/nth/random unsupported-for-editing (M1-14).
- VRRP discovery: family+VRID+interface grouping, typed VIP bindings, owner/priority, role vs configuration hash split, mixed master/backup, no secret/script props (M1-15).
- Bridge/VLAN/switch metadata discovery: VLAN table + HW-offload observations, L2/L3 path indicators, unknown chip finding, no SwOS/transit-ACL/write path (M1-16).
- Packet-path read allowlist: `/container`, `/app`, `/interface/veth`, `/ip/vrf` with network-significant proplists; env/mounts/shell payload forbidden (N1-01).
- RouterOS capability profile: versioned embedded compatibility manifest, SupportState rules (ROS6/testing/unknown), deterministic capability hash without observations, topology-validation invalidation (M1-17).
- Packet-path topology graph: Container/App→VETH→Bridge→Bridge-VLAN/VLAN-IF→VRF projection with shared-VETH findings; `/interface/vlan/print` allowlisted; no 1:1 or firewall-path assumptions (N1-02).
- Packet-path classification: CPU_FIREWALL / HARDWARE_OFFLOADED / MIXED / INDETERMINATE for ingress/egress pairs with blocker hints (N1-03).
- Node topology validation: declared node vs explicit device observations (cardinality, VRRP groups/version/split-master, uplink evidence, SWITCH transit ban, capability-cache short-circuit) without network auto-scan (M1-18).
- Stable-read snapshot coordinator: critical-menu configuration fingerprints around discovery, bounded retry/jitter, cancellation, SNAPSHOT_UNSTABLE without persisting partial complete captures (M1-19).
- Raw snapshot assembly: versioned redacted JSON envelope with per-section capture status, centralized secret stripping, separate capture timestamps, deterministic serialization, and typed size-limit errors (M1-20).
- Canonicalization primitives: IP/prefix normalization, sorted sets, deterministic JSON writer, .id/counter exclusion, separate configuration/observation hashes, snapshot hash with schema version, idempotent Canonicalize (M1-21).
- Menu-specific canonical snapshots: discovery→section-registry projection with ordered firewall, config/observation splits (routes, VRRP role, dynamic address-lists, interface running), unknown properties in compatibility observations only (M1-22).
- Canonical snapshot persistence: content-addressed Brotli payloads, `snapshot_capture_sections`, atomic `PersistCompletedAsync`, hash indexes, cursor pagination, idempotent capture operations (M1-23).
- Deterministic semantic snapshot diff: pure `SemanticDiffEngine` with phased matching (controller UUID / natural key / fingerprint / ordered / conservative), `fwc:rule` markers, field set diffs, complexity limits; `CompareSnapshotsUseCase` loads canonical sections with hash-level fallback (M1-24).
- Inventory/discovery gRPC (`mfc.v1.InventoryService` per Vertical Slice §9.2): ListSites/CreateSite/CreateNode/GetNode/RegisterDevice/UpdateDevice/UpdateDeviceConnection/ValidateDeviceConnection; idempotency + audit; Desktop-safe connection summaries (no credentials); Issue Set `DiscoverDevice` maps to `ValidateDeviceConnection`, `GetDiscoveryStatus` deferred (full discovery = StartCapture in M1-26) (M1-25).
- Snapshot/diff gRPC (`mfc.v1.SnapshotService` per Vertical Slice §9.3): StartCapture/WatchCapture/ListCaptures/GetSnapshotSummary/GetSnapshotSection/CompareSnapshots; Canonical Spec §30 DiffEntry (`repeated DiffChange`, FieldDiff, MatchConfidence CONTROLLER_ID…); CaptureProgressHub streaming; default `NotConfiguredSnapshotCapturePort`; Issue Set aliases CaptureSnapshot→StartCapture, WatchSnapshotCapture→WatchCapture, ListSnapshots→ListCaptures (M1-26).
- Desktop inventory tree (M1-27): `ListNodes` RPC + optional Device observation fields; Avalonia Site→Node→Device TreeView via Contracts-only `InventoryTreeService` (single-flight refresh, cancellation, cached-on-error); unit tests with fake `IInventoryTreeClient`.
- Desktop snapshot viewer (M1-28): Avalonia read-only viewer (sections, config vs observations, three domain hashes + schema version, per-section capture status); `SnapshotSummary.sections` + `SnapshotSectionCaptureStatus`; technical view for `compatibility.unknown-properties`; virtualized record lists; sanitized copy/export (no credential fields); Contracts-only Desktop client.
- Desktop semantic diff viewer (M1-29): Avalonia CompareSnapshots UI (base/target selection, section groups, ADDED/REMOVED/MODIFIED/MOVED/STATE_CHANGED, config vs observation filters, explicit ordinals, No differences state); server-side only — no local SemanticDiffEngine; unknown-properties not masked.
- Standalone CHR vertical-slice acceptance (M1-30): in-process Controller+Postgres suite (identical hashes, filter→config hash+MODIFIED diff, running→observation-only, persist across restart, API-SSL trust target); Desktop wiring checks; live CHR TLS gate via `MFC_CHR_STANDALONE_HOST`; `testlab/chr/scripts/provision-standalone.sh` outside product adapter.
- Multi-WAN CHR vertical-slice acceptance (M1-31): failover/balanced topologies (routing tables/rules/static routes, NAT, mangle/PCC, strict rp-filter finding); active-state vs static-route hash split; config vs observation diffs; `provision-multi-wan.sh` outside product adapter.
