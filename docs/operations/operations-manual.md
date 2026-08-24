# Operations manual (v0.2.0)

Day-2 operator guide for MTDirector after **MVP CLOSED** and **M7 CLOSED** (`v0.2.0`). Packaging and acceptance: [`../release/`](../release/). Production RouterOS read path: **P2 pilot** queue in [`ROADMAP.md`](../../ROADMAP.md) §3.B5.

## Daily surfaces (Desktop)

Seven MVP modules (Contracts-only): Inventory, Node, Snapshots, Policies, Operations, Drift, Audit.

| Module | Operator job |
|--------|----------------|
| Inventory / Node | Register devices, inspect desired/committed/actual hashes + workflow status |
| Snapshots | Capture stable API-SSL snapshots; compare semantic diffs |
| Policies | Author / validate / approve / bind (no deploy from approval alone) |
| Operations | Onboarding + Deploy workflows (plan hash gated) |
| Drift | Read Critical drift; never auto-repair |
| Audit | Read-only hash-chained events |

## Safe change loop

1. Capture + review semantic diff.
2. Compose/analyze policy; resolve BLOCKERs.
3. Approve with SoD; bind desired artifact.
4. Onboard unmanaged Node (prerequisites + guards + watchdogs).
5. Deploy with production rollback watchdog; verify probes.
6. Confirm committed hashes; watch Drift job.

## Jobs / recovery

- Bounded in-process jobs: operation recovery priority over drift; no broker.
- Nonterminal operations after crash or DB restore go through recovery use cases — see [`recovery.md`](recovery.md).
- Never run experimental EF `Down()` migrations.

## Outages

1. Prefer Controller rollback / watchdog restore over manual RouterOS edits in managed namespaces.
2. If managed rules were edited out-of-band: expect Critical drift; remediate via new plan, not silent enforce.
3. Rotate credentials / master key if exposure is suspected.

## Related

- [`installation.md`](installation.md)
- [`prerequisite-checklist.md`](prerequisite-checklist.md)
- [`controller-configuration.md`](controller-configuration.md)
- [`../release/mvp-acceptance.md`](../release/mvp-acceptance.md)
- [`../release/known-limitations.md`](../release/known-limitations.md)
