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
