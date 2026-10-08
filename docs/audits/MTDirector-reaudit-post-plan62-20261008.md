# MTDirector — повторний аудит після PLAN-62

**Дата:** 08.10.2026.  
**База коду:** `origin/main @ f6eb6fabac965a6f2740f6a0942c96ab9b70f1f8` (PLAN62-DONE-01 / W7-427).  
**Нормативний попередній аудит:** [`MTDirector-audit-acd0759-20260923.md`](MTDirector-audit-acd0759-20260923.md) (`acd0759`, вердикт **Fail**).  
**Ремедіація:** [`docs/planning/plan-62-audit-remediation-acd0759.md`](../planning/plan-62-audit-remediation-acd0759.md) — **COMPLETE**; §3.C **NEXT = none**.

## Висновок

Порівняно з `acd0759`, **усі 14 findings F01–F14 мають код/документацію remediation на main**.  
Загальний вердикт щодо **production-safe live write path** лишається **Fail / NOT PROVEN**: Layer C (live CHR / physical CRS) **OFF / NOT SATISFIED**; issue-queue CLOSED ≠ live proof.

| Категорія | Результат |
|---|---|
| Код-дефекти F01–F12 (як у `acd0759`) | **REMEDIATED** у production paths + Living Specs |
| F13 M7 lifecycle | **PARTIAL** — routing/incident wired; **OpenEndpointPresence** без production observation caller (W7-423 residual) |
| F14 honesty / SBOM gates | **REMEDIATED** як honesty + fail-closed real SBOM; live acceptance **не** SATISFIED |
| Готовність виробничого firewall control | **NOT PROVEN** (чесно задокументовано) |

Цей звіт **не** відкриває PLAN-63 і **не** засіває §3.C. Залишки нижче — для операторського TOR, якщо потрібен наступний транш.

## Межі й метод

- Джерела: tracked tree на `f6eb6fab`, порівняння з формулюваннями F01–F14 у `acd0759`, PLAN-62 Living Specs, `docs/release/known-limitations.md`, README / mvp-acceptance / readiness.
- Метод: трасування entry → Application → RouterOs/Infrastructure → тести; grep на маркери remediation (`SealedTransitionEvidence`, `DeploymentCommitPersistence`, `OnboardingOwnership`, `IManagedDriftLiveReadPort`, `PolicyBoundLayerLoader`, `PolicyServerOwnedAnalysis`, `ChannelDeploymentStartWorkChannel`, тощо).
- **НЕ виконано в цьому проході:** повний `dotnet test` suite, live CHR write, physical CRS, зовнішній CI billing-status як доказ PASS.
- Зовнішні лабораторні прогони GNS3 / OCR walk **не** зараховуються як Layer C acceptance.

## Матриця F01–F14

| ID | Було (`acd0759`) | Зараз | PLAN-62 | Confidence |
|----|------------------|-------|---------|------------|
| **F01** | recovery vs active onboarding | **REMEDIATED** | AUDIT-OWN-01 W7-399 | 0.95 |
| **F02** | `AllSafeEvidence` у sealed plan | **REMEDIATED** | AUDIT-EVID-01 W7-403 | 0.95 |
| **F03** | commit/journal не персистяться | **REMEDIATED** | AUDIT-COMMIT-01 W7-401 | 0.93 |
| **F04** | automatic rollback слабший | **REMEDIATED** | AUDIT-RB-01 W7-405 | 0.92 |
| **F05** | watchdog = Controller clock | **REMEDIATED** | AUDIT-CLK-01 W7-407 | 0.93 |
| **F06** | client-owned analysis / FP echo | **REMEDIATED** | AUDIT-AN-03 W7-415 | 0.90 |
| **F07** | compose = latest Approved | **REMEDIATED** | AUDIT-BIND-01 W7-417 | 0.95 |
| **F08** | facility/routing/UTF-8/obs | **REMEDIATED** | AUDIT-CAP-03 W7-411 | 0.92 |
| **F09** | capture attempt identity | **REMEDIATED** | AUDIT-CAP-04 W7-413 | 0.93 |
| **F10** | Start = довгий unary | **REMEDIATED** | AUDIT-RPC-01 W7-409 | 0.94 |
| **F11** | GUI onboarding stub / stale safety | **REMEDIATED** | AUDIT-GUI-02 W7-419 | 0.94 |
| **F12** | drift без live RouterOS read | **REMEDIATED** | AUDIT-DRIFT-01 W7-421 | 0.94 |
| **F13** | M7 без production lifecycle | **PARTIAL** | AUDIT-M7-01 W7-423 | 0.90 |
| **F14** | false CLOSED / soft SBOM | **REMEDIATED*** | AUDIT-ACC-01 W7-425 + AUDIT-SBOM-01 W7-397 | 0.95 |

