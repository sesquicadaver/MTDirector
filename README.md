# MTDirector

MikroTik Firewall Controller — топологічно обізнаний контролер firewall-політик для RouterOS.

## Статус (v0.2.0)

| Область | Стан |
|---------|------|
| MVP (M0–M6 + N1) | **CLOSED** — 109/109 issues |
| Post-MVP M7 (M7.1–M7.4) | **CLOSED** — 27/27 issues |
| P2 Pilot (RouterOS wiring) | read path **CLOSED** (P2-04…P2-06); write path **CLOSED** (P2-07…P2-11) |
| Release | [`v0.2.0`](https://github.com/sesquicadaver/MTDirector/releases/tag/v0.2.0) (2026-08-24) |

**Queue (§3.C):** **§3.C NEXT = W7-343 (#1092)** (PLAN-31 inventory DONE [#931](https://github.com/sesquicadaver/MTDirector/issues/931); seed [#932](https://github.com/sesquicadaver/MTDirector/issues/932) **DONE**; LIST-01 [#934](https://github.com/sesquicadaver/MTDirector/issues/934) **DONE**; RO seed [#935](https://github.com/sesquicadaver/MTDirector/issues/935) **DONE**; RO implement [#939](https://github.com/sesquicadaver/MTDirector/issues/939) **DONE**; COMPLETE seed [#940](https://github.com/sesquicadaver/MTDirector/issues/940) **DONE**; PLAN-32 inventory [#943](https://github.com/sesquicadaver/MTDirector/issues/943) **DONE**; seed [#944](https://github.com/sesquicadaver/MTDirector/issues/944) **DONE**; implement [#946](https://github.com/sesquicadaver/MTDirector/issues/946) **DONE**; seed [#947](https://github.com/sesquicadaver/MTDirector/issues/947) **DONE**; implement [#951](https://github.com/sesquicadaver/MTDirector/issues/951) **DONE**; COMPLETE [#952](https://github.com/sesquicadaver/MTDirector/issues/952) **DONE**; inventory [#955](**NEXT**)(https://github.com/sesquicadaver/MTDirector/issues/955); seed [#956](https://github.com/sesquicadaver/MTDirector/issues/956) **OPEN**; PLAN-29 COMPLETE; W7-255 DONE; PLAN-30 inventory DONE [#919](https://github.com/sesquicadaver/MTDirector/issues/919); seed [#920](https://github.com/sesquicadaver/MTDirector/issues/920) **DONE**; [#922](https://github.com/sesquicadaver/MTDirector/issues/922) WATCH-OWN-01 **DONE**/[#923](https://github.com/sesquicadaver/MTDirector/issues/923) seed → WATCH-BP-01 **DONE**; [#928](https://github.com/sesquicadaver/MTDirector/issues/928) PLAN-30 COMPLETE **DONE**; PLAN-28 COMPLETE; W7-249 DONE; PLAN-29 inventory DONE [#907](https://github.com/sesquicadaver/MTDirector/issues/907); seed [#908](https://github.com/sesquicadaver/MTDirector/issues/908) DONE; DESK-CONN-HEALTH-01 [#910](https://github.com/sesquicadaver/MTDirector/issues/910) DONE; seed [#911](https://github.com/sesquicadaver/MTDirector/issues/911) DONE; implement [#915](https://github.com/sesquicadaver/MTDirector/issues/915) RECONNECT-01 DONE; PLAN-27 COMPLETE; W7-243 DONE; W7-242 DONE; W7-241 DONE; W7-240 DONE; W7-239 DONE; W7-238 DONE) — W7-113 ([#626](https://github.com/sesquicadaver/MTDirector/issues/626)) **DONE**; W7-114 ([#628](https://github.com/sesquicadaver/MTDirector/issues/628)) **DONE**; W7-112 ([#625](https://github.com/sesquicadaver/MTDirector/issues/625)) **DONE**; W7-111 ([#620](https://github.com/sesquicadaver/MTDirector/issues/620)) **DONE**; W7-110 ([#617](https://github.com/sesquicadaver/MTDirector/issues/617)) **DONE**; PLAN-10 **COMPLETE**; W7-109 ([#616](https://github.com/sesquicadaver/MTDirector/issues/616)) **DONE**; W7-108 ([#614](https://github.com/sesquicadaver/MTDirector/issues/614)) **DONE**; W7-107 ([#612](https://github.com/sesquicadaver/MTDirector/issues/612)) **DONE**; W7-106 ([#611](https://github.com/sesquicadaver/MTDirector/issues/611)) **DONE**; W7-105 ([#608](https://github.com/sesquicadaver/MTDirector/issues/608)) **DONE**; PLAN-10 inventory; PLAN-09 **COMPLETE**; PLAN-08 **COMPLETE**; PLAN-07 **COMPLETE**; PLAN-06 **COMPLETE**; PLAN-05 **COMPLETE**. [`docs/planning/continuous-queue-plan.md`](docs/planning/continuous-queue-plan.md). Pilot (parallel): [`docs/operations/pilot-runbook.md`](docs/operations/pilot-runbook.md) (`Enabled` / `WriteEnabled`).  
**Alignment P0–P2 (2026-08-30…31):** Desktop W1–W4 / W2.1–W2.2 **DONE**. Add router ([#309](https://github.com/sesquicadaver/MTDirector/pull/309)) remains the inventory registration path.

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
M0 → M1 → M2 → M3 → M5 → M4 → M6 → MVP CLOSED
                 (+ N1 packet-path weave)
→ M7.1…M7.4 → v0.2.0
→ P2 read path (P2-04…P2-06) → **CLOSED**
→ TRACKER-01 (#289) → **DONE**
→ PLAN-01 (#290) → **DONE**
→ P2-07 (#293) → **DONE**
→ P2-08 (#294) → **DONE**
→ P2-09 (#295) → **DONE**
→ P2-10 (#296) → **DONE**
→ P2-11 (#297) → **DONE** — **P2 write-path CLOSED**
→ Desktop Add router UX ([#309](https://github.com/sesquicadaver/MTDirector/pull/309)) → **DONE**
→ Alignment W1–W4 / W2.1–W2.2 → **DONE**
→ PLAN-02 (#339) → continuous §3.C
→ CONT-01 (#340) → **DONE**
→ CONT-02 (#341) → **DONE**
→ W5-01 (#342) → **DONE**
→ W5-02 (#343) → **DONE**
→ W5-03 (#344) → **DONE**
→ **§3.C NEXT = W7-343 (#1092)** · PLAN-36 COMPLETE; NEXT PLAN-37 inventory (OPS-HOST-ENV); OPS-HOST-BUNDLE DONE
```

## Стек

Desktop Avalonia → gRPC/mTLS → ASP.NET Core Controller → PostgreSQL → RouterOS API-SSL.

## Toolchain

- SDK: [`global.json`](global.json) (.NET 10, `allowPrerelease: false`)
- Packages: [`Directory.Packages.props`](Directory.Packages.props)
- Solution: [`MikroTikFirewallController.sln`](MikroTikFirewallController.sln)
- CI: [`.github/workflows/ci.yml`](.github/workflows/ci.yml)
