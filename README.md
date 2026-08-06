# MTDirector

MikroTik Firewall Controller — топологічно обізнаний контролер firewall-політик для RouterOS.

## Статус

M0 bootstrap complete for governance, toolchain, solution, health host, Desktop shell, PostgreSQL, CI, CHR skeleton, and initial ADRs. Next: M1 vertical slice — [`ROADMAP.md`](ROADMAP.md) / [`ISSUES.md`](ISSUES.md).

Див. також [`CONTRIBUTING.md`](CONTRIBUTING.md), [`SECURITY.md`](SECURITY.md), [`CHANGELOG.md`](CHANGELOG.md).

## Швидкий старт

1. [`docs/development/local-environment.md`](docs/development/local-environment.md)
2. [`docs/development/testing.md`](docs/development/testing.md)
3. Architecture decisions: [`docs/architecture/overview.md`](docs/architecture/overview.md)

## Документи

| Документ | Призначення |
|----------|-------------|
| [`ROADMAP.md`](ROADMAP.md) | **Єдиний порядок атомарних задач** (M0→M6 + N1 + M7) |
| [`ISSUES.md`](ISSUES.md) | Мапінг логічних ID → GitHub issues |
| [`docs/architecture/overview.md`](docs/architecture/overview.md) | Огляд + ADR index |
| [Issues](https://github.com/sesquicadaver/MTDirector/issues) | Трекер реалізації |
| [`TOR-1.md`](TOR-1.md) | Базове архітектурне рішення |
| [`TOR-2.md`](TOR-2.md) | Scope MVP / поза MVP |

Нормативні MVP/Issue Set специфікації — у корені репозиторію (не дублюються тут).

## Критичний шлях

```text
M0 → M1 → M2 → M3 → M5 → M4 → M6 → MVP
                 (+ N1 packet-path weave)
```

## Стек

Desktop Avalonia → gRPC/mTLS → ASP.NET Core Controller → PostgreSQL → RouterOS API-SSL.

## Toolchain

- SDK: pinned in [`global.json`](global.json) (`.NET 10`, `allowPrerelease: false`)
- Packages: Central Package Management — [`Directory.Packages.props`](Directory.Packages.props)
- Build defaults: [`Directory.Build.props`](Directory.Build.props)
- Solution: [`MikroTikFirewallController.sln`](MikroTikFirewallController.sln)
- CI: [`.github/workflows/ci.yml`](.github/workflows/ci.yml)