\*F14: honesty + behavioral acceptance gates + real-mode SBOM fail-closed — **закриті**. Live Layer C лишається **NOT SATISFIED** за дизайном acceptance layers.

---

### F01 — REMEDIATED

`RecoverNonterminalOperationsJobUseCase.RecoverOnboardingAsync` читає `OnboardingLock` і пропускає recovery, якщо lease живий (`OnboardingOwnership.IsAbandonedForRecovery` → `RecoverySkippedLockHeld`). Start набуває durable lock (`OnboardingWorkflowUseCases` / `OnboardingOwnership.AcquireForStart`). Аналог для deployment уже був і збережений.

### F02 — REMEDIATED

`SealedDeploymentPlanBuilder` викликає `SealedTransitionEvidence.Prove(...)`; production builder **не** викликає `AllSafeEvidence(` (лише тести/хелпери). Living Spec: `AuditEvid01RealSafetyEvidenceW7403LivingSpecTests`.

### F03 — REMEDIATED

Після успішного Execute `DeploymentCommitPersistence.PersistAsync` пише device state, activation journal steps, commit steps і оновлює `DeviceHashState.lastCommittedArtifactHash`. Виклик з `ContinueAcceptedAsync` / workflow path.

### F04 — REMEDIATED

Strict rollback path: third/manual target → `RecoveryRequired`; disarm/old verification через unified coordinator (`RecoverDeploymentUseCase` / `ExecuteDeploymentRollbackUseCase`); automatic path більше не ігнорує failed disarm як успішний RolledBack.

### F05 — REMEDIATED

`ExecuteOnboardingBootstrapUseCase` читає `ReadRouterClockAsync` на Device і веде `WatchdogTimeBudget` з `remainingTtl` на arm/disarm (не Controller `now` як router clock).

### F06 — REMEDIATED

`PolicyServerOwnedAnalysis.Build` — авторитетний outcome/risk/evidence; client PASS не зберігається як PASS. Approve/Bind/Compile перераховують fingerprint (`FrozenRunFingerprint = null` + live calculator з повним vector при `NodeId`).

**Residual (не відкриває F06):** Desktop ще може слати placeholder context hashes — сервер їх не трактує як PASS.

### F07 — REMEDIATED

`PolicyBoundLayerLoader.LoadBoundLayerAsync` бере `binding.DesiredRevisionId`; Compose/Compile використовують loader. Тест `A1LoadsBoundRevisionNotLatestApproved`.

### F08 — REMEDIATED

Facility KnownProperties (NAT/RAW/Mangle), routing src/dst, dynamic filter observation sequence, strict UTF-8 / duplicate fail у read mapper.

### F09 — REMEDIATED

Idempotency bound to device; identical hash → нова attempt identity; node capture не abort на першій помилці + time-set fit.

**Residual:** DB unique ще `(RequestedBy, IdempotencyKey)` без `TargetId` — app-layer закриває cross-device; schema не розширена.

### F10 — REMEDIATED

