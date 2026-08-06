# ADR 0001: Modular monolith

- **Status:** Accepted
- **Date:** 2026-08-06
- **Deciders:** Architecture (TOR-1 / Bootstrap Plan)

## Context

MTDirector must deliver a Desktop client, Controller, PostgreSQL persistence, and a typed RouterOS adapter with strict assembly boundaries. Splitting into microservices early would force distributed transactions, extra ops surface, and unclear ownership before the domain model stabilizes.

## Decision

Ship a **modular monolith**:

- One Controller host process (`Mfc.Controller`) composing Application, Infrastructure, RouterOs, and Contracts.
- One Desktop process (`Mfc.Desktop`) talking only over gRPC contracts.
- Assembly dependency rules enforced by architecture tests (Domain has no infrastructure; Desktop has no Domain/Application/Infrastructure/RouterOs).

Forbidden without a new ADR: microservice split, MediatR-as-architecture, shared “Utils” assemblies that dissolve boundaries.

## Consequences

- **Positive:** Single deployable for MVP; transactional workflows stay in-process; architecture tests catch boundary violations early.
- **Negative:** Careful module discipline is required; a future split needs an explicit ADR and contract extraction.
- **Follow-up:** Keep CI architecture tests mandatory on every PR.
