# Desktop UI ↔ backend alignment plan

**Дата:** 2026-08-31  
**Мета:** UI показує **реальні дані**, які Controller вже віддає (або Desktop уже тримає в VM), без «хеш-лише» Diff, без UUID-ритуалу там, де є selection, і з чесною VRRP-поверхнею.  
**P3:** нові Contracts лише як **засіяні** рядки §3.C (W5-01…03) — не окремий стоп «чекай PLAN».

Суміжність з лабою: [`~/gns3-lab/VERIFICATION-MATRIX.md`](file:///home/sesquicadaver/gns3-lab/VERIFICATION-MATRIX.md) (A/B/C/D). Цей документ — **product UI wiring**, не lab provision.

## Класифікація gap

| Клас | Значення | Типовий обсяг |
|------|----------|----------------|
| **P0** | Дані вже в VM/DTO — axaml не біндить | 1 PR / модуль |
| **P1** | Wire є — Desktop губить/сплющує поля | 1 PR mapping+UI |
| **P2** | RPC є — немає client виклику / selection glue / Watch | 1–2 PR |
| **P3** | Немає Contracts/наповнення | Atomic §3 row (PLAN seeds in the same cycle; never idle) |

**Правило DoD кроку:** Living Spec / Desktop unit тест на наявність binding або AC «FieldLines visible»; anti-stub; docs sync (цей файл + CHANGELOG).

---

## Хвиля 1 — P0 «показати те, що вже в памʼяті» (найвищий ROI)

### W1.1 Diff: field-level changes *(блокер скарги «лише хеші»)* — **DONE**
- **Дані:** `SnapshotDiffEntryItem.FieldLines`, `SnapshotDiffViewModel.Warnings` / `HasWarnings`
- **Зроблено:** Semantic diff ItemTemplate — `ItemsControl` → `FieldLines.Summary`; блок «Compare warnings»
- **Не чіпали:** local SemanticDiffEngine
- **Перевірка:** Living Spec `Ac4bSemanticDiffShowsFieldLinesAndWarnings`; unit `SnapshotDiffServiceTests` (field Summary); lab Compare seed → field lines visible
- **Файли:** `MainWindow.axaml`, `SnapshotDiffViewModel.cs`, `DesktopMvpWorkflowsLivingSpecTests`

### W1.2 Snapshots: повні поля запису — **DONE**
- **Дані:** `SnapshotRecordListItem.Fields` (список лишає `SummaryLine` ≤4 + ellipsis)
- **Зроблено:** Snapshot tab — select configuration/observation record → detail «Selected record fields» з усіма `Fields.DisplayLine`
- **Не чіпали:** local SemanticDiffEngine
- **Перевірка:** Living Spec `Ac4cSnapshotRecordDetailShowsAllFields`; unit `LoadSectionMapsAllRecordFieldsNotOnlySummaryLine` (chain/action/comment + поля поза SummaryLine)
- **Файли:** `MainWindow.axaml`, `SnapshotViewerViewModel.cs`, `SnapshotViewerModels.cs`, `DesktopMvpWorkflowsLivingSpecTests`

### W1.3 Policies: обʼєкти / contracts / DiffLines + selection glue — **DONE**
- **Дані:** `AddressObjects`, `ServiceObjects`, `ChainContracts`, `DiffLines` + Upsert*/Replace*/RecordAnalysis; Create draft уже пише `RevisionIdText`
- **Зроблено:** Policies axaml — секції списків address/service/contracts + Revision diff; Compose UUID ← inventory Node (або parent Node при Device)
- **Не чіпали:** ListPolicies catalog browse (P3); Save and Deploy; local SemanticDiffEngine
- **Перевірка:** Living Spec `Ac5bPoliciesBindCatalogListsAndComposeFromSelectedNode`; `PoliciesViewModelTests` (Node/Device → Compose; Create draft → revision id)
- **Файли:** `MainWindow.axaml`, `PoliciesViewModel.cs`, `App.axaml.cs`, `DesktopMvpWorkflowsLivingSpecTests`

### W1.4 Deploy / Onboarding: приховані колекції — **DONE**
- **Дані:** Deploy `ArtifactLines` / `OrderLines` / `ProbeAndWatchdogLines` / `SemanticDiffLines`; Onboarding `Placements`
- **Зроблено:** Operations axaml біндить усі чотири Deploy-списки; Onboarding — Anchor placements; підпис SemanticDiffLines = «Artifact hash delta»
- **Не чіпали:** багатший Contracts policy diff (P3)
- **Перевірка:** Living Spec `Ac6bOperationsShowsPlanCollectionsNotOnlyHashDelta`
- **Файли:** `MainWindow.axaml`, `DesktopMvpWorkflowsLivingSpecTests`

### W1.5 Drift: findings зі list response — **DONE**
- **Дані:** `DriftEvent.findings` already on `ListDeviceDriftEvents`; Desktop previously used only `Findings.Count`
- **Зроблено:** `DriftFindingListItem` (kind/severity/detail) + list під вибраною подією; `SemanticDiffText` лишається другим джерелом
- **Не чіпали:** `GetDriftEvent` RPC (W3.7); local SemanticDiffEngine; auto-fix
- **Перевірка:** Living Spec `Ac7bDriftShowsFindingsFromListResponseNotOnlySemanticDiff`; `DriftViewModelTests.FromProtoKeepsFindingKindSeverityAndDetail`
- **Файли:** `MainWindow.axaml`, `DriftViewModel.cs`, `DesktopMvpWorkflowsLivingSpecTests`

### W1.6 Inventory/Node: явні device поля — **DONE**
- **Дані:** `InventoryNodeViewModel` уже має `ReachabilityText` / `ModelText` / `RouterOsVersionText` / `VrrpRolesText` / `LastSnapshotText` (мапинг з `Device` proto)
- **Зроблено:** Inventory detail біндить поля окремо (не лише `DetailSummary`); VRRP лише коли `HasVrrpRoles`; Node — список `DeviceMembers` з тими ж полями
- **Не чіпали:** GetNode fill (W2.3); WriteEnabled
- **Перевірка:** Living Spec `Ac3bInventoryAndNodeShowExplicitDeviceFields`; `InventoryNodeViewModelTests`; `NodeDetailViewModelTests`; `InventoryTreeServiceTests.MapDeviceKeepsReachabilityModelVersionVrrpAndLastSnapshot`
- **Файли:** `MainWindow.axaml`, `InventoryNodeViewModel.cs`, `NodeDetailViewModel.cs`, `DesktopMvpWorkflowsLivingSpecTests`

**Exit W1:** оператор на GNS3 seed бачить зміну правила в Diff як поля; Policies не «порожня форма»; Deploy/Onboarding показують plan details.

---

## Хвиля 2 — P1 якість mapping (без нових RPC)

### W2.1 Diff / Snapshot record fidelity — **DONE**
- **Дані:** `DiffEntry.before` / `after` SnapshotRecord; `DiffPage.warnings` (wire already)
- **Зроблено:** selected Diff entry shows sanitized Before/After record fields (credentials stripped, same as snapshot viewer). Compare warnings: union across pages (dedupe); Desktop shows first 12 + overflow line.
- **Не чіпали:** local SemanticDiffEngine; WriteEnabled; server Compare
- **Перевірка:** Living Spec `Ac4fSemanticDiffShowsBeforeAfterRecordsAndTruncatesWarnings`; `SnapshotDiffServiceTests`; `SnapshotDiffViewModelTests`
- **Файли:** `SnapshotDiffService`, `SnapshotDiffViewModel`, `GrpcSnapshotViewerClient`, `MainWindow.axaml`

### W2.2 Routing assurance detail — **DONE**
- **Дані:** `RouteExpectation.allowed_next_hops`, `RouteFinding.subject`, `RouteResolutionTraceSummary.next_hop_gateways` (wire already)
- **Зроблено:** typed list rows bind next-hop **values** and finding **subject** as distinct fields (not `next_hops={count}` / one SummaryLine). Compact SummaryLine stays the header.
- **Не чіпали:** WriteEnabled; routing writes; full BGP/FIB dump; local SemanticDiffEngine
- **Перевірка:** Living Spec `Ac9RoutingAssuranceBindsNextHopAndSubjectFields`; `RoutingAssuranceViewModelTests`
- **Файли:** `RoutingAssuranceViewModel`, `MainWindow.axaml`

### W2.3 VRRP labels pipeline — **DONE**
- **Дані:** last completed capture canonical `ha.vrrp` **observations** (`role` + `group`); proto mapper already copies `VrrpRoleLabels`
- **Зроблено:** `GetNodeUseCase` проєктує labels через `DeviceVrrpRoleLabelProjector`; без snapshot / без `role` → порожньо (не вигадуємо Master/Backup)
- **Не чіпали:** live RouterOS probe на GetNode; version/model/reachability (окремий projector)
- **Перевірка:** Living Spec `VrrpRoleLabelsLivingSpecTests`; `GetNodeMapsVrrpRoleLabelsFromLastCaptureObservations`
- **Файли:** `DeviceVrrpRoleLabelProjector.cs`, `InventoryUseCases.cs`, `ViewMapper.cs`

**Exit W2:** VRRP roles з last capture; Diff Before/After + warning truncate; Routing assurance next-hop/subject fields. Хвиля 2 **CLOSED**.

---

## Хвиля 3 — P2 glue: викликати наявні RPC з Desktop

Порядок за операторським шляхом:

| Крок | RPC / client | UI |
|------|----------------|-----|
| W3.1 | `StartCapture` + `WatchCapture` | Snapshots: кнопка Capture + progress — **DONE** |
| W3.2 | `ValidateDeviceConnection` | Add router / Inventory «Probe» — **DONE** |
| W3.3 | Onboarding/Deploy `Watch` streams | Progress live, не лише snapshot після Start — **DONE** |
| W3.4 | `GetNodeWorkflow` | Node/Inventory workflow без дублювання ad-hoc — **DONE** |
| W3.5 | Zones `UpdateZoneDefinition`, `ResolveZonesForDevice` | Edit zone; resolve device — **DONE** |
| W3.6 | Policy: `UpdateRule`/`DeleteRule`/`AcknowledgeWarning`/`CompileNodeFilterArtifacts` | за пріоритетом review/deploy loop — **DONE** |
| W3.7 | Drift `GetDriftEvent` | full payload on selection — **DONE** |

**Exit W3:** Capture/Probe/Watch/GetNodeWorkflow/Zones mutate/Policy mutate/GetDriftEvent з Desktop без grpcurl.

---

## Хвиля 4 — VRRP structured UX (P2 UI + можливий P3 data)

1. **Node-centric shell** для `NodeKind.Vrrp`: members table (a/b), role, mgmt host, last capture — **DONE** (W4.1)
2. Selection model: ops на **Node** (pair) з drill-down member; Deploy не «перший child» мовчки — **DONE** (W4.2)
3. Wizard: опція create VRRP node + register 2 devices — **DONE** (W4.3)
4. Pair capture / compare guidance (per-member captures; cross-device compare лишається forbid by design — показати why) — **DONE** (W4.4)

Залежить від W2.3 (labels) і W3.1 (capture).

---

## Хвиля 5 — P3 у §3.C (уже засіяно PLAN-02, не «чекай окремий PLAN»)

Атомарні рядки вже в [`ROADMAP.md`](../../ROADMAP.md) §3.C і [`continuous-queue-plan.md`](../planning/continuous-queue-plan.md). Лаба **не** блокує ці PR.

| Крок | Logical ID | Issue | Scope |
|------|------------|------:|-------|
| W5.a | W5-01 | [#342](https://github.com/sesquicadaver/MTDirector/issues/342) | `ListPolicies` / catalog browse — **DONE** |
| W5.b | W5-02 | [#343](https://github.com/sesquicadaver/MTDirector/issues/343) | ManagementPath / FastTrack Desktop RPC — **DONE** |
| W5.c | W5-03 | [#344](https://github.com/sesquicadaver/MTDirector/issues/344) | Typed deployment semantic policy diff |
| CRS / physical lab | — | — | **Не §3** — residual / ops parallel |

Glue **перед** W5 (існуючі RPC): **CONT-01** Rollback Watch — **DONE**; **CONT-02** neighbor → member b — **DONE**.

---

## Рекомендований порядок PR

```
W1.1 Diff FieldLines     ← DONE
W1.2 Snapshot Fields      ← DONE
W1.3 Policies bind+Compose selection  ← DONE
W1.4 Deploy/Onboarding collections  ← DONE
W1.5 Drift findings               ← DONE
W1.6 Inventory device fields      ← DONE
W2.3 VRRP labels from last capture ← DONE
W3.1 StartCapture Desktop          ← DONE
W3.2 ValidateConnection            ← DONE
W3.3 Watch streams                 ← DONE
W3.4 GetNodeWorkflow               ← DONE
W3.5 Zones Update/ResolveDevice    ← DONE
W3.6 Policy mutate RPCs            ← DONE
W3.7 Drift GetDriftEvent           ← DONE
W4.1 VRRP Node members table       ← DONE
W4.2 Deploy not silent first-child ← DONE
W4.3 VRRP create+register wizard   ← DONE
W4.4 Pair capture / compare guidance ← DONE
W2.1 Diff record Before/After + warning truncate ← DONE
W2.2 Routing assurance next-hop/subject fields ← DONE
CONT-01 Rollback Watch              ← DONE
CONT-02 Neighbor apply member b     ← DONE
W5-01 ListPolicies catalog          ← DONE
W5-02 ManagementPath / FastTrack Desktop  ← DONE
W5-03 Typed deploy policy semantic diff  ← NEXT (§3)
```

Кожен PR: один модуль / один клас gap; оновити цей документ (статус DONE); Desktop Living Spec AC на ключові рядки axaml/VM.

## Анти-цілі (не робити під виглядом alignment)

- Не підміняти server Compare локальним diff engine
- Не вмикати WriteEnabled «щоб UI ожив» — це lab/gate (B), не UI wiring
- Не маскувати порожні VRRP roles фейковими лейблами без backend фактів
- Не роздувати Policies до повного SIEM у одному PR

## Поточний статус

| Хвиля | Статус |
|-------|--------|
| W1.1 | **DONE** (FieldLines + Warnings UI) |
| W1.2 | **DONE** (selected-record Fields detail) |
| W1.3 | **DONE** (catalog lists + Compose ← Node) |
| W1.4 | **DONE** (Deploy/Onboarding plan collections) |
| W1.5 | **DONE** (Drift findings from list response) |
| W1.6 | **DONE** (explicit Inventory/Node device fields) |
| W2.3 | **DONE** (GetNode fills VRRP labels from last capture) |
| W3.1 | **DONE** (Desktop StartCapture + WatchCapture + Capture progress) |
| W3.2 | **DONE** (Desktop ValidateDeviceConnection Probe) |
| W3.3 | **DONE** (Onboarding/Deploy Start + Watch progress) |
| W3.4 | **DONE** (Node GetNodeWorkflow + device contributing/sync) |
| W3.5 | **DONE** (Zones Update zone + Resolve device) |
| W3.6 | **DONE** (Policy Update/Delete/Ack/Compile) |
| W3.7 | **DONE** (Drift GetDriftEvent detail payload) |
| W4.1 | **DONE** (VRRP Node a/b members: role / mgmt host / last capture) |
| W4.2 | **DONE** (Deploy/Onboarding plan all VRRP members, not silent first Device) |
| W4.3 | **DONE** (Add router: CreateNode Vrrp + register two devices) |
| W4.4 | **DONE** (VRRP per-member capture guidance; compare shows why a-against-b is forbidden) |
| W2.1 | **DONE** (Diff Before/After record detail; Compare warnings truncated) |
| W2.2 | **DONE** (Routing assurance next-hop values + finding subject fields) |
| W5 | W5-01…02 **DONE**; W5-03 queued (§3.C) |
| CONT-01 | **DONE** Rollback Watch (#340) |
| CONT-02 | **DONE** neighbor → member b (#341) |

### W3.1 Snapshots: Capture + progress — **DONE**
- **Дані:** SnapshotService `StartCapture` / `WatchCapture` (device_id only; Controller M1-26)
- **Зроблено:** Desktop client + Snapshots кнопка Capture; `CaptureProgressText` зі stage / `current_section`; після COMPLETED — Reload list; FAILED показує sanitized error. Capture RPC off UI thread (`Task.Run`).
- **Не чіпали:** `node_id` StartCapture (deferred); WriteEnabled; local SemanticDiffEngine
- **Перевірка:** Living Spec `Ac4dSnapshotCaptureStartsAndWatchesProgress`; `SnapshotViewerViewModelTests`
- **Файли:** `ISnapshotViewerClient`, `GrpcSnapshotViewerClient`, `SnapshotViewerViewModel`, `MainWindow.axaml`, `App.axaml.cs`

### W3.2 Inventory/Add router: Probe — **DONE**
- **Дані:** InventoryService `ValidateDeviceConnection` → DiscoverDeviceUseCase (read-only Controller probe)
- **Зроблено:** Desktop client + кнопка Probe на Inventory device detail і в Add router; `ProbeResultText` = observed identity / support / mutated; target = selected Device або last registered
- **Не чіпали:** WriteEnabled; local SemanticDiffEngine
- **Перевірка:** Living Spec `Ac2cInventoryAndAddRouterProbeValidateDeviceConnection`; `AddRouterWizardViewModelTests`
- **Файли:** `IInventoryTreeClient`, `GrpcInventoryTreeClient`, `AddRouterWizardViewModel`, `MainWindow.axaml`

### W3.3 Operations: Onboarding/Deploy Watch — **DONE**
- **Дані:** `OnboardingService.Watch` / `DeploymentService.Watch` (clients already existed; Start used only `Timeline`)
- **Зроблено:** Start → Start RPC + Watch stream off UI thread; ProgressLines bind stream `state`/`timeline_entry` (fallback to Start.Timeline if Watch empty). Rollback Watch: **CONT-01**.
- **Не чіпали:** GetNodeWorkflow (W3.4); WriteEnabled
- **Перевірка:** Living Spec `Ac6cOperationsStartWatchesOnboardingAndDeploymentProgress`; `OnboardingViewModelTests`; `DeploymentViewModelTests`
- **Файли:** `OnboardingViewModel.cs`, `DeploymentViewModel.cs`

### CONT-01 Operations: Deployment Rollback Watch — **DONE**
- **Дані:** existing `Watch` / `Rollback` RPCs; hub replay after Committed includes rollback events
- **Зроблено:** `RollbackAndWatchAsync` (Task.Run); ProgressLines from Watch (fallback Timeline); `DeploymentProgressHub` does not stop replay at the first terminal
- **Не чіпали:** Onboarding Rollback Watch; WriteEnabled; new RPC
- **Перевірка:** Living Spec `Ac6eDeploymentRollbackWatchesProgress`; `DeploymentViewModelTests`; `Ac3bWatchReplaysRollbackEventsAfterCommittedTerminal`
- **Файли:** `DeploymentViewModel.cs`, `DeploymentProgressHub.cs`

### W3.4 Node: GetNodeWorkflow — **DONE**
- **Дані:** InventoryService `GetNodeWorkflow` → `NodeWorkflow.workflow_status` + `DeviceWorkflowProjection` (contributing_status / sync_classification)
- **Зроблено:** Desktop client + Node tab loads workflow off UI thread; `WorkflowDeviceLines` bind projections; `DeploymentReadinessText` = canonical workflow status (не mashup Zones+Onboarding). Inventory tree label лишається з `GetNode.workflow_status` (той самий projector, без N+1 на refresh).
- **Не чіпали:** WriteEnabled; Zones mutate (W3.5); local SemanticDiffEngine
- **Перевірка:** Living Spec `Ac3cNodeLoadsGetNodeWorkflowInsteadOfAdHocReadinessMashup`; `NodeDetailViewModelTests`
- **Файли:** `IInventoryTreeClient`, `GrpcInventoryTreeClient`, `NodeDetailViewModel`, `MainWindow.axaml`, `App.axaml.cs`

### W3.5 Zones: Update definition + Resolve device — **DONE**
- **Дані:** ZoneService `UpdateZoneDefinition` (name/description + row_version) / `ResolveZonesForDevice` (selected Device)
- **Зроблено:** Zones panel «Update zone» (edit fields from selection; empty description → reset) і «Resolve device»; RPC off UI thread (`Task.Run`). Existing «Resolve node» unchanged.
- **Не чіпали:** WriteEnabled; Policy mutate (W3.6); local SemanticDiffEngine; Rollback Watch
- **Перевірка:** Living Spec `Ac2dZonesEditDefinitionAndResolveDevice`; `ZonesViewModelTests`; `ZonesDesktopServiceTests`
- **Файли:** `ZonePanelService`, `ZonesViewModel`, `MainWindow.axaml`

### W3.6 Policies: Update / Delete / Ack / Compile — **DONE**
- **Дані:** PolicyService `UpdateRule` / `DeleteRule` (CAS content hash) / `AcknowledgeWarning` (analysis_run_id + warning_hash) / `CompileNodeFilterArtifacts` (semantic summary only)
- **Зроблено:** Policies panel Update/Delete вибраного правила; Acknowledge recorded finding (Desktop SHA-256 = Domain `mfc.policy.warning.v1`); Compile з Compose Node UUID + 64-hex capability з Snapshots. RPC off UI thread (`Task.Run`). Deploy лишається blocked.
- **Не чіпали:** WriteEnabled; Save and Deploy; `GetDriftEvent` (W3.7); ListPolicies catalog (P3); local SemanticDiffEngine
- **Перевірка:** Living Spec `Ac5cPoliciesMutateRulesAckWarningsAndCompile`; `PoliciesViewModelTests`; `PolicyDesktopServiceTests`
- **Файли:** `IPolicyServiceClient`, `GrpcPolicyServiceClient`, `PolicyPanelService`, `PoliciesViewModel`, `MainWindow.axaml`

### W3.7 Drift: GetDriftEvent detail — **DONE**
- **Дані:** DriftService `GetDriftEvent` (той самий `DriftEvent`, що list; list UI обрізав хеші й не показував desired / semantic_diff_hash / node / immutable)
- **Зроблено:** вибір події → `GetDriftEvent` off UI thread (`Task.Run` + epoch); detail = повні хеші, desired (ignored for baseline), semantic_diff_hash, node id, immutable; findings/semantic diff з Get замінюють list snapshot. Get fail → лишається list payload.
- **Не чіпали:** WriteEnabled; auto-fix; local SemanticDiffEngine; W4 VRRP shell
- **Перевірка:** Living Spec `Ac7cDriftLoadsGetDriftEventForSelectedPayload`; `DriftViewModelTests`
- **Файли:** `DriftViewModel`, `MainWindow.axaml`

### W4.1 Node: VRRP members table — **DONE**
- **Дані:** `NodeKind.Vrrp` (`NodeKindText == "Vrrp"`); device children; `vrrp_role_labels` (W2.3); proto `management_host`/`management_port`; last snapshot
- **Зроблено:** Node tab показує таблицю members a/b (role лише з backend labels, mgmt host, last capture); drill-down selected member; generic Devices list лише для non-VRRP. `InventoryTreeService` мапить management host (host або host:port).
- **Не чіпали:** Deploy `RequireDeviceId` first-child (W4.2); Add router VRRP wizard (W4.3); WriteEnabled; вигадані Master/Backup
- **Перевірка:** Living Spec `Ac3dVrrpNodeShowsMemberTableRoleHostAndLastCapture`; `NodeDetailViewModelTests`; `InventoryTreeServiceTests`; `InventoryNodeViewModelTests`
- **Файли:** `InventoryTreeService`, `InventoryNodeViewModel`, `NodeDetailViewModel`, `MainWindow.axaml`

### W4.2 Operations: VRRP pair not silent first Device — **DONE**
- **Дані:** resolved Node (`NodeKindText == "Vrrp"`) + усі Device children; CreatePlan/Validate уже приймають `repeated` device inputs
- **Зроблено:** `InventoryOpsSelection` — ops на Node (pair); план/validate включає всіх members. Вибір member у дереві все одно планує пару. Hint на Onboarding/Deploy. `RequireDeviceId` + `FirstOrDefault` Device child прибрані.
- **Не чіпали:** Add router VRRP wizard (W4.3); pair capture/compare guidance (W4.4); WriteEnabled; Snapshot `device_id` Capture
- **Перевірка:** Living Spec `Ac6dOperationsTargetVrrpNodePairNotSilentFirstDevice`; `InventoryOpsSelectionTests`; `DeploymentViewModelTests`; `OnboardingViewModelTests`
- **Файли:** `InventoryOpsSelection`, `DeploymentViewModel`, `OnboardingViewModel`, `MainWindow.axaml`

### W4.3 Add router: VRRP Node + two devices — **DONE**
- **Дані:** Inventory `CreateNode(declared_kind=Vrrp)` + two `RegisterDevice` / `UpdateDeviceConnection` (existing RPCs)
- **Зроблено:** checkbox «Create as VRRP pair» on new Node; one submit registers members a/b (distinct names/hosts; shared credentials). Roles not invented — capture labels stay W2.3.
- **Не чіпали:** pair capture/compare guidance (W4.4); WriteEnabled
- **Перевірка:** Living Spec `Ac2eAddRouterWizardCreatesVrrpNodeAndRegistersTwoDevices`; `AddRouterWizardViewModelTests`
- **Файли:** `AddRouterWizardViewModel`, `MainWindow.axaml`

### CONT-02 Add router: neighbor apply fills VRRP member b — **DONE**
- **Дані:** existing `ListNeighborCandidates` + Apply (PLAN-NBR-01); pair fields from W4.3
- **Зроблено:** pair mode — first Apply fills member a; second Apply fills `PairMemberB*` (host/port/display). Pair off — primary-only. No auto-register.
- **Не чіпали:** WriteEnabled; VRRP role labels; LAN scan
- **Перевірка:** Living Spec `Ac2fAddRouterNeighborApplyFillsVrrpMemberB`; `AddRouterWizardViewModelTests`
- **Файли:** `AddRouterWizardViewModel`

### W5.a Policies: ListPolicies catalog browse — **DONE**
- **Дані:** new `PolicyService.ListPolicies` (kind filter optional; active policies + latest revision identity). Domain catalog already existed (M2); this is Contracts + Controller + Desktop.
- **Зроблено:** catalog ListBox + Refresh; select row → `GetPolicyRevision` fills existing Rules / address / service / contracts. Create draft refreshes catalog. No Save and Deploy; no local SemanticDiffEngine.
- **Не чіпали:** WriteEnabled; SIEM-scale policy UI; W5-02 ManagementPath (then queued)
- **Перевірка:** Living Spec `Ac5dPoliciesCatalogBrowseListPoliciesThenSelectLoadsRevision`; `ListPoliciesUseCaseTests`; `PoliciesViewModelTests`; `PolicyProtoContractTests`; `PolicyGrpcHostTests`
- **Файли:** `policy.proto`, `ListPoliciesUseCase`, `PolicyGrpcService`, `IPolicyServiceClient`, `PolicyPanelService`, `PoliciesViewModel`, `MainWindow.axaml`

### W5.b Policies: ManagementPath / FastTrack Desktop — **DONE**
- **Дані:** Domain `ManagementPathAnalysis` / `FastTrackAnalysis` already exist (M2-13 / M2-15). New read RPC `GetDevicePolicySafetyAnalysis` runs existing Application mappers on the device's last capture.
- **Зроблено:** Controller hashes + blockers + witnesses + SYSTEM tests; Desktop Policies panel binds them. Controller source CIDRs are required (no invented `/0`). Optional revision supplies FastTrack desired rules. No local Domain analysis on Desktop; no VRRP role invention; no WriteEnabled.
- **Не чіпали:** new analysis algorithms; Save and Deploy; local SemanticDiffEngine
- **Перевірка:** Living Spec `Ac5ePoliciesShowManagementPathAndFastTrackAnalysis`; `GetDevicePolicySafetyAnalysisUseCaseTests`; `PoliciesViewModelTests`; `PolicyDesktopServiceTests`; `PolicyProtoContractTests`; `PolicyGrpcHostTests`
- **Файли:** `policy.proto`, `GetDevicePolicySafetyAnalysisUseCase`, `PolicyGrpcService`, `IPolicyServiceClient`, `PolicyPanelService`, `PoliciesViewModel`, `MainWindow.axaml`

### W4.4 Snapshots: VRRP pair capture / compare guidance — **DONE**
- **Дані:** `StartCapture`/`WatchCapture` лишаються `device_id`; `CompareSnapshots` already forbids different devices (`SNAPSHOTS_FROM_DIFFERENT_DEVICES`, M1-24)
- **Зроблено:** Snapshots hint when VRRP Node/member selected — capture each member separately (no silent first child). Semantic diff shows why a-against-b is forbidden (same-device only). RPC error with that code maps to the same why-text.
- **Не чіпали:** `node_id` StartCapture; WriteEnabled; local SemanticDiffEngine; server Compare
- **Перевірка:** Living Spec `Ac4eVrrpPairCaptureIsPerMemberAndCompareShowsCrossDeviceForbidWhy`; `SnapshotViewerViewModelTests`; `SnapshotDiffViewModelTests`; `InventoryOpsSelectionTests`
- **Файли:** `InventoryOpsSelection`, `SnapshotViewerViewModel`, `SnapshotDiffViewModel`, `MainWindow.axaml`

**NEXT (alignment / §3):** **W5-03** ([#344](https://github.com/sesquicadaver/MTDirector/issues/344)) — typed deployment semantic policy diff. W5-02 ManagementPath **DONE**.
