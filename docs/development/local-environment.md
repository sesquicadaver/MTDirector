# Local development environment

Reproducible M0 bootstrap setup on a developer workstation (Linux recommended; Desktop UI also builds on Windows).

## Prerequisites

| Tool | Notes |
|------|-------|
| .NET SDK | Version pinned in [`global.json`](../../global.json) (`allowPrerelease: false`) |
| Docker | Required for PostgreSQL (compose or Testcontainers) |
| Git | Squash-only workflow — see [git-workflow.md](git-workflow.md) |

```bash
export PATH="$HOME/.dotnet:$PATH"   # if SDK installed via install script
dotnet --info   # confirm SDK matches global.json
```

## Clone and restore

```bash
git clone https://github.com/sesquicadaver/MTDirector.git
cd MTDirector
dotnet tool restore
dotnet restore MikroTikFirewallController.sln --locked-mode
```

## PostgreSQL (Controller)

```bash
docker compose -f testlab/postgres/compose.yml up -d
```

Default Development connection string is in `src/Mfc.Controller/appsettings.Development.json`. Override with:

```bash
export MFC__Database__ConnectionString='Host=127.0.0.1;Port=5432;Database=mfc;Username=mfc;Password=...'
```

Apply schema (does not start gRPC):

```bash
dotnet run --project src/Mfc.Controller -- --environment Development --migrate-only
```

Details: [database-migrations.md](database-migrations.md).

## Run Controller (Development)

```bash
dotnet run --project src/Mfc.Controller -- --environment Development
```

Listens on loopback HTTP only when `AllowInsecureLoopback` + Development are set. Production binds require TLS.

## Run Desktop

```bash
dotnet run --project src/Mfc.Desktop
```

Endpoint comes from `src/Mfc.Desktop/appsettings.json` (`Desktop:ControllerEndpoint`).

## CHR lab (optional)

CHR images are **not** in Git. See [chr-lab.md](chr-lab.md) and `testlab/chr/README.md`.
