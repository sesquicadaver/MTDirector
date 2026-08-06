# MTDirector — ROADMAP реалізації v0.1

**Дата:** 6 серпня 2026  
**Статус:** нормативний індекс атомарних задач реалізації  
**Продукт:** MikroTik Firewall Controller (MTDirector)

Цей документ — **єдиний порядок виконання**. Деталі acceptance, labels і PR titles — у нормативних Issue Sets і профільних специфікаціях. Кожний пункт = **один PR / один перевірюваний результат / без заглушок**.

---

## 1. Нормативна база

| Джерело | Роль |
|---------|------|
| `TOR-1.md` | Архітектурне рішення |
| `TOR-2.md` | Scope MVP / поза MVP |
| `MVP Technical Specification v0.1.md` | Product-level MUST (надмножина; overrides — у E2E) |
| `MVP End-to-End Workflow and Acceptance Specification v0.1.md` | M6 DoD; **пізніша профільна перемагає** |
| `Initial Issue Set v0.1.md` | M0–M1 атомарні issues |
| `M2–M6 Implementation Issue Set v0.1.md` | M2–M6 атомарні issues |
| Профільні Specs M1–M5 | Контракти hash/adapter/policy/compiler/onboarding/deploy |
| `next-1.md` | Packet-path (container/VLAN/VETH/HW) — **вплітання в MVP** |
| `next-2.md`, `Network Rule…М7.1.md` | **Post-MVP** |

---

## 2. Критичний шлях (spine)

```text
M0 → M1 → M2 → M3 → M5 → M4 → M6 → MVP CLOSED
                              ↑
                    N1 weave-in (packet-path)
                              ↓
                    M7.1 → M7.2 → M7.3 → M7.4  (post-MVP)
```

**Правило:** M5 перед M4 (потрібні permanent anchors + bootstrap artifact).  
**Паралель** дозволена лише коли всі залежності issue закриті.

**DoD кожного issue:** див. §4 Initial Issue Set / §3 M2–M6 Issue Set (anti-stub, locked restore, Release без warnings, CancellationToken, secrets redaction, один PR).

---

## 3. Scope lock MVP

**Заборонено в M0–M6 без окремої специфікації:** NAT/RAW/Mangle/routing/VRRP/bridge/VLAN writes; switch ACL; campaigns; auto-deploy; auto-fix drift; web/mobile; multi-tenant; microservices/Redis/K8s; multi-vendor; SIEM/SOAR всередині Controller.

---

## 4. Фаза M0 — Repository Bootstrap (10)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M0-01 | Initialize repository governance | — | Bootstrap Plan |
| M0-02 | Pin .NET toolchain and package management | M0-01 | Bootstrap Plan |
| M0-03 | Create solution and enforce project references | M0-02 | Bootstrap Plan |
| M0-04 | Add architecture boundary tests | M0-03 | Bootstrap Plan |
| M0-05 | Add health-only controller host | M0-03 | Bootstrap Plan |
| M0-06 | Add desktop controller connection shell | M0-05 | Bootstrap Plan |
| M0-07 | Add PostgreSQL bootstrap persistence | M0-03 | Bootstrap Plan |
| M0-08 | Add deterministic CI pipelines | M0-04, M0-05, M0-06, M0-07 | Bootstrap Plan |
| M0-09 | Add isolated CHR testlab skeleton | M0-08 | Bootstrap Plan |
| M0-10 | Record initial ADRs and development documentation | M0-03, M0-05, M0-07, M0-09 | Bootstrap Plan |

**Gate M0 CLOSED:** health Desktop→gRPC→Controller→PostgreSQL; CI green; CHR skeleton; ADRs.

---

## 5. Фаза M1 — Read-Only Vertical Slice (34 + N1 weave)

Після M0 — два паралельні потоки до злиття на M1-10.

### 5.1 Domain / Persistence

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M1-01 | Implement inventory domain model | M0 CLOSED | Vertical Slice |
| M1-02 | Implement snapshot and capability domain types | M1-01 | Vertical Slice, Canonical |
| M1-03 | Add inventory and snapshot persistence schema | M1-01, M1-02, M0-07 | Vertical Slice, Canonical |
| M1-04 | Add secure RouterOS connection profiles | M1-03, M0-07 | Vertical Slice, Adapter |
| M1-05 | Define read-only application ports and use cases | M1-01—M1-04 | Vertical Slice |

