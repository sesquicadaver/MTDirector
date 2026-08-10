# Snapshots, canonicalization, and semantic diff

Operator-facing summary of the M1 read-path data model. Normative detail: `Canonical Snapshot and Semantic Diff Specification v0.1.md`.

## Capture flow

1. `StartCapture` → RouterOS read adapter (stable-read + discovery) → raw redacted payload.
2. Menu projector builds canonical **configuration** and **observation** sections.
3. Hashes: `configuration_hash`, `observation_hash`, `capability_hash`, `snapshot_hash` (schema version included).
4. Persist CAS payloads + `snapshot_capture_sections` atomically; identical `snapshot_hash` deduplicates.
5. Desktop loads `GetSnapshotSummary` / `GetSnapshotSection` / `CompareSnapshots` only (no local recompute).

## Domains

| Domain | Examples | Hash impact |
|--------|----------|-------------|
| Configuration | filter rules, static routes, VRRP priority/VIP, address-lists | `configuration_hash` |
| Observations | interface `running`, route `active`, VRRP role | `observation_hash` |
| Capability | support profile / manifest digest | `capability_hash` |

Runtime role/route active-state changes must not alter configuration hash (proven in multi-WAN / VRRP acceptance).

## Section registry

Canonical section ids live in `Mfc.Domain.Canonicalization.CanonicalSectionIds` (e.g. `firewall.ipv4.filter`, `ha.vrrp`, `routing.ipv4.default-state`, `topology.validation`). Unknown RouterOS properties go to `compatibility.unknown-properties` observations — never silently dropped.

## Semantic diff

- Server-side `SemanticDiffEngine` / `CompareSnapshots` only.
- DiffEntry carries `DiffChange` set (ADDED/REMOVED/MODIFIED/MOVED/STATE_CHANGED) and field diffs.
- Managed rules use `fwc:rule:{uuid}:{rev}` markers for stable MODIFIED matching.
- Empty result → Desktop **No differences** state.

## Schema version

`schema_version` is part of snapshot hash material. Bumping requires coordinated projector + store migration (see Vertical Slice §8 / M1-23).
