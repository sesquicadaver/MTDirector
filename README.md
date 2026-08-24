# MTDirector

MikroTik Firewall Controller — топологічно обізнаний контролер firewall-політик для RouterOS.

## Статус (v0.2.0)

| Область | Стан |
|---------|------|
| MVP (M0–M6 + N1) | **CLOSED** — 109/109 issues |
| Post-MVP M7 (M7.1–M7.4) | **CLOSED** — 27/27 issues |
| P2 Pilot (RouterOS wiring) | **OPEN** — P2-04…P2-06 (#280–#282) |
| Release | [`v0.2.0`](https://github.com/sesquicadaver/MTDirector/releases/tag/v0.2.0) (2026-08-24) |

**NEXT:** [P2-04 / #280](https://github.com/sesquicadaver/MTDirector/issues/280) — production `RouterOsReadPort` (live API-SSL probe).

Лінійна черга: [`ROADMAP.md`](ROADMAP.md) §3.B5. Мапінг issues: [`ISSUES.md`](ISSUES.md).

Acceptance: [`docs/release/mvp-acceptance.md`](docs/release/mvp-acceptance.md). Known gaps: [`docs/release/known-limitations.md`](docs/release/known-limitations.md).

## Швидкий старт

1. [`docs/development/local-environment.md`](docs/development/local-environment.md)
2. [`docs/development/connection-profiles.md`](docs/development/connection-profiles.md)
3. [`docs/development/testing.md`](docs/development/testing.md)
4. Architecture: [`docs/architecture/overview.md`](docs/architecture/overview.md)

Повний індекс документації: [`docs/README.md`](docs/README.md).

## Ключові документи

| Документ | Призначення |
|----------|-------------|
| [`ROADMAP.md`](ROADMAP.md) | Єдиний порядок атомарних задач |
| [`ISSUES.md`](ISSUES.md) | Logical ID → GitHub |
| [`docs/specs/README.md`](docs/specs/README.md) | Нормативні ТЗ та Issue Sets (корінь репо) |
| [`docs/development/testing.md`](docs/development/testing.md) | Living Spec (ТЗ → модуль → тести) |
| [`TOR-1.md`](TOR-1.md) / [`TOR-2.md`](TOR-2.md) | Архітектура / scope lock |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) / [`SECURITY.md`](SECURITY.md) / [`CHANGELOG.md`](CHANGELOG.md) | Процес / безпека / історія |

## Критичний шлях

```text
M0 → M1 → M2 → M3 → M5 → M4 → M6 → MVP CLOSED
                 (+ N1 packet-path weave)
→ M7.1…M7.4 → v0.2.0
→ P2 (production RouterOS read path) → pilot
```

## Стек

Desktop Avalonia → gRPC/mTLS → ASP.NET Core Controller → PostgreSQL → RouterOS API-SSL.

## Toolchain

- SDK: [`global.json`](global.json) (.NET 10, `allowPrerelease: false`)
- Packages: [`Directory.Packages.props`](Directory.Packages.props)
- Solution: [`MikroTikFirewallController.sln`](MikroTikFirewallController.sln)
- CI: [`.github/workflows/ci.yml`](.github/workflows/ci.yml)
