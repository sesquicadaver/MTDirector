# CI pipelines (M0-08)

Workflow: [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml)

## Triggers

- Pull requests
- Pushes to `main`

## Jobs

| Job | Runner | Purpose |
|-----|--------|---------|
| Linux validate | `ubuntu-latest` | locked restore, format, Release build, unit/architecture tests + coverage, PostgreSQL integration (Testcontainers), vulnerability scan, clean tree |
| Windows Desktop build | `windows-latest` | Desktop Release build |

Actions are pinned by full commit SHA. Default permissions: `contents: read`. GitHub-hosted runners only (no privileged self-hosted for untrusted PRs). Artifacts retain 3–7 days.

## Local parity

```bash
dotnet restore --locked-mode
dotnet format MikroTikFirewallController.sln --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
dotnet list package --vulnerable --include-transitive
```

Coverage thresholds for bootstrap (Domain/Application): see `scripts/ci/verify-coverage-thresholds.py` and Repository Bootstrap Plan §13.3.
