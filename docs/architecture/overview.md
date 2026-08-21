# Architecture overview

MTDirector (MikroTik Firewall Controller) is a **modular monolith**: one deployable Controller process, one Desktop client, and clear assembly boundaries.

```text
Avalonia Desktop  --gRPC/mTLS-->  ASP.NET Core Controller
                                        |
                         +--------------+--------------+
                         |              |              |
                      Domain      Infrastructure    RouterOs
                   (pure model)   (PostgreSQL/EF)  (API-SSL only)
```

Normative detail lives in `TOR-1.md` and the MVP specifications. This folder records **decisions** (ADRs), not a restatement of the full ТЗ.

## Domain progress (M1)

| Slice | Status | Location |
|-------|--------|----------|
| Inventory aggregates | Done (M1-01) | `src/Mfc.Domain/Inventory/` |
| Snapshot / capability VOs | Done (M1-02) | `src/Mfc.Domain/Snapshots/`, `src/Mfc.Domain/Capabilities/` |
| Persistence schema | Done (M1-03) | `sites`/`nodes`/`devices`, `capture_operations`, `snapshot_*` |
| Secure connection profiles | Done (M1-04) | AES-256-GCM envelope secrets + INTERNAL_CA/SPKI trust |
| Application ports / use cases | Done (M1-05) | `src/Mfc.Application/Inventory/`, `Snapshots/` + narrow ports |
| RouterOS word-length codec | Done (M1-06) | `src/Mfc.RouterOs/Protocol/ApiWordLengthCodec.cs` |
| RouterOS sentence codec | Done (M1-07) | `ApiSentenceParser` / `ApiSentenceEncoder` |
| Tagged API session | Done (M1-08) | `src/Mfc.RouterOs/Session/RosSession.cs` |
| Authenticated API-SSL | Done (M1-09) | `src/Mfc.RouterOs/Transport/AuthenticatedRosConnection.cs` |
| Typed read executor | Done (M1-10) | `src/Mfc.RouterOs/Commands/RosReadCommandExecutor.cs` |
| System/service discovery | Done (M1-11) | `src/Mfc.RouterOs/Discovery/SystemServiceDiscovery.cs` |
| Interface/address discovery | Done (M1-12) | `src/Mfc.RouterOs/Discovery/InterfaceAddressDiscovery.cs` |
| Firewall filter discovery | Done (M1-13) | `src/Mfc.RouterOs/Discovery/FirewallFilterDiscovery.cs` |
| Routing/dependency discovery | Done (M1-14) | `src/Mfc.RouterOs/Discovery/RoutingDependencyDiscovery.cs` |
| VRRP discovery | Done (M1-15) | `src/Mfc.RouterOs/Discovery/VrrpDiscovery.cs` |
| Bridge/VLAN/switch discovery | Done (M1-16) | `src/Mfc.RouterOs/Discovery/BridgeSwitchDiscovery.cs` |
| Packet-path allowlist | Done (N1-01) | `src/Mfc.RouterOs/Commands/PacketPathAllowlist.cs` |
| Capability profile | Done (M1-17) | `src/Mfc.RouterOs/Capabilities/CapabilityProfileEvaluator.cs` |
| Packet-path topology graph | Done (N1-02) | `src/Mfc.RouterOs/Discovery/PacketPathTopologyDiscovery.cs` |
| Packet-path class | Done (N1-03) | `src/Mfc.RouterOs/Discovery/PacketPathClassifier.cs` |
| Node topology validation | Done (M1-18) | `src/Mfc.Domain/Topology/NodeTopologyValidator.cs` |
| Stable-read coordinator | Done (M1-19) | `src/Mfc.RouterOs/Snapshot/StableReadCoordinator.cs` |
| Raw snapshot assembly | Done (M1-20) | `src/Mfc.RouterOs/Snapshot/RawSnapshotAssembler.cs` |
| Canonicalization primitives | Done (M1-21) | `src/Mfc.Domain/Canonicalization/` |
| Menu-specific canonical snapshots | Done (M1-22) | `src/Mfc.RouterOs/Snapshot/DiscoveryCanonicalProjector.cs` |
| Persist canonical snapshots | Done (M1-23) | `EfSnapshotStore`, `snapshot_capture_sections`, Brotli content-addressed payloads |
| Semantic snapshot diff | Done (M1-24) | `src/Mfc.Domain/Diff/`, `CompareSnapshotsUseCase`, `LoadCanonicalSectionsAsync` |
| Inventory/discovery gRPC | Done (M1-25) | `Protos/mfc/v1/inventory.proto`, `InventoryGrpcService` (VS §9.2; ValidateDeviceConnection ← DiscoverDeviceUseCase) |
| Snapshot/diff gRPC | Done (M1-26) | `Protos/mfc/v1/snapshots.proto`, `SnapshotGrpcService` (VS §9.3; DiffEntry = Canonical Spec §30; Issue Set CaptureSnapshot→StartCapture) |
| Desktop inventory tree | Done (M1-27) | Avalonia Site→Node→Device tree; `ListNodes` RPC; Contracts-only Desktop client; cached-on-error refresh |
| Desktop snapshot viewer | Done (M1-28) | Read-only Avalonia viewer; `SnapshotSummary.sections` statuses; config/obs split; sanitized export |
| Desktop semantic diff viewer | Done (M1-29) | Avalonia CompareSnapshots UI; section-grouped DiffEntry rows; no local recompute |
| Standalone CHR acceptance | Done (M1-30) | In-process vertical-slice suite + live CHR TLS gate + lab provision script |
| Multi-WAN CHR acceptance | Done (M1-31) | Failover/balanced routing+NAT+mangle slice; config≠obs route diffs; lab provision |
| VRRP CHR acceptance | Done (M1-32) | Active/passive + split-master; per-VRID roles; role≠config hash; topology blockers; lab provision |
| Fault-injection suite | Done (M1-33) | Protocol/session/stable-read matrix + snapshot capture faults; typed codes; no orphan completes |
| M1 acceptance package | Done (M1-34) | Operator docs + acceptance report; **M1 CLOSED** |
| Policy document lifecycle | Done (M2-01) | `Policy` / `PolicyRevision`, MFC-CJ1 hash, Brotli `policy_revisions` |
| Policy Pipeline v1 + chain contracts | Done (M2-02) | Fixed stages + owner/effect matrix; company-only `ChainContract` |
| Address objects + selectors | Done (M2-03) | Typed entries, interval algebra, `AddressSelectorEvaluator` |
| Service objects + selectors | Done (M2-04) | Numeric protocols, ports, ICMP split, `ServiceSelectorEvaluator` |
| Logical zones + Node bindings | Done (M2-05) | `ZoneDefinition` / `NodeZoneBinding`, ZoneService, Desktop Zones |
| Zone VETH/VLAN/bridge resolve | Done (N1-05) | `topology.container-veth` / `shared-veth`; marker expand in `ZoneResolveEngine`; live capture←projector residual (M1-22) |
| Typed policy rules | Done (M2-06) | Typed rules/predicates; App CAS CRUD; `PolicyService`; thin Desktop list |
| Deterministic policy composition | Done (M2-07) | `EffectivePolicyComposer`; `ComposeEffectivePolicy`; logical IncrementalHash |
| Scoped deny-stage exceptions | Done (M2-08) | `ExceptionMetadata`; `UpdateExceptionMetadata`; exemption-stage insert; exception hash slot |
| Bounded packet predicate algebra | Done (M2-09) | Domain cubes + exception interval subset/overlap; conservative subset/subtract; `PREDICATE_COMPLEXITY_LIMIT` |
| Structural + satisfiability analysis | Done (M2-10) | `PolicyAnalysisEngine`; `RULE_*` compose blockers before sequence; disabled rules included |
| Duplicate / shadow / overlap analysis | Done (M2-11) | `PolicySequenceAnalysis`; fail-closed equal; witness packets; sequence BLOCKERs on compose map to FailedPrecondition |
| Actual filter CFG / pre-anchor | Done (M2-12) | `ActualFilterAnalysis`; bounded CFG; pre-anchor BLOCKERs; implicit accept ≠ managed default; actual context hash |
| Packet-path FORWARD blockers | Done (N1-04) | `PacketPathAnalysis`; HW/INDETERMINATE BLOCKERs; MIXED not those codes; packet-path context hash |
| Management-path safety | Done (M2-13) | `ManagementPathAnalysis`; API-SSL + source + guard-before-anchor; VIP-only/unknown INDETERMINATE; SYSTEM tests + witnesses |
| Topology / dependency safety | Done (M2-14) | `TopologyDependencyAnalysis`; VRRP proto-112 + sync; missing member; split-master vector; STRICT_RPF_*; RAW/NAT/Mangle; SWITCH FORWARD; observation hash excluded from policy context |
| FastTrack policy validation | Done (M2-15) | `FastTrackAnalysis`; IPv4 FORWARD STATE_PRELUDE TCP/UDP; PCC/balanced/marks/VRF/IPsec/pre-anchor fail-closed; fallback flag; risk HIGH; 5-arg hash isolation |
| Policy tests / semantic diff / risk | Done (M2-16) | `PolicyEvidenceAnalysis`; MANAGED_ONLY/NODE_EFFECTIVE; UUID diff + object impact; packet-space classes; risk floor; 6-arg hash isolation |
| Approval / desired binding | Done (M2-17) | `PolicyApprovalGate` / `PolicyBindingGate`; immutable analysis run; bundle hash + SoD; binding ≠ deploy; exception expiry without deploy |
| Policy authoring / review Desktop | Done (M2-18) | Contracts-only catalog editors + Validate/Submit/Approve/Bind; Deploy tab M4-12 DONE; **M2 CLOSED** |
| RouterOS filter artifact model | Done (M3-01) | `RouterOsFilterArtifact` + MFC-CJ1 canonical writer; physical_semantics/artifact_id/resource_hash; no `.id`/API commands |
| Managed chain namespace / layout | Done (M3-02) | `ManagedChainNamespace` + `ManagedChainLayoutBuilder`; mfc4/mfc6 root+deny layout; Pipeline v1 order; no guard/physical anchors |
| Content-addressed address lists | Done (M3-03) | `AddressListCompileSession` + `AddressPrefixEncoder`; intern by content hash; `mfc{4\|6}.a.<16-hex>`; negated universe-minus-exclusions; layout v1 limits |
| Zone and service variants | Done (M3-04) | `ZoneServiceVariantCompiler` + `PortMatcherEncoder`; direct interface-list or finite expansion; ICMP variants; WAN/running ignored |
| Filter matchers and regular effects | Done (M3-05) | `FilterMatcherEffectCompiler` + `RouterOsCompilerProfile`; exact tokens; REJECT≠DROP; exceptions=`return`; input order |
| FastTrack pairs and terminal rules | Done (M3-06) | FastTrack adjacent pair + `hw-offload=no`; `ChainTerminalCompiler`; context fail-closed |
| Per-device compile + artifact storage | Done (M3-07) | `DeviceFilterCompiler` + `filter_artifacts`; semantic summary RPC; fail-closed Node compile |
| Compiler acceptance / M3 CLOSED | Done (M3-08) | Living Spec topology vectors; Switch FORWARD forbidden; deterministic compile |
| Onboarding domain + persistence | Done (M5-01) | Immutable plans, operation SM, write-ahead journal, `ManagementState`, EF `OnboardingSchemaM501` |
| Onboarding prerequisite validation | Done (M5-02) | Typed facts + `OnboardingPrerequisiteValidator`; Spec §58 codes; no user/service/device-mode writes |
| Management guard verification | Done (M5-03) | Typed `GuardProfile` + `OnboardingGuardVerifier`; breadth/default-route; plan hash; no guard writes |
| Explicit anchor placement | Done (M5-04) | Operator intent + `AnchorPlacementPlanner`; fingerprint/rank; no `.id`; preview before/after |
| Restricted bootstrap writer | Done (M5-05) | Allowlisted filter add/set/remove; disabled anchors; Spec §23 ID; no generic Write namespace |
| Scheduler proof + watchdog | Done (M5-06) | Fixed no-op proof; deadline+startup; source hash; TTL/commit margin; collision fail-closed |
| Onboarding execution + verification | Done (M5-07) | Stage/arm/enable/verify/disarm/commit; pass-through equivalence; Node MANAGED only fully |
| Onboarding rollback + crash recovery | Done (M5-08) | Disable-first exact rollback; Spec §46 decision table; no automatic adoption |
| Onboarding API + Desktop workflow | Done (M5-09) | `OnboardingService` RPCs + Desktop checklist/placement/recovery; plan_hash; no script source |
| Onboarding integration acceptance / M5 CLOSED | Done (M5-10) | Topology Living Spec + testlab dual-stack/CRS; crash/watchdog/guard; no partial managed Node |
| Deployment plan + persistence | Done (M4-01) | Immutable plan + Node/device SM + lock + journal; EF `DeploymentSchemaM401`; no campaign / writer |
| Packet-path deploy gate | Done (N1-06) | `PACKET_PATH_*` fail-closes Router/VRRP deploy; CPU/MIXED allowed; no offload writes |
| Restricted deployment writer | Done (M4-02) | Allowlisted `RouterOsDeploymentSession` + managed-state reader; no `Mfc.RouterOs.Write` |
| Address-list create-or-verify | Done (M4-03) | `AddressListCreateOrVerify` + `StageAddressListUseCase`; no AL set/remove |
| Detached chain staging | Done (M4-04) | `FilterChainCreateOrVerify` + `StageDetachedChainsUseCase`; deny before root |
| Production rollback watchdog | Done (M4-05) | `DeploymentWatchdogScript` + `DeploymentWatchdogWriter`; deadline/startup; VRRP arm gate |
| Transition validation + anchor activation | Done (M4-06) | `TransitionStateValidator` + `ActivateAnchorsUseCase`; management-critical last; no blind retry |
| Probes + post-activation verification | Done (M4-07) | `PostActivationVerification` + `VerifyDeploymentActivationUseCase`; API_SSL/ROUTER_PING; fresh session |
| Standalone Node coordinator | Done (M4-08) | `ExecuteStandaloneDeploymentUseCase`; NO_CHANGES; verify-fail rollback; commit snapshot |
| Multi-WAN deployment verification | Done (M4-09) | `MultiWanDeploymentVerification` + `VerifyMultiWanDeploymentUseCase`; no forced failover |
| VRRP deployment coordinator | Done (M4-10) | `VrrpDeploymentPolicy` + `ExecuteVrrpDeploymentUseCase`; standby-first; no partial commit |
| Rollback + crash recovery | Done (M4-11) | `DeploymentRecoveryDecision` + rollback/recover use cases; only COMMITTED keeps new |
| Deployment API + Desktop workflow | Done (M4-12) | `DeploymentService` + Deploy tab; plan_hash start; streaming Watch; audited mutations |
| Deployment fault and security acceptance | Done (M4-13) **M4 CLOSED** | Living Spec AC 1–13 all passed; `DeploymentFaultSecurityAcceptanceLivingSpecTests` + `DeploymentAcceptanceHarness`; standalone/VRRP/multi-WAN/rollback/security vectors; ArchitectureBoundary green |
| Desired / committed / actual projection | Done (M6-01) | `DeviceHashState` + classifier + `NodeWorkflowStatusProjector`; EF `device_hash_states`; `GetNodeWorkflow`; Desktop desired/committed/actual hashes |
| Managed drift detection | Done (M6-02) | `ManagedDriftDetector` + `DriftEvent` (immutable); EF `drift_events`; deploy gate blocks Critical; no auto-repair; Living Spec AC 1–12 |
| Bounded operational jobs | Done (M6-03) | `OperationalJobSchedulerHostedService` + bounded priority bag; recovery > drift; expired-exception DB-only; restricted watchdog cleanup; no broker; Living Spec AC 1–10 |
| Desktop MVP workflows | Done (M6-04) | Seven modules + `DriftService`/`AuditService` read paths; Shell nav; no auto-fix; Living Spec AC 1–12 |
| Standalone / dual-stack E2E | Done (M6-05) | Living Spec AC 1–10 + Integration inventory→capture→onboarding; scripted runtimes; Live CHR OFF |
| Multi-WAN E2E | Done (M6-06) | Living Spec AC 1–10; failover/PCC/probes/FastTrack/drift; scripted runtimes; Live CHR OFF |
| VRRP / CRS E2E | Done (M6-07) | Living Spec AC 1–11; VRRP coordinator + Switch FORWARD gate + CRS fixtures; Live CHR OFF |
| Security / backup / restore acceptance | Done (M6-08) | Living Spec AC 1–10 + Integration pg_dump/restore AC 11–14; no live CHR |
| MVP production acceptance | Done (M6-09) **M6 CLOSED** | Release docs + `scripts/release/*` + Living Spec AC 1–16; Live CHR OFF; no release tag in PR |
| Remaining delivery order | See ROADMAP v0.2 §3 | Linear queue continues at N1-07 (#109); MVP CLOSED after N1-07 |

## ADRs

| ID | Title | Status |
|----|-------|--------|
| [0001](adr/0001-modular-monolith.md) | Modular monolith | Accepted |
| [0002](adr/0002-routeros-api-ssl.md) | RouterOS API-SSL transport | Accepted |
| [0003](adr/0003-node-deployment-atomicity.md) | Node deployment atomicity | Accepted |
| [0004](adr/0004-postgresql-source-of-truth.md) | PostgreSQL as source of truth | Accepted |
| [0005](adr/0005-no-direct-desktop-routeros-access.md) | No Desktop→RouterOS access | Accepted |

## Related development docs

- [Local environment](../development/local-environment.md)
- [Testing](../development/testing.md)
- [Database migrations](../development/database-migrations.md)
- [CHR lab isolation](../development/chr-lab.md)
- [CI](../development/ci.md)