### 5.2 RouterOS Protocol

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M1-06 | Implement RouterOS word-length codec | M0 CLOSED | Adapter |
| M1-07 | Implement RouterOS sentence encoder and parser | M1-06 | Adapter |
| M1-08 | Implement asynchronous tagged RouterOS session | M1-07 | Adapter |
| M1-09 | Implement authenticated TLS RouterOS connection | M1-04, M1-08 | Adapter |
| M1-10 | Add typed allowlisted RouterOS read executor | M1-09, M1-05 | Adapter |

### 5.3 Discovery

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M1-11 | Implement system and service discovery | M1-10 | Adapter |
| M1-12 | Implement interface and address discovery | M1-10 | Adapter |
| M1-13 | Implement firewall and address-list discovery | M1-10 | Adapter |
| M1-14 | Implement routing and firewall-dependency discovery | M1-10 | Adapter |
| M1-15 | Implement VRRP discovery | M1-10, M1-12 | Adapter |
| M1-16 | Implement bridge, VLAN and switch metadata discovery | M1-10, M1-12 | Adapter, next-1 |

### 5.4 Capabilities / Topology / Snapshots / Diff

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M1-17 | Implement RouterOS capability profile | M1-11—M1-16 | Adapter |
| M1-18 | Implement node topology validation | M1-17, M1-01 | Vertical Slice |
| M1-19 | Implement stable-read snapshot coordinator | M1-11—M1-18 | Adapter, Canonical |
| M1-20 | Implement raw snapshot assembly and redaction | M1-19 | Adapter, Canonical |
| M1-21 | Implement canonicalization primitives | M1-20 | Canonical (MFC-CJ1) |
| M1-22 | Implement menu-specific canonical snapshots | M1-21 | Canonical |
| M1-23 | Persist snapshots and detect identical captures | M1-03, M1-22 | Canonical |
| M1-24 | Implement deterministic semantic snapshot diff | M1-22, M1-23 | Canonical |

### 5.5 API / Desktop / Acceptance

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M1-25 | Add inventory and discovery gRPC services | M1-05, M1-18, M1-23 | Vertical Slice |
| M1-26 | Add snapshot and diff gRPC services | M1-24, M1-25 | Vertical Slice |
| M1-27 | Add desktop inventory tree | M1-25 | Vertical Slice |
| M1-28 | Add desktop snapshot viewer | M1-26, M1-27 | Vertical Slice |
| M1-29 | Add desktop semantic diff viewer | M1-24, M1-26, M1-28 | Vertical Slice |
| M1-30 | Add standalone CHR vertical-slice acceptance test | M1-11—M1-29 | Vertical Slice |
| M1-31 | Add multi-WAN CHR vertical-slice acceptance test | M1-30 | Vertical Slice |
| M1-32 | Add VRRP CHR vertical-slice acceptance test | M1-30 | Vertical Slice |
| M1-33 | Add protocol and snapshot fault-injection suite | M1-30—M1-32 | Vertical Slice |
| M1-34 | Complete read-only vertical-slice acceptance | M1-01—M1-33, N1-01—N1-03 | Vertical Slice, next-1 |

### 5.6 N1 weave-in (M1) — packet-path inventory

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| N1-01 | Extend read allowlist: `/container`, `/app`, `/interface/veth`, `/ip/vrf` | M1-10 | next-1, Adapter |
| N1-02 | Project Container/App→VETH→Bridge→VLAN→VRF topology graph | N1-01, M1-16 | next-1, Canonical |
| N1-03 | Classify packet path CPU / HW-offload / MIXED / INDETERMINATE in snapshot | N1-02, M1-17 | next-1 |

**Gate M1 CLOSED:** end-to-end read-only Desktop→…→diff; zero RouterOS writes; CHR suites green; N1-01—N1-03 merged.

---

