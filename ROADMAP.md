# MTDirector — ROADMAP реалізації v0.2

**Дата оновлення:** 3 вересня 2026 (SEC-13 UpsertDeviceHashState UoW)
**Статус:** нормативний індекс + **лінійна черга** атомарних задач
**Продукт:** MikroTik Firewall Controller (MTDirector)
**Базовий коміт аудиту:** `main` @ post-SEC-13 — M7.4 CLOSED; W6-01…W6-09 DONE; SEC-01…13 DONE; **§3.C NEXT = SEC-14 (#396)**

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
| [`docs/specs/README.md`](docs/specs/README.md) | Індекс нормативних ТЗ (корінь репо) |
| `Network Rule…М7.1.md` | Routing assurance (M7.1) |

**Spine:** `M0 → … → MVP CLOSED → M7.* → v0.2.0 → P2 (pilot RouterOS wiring) → …`

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
| M7 Post-MVP | 27 | 0 | 100% |
| P2 Pilot (read path) | 3 | 0 | 100% |
| Queue integrity + planning | 3 | 0 | TRACKER-01 + PLAN-01 + **PLAN-02 DONE** |
| P2 Pilot (write path) | 5 | 0 | **CLOSED** (P2-07…P2-11) |
| §3.C Continuous (glue + W5 + W6 + SEC) | 27 | 1 | CONT…W6-09 + SEC-01…13 **DONE**; **NEXT = SEC-14** |
| **Разом (код)** | **154** | **0** | SEC-13 closed in Application |
| **Разом (черга §3)** | **156** | **1** | **NEXT = SEC-14 (#396)** |

MVP issues (109) = **109 done + 0 remaining** — **MVP CLOSED (100%)**.  
M7.1-03 DONE. M7.1-04 DONE. M7.1-05 DONE. M7.1-06 DONE. M7.1-07 DONE. M7.1-08 DONE. M7.1-09 DONE. M7.1-10 DONE. **M7.1-11 DONE. M7.1 CLOSED.** **M7.2-01 DONE.** **M7.2-02 DONE.** **M7.2-03 DONE.** **M7.2-04 DONE. M7.2 CLOSED.** **M7.3-01 DONE.** **M7.3-02 DONE.** **M7.3-03 DONE.** **M7.3-04 DONE.** **M7.3-05 DONE.** **M7.3-06 DONE. M7.3 CLOSED.** **M7.4-01 DONE.** **M7.4-02 DONE.** **M7.4-03 DONE.** **M7.4-04 DONE.** **M7.4-05 DONE.** **M7.4-06 DONE. M7.4 CLOSED.** Post-MVP M7 = **0** open. Release **`v0.2.0`**. **P2 read path CLOSED** (P2-04…P2-06). **TRACKER-01 DONE** (#289). **PLAN-01 DONE** (#290). **P2-07 DONE** (#293). **P2-08 DONE** (#294). **P2-09 DONE** (#295). **P2-10 DONE** (#296). **P2-11 DONE** (#297). **P2 write-path CLOSED.** Desktop Add router UX [#309](https://github.com/sesquicadaver/MTDirector/pull/309) DONE. Alignment W1–W4 / W2.1–W2.2 DONE. **PLAN-02 DONE** (#339 / [#345](https://github.com/sesquicadaver/MTDirector/pull/345)). **CONT-01 DONE** (#340). **CONT-02 DONE** (#341). **W5-01 DONE** (#342). **W5-02 DONE** (#343). **W5-03 DONE** (#344). **W6-01 DONE** (#352). **§3.C NEXT = SEC-14 (#396)**.
Операційно: read-only зріз **готовий**; policy authoring Desktop **готовий**; **M3 Compiler CLOSED**; **M5 Onboarding CLOSED**; packet-path deploy **fail-closed**; standalone deploy path **готовий**; multi-WAN verify **готовий**; VRRP coordinator **готовий**; rollback/crash recovery **готовий**; deployment API/Desktop **готовий**; fault/security acceptance **DONE**; **M4 CLOSED**; desired/committed/actual projection **готовий** (M6-01); managed drift detection **готовий** (M6-02); bounded operational jobs **готовий** (M6-03); Desktop MVP workflows **готовий** (M6-04); standalone/dual-stack E2E **готовий** (M6-05); multi-WAN E2E **готовий** (M6-06); VRRP/CRS E2E **готовий** (M6-07); security/backup/restore acceptance **готовий** (M6-08); MVP production acceptance **готовий** (M6-09); **M6 CLOSED**; path-class E2E/drift **готовий** (N1-07); **MVP CLOSED**; routing-assurance read allowlist **готовий** (M7.1-01); RoutingAssuranceState persistence **готовий** (M7.1-02); RouteResolutionTrace **готовий** (M7.1-03); ECMP ONE_OF sets **готовий** (M7.1-04); dynamic route origins **готовий** (M7.1-05); RouteExpectation evaluation **готовий** (M7.1-06); reverse-path symmetry **готовий** (M7.1-07); network path profile latency probes **готовий** (M7.1-08); routing configuration vs operational drift **готовий** (M7.1-09); routing assurance Desktop viewer **готовий** (M7.1-10); routing assurance CHR acceptance **готовий** (M7.1-11); endpoint attribution **готовий** (M7.2-01); endpoint presence **готовий** (M7.2-02); endpoint mobility **готовий** (M7.2-03); endpoint mobility CHR acceptance **готовий** (M7.2-04); incident signal ingress **готовий** (M7.3-01); active-state interval **готовий** (M7.3-02); session context **готовий** (M7.3-03); sensor correlation **готовий** (M7.3-04); assessment quality **готовий** (M7.3-05); incident assessment contract **готовий** (M7.3-06); incident deny overlay **готовий** (M7.4-01); response intent feasibility **готовий** (M7.4-02); overlay compile/deploy **готовий** (M7.4-03); TTL removal plan **готовий** (M7.4-04); RESPONSE_* feedback **готовий** (M7.4-05); incident response E2E **готовий** (M7.4-06); **M7.4 CLOSED**; Post-MVP M7 = **0** open; release tag **`v0.2.0`** (2026-08-24).

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

### 2.3 Зрізи коду (стан після P2 + Add router)

| Збірка | Стан |
|--------|------|
| `Mfc.RouterOs` | … + `RouterOsReadPort` / `RouterOsSystemProbe` (P2-04) + `RouterOsSnapshotCapturePort` (P2-05) + `AddMfcRouterOs` production DI gate (P2-06) |
| `Mfc.Contracts` | `mfc.v1` inventory (+ workflow status / hash fields + `GetNodeWorkflow`) + snapshot/diff + `ZoneService` + `PolicyService` + `OnboardingService` + `DeploymentService` + `DriftService` + `AuditService` |
| `Mfc.Application` | inventory/snapshot + … + `ValidateIncidentDenyOverlayUseCase` (M7.4-01) + `AssessResponseIntentFeasibilityUseCase` (M7.4-02) + `DeployIncidentDenyOverlayUseCase` (M7.4-03) + `PlanIncidentDenyOverlayRemovalUseCase` (M7.4-04) |
| `Mfc.Controller` | health + `InventoryService` (incl. `GetNodeWorkflow`) + `SnapshotService` + `ZoneService` + `PolicyService` + `OnboardingService` + `DeploymentService` + `DriftService` + `AuditService` gRPC |
| `Mfc.Desktop` | seven MVP modules + Inventory **Add router** wizard; Contracts-only; cached inventory badge; no auto-fix drift |
| Persistence | inventory + snapshot CAS + policy lifecycle + zones + approvals/bindings + filter_artifacts + onboarding_* + deployment_* + `device_hash_states` (M6-01) + `drift_events` (M6-02) + `routing_assurance_states` (M7.1-02) + `endpoint_presence_intervals` / `endpoint_routing_contexts` (M7.2-02) + audit read store (M6-04) |
| `Mfc.Domain.Workflow` | `DeviceHashState` + classifier + `NodeWorkflowStatusProjector` (derived status; never persisted on Node) |
| `Mfc.Domain.Endpoint` | `EndpointAttributionResolver` + snapshot facts + hop chain + certainty (M7.2-01) + `EndpointPresenceInterval` / `EndpointRoutingContext` + builders + migration open/close (M7.2-02) + `EndpointMobilityHandler` / `ResponseAssessment` (M7.2-03) + `ResponseAssessmentQualityEvaluator` (M7.3-05) |
| `Mfc.Domain.Incident` | … + `IncidentResponseAssessmentContract` (M7.3-06) + `ResponseIntent` / `ResponseIntentFeasibilityMatrix` (M7.4-02) |
| `Mfc.Domain.Routing` | `RoutingAssuranceState` + config/ops snapshots + property classifier + hash contract (M7.1-02) + `RouteResolutionTraceEngine` / policy-routing trace (M7.1-03) + `EcmpRouteSet` ONE_OF bounded next-hop sets (M7.1-04) + `RouteOriginClassifier` / `DynamicRouteOriginAnalysis` read-only origins (M7.1-05) + `RouteExpectationEvaluator` / expectation findings (M7.1-06) + `ReversePathSymmetryAnalyzer` forward/reverse comparison (M7.1-07) + `NetworkPathProfileBinder` / latency evaluation bound to routing result (M7.1-08) + `RoutingDriftAnalyzer` / config vs ops drift classification (M7.1-09) |
| `Mfc.Domain.Policy` | lifecycle + Pipeline v1 + … + per-device compile orchestration (M3-07) + compiler acceptance / Switch FORWARD gate (M3-08) + `IncidentDenyOverlayCompileMerge` (M7.4-03) |
| `Mfc.Domain.Onboarding` | immutable plans + plan hasher + operation SM + write-ahead steps + bootstrap artifact + `ManagementState` (M5-01) + prerequisite validator (M5-02) + `GuardProfile` / guard verifier (M5-03) + `AnchorPlacementPlanner` (M5-04) + `OnboardingBootstrapWritePlanner` (M5-05) + `OnboardingWatchdogPlanner` (M5-06) + pass-through equivalence / enable order (M5-07) + Spec §46 recovery decision table (M5-08) |
| `Mfc.Domain.Deployment` | immutable `DeploymentPlan` + plan hasher `mfc.deployment.plan.v1` + Node/device SM + exclusive lock + write-ahead steps (M4-01) + packet-path deploy gate (N1-06) + address-list create-or-verify (M4-03) + detached chain create-or-verify (M4-04) + production watchdog planner/script (M4-05) + transition-state validation + anchor activation order/decision (M4-06) + post-activation integrity/probes/watchdog readiness (M4-07) + standalone eligibility/NO_CHANGES policy (M4-08) + multi-WAN dependency/probe gates (M4-09) + VRRP classification/order/partial-failure policy (M4-10) + recovery decision table / controller rollback (M4-11); no campaign |

**M7.4 CLOSED.** Post-MVP M7 incident response pipeline complete (M7.4-01…06). **P2 pilot read-path** (P2-04…P2-06) — production RouterOS probe + capture + DI; see §3.B5.

### 2.4 Операційний план до MVP CLOSED (історичний)

Горизонт **закрито** (2026-08-15…24): хвилі 0–7 (M2 analysis → M6 + N1-07) → **MVP CLOSED**. Далі M7 → `v0.2.0` → P2 read/write → alignment P0–P2 → **§3.C continuous** (PLAN-02).

Детальний strikethrough план хвиль і відкритих кроків прибрано (див. git history). Актуальний стан: §3 + [`docs/release/readiness.md`](docs/release/readiness.md).

**DoD кожного PR:** AC issue set; Living Spec рядок; CHANGELOG; CI Linux validate + Windows Desktop; без `pass`/`NotImplemented`; Domain/App ↛ RouterOs.

---

## 3. Лінійна черга (стан)

**Статус:** **§3.C NEXT = SEC-14 (#396).** SEC-01…13 **DONE**. W6-01…W6-09 **DONE**. Physical CRS runner remains ops residual.  
Канонічний план: [`docs/planning/continuous-queue-plan.md`](docs/planning/continuous-queue-plan.md).  
**Lab/CHR/`WriteEnabled` не є попередниками §3**. Production blockers (private audit): SEC-01…03 **DONE**. CRS runner remains ops residual.

| Logical ID | Issue | Scope |
|------------|------:|-------|
| PLAN-NBR-01 | #314 | Allowlist `/ip/neighbor/print` + `ListNeighborCandidates` + Desktop Add Router Load/Apply — **DONE** ([#315](https://github.com/sesquicadaver/MTDirector/pull/315)) |

### §3.C Continuous delivery (після alignment P0–P2)

| # | Logical ID | Issue | Scope | Status |
|--:|------------|------:|-------|--------|
| 133 | PLAN-02 | [#339](https://github.com/sesquicadaver/MTDirector/issues/339) | Seed continuous queue + no phase-stop process | **DONE** ([#345](https://github.com/sesquicadaver/MTDirector/pull/345)) |
| 134 | CONT-01 | [#340](https://github.com/sesquicadaver/MTDirector/issues/340) | Desktop Rollback + Watch (existing RPC) | **DONE** |
| 135 | CONT-02 | [#341](https://github.com/sesquicadaver/MTDirector/issues/341) | Neighbor apply fills VRRP member b | **DONE** |
| 136 | W5-01 | [#342](https://github.com/sesquicadaver/MTDirector/issues/342) | `ListPolicies` catalog browse (Contracts + Desktop) | **DONE** |
| 137 | W5-02 | [#343](https://github.com/sesquicadaver/MTDirector/issues/343) | ManagementPath / FastTrack Desktop RPC + surface | **DONE** |
| 138 | W5-03 | [#344](https://github.com/sesquicadaver/MTDirector/issues/344) | Typed deployment semantic policy diff | **DONE** |
| 139 | W6-01 | [#352](https://github.com/sesquicadaver/MTDirector/issues/352) | Operator-readable snapshot/diff, VRRP surface, captured filter | **DONE** |
| 140 | W6-02 | [#354](https://github.com/sesquicadaver/MTDirector/issues/354) | VRRP pair consistency (config + logical firewall) | **DONE** |
| 141 | W6-03 | [#356](https://github.com/sesquicadaver/MTDirector/issues/356) | StartCapture node_id fan-out to Node members | **DONE** |
| 142 | W6-04 | [#358](https://github.com/sesquicadaver/MTDirector/issues/358) | Onboarding Rollback + Watch (hub replay + Desktop) | **DONE** |
| 143 | W6-05 | [#360](https://github.com/sesquicadaver/MTDirector/issues/360) | GetNode Reachability from probe (LastSupportState + observation) | **DONE** |
| 144 | W6-06 | [#362](https://github.com/sesquicadaver/MTDirector/issues/362) | Policies typed revision Diff rows (kind/detail) | **DONE** |
| 145 | W6-07 | [#364](https://github.com/sesquicadaver/MTDirector/issues/364) | Policies Diff baseline from catalog picker | **DONE** |
| 146 | W6-08 | [#366](https://github.com/sesquicadaver/MTDirector/issues/366) | Durable GetNode Unreachable (Device.LastObservedReachability) | **DONE** |
| 147 | W6-09 | [#369](https://github.com/sesquicadaver/MTDirector/issues/369) | Policies ReorderRules via Move up/down (no UUID paste) | **DONE** |
| 148 | SEC-01 | [#371](https://github.com/sesquicadaver/MTDirector/issues/371) | Reject system actor spoofing via `x-mfc-actor` gRPC metadata | **DONE** |
| 149 | SEC-02 | [#372](https://github.com/sesquicadaver/MTDirector/issues/372) | Production deployment artifact materializer + observed hash | **DONE** |
| 150 | SEC-03 | [#373](https://github.com/sesquicadaver/MTDirector/issues/373) | Cryptographically correct audit hash chain | **DONE** |
| 151 | SEC-04 | [#377](https://github.com/sesquicadaver/MTDirector/issues/377) | INTERNAL_CA directory trusted CA store + revocation | **DONE** |
| 152 | SEC-05 | [#378](https://github.com/sesquicadaver/MTDirector/issues/378) | Atomic mutation + idempotency + audit boundary | **DONE** |
| 153 | SEC-06 | [#380](https://github.com/sesquicadaver/MTDirector/issues/380) | Incident assessment gRPC surface | **DONE** |
| 154 | SEC-07 | [#383](https://github.com/sesquicadaver/MTDirector/issues/383) | Extend atomic mutation boundary | **DONE** |
| 155 | SEC-08 | [#385](https://github.com/sesquicadaver/MTDirector/issues/385) | Connection profile + deployment UoW | **DONE** |
| 156 | SEC-09 | [#387](https://github.com/sesquicadaver/MTDirector/issues/387) | Onboarding workflow UoW | **DONE** |
| 157 | SEC-10 | [#389](https://github.com/sesquicadaver/MTDirector/issues/389) | Incident overlay expire UoW | **DONE** |
| 158 | SEC-11 | [#391](https://github.com/sesquicadaver/MTDirector/issues/391) | Drift detect + response-feedback UoW | **DONE** |
| 159 | SEC-12 | [#392](https://github.com/sesquicadaver/MTDirector/issues/392) | CaptureSnapshot persist+audit UoW | **DONE** |
| 160 | SEC-13 | [#394](https://github.com/sesquicadaver/MTDirector/issues/394) | UpsertDeviceHashState UoW | **DONE** |
| 161 | SEC-14 | [#396](https://github.com/sesquicadaver/MTDirector/issues/396) | OpenEndpointPresence UoW | **OPEN** |

Повна історія закритих рядків §3.A / §3.B (M0–M6 + N1 + M7 + P2) збережена в git history (до docs-purge) і зведена в [`ISSUES.md`](ISSUES.md) + §2.2 DONE.

| Сегмент | Підсумок |
|---------|----------|
| §3.A До MVP CLOSED | 95 атомарних задач — **усі DONE** → **MVP CLOSED** (N1-07) |
| §3.B1–B4 Post-MVP M7 | 27 задач — **усі DONE** → **M7.4 CLOSED** / `v0.2.0` |
| §3.B5 P2 read path | P2-04…P2-06 — **CLOSED** |
| §3.B6 Tracker / plan | TRACKER-01 (#289), PLAN-01 (#290) — **DONE** |
| §3.B7 P2 write path | P2-07…P2-11 — **CLOSED** |
| Поза чергою (історія) | Desktop Add router — [PR #309](https://github.com/sesquicadaver/MTDirector/pull/309); alignment W1–W4 / W2.1–W2.2 — **DONE** |
| **§3.C Continuous** | PLAN-02 + CONT-01…02 + W5 + W6 + SEC-01…13 — **DONE**; **NEXT = SEC-14** |

Pilot (ops, parallel): [`docs/operations/pilot-runbook.md`](docs/operations/pilot-runbook.md).

---
## 4. Підсумок лічильників

| Сегмент | У черзі | Примітка |
|---------|--------:|----------|
| До MVP CLOSED | 0 | **MVP CLOSED** (N1-07 DONE) |
| Post-MVP M7 | 0 | **M7.4 CLOSED** |
| P2 Pilot (read path) | 0 | **CLOSED** (P2-04…P2-06) |
| Queue integrity + planning | 0 | TRACKER-01 + PLAN-01 + PLAN-02 **DONE** |
| P2 Pilot (write path) | 0 | **CLOSED** (P2-07…P2-11) |
| §3.C Continuous | 1 | SEC-01…13 **DONE**; SEC-14 **OPEN** |
| **Нереалізовано (§3)** | **1** | **§3.C NEXT = SEC-14 (#396)** |
| DONE у коді (§2.2) | 139 | …+P2-06; release **`v0.2.0`**; alignment P0–P2 DONE |

GitHub-трекер вирівняно **TRACKER-01** (#289, 2026-08-26): stale OPEN #91–#95, #125–#136 closed. Хвиля 0 (2026-08-15): #52, #53, #56, #67 CLOSED.

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
| RouteExpectation evaluation | M7.1-06 | `RouteExpectationEvaluator` + `RouteExpectationCodes`; upsert evaluates expectations vs traces; Living Spec AC 1–10; no routing writes | **DONE** |
| Reverse-path symmetry analysis | M7.1-07 | `ReversePathSymmetryAnalyzer` + `ReversePathSymmetryResults`; forward/reverse table/VRF/egress/decision compare; trace attachment + evaluator findings; Living Spec AC 1–8; no routing writes | **DONE** |
| IncidentSignal ingress contract | M7.3-01 | Domain `IncidentSignal` + `IncidentSignalIngressGuard`; `IngestIncidentSignalUseCase`; Living Spec AC 1–10; no raw syslog store | **DONE** |
| Historical ActiveStateInterval resolver | M7.3-02 | `ActiveStateIntervalResolver` + `ResolveActiveStateIntervalUseCase`; Living Spec AC 1–10; scripted deployment timeline only | **DONE** |
| On-demand session context | M7.3-03 | `IncidentSessionContextResolver` + `ConnectionTrackingAllowlist`; Living Spec AC 1–10; on-demand only; no full-table store | **DONE** |
| Sensor observation correlation | M7.3-04 | `SensorObservationCorrelationResolver` + `CorrelateSensorObservationUseCase`; Living Spec AC 1–10; scripted trace only; no routing writes | **DONE** |
| Assessment visibility/confidence | M7.3-05 | `ResponseAssessmentQualityEvaluator` + `EvaluateResponseAssessmentQualityUseCase`; Living Spec AC 1–10; `response_assessments` columns | **DONE** |
| Incident ↔ assessment contract | M7.3-06 | `IncidentResponseAssessmentContract` + `BindIncidentResponseAssessmentUseCase`; Living Spec AC 1–10; **M7.3 CLOSED** | **DONE** |
| Incident overlay | M7.4 | INCIDENT_PRE_STATE_DENY stage + INCIDENT_DENY_OVERLAY kind | **DONE** (M7.4-01) |
| Production RouterOS read probe | P2-04 | `RouterOsReadPort` + live API-SSL probe | **DONE** (#280) |
| Production snapshot capture | P2-05 | `RouterOsSnapshotCapturePort` + stable-read pipeline | **DONE** (#281) |
| Production RouterOS DI | P2-06 | `AddRouterOsProductionServices` + pilot Living Spec | **DONE** (#282) |
| Deploy materializer + observed hash | SEC-02 | `FilterArtifactStoreDeploymentArtifactMaterializer` + `ObservedManagedResourceHash`; Living Spec `DeploymentArtifactMaterializerSec02LivingSpecTests` | **DONE** (#372) |
| Audit hash chain predecessor bytes | SEC-03 | `AuditEventHashing` + Serializable/`pg_advisory_xact_lock` + unique `PreviousEventHash`; Living Spec `AuditEventHashChainSec03LivingSpecTests` | **DONE** (#373) |
| INTERNAL_CA directory CA store + revocation | SEC-04 | `DirectoryRouterOsTrustedCaStore` + `TrustedCa:RevocationMode`; Living Spec `TrustedCaStoreSec04LivingSpecTests` | **DONE** (#377) |
| Atomic mutation + idempotency + audit | SEC-05 | `IUnitOfWork` inventory/policy + ambient audit join; Living Spec `MutationAtomicitySec05LivingSpecTests` | **DONE** (#378) |
| Incident assessment gRPC | SEC-06 | `incident.proto` + `IncidentGrpcService`; Living Spec `IncidentGrpcSec06LivingSpecTests` | **DONE** (#380) |
| Extended atomic mutation boundary | SEC-07 | Zones + policy draft/rules/approval/validate/exception metadata via `IUnitOfWork`; Living Spec `MutationAtomicitySec07LivingSpecTests` | **DONE** (#383) |
| Connection profile + deployment UoW | SEC-08 | Profile upsert + deployment plan/start/rollback terminal persists; Living Spec `MutationAtomicitySec08LivingSpecTests` | **DONE** (#385) |
| Onboarding workflow UoW | SEC-09 | Onboarding plan/start/rollback terminal persists; Living Spec `MutationAtomicitySec09LivingSpecTests` | **DONE** (#387) |
| Incident overlay expire UoW | SEC-10 | `ExpireIncidentDenyOverlayBindingUseCase` UoW; Living Spec `MutationAtomicitySec10LivingSpecTests` | **DONE** (#389) |
| Drift detect + response-feedback UoW | SEC-11 | `DetectManagedDriftUseCase` + `EmitResponseFeedbackUseCase` store+audit UoW; Living Spec `MutationAtomicitySec11LivingSpecTests` | **DONE** (#391) |
| CaptureSnapshot persist+audit UoW | SEC-12 | `CaptureSnapshotUseCase` persist+audit UoW; Living Spec `MutationAtomicitySec12LivingSpecTests` | **DONE** (#392) |
| Device hash-state upsert UoW | SEC-13 | `UpsertDeviceHashStateUseCase` UoW; Living Spec `MutationAtomicitySec13LivingSpecTests` | **DONE** (#394) |
| Endpoint presence multi-store UoW | SEC-14 | `OpenEndpointPresenceUseCase` UoW | **OPEN** (#396) |
| Production onboarding runtime | P2-07 | `RouterOsOnboardingRuntime` over onboarding writers | **DONE** (#293) |
| Production deployment runtime | P2-08 | `RouterOsDeploymentRuntime` over deployment session | **DONE** (#294) |
| Watchdog residue cleanup | P2-09 | Production `RouterOsWatchdogResidueCleanupPort` | **DONE** (#295) |
| Write-path DI gate | P2-10 | `AddRouterOsWriteServices` + `WriteEnabled` flag | **DONE** (#296) |
| Write-path pilot runbook | P2-11 | `WritePathPilotLivingSpecTests` + `pilot-runbook.md` write checklist | **DONE** (#297) |
| Seed MikroTik neighbor candidates | PLAN-NBR-01 | `NeighborCandidatesLivingSpecTests` + allowlist/use-case/Desktop tests | **DONE** (#314 / #315) |
| Continuous queue (no phase-stop) | PLAN-02 | `docs/planning/continuous-queue-plan.md` + §3.C | **DONE** (#339 / #345) |
| Desktop Rollback Watch | CONT-01 | Deployment Watch after Rollback; Living Spec `Ac6e` | **DONE** (#340) |
| Neighbor apply VRRP member b | CONT-02 | Add router Apply → `PairMemberB*`; Living Spec `Ac2f` | **DONE** (#341) |
| ListPolicies catalog | W5-01 | Contracts + Desktop catalog browse; Living Spec `Ac5d` | **DONE** (#342) |
| ManagementPath / FastTrack Desktop | W5-02 | RPC + Desktop surface of existing analysis | **DONE** (#343) |
| Typed deploy policy semantic diff | W5-03 | Contracts typed entries (not only `repeated string`); Living Spec `Ac6f` | **DONE** (#344) |

Оновлювати рядок **Статус** при закритті issue; **§3.C NEXT = SEC-14 (#396)**. SEC-13 **DONE**. CRS/physical lab runner stays ops residual.

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
- винесення задач з лінійної черги «вперед» без закритих попередників **усередині §3**;
- закриття delivery-хвилі з `NEXT = none` без засіву наступного траншу в тому ж циклі;
- трактування lab/GNS3/CHR/`WriteEnabled` phase N як стоп-гейту для Desktop/Contracts PR.

---

## 7. Операційний старт

1. **§3.C NEXT = SEC-14 (#396)**. SEC-13 **DONE**. Береться лише відкритий рядок §3; лаба **не** блокує. Physical CRS runner — ops residual.
2. Lab/pilot RouterOS (паралельно): [`docs/operations/pilot-runbook.md`](docs/operations/pilot-runbook.md) (`Enabled` / `WriteEnabled`).
3. Desktop реєстрація пристрою: Inventory **Add router** — [`docs/development/connection-profiles.md`](docs/development/connection-profiles.md).
4. Acceptance / readiness: [`docs/release/mvp-acceptance.md`](docs/release/mvp-acceptance.md), [`docs/release/readiness.md`](docs/release/readiness.md). Continuous plan: [`docs/planning/continuous-queue-plan.md`](docs/planning/continuous-queue-plan.md).
5. Build / run / package: [`docs/howto/build-and-run.md`](docs/howto/build-and-run.md).

Мапінг ID → GitHub: [`ISSUES.md`](ISSUES.md).  
Milestones: https://github.com/sesquicadaver/MTDirector/milestones
