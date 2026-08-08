# MTDirector — ROADMAP реалізації v0.2

**Дата оновлення:** 8 серпня 2026  
**Статус:** нормативний індекс + **лінійна черга** атомарних задач  
**Продукт:** MikroTik Firewall Controller (MTDirector)  
**Базовий коміт аудиту:** M1-08 (tagged session) — черга зсунута на M1-09

Цей документ — **єдиний порядок виконання**. Деталі acceptance, labels і PR titles — у Issue Sets і профільних специфікаціях.  
Кожний пункт = **один PR / один перевірюваний результат / без заглушок**.

Мапінг логічний ID → GitHub: [`ISSUES.md`](ISSUES.md).

---

## 1. Нормативна база

| Джерело | Роль |
|---------|------|
| `TOR-1.md` | Архітектурне рішення |
| `TOR-2.md` | Scope MVP / поза MVP |
| `MVP Technical Specification v0.1.md` | Product-level MUST |
| `MVP End-to-End Workflow and Acceptance Specification v0.1.md` | M6 DoD |
| `Initial Issue Set v0.1.md` | M0–M1 атомарні issues |
| `M2–M6 Implementation Issue Set v0.1.md` | M2–M6 атомарні issues |
| Профільні Specs M1–M5 | hash / adapter / policy / compiler / onboarding / deploy |
| `next-1.md` | Packet-path (N1 weave у MVP) |
| `next-2.md`, Network Rule M7.1 | **Post-MVP** |

**Spine:** `M0 → M1(+N1-01..03) → M2(+N1-04..05) → M3 → M5 → M4(+N1-06) → M6(+N1-07) → MVP CLOSED → M7.*`

**Правила:** M5 перед M4; M7 лише після MVP CLOSED; один issue = один PR; anti-stub DoD з Issue Sets.

**Scope lock MVP:** без NAT/RAW/Mangle/routing/VRRP/bridge/VLAN **writes**; без campaigns, auto-deploy, auto-fix drift, web/mobile, multi-tenant, microservices/Redis/K8s, multi-vendor, SIEM/SOAR у Controller.

---

## 2. Аудит реалізації (2026-08-08)

### 2.1 Прогрес

| Область | Closed | Open | % |
|---------|-------:|-----:|--:|
| M0 Bootstrap | 10 | 0 | 100% |
| M1 Read-only slice | 8 | 26 | 24% |
| N1 Packet-path weave | 0 | 7 | 0% |
| M2–M6 (решта MVP) | 0 | 58 | 0% |
| M7 Post-MVP | 0 | 27 | 0% |
| **Разом** | **18** | **118** | **~13% issues** |

MVP issues (109) = 18 done + **91 remaining** до MVP CLOSED.  
Post-MVP M7 = **27** після MVP.

### 2.2 DONE (не в черзі)

| ID | GitHub | Результат у коді |
|----|-------:|------------------|
| M0-01…M0-10 | #1–#10 | Governance, SDK, boundaries, health Controller, Desktop shell, PG bootstrap, CI, CHR skeleton, ADRs |
| M1-01 | #11 | Inventory domain (`Site`/`Node`/`Device` + topology VO) |
| M1-02 | #12 | Snapshot/capability domain types |
| M1-03 | #13 | PG schema: sites/nodes/devices/profiles/captures/payloads (Vertical Slice §8) |
| M1-04 | #14 | AES-256-GCM connection profiles, trust INTERNAL_CA/SPKI, audit pin-change |
| M1-05 | #15 | Read-only application ports + inventory/snapshot use cases |
| M1-06 | #16 | RouterOS API word-length codec (canonical + fragmented) |
| M1-07 | #17 | RouterOS API sentence streaming parser/encoder |
| M1-08 | #18 | Asynchronous tagged RouterOS API session |

### 2.3 Поточні прогалини (код)

