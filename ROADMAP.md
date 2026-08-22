# MTDirector — ROADMAP реалізації v0.2

**Дата оновлення:** 21 серпня 2026
**Статус:** нормативний індекс + **лінійна черга** атомарних задач
**Продукт:** MikroTik Firewall Controller (MTDirector)
**Базовий коміт аудиту:** M7.1-05 — Analyze dynamic route origins (read-only) DONE; **MVP CLOSED**; черга зсунута на M7.1-06 (#115)

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

## 2. Аудит реалізації (2026-08-15)

Базовий коміт: `579bdfd` (M2-09 algebra + TCP-flag intersect hotfix).  
Готовність **за кодом / ROADMAP §2.2**, не за застарілим GitHub CLOSED.

### 2.1 Прогрес

| Область | Closed | Open | % |
|---------|-------:|-----:|--:|
| M0 Bootstrap | 10 | 0 | 100% |
| M1 Read-only slice | 34 | 0 | 100% |
| N1 Packet-path weave | 7 | 0 | 100% |
| M2 Policy core | 18 | 0 | 100% |
| M3 Compiler | 8 | 0 | 100% |
| M5 Onboarding | 10 | 0 | 100% |
| M4 Safe deploy | 13 | 0 | 100% |
| M6 E2E / drift | 9 | 0 | 100% |
| M7 Post-MVP | 5 | 22 | 19% |
| **Разом** | **113** | **23** | **84% issues** |

MVP issues (109) = **109 done + 0 remaining** — **MVP CLOSED (100%)**.  
M7.1-03 DONE. M7.1-04 DONE. M7.1-05 DONE. Post-MVP M7 = **22** open (NEXT = M7.1-06 #115).  
Операційно: read-only зріз **готовий**; policy authoring Desktop **готовий**; **M3 Compiler CLOSED**; **M5 Onboarding CLOSED**; packet-path deploy **fail-closed**; standalone deploy path **готовий**; multi-WAN verify **готовий**; VRRP coordinator **готовий**; rollback/crash recovery **готовий**; deployment API/Desktop **готовий**; fault/security acceptance **DONE**; **M4 CLOSED**; desired/committed/actual projection **готовий** (M6-01); managed drift detection **готовий** (M6-02); bounded operational jobs **готовий** (M6-03); Desktop MVP workflows **готовий** (M6-04); standalone/dual-stack E2E **готовий** (M6-05); multi-WAN E2E **готовий** (M6-06); VRRP/CRS E2E **готовий** (M6-07); security/backup/restore acceptance **готовий** (M6-08); MVP production acceptance **готовий** (M6-09); **M6 CLOSED**; path-class E2E/drift **готовий** (N1-07); **MVP CLOSED**; routing-assurance read allowlist **готовий** (M7.1-01); RoutingAssuranceState persistence **готовий** (M7.1-02); RouteResolutionTrace **готовий** (M7.1-03); ECMP ONE_OF sets **готовий** (M7.1-04); dynamic route origins **готовий** (M7.1-05); NEXT = M7.1-06 (#115).

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
| M1-09 | #19 | Authenticated API-SSL connection + modern /login |
| M1-10 | #20 | Typed allowlisted RouterOS read executor |
| M1-11 | #21 | System and service discovery (identity/API-SSL) |
| M1-12 | #22 | Interface and address discovery |
| M1-13 | #23 | Firewall filter and address-list discovery |
| M1-14 | #24 | Routing and firewall-dependency discovery |
| M1-15 | #25 | VRRP discovery |
| M1-16 | #26 | Bridge/VLAN/switch metadata discovery |
| N1-01 | #45 | Packet-path read allowlist (container/app/veth/vrf) |
| M1-17 | #27 | RouterOS capability profile + compatibility manifest |
| N1-02 | #46 | Packet-path topology graph (Container→VETH→Bridge→VLAN→VRF) |
| N1-03 | #47 | Packet-path class CPU / HW-offload / MIXED / INDETERMINATE |
| M1-18 | #28 | Node topology validation (declared vs observed; no auto-scan) |
| M1-19 | #29 | Stable-read snapshot coordinator (fingerprints + bounded retry) |
| M1-20 | #30 | Raw snapshot assembly + centralized redaction |
| M1-21 | #31 | Canonicalization primitives (normalize + hash contracts) |
| M1-22 | #32 | Menu-specific canonical snapshots (discovery→section registry + config/obs split) |
| M1-23 | #33 | Persist canonical snapshots (CAS payloads, sections, identical capture, pagination) |
| M1-24 | #34 | Deterministic semantic snapshot diff (`SemanticDiffEngine`, CompareSnapshotsUseCase) |
| M1-25 | #35 | Inventory/discovery gRPC (`InventoryService` Vertical Slice §9.2; Issue Set `DiscoverDevice` → `ValidateDeviceConnection`; `GetDiscoveryStatus` deferred to M1-26 `StartCapture`) |
| M1-26 | #36 | Snapshot/diff gRPC (`SnapshotService` VS §9.3; Issue Set `CaptureSnapshot`→`StartCapture`, `WatchSnapshotCapture`→`WatchCapture`, `ListSnapshots`→`ListCaptures`; DiffEntry = Canonical Spec §30) |
| M1-27 | #37 | Desktop inventory tree (Site→Node→Device); `ListNodes` RPC; optional Device observation fields; single-flight cached refresh |
| M1-28 | #38 | Desktop snapshot viewer (sections/status/hashes/schema; config≠obs; technical unknown-props; sanitized copy; `SnapshotSummary.sections`) |
| M1-29 | #39 | Desktop semantic diff viewer (CompareSnapshots UI; section groups; server-side only; No differences) |
| M1-30 | #40 | Standalone CHR vertical-slice acceptance (in-process suite + live TLS gate + lab provision script) |
| M1-31 | #41 | Multi-WAN CHR vertical-slice acceptance (failover/balanced; config≠obs route diffs; lab provision) |
| M1-32 | #42 | VRRP CHR vertical-slice acceptance (active/passive + split-master; per-VRID roles; topology blockers) |
| M1-33 | #43 | Protocol/snapshot fault-injection suite (typed codes, pending=0, no orphan completes, recovery) |
| M1-34 | #44 | **M1 CLOSED** — vertical-slice acceptance package (docs + gates + known limitations) |
| M2-01 | #48 | Policy document lifecycle + document-centric `policies` / `policy_revisions` persistence |
| M2-02 | #49 | Fixed Policy Pipeline v1 + company-baseline chain contracts (DROP/REJECT/RETURN_TO_UNMANAGED) |
| M2-03 | #50 | Static address objects (HOST/PREFIX/IPv4 RANGE) + include/exclude selectors |
| M2-04 | #51 | Typed service objects (protocol/ports/ICMP) + include-only selectors |
| M2-05 | #52 | Logical zones + Node bindings (catalog SoT, ZoneService, Desktop CRUD; AC#10–11 deferred) |
| N1-05 | #67 | Zone VETH/VLAN/bridge resolve (canonical membership sections + container:/app: markers) |
| M2-06 | #53 | Typed policy rules/predicates/effects; App content-hash CAS CRUD; `PolicyService`; thin Desktop list |
| M2-07 | #54 | Deterministic logical policy composition; `ComposeEffectivePolicy`; IncrementalHash `logical_effective.v1` |
| M2-08 | #55 | Scoped deny-stage exceptions; `UpdateExceptionMetadata`; `EXEMPT_DENY_STAGE` insert; exception hash slot |
| M2-09 | #56 | Bounded packet predicate algebra; exception subset/overlap rewired to intervals |
| M2-10 | #57 | Structural + satisfiability analysis; `RULE_*` compose blockers before sequence |
| M2-11 | #58 | Duplicate / shadow / overlap; fail-closed subset equal; witness packets; sequence BLOCKERs on compose |
| M2-12 | #59 | Actual filter CFG + pre/post-anchor findings; implicit accept ≠ managed default; actual context hash |
| N1-04 | #66 | Packet-path analysis BLOCKERs `PACKET_PATH_BYPASSES_IP_FIREWALL` / `PACKET_PATH_NOT_PROVEN` |
| M2-13 | #60 | Management-path safety: API-SSL + source allowlist + guard-before-anchor; VIP-only / unknown matcher INDETERMINATE; SYSTEM tests + witnesses |
| M2-14 | #61 | Topology/dependency safety: VRRP proto-112 + sync flows; missing member; split-master vector; uplink zones; STRICT_RPF_*; RAW notrack; DSTNAT; mangle hash; SWITCH FORWARD; observations excluded from context hash |
| M2-15 | #62 | FastTrack policy validation: IPv4 FORWARD STATE_PRELUDE TCP/UDP ESTABLISHED,RELATED only; PCC/balanced/marks/VRF/IPsec/pre-anchor fail-closed; fallback flag; risk HIGH; 5-arg hash isolation |
| M2-16 | #63 | Policy tests + semantic UUID diff + risk: MANAGED_ONLY/NODE_EFFECTIVE; SYSTEM cannot disable; safety FAIL/INDETERMINATE BLOCKER; object impact; packet-space classes; 6-arg hash isolation |
| M2-17 | #64 | Approval + desired binding: immutable analysis run; bundle-hash + SoD; binding ≠ deploy; exception expiry → EXPIRED_PENDING_RECONCILIATION |
| M2-18 | #65 | **M2 CLOSED** — Desktop policy authoring/review (Contracts-only editors, validate/submit/approve/bind, semantic diff, risk; Deploy residual M4-12) |
| M3-01 | #68 | RouterOS filter artifact model: `RouterOsFilterArtifact` + MFC-CJ1; physical_semantics/artifact_id/resource_hash; golden vectors |
| M3-02 | #69 | Managed chain namespace/layout: `ManagedChainNamespace` + `ManagedChainLayoutBuilder`; mfc4/mfc6; Pipeline v1 root/deny |
| M3-03 | #70 | Content-addressed address lists: `AddressListCompileSession` + `AddressPrefixEncoder`; intern; limits; negated universe-minus-exclusions |
| M3-04 | #71 | Zone/service variants: `ZoneServiceVariantCompiler` + `PortMatcherEncoder`; direct interface-list or finite expansion; ICMP variants; WAN/running ignored |
| M3-05 | #72 | Filter matchers and regular effects: `FilterMatcherEffectCompiler` + `RouterOsCompilerProfile`; exact tokens; REJECT≠DROP; exceptions=`return`; input order; no duplicate deletion |
| M3-06 | #73 | FastTrack pairs + terminals: adjacent `fasttrack-connection`/`accept` with `hw-offload=no`; `:ft`/`:ac`; `ChainTerminalCompiler`; context fail-closed |
| M3-07 | #74 | Per-Device compile orchestration + content-addressed `filter_artifacts`; semantic summary RPC; fail-closed Node compile |
| M3-08 | #75 | **M3 CLOSED** — compiler acceptance matrix (Spec §32–§33); Switch FORWARD forbidden; deterministic topology vectors |
| M5-01 | #76 | Immutable onboarding plans, operation SM, write-ahead journal, Node/Device `ManagementState`, EF `OnboardingSchemaM501` |
| M5-02 | #77 | Prerequisite validation: exact supported build, plain API off, API-SSL cert, accounts/device-mode; Spec §58 codes |
| M5-03 | #78 | Management guard verification: typed `GuardProfile`, strict markers, breadth/`0.0.0.0/0`, plan hash; Spec §58 codes |
| M5-04 | #79 | Explicit permanent-anchor placement: operator intent only, fingerprint+rank, no `.id`, stale on order change |
| M5-05 | #80 | Restricted bootstrap writer: allowlisted filter add/set/remove, disabled anchors, Spec §23 artifact ID |
| M5-06 | #81 | Scheduler proof + onboarding watchdog: fixed no-op proof, deadline+startup, source hash, TTL/commit margin |
| M5-07 | #82 | Onboarding execution: stage roots then disabled anchors, arm watchdogs, enable order, pass-through verify, disarm, MANAGED |
| M5-08 | #83 | Deterministic rollback + crash recovery: disable-first, exact-resource remove, Spec §46 decision table, no automatic adoption |
| M5-09 | #84 | Onboarding API + Desktop workflow: Validate/CreatePlan/Start/Watch/Rollback/GetRecoveryStatus; plan_hash gate; no script source |
| M5-10 | #85 | **M5 CLOSED** — onboarding integration acceptance on every MVP topology; crash/watchdog/guard vectors; no partial managed Node |
| M4-01 | #86 | Immutable `DeploymentPlan` / Node+device SM / lock / write-ahead journal; `IDeploymentStore`; `DeploymentSchemaM401` |
| N1-06 | #99 | Packet-path deploy gate: `PACKET_PATH_*` → PRECHECKING BLOCKED; CPU/MIXED allowed; Switch no FORWARD proof |
| M4-02 | #87 | Restricted `RouterOsDeploymentSession` + managed-state reader; allowlisted paths; no `Mfc.RouterOs.Write` |
| M4-03 | #88 | Address-list create-or-verify staging (`AddressListCreateOrVerify` + `StageAddressListUseCase`) |
| M4-04 | #89 | Detached chain staging (`FilterChainCreateOrVerify` + `StageDetachedChainsUseCase`) |
| M4-05 | #90 | Production rollback watchdog (`DeploymentWatchdogScript` + `DeploymentWatchdogWriter`) |
| M4-06 | #91 | Transition-state validation + anchor activation (`TransitionStateValidator` + `ActivateAnchorsUseCase`) |
| M4-07 | #92 | Post-activation verification + probes (`PostActivationVerification` + `VerifyDeploymentActivationUseCase`) |
| M4-08 | #93 | Standalone Node coordinator (`StandaloneDeploymentPolicy` + `ExecuteStandaloneDeploymentUseCase`) |
| M4-09 | #94 | Multi-WAN deployment verification (`MultiWanDeploymentVerification` + `VerifyMultiWanDeploymentUseCase`) |
| M4-10 | #95 | VRRP deployment coordinator (`VrrpDeploymentPolicy` + `ExecuteVrrpDeploymentUseCase`) |
| M4-11 | #96 | Rollback + crash recovery (`DeploymentRecoveryDecision` + `ExecuteDeploymentRollbackUseCase` / `RecoverDeploymentUseCase`) |
| M4-12 | #97 | Deployment API + Desktop Deploy workflow (`DeploymentService` + plan_hash Watch) |
| M4-13 | #98 | **M4 CLOSED** — deployment fault/security acceptance Living Spec AC 1–13 |
| M6-01 | #100 | Desired/committed/actual hash projection + derived `NodeWorkflowStatus` (E2E §7–§8) |
| M6-02 | #101 | Managed drift detection vs last committed + deploy gate + immutable events (E2E §32–§34) |
| M6-03 | #102 | Bounded operational jobs (recovery > drift; no broker) |
| M6-04 | #103 | Desktop MVP workflows: seven modules + Drift/Audit gRPC read paths |
| M6-05 | #104 | Standalone / dual-stack E2E Living Spec AC 1–10 (Live CHR OFF) |
| M6-06 | #105 | Multi-WAN E2E Living Spec AC 1–10 (Live CHR OFF) |
| M6-07 | #106 | VRRP / CRS E2E Living Spec AC 1–11 (scripted fixtures; Live CHR OFF) |
| M6-08 | #107 | Security/backup/restore Living Spec AC 1–10 + Integration pg_dump/restore AC 11–14 |
| M6-09 | #108 | **M6 CLOSED** — MVP production acceptance (docs/release + scripts/release + Living Spec AC 1–16; no git tag) |
| N1-07 | #109 | Path-class E2E/drift Living Spec AC 1–12 + DriftFindingKind path-class kinds; **MVP CLOSED** |
| M7.1-01 | #110 | Routing-assurance read allowlist (`RoutingAssuranceAllowlist` + `RoutingSettings` / filter-rule command ids) |

### 2.3 Поточні прогалини (код)

| Збірка | Стан |
|--------|------|
| `Mfc.RouterOs` | protocol + discovery + capability + N1 + M7.1-01 routing-assurance allowlist + M7.1-02 assurance-state mapping + stable-read + raw/canonical snapshot projectors; default `ProbeOnlyRouterOsReadPort` + `NotConfiguredSnapshotCapturePort`; actual-filter discovery mapper; packet-path blocker mapper; management-path discovery mapper (`api-ssl.address` in canonical projector); topology-dependency discovery mapper (VRRP sync fields, RAW/NAT/Mangle, rp-filter, switch chip); FastTrack discovery mapper (pre-anchor + VRF); policy-evidence discovery mapper (NODE_EFFECTIVE actual filter); closed `OnboardingBootstrapWriter` (M5-05) + `OnboardingWatchdogWriter` arm/disarm/cleanup (M5-06–M5-08; generic `Write` namespace still absent); restricted `RouterOsDeploymentSession` (M4-02) + `DeploymentWatchdogWriter` (M4-05); anchor jump-target set used by M4-06 activation |
| `Mfc.Contracts` | `mfc.v1` inventory (+ workflow status / hash fields + `GetNodeWorkflow`) + snapshot/diff + `ZoneService` + `PolicyService` + `OnboardingService` + `DeploymentService` + `DriftService` + `AuditService` |
| `Mfc.Application` | inventory/snapshot + … + deployment workflow use cases / `IDeploymentRuntime` (M4-12) + `IDeviceHashStateStore` + workflow use cases (M6-01) + `IDriftEventStore` + `DetectManagedDriftUseCase` (M6-02) + operational job use cases (M6-03) + `ListAuditEventsUseCase` (M6-04) + `IRoutingAssuranceStateStore` + routing assurance upsert/get (M7.1-02) |
| `Mfc.Controller` | health + `InventoryService` (incl. `GetNodeWorkflow`) + `SnapshotService` + `ZoneService` + `PolicyService` + `OnboardingService` + `DeploymentService` + `DriftService` + `AuditService` gRPC |
| `Mfc.Desktop` | seven MVP modules (Inventory/Node/Snapshots/Policies/Operations/Drift/Audit); Contracts-only; cached inventory badge; no auto-fix drift |
| Persistence | inventory + snapshot CAS + policy lifecycle + zones + approvals/bindings + filter_artifacts + onboarding_* + deployment_* + `device_hash_states` (M6-01) + `drift_events` (M6-02) + `routing_assurance_states` (M7.1-02) + audit read store (M6-04) |
| `Mfc.Domain.Workflow` | `DeviceHashState` + classifier + `NodeWorkflowStatusProjector` (derived status; never persisted on Node) |
| `Mfc.Domain.Routing` | `RoutingAssuranceState` + config/ops snapshots + property classifier + hash contract (M7.1-02) + `RouteResolutionTraceEngine` / policy-routing trace (M7.1-03) + `EcmpRouteSet` ONE_OF bounded next-hop sets (M7.1-04) + `RouteOriginClassifier` / `DynamicRouteOriginAnalysis` read-only origins (M7.1-05); deferred expectation/finding slots |
| `Mfc.Domain.Policy` | lifecycle + Pipeline v1 + chain contracts + address/service/zone + N1-05 marker expand + typed rules + logical compose + deny-stage exceptions + bounded predicate algebra (M2-09) + structural/satisfiability (M2-10) + sequence (M2-11) + actual filter CFG/pre-anchor (M2-12) + packet-path FORWARD blockers (N1-04) + management-path safety (M2-13) + topology/dependency safety (M2-14) + FastTrack policy validation (M2-15) + policy tests/diff/risk (M2-16) + approval/desired-binding (M2-17) + object JSON writer (M2-18) + RouterOS filter artifact model (M3-01) + managed chain namespace/layout (M3-02) + content-addressed address lists (M3-03) + zone/service variants (M3-04) + matcher/effect compile (M3-05) + FastTrack pairs + terminals (M3-06) + per-device compile orchestration (M3-07) + compiler acceptance / Switch FORWARD gate (M3-08) |
| `Mfc.Domain.Onboarding` | immutable plans + plan hasher + operation SM + write-ahead steps + bootstrap artifact + `ManagementState` (M5-01) + prerequisite validator (M5-02) + `GuardProfile` / guard verifier (M5-03) + `AnchorPlacementPlanner` (M5-04) + `OnboardingBootstrapWritePlanner` (M5-05) + `OnboardingWatchdogPlanner` (M5-06) + pass-through equivalence / enable order (M5-07) + Spec §46 recovery decision table (M5-08) |
| `Mfc.Domain.Deployment` | immutable `DeploymentPlan` + plan hasher `mfc.deployment.plan.v1` + Node/device SM + exclusive lock + write-ahead steps (M4-01) + packet-path deploy gate (N1-06) + address-list create-or-verify (M4-03) + detached chain create-or-verify (M4-04) + production watchdog planner/script (M4-05) + transition-state validation + anchor activation order/decision (M4-06) + post-activation integrity/probes/watchdog readiness (M4-07) + standalone eligibility/NO_CHANGES policy (M4-08) + multi-WAN dependency/probe gates (M4-09) + VRRP classification/order/partial-failure policy (M4-10) + recovery decision table / controller rollback (M4-11); no campaign |

**NEXT = M7.1-06:** [M7.1-06](https://github.com/sesquicadaver/MTDirector/issues/115) Implement RouteExpectation declarations and evaluation. **M7.1-05 DONE.**

### 2.4 Операційний план до MVP CLOSED (2026-08-15)

Горизонт: **закрито** — **MVP CLOSED** після N1-07. Порядок був **строго лінійний** (§3). Далі лише Post-MVP M7 (§3.B).

**Хвиля 0 — гігієна трекера (DONE 2026-08-15):** CLOSED #52 M2-05, #53 M2-06, #56 M2-09, #67 N1-05. Не відкривати M3, доки черга A6 не закрита.

**Хвиля 1 — M2 analysis (черга #44–#47):**

| # | ID | GitHub | Результат | Жорсткі lock-и з аудиту |
|--:|----|-------:|-----------|-------------------------|
| ~~44~~ | ~~M2-10~~ | ~~#57~~ | ~~Structural + satisfiability blockers **до** sequence analysis~~ → DONE (`PolicyAnalysisEngine`; `RULE_*` compose gate; sequence not invoked on blockers) |
| ~~45~~ | ~~M2-11~~ | ~~#58~~ | ~~Duplicate / shadow / overlap + bounded residual + witness~~ → DONE (`PolicySequenceAnalysis`; fail-closed equal; INDETERMINATE ≠ FULLY_SHADOWED) |
| ~~46~~ | ~~M2-12~~ | ~~#59~~ | ~~Actual RouterOS filter-context (anchors, jumps, unmanaged)~~ → DONE (`ActualFilterAnalysis`; CFG limits; implicit accept ≠ managed default) |
| ~~47~~ | ~~N1-04~~ | ~~#66~~ | ~~`PACKET_PATH_BYPASSES_IP_FIREWALL` / `PACKET_PATH_NOT_PROVEN`~~ → DONE (`PacketPathAnalysis`; HW/INDETERMINATE BLOCKERs; MIXED not those codes) |

**Хвиля 2 — M2 safety (черга #48–#51):** ~~M2-13 management-path (#60)~~ → ~~M2-14 VRRP/multi-WAN/RAW/NAT deps (#61)~~ → ~~M2-15 FastTrack (#62)~~ → ~~M2-16 tests/diff/risk (#63)~~. Усі risk:high, крім M2-16.

**Хвиля 3 — M2 CLOSED (черга #52–#53):** ~~M2-17 approval + desired-binding (#64)~~ → ~~M2-18 Desktop authoring/review (#65)~~ → **M2 CLOSED**.

**Хвиля 4 — M3 Compiler (черга #54–#61, #68–#75):** артефакт → namespace → address-lists → zones/services → matchers → FastTrack/terminal → per-Device orchestration → **M3 CLOSED**. Заборона: compile без актуального analysis (§6).

**Хвиля 5 — M5 Onboarding перед M4 (черга #62–#71, #76–#85):** domain → prerequisites → guard → anchor plan → write adapter → scheduler/watchdog → execute → rollback → API/Desktop → **M5 CLOSED**.

**Хвиля 6 — M4 Safe deploy + N1-06 (черга #72–#85, #86–#99):** N1-06 блокує deploy при packet-path blockers. Далі plan/writer/staging/watchdog/VRRP/rollback/API → **M4 CLOSED**. Заборона: Safe Mode замість watchdog; partial VRRP.

**Хвиля 7 — M6 E2E + N1-07 → MVP CLOSED (черга #86–#95, #100–#109):** desired/committed/actual → drift → jobs → Desktop → CHR suites (standalone, multi-WAN, VRRP/CRS) → security/backup → ~~**M6-09 M6 CLOSED**~~ → ~~**N1-07 → MVP CLOSED**~~. Live CHR matrix увімкнути лише на isolated runner (зараз OFF).

**Поза планом до MVP CLOSED:** ~~M7.* (#110–#136)~~ → тепер **єдина відкрита черга** (§3.B).

**DoD кожного PR:** AC issue set; Living Spec рядок; CHANGELOG; CI Linux validate + Windows Desktop; без `pass`/`NotImplemented`; Domain/App ↛ RouterOs.

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
| ~~5~~ | ~~M1-09~~ | ~~#19~~ | ~~Implement authenticated TLS RouterOS connection~~ → §2.2 DONE |
| ~~6~~ | ~~M1-10~~ | ~~#20~~ | ~~Add typed allowlisted RouterOS read executor~~ → §2.2 DONE |

#### Блок A2 — M1 Discovery

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~7~~ | ~~M1-11~~ | ~~#21~~ | ~~Implement system and service discovery~~ → §2.2 DONE |
| ~~8~~ | ~~M1-12~~ | ~~#22~~ | ~~Implement interface and address discovery~~ → §2.2 DONE |
| ~~9~~ | ~~M1-13~~ | ~~#23~~ | ~~Implement firewall and address-list discovery~~ → §2.2 DONE |
| ~~10~~ | ~~M1-14~~ | ~~#24~~ | ~~Implement routing and firewall-dependency discovery~~ → §2.2 DONE |
| ~~11~~ | ~~M1-15~~ | ~~#25~~ | ~~Implement VRRP discovery~~ → §2.2 DONE |
| ~~12~~ | ~~M1-16~~ | ~~#26~~ | ~~Implement bridge, VLAN and switch metadata discovery~~ → §2.2 DONE |

#### Блок A3 — N1 (M1 weave) + capabilities / topology

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~13~~ | ~~N1-01~~ | ~~#45~~ | ~~Extend read allowlist: `/container`, `/app`, `/interface/veth`, `/ip/vrf`~~ → §2.2 DONE |
| ~~14~~ | ~~M1-17~~ | ~~#27~~ | ~~Implement RouterOS capability profile~~ → §2.2 DONE |
| ~~15~~ | ~~N1-02~~ | ~~#46~~ | ~~Project Container/App→VETH→Bridge→VLAN→VRF topology graph~~ → §2.2 DONE |
| ~~16~~ | ~~N1-03~~ | ~~#47~~ | ~~Classify packet path CPU / HW-offload / MIXED / INDETERMINATE~~ → §2.2 DONE |
| ~~17~~ | ~~M1-18~~ | ~~#28~~ | ~~Implement node topology validation~~ → §2.2 DONE |

#### Блок A4 — M1 Snapshots / canonical / diff

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~18~~ | ~~M1-19~~ | ~~#29~~ | ~~Implement stable-read snapshot coordinator~~ → §2.2 DONE |
| ~~19~~ | ~~M1-20~~ | ~~#30~~ | ~~Implement raw snapshot assembly and redaction~~ → §2.2 DONE |
| ~~20~~ | ~~M1-21~~ | ~~#31~~ | ~~Implement canonicalization primitives~~ → §2.2 DONE |
| ~~21~~ | ~~M1-22~~ | ~~#32~~ | ~~Implement menu-specific canonical snapshots~~ → §2.2 DONE |
| ~~22~~ | ~~M1-23~~ | ~~#33~~ | ~~Persist snapshots and detect identical captures~~ → §2.2 DONE |
| ~~23~~ | ~~M1-24~~ | ~~#34~~ | ~~Implement deterministic semantic snapshot diff~~ → §2.2 DONE |

#### Блок A5 — M1 API / Desktop / acceptance gate

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~24~~ | ~~M1-25~~ | ~~#35~~ | ~~Add inventory and discovery gRPC services~~ → §2.2 DONE (VS §9.2 names; Issue Set DiscoverDevice→ValidateDeviceConnection) |
| ~~25~~ | ~~M1-26~~ | ~~#36~~ | ~~Add snapshot and diff gRPC services~~ → §2.2 DONE (VS §9.3; Issue Set CaptureSnapshot→StartCapture, WatchSnapshotCapture→WatchCapture, ListSnapshots→ListCaptures; Diff = Canonical Spec §30) |
| ~~26~~ | ~~M1-27~~ | ~~#37~~ | ~~Add desktop inventory tree~~ → §2.2 DONE (`ListNodes` + Avalonia Site→Node→Device tree; cached single-flight refresh) |
| ~~27~~ | ~~M1-28~~ | ~~#38~~ | ~~Add desktop snapshot viewer~~ → §2.2 DONE (read-only Avalonia viewer; `SnapshotSummary.sections`; sanitized export) |
| ~~28~~ | ~~M1-29~~ | ~~#39~~ | ~~Add desktop semantic diff viewer~~ → §2.2 DONE (CompareSnapshots UI; section groups; no local recompute) |
| ~~29~~ | ~~M1-30~~ | ~~#40~~ | ~~Add standalone CHR vertical-slice acceptance test~~ → §2.2 DONE |
| ~~30~~ | ~~M1-31~~ | ~~#41~~ | ~~Add multi-WAN CHR vertical-slice acceptance test~~ → §2.2 DONE |
| ~~31~~ | ~~M1-32~~ | ~~#42~~ | ~~Add VRRP CHR vertical-slice acceptance test~~ → §2.2 DONE |
| ~~32~~ | ~~M1-33~~ | ~~#43~~ | ~~Add protocol and snapshot fault-injection suite~~ → §2.2 DONE |
| ~~33~~ | ~~M1-34~~ | ~~#44~~ | ~~Complete read-only vertical-slice acceptance (**M1 CLOSED**)~~ → §2.2 DONE |

#### Блок A6 — M2 Policy core (+ N1)

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~34~~ | ~~M2-01~~ | ~~#48~~ | ~~Implement policy document lifecycle and persistence~~ → §2.2 DONE |
| ~~35~~ | ~~M2-02~~ | ~~#49~~ | ~~Implement fixed Policy Pipeline v1 and chain contracts~~ → §2.2 DONE |
| ~~36~~ | ~~M2-03~~ | ~~#50~~ | ~~Implement address objects and selectors~~ → §2.2 DONE |
| ~~37~~ | ~~M2-04~~ | ~~#51~~ | ~~Implement service objects and selectors~~ → §2.2 DONE |
| ~~38~~ | ~~M2-05~~ | ~~#52~~ | ~~Implement logical zones and Node bindings~~ → §2.2 (catalog SoT + ZoneService + Desktop; AC#10–11 → M2-06) |
| ~~39~~ | ~~N1-05~~ | ~~#67~~ | ~~Bind zones to VETH/VLAN/bridge without ContainerPolicy entities~~ → §2.2 (canonical membership + marker expand) |
| ~~40~~ | ~~M2-06~~ | ~~#53~~ | ~~Implement policy rules, predicates and effects~~ → DONE (typed rules + App CAS + PolicyService + thin Desktop) |
| ~~41~~ | ~~M2-07~~ | ~~#54~~ | ~~Implement deterministic policy composition~~ → DONE (logical compose + `ComposeEffectivePolicy` + IncrementalHash) |
| ~~42~~ | ~~M2-08~~ | ~~#55~~ | ~~Implement temporary deny-stage exceptions~~ → DONE (typed metadata + compose insert + exception hash slot) |
| ~~43~~ | ~~M2-09~~ | ~~#56~~ | ~~Implement normalized predicate algebra~~ → DONE (bounded cubes + exception interval proofs) |
| ~~44~~ | ~~M2-10~~ | ~~#57~~ | ~~Implement structural and satisfiability analysis~~ → §2.2 DONE |
| ~~45~~ | ~~M2-11~~ | ~~#58~~ | ~~Implement duplicate, shadow and overlap analysis~~ → §2.2 DONE |
| ~~46~~ | ~~M2-12~~ | ~~#59~~ | ~~Implement actual RouterOS filter-context analysis~~ → §2.2 DONE |
| ~~47~~ | ~~N1-04~~ | ~~#66~~ | ~~Emit `PACKET_PATH_BYPASSES_IP_FIREWALL` / `PACKET_PATH_NOT_PROVEN` blockers~~ → §2.2 DONE |
| ~~48~~ | ~~M2-13~~ | ~~#60~~ | ~~Implement management-path safety validation~~ → §2.2 DONE (`ManagementPathAnalysis`; API-SSL/source/guard; SYSTEM tests) |
| ~~49~~ | ~~M2-14~~ | ~~#61~~ | ~~Implement topology and dependency safety validation~~ → §2.2 DONE (`TopologyDependencyAnalysis`; VRRP/multi-WAN/RAW/NAT/Mangle/SWITCH) |
| ~~50~~ | ~~M2-15~~ | ~~#62~~ | ~~Implement FastTrack policy validation~~ → §2.2 DONE (`FastTrackAnalysis`; IPv4 FORWARD STATE_PRELUDE TCP/UDP; topology fail-closed; fallback flag; risk HIGH) |
| ~~51~~ | ~~M2-16~~ | ~~#63~~ | ~~Implement policy tests, semantic diff and risk classification~~ → §2.2 DONE (`PolicyEvidenceAnalysis`; MANAGED_ONLY/NODE_EFFECTIVE; UUID diff; risk floor; 6-arg hash isolation) |
| ~~52~~ | ~~M2-17~~ | ~~#64~~ | ~~Implement approval and desired-binding workflow~~ → §2.2 DONE (`PolicyApprovalGate`; immutable analysis run; binding ≠ deploy) |
| ~~53~~ | ~~M2-18~~ | ~~#65~~ | ~~Add policy authoring and review desktop workflow (**M2 CLOSED**)~~ → §2.2 DONE |

#### Блок A7 — M3 Compiler

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~54~~ | ~~M3-01~~ | ~~#68~~ | ~~Implement RouterOS filter artifact model~~ → DONE (`RouterOsFilterArtifact`; MFC-CJ1; golden vectors) |
| ~~55~~ | ~~M3-02~~ | ~~#69~~ | ~~Implement managed chain namespace and layout~~ → DONE (`ManagedChainNamespace` + `ManagedChainLayoutBuilder`) |
| ~~56~~ | ~~M3-03~~ | ~~#70~~ | ~~Compile content-addressed address lists~~ → DONE (`AddressListCompileSession` + `AddressPrefixEncoder`; intern; limits) |
| ~~57~~ | ~~M3-04~~ | ~~#71~~ | ~~Compile zones and service variants~~ → DONE (`ZoneServiceVariantCompiler` + `PortMatcherEncoder`) |
| ~~58~~ | ~~M3-05~~ | ~~#72~~ | ~~Compile supported matchers and regular effects~~ → DONE (`FilterMatcherEffectCompiler` + `RouterOsCompilerProfile`) |
| ~~59~~ | ~~M3-06~~ | ~~#73~~ | ~~Compile FastTrack and terminal rules~~ → DONE (FastTrack pair + `ChainTerminalCompiler`) |
| ~~60~~ | ~~M3-07~~ | ~~#74~~ | ~~Implement per-Device compiler orchestration and artifact storage~~ → DONE (`DeviceFilterCompiler` + `filter_artifacts`) |
| ~~61~~ | ~~M3-08~~ | ~~#75~~ | ~~Complete compiler integration and acceptance (**M3 CLOSED**)~~ → DONE (`DeviceFilterCompilerAcceptanceTests` + Switch FORWARD gate) |

#### Блок A8 — M5 Onboarding (перед M4)

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~62~~ | ~~M5-01~~ | ~~#76~~ | ~~Implement onboarding domain model and persistence~~ → DONE (plans/ops/steps + `ManagementState` + `OnboardingSchemaM501`) |
| ~~63~~ | ~~M5-02~~ | ~~#77~~ | ~~Implement onboarding prerequisite validation~~ → DONE (`OnboardingPrerequisiteValidator` + Living Spec AC 1–12) |
| ~~64~~ | ~~M5-03~~ | ~~#78~~ | ~~Implement management guard verification~~ → DONE (`GuardProfile` + `OnboardingGuardVerifier` + Living Spec AC 1–10) |
| ~~65~~ | ~~M5-04~~ | ~~#79~~ | ~~Implement explicit anchor placement planning~~ → DONE (`AnchorPlacementPlanner` + Living Spec AC 1–10) |
| ~~66~~ | ~~M5-05~~ | ~~#80~~ | ~~Implement onboarding write adapter and bootstrap artifact~~ → DONE (`OnboardingBootstrapWriter` + Living Spec AC 1–12) |
| ~~67~~ | ~~M5-06~~ | ~~#81~~ | ~~Implement scheduler proof and onboarding watchdog~~ → DONE (`OnboardingWatchdogWriter` + Living Spec AC 1–12) |
| ~~68~~ | ~~M5-07~~ | ~~#82~~ | ~~Implement onboarding execution and verification~~ → DONE (`ExecuteOnboardingBootstrapUseCase` + Living Spec AC 1–13) |
| ~~69~~ | ~~M5-08~~ | ~~#83~~ | ~~Implement onboarding rollback and crash recovery~~ → DONE (`RollbackOnboardingBootstrapUseCase` + `RecoverOnboardingUseCase` + Spec §46 table) |
| ~~70~~ | ~~M5-09~~ | ~~#84~~ | ~~Expose onboarding API and desktop workflow~~ → DONE (`OnboardingService` + Desktop panel; plan_hash; no script source) |
| ~~71~~ | ~~M5-10~~ | ~~#85~~ | ~~Complete onboarding integration acceptance (**M5 CLOSED**)~~ → DONE (Living Spec AC 1–12 + testlab dual-stack/CRS + gRPC topology host) |

#### Блок A9 — M4 Safe deployment (+ N1-06)

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~72~~ | ~~M4-01~~ | ~~#86~~ | ~~Implement deployment plan, states and persistence~~ → DONE (`DeploymentPlan` + SM + lock + journal + `DeploymentSchemaM401`) |
| ~~73~~ | ~~N1-06~~ | ~~#99~~ | ~~Block deploy when packet-path blockers present~~ → DONE (`DeploymentPacketPathGate` + PRECHECKING → BLOCKED) |
| ~~74~~ | ~~M4-02~~ | ~~#87~~ | ~~Implement restricted deployment writer and managed-state reader~~ → DONE (`RouterOsDeploymentSession` + Living Spec AC 1–12) |
| ~~75~~ | ~~M4-03~~ | ~~#88~~ | ~~Implement address-list create-or-verify staging~~ → DONE (`AddressListCreateOrVerify` + `StageAddressListUseCase`) |
| ~~76~~ | ~~M4-04~~ | ~~#89~~ | ~~Implement detached chain staging and verification~~ → DONE (`FilterChainCreateOrVerify` + `StageDetachedChainsUseCase`) |
| ~~77~~ | ~~M4-05~~ | ~~#90~~ | ~~Implement production rollback watchdog~~ → DONE (`DeploymentWatchdogScript` + `DeploymentWatchdogWriter`) |
| ~~78~~ | ~~M4-06~~ | ~~#91~~ | ~~Implement transition-state validation and anchor activation~~ → DONE (`TransitionStateValidator` + `ActivateAnchorsUseCase`) |
| ~~79~~ | ~~M4-07~~ | ~~#92~~ | ~~Implement deployment probes and post-activation verification~~ → DONE (`PostActivationVerification` + `VerifyDeploymentActivationUseCase`) |
| ~~80~~ | ~~M4-08~~ | ~~#93~~ | ~~Implement standalone Node deployment coordinator~~ → DONE (`ExecuteStandaloneDeploymentUseCase`) |
| ~~81~~ | ~~M4-09~~ | ~~#94~~ | ~~Implement multi-WAN deployment verification~~ → DONE (`VerifyMultiWanDeploymentUseCase`) |
| ~~82~~ | ~~M4-10~~ | ~~#95~~ | ~~Implement VRRP deployment coordinator~~ → DONE (`ExecuteVrrpDeploymentUseCase`) |
| ~~83~~ | ~~M4-11~~ | ~~#96~~ | ~~Implement rollback and crash recovery~~ → DONE (`RecoverDeploymentUseCase`) |
| ~~84~~ | ~~M4-12~~ | ~~#97~~ | ~~Expose deployment API and desktop operation workflow~~ → DONE (`DeploymentService` + Desktop Deploy) |
| ~~85~~ | ~~M4-13~~ | ~~#98~~ | ~~Complete deployment fault and security acceptance (**M4 CLOSED**)~~ → DONE (`DeploymentFaultSecurityAcceptanceLivingSpecTests` AC 1–13 all passed; `DeploymentAcceptanceHarness` shared infra) |

#### Блок A10 — M6 E2E (+ N1-07) → MVP CLOSED

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~86~~ | ~~M6-01~~ | ~~#100~~ | ~~Implement desired, committed and actual state projection~~ → DONE (`DeviceHashState` + projector + `device_hash_states` + Desktop hashes) |
| ~~87~~ | ~~M6-02~~ | ~~#101~~ | ~~Implement managed drift detection~~ → DONE (`ManagedDriftDetector` + `drift_events` + deploy gate; no auto-repair) |
| ~~88~~ | ~~N1-07~~ | ~~#109~~ | ~~E2E/drift acceptance for container/VLAN/VETH/HW path classes~~ → DONE (`PathClassE2EDriftLivingSpecTests` AC 1–12; path-class `DriftFindingKind` + `PathClassConfigDriftVoiding`; **MVP CLOSED**) |
| ~~89~~ | ~~M6-03~~ | ~~#102~~ | ~~Implement bounded operational background jobs~~ → DONE (`OperationalJobSchedulerHostedService` + bounded queues; no broker) |
| ~~90~~ | ~~M6-04~~ | ~~#103~~ | ~~Integrate final desktop workflows~~ → DONE (seven modules + Drift/Audit gRPC; Living Spec AC 1–12) |
| ~~91~~ | ~~M6-05~~ | ~~#104~~ | ~~Complete standalone and dual-stack end-to-end acceptance~~ → DONE (Living Spec AC 1–10 + Integration inventory→capture→onboarding; Live CHR OFF) |
| ~~92~~ | ~~M6-06~~ | ~~#105~~ | ~~Complete multi-WAN end-to-end acceptance~~ → DONE (Living Spec AC 1–10; scripted runtimes; Live CHR OFF) |
| ~~93~~ | ~~M6-07~~ | ~~#106~~ | ~~Complete VRRP and CRS end-to-end acceptance~~ → DONE (Living Spec AC 1–11; scripted fixtures; Live CHR OFF) |
| ~~94~~ | ~~M6-08~~ | ~~#107~~ | ~~Complete security, backup and restore acceptance~~ → DONE (Living Spec AC 1–10; Integration pg_dump/restore AC 11–14) |
| ~~95~~ | ~~M6-09~~ | ~~#108~~ | ~~Complete MVP release acceptance (**M6 CLOSED**; MVP CLOSED after N1-07)~~ → DONE (`docs/release/*` + `scripts/release/*` + `MvpReleaseAcceptanceLivingSpecTests` AC 1–16) |

---

### 3.B — Post-MVP M7 (27 атомарних задач; після MVP CLOSED)

#### Блок B1 — M7.1 Routing / path assurance

| # | ID | GitHub | Задача |
|--:|----|-------:|--------|
| ~~96~~ | ~~M7.1-01~~ | ~~#110~~ | ~~Extend read allowlist: routing tables/settings/rules, VRF, route filters~~ → DONE (`RoutingAssuranceAllowlist` + `RoutingSettings` / `RoutingFilterRules` / `RoutingFilterSelectRules`; no routing writes) |
| ~~97~~ | ~~M7.1-02~~ | ~~#111~~ | ~~Persist RoutingAssuranceState (config + operational)~~ → DONE (`routing_assurance_states` + Domain RoutingAssuranceState; config≠ops hashes; no routing writes) |
| ~~98~~ | ~~M7.1-03~~ | ~~#112~~ | ~~Implement RouteResolutionTrace~~ → DONE (`RouteResolutionTraceEngine` + Living Spec AC 1–10; upsert computes traces from `TraceQueries`; no routing writes) |
| ~~99~~ | ~~M7.1-04~~ | ~~#113~~ | ~~Model ECMP as ONE_OF bounded next-hop sets~~ → DONE (`EcmpRouteSet` + `EcmpRouteSetBuilder`; Living Spec AC 1–9; persistence round-trip; no routing writes) |
| ~~100~~ | ~~M7.1-05~~ | ~~#114~~ | ~~Analyze dynamic route origins (read-only)~~ → DONE (`RouteOriginClassifier` + `DynamicRouteOriginAnalysis`; Living Spec AC 1–8; operational snapshot persistence; no routing writes) |
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

**Кінець черги:** 22 відкритих атомарних задач (усі Post-MVP M7). Start here: #115 M7.1-06.

---

## 4. Підсумок лічильників

| Сегмент | У черзі | Примітка |
|---------|--------:|----------|
| До MVP CLOSED | 0 | **MVP CLOSED** (N1-07 DONE) |
| Post-MVP M7 | 22 | NEXT = M7.1-06 (#115) |
| **Нереалізовано разом** | **25** | лише M7 |
| DONE у коді (§2.2) | 113 | …+M4-01…13+M6-01…M6-09+N1-07+M7.1-01…M7.1-04 |

GitHub-трекер вирівняно хвилею 0 (2026-08-15): #52, #53, #56, #67 CLOSED.

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
| Authenticated API-SSL | M1-09 | local test CA; login | **DONE** |
| Typed read executor | M1-10 | allowlist; trap/fatal; arch Write ban | **DONE** |
| System/service discovery | M1-11 | identity/API-SSL; sanitized fixture | **DONE** |
| Interface/address discovery | M1-12 | CIDR; list membership; cycles | **DONE** |
| Firewall filter discovery | M1-13 | ordered filters; fwc:; dyn digests | **DONE** |
| Routing/dependency discovery | M1-14 | routes; NAT/RAW/Mangle; rp-filter | **DONE** |
| VRRP discovery | M1-15 | family+VRID+if; role≠config hash | **DONE** |
| Bridge/VLAN/switch discovery | M1-16 | VLAN table; HW-offload obs; unknown chip | **DONE** |
| Packet-path allowlist | N1-01 | container/app/veth/vrf prints | **DONE** |
| Capability profile | M1-17 | manifest; SupportState; cap hash | **DONE** |
| Packet-path topology graph | N1-02 | Container→VETH→Bridge→VLAN→VRF | **DONE** |
| Packet-path class | N1-03 | CPU/HW/MIXED/INDETERMINATE | **DONE** |
| Node topology validation | M1-18 | declared vs observed; no auto-scan | **DONE** |
| Stable-read coordinator | M1-19 | fingerprints; bounded retry; SNAPSHOT_UNSTABLE | **DONE** |
| Raw snapshot assembly | M1-20 | versioned redacted raw; size limit; secret scan | **DONE** |
| Canonicalization primitives | M1-21 | IP/set/JSON normalize; config≠obs hashes | **DONE** |
| Menu canonical snapshots | M1-22 | section registry; config≠obs; unknown→compat obs | **DONE** |
| Packet-path blockers | N1-04 | `PacketPathAnalysis`; HW → BYPASSES; INDETERMINATE → NOT_PROVEN; MIXED not those codes | **DONE** |
| Logical zones + Node bindings | M2-05 | catalog SoT; per-Device resolve; ZoneService; Desktop CRUD; AC#10–11 deferred | **DONE** |
| Zone VETH/VLAN/bridge resolve | N1-05 | topology.container-veth/shared-veth; container:/app: markers; typed blockers; hash v1; live projector←PacketPath wiring residual (M1-22 seam) | **DONE** (library+resolve) |
| Typed policy rules | M2-06 | typed rules; content-hash CAS; PolicyService; soft `POLICY_SELECTOR_CATALOG_SOFT`; thin Desktop | **DONE** |
| Deterministic policy composition | M2-07 | logical compose; UUID resolve; `POLICY_COMPOSE_*`; `ComposeEffectivePolicy`; IncrementalHash ≠ synthetic document | **DONE** |
| Scoped deny-stage exceptions | M2-08 | `EXEMPT_DENY_STAGE`; fail-closed subset; `UpdateExceptionMetadata`; `POLICY_EXCEPTION_*`; exception hash slot | **DONE** |
| Bounded predicate algebra | M2-09 | cubes; exception interval subset/overlap; `PREDICATE_COMPLEXITY_LIMIT` | **DONE** |
| Structural + satisfiability analysis | M2-10 | `PolicyAnalysisEngine`; `RULE_*`; disabled rules; no sequence on blockers | **DONE** |
| Duplicate / shadow / overlap | M2-11 | `PolicySequenceAnalysis`; fail-closed equal; witness; sequence BLOCKERs on compose | **DONE** |
| Actual RouterOS filter-context | M2-12 | `ActualFilterAnalysis`; bounded CFG; pre-anchor BLOCKERs; implicit accept ≠ managed default; actual context hash | **DONE** |
| Management-path safety | M2-13 | `ManagementPathAnalysis`; API-SSL + source allowlist + guard-before-anchor; VIP-only/unknown INDETERMINATE; SYSTEM tests + witnesses; management context hash | **DONE** |
| Topology / dependency safety | M2-14 | `TopologyDependencyAnalysis`; proto-112 + sync flows; `VRRP_MEMBER_MISSING`; split-master vector; uplink zones; tables/rules in context; PCC/routing-mark WARNING; `STRICT_RPF_*`; RAW notrack; DSTNAT; mangle hash; SWITCH FORWARD; observations excluded from context hash | **DONE** |
| FastTrack policy validation | M2-15 | `FastTrackAnalysis`; IPv4 FORWARD STATE_PRELUDE TCP/UDP ESTABLISHED,RELATED; PCC/balanced/marks/VRF/IPsec/pre-anchor BLOCKER; fallback flag; risk HIGH; 5-arg hash isolation | **DONE** |
| Policy tests / semantic diff / risk | M2-16 | `PolicyEvidenceAnalysis`; MANAGED_ONLY/NODE_EFFECTIVE; SYSTEM cannot disable; safety FAIL BLOCKER; UUID diff; object impact; packet-space classes; 6-arg hash isolation | **DONE** |
| Persist canonical snapshots | M1-23 | PG sections; payload dedupe; pagination; immutability | **DONE** |
| Semantic snapshot diff | M1-24 | `SemanticDiffEngine` unit AC#1–13; CompareSnapshotsUseCase | **DONE** |
| gRPC + Desktop read-only UI | M1-25…29 | contract + UI smoke | M1-25…29 DONE |
| M1 acceptance gate | M1-30…34 | CHR suites + acceptance package | **M1 CLOSED** |
| Policy compose + analysis | M2 | compose DONE (M2-07…09); structural DONE (M2-10); sequence DONE (M2-11); actual-filter DONE (M2-12); packet-path BLOCKERs DONE (N1-04); management-path DONE (M2-13); topology/deps DONE (M2-14); FastTrack DONE (M2-15); tests/diff/risk DONE (M2-16); approval/binding DONE (M2-17); Desktop authoring/review DONE (M2-18) | **M2 CLOSED** |
| Deterministic filter artifact | M3-01 | `RouterOsFilterArtifact` + MFC-CJ1 writer; physical_semantics/artifact_id/resource_hash; golden vectors | **DONE** |
| Managed chain namespace / layout | M3-02 | `ManagedChainNamespace` + `ManagedChainLayoutBuilder`; mfc4/mfc6; Pipeline v1 root/deny layout | **DONE** |
| Content-addressed address lists | M3-03 | `AddressListCompileSession` + `AddressPrefixEncoder`; content-hash intern; negated universe-minus-exclusions; layout v1 limits | **DONE** |
| Zone and service variants | M3-04 | `ZoneServiceVariantCompiler` + `PortMatcherEncoder`; direct interface-list or finite expansion; ICMP variants; WAN/running ignored | **DONE** |
| Filter matchers and regular effects | M3-05 | `FilterMatcherEffectCompiler` + `RouterOsCompilerProfile`; exact tokens; REJECT≠DROP; exceptions=`return`; input order | **DONE** |
| FastTrack pairs and terminal rules | M3-06 | FastTrack adjacent pair + `hw-offload=no`; `ChainTerminalCompiler`; context fail-closed | **DONE** |
| Per-device compile + artifact storage | M3-07 | `DeviceFilterCompiler` + content-addressed `filter_artifacts`; semantic summary RPC | **DONE** |
| Compiler acceptance / M3 CLOSED | M3-08 | Spec §32–§33 topology vectors + `SWITCH_FORWARD_COMPILATION_FORBIDDEN`; Living Spec `DeviceFilterCompilerAcceptanceTests` | **DONE** |
| Onboarding domain + persistence | M5-01 | Living Spec `OnboardingLivingSpecTests` AC#1–10 + `OnboardingPersistTests`; `ManagementState`; `OnboardingSchemaM501` | **DONE** |
| Scheduler proof + onboarding watchdog | M5-06 | Living Spec `OnboardingWatchdogLivingSpecTests` AC#1–12; `OnboardingWatchdogWriter` | **DONE** |
| Onboarding execution + verification | M5-07 | Living Spec `OnboardingExecutionLivingSpecTests` AC#1–13; `ExecuteOnboardingBootstrapUseCase` | **DONE** |
| Onboarding rollback + crash recovery | M5-08 | Living Spec `OnboardingRollbackLivingSpecTests` AC#1–11; Spec §46 `OnboardingRecoveryDecision` | **DONE** |
| Onboarding API + Desktop workflow | M5-09 | Living Spec `OnboardingWorkflowLivingSpecTests` AC#1–10; `OnboardingService` + Desktop panel | **DONE** |
| Onboarding integration acceptance / M5 CLOSED | M5-10 | Living Spec `OnboardingIntegrationAcceptanceLivingSpecTests` AC#1–12; `OnboardingTopologyAcceptanceTests` | **DONE** |
| Deployment plan + persistence | M4-01 | Living Spec `DeploymentLivingSpecTests` AC#1–12 + `DeploymentPersistTests`; `DeploymentSchemaM401` | **DONE** |
| Packet-path deploy gate | N1-06 | Living Spec `DeploymentPacketPathGateLivingSpecTests` AC#1–10; `DeploymentPacketPathPrecheck` | **DONE** |
| Restricted deployment writer | M4-02 | Living Spec `DeploymentWriterLivingSpecTests` AC#1–12; `RouterOsDeploymentSession` | **DONE** |
| Address-list create-or-verify | M4-03 | Living Spec `AddressListStagingLivingSpecTests` AC#1–10; `StageAddressListUseCase` | **DONE** |
| Detached chain staging | M4-04 | Living Spec `DetachedChainStagingLivingSpecTests` AC#1–11; `StageDetachedChainsUseCase` | **DONE** |
| Production rollback watchdog | M4-05 | Living Spec `DeploymentWatchdogLivingSpecTests` AC#1–12; `DeploymentWatchdogWriter` | **DONE** |
| Transition + anchor activation | M4-06 | Living Spec `AnchorActivationLivingSpecTests` AC#1–11; `ActivateAnchorsUseCase` | **DONE** |
| Post-activation verification | M4-07 | Living Spec `DeploymentVerificationLivingSpecTests` AC#1–11; `VerifyDeploymentActivationUseCase` | **DONE** |
| Standalone Node coordinator | M4-08 | Living Spec `StandaloneDeploymentLivingSpecTests` AC#1–10; `ExecuteStandaloneDeploymentUseCase` | **DONE** |
| Multi-WAN deployment verification | M4-09 | Living Spec `MultiWanDeploymentVerificationLivingSpecTests` AC#1–10; `VerifyMultiWanDeploymentUseCase` | **DONE** |
| VRRP deployment coordinator | M4-10 | Living Spec `VrrpDeploymentLivingSpecTests` AC#1–13; `ExecuteVrrpDeploymentUseCase` | **DONE** |
| Deployment rollback + crash recovery | M4-11 | Living Spec `DeploymentRollbackRecoveryLivingSpecTests` AC#1–12; Spec §46–§49 `DeploymentRecoveryDecision` | **DONE** |
| Deployment API + Desktop workflow | M4-12 | Living Spec `DeploymentWorkflowLivingSpecTests` AC#1–11; `DeploymentService` + Deploy tab | **DONE** |
| Deployment fault and security acceptance / **M4 CLOSED** | M4-13 | Living Spec `DeploymentFaultSecurityAcceptanceLivingSpecTests` AC#1–13 all passed; `DeploymentAcceptanceHarness` shared infra (FakeRuntime / RecordingChannel / ScriptedMember / ScriptedRollbackRuntime); `ArchitectureBoundaryTests` + `DeploymentProtoContractTests` still green | **DONE** |
| Desired / committed / actual projection | M6-01 | Living Spec `DeviceStateProjectionLivingSpecTests` AC#1–10; `DeviceHashStateClassifier` + `NodeWorkflowStatusProjector`; `device_hash_states`; Desktop hash surfaces | **DONE** |
| Managed drift detection | M6-02 | Living Spec `ManagedDriftDetectionLivingSpecTests` AC#1–12; `ManagedDriftDetector` + immutable `DriftEvent`; `drift_events`; Critical blocks deploy; no auto-repair | **DONE** |
| Bounded operational jobs | M6-03 | Living Spec `BoundedOperationalJobsLivingSpecTests` AC#1–10; `OperationalJobSchedulerHostedService` + bounded priority bag; recovery>drift; no Hangfire/Quartz/broker | **DONE** |
| Desktop workflows (seven modules) | M6-04 | Living Spec `DesktopMvpWorkflowsLivingSpecTests` AC#1–12; `DriftService`/`AuditService`; Shell nav; no auto-fix | **DONE** |
| Standalone / dual-stack E2E DoD | M6-05 | Living Spec `StandaloneDualStackE2ELivingSpecTests` AC#1–10 + `StandaloneDualStackE2EAcceptanceTests`; scripted runtimes; Live CHR OFF | **DONE** |
| Multi-WAN E2E DoD | M6-06 | Living Spec `MultiWanE2ELivingSpecTests` AC#1–10; `VerifyMultiWanDeploymentUseCase` + drift/FastTrack; Live CHR OFF | **DONE** |
| VRRP / CRS E2E DoD | M6-07 | Living Spec `VrrpCrsE2ELivingSpecTests` AC#1–11; `ExecuteVrrpDeploymentUseCase` + Switch FORWARD gate + CRS fixtures; Live CHR OFF | **DONE** |
| Security / backup / restore | M6-08 | Living Spec `SecurityBackupRestoreLivingSpecTests` AC#1–10 + Integration AC#11–14; Live CHR OFF | **DONE** |
| MVP production acceptance / **M6 CLOSED** | M6-09 | Living Spec `MvpReleaseAcceptanceLivingSpecTests` AC#1–16; `docs/release/*` + `scripts/release/*`; no git tag | **DONE** |
| Path-class E2E / drift / **MVP CLOSED** | N1-07 | Living Spec `PathClassE2EDriftLivingSpecTests` AC#1–12; path-class `DriftFindingKind` + `PathClassConfigDriftVoiding`; Live CHR OFF | **DONE** |
| Routing-assurance read allowlist | M7.1-01 | `RoutingAssuranceAllowlist` + registry `RoutingSettings` / filter-rule ids; `RoutingAssuranceAllowlistTests`; no routing writes | **DONE** |
| RoutingAssuranceState persistence | M7.1-02 | Domain `RoutingAssuranceState` + `routing_assurance_states`; config≠ops hashes; Living Spec; no routing writes | **DONE** |
| Route resolution traces | M7.1-03 | `RouteResolutionTraceEngine` + Living Spec AC 1–10; upsert `TraceQueries`; no routing writes | **DONE** |
| ECMP ONE_OF bounded next-hop sets | M7.1-04 | `EcmpRouteSet` + `EcmpRouteSetBuilder`; Living Spec AC 1–9; persistence round-trip; no routing writes | **DONE** |
| Dynamic route origins (read-only) | M7.1-05 | `RouteOriginClassifier` + `DynamicRouteOriginAnalysis`; Living Spec AC 1–8; operational snapshot persistence; trace `Origin`; no routing writes | **DONE** |
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

1. Хвиля 0: stale OPEN на DONE-коді (#52, #53, #56, #67) — **DONE** 2026-08-15.
2. ~~Відкрити **M2-18** → [issue #65](https://github.com/sesquicadaver/MTDirector/issues/65).~~ → **DONE / M2 CLOSED**.
3. ~~Відкрити **M3-01** → [issue #68](https://github.com/sesquicadaver/MTDirector/issues/68).~~ → **DONE**.
4. ~~Відкрити **M3-02** → [issue #69](https://github.com/sesquicadaver/MTDirector/issues/69).~~ → **DONE**.
5. ~~Відкрити **M3-03** → [issue #70](https://github.com/sesquicadaver/MTDirector/issues/70).~~ → **DONE**.
6. ~~Відкрити **M3-04** → [issue #71](https://github.com/sesquicadaver/MTDirector/issues/71).~~ → **DONE**.
7. ~~Відкрити **M3-05** → [issue #72](https://github.com/sesquicadaver/MTDirector/issues/72).~~ → **DONE**.
8. ~~Відкрити **M3-06** → [issue #73](https://github.com/sesquicadaver/MTDirector/issues/73).~~ → **DONE**.
9. ~~Відкрити **M3-07** → [issue #74](https://github.com/sesquicadaver/MTDirector/issues/74).~~ → **DONE**.
10. ~~Відкрити **M3-08** → [issue #75](https://github.com/sesquicadaver/MTDirector/issues/75).~~ → **DONE / M3 CLOSED**.
11. ~~Відкрити **M5-01** → [issue #76](https://github.com/sesquicadaver/MTDirector/issues/76).~~ → **DONE**.
12. ~~Відкрити **M5-02** → [issue #77](https://github.com/sesquicadaver/MTDirector/issues/77).~~ → **DONE**.
13. ~~Відкрити **M5-03** → [issue #78](https://github.com/sesquicadaver/MTDirector/issues/78).~~ → **DONE**.
14. ~~Відкрити **M5-04** → [issue #79](https://github.com/sesquicadaver/MTDirector/issues/79).~~ → **DONE**.
15. ~~Відкрити **M5-05** → [issue #80](https://github.com/sesquicadaver/MTDirector/issues/80).~~ → **DONE**.
16. ~~Відкрити **M5-06** → [issue #81](https://github.com/sesquicadaver/MTDirector/issues/81).~~ → **DONE**.
17. ~~Відкрити **M5-07** → [issue #82](https://github.com/sesquicadaver/MTDirector/issues/82).~~ → **DONE**.
18. ~~Відкрити **M5-08** → [issue #83](https://github.com/sesquicadaver/MTDirector/issues/83).~~ → **DONE**.
19. ~~Відкрити **M5-09** → [issue #84](https://github.com/sesquicadaver/MTDirector/issues/84).~~ → **DONE**.
20. ~~Відкрити **M5-10** → [issue #85](https://github.com/sesquicadaver/MTDirector/issues/85).~~ → **DONE / M5 CLOSED**.
21. ~~Відкрити **M4-01** → [issue #86](https://github.com/sesquicadaver/MTDirector/issues/86).~~ → **DONE**.
22. ~~Відкрити **N1-06** → [issue #99](https://github.com/sesquicadaver/MTDirector/issues/99).~~ → **DONE**.
23. ~~Відкрити **M4-02** → [issue #87](https://github.com/sesquicadaver/MTDirector/issues/87).~~ → **DONE**.
24. ~~Відкрити **M4-03** → [issue #88](https://github.com/sesquicadaver/MTDirector/issues/88).~~ → **DONE**.
25. ~~Відкрити **M4-04** → [issue #89](https://github.com/sesquicadaver/MTDirector/issues/89).~~ → **DONE**.
26. ~~Відкрити **M4-05** → [issue #90](https://github.com/sesquicadaver/MTDirector/issues/90).~~ → **DONE**.
27. ~~Відкрити **M4-06** → [issue #91](https://github.com/sesquicadaver/MTDirector/issues/91).~~ → **DONE**.
28. ~~Відкрити **M4-07** → [issue #92](https://github.com/sesquicadaver/MTDirector/issues/92).~~ → **DONE**.
29. ~~Відкрити **M4-08** → [issue #93](https://github.com/sesquicadaver/MTDirector/issues/93).~~ → **DONE**.
30. ~~Відкрити **M4-09** → [issue #94](https://github.com/sesquicadaver/MTDirector/issues/94).~~ → **DONE**.
31. ~~Відкрити **M4-10** → [issue #95](https://github.com/sesquicadaver/MTDirector/issues/95).~~ → **DONE**.
32. ~~Відкрити **M4-11** → [issue #96](https://github.com/sesquicadaver/MTDirector/issues/96).~~ → **DONE**.
33. ~~Відкрити **M4-12** → [issue #97](https://github.com/sesquicadaver/MTDirector/issues/97).~~ → **DONE**.
34. ~~Відкрити **M4-13** → [issue #98](https://github.com/sesquicadaver/MTDirector/issues/98).~~ → **DONE / M4 CLOSED**.
35. ~~Відкрити **M6-01** → [issue #100](https://github.com/sesquicadaver/MTDirector/issues/100).~~ → **DONE**.
36. ~~Відкрити **M6-02** → [issue #101](https://github.com/sesquicadaver/MTDirector/issues/101).~~ → **DONE**.
37. ~~Відкрити **M6-03** → [issue #102](https://github.com/sesquicadaver/MTDirector/issues/102).~~ → **DONE**.
38. ~~Відкрити **M6-04** → [issue #103](https://github.com/sesquicadaver/MTDirector/issues/103).~~ → **DONE**.
39. ~~Відкрити **M6-05** → [issue #104](https://github.com/sesquicadaver/MTDirector/issues/104).~~ → **DONE**.
40. ~~Відкрити **M6-06** → [issue #105](https://github.com/sesquicadaver/MTDirector/issues/105).~~ → **DONE**.
41. ~~Відкрити **M6-07** → [issue #106](https://github.com/sesquicadaver/MTDirector/issues/106).~~ → **DONE**.
42. ~~Відкрити **M6-08** → [issue #107](https://github.com/sesquicadaver/MTDirector/issues/107).~~ → **DONE**.
43. ~~Відкрити **M6-09** → [issue #108](https://github.com/sesquicadaver/MTDirector/issues/108).~~ → **DONE / M6 CLOSED**.
44. ~~Відкрити **N1-07** → [issue #109](https://github.com/sesquicadaver/MTDirector/issues/109).~~ → **DONE / MVP CLOSED**.
45. ~~Відкрити **M7.1-01** → [issue #110](https://github.com/sesquicadaver/MTDirector/issues/110).~~ → **DONE**.
46. ~~Відкрити **M7.1-02** → [issue #111](https://github.com/sesquicadaver/MTDirector/issues/111).~~ → **DONE**.
47. ~~Відкрити **M7.1-03** → [issue #112](https://github.com/sesquicadaver/MTDirector/issues/112).~~ → **DONE**.
48. ~~Відкрити **M7.1-04** → [issue #113](https://github.com/sesquicadaver/MTDirector/issues/113).~~ → **DONE**.
49. ~~Відкрити **M7.1-05** → [issue #114](https://github.com/sesquicadaver/MTDirector/issues/114).~~ → **DONE**.
50. Відкрити **M7.1-06** → [issue #115](https://github.com/sesquicadaver/MTDirector/issues/115).

Деталі acceptance: `Initial Issue Set v0.1.md`, `M2–M6 Implementation Issue Set v0.1.md`.  
Milestones: https://github.com/sesquicadaver/MTDirector/milestones
