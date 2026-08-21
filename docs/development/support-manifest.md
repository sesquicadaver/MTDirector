# Support / compatibility manifest

Capability evaluation (M1-17) builds a deterministic **capability profile** and hash from RouterOS version, packages, and an embedded compatibility manifest.

## Operator procedure

1. Capture a device (`StartCapture`) so `capabilities.device` is present on the summary.
2. Read support state from inventory observation fields / snapshot capability section (`SupportState`: supported / testing / unknown / etc.).
3. Topology validation short-circuits when capability caches remain valid for the capability hash.
4. Manifest updates are code changes under `Mfc.RouterOs` (versioned embedded resource) — not runtime edits from Desktop.

## Supported hardware profiles (MVP)

| Profile | Lab topology | Notes |
|---------|--------------|-------|
| Standalone router | `testlab/chr/topologies/standalone` | Single Device Node |
| Dual-stack router | `standalone-dual-stack` | IPv4/IPv6 independent managed filters |
| Multi-WAN failover / balanced | `multi-wan-failover`, `multi-wan-balanced` | Filter-only verify; no routing writes |
| VRRP active/passive | `vrrp-active-passive` | All members onboard/deploy together |
| VRRP split-master (fail-closed) | `vrrp-split-master` | Must not simplify to single master |
| CRS switch | `crs-switch` | INPUT/OUTPUT only; FORWARD / Bridge/VLAN/HW writes rejected |

Board class evaluation (including CRS) is part of `CapabilityProfileEvaluator`. Physical CRS live exercise remains optional residual; scripted fixture is DoD (M6-07 / M6-09).

## Rules of thumb

- RouterOS 7+ expected for VRRP/base features used in MVP topologies.
- Unknown majors stay non-silent (`SupportState` / findings) — never pretend full support.
- Capability hash excludes observation-only material so role/running flaps do not invalidate capability caches incorrectly.

See Capability types in `Mfc.Domain.Capabilities` and Living Spec rows in [`testing.md`](testing.md). Release packaging: [`../release/packaging.md`](../release/packaging.md).
