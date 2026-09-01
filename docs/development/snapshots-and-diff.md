# Snapshots, canonicalization, and semantic diff

Operator-facing summary of the M1 read-path data model. Normative detail: `Canonical Snapshot and Semantic Diff Specification v0.1.md`.

## Capture flow

1. `StartCapture` → RouterOS read adapter (stable-read + discovery) → raw redacted payload.
2. Menu projector builds canonical **configuration** and **observation** sections.
3. Hashes: `configuration_hash`, `observation_hash`, `capability_hash`, `snapshot_hash` (schema version included).
4. Persist CAS payloads + `snapshot_capture_sections` atomically; identical `snapshot_hash` deduplicates.
5. Desktop Capture (W3.1) calls `StartCapture` + `WatchCapture` (device_id); viewer then loads `GetSnapshotSummary` / `GetSnapshotSection` / `CompareSnapshots` (no local recompute).

## Domains

| Domain | Examples | Hash impact |
|--------|----------|-------------|
| Configuration | filter rules, static routes, VRRP priority/VIP, address-lists | `configuration_hash` |
| Observations | interface `running`, route `active`, VRRP role | `observation_hash` |
| Capability | support profile / manifest digest | `capability_hash` |

Runtime role/route active-state changes must not alter configuration hash (proven in multi-WAN / VRRP acceptance).

## Section registry

Canonical section ids live in `Mfc.Domain.Canonicalization.CanonicalSectionIds` (e.g. `firewall.ipv4.filter`, `ha.vrrp`, `routing.ipv4.default-state`, `topology.container-veth`, `topology.shared-veth`, `topology.validation`). Unknown RouterOS properties go to `compatibility.unknown-properties` observations — never silently dropped.

## Desktop snapshot viewer

- Record lists stay read-only: `GetSnapshotSummary` / `GetSnapshotSection`; copy is sanitized (no credential field values).
- W1.2: selected record detail binds all `SnapshotRecordListItem.Fields` (`DisplayLine`); list `SummaryLine` stays compact (≤4 + ellipsis). Unmanaged fingerprint `StableKey` does not lead the list line (W6-01).
- W6-01: when present, Snapshots open `firewall.ipv4.filter` first (then `firewall.ipv6.filter` / `ha.vrrp`); section sidebar is operator-facing order, not alphabetical.
- W3.1: Capture button → `StartCapture` + `WatchCapture` (device_id); progress shows stage / `current_section`; COMPLETED reloads the device list. Not a Desktop→RouterOS write and not WriteEnabled.
- W6-03: `StartCapture(node_id)` captures every Device on the Node under one operation/Watch stream (Desktop Node Capture-all).
- W4.4: VRRP pair — Capture is per member (select Device a or b; the Node is not a capture target and the first child is not used silently). Compare remains same-device only.

## Semantic diff

- Server-side `SemanticDiffEngine` / `CompareSnapshots` only.
- DiffEntry carries `DiffChange` set (ADDED/REMOVED/MODIFIED/MOVED/STATE_CHANGED) and field diffs.
- Managed rules use `fwc:rule:{uuid}:{rev}` markers for stable MODIFIED matching.
- Empty result → Desktop **No differences** state.
- Desktop Semantic diff (W1.1): binds `FieldLines.Summary` per entry + Compare `Warnings` (`HasWarnings`); does not re-run local SemanticDiffEngine.
- W6-01: unmanaged fingerprint `RecordKey` stays on the wire but does not lead `HeaderLine`; ADDED/REMOVED rows show After/Before fields when FieldDiffs are empty. Selecting a Device with two captures auto-runs Compare.
- W2.1: selected Diff entry shows sanitized Before/After `SnapshotRecord` fields (credentials omitted). Compare warnings are unioned across pages and truncated to 12 in the UI with an overflow line.
- W4.4: VRRP members a and b are different devices — comparing a against b is forbidden (`SNAPSHOTS_FROM_DIFFERENT_DEVICES`); Desktop shows why. Capture each member and compare two captures of that same member.

## Schema version

`schema_version` is part of snapshot hash material. Bumping requires coordinated projector + store migration (see Vertical Slice §8 / M1-23).