`StartDeploymentUseCase`: durable accept + idempotency, enqueue у `ChannelDeploymentStartWorkChannel`, швидке повернення `operation_id`; ефекти в `ContinueAcceptedAsync` / `DeploymentStartHostedService`. Progress hub публікує Accept, не підміняє terminal фазами Continue.

### F11 — REMEDIATED

`OnboardingViewModel`: Validate з `[]` facts → Controller capture-readiness; CreatePlan з empty devices → last-capture plan (більше не stub throw). Policies: clear stale safety + generation guard на зміні inventory.

### F12 — REMEDIATED

`PollManagedDriftJobUseCase` завжди викликає `IManagedDriftLiveReadPort` перед detect; fail/diverge ≠ NoDrift; production `RouterOsManagedDriftLiveReadPort`; без адаптера — fail-closed `NotConfigured*`.

### F13 — PARTIAL

| Підпункт | Статус |
|----------|--------|
| Routing assurance з capture | REMEDIATED (`IRoutingAssuranceCaptureProjectionPort`) |
| Presence з capture / observations | **STILL OPEN (deferred)** — `OpenEndpointPresenceUseCase` лише DI + harness |
| Bind → persist assessment | REMEDIATED |
| Incident TTL tick | REMEDIATED (ExpiredException tick + incident reconcile) |
| ReportIncidentDeploymentOutcome з deploy | REMEDIATED |
| DevicePlan ↔ sealed compile | REMEDIATED (gate `EnsureMatch`) |

Зафіксовано в `known-limitations.md` W7-423.

### F14 — REMEDIATED (honesty); live **NOT SATISFIED**

- README / readiness / mvp-acceptance: **production-safe write NOT PROVEN**; Layer C **OFF**.
- Acceptance Living Specs вимагають поведінкові `ExecuteAsync` gates, не file-presence = green.
- SBOM real mode fail-closed (порожній inventory / відсутній SDK → exit ≠ 0). Dry-run може мати empty components (W7-397 residual).

---

## Що змінилось відносно вердикту `acd0759`

| Твердження аудиту 23.09 | Стан 08.10 |
|---|---|
| Write/recovery блокери F01–F05, F10 відкриті | Закриті в коді на main |
| Analysis/capture F06–F09 відкриті | Закриті в коді |
| Operator cycle F11–F12 відкриті | Закриті в коді |
| M7 «компоненти без lifecycle» | Майже wired; **presence** deferred |
| False MVP/M7 CLOSED / soft SBOM | Honesty + fail-closed real SBOM |
| Production-safe write не доведений | **Досі не доведений** (Layer C OFF) |

## Відкриті / задокументовані залишки (не §3.C черга)

1. **OpenEndpointPresence** — немає production gRPC/observation caller з capture (F13 residual / W7-423).
2. **Layer C** — live CHR / physical CRS **NOT SATISFIED** (W7-425).
3. **Capture idempotency schema** — unique без `TargetId` (F09 residual).
4. **SBOM dry-run / optional CycloneDX / cleartext `.asc` без GPG** (W7-397 / W7-113).
5. **§3.C NEXT = none** — наступний транш лише за явним операторським TOR/аудитом (ROADMAP §6 / PLAN62-DONE-01).

## Підсумок перевірки

| Перевірка | Результат |
|---|---|
| Commit base | `f6eb6fab` = origin/main; PLAN-62 ancestor confirmed |
| F01–F14 code/docs trace | Виконано (див. матрицю) |
| .NET full suite / live CHR | **NOT RUN** у цьому проході |
| SBOM real-mode (повторне відтворення) | Не повторювалось; код/скрипт fail-closed перевірені статично vs W7-397 |
| Засів PLAN-63 | **Не виконано** (потрібен оператор) |

**Підсумок однією фразою:** дефекти `acd0759` F01–F12 закриті на main; F13 частково (presence); F14 чесний; виробнича безпека write path досі **не доведена** live-прийманням.
