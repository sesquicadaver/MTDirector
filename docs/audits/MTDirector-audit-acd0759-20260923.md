# MTDirector — аудит поточного репозиторію

**Дата:** 23.09.2026. **Версія:** `main @ acd0759e85414a83460c4cab971db2b0b58b30cd`.

**Висновок:** у проєкті є значна робоча архітектурна основа, але оголошення `MVP CLOSED`, `M7 CLOSED` і завершеного write path не відповідає наскрізній реалізації. Найсуттєвіші проблеми — достовірність safety evidence, узгодженість фактичного й збереженого стану, конкурентність recovery та незавершені зв’язки між уже наявними компонентами. За перевіреними критеріями специфікацій результат — **Fail**; готовність до виробничого керування firewall не доведена.

## Межі й метод

Джерела — виключно файли цього commit: нормативні специфікації, код, тести, workflows і скрипти. Попередні зовнішні звіти, перекази Cursor, скриншоти й результати лабораторних прогонів не використовувалися. GitHub API та `git fetch` використано лише для встановлення актуального commit. Статус зовнішніх CI-запусків до висновку не включено.

У tracked tree: 1880 файлів, із них 881 C# у `src`, 764 C# у `tests`, 127 Markdown/MDC. Це інвентаризація, а не твердження про построкову перевірку всіх файлів. Детально перевірено основні production paths, наведені нижче, та відповідні вимоги й тести. Старі planning-документи не трактувалися як докази працездатності.

Трасування починалося з:

