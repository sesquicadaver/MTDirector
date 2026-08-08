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
