# RouterOS prerequisite checklist (MVP)

Operator checklist before onboarding / deploy. Typed enforcement lives in `OnboardingPrerequisiteValidator` (M5-02) and related Living Specs.

## Device / platform

- [ ] RouterOS major/build is on the supported compatibility manifest ([`../development/support-manifest.md`](../development/support-manifest.md))
- [ ] Hardware profile matches declared Node topology (standalone / multi-WAN / VRRP / CRS)
- [ ] CRS boards: INPUT/OUTPUT management only — no Bridge/VLAN/HW-offload writes from Controller

## Management plane

- [ ] API-SSL enabled; plain API (8728) disabled
- [ ] Certificate trust path ready (INTERNAL_CA or SPKI pin)
- [ ] Dedicated management account (default `full` group rejected)
- [ ] Management source allowlist / guard path planned (VIP-only where required)
- [ ] Scheduler available for onboarding/deploy watchdogs

## Topology readiness

- [ ] Declared Site → Node → Device inventory matches cabling/roles
- [ ] VRRP: all members reachable; no unacknowledged split-master
- [ ] Multi-WAN: dependency/probe expectations documented
- [ ] Packet-path classes reviewed (CPU/MIXED allowed; HW/INDETERMINATE block deploy)

## Controller side

- [ ] PostgreSQL backup taken before first manage
- [ ] Master-key provider is not `Development` outside Development
- [ ] Device registered via Desktop **Add router** or gRPC (`RegisterDevice` + `UpdateDeviceConnection`)
- [ ] For live RouterOS: `Enabled` / `WriteEnabled` set per [`pilot-runbook.md`](pilot-runbook.md)
- [ ] Desktop Operators understand Drift is detect-only (no auto-fix)

Normative codes: Onboarding Spec §58; Living Spec filters in [`../development/testing.md`](../development/testing.md).
