# ADR 0003: Node deployment atomicity

- **Status:** Accepted
- **Date:** 2026-08-06
- **Deciders:** Architecture (TOR-1 / Safe Deployment Specification)

## Context

Firewall policy changes on a logical node (single router or VRRP group) must not leave members in divergent, partially applied states. Partial success is worse than a controlled failure with rollback.

## Decision

Treat **node deployment as an atomic unit**:

- A node deployment reaches a terminal success only when all required members accept the planned change under the rollout policy.
- Failure triggers the documented rollback / residual-risk path — not silent partial apply.
- Durable deployment state and audit trail live in PostgreSQL (see ADR 0004).

## Consequences

- **Positive:** Clear operator semantics; matches Safe Deployment and E2E acceptance rules.
- **Negative:** VRRP and multi-WAN topologies need careful orchestration and CHR coverage.
- **Follow-up:** M4/M5 issues implement the state machine; no auto-deploy without an explicit later ADR.