| Збірка | Стан |
|--------|------|
| `Mfc.RouterOs` | word/sentence/session (M1-06..08); немає TLS/discovery |
| `Mfc.Contracts` | лише marker — немає proto gRPC inventory/diff |
| `Mfc.Application` | inventory/snapshot use cases (M1-05); RouterOS port без реалізації |
| `Mfc.Controller` | health-only gRPC + persistence/secrets DI |
| `Mfc.Desktop` | connection shell; **немає** inventory/snapshot/diff UI |
| Persistence | schema snapshots є; EF adapters для inventory/capture — наступні issues |

**NEXT = черга #5:** [M1-09](https://github.com/sesquicadaver/MTDirector/issues/19).

---

## 3. Лінійна черга нереалізованого (єдиний порядок)

Виконувати **строго зверху вниз**. Колонка `#` — позиція в черзі.  
Залежності задовольняються попередніми рядками (топологічний порядок).  
Паралель **не** відкривати, доки не змінено цю політику окремим рішенням.

### 3.A — До MVP CLOSED (95 атомарних задач)

#### Блок A1 — M1 Application + RouterOS protocol

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~1~~ | ~~M1-05~~ | ~~#15~~ | ~~Define read-only application ports and use cases~~ → §2.2 DONE |
| ~~2~~ | ~~M1-06~~ | ~~#16~~ | ~~Implement RouterOS word-length codec~~ → §2.2 DONE |
| ~~3~~ | ~~M1-07~~ | ~~#17~~ | ~~Implement RouterOS sentence encoder and parser~~ → §2.2 DONE |
| ~~4~~ | ~~M1-08~~ | ~~#18~~ | ~~Implement asynchronous tagged RouterOS session~~ → §2.2 DONE |
| 5 | M1-09 | #19 | Implement authenticated TLS RouterOS connection |
| 6 | M1-10 | #20 | Add typed allowlisted RouterOS read executor |

#### Блок A2 — M1 Discovery

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 7 | M1-11 | #21 | Implement system and service discovery |
| 8 | M1-12 | #22 | Implement interface and address discovery |
| 9 | M1-13 | #23 | Implement firewall and address-list discovery |
| 10 | M1-14 | #24 | Implement routing and firewall-dependency discovery |
| 11 | M1-15 | #25 | Implement VRRP discovery |
| 12 | M1-16 | #26 | Implement bridge, VLAN and switch metadata discovery |

#### Блок A3 — N1 (M1 weave) + capabilities / topology

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 13 | N1-01 | #45 | Extend read allowlist: `/container`, `/app`, `/interface/veth`, `/ip/vrf` |
| 14 | M1-17 | #27 | Implement RouterOS capability profile |
| 15 | N1-02 | #46 | Project Container/App→VETH→Bridge→VLAN→VRF topology graph |
| 16 | N1-03 | #47 | Classify packet path CPU / HW-offload / MIXED / INDETERMINATE |
| 17 | M1-18 | #28 | Implement node topology validation |

#### Блок A4 — M1 Snapshots / canonical / diff

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 18 | M1-19 | #29 | Implement stable-read snapshot coordinator |
| 19 | M1-20 | #30 | Implement raw snapshot assembly and redaction |
| 20 | M1-21 | #31 | Implement canonicalization primitives |
| 21 | M1-22 | #32 | Implement menu-specific canonical snapshots |
| 22 | M1-23 | #33 | Persist snapshots and detect identical captures |
| 23 | M1-24 | #34 | Implement deterministic semantic snapshot diff |

#### Блок A5 — M1 API / Desktop / acceptance gate

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 24 | M1-25 | #35 | Add inventory and discovery gRPC services |
| 25 | M1-26 | #36 | Add snapshot and diff gRPC services |
| 26 | M1-27 | #37 | Add desktop inventory tree |
| 27 | M1-28 | #38 | Add desktop snapshot viewer |
| 28 | M1-29 | #39 | Add desktop semantic diff viewer |
| 29 | M1-30 | #40 | Add standalone CHR vertical-slice acceptance test |
| 30 | M1-31 | #41 | Add multi-WAN CHR vertical-slice acceptance test |
| 31 | M1-32 | #42 | Add VRRP CHR vertical-slice acceptance test |
| 32 | M1-33 | #43 | Add protocol and snapshot fault-injection suite |
| 33 | M1-34 | #44 | Complete read-only vertical-slice acceptance (**M1 CLOSED**) |

