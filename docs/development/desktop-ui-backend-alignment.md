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

### W1.1 Diff: field-level changes *(блокер скарги «лише хеші»)*
- **Дані:** `SnapshotDiffEntryItem.FieldLines`, `SnapshotDiffViewModel.Warnings`
- **Зробити:** у Semantic diff ItemTemplate — список `FieldLines.Summary`; блок Warnings
- **Не чіпати:** local SemanticDiffEngine
- **Перевірка:** Compare seed captures після day-1 tweak → видно `comment: … → …` / field diffs, не лише `RecordKey`
- **Файли:** `MainWindow.axaml`, опційно Living Spec string AC

### W1.2 Snapshots: повні поля запису
- **Дані:** `SnapshotRecordListItem.Fields` (зараз UI = `SummaryLine` ≤4)
- **Зробити:** detail pane / expand selected record → усі fields
- **Перевірка:** filter rule з day-1 показує chain/action/comment повністю

### W1.3 Policies: обʼєкти / contracts / DiffLines + selection glue
- **Дані вже в VM:** `AddressObjects`, `ServiceObjects`, `ChainContracts`, `DiffLines` + команди Upsert*/Replace*/Reorder*/RecordAnalysis
- **Зробити:**
  1. Секції списків у Policies axaml
  2. Compose: default `ComposeNodeIdText` ← `Inventory.SelectedNode` (Node)
  3. Після Create draft — уже є revision id; Load лишається, але не єдиний шлях
- **Перевірка:** Create draft → видно address/service/contracts після upsert; Compose без ручного UUID при вибраному Node

### W1.4 Deploy / Onboarding: приховані колекції
- Deploy: bind `ArtifactLines`, `OrderLines`, `ProbeAndWatchdogLines` (зараз лише слабкі `SemanticDiffLines`)
- Onboarding: bind `Placements`
- Підпис SemanticDiffLines чесно: «artifact hash delta», доки немає багатшого Contracts diff

### W1.5 Drift: findings зі list response
- Не губити `DriftFinding` у DTO; показати list під подією (kind/severity/detail)
- `SemanticDiffText` лишається, але не єдине джерело змісту

### W1.6 Inventory/Node: явні device поля
- Окремо (не лише `DetailSummary`): reachability, model, ROS version, **VRRP roles** (коли непорожні), last snapshot

**Exit W1:** оператор на GNS3 seed бачить зміну правила в Diff як поля; Policies не «порожня форма»; Deploy/Onboarding показують plan details.

---

## Хвиля 2 — P1 якість mapping (без нових RPC)

### W2.1 Diff / Snapshot record fidelity
- Опційно мапити `DiffEntry.Before`/`After` SnapshotRecord у detail (wire уже є)
- Warnings pagination / truncate policy

### W2.2 Routing assurance detail
- Розгорнути next-hop / subject поля з proto замість одного SummaryLine (де корисно)

### W2.3 VRRP labels pipeline *(може ескалюватись у P3)*
1. Трасувати Controllers `GetNode` → чи `vrrp_role_labels` коли-небудь non-empty
2. Якщо discovery є, а ViewMapper/`Device` view завжди `[]` → **backend fill** (P3/PLAN), потім UI badge (W1.6)
3. Node view: список members з ролями, не «перший device»

**Exit W2:** Diff detail = rule semantics; VRRP roles або заповнені, або явний PLAN на backend hole.

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
W1.1 Diff FieldLines     ← зробити першим (доводить «бекенд уже вміє»)
W1.2 Snapshot Fields
W1.3 Policies bind+Compose selection
W1.4 Deploy/Onboarding collections
W1.5 Drift findings
W1.6 Inventory device fields
W2.3 VRRP labels audit → fix or PLAN
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
| W1.1–W1.6 | **TODO** |
| W2–W5 | **TODO** |

Lab read-path (captures/Diff RPC) уже дає дані для **W1.1** на seed — блокер лише UI.
