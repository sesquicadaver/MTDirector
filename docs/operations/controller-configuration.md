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
| `Security:MasterKeyProvider` | Named master-key provider (`Development` forbidden outside Development) |
| `Authentication:AllowDevelopmentAuthentication` | Dev-only; loopback bind required |
| `Database:ConnectionString` | PostgreSQL only |

## Examples

```bash
export MFC__Database__ConnectionString='Host=127.0.0.1;Port=5432;Database=mfc;Username=mfc;Password=...'
export MFC__Security__MasterKeyProvider=Development
export ASPNETCORE_ENVIRONMENT=Development
```

Never commit production connection strings or master keys. Connection strings are redacted in Controller JSON logs.