## 6. Фаза M2 — Policy Core (18 + N1 weave)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M2-01 | Implement policy document lifecycle and persistence | M1 CLOSED | Policy Model |
| M2-02 | Implement fixed Policy Pipeline v1 and chain contracts | M2-01 | Policy Model |
| M2-03 | Implement address objects and selectors | M2-01 | Policy Model |
| M2-04 | Implement service objects and selectors | M2-01 | Policy Model |
| M2-05 | Implement logical zones and Node bindings | M2-01, M1-18 | Policy Model |
| M2-06 | Implement policy rules, predicates and effects | M2-02—M2-05 | Policy Model |
| M2-07 | Implement deterministic policy composition | M2-02—M2-06 | Policy Model |
| M2-08 | Implement temporary deny-stage exceptions | M2-07 | Policy Model |
| M2-09 | Implement normalized predicate algebra | M2-03—M2-06 | Policy Model |
| M2-10 | Implement structural and satisfiability analysis | M2-06, M2-09 | Policy Model |
| M2-11 | Implement duplicate, shadow and overlap analysis | M2-09, M2-10 | Policy Model |
| M2-12 | Implement actual RouterOS filter-context analysis | M2-11, M1-24 | Policy Model |
| M2-13 | Implement management-path safety validation | M2-12 | Policy Model |
| M2-14 | Implement topology and dependency safety validation | M2-12, M1-18, N1-04 | Policy Model, next-1 |
| M2-15 | Implement FastTrack policy validation | M2-14 | Policy Model |
| M2-16 | Implement policy tests, semantic diff and risk classification | M2-11—M2-15 | Policy Model |
| M2-17 | Implement approval and desired-binding workflow | M2-16 | Policy Model |
| M2-18 | Add policy authoring and review desktop workflow | M2-17 | Policy Model, E2E |

### N1 weave-in (M2)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| N1-04 | Emit `PACKET_PATH_BYPASSES_IP_FIREWALL` / `PACKET_PATH_NOT_PROVEN` blockers | N1-03, M2-12 | next-1 |
| N1-05 | Bind zones to VETH/VLAN/bridge without ContainerPolicy entities | M2-05, N1-02 | next-1 |

**Gate M2 CLOSED:** compose→analyze→approve; immutable APPROVED; SoD; N1-04/N1-05.

---

## 7. Фаза M3 — Policy Compiler (8)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M3-01 | Implement RouterOS filter artifact model | M2 CLOSED | Compiler |
| M3-02 | Implement managed chain namespace and layout | M3-01 | Compiler |
| M3-03 | Compile content-addressed address lists | M3-01, M2-03 | Compiler |
| M3-04 | Compile zones and service variants | M3-03, M2-04, M2-05 | Compiler |
| M3-05 | Compile supported matchers and regular effects | M3-02, M3-04 | Compiler |
| M3-06 | Compile FastTrack and terminal rules | M3-05, M2-15 | Compiler |
| M3-07 | Implement per-Device compiler orchestration and artifact storage | M3-01—M3-06 | Compiler |
| M3-08 | Complete compiler integration and acceptance | M3-07 | Compiler |

**Gate M3 CLOSED:** pure-function compile; deterministic `RouterOsFilterArtifact`; no RouterOS transport in compiler.

---

## 8. Фаза M5 — Managed Device Onboarding (10)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M5-01 | Implement onboarding domain model and persistence | M3 CLOSED | Onboarding |
| M5-02 | Implement onboarding prerequisite validation | M5-01, M1 CLOSED | Onboarding |
| M5-03 | Implement management guard verification | M5-02, M2-13 | Onboarding |
| M5-04 | Implement explicit anchor placement planning | M5-03 | Onboarding |
| M5-05 | Implement onboarding write adapter and bootstrap artifact | M5-04 | Onboarding |
| M5-06 | Implement scheduler proof and onboarding watchdog | M5-05 | Onboarding |
| M5-07 | Implement onboarding execution and verification | M5-01—M5-06 | Onboarding |
| M5-08 | Implement onboarding rollback and crash recovery | M5-07 | Onboarding |
| M5-09 | Expose onboarding API and desktop workflow | M5-08 | Onboarding, E2E |
| M5-10 | Complete onboarding integration acceptance | M5-09 | Onboarding |

**Gate M5 CLOSED:** UNMANAGED→MANAGED; semantic equivalence; bootstrap as old targets for first deploy; no auto users/guard/api-ssl.

---

## 9. Фаза M4 — Safe Deployment (13 + N1 weave)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M4-01 | Implement deployment plan, states and persistence | M5 CLOSED | Safe Deployment |
| M4-02 | Implement restricted deployment writer and managed-state reader | M4-01 | Safe Deployment |
| M4-03 | Implement address-list create-or-verify staging | M4-02, M3 CLOSED | Safe Deployment, Compiler |
| M4-04 | Implement detached chain staging and verification | M4-03 | Safe Deployment |
| M4-05 | Implement production rollback watchdog | M4-04 | Safe Deployment |
| M4-06 | Implement transition-state validation and anchor activation | M4-05, M2 CLOSED | Safe Deployment |
| M4-07 | Implement deployment probes and post-activation verification | M4-06 | Safe Deployment |
| M4-08 | Implement standalone Node deployment coordinator | M4-01—M4-07 | Safe Deployment |
| M4-09 | Implement multi-WAN deployment verification | M4-08 | Safe Deployment |
| M4-10 | Implement VRRP deployment coordinator | M4-08 | Safe Deployment |
| M4-11 | Implement rollback and crash recovery | M4-08—M4-10 | Safe Deployment |
| M4-12 | Expose deployment API and desktop operation workflow | M4-11 | Safe Deployment, E2E |
| M4-13 | Complete deployment fault and security acceptance | M4-12, N1-06 | Safe Deployment, next-1 |

