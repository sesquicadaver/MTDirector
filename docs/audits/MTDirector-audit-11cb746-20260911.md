# MTDirector — аудит коду, 11.09.2026

**Перевірений `main/HEAD`: `11cb746de60191e6eb83e52013f7f544306d5c9d`.** Значення отримано через `git ls-remote --symref` і звірено з чистим checkout. Воно збігається з commit попереднього GUI E2E, але перевірене заново.

**Висновок:** суттєві дефекти стосуються збереження правил, достовірності snapshots, analysis/approval, deployment/recovery та інтеграції GUI. Значна частина алгоритмів реалізована, але не під’єднана до робочого процесу. Зелене CI і CLOSED у документації не доводять працездатності GUI→Controller→CHR.

**Межі доказів:** нове читання оригінальних файлів, трасування production-викликів, перевірка тестового коду й GitHub CI-логів цього SHA. Локальні тести не запускались: `dotnet --info` завершився exit 127 — SDK відсутній. Доступ до живої GNS3/CHR не використовувався. Код, тести, конфігурація й лабораторія не змінювалися. Наведені сценарії відтворення виведені з коду, а не виконані на CHR.

Трасування почато з `Mfc.Controller/Program.cs` та `Mfc.Desktop/Program.cs → App.axaml.cs`. Корпус пошуку: 860 C#-файлів у `src`, включно з міграціями, і 559 у `tests`. Поглиблено перевірено GUI, policy/safety/compiler, snapshot/capture, RouterOS adapters, deployment/recovery, DI та jobs. Це не твердження про ручну перевірку кожного рядка або відсутність інших дефектів.

**Пріоритети:** P1 — високий: спотворення даних, недостовірні safety-висновки, помилки write/recovery або незавершені основні сценарії. P2 — середній: обмежені дефекти поведінки, ресурсів та інтеграції. Впевненість у зазначених властивостях коду — висока; конкретні наслідки на лабораторних CHR потребують відтворення.

## 01. P1 — Capture втрачає firewall-поля і ламає перевірку management guard

`ProjectOrderedFilter` серіалізує тільки ordinal, chain, action, protocol, src/dst-address, connection-state, disabled, comment. Зібрані `src-port`, `dst-port`, interfaces, interface/address lists, `jump-target` та інші matchers не потрапляють до canonical filter.

**Наслідки:** зміна відкинутих полів не змінює configuration hash і не відображається коректно в semantic diff. Якщо observations також незмінні, dedup поверне попередній snapshot. Safety query читає canonical filter, а ManagementPath вимагає dst-port для input guard і src-port для output guard. Отже, навіть правильні позначені guards на CHR після production Capture отримають блокери про відсутній порт.

**Виправлення:** повний підтримуваний семантичний набір firewall-полів у canonical representation. Перевіряти зміну лише порту/jump-target та правильний guard через production projection→analyzer. Нормативна опора: Canonical Snapshot Specification §17.3.

