# ADR 0002: RouterOS API-SSL transport

- **Status:** Accepted
- **Date:** 2026-08-06
- **Deciders:** Architecture (TOR-1 / RouterOS Read Adapter Specification)

## Context

Controller must read (and later carefully write) RouterOS configuration. Options include SSH/scripting, REST, SNMP, or the native RouterOS API over TLS (API-SSL). Arbitrary command execution and plaintext protocols are unacceptable for a managed enterprise controller.

## Decision

Use a **typed RouterOS API-SSL client** owned by `Mfc.RouterOs`:

- TLS is mandatory for device management traffic.
- No production assembly may expose arbitrary RouterOS command execution APIs.
- Credentials never leave Controller-side secret handling; Desktop never stores RouterOS passwords.

## Consequences

- **Positive:** Structured, testable protocol surface; aligns with TOR-1; supports CHR lab over TLS.
- **Negative:** Custom client work instead of shelling out to SSH; CHR fixtures must exercise API-SSL paths.
- **Follow-up:** Adapter issues in M1 implement the typed client against the Adapter Specification.
