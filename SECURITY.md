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
- **W7-02:** outside Development, gRPC actor is bound to the authenticated principal (client certificate CN / claims / gRPC peer identity). Free choice of `x-mfc-actor` without a principal is rejected. When a principal is present, metadata must match it. `Mfc:Authentication:AllowMetadataActor` is Development-only and forbidden in Production.
- **W7-03:** `Mfc:Grpc:ClientCertificateMode` (`NoCertificate` / `AllowCertificate` / `RequireCertificate`) configures Kestrel HTTPS client-certificate negotiation so Production can obtain a principal. Desktop may present a PFX via `Desktop:ClientCertificatePath` (+ optional password).
- **W7-04:** when Allow/Require is set, inbound client certificates are validated against `Mfc:Security:TrustedCa` (`ClientCaProfileRef` + `ProfilesDirectory` + `RevocationMode`) via CustomRootTrust — not «any cert TLS accepts». Missing profile material fails closed at startup.
- **W7-05:** Desktop `x-mfc-actor` is derived from the client certificate CN when `Desktop:ClientCertificatePath` is set (`DesktopGrpcActorResolver`); free-form `Desktop:Actor` remains the fallback without a cert.
- WriteEnabled production DI loads staging drafts from `IFilterArtifactStore` (`FilterArtifactStoreDeploymentArtifactMaterializer`); observed managed `resource_hash` is measured from live RouterOS state (SEC-02), not echoed from the plan.
- Audit `EventHash` chains predecessor **bytes** (not length) plus event id; appends use Serializable + `pg_advisory_xact_lock` and a unique index on `PreviousEventHash` (SEC-03 / `AuditEventHashing`).

## Safe Harbor

Good-faith research that avoids service disruption and data exfiltration is welcome. Do not access data that is not yours.
