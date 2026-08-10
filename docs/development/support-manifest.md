# Support / compatibility manifest

Capability evaluation (M1-17) builds a deterministic **capability profile** and hash from RouterOS version, packages, and an embedded compatibility manifest.

## Operator procedure

1. Capture a device (`StartCapture`) so `capabilities.device` is present on the summary.
2. Read support state from inventory observation fields / snapshot capability section (`SupportState`: supported / testing / unknown / etc.).
3. Topology validation short-circuits when capability caches remain valid for the capability hash.
4. Manifest updates are code changes under `Mfc.RouterOs` (versioned embedded resource) — not runtime edits from Desktop.

## Rules of thumb

- RouterOS 7+ expected for VRRP/base features used in M1 topologies.
- Unknown majors stay non-silent (`SupportState` / findings) — never pretend full support.
- Capability hash excludes observation-only material so role/running flaps do not invalidate capability caches incorrectly.

See Capability types in `Mfc.Domain.Capabilities` and Living Spec rows in [`testing.md`](testing.md).
