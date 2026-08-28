# Local development environment

Reproducible workstation setup (Linux recommended; Desktop UI also builds on Windows). Matches `global.json` SDK and the current `main` feature set (MVP + M7 + P2 gates + Desktop Add router).

**Крос-платформний HOWTO** (збірка / package / запуск Linux+Windows): [`../howto/build-and-run.md`](../howto/build-and-run.md).

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

Default host port is **`127.0.0.1:5432`** (`mfc-postgres-dev`). If the port is busy, remap in compose or override the connection string.

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

RouterOS adapters stay fail-closed until you set `Mfc:RouterOs:Enabled` / `WriteEnabled` — see [pilot-runbook.md](../operations/pilot-runbook.md).

## Run Desktop

```bash
dotnet run --project src/Mfc.Desktop
```

Endpoint comes from `src/Mfc.Desktop/appsettings.json` (`Desktop:ControllerEndpoint`).

1. **Connect** to Controller.
2. Open **Inventory** → **Add router** to create Site/Node/Device + connection profile ([connection-profiles.md](connection-profiles.md)).
3. Prefer selecting an existing Site/Node in the tree so pickers pre-fill.

**Lifecycle:** Desktop and Controller are separate OS processes. Closing Desktop does **not** stop Controller — terminate the Controller process (or free its listen port) explicitly.

## CHR lab (optional)

CHR images are **not** in Git. See [chr-lab.md](chr-lab.md) and `testlab/chr/README.md`.