Докази: [src/Mfc.RouterOs/Snapshot/DiscoveryCanonicalProjector.cs:294](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Snapshot/DiscoveryCanonicalProjector.cs#L294), [src/Mfc.Application/Snapshots/CaptureSnapshotUseCase.cs:155](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Snapshots/CaptureSnapshotUseCase.cs#L155), [src/Mfc.Application/Policies/GetDevicePolicySafetyAnalysisUseCase.cs:99](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Policies/GetDevicePolicySafetyAnalysisUseCase.cs#L99), [src/Mfc.Domain/Policy/ManagementPathAnalysis.cs:519](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/ManagementPathAnalysis.cs#L519).

## 02. P1 — Невдале required-section читання може стати completed snapshot

RouterOS command errors повертаються як результати; discovery не перевіряє успішність усіх обов’язкових читань перед побудовою моделі. Стабільна помилка хешується як `unavailable` і може пройти stable-read comparison. Builder виставляє всім canonical sections `Status = 1`, після чого application викликає `PersistCompletedAsync`.

**Сценарій:** повторюваний `!trap` для обов’язкового firewall menu при успішних інших командах. Canonical-модель показує порожні правила замість недоступних даних. Raw payload помилку зберігає; неправильними є completed verdict і подальша інтерпретація.

**Виправлення:** required/optional classification та перенесення command failures у partial/failed capture; недопущення аналізу відсутніх facts як порожньої конфігурації. Read Adapter §38.2 прямо вимагає partial/failed.

Докази: [src/Mfc.RouterOs/Snapshot/RouterOsDiscoveryReader.cs:26](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Snapshot/RouterOsDiscoveryReader.cs#L26), [src/Mfc.RouterOs/Snapshot/ConfigurationFingerprintBuilder.cs:27](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Snapshot/ConfigurationFingerprintBuilder.cs#L27), [src/Mfc.RouterOs/Snapshot/StableReadCoordinator.cs:66](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Snapshot/StableReadCoordinator.cs#L66), [src/Mfc.RouterOs/Snapshot/SnapshotCaptureResultBuilder.cs:95](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Snapshot/SnapshotCaptureResultBuilder.cs#L95), [RouterOS Read Adapter Specification v0.1.md:2588](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/RouterOS%20Read%20Adapter%20Specification%20v0.1.md#L2588).

## 03. P1 — Update rule втрачає predicate та приховані параметри

Presentation row не переносить повний predicate. Вибір правила заповнює лише Family/Chain/Stage/Effect/Description. Update будує predicate з поточної форми; порожня форма дає `null`, який backend перетворює на необмежений `TrafficPredicate.Create()`. Клієнт примусово встановлює `Logging.Enabled=false` і `ExceptionEligible=false`.

**Сценарій:** завантажити Draft з обмеженим ACCEPT → вибрати правило → змінити тільки опис → Update. Адресні/сервісні/зональні/state-обмеження зникають або підміняються залишками іншого правила. Це підтверджена зміна policy data; негайного відкриття firewall на CHR не доведено.

**Виправлення:** round-trip повної структури rule, збереження всіх невідредагованих полів. Наявний VM-тест перевіряє ID/ordinal/effect/description, але не збереження predicate.

Докази: [src/Mfc.Desktop/Services/PolicyPanelService.cs:1154](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/Services/PolicyPanelService.cs#L1154), [src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs:373](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs#L373), [src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs:994](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs#L994), [src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs:1342](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs#L1342), [src/Mfc.Desktop/Services/GrpcPolicyServiceClient.cs:139](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/Services/GrpcPolicyServiceClient.cs#L139), [src/Mfc.Application/Policies/PolicyRuleFactory.cs:11](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Policies/PolicyRuleFactory.cs#L11), [src/Mfc.Application/Policies/PolicyRuleUseCases.cs:727](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Policies/PolicyRuleUseCases.cs#L727), [tests/Mfc.UnitTests/Desktop/PoliciesViewModelTests.cs:225](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/tests/Mfc.UnitTests/Desktop/PoliciesViewModelTests.cs#L225).

## 04. P1 — Validate/Record analysis не виконують повний аналіз і mandatory tests

Validate перевіряє content CAS, викликає `MarkValidated()` і зберігає state. GUI Record підставляє один logical/content hash у різні analysis/evidence/topology/impact/device/dependency slots та передає порожні test results. Backend записує клієнтські findings/tests/hashes. `IsPass()` та approval gate перевіряють передані тести, але не їхню повноту: порожній цикл проходить.

**Наслідок:** політика з тестом, який мав би FAIL, може отримати analysis run без виконання цього тесту. Окремий Analyze safety не формує докази для цього run. `EvidenceSignalsPresent=false` справді підвищує ризик до CRITICAL — це не LOW/self-approval bypass, але votes reviewers не замінюють mandatory checks.

**Виправлення:** server-side analysis use case зі збором facts, запуском наявних аналізаторів/тестів і перевіркою повного target/test set. Вимоги: Policy Model §63; Compiler Specification §4.

Докази: [src/Mfc.Application/Policies/ValidateRevisionUseCase.cs:99](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Policies/ValidateRevisionUseCase.cs#L99), [src/Mfc.Desktop/Services/PolicyPanelService.cs:811](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/Services/PolicyPanelService.cs#L811), [src/Mfc.Application/Policies/PolicyApprovalUseCases.cs:261](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Policies/PolicyApprovalUseCases.cs#L261), [src/Mfc.Domain/Policy/PolicyApprovalGate.cs:66](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/PolicyApprovalGate.cs#L66), [src/Mfc.Domain/Policy/PolicyAnalysisRun.cs:282](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/PolicyAnalysisRun.cs#L282).

## 05. P1 — Актуальність analysis fingerprint перевіряється проти значення клієнта

GUI повторно надсилає fingerprint зі збереженого run як поточний для Approve/Bind/Compile. Compile обчислює `analysisCurrent` порівнянням із client input, без обчислення fingerprint актуальних залежностей на Controller.

**Сценарій:** approved run → зміна guard/anchor safety-контексту → новий Capture за незмінних logical policy/capabilities → Compile зі старим fingerprint. Цей stale-check лишається true. Наявні logical/capability checks не доводять актуальності всього safety context.

**Виправлення:** Controller обчислює current fingerprint; клієнтський hash використовується тільки як CAS expectation.

Докази: [src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs:817](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs#L817), [src/Mfc.Application/Policies/CompileNodeFilterArtifactsUseCase.cs:203](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Policies/CompileNodeFilterArtifactsUseCase.cs#L203).

## 06. P1 — Semantic policy diff може приховувати зміни доступності трафіку

Три незалежні причини:

- Before та after rules нормалізуються з **after** address/service catalogs. Зміна object `/24 → /16` може дати packet-space `NO_EFFECTIVE_CHANGE`, хоча ObjectImpacts окремо покаже object change.
- Acceptance розраховується як union ACCEPT-предикатів без first-match семантики попередніх terminal DROP/REJECT. Перестановка/вимкнення deny може не змінити semantic classification.
- Diff не порівнює ChainContracts і передає `PolicyEvidenceSignals.None`. Заміна default disposition `DROP → RETURN_TO_UNMANAGED` за незмінних rules може дати `NO_EFFECTIVE_CHANGE`/risk NONE; Policy Model §60.2 вимагає CRITICAL.

**Виправлення:** окремі before/after catalogs, ordered terminal semantics, chain contracts; недоведений результат — INDETERMINATE.

Докази: [src/Mfc.Domain/Policy/PolicyRevisionDiffer.cs:94](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/PolicyRevisionDiffer.cs#L94), [src/Mfc.Domain/Policy/PolicyRevisionDiffer.cs:280](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/PolicyRevisionDiffer.cs#L280), [src/Mfc.Domain/Policy/PolicyRevisionDiffer.cs:332](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/PolicyRevisionDiffer.cs#L332), [src/Mfc.Application/Policies/PolicyReviewUseCases.cs:82](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Policies/PolicyReviewUseCases.cs#L82).

## 07. P1 — Background recovery може втрутитися в активний deployment

Recovery увімкнений за замовчуванням, сканує nonterminal operations кожні 15 с і не перевіряє, чи операцію покинуто, чи owner/lease ще живий. Start записує operation перед runtime; наступне збереження state виконується після runtime. Production-викликів створення deployment lock і write-ahead steps не знайдено.

**Наслідок при часовому перекритті:** recovery бачить durable Created, хоча deployment уже arming/activating. За all-old anchors він може прибрати живий watchdog і позначити operation failed/canceled; за new/mixed — почати controller rollback паралельно activation. Це статично підтверджений конкурентний шлях, а не спостережений збій поточної лабораторії.

**Виправлення:** єдине ownership/lease для write/recovery, recovery лише покинутих операцій, durable transitions/journal до зовнішнього ефекту. Unique nonterminal DB-index існує, але не захищає одну operation від одночасних runtime і recovery. Вимоги: Safe Deployment §§15–16, 49.

Докази: [src/Mfc.Controller/Jobs/OperationalJobsOptions.cs:14](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Jobs/OperationalJobsOptions.cs#L14), [src/Mfc.Controller/Jobs/OperationalJobTickPlanner.cs:27](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Jobs/OperationalJobTickPlanner.cs#L27), [src/Mfc.Application/Jobs/RecoverNonterminalOperationsJobUseCase.cs:77](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Jobs/RecoverNonterminalOperationsJobUseCase.cs#L77), [src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs:507](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs#L507), [src/Mfc.Domain/Deployment/DeploymentRecoveryDecision.cs:173](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Deployment/DeploymentRecoveryDecision.cs#L173), [src/Mfc.Application/Deployment/RecoverDeploymentUseCase.cs:362](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Deployment/RecoverDeploymentUseCase.cs#L362), [src/Mfc.Infrastructure/Persistence/Configurations/DeploymentOperationConfiguration.cs:30](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Infrastructure/Persistence/Configurations/DeploymentOperationConfiguration.cs#L30).

## 08. P1 — Watchdog використовує час Controller і незмінний TTL

Runtime передає Controller `nowUtc` як `routerClock`. Standalone executor один раз фіксує margin і використовує ті самі секунди для activation, verify, disarm. VRRP також передає повний RollbackTtl замість фактичного залишку.

**Тригери:** відмінний час/timezone CHR; тривалі staging/verification. Deadline може опинитися в неправильному моменті, а перевірка запасу часу — пройти після його фактичного вичерпання.

**Виправлення:** читати RouterOS date/time/timezone/uptime, фіксувати deadline і повторно обчислювати remaining TTL за монотонним часом. Це прямо вимагають Safe Deployment §§25–26.

Докази: [src/Mfc.RouterOs/Deployment/RouterOsDeploymentRuntime.cs:88](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Deployment/RouterOsDeploymentRuntime.cs#L88), [src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs:257](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs#L257), [src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs:285](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs#L285), [src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs:93](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs#L93), [Safe Deployment and Rollback Specification v0.1.md:982](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/Safe%20Deployment%20and%20Rollback%20Specification%20v0.1.md#L982).

## 09. P1 — Recovery відкидає результат watchdog cleanup

`DisarmAndCleanupWatchdogAsync` ігнорує structured result `CleanupWatchdogAsync`. Recovery після виклику записує cleanup timeline і може повернути успішний terminal outcome. RouterOS trap на disable/remove не обов’язково є exception.

**Наслідок:** успішне завершення не гарантує вимкнений watchdog. Невдалий remove вже disabled resource і невдалий disable різні; особливо критичний недоведений disable.

**Виправлення:** перевіряти результат і фактичний disabled state перед успішним завершенням recovery.

Докази: [src/Mfc.RouterOs/Deployment/RouterOsDeploymentDeviceSession.cs:201](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Deployment/RouterOsDeploymentDeviceSession.cs#L201), [src/Mfc.Application/Deployment/RecoverDeploymentUseCase.cs:362](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Deployment/RecoverDeploymentUseCase.cs#L362).

## 10. P1 — Перемикання ноди не інвалідує mutation context GUI

Zones зберігає Bindings/SelectedBinding, змінюючи тільки заголовок. **A → Refresh → select binding → B → Delete** видаляє binding A за його ID, хоча GUI показує B. RowVersion не захищає від неправильного owner context.

Onboarding/Deployment аналогічно зберігають PlanId/PlanHash/OperationId після зміни TargetHint. Start/Rollback надсилають старі ID. Фактичний запис на CHR залежить від backend gates, але неузгоджений адресат команди доведений.

**Виправлення:** owner NodeId/DeviceId у state, інвалідація при switch і відхилення запізнілих async responses; або явно закріплений і видимий context operation.

Докази: [src/Mfc.Desktop/ViewModels/ZonesViewModel.cs:258](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/ZonesViewModel.cs#L258), [src/Mfc.Desktop/ViewModels/ZonesViewModel.cs:438](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/ZonesViewModel.cs#L438), [src/Mfc.Desktop/Services/ZonePanelService.cs:207](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/Services/ZonePanelService.cs#L207), [src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs:351](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs#L351), [src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs:325](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs#L325).

## 11. P1 — Onboarding/Deployment GUI надсилає синтетичні payloads

Onboarding використовує DefaultFacts з вигаданими version/services/accounts/prefixes/device-mode та UUID-based hashes. Deployment використовує `deployment-desktop`, `old-art`, `new-art`, probe `192.0.2.1` і fabricated `ether1 → wan1 / CpuFirewall`. Policies Deploy завжди disabled; Compile лише показує результат, не передаючи sealed artifact до Deployment.

**Наслідок:** зв’язного GUI сценарію Compile→реальний plan→Start немає. Onboarding Validate не є перевіркою незалежно зібраних CHR prerequisites. Це production-заглушки незалежно від наявності TODO. Backend materializer перевіряє sealed artifact і може відхилити фіктивний plan — успішного deployment такого payload не доведено.

**Виправлення:** Controller формує facts/plans зі свіжого capture та конкретного compiled artifact; GUI працює з їхніми ID/версіями.

Докази: [src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs:340](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs#L340), [src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs:366](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs#L366), [src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs:267](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs#L267), [src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs:933](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs#L933), [src/Mfc.Application/Deployment/DeploymentArtifactMaterializer.cs:65](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Deployment/DeploymentArtifactMaterializer.cs#L65).

## 12. P1 — Production operator authorization незавершена

Поза дозволеним Development composition root використовує `DenyAllAuthorizationBoundary`. mTLS identity resolution існує, але штатний boundary operator→permissions не під’єднаний. System actor дозволений для jobs, зовнішнє пред’явлення цього actor resolver відхиляє.

**Наслідок:** валідний operator certificate не завершує production workflow. Це fail-closed блокування, не загальний auth bypass.

**Виправлення:** реальні operator permissions із deny-by-default; перевірка production host дозволеними і забороненими principal.

Докази: [src/Mfc.Controller/Program.cs:224](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Program.cs#L224), [src/Mfc.Controller/Authorization/DevelopmentAuthorizationBoundaries.cs:1](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Authorization/DevelopmentAuthorizationBoundaries.cs#L1), [src/Mfc.Controller/Grpc/GrpcRequestActorResolver.cs:51](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Grpc/GrpcRequestActorResolver.cs#L51).

## 13. P2 — Compose finding ламає GUI Record analysis

Compose переносить тільки SummaryLine. Record конвертує кожен finding у Severity `INFO`, тоді як домен дозволяє тільки BLOCKER/WARNING і повертає `Unknown finding severity 'INFO'`.

**Сценарій:** невикористаний address/service object → Compose warning → Record. Run не створюється, acknowledgment не доходить до робочого стану.

**Виправлення:** зберігати справжні code/severity/target/message та узгодити DTO з доменом.

Докази: [src/Mfc.Desktop/Services/PolicyPanelService.cs:794](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/Services/PolicyPanelService.cs#L794), [src/Mfc.Desktop/Services/PolicyPanelService.cs:819](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/Services/PolicyPanelService.cs#L819), [src/Mfc.Domain/Policy/PolicyAnalysisRun.cs:140](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/PolicyAnalysisRun.cs#L140).

## 14. P1 — ManagementPath перевіряє неповний guard contract

Окрім втрати портів у пункті 01, алгоритм має незалежні прогалини: input перевіряється на NEW без повного ESTABLISHED path; output — на ESTABLISHED без повного нормативного набору. Containment допускає `/0`, marker parser приймає довільний suffix замість строгого version/profile/family/direction/ordinal. Pre-guard DROP/REJECT оголошується блокуючим без перевірки перетину predicate з management packet.

**Контрприклади:** input NEW-only з наступним DROP established; guard `/0`; окремий UDP:53 DROP перед TCP:8729 guard. False-negative потребує повних вхідних facts; у поточному production Capture частину дефектів маскує пункт 01.

**Виправлення:** строгий guard contract, повний TCP flow/state proof, marker uniqueness та predicate intersection. Onboarding §§15–17 забороняють `/0` і визначають структуру guard.

Докази: [src/Mfc.Domain/Policy/ManagementPathAnalysis.cs:497](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/ManagementPathAnalysis.cs#L497), [src/Mfc.Domain/Policy/ManagementPathAnalysis.cs:543](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/ManagementPathAnalysis.cs#L543), [src/Mfc.Domain/Policy/ManagementPathAnalysis.cs:633](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/ManagementPathAnalysis.cs#L633), [src/Mfc.Domain/Policy/ActualFilterMarker.cs:43](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/ActualFilterMarker.cs#L43).

## 15. P1 — Фіктивні reachability/traffic facts у VRRP runtime

`IsReachableAsync` без I/O повертає true. Role snapshot також має Reachable=true і HasIndependentRoutedTraffic=false.

**Наслідок:** недоступний activated member потрапляє до controller rollback замість watchdog-retain; його невдалий rollback може перервати обробку інших members. Backup з незалежним routed traffic класифікується як StandbyOnly. Це production assumptions, не test doubles.

**Виправлення:** bounded fresh observations, явний unknown, вибір verification/rollback за фактичним станом.

Докази: [src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs:40](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs#L40), [src/Mfc.RouterOs/Deployment/RouterOsDeploymentDeviceSession.cs:237](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Deployment/RouterOsDeploymentDeviceSession.cs#L237), [src/Mfc.Application/Deployment/ExecuteVrrpDeploymentUseCase.cs:341](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Deployment/ExecuteVrrpDeploymentUseCase.cs#L341).

## 16. P2 — FASTTRACK_ACCEPT не під’єднаний до Controller compile context

Application створює DeviceFilterCompileRequest без FastTrackTopology. Effect compiler отримує null і для FASTTRACK_ACCEPT повертає FasttrackContextUnsupported.

**Наслідок:** навіть нормативно безпечний supported FastTrack rule не проходить цей application path. Алгоритм існує, бракує інтеграції facts.

**Виправлення:** per-device topology context, прив’язаний до актуального analysis run.

Докази: [src/Mfc.Application/Policies/CompileNodeFilterArtifactsUseCase.cs:330](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Policies/CompileNodeFilterArtifactsUseCase.cs#L330), [src/Mfc.Domain/Policy/DeviceFilterCompiler.cs:303](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/DeviceFilterCompiler.cs#L303), [src/Mfc.Domain/Policy/FilterMatcherEffectCompiler.cs:268](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Domain/Policy/FilterMatcherEffectCompiler.cs#L268).

## 17. P2 — Fresh verification sessions не звільняють API-SSL transport

Factory створює AuthenticatedRosConnection, але повертає тільки wrapper над connection.Session. Власник TCP/TLS не передається. DisposeAsync повернутого wrapper лише встановлює `_disposed=true`.

**Наслідок:** `await using` не закриває fresh connection/read loop; повторні verification/recovery накопичують ресурси до зовнішнього закриття/завершення процесу.

**Виправлення:** явне ownership і cascading disposal; перевірка close на success/error/cancel.

Докази: [src/Mfc.RouterOs/Deployment/RouterOsDeploymentDeviceSession.cs:342](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Deployment/RouterOsDeploymentDeviceSession.cs#L342), [src/Mfc.RouterOs/Deployment/RouterOsDeploymentSession.cs:388](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Deployment/RouterOsDeploymentSession.cs#L388).

## 18. P2 — Progress, operation recovery у GUI та connection status неповні

Onboarding/Deployment буферизують весь Watch, після завершення записують OperationId/progress. Якщо Start повернув новий ID, а Watch зірвався, ID не зберігається; Rollback бачить порожній/старий ID. Controller Start теж очікує workflow і лише потім публікує timeline, отже повноцінного live progress активної операції цей шлях не дає.

Connection service перевіряє health при Connect, але у Connected reconnect loop лише очікує. Після зупинки Controller GUI може залишитись Connected; transport retries gRPC не доводять правильного shell status.

**Виправлення:** негайне збереження operation ID, progress під час роботи, незалежне відновлення Watch і bounded health/transport-state updates. Документ про live progress DONE суперечить коду.

Докази: [src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs:173](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs#L173), [src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs:264](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs#L264), [src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs:158](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs#L158), [src/Mfc.Controller/Grpc/DeploymentGrpcService.cs:80](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Grpc/DeploymentGrpcService.cs#L80), [src/Mfc.Desktop/Services/ControllerConnectionService.cs:165](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Desktop/Services/ControllerConnectionService.cs#L165), [docs/development/desktop-ui-backend-alignment.md:105](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/docs/development/desktop-ui-backend-alignment.md#L105).

## 19. P2 — Watch не має application authorization; hubs не мають retention

WatchCapture, Onboarding.Watch, Deployment.Watch читають hub без ResolveActor/permission check, на відміну від інших RPC. Transport mTLS — окремий захист, але доступ до endpoint і operation ID дозволяють читати наявний progress без operator permission gate.

Singleton hubs зберігають усі operations/history без видалення завершених і мають unbounded subscriber channels. Пам’ять росте з історією процесу.

**Виправлення:** узгоджена read/watch authorization, operation ownership, обмежений replay/retention і керування повільними subscribers.

Докази: [src/Mfc.Controller/Grpc/SnapshotGrpcService.cs:233](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Grpc/SnapshotGrpcService.cs#L233), [src/Mfc.Controller/Grpc/DeploymentGrpcService.cs:110](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Grpc/DeploymentGrpcService.cs#L110), [src/Mfc.Controller/Grpc/OnboardingGrpcService.cs:147](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Grpc/OnboardingGrpcService.cs#L147), [src/Mfc.Controller/Grpc/CaptureProgressHub.cs:15](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Grpc/CaptureProgressHub.cs#L15), [src/Mfc.Controller/Grpc/DeploymentProgressHub.cs:15](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Grpc/DeploymentProgressHub.cs#L15).

## Від’єднані реалізації та код без production-викликів

Перевірено definitions/DI/callers у всьому `src`. Відсутній caller не означає, що алгоритм неправильний або його слід видалити: частина компонентів повинна завершувати заявлений workflow.

| Компонент | Фактичне під’єднання | Наслідок |
|---|---|---|
| `DeploymentLock.Acquire`, `AddLockAsync` | Визначення, EF, test callers; Start не викликає | Durable lease не створюється штатним deploy; P1, пункт 07 |
| `AddDeviceStateAsync`, `AddStepAsync`/`SaveStepAsync` | Persistence та тести; production orchestration не веде записи | Схема device states/journal є, write-ahead lifecycle відсутній; P1 |
| `DeploymentCommitSnapshot` | Створюється standalone executor, runtime не передає далі | Commit evidence лишається transient object; P2 |
| `PolicyEvidenceContextMapper.Analyze`, `PolicyEvidenceBlockerMapper.Analyze` | Реалізації/тести, production-викликів немає | Немає цілісного server-side analysis/test pipeline; пункт 04 |
| `PlanTransitionStatesUseCase.ValidateTransitions` | Test callers; activation використовує лише HashState | Перевірка evidence проміжних transition states не під’єднана |
| `VerifyMultiWanDeploymentUseCase` | Прямі test callers; runtime не викликає | Окремий multi-WAN verifier не входить до штатного deployment |
| Incident Deploy/Removal/Outcome use cases | DI; Incident RPC має тільки Ingest і Bind | Incident lifecycle недосяжний через штатні RPC; P2 |
| `ReconcileExpiredIncidentDenyOverlayBindingsJobUseCase` | DI та тести, scheduler/executor не запускає | Incident TTL reconciliation не працює автоматично; P2 |
| Watchdog residue discovery | `CleanupCandidatesAsync` production composition не встановлює | Planner створює empty cleanup work item; executor виходить без роботи; P2 |

Окремий латентний дефект incident deploy/removal: use cases компілюють artifact, але формують deployment із переданих command.DevicePlans/hashes, не використовуючи output compile. Оскільки ці use cases не мають production callers, це не доведений reachable GUI write bug.

Докази orchestration: [src/Mfc.Controller/Program.cs:280](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Program.cs#L280), [src/Mfc.Controller/Jobs/OperationalJobExecutor.cs:29](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Jobs/OperationalJobExecutor.cs#L29), [src/Mfc.Controller/Jobs/OperationalJobSchedulerHostedService.cs:139](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Jobs/OperationalJobSchedulerHostedService.cs#L139), [src/Mfc.Controller/Jobs/OperationalJobTickPlanner.cs:56](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Controller/Jobs/OperationalJobTickPlanner.cs#L56), [src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs:516](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs#L516), [src/Mfc.RouterOs/Deployment/RouterOsDeploymentRuntime.cs:85](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.RouterOs/Deployment/RouterOsDeploymentRuntime.cs#L85), [src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs:344](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs#L344).

## Тести, CI та достовірність показників

Перевірено [ci run 34540110649](https://github.com/sesquicadaver/MTDirector/actions/runs/34540110649) для зазначеного SHA та Linux job `103080636564`. Це наявний CI-ран цього commit, не новий локальний прогін.

| Перевірка | Підтверджений результат |
|---|---|
| Linux restore/format/build | success |
| Unit/architecture | 2824 passed; 0 failed; 0 skipped |
| PostgreSQL integration | 96 passed; 0 failed; 0 skipped |
| RouterOS project smoke | 10 passed; 0 failed; 0 skipped |
| Windows Desktop build | success |
| Локальний SDK | відсутній; тести не запускались |
| Жива CHR / GNS3 / GUI E2E / LAN-WAN traffic | цим аудитом не перевірялися |

`StandaloneChrLiveAcceptanceTests` без `MFC_CHR_STANDALONE_HOST` просто повертається. Тому counted PASS і Skipped=0 не означають live CHR verification. Решта RouterOS smoke переважно перевіряє manifest/fixtures/skeleton. Навіть live TLS test перевіряє наявність сертифіката з subject, а не повний production trust/RouterOS workflow.

`AntiStubScanner` шукає NotImplementedException, окремі TODO-patterns і xUnit Skip. Він не виявляє synthetic hashes/facts, безумовний true, disconnected DI чи порожні evidence results. Проходження gate не доводить відсутності заглушок.

Частина Living Spec тестів перевіряє кнопки/методи, текст документації або fake client/runtime. Вони корисні для своїх контрактів, але не доводять роботу композиції реальних компонентів. Особливо бракує тестів predicate round-trip, switch-node→mutation, required-read failure, production guard projection, concurrent active deploy/recovery та тривалого watchdog lifecycle.

Докази: [.github/workflows/ci.yml:40](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/.github/workflows/ci.yml#L40), [tests/Mfc.RouterOs.IntegrationTests/StandaloneChrLiveAcceptanceTests.cs:22](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/tests/Mfc.RouterOs.IntegrationTests/StandaloneChrLiveAcceptanceTests.cs#L22), [tests/Mfc.UnitTests/Documentation/AntiStubScanner.cs:1](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/tests/Mfc.UnitTests/Documentation/AntiStubScanner.cs#L1), [tests/Mfc.UnitTests/Desktop/DesktopOnboardingLivingSpecTests.cs:67](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/tests/Mfc.UnitTests/Desktop/DesktopOnboardingLivingSpecTests.cs#L67), [docs/release/readiness.md:16](https://github.com/sesquicadaver/MTDirector/blob/11cb746de60191e6eb83e52013f7f544306d5c9d/docs/release/readiness.md#L16).

## Що не оголошено новими дефектами

- `NotConfigured*` при вимкнених gates — навмисна fail-closed поведінка; production adapters існують.
- `AnchorOnlyDeploymentArtifactMaterializer` не зареєстрований як production materializer; фактичний adapter перевіряє sealed body/hash.
- Пробіли в zone key не оголошуються дефектом без нормативної заборони.
- Старі 3/11 detected, 0/3 correction, stale VRRP/Audit і cleanup-звіт Cursor цим аудитом не підтверджені.
- Management guard лишається передумовою, яку налаштовує адміністратор; виправлення Capture/analyzer не переносить guard до звичайної Policy GUI.
- Порожній capability hash, збереження старих policy error/safety results при switch та несинхронний lifecycle catalog повторно узгоджуються з поточним кодом. Вони не замінюють нових знахідок.

## Послідовність виправлень

1. Збереження повних rule fields і правильний owner context мутацій.
2. Canonical capture та required-section status: всі наступні висновки залежать від достовірних facts.
3. Server-side analysis/tests/fingerprint/approval/compile, semantic diff і guard contract.
4. Durable ownership/journal, координація recovery, фактичний watchdog time budget і cleanup verification.
5. Реальні GUI plans/artifact references та production operator permissions.
6. Потрібні FastTrack/multi-WAN/incident entry points; disposal/progress/connection status.
7. Перевірка через справжні composition roots, потім контрольований GUI E2E на CHR із незалежним traffic verdict.

**Відповідність специфікаціям:** FAIL на перелічених шляхах. **Інженерна якість:** матеріальні дефекти й неповна інтеграція компонентів. **Новий runtime/E2E verdict:** не встановлено — лабораторію цим аудитом не запускали.
