# Operations manual (v0.2.0)

Day-2 operator guide for MTDirector after **MVP CLOSED**, **M7 CLOSED** (`v0.2.0`), and **P2 read/write pilot CLOSED**. Packaging and acceptance: [`../release/`](../release/). RouterOS gates: [`pilot-runbook.md`](pilot-runbook.md). Queue: [`ROADMAP.md`](../../ROADMAP.md) §3 (**empty**).

## Daily surfaces (Desktop)

Seven MVP modules (Contracts-only): Inventory, Node, Snapshots, Policies, Operations, Drift, Audit.

| Module | Operator job |
|--------|----------------|
| Inventory | **Add router** wizard (Site→Node→Device + API-SSL profile); inspect tree + hashes + workflow status |
| Node | Topology / zones / onboarding readiness / routing assurance viewers |
| Snapshots | Capture stable API-SSL snapshots; compare semantic diffs |
| Policies | Author / validate / approve / bind (no deploy from approval alone) |
| Operations | Onboarding + Deploy workflows (plan hash gated; needs `WriteEnabled` for live RouterOS) |
| Drift | Read Critical drift; never auto-repair |
| Audit | Read-only hash-chained events |

## Safe change loop

1. Register device via Inventory **Add router** (or gRPC) + connection profile — [`../development/connection-profiles.md`](../development/connection-profiles.md).
2. Capture + review semantic diff.
3. Compose/analyze policy; resolve BLOCKERs.
4. Approve with SoD; bind desired artifact.
5. Onboard unmanaged Node (prerequisites + guards + watchdogs) with write gate enabled in lab.
6. Deploy with production rollback watchdog; verify probes.
7. Confirm committed hashes; watch Drift job.

## Jobs / recovery

- Bounded in-process jobs: operation recovery priority over drift; no broker.
- Nonterminal operations after crash or DB restore go through recovery use cases — see [`recovery.md`](recovery.md).
- Never run experimental EF `Down()` migrations.
- Desktop ≠ Controller lifecycle: stopping Desktop leaves Controller listening until you stop it.

## Outages

1. Prefer Controller rollback / watchdog restore over manual RouterOS edits in managed namespaces.
2. If managed rules were edited out-of-band: expect Critical drift; remediate via new plan, not silent enforce.
3. Rotate credentials / master key if exposure is suspected.

## Related

- [`installation.md`](installation.md)
- [`pilot-runbook.md`](pilot-runbook.md)
- [`prerequisite-checklist.md`](prerequisite-checklist.md)
- [`controller-configuration.md`](controller-configuration.md)
- [`../release/mvp-acceptance.md`](../release/mvp-acceptance.md)
- [`../release/known-limitations.md`](../release/known-limitations.md)
- [`../release/readiness.md`](../release/readiness.md)
