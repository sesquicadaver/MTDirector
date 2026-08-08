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
