# Security Policy

## Supported versions

Until the first production release tag, security fixes apply to `main` only.

## Reporting a vulnerability

Do **not** open a public GitHub issue for security-sensitive reports.

Email the repository owner (`sesquicadaver`) with:

- description and impact;
- reproduction steps or proof-of-concept (non-destructive);
- affected component (Controller, Desktop, RouterOS adapter, CI, docs);
- whether credentials or customer data are involved.

You should receive an acknowledgement within a reasonable time. We will coordinate disclosure after a fix is available.

## Hard requirements

- Secrets stay outside the repository and outside the Desktop client.
- RouterOS credentials are encrypted at rest in Controller storage only (once implemented).
- No `SkipCertificateValidation` for production API-SSL.
- No generic RouterOS command executor in production assemblies.
- Audit trail must not contain passwords, tokens, or private keys.
- Production RouterOS write paths require CODEOWNERS review.
- Reserved `Mfc:OperationalJobs:SystemActor` is for **in-process** operational jobs only. gRPC clients must not assert it via `x-mfc-actor` (SEC-01 / `GrpcRequestActorResolver`).

## Safe Harbor

Good-faith research that avoids service disruption and data exfiltration is welcome. Do not access data that is not yours.