#### Блок A6 — M2 Policy core (+ N1)

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 34 | M2-01 | #48 | Implement policy document lifecycle and persistence |
| 35 | M2-02 | #49 | Implement fixed Policy Pipeline v1 and chain contracts |
| 36 | M2-03 | #50 | Implement address objects and selectors |
| 37 | M2-04 | #51 | Implement service objects and selectors |
| 38 | M2-05 | #52 | Implement logical zones and Node bindings |
| 39 | N1-05 | #67 | Bind zones to VETH/VLAN/bridge without ContainerPolicy entities |
| 40 | M2-06 | #53 | Implement policy rules, predicates and effects |
| 41 | M2-07 | #54 | Implement deterministic policy composition |
| 42 | M2-08 | #55 | Implement temporary deny-stage exceptions |
| 43 | M2-09 | #56 | Implement normalized predicate algebra |
| 44 | M2-10 | #57 | Implement structural and satisfiability analysis |
| 45 | M2-11 | #58 | Implement duplicate, shadow and overlap analysis |
| 46 | M2-12 | #59 | Implement actual RouterOS filter-context analysis |
| 47 | N1-04 | #66 | Emit `PACKET_PATH_BYPASSES_IP_FIREWALL` / `PACKET_PATH_NOT_PROVEN` blockers |
| 48 | M2-13 | #60 | Implement management-path safety validation |
| 49 | M2-14 | #61 | Implement topology and dependency safety validation |
| 50 | M2-15 | #62 | Implement FastTrack policy validation |
| 51 | M2-16 | #63 | Implement policy tests, semantic diff and risk classification |
| 52 | M2-17 | #64 | Implement approval and desired-binding workflow |
| 53 | M2-18 | #65 | Add policy authoring and review desktop workflow (**M2 CLOSED**) |

#### Блок A7 — M3 Compiler

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 54 | M3-01 | #68 | Implement RouterOS filter artifact model |
| 55 | M3-02 | #69 | Implement managed chain namespace and layout |
| 56 | M3-03 | #70 | Compile content-addressed address lists |
| 57 | M3-04 | #71 | Compile zones and service variants |
| 58 | M3-05 | #72 | Compile supported matchers and regular effects |
| 59 | M3-06 | #73 | Compile FastTrack and terminal rules |
| 60 | M3-07 | #74 | Implement per-Device compiler orchestration and artifact storage |
| 61 | M3-08 | #75 | Complete compiler integration and acceptance (**M3 CLOSED**) |

#### Блок A8 — M5 Onboarding (перед M4)

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 62 | M5-01 | #76 | Implement onboarding domain model and persistence |
| 63 | M5-02 | #77 | Implement onboarding prerequisite validation |
| 64 | M5-03 | #78 | Implement management guard verification |
| 65 | M5-04 | #79 | Implement explicit anchor placement planning |
| 66 | M5-05 | #80 | Implement onboarding write adapter and bootstrap artifact |
| 67 | M5-06 | #81 | Implement scheduler proof and onboarding watchdog |
| 68 | M5-07 | #82 | Implement onboarding execution and verification |
| 69 | M5-08 | #83 | Implement onboarding rollback and crash recovery |
| 70 | M5-09 | #84 | Expose onboarding API and desktop workflow |
| 71 | M5-10 | #85 | Complete onboarding integration acceptance (**M5 CLOSED**) |

#### Блок A9 — M4 Safe deployment (+ N1-06)

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 72 | M4-01 | #86 | Implement deployment plan, states and persistence |
| 73 | N1-06 | #99 | Block deploy when packet-path blockers present |
| 74 | M4-02 | #87 | Implement restricted deployment writer and managed-state reader |
| 75 | M4-03 | #88 | Implement address-list create-or-verify staging |
| 76 | M4-04 | #89 | Implement detached chain staging and verification |
| 77 | M4-05 | #90 | Implement production rollback watchdog |
| 78 | M4-06 | #91 | Implement transition-state validation and anchor activation |
| 79 | M4-07 | #92 | Implement deployment probes and post-activation verification |
| 80 | M4-08 | #93 | Implement standalone Node deployment coordinator |
| 81 | M4-09 | #94 | Implement multi-WAN deployment verification |
| 82 | M4-10 | #95 | Implement VRRP deployment coordinator |
| 83 | M4-11 | #96 | Implement rollback and crash recovery |
| 84 | M4-12 | #97 | Expose deployment API and desktop operation workflow |
| 85 | M4-13 | #98 | Complete deployment fault and security acceptance (**M4 CLOSED**) |

