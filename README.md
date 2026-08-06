# MTDirector

MikroTik Firewall Controller — топологічно обізнаний контролер firewall-політик для RouterOS.

## Статус

Документальний етап + governance bootstrap. Наступна реалізація — за [`ROADMAP.md`](ROADMAP.md) / [`ISSUES.md`](ISSUES.md).

Див. також [`CONTRIBUTING.md`](CONTRIBUTING.md), [`SECURITY.md`](SECURITY.md), [`CHANGELOG.md`](CHANGELOG.md).

## Документи

| Документ | Призначення |
|----------|-------------|
| [`ROADMAP.md`](ROADMAP.md) | **Єдиний порядок атомарних задач** (M0→M6 + N1 + M7) |
| [`ISSUES.md`](ISSUES.md) | Мапінг логічних ID → GitHub issues |
| [Issues](https://github.com/sesquicadaver/MTDirector/issues) | Трекер реалізації |
| [`TOR-1.md`](TOR-1.md) | Базове архітектурне рішення |
| [`TOR-2.md`](TOR-2.md) | Scope MVP / поза MVP |
| [`MVP Technical Specification v0.1.md`](MVP%20Technical%20Specification%20v0.1.md) | Повне ТЗ MVP |
| [`MVP End-to-End Workflow and Acceptance Specification v0.1.md`](MVP%20End-to-End%20Workflow%20and%20Acceptance%20Specification%20v0.1.md) | M6 DoD, overrides |
| [`Initial Issue Set v0.1.md`](Initial%20Issue%20Set%20v0.1.md) | Деталі M0–M1 issues |
| [`M2–M6 Implementation Issue Set v0.1.md`](M2–M6%20Implementation%20Issue%20Set%20v0.1.md) | Деталі M2–M6 issues |

Профільні специфікації (Adapter, Canonical, Policy, Compiler, Onboarding, Safe Deployment) — у корені репозиторію.

## Критичний шлях

```text
M0 → M1 → M2 → M3 → M5 → M4 → M6 → MVP
                 (+ N1 packet-path weave)
```

## Стек (ціль)

Desktop Avalonia → gRPC/mTLS → ASP.NET Core Controller → PostgreSQL → RouterOS API-SSL.

## Toolchain

- SDK: pinned in [`global.json`](global.json) (`.NET 10`, `allowPrerelease: false`)
- Packages: Central Package Management — [`Directory.Packages.props`](Directory.Packages.props)
- Build defaults: [`Directory.Build.props`](Directory.Build.props) (`Nullable`, `TreatWarningsAsErrors`, `Deterministic`, lock files)
- Solution: [`MikroTikFirewallController.sln`](MikroTikFirewallController.sln)
)