### N1 weave-in (M4)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| N1-06 | Block deploy when packet-path blockers present | N1-04, M4-01 | next-1, Safe Deployment |

**Gate M4 CLOSED:** one Node per effectful op; watchdog mandatory; COMMITTED durable; VRRP backups-first; no Safe Mode as sole rollback.

---

## 10. Фаза M6 — End-to-End Integration (9 + N1 weave)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M6-01 | Implement desired, committed and actual state projection | M4 CLOSED | E2E |
| M6-02 | Implement managed drift detection | M6-01, M1 CLOSED | E2E |
| M6-03 | Implement bounded operational background jobs | M6-02 | E2E |
| M6-04 | Integrate final desktop workflows | M6-01—M6-03 | E2E |
| M6-05 | Complete standalone and dual-stack end-to-end acceptance | M6-04 | E2E |
| M6-06 | Complete multi-WAN end-to-end acceptance | M6-04 | E2E |
| M6-07 | Complete VRRP and CRS end-to-end acceptance | M6-04 | E2E |
| M6-08 | Complete security, backup and restore acceptance | M6-05—M6-07 | E2E |
| M6-09 | Complete MVP release acceptance | M6-08, N1-07 | E2E, next-1 |

### N1 weave-in (M6)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| N1-07 | E2E/drift acceptance for container/VLAN/VETH/HW path classes | N1-01—N1-06, M6-02 | next-1, E2E |

**Gate MVP CLOSED:** повний цикл inventory→policy→onboard→deploy→drift без direct RouterOS з Desktop; усі E2E gates; N1-07.

---

## 11. Підсумок MVP

| Фаза | Issues | Примітка |
|------|-------:|----------|
| M0 | 10 | Bootstrap |
| M1 | 34 | Read-only slice |
| N1 (MVP weave) | 7 | Packet-path (next-1) |
| M2 | 18 | Policy core |
| M3 | 8 | Compiler |
| M5 | 10 | Onboarding |
| M4 | 13 | Safe deploy |
| M6 | 9 | E2E |
| **Разом MVP** | **109** | 102 з Issue Sets + 7 N1 |

---

## 12. Post-MVP — M7 Network / Assurance / Incident

Нових write-path для routing немає. Enforcement = існуючий M2→M3→M4 pipeline.

### 12.1 M7.1 — Network Rule, Routing and Path Assurance

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M7.1-01 | Extend read allowlist: routing tables/settings/rules, VRF, route filters | MVP CLOSED | M7.1 |
| M7.1-02 | Persist RoutingAssuranceState (config + operational) | M7.1-01 | M7.1 |
| M7.1-03 | Implement RouteResolutionTrace (policy routing → FIB → recursive NH) | M7.1-02 | M7.1 |
| M7.1-04 | Model ECMP as ONE_OF bounded next-hop sets | M7.1-03 | M7.1 |
| M7.1-05 | Analyze dynamic route origins (BGP/OSPF/DHCP/VPN) as read-only | M7.1-02 | M7.1 |
| M7.1-06 | Implement RouteExpectation declarations and evaluation | M7.1-03 | M7.1 |
| M7.1-07 | Implement reverse-path symmetry analysis | M7.1-03 | M7.1 |
| M7.1-08 | Bind NetworkPathProfile latency probes to routing result | M7.1-06, M4-07 | M7.1 |
| M7.1-09 | Classify routing configuration vs operational drift | M7.1-02, M6-02 | M7.1 |
| M7.1-10 | Desktop: routing assurance / expectation viewers | M7.1-06—M7.1-09 | M7.1 |
| M7.1-11 | CHR acceptance: multi-WAN recursive, ECMP, VRF, expectation fail | M7.1-10 | M7.1 |