#### Блок A10 — M6 E2E (+ N1-07) → MVP CLOSED

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 86 | M6-01 | #100 | Implement desired, committed and actual state projection |
| 87 | M6-02 | #101 | Implement managed drift detection |
| 88 | N1-07 | #109 | E2E/drift acceptance for container/VLAN/VETH/HW path classes |
| 89 | M6-03 | #102 | Implement bounded operational background jobs |
| 90 | M6-04 | #103 | Integrate final desktop workflows |
| 91 | M6-05 | #104 | Complete standalone and dual-stack end-to-end acceptance |
| 92 | M6-06 | #105 | Complete multi-WAN end-to-end acceptance |
| 93 | M6-07 | #106 | Complete VRRP and CRS end-to-end acceptance |
| 94 | M6-08 | #107 | Complete security, backup and restore acceptance |
| 95 | M6-09 | #108 | Complete MVP release acceptance (**MVP CLOSED**) |

---

### 3.B — Post-MVP M7 (27 атомарних задач; лише після #95)

#### Блок B1 — M7.1 Routing / path assurance

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 96 | M7.1-01 | #110 | Extend read allowlist: routing tables/settings/rules, VRF, route filters |
| 97 | M7.1-02 | #111 | Persist RoutingAssuranceState (config + operational) |
| 98 | M7.1-03 | #112 | Implement RouteResolutionTrace |
| 99 | M7.1-04 | #113 | Model ECMP as ONE_OF bounded next-hop sets |
| 100 | M7.1-05 | #114 | Analyze dynamic route origins (read-only) |
| 101 | M7.1-06 | #115 | Implement RouteExpectation declarations and evaluation |
| 102 | M7.1-07 | #116 | Implement reverse-path symmetry analysis |
| 103 | M7.1-08 | #117 | Bind NetworkPathProfile latency probes to routing result |
| 104 | M7.1-09 | #118 | Classify routing configuration vs operational drift |
| 105 | M7.1-10 | #119 | Desktop: routing assurance / expectation viewers |
| 106 | M7.1-11 | #120 | CHR acceptance: multi-WAN recursive, ECMP, VRF (**M7.1 CLOSED**) |

#### Блок B2 — M7.2 Endpoint presence

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 107 | M7.2-01 | #121 | Endpoint attribution resolver |
| 108 | M7.2-02 | #122 | EndpointPresenceInterval and EndpointRoutingContext |
| 109 | M7.2-03 | #123 | Mobility: invalidate assessment, recompute traces |
| 110 | M7.2-04 | #124 | CHR/fixture acceptance for migration (**M7.2 CLOSED**) |

#### Блок B3 — M7.3 External correlation contract

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 111 | M7.3-01 | #125 | Define IncidentSignal ingress contract |
| 112 | M7.3-02 | #126 | Historical ActiveStateInterval resolver by occurred_at |
| 113 | M7.3-03 | #127 | On-demand connection-tracking / session context |
| 114 | M7.3-04 | #128 | Correlate sensor observation with RouteResolutionTrace |
| 115 | M7.3-05 | #129 | Emit visibility_status / confidence on every assessment |
| 116 | M7.3-06 | #130 | Contract tests IncidentSignal ↔ ResponseAssessment (**M7.3 CLOSED**) |

