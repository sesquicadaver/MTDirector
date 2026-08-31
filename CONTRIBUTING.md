# Contributing to MTDirector

MikroTik Firewall Controller (namespace `Mfc`) — monorepo, trunk-based workflow on `main`.

## Before you start

1. Pick the next open row from [`ROADMAP.md`](ROADMAP.md) §3 (**NEXT**). Product work stays **one linear track**. Lab/GNS3/CHR/`WriteEnabled` phases run **in parallel** and are **not** predecessors of §3. Closing a wave with empty NEXT is forbidden — seed the next tranche in the same cycle ([PLAN-02](https://github.com/sesquicadaver/MTDirector/issues/339) / [`docs/planning/continuous-queue-plan.md`](docs/planning/continuous-queue-plan.md)). Do not invent a second product track.
2. One issue → one short-lived branch → one PR.
3. Do not expand scope without a new issue.
4. No stubs, `NotImplementedException`, or disabled tests in production code.

## Branch naming

```text
feat/<issue-id>-short-slug
fix/<issue-id>-short-slug
chore/<issue-id>-short-slug
build/<issue-id>-short-slug
test/<issue-id>-short-slug
docs/<issue-id>-short-slug
```

Example: `chore/m0-01-repository-governance`

## Commits

- Conventional Commits (`feat`, `fix`, `chore`, `build`, `test`, `docs`, `refactor`).
- PR title matches Conventional Commits and links the issue.

## Pull requests

- Target `main` only.
- Squash merge only (linear history).
- Fill the PR template completely.
- CI must be green; conversations resolved.
- High-risk changes (RouterOS write, deployment, secrets, authz, migrations, CI/release) need at least two reviews when a second reviewer is available.

> **Note:** Automated branch protection on private repos requires GitHub Pro (or a public repo). Until then, follow the process rules in [`docs/development/git-workflow.md`](docs/development/git-workflow.md).

## Local checks

```bash
dotnet restore --locked-mode
dotnet build -c Release
dotnet test -c Release
```

PostgreSQL for persistence tests is started via Testcontainers (Docker required). Local Controllers use [`testlab/postgres/compose.yml`](testlab/postgres/compose.yml) — see [`docs/development/database-migrations.md`](docs/development/database-migrations.md).

CI details: [`docs/development/ci.md`](docs/development/ci.md). Pull requests must keep the `ci` workflow green.

CHR lab: [`docs/development/chr-lab.md`](docs/development/chr-lab.md) / [`testlab/chr/README.md`](testlab/chr/README.md).

Architecture ADRs: [`docs/architecture/overview.md`](docs/architecture/overview.md).

## Security

- Never commit secrets, certificates, CHR images, or production configs.
- Desktop must not store RouterOS passwords or talk to devices directly.
- Report vulnerabilities per [`SECURITY.md`](SECURITY.md).

## Architecture constraints

See [`Repository Bootstrap Plan v0.1.md`](Repository%20Bootstrap%20Plan%20v0.1.md) and ADRs in [`docs/architecture/adr/`](docs/architecture/adr/). Forbidden without ADR: microservices, MediatR, AutoMapper, generic Utils/Helpers assemblies, direct Desktop→RouterOS access.