### 12.2 M7.2 — Endpoint Presence and Mobility

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M7.2-01 | Endpoint attribution resolver (IP→MAC→VLAN→port→VETH/VPN) | M7.1 CLOSED | next-2, M7.2 |
| M7.2-02 | EndpointPresenceInterval and EndpointRoutingContext | M7.2-01, M7.1-03 | M7.2 |
| M7.2-03 | Mobility: invalidate assessment, recompute traces, no auto-deploy | M7.2-02 | M7.2 |
| M7.2-04 | CHR/fixture acceptance for migration scenarios | M7.2-03 | M7.2 |

### 12.3 M7.3 — External Correlation Contract (не SIEM)

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M7.3-01 | Define IncidentSignal ingress contract (no raw syslog store) | M7.2 CLOSED | next-2 |
| M7.3-02 | Historical ActiveStateInterval resolver by occurred_at | M7.3-01, M6-01 | next-2 |
| M7.3-03 | On-demand connection-tracking / session context for incident | M7.3-01 | next-2 |
| M7.3-04 | Correlate sensor observation point with RouteResolutionTrace | M7.3-02, M7.1-03 | M7.3 |
| M7.3-05 | Emit visibility_status / confidence on every assessment | M7.3-03, N1-03 | next-2 |
| M7.3-06 | Contract tests for IncidentSignal ↔ ResponseAssessment | M7.3-01—M7.3-05 | next-2 |

### 12.4 M7.4 — Incident Enforcement

| ID | Задача | Depends | Spec |
|----|--------|---------|------|
| M7.4-01 | Add INCIDENT_PRE_STATE_DENY / INCIDENT_DENY_OVERLAY to pipeline | M7.3 CLOSED, M2-02 | next-2 |
| M7.4-02 | ResponseIntent → ResponseAssessment feasibility matrix | M7.4-01, N1-04 | next-2 |
| M7.4-03 | Compile/deploy overlay via existing M3/M4 (one Node) | M7.4-02, M4 CLOSED | next-2 |
| M7.4-04 | TTL expiry → mandatory removal plan (no silent write) | M7.4-03 | next-2 |
| M7.4-05 | Feedback events RESPONSE_* to external complex | M7.4-03 | next-2 |
| M7.4-06 | E2E: enforceable / not-enforceable / rollback / residual risk | M7.4-01—M7.4-05 | next-2 |

**Явно поза проєктом (next-2):** raw syslog store, correlation engine, NetFlow collector, SNMP/NMS, auto event→block, multi-Node campaign, universal SOAR.

---

## 13. Living Specification — матриця ТЗ → фаза → тести

| ТЗ / вимога | Фаза / модуль | Тести (мінімум) |
|-------------|---------------|-----------------|
| Inventory + API-SSL read | M1 Adapter | unit codec/session; CHR standalone |
| Canonical hash + semantic diff | M1 Canonical | test vectors §Canonical; diff unit |
| Packet-path / HW-offload blockers | N1 | fixtures path classes; deploy block |
| Policy compose + static analysis | M2 | analysis unit; SoD; management safety |
| Deterministic filter artifact | M3 | golden artifacts; hash stability |
| Anchor bootstrap | M5 | semantic equivalence; crash recovery |
| Watchdog deploy / rollback | M4 | fault-injection; VRRP; multi-WAN |
| Drift + E2E DoD | M6 | E2E suites §E2E; security; backup |
| Routing assurance | M7.1 | RouteResolutionTrace fixtures |
| Incident overlay | M7.4 | feasibility matrix; TTL removal |

Оновлювати цю матрицю при закритті кожного milestone.

---

## 14. Заборонені обхідні рішення

- generic RouterOS writer / raw command API «для тестів»;
- compile без актуального analysis;
- Safe Mode замість watchdog;
- partial VRRP onboard/deploy;
- automatic drift repair;
- auto-create management guard / users / api-ssl;
- campaigns у MVP;
- заглушки `pass` / `NotImplemented` / вимкнені тести.

---

## 15. Старт реалізації

1. Створити GitHub Issues з логічними ID з цього ROADMAP (або з Issue Sets + N1/M7).  
2. Почати з **M0-01**.  
3. Не відкривати M2 до **M1 CLOSED** (включно з N1-01—N1-03).  
4. Не відкривати M4 до **M5 CLOSED**.  
5. Не відкривати M7 до **MVP CLOSED**.

Деталі кожного MVP issue: `Initial Issue Set v0.1.md`, `M2–M6 Implementation Issue Set v0.1.md`.
)
