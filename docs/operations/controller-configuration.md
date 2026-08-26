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
| `Security:RequireTls` | Reject non-TLS production binds |
| `Security:MasterKeyProvider` | Named master-key provider (`Development` or `OsKeyStore`; `Development` forbidden outside Development) |
| `Security:MasterKeyBase64` (env `MFC__Security__MasterKeyBase64`) | Required for `OsKeyStore`: base64 of a 32-byte master key (never stored in PostgreSQL) |
| `Authentication:AllowDevelopmentAuthentication` | Dev-only; loopback bind required |
| `Database:ConnectionString` | PostgreSQL only |

## RouterOS production ports (P2 pilot)

Controller registers **fail-closed** stubs by default (`Mfc:RouterOs:Enabled=false`). Set **`Enabled=true`** for production read/capture adapters (read-only pilot).

| Port | Default (CI / Development) | Production (`RouterOs:Enabled=true`) |
|------|----------------------------|--------------------------------------|
| `IRouterOsReadPort` | `ProbeOnlyRouterOsReadPort` | `RouterOsReadPort` |
| `ISnapshotCapturePort` | `NotConfiguredSnapshotCapturePort` | `RouterOsSnapshotCapturePort` |

Registration: `AddMfcRouterOs(IConfiguration)` in `Program.cs` (or explicit `AddRouterOsProductionServices()` for tests).

| Key | Purpose |
|-----|---------|
| `RouterOs:Enabled` | `false` by default; when `true`, registers production read/capture services |
| `RouterOs:ProbeTimeoutSeconds` | Reserved bounded API-SSL probe timeout (1–600; default 30); connect timeout still comes from the device connection profile |

With `RouterOs:Enabled=false` (default), `ValidateDeviceConnection` and `StartCapture` remain fail-closed on not-configured ports — CI behaviour unchanged.

Pilot checklist: [`pilot-runbook.md`](pilot-runbook.md).

## Examples

```bash
export MFC__Database__ConnectionString='Host=127.0.0.1;Port=5432;Database=mfc;Username=mfc;Password=...'
export MFC__Security__MasterKeyProvider=Development
export ASPNETCORE_ENVIRONMENT=Development
```

Never commit production connection strings or master keys. Connection strings are redacted in Controller JSON logs.