#### Блок B4 — M7.4 Incident enforcement

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| 117 | M7.4-01 | #131 | Add INCIDENT_PRE_STATE_DENY / INCIDENT_DENY_OVERLAY to pipeline |
| 118 | M7.4-02 | #132 | ResponseIntent → ResponseAssessment feasibility matrix |
| 119 | M7.4-03 | #133 | Compile/deploy overlay via existing M3/M4 |
| 120 | M7.4-04 | #134 | TTL expiry → mandatory removal plan |
| 121 | M7.4-05 | #135 | Feedback events RESPONSE_* to external complex |
| 122 | M7.4-06 | #136 | E2E: enforceable / not-enforceable / rollback / residual risk |

**Кінець черги:** 118 відкритих атомарних задач (#5…#122; #1–#4 → §2.2 DONE).

---

## 4. Підсумок лічильників

| Сегмент | У черзі | Примітка |
|---------|--------:|----------|
| До MVP CLOSED | 91 | M1 залишок + N1 + M2–M6 |
| Post-MVP M7 | 27 | лише після M6-09 |
| **Нереалізовано разом** | **118** | GitHub OPEN |
| Вже CLOSED | 18 | M0 + M1-01…08 — поза чергою |

---

## 5. Living Specification — матриця ТЗ → модуль → тести

| ТЗ / вимога | Фаза | Тести (мінімум) | Статус |
|-------------|------|-----------------|--------|
| Bootstrap / boundaries / PG / CI | M0 | unit arch; integration health/PG | **DONE** |
| Inventory domain + snapshot VO | M1-01…02 | unit domain | **DONE** |
| Inventory/snapshot schema + secrets | M1-03…04 | PG constraints; ciphertext-only | **DONE** |
| Application ports / use cases | M1-05 | unit ports; typed errors | **DONE** |
| Word-length codec | M1-06 | normative vectors; round-trip | **DONE** |
| Sentence codec | M1-07 | fragmented frames; reply fixtures | **DONE** |
| Tagged session | M1-08 | concurrent tags; stress routing | **DONE** |
| API-SSL read adapter + discovery | M1-09…17 | unit TLS/login; CHR later | TODO #5 |
| Packet-path / HW-offload | N1-01…03 | fixtures path classes | TODO |
| Canonical hash + semantic diff | M1-19…24 | Canonical vectors; diff unit | TODO |
| gRPC + Desktop read-only UI | M1-25…29 | contract + UI smoke | TODO |
| M1 acceptance gate | M1-30…34 | CHR suites | TODO |
| Policy compose + analysis | M2 | analysis unit; SoD | TODO |
| Deterministic filter artifact | M3 | golden artifacts | TODO |
| Anchor bootstrap | M5 | equivalence; crash recovery | TODO |
| Watchdog deploy / rollback | M4 | fault-injection; VRRP | TODO |
| Drift + E2E DoD | M6 | E2E §E2E | TODO |
| Routing assurance | M7.1 | RouteResolutionTrace fixtures | TODO post-MVP |
| Incident overlay | M7.4 | feasibility; TTL removal | TODO post-MVP |

Оновлювати рядок **Статус** і зсувати «NEXT» при закритті кожного issue з §3.

---

## 6. Заборонені обхідні рішення

- generic RouterOS writer / raw command API «для тестів»;
- compile без актуального analysis;
- Safe Mode замість watchdog;
- partial VRRP onboard/deploy;
- automatic drift repair;
- auto-create management guard / users / api-ssl;
- campaigns у MVP;
- заглушки `pass` / `NotImplemented` / вимкнені тести;
- винесення задач з лінійної черги «вперед» без закритих попередників.

---

## 7. Операційний старт

1. Відкрити **чергу #5** → [M1-09 / issue #19](https://github.com/sesquicadaver/MTDirector/issues/19).  
2. Після merge — закреслити рядок у §3 (або перенести в §2.2 DONE) і взяти наступний `#`.  
3. Не стартувати M2, доки не закрито **M1-34** (черга #33).  
4. Не стартувати M4, доки не закрито **M5-10** (черга #71).  
5. Не стартувати M7, доки не закрито **M6-09** (черга #95).

Деталі acceptance: `Initial Issue Set v0.1.md`, `M2–M6 Implementation Issue Set v0.1.md`.  
Milestones: https://github.com/sesquicadaver/MTDirector/milestones
