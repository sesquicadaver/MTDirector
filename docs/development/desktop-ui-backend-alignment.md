# Desktop UI ↔ backend alignment plan

**Дата:** 2026-08-30  
**Мета:** UI показує **реальні дані**, які Controller вже віддає (або Desktop уже тримає в VM), без «хеш-лише» Diff, без UUID-ритуалу там, де є selection, і з чесною VRRP-поверхнею.  
**Поза scope цього плану:** нова доменна аналітика без Contracts (ManagementPath Desktop panel тощо) — лише якщо позначено **P3**.

Суміжність з лабою: [`~/gns3-lab/VERIFICATION-MATRIX.md`](file:///home/sesquicadaver/gns3-lab/VERIFICATION-MATRIX.md) (A/B/C/D). Цей документ — **product UI wiring**, не lab provision.

## Класифікація gap

| Клас | Значення | Типовий обсяг |
|------|----------|----------------|
| **P0** | Дані вже в VM/DTO — axaml не біндить | 1 PR / модуль |
| **P1** | Wire є — Desktop губить/сплющує поля | 1 PR mapping+UI |
| **P2** | RPC є — немає client виклику / selection glue / Watch | 1–2 PR |
| **P3** | Немає Contracts/наповнення (напр. порожні `vrrp_role_labels` з бекенду) | PLAN issue |

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
- **Не чіпали:** local SemanticDiffEngine; capture RPC (W3.1)
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
- **Не чіпали:** Watch streams (W3.3); багатший Contracts policy diff (P3)
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

### W2.1 Diff / Snapshot record fidelity
- Опційно мапити `DiffEntry.Before`/`After` SnapshotRecord у detail (wire уже є)
- Warnings pagination / truncate policy

### W2.2 Routing assurance detail
- Розгорнути next-hop / subject поля з proto замість одного SummaryLine (де корисно)

### W2.3 VRRP labels pipeline — **DONE**
- **Дані:** last completed capture canonical `ha.vrrp` **observations** (`role` + `group`); proto mapper already copies `VrrpRoleLabels`
- **Зроблено:** `GetNodeUseCase` проєктує labels через `DeviceVrrpRoleLabelProjector`; без snapshot / без `role` → порожньо (не вигадуємо Master/Backup)
- **Не чіпали:** live RouterOS probe на GetNode; version/model/reachability (окремий projector)
- **Перевірка:** Living Spec `VrrpRoleLabelsLivingSpecTests`; `GetNodeMapsVrrpRoleLabelsFromLastCaptureObservations`
- **Файли:** `DeviceVrrpRoleLabelProjector.cs`, `InventoryUseCases.cs`, `ViewMapper.cs`

**Exit W2 (partial):** VRRP roles заповнюються з last capture observations. W2.1/W2.2 лишаються TODO.

---

## Хвиля 3 — P2 glue: викликати наявні RPC з Desktop

Порядок за операторським шляхом:

| Крок | RPC / client | UI |
|------|----------------|-----|
| W3.1 | `StartCapture` + `WatchCapture` | Snapshots: кнопка Capture + progress |
| W3.2 | `ValidateDeviceConnection` | Add router / Inventory «Probe» |
| W3.3 | Onboarding/Deploy `Watch` streams | Progress live, не лише snapshot після Start |
| W3.4 | `GetNodeWorkflow` | Node/Inventory workflow без дублювання ad-hoc |
| W3.5 | Zones `UpdateZoneDefinition`, `ResolveZonesForDevice` | Edit zone; resolve device |
| W3.6 | Policy: `UpdateRule`/`DeleteRule`/`AcknowledgeWarning`/`CompileNodeFilterArtifacts` | за пріоритетом review/deploy loop |
| W3.7 | Drift `GetDriftEvent` | якщо list недостатній для payload |

**Exit W3:** Capture/Probe/Watch з Desktop без grpcurl; Zones/Policy mutate paths доступні з UI.

---

## Хвиля 4 — VRRP structured UX (P2 UI + можливий P3 data)

1. **Node-centric shell** для `NodeKind.Vrrp`: members table (a/b), role, mgmt host, last capture
2. Selection model: ops на **Node** (pair) з drill-down member; Deploy не «перший child» мовчки
3. Wizard: опція create VRRP node + register 2 devices
4. Pair capture / compare guidance (per-member captures; cross-device compare лишається forbid by design — показати why)

Залежить від W2.3 (labels) і W3.1 (capture).

---

## Хвиля 5 — P3 лише за PLAN (не «align existing»)

- `ListPolicies` / catalog browse (немає в proto)
- Desktop surface для ManagementPath / FastTrack analysis (немає RPC)
- Багатий deployment semantic policy diff у Contracts замість `repeated string`
- CRS / physical lab

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
W3.1 StartCapture Desktop
W3.2 ValidateConnection
W3.3 Watch streams
W4  VRRP Node shell
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
| W2.1–W2.2, W3–W5 | **TODO** |

**NEXT (alignment):** W3.1 StartCapture + WatchCapture з Desktop.
