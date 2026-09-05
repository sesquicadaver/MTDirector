# Controller configuration

Configuration sources (highest wins last):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. Environment variables with prefix `MFC__`
4. OS/service secret provider (later)
5. Command-line overrides (`--Mfc:...`)

## Required sections (`Mfc`)

| Key | Purpose |
|-----|---------|
| `Grpc:ListenAddress` | Kestrel URL (`https://` required outside Dev insecure loopback) |
| `Grpc:ShutdownTimeoutSeconds` | Graceful shutdown budget (1–600) |
| `Grpc:AllowInsecureLoopback` | Development-only HTTP on loopback |
| `Grpc:ClientCertificateMode` | W7-03: `NoCertificate` (default), `AllowCertificate`, or `RequireCertificate` (HTTPS only; Kestrel `ConfigureHttpsDefaults`) |
| `Security:RequireTls` | Reject non-TLS production binds |
| `Security:MasterKeyProvider` | Named master-key provider (`Development` or `OsKeyStore`; `Development` forbidden outside Development) |
| `Security:MasterKeyBase64` (env `MFC__Security__MasterKeyBase64`) | Required for `OsKeyStore`: base64 of a 32-byte master key (never stored in PostgreSQL) |
| `Security:TrustedCa:ProfilesDirectory` | Absolute path; `{dir}/{CaProfileRef}/*.{pem,crt,cer,der}` for INTERNAL_CA (SEC-04). Empty → fail-closed at materialize |
| `Security:TrustedCa:RevocationMode` | `Online` (default), `Offline`, or `NoCheck` for INTERNAL_CA custom-chain builds |
| `Security:TrustedCa:ClientCaProfileRef` | W7-04: profile under ProfilesDirectory for inbound mTLS client trust; required when ClientCertificateMode is Allow/Require |
| `Authentication:AllowDevelopmentAuthentication` | Dev-only; loopback bind required |
| `Authentication:AllowMetadataActor` | Dev-only (W7-02); documents lab metadata actor path; **forbidden outside Development**. Production binds actor to TLS/auth principal (`GrpcRequestActorResolver`) |
| `Database:ConnectionString` | PostgreSQL only |

## RouterOS production ports (P2 pilot)

Controller registers **fail-closed** stubs by default (`Mfc:RouterOs:Enabled=false`, `Mfc:RouterOs:WriteEnabled=false`).

### Read path (`RouterOs:Enabled`)

Set **`Enabled=true`** for production read/capture adapters (read-only pilot).

| Port | Default (CI / Development) | Production (`RouterOs:Enabled=true`) |
|------|----------------------------|--------------------------------------|
| `IRouterOsReadPort` | `ProbeOnlyRouterOsReadPort` | `RouterOsReadPort` |
| `ISnapshotCapturePort` | `NotConfiguredSnapshotCapturePort` | `RouterOsSnapshotCapturePort` |

### Write path (`RouterOs:WriteEnabled`)

Set **`WriteEnabled=true`** for production onboarding / deployment / watchdog-residue adapters (P2-10). Independent of `Enabled`.

| Port | Default (CI / Development) | Production (`RouterOs:WriteEnabled=true`) |
|------|----------------------------|-------------------------------------------|
| `IOnboardingRuntime` | `NotConfiguredOnboardingRuntime` | `RouterOsOnboardingRuntime` |
| `IDeploymentRuntime` | `NotConfiguredDeploymentRuntime` | `RouterOsDeploymentRuntime` |
| `IWatchdogResidueCleanupPort` | `NotConfiguredWatchdogResidueCleanupPort` | `RouterOsWatchdogResidueCleanupPort` |

Registration: `AddMfcRouterOs(IConfiguration)` in `Program.cs` (or explicit `AddRouterOsProductionServices()` / `AddRouterOsWriteServices()` for tests). `Program.cs` keeps `TryAdd` NotConfigured write stubs as fail-closed fallback when `WriteEnabled=false`.

| Key | Purpose |
|-----|---------|
| `RouterOs:Enabled` | `false` by default; when `true`, registers production read/capture services |
| `RouterOs:WriteEnabled` | `false` by default; when `true`, registers production write-path services (P2-10) |
| `RouterOs:ProbeTimeoutSeconds` | Reserved bounded API-SSL probe timeout (1–600; default 30); connect timeout still comes from the device connection profile |

With both flags `false` (default), inventory probe/capture and write runtimes remain fail-closed — CI behaviour unchanged.

Pilot checklist: [`pilot-runbook.md`](pilot-runbook.md).

## Desktop mTLS client certificate (W7-03 / W7-05)

When Controller HTTPS uses `AllowCertificate` / `RequireCertificate`, Desktop can present a lab PFX:

| Key | Purpose |
|-----|---------|
| `Desktop:ClientCertificatePath` | Absolute path to client PFX (empty = no client cert) |
| `Desktop:ClientCertificatePassword` | Optional PFX password |
| `Desktop:Actor` | Used as `x-mfc-actor` only when no client cert is configured; with a cert, actor is the certificate CN (W7-05) |

When Connected, the Desktop shell status line shows the same resolved actor (`Connected · actor: …`) via `DesktopConnectionStatusText` (W7-08).

When `ClientCertificateMode` is `AllowCertificate` / `RequireCertificate`, Controller also installs `MtlsClientCertificatePrincipalMiddleware` so an accepted client certificate becomes authenticated `HttpContext.User` (CN as Name) for actor resolution (W7-06). Actor binding then prefers that User over connection cert CN / gRPC peer identity (W7-07; `GrpcRequestActorResolver`). Successful maps log CN + truncated thumbprint only (W7-10; no PEM) plus `TraceIdentifier` (W7-13).

## Examples

```bash
export MFC__Database__ConnectionString='Host=127.0.0.1;Port=5432;Database=mfc;Username=mfc;Password=...'
export MFC__Security__MasterKeyProvider=Development
export MFC__Security__TrustedCa__ProfilesDirectory=/var/lib/mfc/trusted-ca
export MFC__Security__TrustedCa__RevocationMode=Online
export ASPNETCORE_ENVIRONMENT=Development
```

Layout example: `/var/lib/mfc/trusted-ca/lab-ca/root.pem`. INTERNAL_CA profiles without files fail closed. Private CAs must publish CRL/OCSP for `Online`/`Offline`, or use `SPKI_PIN` / explicit `NoCheck` only in controlled labs.

Never commit production connection strings or master keys. Connection strings are redacted in Controller JSON logs.
