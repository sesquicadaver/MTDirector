# Database migrations (M0-07)

PostgreSQL is the only supported production database. SQLite is forbidden.

## Local PostgreSQL

```bash
docker compose -f testlab/postgres/compose.yml up -d
```

Default development connection (also in `appsettings.Development.json`):

```text
Host=127.0.0.1;Port=5432;Database=mfc;Username=mfc;Password=mfc_dev_only_change_me
```

Override with `MFC__Database__ConnectionString` for non-default setups. Never commit production passwords.

## Apply migrations

Controller does **not** migrate on normal startup.

```bash
dotnet run --project src/Mfc.Controller -- --environment Development --migrate-only
```

After success the process exits without starting gRPC.

## Startup schema guard

If mandatory migrations are pending, Controller fails to start with a clear error. Fix by running `--migrate-only`.

## Creating a new migration

```bash
dotnet tool restore
dotnet ef migrations add <Name> \
  --project src/Mfc.Infrastructure \
  --startup-project src/Mfc.Controller \
  --output-dir Persistence/Migrations
```

Commit the migration with the code change. Integration tests must cover empty-database apply and idempotent re-check.
