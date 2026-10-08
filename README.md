# MTDirector

MikroTik Firewall Controller — топологічно обізнаний контролер firewall-політик для RouterOS.

## Статус (v0.2.0)

| Область | Стан |
|---------|------|
| MVP issue-queue (M0–M6 + N1) | **CLOSED** — 109/109 issues (черга issues, не live E2E proof) |
| Post-MVP M7 issue-queue (M7.1–M7.4) | **CLOSED** — 27/27 issues (черга issues, не production lifecycle proof) |
| P2 Pilot RouterOS wiring | read path code **CLOSED** (P2-04…P2-06); write path **code rows CLOSED** (P2-07…P2-11) — **production-safe write path NOT PROVEN** ([audit `acd0759`](docs/audits/MTDirector-audit-acd0759-20260923.md) / [PLAN-62](docs/planning/plan-62-audit-remediation-acd0759.md)) |
| Live / production acceptance | **NOT SATISFIED** — live CHR/CRS OFF; scripted Living Specs ≠ production-safe RouterOS write proof |
| Release tag | [`v0.2.0`](https://github.com/sesquicadaver/MTDirector/releases/tag/v0.2.0) (2026-08-24) — baseline tag |
| Delivery queue (§3.C) | **§3.C NEXT = W7-431 (#1256)** — PLAN-63 seed → EVID-LIVE-01 (OWN-HB-01 DONE); wave B Layer C deferred |

`CLOSED` для MVP/M7/P2 означає завершення issue-queue / code-row (**AUDIT-STATUS-01**). Це не доводить безпечний end-to-end write path на RouterOS — див. [`plan-62-audit-remediation-acd0759.md`](docs/planning/plan-62-audit-remediation-acd0759.md) і [`docs/release/known-limitations.md`](docs/release/known-limitations.md).

Лінійна черга: [`ROADMAP.md`](ROADMAP.md) §3.C. Мапінг issues: [`ISSUES.md`](ISSUES.md).

Acceptance: [`docs/release/mvp-acceptance.md`](docs/release/mvp-acceptance.md). Readiness: [`docs/release/readiness.md`](docs/release/readiness.md). Known gaps: [`docs/release/known-limitations.md`](docs/release/known-limitations.md).

## Швидкий старт

1. [`docs/howto/build-and-run.md`](docs/howto/build-and-run.md) — збірка / запуск Linux і Windows
2. [`docs/development/local-environment.md`](docs/development/local-environment.md) — деталі Dev PostgreSQL
3. [`docs/development/connection-profiles.md`](docs/development/connection-profiles.md) — Desktop **Add router** або gRPC
4. [`docs/operations/pilot-runbook.md`](docs/operations/pilot-runbook.md) — lab read/write gates
5. Architecture: [`docs/architecture/overview.md`](docs/architecture/overview.md)

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
M0 → M1 → M2 → M3 → M5 → M4 → M6 → MVP CLOSED (issue-queue)
                 (+ N1 packet-path weave)
→ M7.1…M7.4 → v0.2.0
→ P2 read (P2-04…P2-06) + write code rows (P2-07…P2-11) CLOSED
  (production-safe write NOT PROVEN)
→ Desktop Add router + alignment W1–W4 / W2.1–W2.2 DONE
→ Continuous §3.C through PLAN-62 (audit acd0759 remediation) COMPLETE
→ PLAN-63 wave A (re-audit residuals) — §3.C NEXT = W7-431 (#1256)
```

## Стек

Desktop Avalonia → gRPC/mTLS → ASP.NET Core Controller → PostgreSQL → RouterOS API-SSL.

## Toolchain

- SDK: [`global.json`](global.json) (.NET 10, `allowPrerelease: false`)
- Packages: [`Directory.Packages.props`](Directory.Packages.props)
- Solution: [`MikroTikFirewallController.sln`](MikroTikFirewallController.sln)
- CI: [`.github/workflows/ci.yml`](.github/workflows/ci.yml)