- Controller: `Program.Main → BuildHost → registrations → mapped gRPC services → Application → Infrastructure/RouterOs` — [Program.cs:58](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Controller/Program.cs#L58).
- Desktop: `Program.Main → App.OnFrameworkInitializationCompleted → clients/services/view models` — [App.axaml.cs:23](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/App.axaml.cs#L23).
- Production read/write adapters реально підключаються через `AddMfcRouterOs` за відповідних `Enabled`/`WriteEnabled` — [RouterOsServiceCollectionExtensions.cs:30](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/DependencyInjection/RouterOsServiceCollectionExtensions.cs#L30).

За E2E specification §2 пізніша профільна специфікація має пріоритет. Тому відсутність campaigns, routing/NAT writes, automatic drift repair або автоматичного створення management guard **не є дефектом**. Guard налаштовує адміністратор; Controller повинен його перевіряти. [MVP End-to-End Workflow and Acceptance Specification v0.1.md:66](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/MVP%20End-to-End%20Workflow%20and%20Acceptance%20Specification%20v0.1.md#L66)

Усі findings нижче мають високу впевненість як висновки з коду. Наслідки на живому RouterOS не відтворювалися. Окремо позначено єдине виконане відтворення — release-скрипт.

## Головні findings

### F01. HIGH — recovery може втрутитися в активний onboarding

**Вимога:** Onboarding §§52–53: один writer на Device, durable lock із owner/heartbeat/expiry.

`StartOnboardingUseCase` зберігає операцію `Created`, потім очікує runtime. Наступний стан записується лише після виконання. Незалежний recovery job вибирає незавершені onboarding operations та викликає recovery без перевірки owner/lease. Recovery увімкнений за замовчуванням, інтервал — 15 секунд.

Джерела: [OnboardingWorkflowUseCases.cs:517](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Onboarding/OnboardingWorkflowUseCases.cs#L517), [RecoverNonterminalOperationsJobUseCase.cs:90](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Jobs/RecoverNonterminalOperationsJobUseCase.cs#L90), [RecoverNonterminalOperationsJobUseCase.cs:190](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Jobs/RecoverNonterminalOperationsJobUseCase.cs#L190), [OperationalJobsOptions.cs:17](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Controller/Jobs/OperationalJobsOptions.cs#L17).

`RecoverOnboardingUseCase` може передати звичайні staged/enabled bootstrap resources у rollback. Rollback допускає перехід із persisted `Created` та вимикає/видаляє anchors і bootstrap rules. [RecoverOnboardingUseCase.cs:53](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Onboarding/RecoverOnboardingUseCase.cs#L53), [RollbackOnboardingBootstrapUseCase.cs:107](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Onboarding/RollbackOnboardingBootstrapUseCase.cs#L107)

**Тригер:** виконання onboarding перетинається з recovery tick. Операція не обов’язково має тривати понад 15 або 30 секунд. Наслідок залежить від чергування: два writers працюють із тими самими ресурсами, фактичний і збережений стан можуть розійтися. Для deployment перевірка чинного lock є; для onboarding аналогічного захисту немає.

**Напрям виправлення:** спільне durable writer exclusion для onboarding/deploy/recovery; recovery лише після доведеного завершення lease. Перевірка: утримати Start перед ефектом і виконати recovery scan; активну операцію не можна відкочувати.

### F02. HIGH — deployment plan містить синтетичні safety evidence; preflight їх не замінює

**Вимога:** Safe Deployment §§9–12, §29: реальні preconditions, old state, докази безпеки проміжних old/new комбінацій.

Production `SealedDeploymentPlanBuilder`:

- викликає `AllSafeEvidence`, що встановлює `isSafe=true` для всіх transition states;
- підставляє нульовий compatibility hash;
- використовує один hash від configuration/capability як guard і anchor contexts;
- формує `probes: []`;
- за відсутності old body підставляє bootstrap targets, навіть коли old hash узято з іншого committed artifact.

[SealedDeploymentPlanBuilder.cs:51](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Deployment/SealedDeploymentPlanBuilder.cs#L51), [TransitionStateValidator.cs:214](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Domain/Deployment/TransitionStateValidator.cs#L214)

Наступна перевірка не компенсує це. Standalone `RecheckPreconditions` викликає pure gate: Node, plan hash/expiry, незавершені deployment operations, відомий drift, packet-path facts. Він не читає і не зіставляє актуальні version/configuration/capability/guard/old artifact. VRRP `PrecheckAsync` читає managed state й відкидає результат. `NO_CHANGES` визначається лише рівністю old/new hash у плані.

[StandaloneDeploymentPolicy.cs:14](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Domain/Deployment/StandaloneDeploymentPolicy.cs#L14), [DeploymentOperationGate.cs:22](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Domain/Deployment/DeploymentOperationGate.cs#L22), [RouterOsVrrpMemberDeploymentRuntime.cs:47](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs#L47)

**Наслідок:** запечатаний hash плану засвідчує незмінність його полів, але не достовірність safety evidence. Зміни guard/configuration або пошкоджені rollback resources можуть не зупинити staging.

**Напрям:** обчислювати evidence Controller-ом із реальних даних; перевіряти кожну sealed precondition перед ефектами. Unknown/missing evidence має блокувати план. Bootstrap fallback дозволений лише для доведеного bootstrap state.

### F03. HIGH — commit і write-ahead journal не завершені

**Вимога:** Safe Deployment §§16,45; Onboarding §§43,54: durable intent перед mutation, результат після read-back, повний atomic commit.

Standalone coordinator повертає `DeploymentCommitSnapshot`, але `RouterOsDeploymentRuntime` відкидає його. Фінальна transaction зберігає operation, idempotency та audit; нові committed hashes, artifact references, postdeployment snapshots і per-device state не записуються.

[ExecuteStandaloneDeploymentUseCase.cs:347](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs#L347), [RouterOsDeploymentRuntime.cs:106](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Deployment/RouterOsDeploymentRuntime.cs#L106), [DeploymentWorkflowUseCases.cs:623](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs#L623)

Це має конкретного наступного споживача: наступний CreatePlan читає `LastCommittedArtifactHash`. Після успішного deploy ця база не оновлюється; old artifact для наступного плану/drift може залишитися старим або bootstrap. [CreateDeploymentPlanFromSealedArtifactsUseCase.cs:174](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Deployment/CreateDeploymentPlanFromSealedArtifactsUseCase.cs#L174)

Журнал ефектів також неповний. Start записує лише початковий `Precheck` intent на Device. `ActivateAnchorsUseCase` додає intent у список у пам’яті перед записом на RouterOS. Пошук production callers не знайшов викликів `SaveStepAsync`, `AddDeviceStateAsync`/`SaveDeviceStateAsync` або створення onboarding steps у потоці виконання.

[DeploymentWorkflowUseCases.cs:554](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Deployment/DeploymentWorkflowUseCases.cs#L554), [ActivateAnchorsUseCase.cs:130](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Deployment/ActivateAnchorsUseCase.cs#L130), [DeploymentStores.cs:31](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Abstractions/Persistence/DeploymentStores.cs#L31)

Тест `Ac9CommitSnapshotIsStored` перевіряє повернений DTO, а не PostgreSQL. [StandaloneDeploymentLivingSpecTests.cs:160](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/tests/Mfc.UnitTests/Deployment/StandaloneDeploymentLivingSpecTests.cs#L160)

**Напрям:** провести commit evidence через runtime contract до атомарного запису; зробити journal persistence частиною кожного ефекту. Критерій — після restart та повторного планування доступні точні committed artifact/hash і останній підтверджений крок.

### F04. HIGH — automatic rollback слабший за explicit recovery

**Вимога:** Safe Deployment §§46,48: невідомий target не перезаписується; успішний rollback підтверджує old artifact, management path та watchdog state.

Після невдалої postactivation verification standalone rollback безумовно записує всі old targets. Наявність третього, вручну зміненого target не зупиняє цей шлях. Потім результат `DisarmWatchdogAsync` відкидається, а стан стає `RolledBack`. Верифікації old artifact/fresh connection/old probes тут немає. VRRP повторює такий спрощений rollback.

[ExecuteStandaloneDeploymentUseCase.cs:439](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs#L439), [ExecuteStandaloneDeploymentUseCase.cs:463](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Deployment/ExecuteStandaloneDeploymentUseCase.cs#L463), [RouterOsVrrpMemberDeploymentRuntime.cs:165](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Deployment/RouterOsVrrpMemberDeploymentRuntime.cs#L165)

**Наслідок:** можливе перетирання ручної зміни та неправдивий terminal state із непідтвердженим вимкненням watchdog. Наявність окремого строгішого explicit rollback цього шляху не виправляє.

**Напрям:** використовувати один перевірений rollback coordinator для automatic/explicit/recovery; third target → RecoveryRequired; failed disarm або old-state verification не може завершуватися успішним rollback.

### F05. HIGH — onboarding watchdog отримує час Controller замість RouterOS

**Вимога:** Onboarding §§35–36: deadline від RouterOS clock та фактичний remaining TTL/commit margin.

`StartOnboardingUseCase` передає `now, now`; production runtime використовує друге значення як router clock. Watchdog writer обчислює deadline із нього. Під час arm/disarm coordinator не передає фактичний залишок TTL, тому застосовується початковий бюджет.

[OnboardingWorkflowUseCases.cs:527](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Onboarding/OnboardingWorkflowUseCases.cs#L527), [RouterOsOnboardingRuntime.cs:38](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Onboarding/RouterOsOnboardingRuntime.cs#L38), [ExecuteOnboardingBootstrapUseCase.cs:158](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Onboarding/ExecuteOnboardingBootstrapUseCase.cs#L158), [OnboardingWatchdogWriter.cs:153](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Onboarding/OnboardingWatchdogWriter.cs#L153)

**Наслідок:** розбіжність годинників і тривала операція не враховуються належним чином. Фактичний момент watchdog rollback на пристрої в цьому аудиті не вимірювався.

**Напрям:** читати clock кожного Device та відстежувати elapsed/remaining budget протягом операції; перевірити clock skew і вичерпання commit margin.

### F06. HIGH — Controller довіряє клієнтському analysis; stale-check частково порівнює hash із самим собою

**Вимога:** Policy Model §§63–64: обчислені результати mandatory/system tests, safety/risk та актуальні dependencies.

`RecordAnalysisRun` приймає hashes, findings, test outcomes/proofs, risk і evidence flag від клієнта. Controller додає structural findings і перевіряє наявність потрібних test IDs, але не виконує весь safety/test pipeline. Desktop сам повторює один logical/content hash у різних context slots та передає порожні test results.

[PolicyGrpcService.cs:355](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Controller/Grpc/PolicyGrpcService.cs#L355), [PolicyApprovalUseCases.cs:298](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Policies/PolicyApprovalUseCases.cs#L298), [PolicyPanelService.cs:856](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/Services/PolicyPanelService.cs#L856)

Уповноважений клієнт може подати LOW risk і PASS/PROVEN як готові результати. Це дефект довіри до даних після authorization, а не доведений обхід authentication.

Окремо Approve і Bind викликають fingerprint calculator із `NodeId=null`; calculator повертає frozen fingerprint run. Зміна dependencies не перевіряється цим CAS. Compile перевіряє частину live Node dependencies, але company/site/node bindings, active exceptions, compatibility та management profile у vector залишені нульовими.

[PolicyApprovalUseCases.cs:887](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Policies/PolicyApprovalUseCases.cs#L887), [PolicyBindingUseCases.cs:132](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Policies/PolicyBindingUseCases.cs#L132), [LivePolicyDependencyFingerprintCalculator.cs:43](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Policies/LivePolicyDependencyFingerprintCalculator.cs#L43), [LivePolicyDependencyFingerprintCalculator.cs:123](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Policies/LivePolicyDependencyFingerprintCalculator.cs#L123)

**Напрям:** server-owned analysis із конкретним target context; повний dependency vector та його повторний розрахунок на Approve/Bind/Compile/Start. Клієнт задає намір, не авторитетний PASS.

### F07. HIGH — desired binding не визначає revision для composition

**Вимога:** Policy Model §§10,29–30: desired revision та активні overlay/exception bindings.

Compose і Compile вибирають найновішу `Approved` revision за номером. Active binding використовується частково як gate, але не як джерело revision кожного шару. Approved exception може потрапити в composition без потрібного active binding.

[ComposeEffectivePolicyUseCase.cs:250](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Policies/ComposeEffectivePolicyUseCase.cs#L250), [CompileNodeFilterArtifactsUseCase.cs:692](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Policies/CompileNodeFilterArtifactsUseCase.cs#L692)

**Контрольний сценарій:** revision 1 approved+bound; revision 2 approved, але не activated. Compose вже вибере revision 2. Compile для analysis revision 1 може отримати logical-hash mismatch, хоча desired binding не змінювався. Це не твердження про автоматичний live deploy revision 2: подальші gates можуть його блокувати.

Тест `A1LoadsNodeUniqueCompanyAndLatestApproved` прямо закріплює вибір latest approved. [ComposeEffectivePolicyUseCaseTests.cs:19](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/tests/Mfc.UnitTests/Application/ComposeEffectivePolicyUseCaseTests.cs#L19)

**Напрям:** визначати revision IDs усіх шарів через active desired bindings. Approval і activation мають залишатися різними діями.

### F08. HIGH — canonical capture втрачає важливу частину фактичного firewall/routing state

**Вимога:** Canonical Snapshot §§4,16–17; Policy Model §§44,64.

1. `ProjectFacility` для NAT/RAW/Mangle не переносить низку прочитаних known matchers: protocol, адреси/порти, інтерфейси, jump-target, passthrough. Routing rule projection відкидає source/destination selectors. Вони вже є у discovery, тому проблема в projection. `RawProperties` не повертає втрачені known fields.
2. Dynamic filter rules правильно виключено з configuration, але observation projection їхньої effective sequence відсутня. Management safety читає лише configuration filter sections.
3. Read mapper декодує UTF-8 із replacement і перезаписує повторні scalar attributes через dictionary assignment. Read Adapter specification вимагає binary compatibility handling і duplicate error.

[DiscoveryCanonicalProjector.cs:487](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Snapshot/DiscoveryCanonicalProjector.cs#L487), [DiscoveryCanonicalProjector.cs:384](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Snapshot/DiscoveryCanonicalProjector.cs#L384), [DiscoveryCanonicalProjector.cs:304](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Snapshot/DiscoveryCanonicalProjector.cs#L304), [GetDevicePolicySafetyAnalysisUseCase.cs:143](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Policies/GetDevicePolicySafetyAnalysisUseCase.cs#L143), [RosReadCommandExecutor.cs:136](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Commands/RosReadCommandExecutor.cs#L136)

**Наслідки:** зміна лише NAT `dst-port` або routing `src-address` може не змінити configuration hash/diff. Dynamic pre-guard drop не потрапляє у snapshot-based management analysis. Це не поширюється автоматично на всі live paths: прямий onboarding mapper читає discovery окремо.

**Напрям:** забезпечити повну profile-based projection, observation effective sequence та lossless/strict mapping. Перевірки повинні проходити через production capture→persist→analysis, а не подавати готові canonical records напряму.

### F09. MEDIUM — capture змішує payload deduplication із новою спробою спостереження

`CaptureSnapshotUseCase` знаходить snapshot за actor/key без перевірки, що він належить запитаному Device. При однаковому snapshot hash повертає попередній capture; новий idempotency key у цій гілці не зберігається як окрема спроба. [CaptureSnapshotUseCase.cs:91](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Snapshots/CaptureSnapshotUseCase.cs#L91), [CaptureSnapshotUseCase.cs:160](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Snapshots/CaptureSnapshotUseCase.cs#L160)

**Наслідки:** повторне використання key для іншого Device може повернути результат першого; нове успішне спостереження незмінного стану не має власної свіжої persisted capture identity/time. Hash-рівність вмісту не доводить час останньої перевірки.

Node capture виконує members послідовно та завершується при першій помилці, без спільного durable partial result/skew contract. [CaptureNodeSnapshotsUseCase.cs:99](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Snapshots/CaptureNodeSnapshotsUseCase.cs#L99)

**Напрям:** deduplicate payload bytes, зберігати кожну capture attempt; зв’язувати idempotency із hash повного request. Node capture має представляти результати всіх members і придатність їхнього часового набору.

### F10. HIGH — Start/Watch прив’язані до тривалого unary RPC

Controller повертає відповідь Start тільки після всього runtime. Progress hub отримує timeline також після завершення; всі записи публікуються з фінальним state. Desktop починає Watch лише після відповіді Start. Його default unary deadline — 30 секунд, а повторний виклик генерує новий idempotency key.

[DeploymentGrpcService.cs:147](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Controller/Grpc/DeploymentGrpcService.cs#L147), [DeploymentViewModel.cs:271](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs#L271), [GrpcDeploymentServiceClient.cs:61](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/Services/GrpcDeploymentServiceClient.cs#L61), [DesktopOptions.cs:26](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/Configuration/DesktopOptions.cs#L26)

**Наслідок:** довгий Start може перерватися за deadline до отримання GUI operation ID; це впливає на cancellation/recovery. Поточний Watch не відображає живе виконання Start. Жодного такого timeout на CHR тут не відтворено.

**Напрям:** durable accept/idempotency до ефектів, швидке повернення operation ID, виконання Controller-ом незалежно від GUI RPC, реальні phase events і повтор того самого key. Збільшення timeout не усуває розрив життєвого циклу.

### F11. HIGH — GUI onboarding незавершений; policy panel може показувати чужі результати

`OnboardingViewModel.ValidateAsync` передає порожні facts. `CreatePlanAsync` безумовно кидає exception. Звичайний GUI workflow не може створити onboarding plan незалежно від стану пристрою. Відсутність вигаданих facts — правильна властивість, але сама по собі не реалізація функції. [OnboardingViewModel.cs:95](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs#L95), [OnboardingViewModel.cs:137](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/ViewModels/OnboardingViewModel.cs#L137)

Policies при зміні Device/Node оновлює target IDs і handoff, але не очищає попередні safety findings/context hashes/errors. Відповідь асинхронного analysis застосовується без повторної звірки поточного target. `ApplyState` змінює стан панелі, не оновлюючи одночасно catalog row.

[PoliciesViewModel.cs:1307](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs#L1307), [PoliciesViewModel.cs:1370](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs#L1370), [PoliciesViewModel.cs:1126](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/ViewModels/PoliciesViewModel.cs#L1126)

**Напрям:** Controller будує onboarding facts/plan; GUI результат пов’язаний із точними Device/Node/revision/capture і інвалідовується при зміні контексту. Просте збереження старих findings не можна видавати за новий аналіз.

### F12. HIGH — drift polling не спостерігає поточний RouterOS

**Вимога:** E2E §§32–34,49: actual managed state проти last committed, bounded polling.

Scheduler dispatch `DriftCapture` викликає `PollManagedDriftJobUseCase`, який читає PostgreSQL hash states і запускає detector із `PersistActualHash=false`. Detector використовує переданий або вже збережений actual hash; у цьому ланцюжку немає нового RouterOS read/capture.

[OperationalJobExecutor.cs:74](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Controller/Jobs/OperationalJobExecutor.cs#L74), [PollManagedDriftJobUseCase.cs:45](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Jobs/PollManagedDriftJobUseCase.cs#L45), [DriftUseCases.cs:121](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Drift/DriftUseCases.cs#L121)

**Наслідок:** ручна зміна CHR сама по собі не потрапляє в actual state через цей poll. Класифікація заданих hashes є, автоматичне виявлення зовнішньої зміни не завершене.

**Напрям:** actual read відповідного managed-resource hash domain → semantic diff → detector; failed read не прирівнюється до NoDrift. Потрібна також коректна committed база F03.

### F13. HIGH — M7 складається з компонентів без завершеного production lifecycle

Нормативне джерело M7 є в репо; функціональність не можна оголосити необов’язковою лише через відсутність wiring.

| Частина | Перевірений розрив | Наслідок |
|---|---|---|
| Routing/presence | Upsert routing state та OpenEndpointPresence мають DI registrations, але немає production caller від capture/observations | Звичайний capture не формує стан для routing viewer і mobility flow |
| Incident Bind | Реальний RPC створює assessment і повертає DTO; use case має лише authorization dependency, без запису assessment | Після успішного Bind новий active assessment відсутній у store для подальшої mobility invalidation |
| Incident TTL | Incident reconciliation job зареєстрований, але scheduler викликає лише звичайний exception reconciliation | Incident binding не переходить штатно в expired-pending-reconciliation |
| Outcome/feedback | ReportIncidentDeploymentOutcome зареєстрований, але deployment не викликає його | Успіх/rollback runtime не завершують incident feedback цикл |
| Incident deploy/removal | Use cases компілюють effective policy/artifacts для deploy або removal, але plan будують з незалежних caller DevicePlans, не результату compile | Майбутнє підключення цього dormant path дозволить розійтися compiled і deployed artifacts |

Джерела: [Program.cs:390](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Controller/Program.cs#L390), [IncidentResponseAssessmentUseCases.cs:39](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Incident/IncidentResponseAssessmentUseCases.cs#L39), [EndpointPresenceUseCases.cs:108](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Endpoint/EndpointPresenceUseCases.cs#L108), [OperationalJobExecutor.cs:47](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Controller/Jobs/OperationalJobExecutor.cs#L47), [IncidentDenyOverlayDeployUseCases.cs:140](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Application/Incident/IncidentDenyOverlayDeployUseCases.cs#L140).

Для останнього рядка **production entry не знайдено**: це дефект непідключеної реалізації, не доведена доступна remote-вразливість. TTL не означає дозволу на автоматичні RouterOS writes.

Тести routing host самі засівають state через DI; incident E2E сам викликає reporter після deployment. Такі перевірки корисні для окремих компонентів, але не доводять існування production зв’язків. [RoutingAssuranceGrpcHostTests.cs:51](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/tests/Mfc.IntegrationTests/Controller/RoutingAssuranceGrpcHostTests.cs#L51), [IncidentResponseE2ELivingSpecTests.cs:183](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/tests/Mfc.UnitTests/E2E/IncidentResponseE2ELivingSpecTests.cs#L183)

**Напрям:** підключити наявні use cases до джерел фактичних даних і durable lifecycle; incident deployment має використовувати той самий sealed-artifact path, що й звичайний.

### F14. MEDIUM — acceptance і release gates допускають формальне завершення без потрібного результату

README оголошує MVP/M7 CLOSED. `mvp-acceptance.md` прямо підміняє live CHR/physical CRS перевірки Living Specs і називає live acceptance optional. Це суперечить нормативному M2–M6 Issue Set та E2E acceptance.

[README.md:5](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/README.md#L5), [mvp-acceptance.md:53](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/docs/release/mvp-acceptance.md#L53), [M2–M6 Implementation Issue Set v0.1.md:3165](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/M2%E2%80%93M6%20Implementation%20Issue%20Set%20v0.1.md#L3165)

`MvpReleaseAcceptanceLivingSpecTests` перевіряє існування файлів/папок і текстових міток. `DesktopAuditGui01LivingSpecTests` доводить відсутність старих placeholder strings, а не успішне CreatePlan. Основний CI містить змістовні build/unit/PostgreSQL checks, але окремий RouterOS workflow запускає skeleton tests.

[MvpReleaseAcceptanceLivingSpecTests.cs:163](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/tests/Mfc.UnitTests/Release/MvpReleaseAcceptanceLivingSpecTests.cs#L163), [DesktopAuditGui01LivingSpecTests.cs:16](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/tests/Mfc.UnitTests/Desktop/DesktopAuditGui01LivingSpecTests.cs#L16), [routeros-integration.yml:24](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/.github/workflows/routeros-integration.yml#L24)

Release signing workflow працює з dry-run output у новому temp directory; побудови реальних Controller/Desktop packages у ньому немає. SBOM script допускає `components: []`, обрізаний package inventory та приглушення помилок через `|| true`. Без GPG створює текстовий `.asc`, прямо позначений як не криптографічний підпис.

[release-signing.yml:28](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/.github/workflows/release-signing.yml#L28), [generate-sbom-and-checksums.sh:18](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/scripts/release/generate-sbom-and-checksums.sh#L18)

**Виконане відтворення:** запущено незмінений `bash scripts/release/generate-sbom-and-checksums.sh` із `MFC_RELEASE_DRY_RUN=0`, порожнім GPG key ID та тимчасовим `OUT_DIR`. SDK відсутній і в PATH, і в `$HOME/.dotnet`.

```text
exit code: 0
stderr: line 70: dotnet: command not found
sbom components: 0
mode: lite-no-cyclonedx-tool
created: sbom.cdx.json, package-inventory.txt, SHA256SUMS, SHA256SUMS.asc
```

Це безпосередньо підтверджує false-success реального режиму script при неможливості отримати dependency inventory. Тимчасові outputs після перевірки видалено; репо не змінено. Це не перевірка всього release pipeline.

Окрема суперечність документації: `.cursor/rules/slash-autopilot.mdc` наказує зупинитися на порожньому NEXT, а ROADMAP §6 досі забороняє завершити хвилю без нового траншу. [slash-autopilot.mdc:15](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/.cursor/rules/slash-autopilot.mdc#L15), [ROADMAP.md:911](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/ROADMAP.md#L911)

**Напрям:** окремі статуси implementation/unit integration/live acceptance; ніякого PASS для відсутнього середовища. Release повинен падати без повного SBOM і вимаганого підпису саме випускних artifacts. Прибрати суперечливі чинні інструкції, зберігши історію як історію.

## Що реалізовано змістовно

- Modular monolith і межа Desktop→gRPC→Controller збережені. Production RouterOS adapters не є суцільними mock-об’єктами; `NotConfigured*` за вимкнених можливостей — штатна відмова, а не сама по собі помилка.
- Є typed RouterOS reads/writes, command allowlists, actual item lookup/read-back, detached chain staging, watchdog generation і свіжа API-SSL verification після activation.
- Certificate validator перевіряє validity, serverAuth EKU та configured CA/SPKI; секрети мають AES-GCM envelope protection. Production authorization використовує permission allowlist, не безумовну відмову всім операторам. [ApiSslCertificateValidator.cs:14](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Transport/ApiSslCertificateValidator.cs#L14), [AesGcmSecretProtector.cs:18](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Infrastructure/Secrets/AesGcmSecretProtector.cs#L18), [Program.cs:346](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Controller/Program.cs#L346)
- Є policy analyzers, immutable revision/hash models, CAS, warning acknowledgment і compiler gates. Їх варто завершити й правильно з’єднати, а не переписувати всю архітектуру.
- Compile→sealed artifacts→Operations/Deploy handoff реалізовано. Вимкнена кнопка Deploy на Policies не доводить відсутності цього шляху. [DeploymentViewModel.cs:105](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/ViewModels/DeploymentViewModel.cs#L105)
- Required-section capture gate активний; повноту matchers для static **filter** уже реалізовано. F08 стосується інших facility projections та dynamic observation path. [SnapshotCaptureResultBuilder.cs:15](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.RouterOs/Snapshot/SnapshotCaptureResultBuilder.cs#L15)
- Є реальні PostgreSQL integration tests для transactions/conflicts і поведінкові domain tests. Не всі Living Specs є текстовими перевірками. [DeploymentPersistTests.cs:108](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/tests/Mfc.IntegrationTests/Persistence/DeploymentPersistTests.cs#L108)
- Audit GUI виконує явний Refresh останніх 100 подій; read store сортує їх за часом та ID. Підстав оголошувати його runtime-stale лише з цих файлів немає. [AuditViewModel.cs:81](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Desktop/ViewModels/AuditViewModel.cs#L81), [EfAuditEventReadStore.cs:19](https://github.com/sesquicadaver/MTDirector/blob/acd0759e85414a83460c4cab971db2b0b58b30cd/src/Mfc.Infrastructure/Audit/EfAuditEventReadStore.cs#L19)

## Пріоритет виправлень за залежностями

1. **Захист write/recovery:** F01–F05, F10. Durable ownership/journal/commit, справжній preflight, єдиний rollback, коректний watchdog budget. Це блокери безпечних змін на пристроях.
2. **Достовірна основа аналізу:** F08–F09 → F06–F07. Повні capture facts, server-owned evidence, active bindings та повна invalidation. Паралельно з п.1 за чітких меж contracts.
3. **Робочий операторський цикл:** F11–F12. Controller-built onboarding, правильний GUI context і actual RouterOS drift спираються на виправлені пп.1–2.
4. **Завершення заявлених M7 і приймання:** F13–F14. Підключити наявні production ланцюжки; acceptance має перевіряти їх через public entry points. Уточнення неправдивих статусів і fail-open release script не потребує очікування всіх продуктових виправлень.

Це порядок усунення підтверджених дефектів, не нова активована черга робіт. Нові продуктові функції, broker, універсальний workflow framework чи переписування стека для цього не потрібні.

## Підсумок перевірки

| Перевірка | Результат і межа |
|---|---|
| Commit / diff / статус | `acd0759e`; поточний main підтверджено; source/test/config не змінені |
| Trace requirements→entry→implementation→tests | Виконано для наведених контурів; виявлені прямі порушення вимог |
| .NET build/unit/integration | **NOT RUN**: SDK і Docker відсутні; нічого не встановлювалося |
| SBOM real-mode failure handling | **FAIL**, відтворено: відсутній SDK, empty SBOM, exit 0 |
| GUI, PostgreSQL runtime, CHR, VRRP, physical CRS | **NOT RUN** у цьому аудиті |
| Зовнішні CI/E2E звіти | Не використовувалися як доказ |

Загальний відсоток готовності не обчислювався: кількість файлів, issues або DONE-маркерів не вимірює виконання нормативних сценаріїв. Наявні дефекти достатні, щоб відхилити твердження про завершений безпечний end-to-end продукт на цьому commit.
