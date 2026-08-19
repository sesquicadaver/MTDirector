# Testing

Commands match CI ([ci.md](ci.md)). Always use the pinned SDK from `global.json`.

## Full local gate

```bash
dotnet restore MikroTikFirewallController.sln --locked-mode
dotnet format MikroTikFirewallController.sln --verify-no-changes --no-restore
dotnet build MikroTikFirewallController.sln -c Release --no-restore
dotnet test MikroTikFirewallController.sln -c Release --no-build
dotnet list MikroTikFirewallController.sln package --vulnerable --include-transitive
```

Working tree must stay clean after build/test.

## Project map

| Project | Role |
|---------|------|
| `tests/Mfc.UnitTests` | Unit + architecture boundary tests + coverage (incl. Inventory, Snapshots/Capabilities, RouterOS discovery + capability + N1 topology/path class, node topology validation, stable-read, raw snapshot, canonicalization, menu projector, capture idempotency/audit, semantic diff M1-24, inventory/snapshot proto contracts M1-25/M1-26, Desktop inventory tree M1-27, Desktop snapshot viewer M1-28, Desktop semantic diff viewer M1-29, fault-injection matrix M1-33) |
| `tests/Mfc.IntegrationTests` | Controller health + Inventory/Snapshot gRPC host (M1-25/M1-26/ListNodes M1-27), Desktop connection, PostgreSQL bootstrap + inventory/snapshot persist (Testcontainers), standalone/multi-WAN/VRRP vertical-slice acceptance M1-30/M1-31/M1-32, fault-injection acceptance M1-33 |
| `tests/Mfc.RouterOs.IntegrationTests` | RouterOS markers + CHR skeleton contracts + optional live CHR TLS gate (`MFC_CHR_STANDALONE_HOST`) |

## Living Specification — semantic diff (M1-24)

Canonical Spec §29–35 / Initial Issue Set M1-24 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Config vs observation separation | `SemanticDiffEngine` / `RecordMatcher` | `ObservationVrrpRoleChangeIsStateChangedNotModified` |
| MOVED same fingerprint, different ordinal | `RecordMatcher` phase 3 | `OrderedExactFingerprintMoveEmitsMoved` |
| `fwc:rule` → MODIFIED | `FwcRuleMarker` + ControllerId match | `FwcMarkerActionChangeIsModified` |
| Unmanaged unique fingerprint move | ExactFingerprint | `UnmanagedUniqueFingerprintMoveIsMovedExactFingerprint` |
| Unmanaged content change → REMOVED+ADDED | Conservative | `UnmanagedContentChangeIsRemovedPlusAddedWithoutModified` |
| Ordered mid-list insert | Ordered matching | `OrderedMidListInsertProducesAddedNotChaos` |
| Address-list set / order irrelevant | NaturalKey | `AddressListOrderIrrelevantAndNewEntryIsAdded` |
| Interface-list members CSV set | `FieldDiffComparer` | `InterfaceListMembersCsvSetFieldDiff` |
| VRRP STATE_CHANGED | Observation domain | `VrrpObservationStateChanged` |
| Determinism | §35 sort | `DiffIsDeterministicAcrossRuns` |
| Identical empty | Utf8Bytes short-circuit | `IdenticalSectionsProduceEmptyDocument` |
| >20000 ordered → DIFF_COMPLEXITY_LIMIT | `DiffLimits` / `OrderedDiff` | `HugeOrderedSectionEmitsComplexityWarningWithoutThrow` |
| Duplicate unmanaged fingerprints → no false MOVED | Phase 3/4 uniqueness | `DuplicateUnmanagedIdenticalFingerprintsDoNotFalseMove` |

Filter: `dotnet test --filter FullyQualifiedName~SemanticDiff`.

## Living Specification — inventory gRPC (M1-25)

Vertical Slice §9.2 / Initial Issue Set M1-25 AC → module → tests (Issue Spec = Vertical Slice; Issue Set `DiscoverDevice`/`GetDiscoveryStatus` names are not on the wire):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| VS §9.2 RPC surface | `inventory.proto` / `InventoryGrpcService` | `InventoryServiceDescriptorExposesVerticalSliceRpcs` |
| Mutation idempotency | `IIdempotencyStore` + use cases | `InventoryLifecycleListGetRegisterValidateAndIdempotency` |
| Optimistic concurrency | `UpdateDeviceUseCase` / `row_version` | `UpdateDeviceHonorsOptimisticConcurrencyAndIdempotency` |
| No credentials in responses | `DeviceConnectionSummary` | `DeviceConnectionSummaryHasNoPasswordFields` + host test |
| ValidateDeviceConnection = probe | `DiscoverDeviceUseCase` | host Validate + `DiscoverDeviceIsReadOnly…` |
| Concurrent probe coalesce | `ValidateDeviceConnectionCoordinator` | concurrent Validate in host test |
| Auth before use case | `IAuthorizationBoundary` | `InventoryMutationsAreForbiddenWithoutPermission` |
| Pagination | `ListSitesUseCase` | `ListSitesPaginatesAndRequiresReadPermission` |
| ListNodes pagination (M1-27) | `ListNodesUseCase` | `ListNodesPaginatesBySiteAndRequiresReadPermission` + host ListNodes |
| Contract round-trip | `Uuid` / `Site` | `InventoryProtoContractTests` |

Filter: `dotnet test --filter FullyQualifiedName~Inventory`.

## Living Specification — desktop inventory tree (M1-27)

Initial Issue Set M1-27 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Tree uses server data | `InventoryTreeService` + `IInventoryTreeClient` | `RefreshBuildsSiteNodeDeviceHierarchy` |
| UI/VM without Domain objects | Desktop → Contracts only | `InventoryTreeViewModelAssemblyHasNoDomainOrRouterOsReferences` + architecture |
| Display fields (reachability/version/model/support/kind/uplink/VRRP/last snapshot) | Device proto + `InventoryNodeViewModel` | hierarchy test + host GetNode observation defaults |
| Refresh cancellation | `InventoryTreeService.RefreshAsync` | `CancellationStopsRefresh` |
| No parallel overlapping refresh | single-flight coalesce | `ParallelRefreshDoesNotStartTwoOverlappingLoads` |
| Large inventory paged | `ListSites`/`ListNodes` page loops | `ListNodesPaginates…` + `GrpcInventoryTreeClient` |
| Error keeps last successful tree | `InventoryTreeService` | `FailedRefreshPreservesPreviousTreeAndSetsCached` |
| Cached state marked | `IsCached` / UI badge | failed-refresh test + MainWindow badge |
| No RouterOS/SQL in ViewModel | presentation DTOs only | assembly + architecture Desktop bans |
| GUI/state tests | unit (no Avalonia headless) | `InventoryTreeServiceTests` |

Filter: `dotnet test --filter FullyQualifiedName~InventoryTree`.

## Living Specification — desktop snapshot viewer (M1-28)

Initial Issue Set M1-28 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Sections + capture status | `SnapshotSummary.sections` / `ListSectionDescriptorsAsync` | host GetSnapshotSummary sections + `SnapshotSummarySectionsRoundTrip…` |
| Config vs observations | `SnapshotViewerService.LoadSectionAsync` | `LoadDeviceLoadsSummarySectionsAndSeparatesDomains` |
| Three hashes + schema version | summary header mapping | same unit test |
| Unknown props technical-only | `ShowTechnicalView` / `IsTechnicalOnly` | `ExportIncludesTechnicalSectionWhenRequested` |
| No credentials in copy/export | field filter + export | domain test strips `password` |
| Virtualization / off-UI load | ListBox `VirtualizingStackPanel` + `Task.Run` | UI wiring + service loads on background |
| Read-only viewer | no mutation RPCs from Desktop client | `ISnapshotViewerClient` surface |

Filter: `dotnet test --filter FullyQualifiedName~SnapshotViewer`.

## Living Specification — desktop semantic diff viewer (M1-29)

Initial Issue Set M1-29 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Base/target selection | `SnapshotDiffViewModel` / `LoadCapturesAsync` | `CompareMapsServerEntriesWithoutLocalRecompute` |
| Group by sections | `SnapshotDiffService` section groups | same |
| ADDED/REMOVED/MODIFIED/MOVED/STATE_CHANGED | wire `DiffChange` → `ChangesText` | same |
| Config vs observation | domain filter checkboxes + DomainText | same |
| Explicit rule order | `order: before → after` | ordinal assertion |
| Address-list entry level | server DiffEntry record_key | address-lists entry present |
| No differences state | `IsNoDifferences` | `IdenticalEmptyDiffIsNoDifferences` |
| Unknown props not masked | compatibility.unknown-properties kept | mapped in compare test |
| Virtualized rows | ListBox `VirtualizingStackPanel` | MainWindow Diff tab |
| No local semantic recompute | Desktop calls CompareSnapshots only | `CompareCalls == 1` |

Filter: `dotnet test --filter FullyQualifiedName~SnapshotDiff`.

## Living Specification — standalone CHR vertical slice (M1-30)

Initial Issue Set M1-30 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| API-SSL trust path | connection profile → `RouterOsReadTarget` | `StandaloneVerticalSliceHashesDiffPersistAndApiSslTrust` |
| Live CHR certificate | TLS probe (gated) | `LiveChrApiSslCertificateIsPresentAndTrustedByLabPolicy` |
| Supported sections | `SnapshotSummary.sections` | expected section ids + Ok status |
| Identical captures → same hashes | snapshot-hash dedupe | identical StartCapture |
| Filter change → config hash + MODIFIED | CompareSnapshots | action accept→drop field diff |
| Running change → observation only | hash compare + obs DiffEntry | InterfaceRunning toggle |
| Persist after Controller restart | second host, same PG | GetSnapshotSummary after Stop/Start |
| Desktop inventory/snapshot/diff | Shell wiring | `DesktopVerticalSliceWiringTests` |
| No product write path | allowlist + lab script | `RegistryRejectsWrite…` + `provision-standalone.sh` |
| Provisioning outside adapter | `testlab/chr/scripts/` | `StandaloneProvisioningScriptExistsOutsideProductAdapter` |

Filter: `dotnet test --filter FullyQualifiedName~StandaloneVerticalSlice`.

## Living Specification — multi-WAN CHR vertical slice (M1-31)

Initial Issue Set M1-31 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Topology failover/balanced | topology.json + capture mode | `MultiWanTopologiesDeclareDistinctUplinkRoles…` + host test |
| Routing tables/rules/NAT/mangle | section ids on summary | `AssertExpectedSections` |
| Primary/backup not mixed | `uplink-role` on tables | `AssertUplinksNotMixedAsync` |
| Active-state ≠ config hash | default-state observation | active toggle assertions |
| Static route → config hash | static-routes change | gateway change assertions |
| Strict rp-filter finding | ipv4.settings + topology.validation | `AssertStrictRpFilterFindingAsync` |
| Config vs operational diffs | CompareSnapshots domains | routeDiff entries |
| No WAN/routing writes | SnapshotService surface | `SnapshotServiceHasNoRoutingOrWanMutationRpcs` |
| Provision outside adapter | `provision-multi-wan.sh` | skeleton script test |

Filter: `dotnet test --filter FullyQualifiedName~MultiWan`.

## Living Specification — VRRP CHR vertical slice (M1-32)

Initial Issue Set M1-32 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Connect per physical member | RegisterDevice hosts + capture target | host assertions on `LastTarget` |
| VRRP groups + per-VRID roles | `ha.vrrp` config/obs sections | `AssertVrrpGroupsAndRolesAsync` |
| Active/passive classification | topology findings + `NodeTopologyValidator` | valid one-master pair |
| Split-master ≠ global master | `VRRP_SPLIT_MASTER` + `global-master=false` | split-master capture + validator |
| Role switch → obs hash only | StartCapture + CompareSnapshots | config stable / obs changed |
| Version mismatch blocker | `NodeTopologyValidator` | `VRRP_VERSION_MISMATCH` |
| Unreachable member not masked | missing facts | `FACTS_DEVICE_UNKNOWN` |
| Per-member snapshots + node view | GetNode / ListNodes | two devices, distinct capture ids |
| No VRRP writes | SnapshotService surface | `SnapshotServiceHasNoVrrpMutationRpcs` |
| Provision outside adapter | `provision-vrrp.sh` | `VrrpTopologiesDeclareDistinctMembers…` |

Filter: `dotnet test --filter FullyQualifiedName~VrrpVerticalSlice`.

## Living Specification — protocol/snapshot fault injection (M1-33)

Initial Issue Set M1-33 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Fragmented prefix/word | `FaultInjectionTransport` + parser | `FragmentedLengthPrefixAndWordResolve…` |
| Interleaved tagged replies | `RosSession` harness | `InterleavedTaggedRepliesCompleteWithZeroPending` |
| !trap / !fatal | executor + session | `TrapYieldsRosTrapCode…` / `FatalYieldsApiFatal…` |
| timeout / /cancel | session | `CommandTimeout…` / `CancelCommandIsDeterministic…` |
| TLS/socket close mid-command | peer pipe complete | `PeerCloseMidCommandYieldsDefinedLifecycle…` |
| oversized word/sentence | codec/parser | `OversizedWordAndSentenceYieldTypedCodes` |
| unstable config | `StableReadCoordinator` | `UnstableConfigurationYieldsSnapshotUnstable…` |
| controller cancel + no complete | capture port + gRPC | `FaultsDoNotPersistCompleteCaptures…` |
| DB fail → no orphans | `ISnapshotStore` conflict | same acceptance test |
| restart + recovery | host Stop/Start | `CompletedCaptureSurvivesControllerRestart` |
| bounded memory on faults | codec loop | `RepeatedOversizedWordFaultsDoNotGrow…` |
| no production network | unit + Testcontainers only | suite location |

Filter: `dotnet test --filter FullyQualifiedName~FaultInjection`.

## Living Specification — M1 vertical-slice acceptance package (M1-34)

Initial Issue Set M1-34 AC → module → tests/docs:

| AC / вимога | Модуль | Тест / документ |
|-------------|--------|-----------------|
| Standalone/Multi-WAN/VRRP/Fault matrices | acceptance suites | filters in [`m1-vertical-slice-acceptance.md`](m1-vertical-slice-acceptance.md) |
| Identical config hash / no runtime config drift | M1-30…32 suites | Living Spec rows above |
| Desktop server data | Shell ViewModels | `DesktopVerticalSliceWiringTests` |
| No write path / no Desktop RouterOS | Architecture + csproj | `RouterOsMustNotExposeForbiddenWriteNamespaces` + `DesktopAssembliesDoNotReferenceRouterOs` |
| Fixtures / vuln / architecture / restore | docs + gates | fixtures README; `dotnet list … --vulnerable`; ArchitectureBoundary; recovery + migrate |
| CHANGELOG + known limitations + RC | acceptance package | `M1VerticalSliceAcceptanceDocumentationTests` |

Filter: `dotnet test --filter FullyQualifiedName~M1VerticalSliceAcceptance`.

## Living Specification — policy document lifecycle (M2-01)

Policy Model §7–§9 / §33 / §66 + Issue Set M2-01 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Kinds COMPANY/SITE/NODE/EXCEPTION | `Policy` / `PolicyEnums` | `CreateSupportsNormativeKinds` |
| Normative revision states + transitions | `PolicyRevision` | `LifecycleDraftToApprovedAndTerminalTransitions` |
| Edit only DRAFT; VALIDATED→DRAFT on edit | `PolicyRevision.ReplaceDocument` | `ReplaceDocumentOnValidatedReturnsToDraftAndChangesHash` |
| APPROVED payload immutable | domain + DbContext | unit reject + `ApprovedPayloadIsImmutableDeleteForbiddenLifecycleStateAllowed` |
| Hash over exact canonical bytes | `PolicyHashing` / `PolicyCanonicalWriter` | `PolicyCanonicalHashTests` |
| Draft edit changes hash / clears validation | domain + store | unit + `DraftEditPersistsNewHashAndInvalidatesValidationState` |
| Approved not update/delete via app role | `MfcDbContext` | `ApprovedPayloadIsImmutableDeleteForbiddenLifecycleStateAllowed` |
| Clone approved → new DRAFT | `CloneToDraft` + store | unit + `CloneApprovedPersistsNewDraftRevision` |
| Payload compressed; hash before compression | `BrotliPayloadCodec` + store | `ContentHashIsSha256OfExactCanonicalBytesIndependentOfBrotli` + persist round-trip |
| Lifecycle unit + PostgreSQL integration | unit + Testcontainers | `FullyQualifiedName~Policy` |

Filter: `dotnet test --filter FullyQualifiedName~Policy`.

## Living Specification — policy pipeline and chain contracts (M2-02)

Policy Model §12–§15 + Issue Set M2-02 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| All Pipeline v1 stages | `PolicyPipelineV1.OrderedStages` | `OrderedStagesMatchNormativePipelineV1` |
| Stage order not user-editable | hardcoded domain catalog | same + no DB stage table |
| Scope/effect permissions | `IsOwnerEffectAllowed` | `AllowedOwnerEffectCombinationsPass` / `ForbiddenOwnerEffectCombinationsThrow` |
| Chain contracts DROP/REJECT/RETURN_TO_UNMANAGED | `ChainContract` | `SupportsNormativeDispositions` / `ReturnToUnmanagedRequiresMigrationCoexistenceAndIsCritical` |
| Default ACCEPT impossible | `ChainDefaultDisposition` | `AcceptDefaultDispositionIsImpossible` |
| Contract only company baseline | `PolicyDocument` / `ChainContractSet` | `CompanyBaselineMayDefineContractsOverlaysCannot` |
| Site/Node cannot change contract | `WithChainContracts` | same |
| Deterministic IPv4/IPv6 × INPUT/FORWARD/OUTPUT | `OrderedSurfaces` | `StageOrderIsIdenticalForEveryFamilyAndChainSurface` |
| Forbidden owner/effect unit coverage | permission matrix | `ForbiddenOwnerEffectCombinationsThrow` |

Filter: `dotnet test --filter FullyQualifiedName~PolicyPipeline|FullyQualifiedName~ChainContract`.

## Living Specification — address objects and selectors (M2-03)

Policy Model §11 / §16–§17 + Issue Set M2-03 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| HOST / PREFIX / IPv4 RANGE | `AddressEntry` / `AddressObject` | `SupportsHostPrefixAndIpv4RangeMasksPrefixHostBits` |
| FQDN/dynamic/timeout forbidden | typed entry API | `Ipv6RangeAndFqdnStyleEntriesAreImpossible` |
| Family checked | `AddressObject.Create` | `FamilyMismatchIsRejected` |
| Prefix host bits masked | `AddressInterval.FromPrefix` | host-bits assert in object test |
| Disjoint normalize + merge | `AddressSetAlgebra.Normalize` | `NormalizationMergesOverlapsDuplicatesAndAdjacentDeterministically` |
| AddressSelector include/exclude | `AddressSelector` / resolver | `AddressSelectorTests` |
| Empty include = Universe | resolver | `EmptyIncludeMeansUniverseMinusExclusions` |
| Empty result = RULE_UNSATISFIABLE | `AddressSelectorResolveResult` | `UniverseMinusEverythingIsUnsatisfiableBlocker` |
| Inline IP forbidden | `ManagedRuleAddressConstraint` | `InlineIpInManagedRuleIsForbidden` |
| UUID visibility | `AddressObjectVisibility` + evaluator | `VisibilityIsUuidScopedUpwardReferencesForbidden` |
| Property-based normalize/subset/∩ | `AddressSetAlgebraPropertyTests` | 200 seeded trials each |

Filter: `dotnet test --filter FullyQualifiedName~Address`.

## Living Specification — service objects and selectors (M2-04)

Policy Model §18–§19 + Issue Set M2-04 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Numeric protocol semantics | `IpProtocol` | `ProtocolSemanticsUseNumericValueTcpUdpPortsSupported` |
| TCP/UDP source/dest ports | `ServiceTerm` / `PortSet` | same |
| ICMP / ICMPv6 separated | `ServiceObject.CanonicalizeTerms` | `IcmpAndIcmpV6AreSeparatedWrongFamilySelectorRejected` |
| Ports only on port-capable protocols | `ServiceTerm.Create` | `PortMatcherWithoutPortCapableProtocolIsForbidden` |
| Wrong ICMP family rejected | resolver | `Ipv4RuleRejectsIcmpV6ServiceObject` |
| Port intervals normalize/merge | `PortSet.Normalize` | merge test + property trials |
| Duplicate terms canonicalized | `CanonicalizeTerms` | order-independent object test |
| protocol=any + ports forbidden | `ServiceTerm` | `ProtocolAnyWithPortsIsForbiddenEmptyObjectForbidden` |
| No service negation | `ServiceSelector` | `ServiceSelectorHasNoNegationEmptyIncludeIsAnyProtocol` |
| Empty service object forbidden | `ServiceObject.Create` | same |
| UUID visibility | `ServiceObjectVisibility` + evaluator | `VisibilityIsUuidScopedUpwardForbidden` |
| Canonical order ≠ input order | canonicalize | property + object tests |

Filter: `dotnet test --filter FullyQualifiedName~Service`.

## Living Specification — logical zones and Node bindings (M2-05)

Policy Model §§20–21 + Issue Set M2-05 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Three Policy binding kinds | `NodeZoneBindingKind` / `NodeZoneBinding` | `ZoneBindingTests` |
| `expected_dependency_hash` stored | Domain + `EfNodeZoneBindingStore` | unit + `ZoneBindingsPersistTests` |
| Resolve per Device | `ZoneResolveEngine` + `ResolveZonesForDeviceUseCase` | Domain + `ZoneUseCaseTests` |
| VRRP different physical names | Domain resolve fixtures | `InterfaceListIncludeExcludeHonoredAndVrrpNamesMayDiffer` |
| Dynamic iface → blocker | `ZoneResolveEngine` | Domain unit |
| Empty resolved → blocker | `ZoneResolveEngine` | Domain unit |
| Missing iface → blocker | `ZoneResolveEngine` | Domain unit |
| Interface-list include/exclude | Domain `InterfaceListMembership` (+ RouterOs delegate) | Domain + optional RouterOs regression |
| Membership change → AnalysisStale | `NodeZoneBinding.RecordResolve` + resolve use case | Domain + `ZoneUseCaseTests` |
| Optimistic concurrency | Update zone/binding RowVersion → Conflict | `ZoneUseCaseTests` + persist |
| Catalog SoT tables | `zone_definitions` / `node_zone_bindings` | `ZoneBindingsPersistTests` |
| `PolicyDocument.ZoneDefinitions` stays `[]` | `PolicyDocument` | composition/hash embedding deferred (M2-06+) |
| gRPC `mfc.v1.ZoneService` | `zones.proto` / `ZoneGrpcService` | `ZoneProtoContractTests` |
| Desktop CRUD + blockers | `ZonePanelService` / Zones tab | `ZonesDesktopServiceTests` |
| AC#10–11 ZoneSelector / rules | `ZoneSelector` + `PolicyRule` (M2-06 Domain) | `PolicyRuleTests` D7 |
| No Domain/App→RouterOs; Desktop Contracts-only | ArchitectureBoundary | `ArchitectureBoundaryTests` |

Observation input: latest completed capture sections via `SnapshotZoneResolveObservationSource` (`network.interfaces` + `network.interface-lists`). Missing capture → `ZONE_OBSERVATION_UNAVAILABLE`.

Filter: `dotnet test --filter "FullyQualifiedName~Zone\|FullyQualifiedName~ArchitectureBoundary"`.

## Living Specification — zone VETH/VLAN/bridge resolve (N1-05)

Issue #67 / PRD rev 2 → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| A Plain vlan/bridge/veth via IF table | `ZoneResolveEngine` | `PlainVlanBridgeVethNamesResolveViaInterfaceTable` |
| B `container:X` expands to VETH set | `ZoneResolveEngine` + observation edges | `ContainerMarkerExpandsToVethSet` |
| C `app:Y` expands similarly | `ZoneResolveEngine` | `AppMarkerExpandsToVethSet` |
| D Missing container/app → `ZONE_MISSING_*` | `ZoneResolveEngine` | `MissingContainerAndAppProduceTypedBlockers` |
| D2 Empty marker remainder (`container:` / `app: `) → typed missing | `ZoneResolveEngine` | `EmptyMarkerRemainderProducesTypedMissingBlocker` |
| E Unresolved VETH → `ZONE_*_VETH_UNRESOLVED` (+ empty) | `ZoneResolveEngine` | `UnresolvedVethAfterExpansionProducesTypedBlocker` |
| F Shared VETH → `ZONE_SHARED_VETH` + keep members | `ZoneResolveEngine` LOCK-5 | `SharedVethProducesBlockerButKeepsResolvedMembers` |
| G Marker as InterfaceList name → `ZONE_MARKER_NOT_ALLOWED_ON_INTERFACE_LIST` | `ZoneResolveEngine` | `MarkerOnInterfaceListProducesTypedBlockerWithoutExpansion` |
| H App enrichment from `topology.container-veth` / `topology.shared-veth` | `SnapshotZoneResolveObservationSource` | `ParsesContainerVethAndSharedVethCanonicalSections` |
| H2 Interfaces without topology + marker → typed missing (not `MISSING_INTERFACE` on marker) | App + Domain | `InterfacesWithoutTopologySectionsYieldTypedMarkerBlockers` |
| H3 / LOCK-6 Hash v1 (`mfc.zone.dependency.v1`) = kind + binding values + post-expansion members | `NodeZoneBinding.ComputeDependencyHash` | `DependencyHashV1UsesMarkersAndPostExpansionMembers` |
| I Architecture: Domain/App ↛ RouterOs; Desktop Contracts-only | ArchitectureBoundary | `ArchitectureBoundaryTests` |
| J Projector emits LOCK-2 sections from discovery | `DiscoveryCanonicalProjector` + `PacketPathTopologyDiscovery` | `PacketPathTopologyEmitsContainerVethAndSharedVethSections` |
| K Living Spec + ROADMAP NEXT → M2-06 | docs | this matrix + ROADMAP §3 |

**LOCK-6 (hash):** prefix stays `mfc.zone.dependency.v1`; inputs are binding kind, raw binding values (markers allowed), and post-expansion interface member names (sorted). Marker tokens are not rewritten into the hash as VETH names — expansion affects the members list only.

**LOCK-7 (blocker codes):** `ZONE_MISSING_CONTAINER`, `ZONE_MISSING_APP`, `ZONE_CONTAINER_VETH_UNRESOLVED`, `ZONE_APP_VETH_UNRESOLVED`, `ZONE_SHARED_VETH`, `ZONE_MARKER_NOT_ALLOWED_ON_INTERFACE_LIST` (plus pre-existing `ZONE_MISSING_INTERFACE` / `ZONE_EMPTY_RESOLVED_SET` / `ZONE_OBSERVATION_UNAVAILABLE` unchanged).

Canonical section IDs: `topology.container-veth`, `topology.shared-veth` (configuration). Domain observation DTOs are plain strings only.

**Known residual (tracked, non-blocking for N1-05 library slice):** live `ISnapshotCapturePort` still defaults to `NotConfiguredSnapshotCapturePort`; `DiscoveryCanonicalProjector` (M1-22) is not yet on the production capture path. Marker expansion works whenever LOCK-2 sections are present in a persisted snapshot; assemblers that adopt the projector **must** set `DiscoveryCanonicalInput.PacketPathTopology`. Same seam as M1-22 — not a Domain/App gap.

Filter: `dotnet test --filter "FullyQualifiedName~ZoneResolve|FullyQualifiedName~SnapshotZoneResolve|FullyQualifiedName~ArchitectureBoundary|FullyQualifiedName~DiscoveryCanonical|FullyQualifiedName~CanonicalSectionIds"`.

## Living Specification — typed policy rules (M2-06)

Policy Model §§22–27 + Issue Set M2-06 AC#1–12 → Domain + Application + gRPC + thin Desktop:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Normative managed matchers | `TrafficPredicate` | `PolicyRuleTests` D1/D8 |
| Unsupported matchers no surface | Domain types (no raw matcher) | D10 |
| Effects ACCEPT/DROP/REJECT/FASTTRACK/EXEMPT | `RuleEffectSpec` / `PolicyRuleEffect` | D1 |
| REJECT typed reject mode | `RuleEffectSpec` | D1 |
| TCP_RESET TCP-only | `TrafficPredicate.IsTcpOnly` | D2 |
| Contiguous ordinals | `PolicyRuleSet` + `AddRuleUseCase` | D5 / A2 |
| Rule UUID ≠ ordinal | `RuleId` / `PolicyRule` | D5 |
| Disabled ∉ active | `PolicyRuleSet.ActiveRules` + `ListRulesUseCase` | D6 / A4 |
| exception_eligible deny-only | `PolicyRule` | D3 |
| Log prefix ≤32 ASCII | `LogSpecification` | D4 |
| No raw matcher string | `TrafficPredicate` | D8/D10 |
| Deterministic canonicalize | `PolicyCanonicalWriter` | D9/D11 |
| ZoneSelector chain constraints | `ZoneSelector.EnsureAllowedOnChain` | D7 |
| Writer↔reader hash | `PolicyDocumentReader` | D11 |
| Legacy opaque rules → `POLICY_RULES_UNSUPPORTED_SHAPE` | `PolicyDocumentReader` | D12 |
| Typed `PolicyDocument.Rules` | `PolicyDocument` | D9/D11 + lifecycle hash tests |
| Draft-only mutate | `AddRuleUseCase` / `PolicyRevision.ReplaceDocument` | A1 |
| `expected_content_hash` CAS | App mutators | A3 / `PolicyGrpcHostTests` |
| GetRevision / GetRule / Update / Delete / Reorder + idempotent replay | `PolicyRuleUseCases` | `GetRevisionAndGetRuleRoundTrip`, `UpdateDeleteReorderAndIdempotentReplay`, `DraftReplayCatalogBranchesAndBadHashLength` |
| Zone hard / Address·Service soft `POLICY_SELECTOR_CATALOG_SOFT` | `PolicyRevisionSupport` | A5 / O1 |
| Non-empty doc address/service arrays → hard UUID membership | `EnsureAddressServiceCatalog` | A5b |
| gRPC `mfc.v1.PolicyService` | `policy.proto` / `PolicyGrpcService` | `PolicyProtoContractTests` / C2 |
| Desktop thin list (Contracts-only) | `PolicyPanelService` | U1 `PolicyDesktopServiceTests` |
| Architecture boundary | Desktop → Contracts only | C3 `ArchitectureBoundaryTests` |

**Residuals (documented, non-blocking):** TCP_RESET on App/gRPC path needs an in-document service catalog that proves TCP-only (`IsTcpOnly`); CreateDraft starts with empty service_objects, so wire TCP_RESET remains Domain-proven until Address/Service object CRUD embeds catalogs. Idempotency hashes now include predicate+logging.

Filter:
```bash
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~Policy"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~Policy"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — deterministic policy composition (M2-07)

Policy Model §§29–34.1 + Issue Set M2-07 AC#1–14 → Domain composer + Application load/select + `ComposeEffectivePolicy` (Desktop OUT):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Company baseline required | `EffectivePolicyComposer` / `ComposeEffectivePolicyUseCase` | D1 / A1 / A4 |
| Site/Node overlays optional | composer + App 0\|1 overlay | D2 / A2 |
| UUID-only resolve (no names) | merged object namespace | D3 |
| Parent context hashes | `PolicyHashing.ComputeParentContextHash` | D4 / A3 |
| Scope visibility | `AddressObjectVisibility` / `PolicyObjectIdentity` | D5 |
| Stage ownership | `PolicyPipelineV1.IsOwnerEffectAllowed` | D6 |
| Disabled ∉ active; ordinals kept | composer active list | D7 |
| No auto-dedup | duplicate predicates kept | D8 |
| Pipeline v1; exemption stages empty | §31 order | D9 |
| No VRRP/WAN/device/bindings | composer signature + request | D10 / C1 |
| Deterministic logical hash | `PolicyHashing.HashLogicalEffective` | D11 / D12 |
| Unused object INFO | `UNUSED_POLICY_OBJECT` | D13 / O3 |
| UUID collision objects/rules | `POLICY_COMPOSE_UUID_COLLISION` | D14 / D15 |
| Missing zone catalog | `POLICY_COMPOSE_ZONE_NOT_FOUND` | D16 |
| Hash ≠ synthetic `PolicyDocument` write | LOCK-10 IncrementalHash | D17 |
| Absent overlay ≠ 32 zero bytes | omit-if-absent | D18 |
| Prefix + NUL + uint32 exception count 0 | `BuildLogicalEffectivePreimage` | D19 |
| Unique ACTIVE company; archived ignored | `ListActiveByKindAsync` | A4 / A5 / A7 |
| Missing node → `not_found` | App | A6 |
| gRPC `ComposeEffectivePolicy` | `policy.proto` / `PolicyGrpcService` | C2 / C4 |
| `POLICY_COMPOSE_*` trailer FailedPrecondition, retryable=false | `GrpcApplicationErrorMapper` | O1 |
| Architecture boundary Domain/App ↛ RouterOs | existing | C3 |

**Residuals (documented, non-blocking):** Desktop does not call compose.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~EffectivePolicy|FullyQualifiedName~ComposeEffective|FullyQualifiedName~LogicalEffective|FullyQualifiedName~PolicyHashing|FullyQualifiedName~ArchitectureBoundary"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~PolicyGrpc|FullyQualifiedName~PolicyProto"
```

## Living Specification — scoped deny-stage exceptions (M2-08)

Policy Model §28 + Issue Set M2-08 AC#1–14 → Domain proofs + Application load/clock + `UpdateExceptionMetadata` (Desktop OUT):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| One target deny; exactly one enabled EXEMPT | `ExceptionComposeProof` | D1 / D_RULE_COUNT |
| Enabled + exception_eligible DROP\|REJECT | composer proofs | D2 |
| `target_stage` = waived rule; rule stage = twin | `PolicyPipelineV1.TryExemptionTwin` | D3 |
| Family/chain match | `ExceptionComposeProof` | D4 |
| Fail-closed structural subset (all dimensions) | `ExceptionPredicateProof` | D5 |
| UUID-disjoint include may still overlap in interval space | superseded by M2-09 | D6 (interval) |
| Mandatory deny forbidden | composer | D7 |
| Effect EXEMPT only; EXEMPT not on deny stages | `EnsureAllowedEffect` | D8 / D14 |
| Company-wide EXCEPTION forbidden | `Policy.ValidateOwner` / CreateDraft | D9 / A9 |
| Finite `valid_until` + reason + ticket | `ExceptionMetadata` | D10 |
| Universe target forbidden | L5 | D11 |
| Target rule change invalidates parent | parent_context | D12 / A_META |
| Expired skipped via `IClock` | `ComposeEffectivePolicyUseCase` | A13 |
| Hash slot = count + N×32 digests; order waived then policy | `PolicyHashing` | D13 / D15 |
| Hash ≠ synthetic document | LOCK-10 | D16 |
| Site may waive COMPANY_DENY; Node cannot waive SITE_DENY | `POLICY_EXCEPTION_STAGE_OWNERSHIP` | D17 |
| `target_scope` = policy owner | LOCK-18 | D18 |
| Exception objects forbidden | LOCK-1′ | D19 |
| Site parent omits node overlay hash | LOCK-4′ | D_PARENT_SITE |
| Exemption-stage active order: revision, ordinal, UUID | LOCK-15 | D_SORT |
| Two exceptions same owner; no overlay uniqueness | LOCK-11 | A_LOAD / A1 |
| `UpdateExceptionMetadata` CAS + draft-only + parent rewrite | App | A_META / C_META |
| Host compose inserts exemption; hash ≠ no-exception | gRPC | C2 |
| Compose request still `node_id` only | proto | C1 |
| `POLICY_EXCEPTION_*` trailer FailedPrecondition, retryable=false | `GrpcApplicationErrorMapper` | O1 |
| No expire ROS writer; Desktop OUT | ArchitectureBoundary | C3 / C14 |

**Residuals (documented, non-blocking):** Domain composer does not expire (Application `IClock` filter only). Desktop does not call compose.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ExceptionMetadata|FullyQualifiedName~EffectivePolicy|FullyQualifiedName~ComposeEffective|FullyQualifiedName~PolicyHashing|FullyQualifiedName~UpdateException|FullyQualifiedName~ArchitectureBoundary"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~PolicyGrpc|FullyQualifiedName~PolicyProto"
```

## Living Specification — bounded packet predicate algebra (M2-09)

Policy Model §37 + Issue Set M2-09 AC#1–11 → Domain algebra library + exception-proof rewire (Desktop OUT, no new RPC):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Exact representations (intervals, UUID zone sets, protocol bits, ports, ICMP, conn/NAT/addr-type, flags, IPsec) | `AtomicTrafficCube` / `ProtocolBitSet` / `SymbolicSet` | P1 |
| Relations empty/equal/disjoint/subset/superset/partial overlap; no INDETERMINATE | `PredicateAlgebra.Relate` | P2 |
| TCP present∩absent after intersect → empty | `AtomicTrafficCube.Intersect` | P3 |
| Identical TCP flags remain overlapping (`Intersect(A,A)` with SYN) | `IntersectFlags` set-union | P3i |
| Protocol-specific port spaces | TCP:80 ⟂ UDP:80 | P4 |
| IPv4 and IPv6 never mix | cube family | P5 |
| 128 cubes/rule; 4096 residual fragments | `NormalizedPredicate.Create` | P6 |
| Overflow → `PREDICATE_COMPLEXITY_LIMIT` | compose + mapper | CX / O1p |
| No unbounded fallback | complexity fail | P6 / CX |
| Algebraic identities (xUnit trials, no FsCheck) | `PredicateAlgebraPropertyTests` | P9 |
| Port interval algebra | `PortSetAlgebra` | P7 |
| JSON HOST/PREFIX/RANGE + service terms | `PolicyObjectJsonReader` | P8 |
| Malformed service ports/object (missing bounds, non-array) → parse failure | `PolicyObjectJsonReader` | P8u |
| Exception subset is interval-true (host∈prefix, different UUID) | `ExceptionPredicateProof` | D5i |
| Same prefix, different UUID → `POLICY_EXCEPTION_OVERLAP` | overlap rewire | D6i |
| Identical TCP flags on overlapping denies → `POLICY_EXCEPTION_OVERLAP` | `IntersectFlags` | D6f |
| Disjoint hosts, different UUID still compose | D6 | D6d |
| Flags/IPsec equality (omit-vs-constrained) | D5 flags/ipsec | D5 |
| Unparseable exception-path object → `POLICY_COMPOSE_SELECTOR_UNRESOLVED` | catalog builder | UR |
| Unparseable exception-path service ports → `POLICY_COMPOSE_SELECTOR_UNRESOLVED` | catalog builder | URs |
| Compose invokes algebra only for exceptions | L4 | M2-07 stub objects still compose |
| `PREDICATE_*` trailer FailedPrecondition, retryable=false | `GrpcApplicationErrorMapper` | O1p |
| Hash preimage format unchanged; Desktop OUT; no new RPC | existing | D15 / C1 / C3 |

**Residuals (documented, non-blocking):** Flag/IPsec exception subset stays equality (not flag-implication). Cube subtract omits ICMP/flags/IPsec residuals that cannot be represented. `PredicateAlgebra.IsSubset` is fail-closed single-cube cover (union of cubes is not a cover); `Relate` is not used as packet-space truth in M2-10. Compose uses interval algebra on the exception path (L4) and interval/zone/service emptiness on all rules (M2-10); UUID/catalog validation remains. `PolicyObjectJsonReader` is a second parser beside `PolicyDocumentReader` (hash preimage unchanged). Desktop does not call compose.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~PredicateAlgebra|FullyQualifiedName~PortSetAlgebra|FullyQualifiedName~PolicyObjectJson|FullyQualifiedName~ExceptionCompose|FullyQualifiedName~ComposeEffective|FullyQualifiedName~PolicyAnalysis|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~ArchitectureBoundary"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~PolicyGrpc|FullyQualifiedName~PolicyProto"
```

## Living Specification — structural and satisfiability analysis (M2-10)

Policy Model §22 / §25 / §38–§39 + Issue Set M2-10 AC#1–12 → Domain `PolicyAnalysisEngine` + compose-on-read gate (Desktop OUT, no new RPC):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Schema/family/chain/stage/object constraints | `PolicyRule` + compose selectors + analysis | `Ac1ValidRuleHasNoBlockersAndInvokesSequence` |
| Wrong zone direction | `PolicyAnalysisEngine.TryZoneDirection` | `Ac2WrongZoneDirectionIsBlocker` |
| Empty selector | include−exclude / universe minus all | `Ac3EmptySelectorIsUnsatisfiableBlocker` |
| TCP flags with non-TCP service | `RULE_TCP_FLAGS_PROTOCOL` | `Ac4TcpFlagsWithUdpServiceAreBlocked` |
| TCP flags + any-protocol remain satisfiable | flags without service selector | `Ac4TcpFlagsWithAnyProtocolRemainSatisfiable` |
| ICMP family mismatch | `RULE_ICMP_FAMILY` | `Ac5IcmpFamilyMismatchIsBlocked` |
| IPsec direction vs INPUT/OUTPUT | `RULE_IPSEC_DIRECTION` | `Ac6IpsecDirectionIsChecked` |
| Connection-state contradictions | INVALID/UNTRACKED vs tracked | `Ac7ConnectionStateContradictionIsBlocked` |
| Unsupported matcher | `RULE_UNSUPPORTED_MATCHER` | `Ac8UnsupportedMatcherBlocksRule` + reader D12 |
| Disabled rule still validated | analysis + compose | `Ac9DisabledRuleStillGetsStructuralValidation` / `DisabledUnsatisfiableRuleFailsCompose` |
| Structured findings + stable codes | `PolicyAnalysisFinding` | `Ac10Ac11FindingsAreStructuredWithStableCodes` |
| Invalid rule not passed to sequence | `SequenceAnalyzerInvoked=false` | `Ac12InvalidRuleIsNotPassedToSequenceAnalyzer` |
| IPv6 + broadcast type empty | `RULE_UNSATISFIABLE` | `Ipv6BroadcastAddressTypeIsUnsatisfiable` |
| Empty zone include−exclude | `RULE_UNSATISFIABLE` | `EmptyZoneIncludeMinusExcludeIsUnsatisfiable` |
| TCP_RESET without TCP (reconstitute) | `RULE_TCP_FLAGS_PROTOCOL` | `TcpResetWithoutTcpServiceIsBlockedOnReconstitute` |
| Disabled dangling UUID | compose `POLICY_COMPOSE_SELECTOR_UNRESOLVED` | `DisabledDanglingSelectorFailsCompose` |
| Compose gate for flags/IPsec | `EffectivePolicyComposer` | `TcpFlagsWithUdpServiceFailsCompose` / `IpsecInputOutFailsCompose` |
| `RULE_*` trailer FailedPrecondition, retryable=false | `GrpcApplicationErrorMapper` | `RuleUnsatisfiableIsFailedPreconditionNotRetryable` |

**Residuals:** Sequence/shadow/overlap is M2-11. Algebra `Relate`/`Subtract` is not the satisfiability oracle; emptiness uses interval resolve + cube drop. Unsupported matchers on typed `TrafficPredicate` cannot appear after `PolicyDocumentReader` (`POLICY_RULES_UNSUPPORTED_SHAPE`); analysis accepts explicit extra-matcher keys for the AC gate.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~PolicyAnalysis|FullyQualifiedName~PolicySequence|FullyQualifiedName~ComposeEffective|FullyQualifiedName~ExceptionCompose|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — duplicate, shadow and overlap analysis (M2-11)

Policy Model §40–§43 + Issue Set M2-11 AC#1–12 → Domain `PolicySequenceAnalysis` after M2-10 blockers, on pipeline-ordered enabled rules (Desktop OUT, no new RPC):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Exact duplicates | `RULE_EXACT_DUPLICATE` WARNING | `Ac1ExactDuplicatesAreWarningsAndKeepBothRules` |
| Same predicate, different effect | `RULE_CONFLICTING_DUPLICATE` BLOCKER | `Ac2SamePredicateDifferentEffectIsBlocker` |
| Fully shadowed enabled rule | `RULE_FULLY_SHADOWED` BLOCKER | `Ac3FullyShadowedEnabledRuleIsBlocker` |
| Partial shadowing | `RULE_PARTIALLY_SHADOWED` WARNING | `Ac4PartialShadowingIsWarning` |
| Allow-before-deny overlap | `EARLIER_ALLOW_BYPASSES_DENY` | `Ac5AllowBeforeDenyOverlapIsDetected` |
| Deny-before-allow overlap | `ORDER_DEPENDENT_OVERLAP` | `Ac6DenyBeforeAllowOverlapIsDetected` |
| FASTTRACK overlap distinct | `FASTTRACK_OVERLAP`; vs deny = BLOCKER | `Ac7FasttrackOverlapIsDistinct` |
| Bounded residual fragments | subtract limit / split-cover | `Ac8Ac9SplitCoverEmptyResidualIsIndeterminateBlocker` |
| Indeterminate safety is blocker | `SHADOW_ANALYSIS_INDETERMINATE` | `Ac8Ac9SplitCoverEmptyResidualIsIndeterminateBlocker` |
| Witness packet on proven findings | `PolicyWitnessPacket` | `Ac10ProvenFindingsHaveWitnessPackets` |
| Duplicate not auto-removed | compose keeps both active rules | `Ac11DuplicateIsNotRemovedFromCompose` |
| Deterministic vs thread scheduling | stable sort by rule/code/related | `Ac12FindingsAreIndependentOfRepeatedInvocation` |
| Family/chain isolation | no cross-surface shadow | `DifferentFamiliesDoNotShadowEachOther` |
| Disabled ignored at sequence | M2-10 still validates | `DisabledRulesAreIgnoredBySequenceAnalysis` |
| Exempt skip (M2-08 proofs) | no duplicate/shadow/overlap vs `EXEMPT_DENY_STAGE` | `ExemptDenyStageIsSkippedForDuplicateShadowAndOverlap` |
| Same effect, different logging | not `RULE_CONFLICTING_DUPLICATE` | `SameEffectDifferentLoggingIsFullyShadowedNotConflictingDuplicate` |
| Sequence BLOCKER gRPC trailer | FailedPrecondition, retryable=false | `SequenceComposeBlockersAreFailedPreconditionNotRetryable` |

**Residuals:** Equal uses fail-closed single-cube `IsSubset` both ways, not `Relate` as packet-space EQUAL. Empty residual without fail-closed cover is INDETERMINATE, not FULLY_SHADOWED. Exception `EXEMPT_DENY_STAGE` is non-terminal and skipped for overlap. Compose fails closed on sequence BLOCKERs; WARNINGs attach to `ComposedEffectivePolicy.Findings`. FASTTRACK vs ACCEPT stays WARNING (`FASTTRACK_OVERLAP`); vs DROP/REJECT is BLOCKER. Witness stays on Domain findings (Desktop OUT; `PolicyComposeFinding` has no witness slot). `PolicySequenceAnalyzer` on `PolicyAnalysisEngine` is a test seam, not the compose path.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~PolicySequence|FullyQualifiedName~ComposeEffective|FullyQualifiedName~ExceptionCompose|FullyQualifiedName~PolicyAnalysis|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — actual RouterOS filter-context (M2-12)

Policy Model §44–§45 + Issue Set M2-12 AC#1–12 → Domain `ActualFilterAnalysis` + Application canonical mapper + RouterOs discovery mapper (Desktop OUT, no new RPC; compose unchanged):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Bounded filter CFG | `ActualFilterAnalysis` graph | `Ac1BoundedFilterControlFlowGraphIsBuilt` |
| Jump + return | CFG edges | `Ac2JumpAndReturnAreSupported` |
| Jump cycles | `ACTUAL_FILTER_JUMP_CYCLE` | `Ac3JumpCyclesAreDetected` / `SelfJumpIsCycle` |
| Depth / node / chain limits | 16 / 50 000 / 1024 | `Ac4DepthAndNodeLimitsAreApplied` |
| Pre-anchor ACCEPT bypass | `PRE_ANCHOR_ACCEPT_BYPASSES_POLICY` | `Ac5PreAnchorAcceptBypassIsDetected` |
| Pre-anchor DROP/REJECT shadow | `PRE_ANCHOR_DROP_SHADOWS_POLICY` | `Ac6PreAnchorDropShadowIsDetected` / `PreAnchorRejectShadowsPolicy` |
| Pre-anchor FastTrack bypass | `PRE_ANCHOR_FASTTRACK_BYPASSES_POLICY` | `Ac7PreAnchorFastTrackBypassIsDetected` |
| Dynamic pre-anchor | `PRE_ANCHOR_DYNAMIC_RULE_PRESENT` | `Ac8DynamicPreAnchorRuleIsMarked` |
| Unsupported matcher/action | `ACTUAL_FILTER_UNKNOWN_*` + pre-anchor indeterminate | `Ac9UnsupportedMatcherOrActionIsIndeterminate` |
| Post-anchor only if `RETURN_TO_UNMANAGED` | `PostAnchorAnalyzed` | `Ac10PostAnchorContextIsAnalyzedOnlyForReturnToUnmanaged` |
| Implicit accept ≠ managed default | `UsesRouterOsImplicitAcceptAsManagedDefault=false` | `Ac11RouterOsImplicitAcceptIsNotManagedDefault` |
| Actual context hash in analysis context | `mfc.policy.actual_filter_context.v1` | `Ac12ActualContextHashEntersAnalysisContext` |
| Disabled / controller-owned skip bypass | marker + disabled | `DisabledPreAnchorAcceptIsIgnored` / `ControllerOwnedPreAnchorIsNotUnmanagedBypass` |
| Miss-path after guard/terminals | §44 layout | `GuardDoesNotHideLaterUnmanagedPreAnchorBypass` |
| Jump → empty builtin implicit accept | pre-anchor bypass | `JumpToEmptyBuiltinIsPreAnchorAcceptBypass` |
| Return edge to jump successor | CFG | `ReturnEdgeTargetsSuccessorAfterJump` |
| Unmanaged jump into `fwc.*` / `mfc4.*` / `mfc6.*` / legacy `mfc.*` | INDETERMINATE via `IsManagedChainName` | `UnmanagedJumpIntoManagedIsIndeterminate`; `MarkerAndRuleInvariantsHold` |
| Canonical mapper | `ActualFilterContextMapper` | `CanonicalFilterRecordsMapToDomainRulesAndDetectPreAnchorAccept` |
| Discovery mapper (dynamic + unknown) | `ActualFilterRuleMapper` | `DiscoveryMapsDynamicJumpAndUnknownMatchers` |
| `ACTUAL_FILTER_*` / `PRE_ANCHOR_*` trailer | FailedPrecondition, retryable=false | `SequenceAndActualFilterBlockersAreFailedPreconditionNotRetryable` |

**Residuals:** Desktop OUT; no new RPC; compose-on-read stays logical (actual CFG is analysis level 6, not wired into `ComposeEffectivePolicy`). Witness packets N/A for actual CFG. Management-path safety is M2-13 (DONE). Canonical filter sections still omit dynamics; RouterOs discovery mapper is the dynamic path. Jump into managed `fwc.*` / `mfc4.*` / `mfc6.*` / legacy `mfc.*` from controller-owned comments is an opaque `ManagedPipeline` node (candidate policy remains M2-11). Walk continues miss-path after terminals so later unmanaged pre-anchor rules stay visible; the anchor itself still stops post-anchor unless `RETURN_TO_UNMANAGED`. M2-13 must not treat `Graph.Edges` as the only reachability oracle — findings come from the walk.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ActualFilter|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — packet-path FORWARD blockers (N1-04)

next-1 hardware offload + ROADMAP N1-04 → Domain `PacketPathAnalysis` (Desktop OUT, no new RPC; compose unchanged; live N1-05 projector residual untouched):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| HW offload → `PACKET_PATH_BYPASSES_IP_FIREWALL` BLOCKER | `PacketPathAnalysis` | `HardwareOffloadedPathEmitsBypassesBlocker` |
| INDETERMINATE → `PACKET_PATH_NOT_PROVEN` BLOCKER | `PacketPathAnalysis` | `IndeterminatePathEmitsNotProvenBlocker` |
| CPU path does not block FORWARD | no findings | `CpuFirewallPathDoesNotBlockManagedForward` |
| MIXED does not emit those two codes | next-1 mapping | `MixedPathDoesNotEmitNext1ForwardBlockers` |
| Packet-path hash enters analysis context | `mfc.policy.packet_path_context.v1` | `PacketPathHashEntersAnalysisContext` |
| Deterministic vs input order | sort by pair/code | `FindingsAreIndependentOfInputOrder` |
| Canonical mapper (no re-classify) | `PacketPathContextMapper` | `CanonicalPairRecordsMapToDomainBlockersWithoutReclassification` |
| Discovery classification mapper | `PacketPathBlockerMapper` | `ClassificationMapsToDomainBlockersWithoutDisablingOffload` |
| `PACKET_PATH_*` trailer | FailedPrecondition, retryable=false | `SequenceAndActualFilterBlockersAreFailedPreconditionNotRetryable` |

**Residuals:** Desktop OUT; no new RPC; logical compose unchanged (device packet-path is not a company document). N1-03 still attaches discovery hints; Domain is the analysis BLOCKER authority. MIXED is not `PACKET_PATH_NOT_PROVEN` (next-1 names only HW + INDETERMINATE). Controller never disables L2/L3 hardware offload. Live capture still omits N1-05 projector membership (M1-22 seam). Deploy gating of these blockers is N1-06.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~PacketPath|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — management-path safety (M2-13)

Policy Model §46–§46.1 + Onboarding §15–§16 + Issue Set M2-13 AC#1–12 → Domain `ManagementPathAnalysis` + Application canonical mapper + RouterOs discovery mapper (Desktop OUT, no new RPC; compose unchanged; guards never auto-created):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| API-SSL service enabled + port matches | `MANAGEMENT_SERVICE_DISABLED` | `Ac1ApiSslDisabledIsServiceDisabled` / `Ac1ApiSslMissingIsServiceDisabled` / `Ac1PortMismatchIsServiceDisabled` |
| Source restrictions | `MANAGEMENT_SOURCE_NOT_ALLOWED` | `Ac2SourceRestrictionBlocksDisallowedPrefix` |
| Unparseable IP-service allowlist | `MANAGEMENT_PATH_INDETERMINATE` | `UnparseableSourceRestrictionIsIndeterminate` |
| Guard exists and precedes anchor | `MANAGEMENT_GUARD_MISSING` / `MANAGEMENT_GUARD_MOVED` | `Ac3GuardMustExistAndPrecedeAnchor` |
| Guard ownership marker valid | `MANAGEMENT_PATH_INDETERMINATE` | `Ac4InvalidGuardMarkerIsIndeterminate` |
| TCP NEW to API-SSL | `MANAGEMENT_INPUT_BLOCKED` | `Ac5TcpNewMustBeAllowedOnInput` |
| OUTPUT ESTABLISHED reply | `MANAGEMENT_OUTPUT_BLOCKED` | `Ac6OutputEstablishedReplyMustBeAllowed` |
| Each VRRP member by physical address | `Analyze` + `WithDestination` on that member's snapshot | `Ac7EachVrrpMemberIsCheckedByPhysicalAddress` |
| VIP is not the only management endpoint | `MANAGEMENT_PATH_INDETERMINATE` | `Ac8VirtualIpIsNotTheOnlyManagementEndpoint` |
| Unknown matcher on management path | `MANAGEMENT_PATH_INDETERMINATE` | `Ac9UnknownMatcherOnManagementPathIsBlocker` |
| Candidate cannot change guard | `MANAGEMENT_GUARD_MOVED` | `Ac10CandidateMustNotChangeGuard` |
| SYSTEM tests generated | INPUT NEW + OUTPUT ESTABLISHED | `Ac11ManagementSystemTestsAreGenerated` |
| Witness when a concrete packet exists | `PolicyWitnessPacket` | `Ac12SafetyFindingHasWitnessWhenPossible` |
| Unmanaged FastTrack/unknown action before guard | `MANAGEMENT_PATH_INDETERMINATE` | `UnmanagedPreGuardFastTrackIsIndeterminate` |
| Proven path has no blockers | empty findings | `ProvenPathHasNoBlockersAndDoesNotUseImplicitAccept` |
| OOB does not skip in-band | still `MANAGEMENT_SERVICE_DISABLED` | `OutOfBandFlagDoesNotSkipInBandApiSslChecks` |
| DNS dest cannot prove VIP vs physical | INDETERMINATE; no SYSTEM tests | `DnsDestinationIsIndeterminate` |
| Management hash enters analysis context | `mfc.policy.management_path_context.v1` | `ManagementPathHashEntersAnalysisContextWithoutChangingPriorPreimages` |
| Canonical mapper (no RouterOS) | `ManagementPathContextMapper` | `CanonicalIpServicesAndFilterMapToDomainBlockersWithoutRewritingGuards` |
| Discovery mapper (address + dynamics) | `ManagementPathBlockerMapper` | `DiscoveryMapsApiSslAddressAndFilterWithoutCreatingGuards` |
| `MANAGEMENT_*` trailer | FailedPrecondition, retryable=false | `SequenceAndActualFilterBlockersAreFailedPreconditionNotRetryable` |

**Residuals:** Desktop OUT; no new RPC; compose unchanged (management-path is analysis, not a company document). Production entry is `Analyze()` on **one** physical-device snapshot; caller iterates members with `WithDestination` and that member's filter/API-SSL facts. VIP-only fail-closed requires the profile's physical/virtual address lists (VRRP discovery is available to callers after M2-14; this mapper still does not auto-fill them). Over-broad `0.0.0.0/0` / `::/0` and strict `mfc:guard:v1:` onboarding verification are M5-03 (DONE). VRRP protocol-112 advertisement/sync flows are M2-14 (DONE). Deploy gating of these blockers is N1-06. Guards are never auto-created. M2-12 one-argument and N1-04 two-argument analysis-context preimages are unchanged. Controller never disables L2/L3 hardware offload.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ManagementPath|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — topology and dependency safety (M2-14)

Policy Model §47–§53 + Issue Set M2-14 AC#1–14 → Domain `TopologyDependencyAnalysis` + Application canonical mapper + RouterOs discovery mapper (Desktop OUT, no new RPC; compose unchanged; NAT/RAW/Mangle/VRRP never written; primary WAN never disabled):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| VRRP protocol 112 flows | `ProtectedVrrpFlow` advertisement | `Ac1VrrpProtocol112AdvertisementFlowsAreGenerated` |
| IPv4/IPv6 multicast destinations | `224.0.0.18` / `ff02::12`, TTL/HL 255 | `Ac2Ipv4AndIpv6MulticastDestinationsAreChecked` |
| VRRP conntrack sync | UDP 8275 INPUT/OUTPUT | `Ac3VrrpConnectionTrackingSyncFlowsUseConfiguredUdpPort` |
| Missing VRRP member | `VRRP_MEMBER_MISSING` | `Ac4MissingVrrpMemberIsBlocker` |
| Split-master role vector preserved | `RoleVector` + `HasCollapsedGlobalMaster=false` | `Ac5SplitMasterRoleVectorIsPreserved` |
| All uplinks have zone coverage | `UPLINK_ZONE_COVERAGE_MISSING` | `Ac6AllUplinksMustHaveZoneCoverage` |
| Routing tables/rules in context | `mfc.policy.topology_dependency_context.v1` | `Ac7RoutingTablesAndRulesEnterContextHash` |
| PCC and routing marks detected | `MANGLE_PCC_PRESENT` / `MANGLE_ROUTING_MARK_PRESENT` WARNING | `Ac8PccAndRoutingMarksAreDetectedAsWarnings` |
| Strict rp-filter + VRRP/asymmetry | `STRICT_RPF_*` | `Ac9StrictRpFilterWithVrrpAndAsymmetryIsBlocked` |
| RAW notrack intersection | `RAW_NOTRACK_*` / `RAW_DEPENDENCY_INDETERMINATE` | `Ac10RawNotrackIntersectionIsAnalyzed` |
| DSTNAT dependencies | `DSTNAT_MATCH_WITHOUT_NAT_EVIDENCE` WARNING / `NAT_DEPENDENCY_INDETERMINATE` | `Ac11DstNatDependenciesAreAnalyzed` |
| Mangle dependency hash in analysis context | 4-arg combiner ≠ 3-arg | `Ac12MangleDependencyHashEntersAnalysisContext` |
| Switch FORWARD blocked | `SWITCH_FORWARD_POLICY_UNSUPPORTED` always when `NodeKind.Switch`; chip/transit fail-closed | `Ac13SwitchForwardPolicyIsBlocked` |
| Operational route or VRRP role ≠ policy/context hash | observation hash only | `Ac14OperationalRouteOrVrrpRoleDoesNotChangeContextHash` |
| Canonical mapper (no RouterOS) | `TopologyDependencyContextMapper` | `CanonicalSectionsMapToDomainBlockersWithoutWritingNatOrVrrp` |
| Discovery mapper (sync + RAW + rp-filter) | `TopologyDependencyBlockerMapper` | `DiscoveryMapsVrrpSyncRawNatAndRpFilterWithoutWritingFacilities` |
| Topology BLOCKERs trailer | FailedPrecondition, retryable=false | `SequenceAndActualFilterBlockersAreFailedPreconditionNotRetryable` |

**Residuals:** Desktop OUT; no new RPC; compose unchanged (topology-dependency is analysis, not a company document). Proto-112 **write**/guard placement is M5/M3. FastTrack PCC/balanced block is M2-15 (DONE). Deploy gating is N1-06. M1-18 remains topology SoT for version/cardinality/`VRRP_SPLIT_MASTER` inventory findings; this slice emits `VRRP_MEMBER_MISSING` and preserves the per-VRID role vector without collapsing it. Explicit approved infrastructure exception for strict RPF is not modeled — fail-closed BLOCKER. Cube-level RAW notrack vs stateful disjoint proof is fail-closed intersection when both exist. `NAT_DEPENDENCY_CHANGED` / `MANGLE_DEPENDENCY_CHANGED` are reserved FailedPrecondition codes; single-shot `Analyze()` has no prior snapshot, so identity changes are proven by topology context hash (AC#12) rather than a CHANGED finding. M2-12/N1-04/M2-13 analysis-context preimages are unchanged. Controller never writes NAT/RAW/Mangle/VRRP and never disables primary WAN or L2/L3 hardware offload.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~TopologyDependency|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — FastTrack policy validation (M2-15)

Policy Model §52–§52.3 + Compiler §21 + Issue Set M2-15 AC#1–12 → Domain `FastTrackAnalysis` + Application canonical mapper + RouterOs discovery mapper (Desktop OUT, no new RPC; compose unchanged; FastTrack/ACCEPT pair never compiled or written):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| IPv4 FORWARD only | `FASTTRACK_CONTEXT_UNSUPPORTED` on IPv6/INPUT/OUTPUT | `Ac1AllowedOnlyOnIpv4Forward` |
| Company STATE_PRELUDE only | stage allowlist (`RequiredOwner` = Company) | `Ac2AllowedOnlyOnCompanyStatePrelude` |
| ESTABLISHED,RELATED subset | unconstrained / NEW / UNTRACKED blocked | `Ac3ConnectionStateMustBeEstablishedRelatedSubset` |
| TCP/UDP protocol subset | any-protocol / ICMP / missing catalog blocked | `Ac4ProtocolMustBeTcpOrUdpSubset` |
| IPv6 FastTrack blocked | CONTEXT BLOCKER | `Ac5Ipv6FastTrackIsBlocked` |
| PCC and balanced/mixed WAN blocked | CONTEXT; FAILOVER+main allowed | `Ac6PccAndBalancedMixedMultiWanBlockFastTrack` |
| Routing marks and non-main tables blocked | CONTEXT; unknown uplink mode blocked | `Ac7RoutingMarksAndNonMainTablesBlockFastTrack` |
| IPsec, VRF, unknown Mangle blocked | CONTEXT | `Ac8IpsecVrfAndUnknownMangleBlockFastTrack` |
| Pre-anchor unmanaged FastTrack accounted | `PRE_ANCHOR_FASTTRACK_BYPASSES_POLICY` | `Ac9PreAnchorUnmanagedFastTrackIsAccounted` |
| ACCEPT fallback compiler contract | `RequiresAcceptFallback` + WARNING, not FailedPrecondition | `Ac10FallbackAcceptIsMandatoryCompilerContract` |
| Risk not below HIGH | `RiskFloor=HIGH`; logging/capability BLOCKERs | `Ac11FastTrackRiskIsNotBelowHigh` |
| Allowed and forbidden topologies + hash isolation | 5-arg combiner ≠ 4-arg; M2-14 preimage unchanged | `Ac12HashSlotIsIsolatedFromPriorCombiners` |
| Canonical mapper (no RouterOS) | `FastTrackContextMapper` | `CanonicalSingleWanMapsToSafeFastTrackWithoutCompilingFallback` |
| Discovery mapper (pre-anchor + VRF; FT-active observation-only) | `FastTrackBlockerMapper` | `DiscoveryPccPreAnchorAndVrfBlockFastTrackAndFasttrackActiveIsObservationOnly` |
| FastTrack BLOCKERs trailer | FailedPrecondition, retryable=false | `SequenceAndActualFilterBlockersAreFailedPreconditionNotRetryable` |

**Residuals:** Desktop OUT; no new RPC; compose unchanged (FastTrack analysis is not a company document). Compiler FastTrack+ACCEPT pair write is M3-06. Deploy gating is N1-06. `PolicyRule` has no owner field — AC#2 is STATE_PRELUDE ⇒ Company via `PolicyPipelineV1.RequiredOwner`. HotSpot / global queue-tree are caller-supplied topology flags (discovery does not currently prove absence). `ipv4-fasttrack-active` is observation-only and does not enter the FastTrack context hash. Pre-anchor FastTrack without a candidate FastTrack rule remains M2-12 `ActualFilterAnalysis`. `FASTTRACK_OVERLAP` stays M2-11 sequence analysis. M2-12/N1-04/M2-13/M2-14 analysis-context preimages are unchanged. Controller never writes FastTrack or the ACCEPT fallback pair.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~FastTrack|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — policy tests, semantic diff and risk (M2-16)

Policy Model §54–§61 + Issue Set M2-16 AC#1–12 → Domain `PolicyEvidenceAnalysis` + Application canonical mapper + RouterOs discovery mapper (Desktop OUT, no new RPC; compose unchanged; `PolicyDocument.Tests` stays opaque JSON; tests evaluated, not generated):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| MANAGED_ONLY and NODE_EFFECTIVE | missing actual filter → INDETERMINATE; anchor-only NODE_EFFECTIVE walks managed | `Ac1ManagedOnlyAndNodeEffectiveModesAreSupported` |
| SYSTEM tests cannot be disabled | `Create(..., enabled: false)` throws; evaluator BLOCKER | `Ac2SystemTestsCannotBeDisabled` |
| Failed safety test is a blocker | SYSTEM expected mismatch → `POLICY_TEST_SAFETY_FAILED` | `Ac3FailedSafetyTestIsBlocker` |
| Matched rule and path returned | `MatchedRuleId` / `MatchedPath` / stage | `Ac4MatchedRuleAndPathAreReturned` |
| Managed rule UUID for diff | no fuzzy match; reconstituted identical predicate ≠ MODIFIED | `Ac5ManagedRuleUuidIsUsedForDiff` |
| Added/removed/modified/moved/enabled/disabled | UUID change classes | `Ac6AddedRemovedModifiedMovedEnabledDisabledAreDetermined` |
| Object changes have impact set | address UUID + dependent rule ids | `Ac7ObjectChangesHaveImpactSet` |
| Newly accepted / newly denied packet spaces | `Relate` on union of enabled ACCEPT/FastTrack | `Ac8NewlyAcceptedAndNewlyDeniedPacketSpacesAreClassified` |
| Risk from normative mapping | add-allow HIGH; identical NONE/LOW | `Ac9RiskUsesNormativeMapping` |
| Management / FastTrack / exception / default minimums | signals + FastTrack UUID change | `Ac10ManagementFastTrackExceptionAndDefaultHaveMinimumRisk` |
| Diff and risk deterministic | same inputs → same evidence hash | `Ac11DiffAndRiskAreDeterministic` |
| Tests/diff/risk enter analysis hash | 6-arg combiner ≠ 5-arg; M2-15 preimage unchanged | `Ac12TestsDiffAndRiskEnterAnalysisContextHash` |
| Canonical mapper (no RouterOS) | `PolicyEvidenceContextMapper` | `CanonicalFilterEnablesNodeEffectiveWithoutWritingPolicy` |
| Discovery mapper (pre-anchor unmanaged) | `PolicyEvidenceBlockerMapper` | `DiscoveryPreAnchorAcceptFailsNodeEffectiveSafetyWithoutWritingFilters` |
| Unevaluated actual matchers fail closed | CIDR / extra known / unknown action → INDETERMINATE | `NodeEffectiveUnevaluatedActualMatchersAreIndeterminate` |
| Safety BLOCKERs trailer | FailedPrecondition, retryable=false | `SequenceAndActualFilterBlockersAreFailedPreconditionNotRetryable` |

**Residuals:** Desktop OUT; compose unchanged. Typed `PolicyDocument.Tests` deferred (opaque JSON so canonical hashes stay stable). Mandatory *generation* of missing user tests for every ACCEPT/FastTrack/exception (§55) is out of scope — this slice evaluates caller-supplied tests. Approval/binding is M2-17 (missing `PolicyEvidenceSignals` is unknown CRITICAL there). Compiler writes are M3. Deploy gating is N1-06. Exception/management/default/zone-binding minimum risk is caller-supplied `PolicyEvidenceSignals` except FastTrack inferred from rule-effect + UUID changes. NODE_EFFECTIVE actual-filter match is coarse: only exact-host `protocol`/`src-address`/`dst-address`/`connection-state` may Hit or Miss; CIDR/range/list, extra known matchers (`src-port`, interface, …), unknown matchers, and actions outside accept/drop/reject/fasttrack-connection/return are INDETERMINATE. Packet-space classes union enabled ACCEPT/FastTrack cubes and do not replay M2-11 sequence. M2-12…M2-15 analysis-context preimages are unchanged. Controller never writes tests, policy, or filter rules.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~PolicyEvidence|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — approval and desired binding (M2-17)

Policy Model §9–§10 / §34.4 / §63–§67 + Issue Set M2-17 AC#1–13 + E2E §23–§24 → Domain gate + Application use cases + Persistence + `PolicyService` RPCs (Desktop OUT; compose unchanged; no compiler/RouterOS writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Analysis run immutable | `PolicyAnalysisRun` + DbContext append-only | `Ac1AnalysisRunIsImmutableAndBundleHashIsContentAddressed`; persist `AnalysisRunAndApprovalAreAppendOnlyBindingStateCanChange` |
| Approval bound to exact bundle hash | `PolicyApprovalGate` / `PolicyApprovalHasher` | `Ac2ApprovalIsBoundToExactAnalysisBundleHash` |
| Blocker forbids approval | findings BLOCKER | `Ac3BlockerForbidsApproval`; `Ac3BlockerForbidsApprovalUseCase` |
| Warning needs exact-hash ack | `PolicyWarningAcknowledgment` | `Ac4WarningRequiresAcknowledgmentOfExactHash`; `Ac4WarningAckAndAc11StaleFingerprint` |
| High/Critical SoD | HIGH ≠ author; CRITICAL two + security | `Ac5HighAndCriticalSeparationOfDutiesApplies` |
| Missing signals = CRITICAL | `EffectiveRiskLevel` | `Ac5MissingEvidenceSignalsIsUnknownCriticalNotSilentNone` |
| Approval does not activate binding | vote `BindingIds` empty | `Ac6ApprovalDoesNotActivateBinding`; `Ac6AndAc13ApproveDoesNotBindAndIsAudited` |
| Binding only APPROVED / not REVOKED | `PolicyBindingGate` | `Ac7BindingAllowedOnlyForApprovedRevision` |
| Binding pinned to approved analysis run | `PolicyRevision.ApprovedAnalysisRunId` | `Ac2BindingRejectsAnalysisRunThatDidNotCompleteApproval`; `BindingRejectsAnalysisRunThatDidNotCompleteApproval` |
| Completing vote + APPROVED are atomic | `IUnitOfWork` | `CompletingVoteRecoversWhenRevisionStillInReview` |
| Binding / expiry do not deploy | `DeploymentStarted = false` | `Ac8AndAc10BindingAndExpiryDoNotDeploy`; host `ApprovalAndDesiredBindingAreSeparateAndDoNotDeploy` |
| Company/Site/Node cardinality | replacement leaves one ACTIVE; EXCEPTION cap 256 | `Ac9CompanySiteNodeCardinalityReplacementLeavesOneActive`; `Ac9ExceptionCapIsTwoHundredFiftySix` |
| Dependency change stale; runtime obs excluded | fingerprint vs analyzer bump | `Ac11DependencyChangeMarksApprovalStaleAndRuntimeObservationIsExcluded` |
| Idempotency + CAS | use cases | `Ac12IdempotencyReplayAndCasConflict`; `Ac12RowVersionOptimisticConcurrencyIncrementsOnBindingMutation`; `RecordAnalysisRunIdempotencyConflictsWhenTestsDiffer` |
| Transitions audited | `IAuditEventWriter` | `Ac6AndAc13ApproveDoesNotBindAndIsAudited` |
| Frozen codes trailer | FailedPrecondition, retryable=false | `SequenceAndActualFilterBlockersAreFailedPreconditionNotRetryable` |
| RPC surface | `policy.proto` | `ApprovalAndBindingRpcsExposeIdempotencyAndCas` |

**Residuals:** Dedicated `policy_findings` / `policy_test_results` tables deferred (payload JSON on the immutable run). Mandatory test *generation* remains out of scope. Compiler writes are M3. Deploy gating is N1-06. Emergency approval bypass is out of scope. Compose RPC and `PolicyDocument.Tests` opaque JSON are unchanged. M2-12…M2-16 analysis-context combiners are unchanged. Controller never writes RouterOS. Desktop authoring/review delivered in M2-18.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~PolicyApproval|FullyQualifiedName~GrpcApplicationErrorMapper|FullyQualifiedName~PolicyProtoContract"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~PolicyApprovalPersist|FullyQualifiedName~ApprovalAndDesiredBinding"
```

## Living Specification — policy authoring and review (M2-18)

Policy Model §16 / §18 / §9 / §60–§61 + Issue Set M2-18 → Domain writer + Application validate/catalog/diff + `PolicyService` RPCs + Desktop authoring/review (Contracts-only ADR 0005; no RouterOS):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Address/Service JSON writer ↔ reader | `PolicyObjectJsonWriter` | `AddressRoundTripsHostPrefixAndRange`; `Ipv6PrefixRoundTrips`; `ServiceRoundTripsTcpTerm` |
| Document catalog helpers | `PolicyDocument.WithAddressObjects/WithServiceObjects/WithTests` | `WithCatalogHelpersReplaceCollections` |
| DRAFT→VALIDATED + CAS + idempotency + audit | `ValidateRevisionUseCase` | `ValidateRevisionDraftToValidatedWithCasAndIdempotency` |
| Upsert address/service by id | `UpsertAddressObjectUseCase` / `UpsertServiceObjectUseCase` | `UpsertAddressObjectAddsAndReplacesById`; `UpsertServiceObjectAndReplaceTests` |
| COMPANY_BASELINE chain contracts | `ReplaceChainContractsUseCase` | `ReplaceChainContractsCompanyBaselineOnly` |
| Opaque tests replace | `ReplacePolicyTestsUseCase` | `UpsertServiceObjectAndReplaceTests` |
| Semantic diff + risk (signals none) | `DiffPolicyRevisionsUseCase` | `DiffPolicyRevisionsReportsRuleChangeAndRisk` |
| GetPolicyRevision catalogs on wire | `policy.proto` fields 13–16 | `PolicyRevisionExposesCatalogFields` |
| New RPC surface | `PolicyService` | `M218AuthoringReviewRpcsArePresent` |
| AC#1 Desktop editors (address/service/rules/contracts/tests) | `PolicyPanelService` / `PoliciesViewModel` | `Ac1EditorsUpsertAddressServiceContractsAndTests` |
| AC#2 Fixed stages cannot cross-reorder | `PolicyPanelService.ReorderRulesInStageAsync` | `Ac2Ac3ReorderRejectsCrossStageAndAcceptsSameStagePermutation` |
| AC#3 Contiguous ordinal via ReorderRules | same + family/chain/stage | `Ac2Ac3ReorderRejectsCrossStageAndAcceptsSameStagePermutation` |
| AC#4 No raw matcher string (proto TrafficPredicate only) | `ParseAddressEntries` + AddRule selectors | `Ac4ParseAddressEntriesRejectsRawMatcherAndAcceptsHostCidrRange` |
| AC#5 Server validation via RpcException detail | `PoliciesViewModel.RunBusyAsync` | (UI surfaces `RpcException.Status.Detail`; panel uses server RPCs) |
| AC#6 Findings + compose / residual NODE_EFFECTIVE | `ComposeAsync` + RecordAnalysisRun | `Ac6Ac7Ac8ComposeDiffAndAnalysisRiskSurfaces` |
| AC#7 Semantic diff before approval | `DiffAsync` | `Ac6Ac7Ac8ComposeDiffAndAnalysisRiskSurfaces` |
| AC#8 Risk level from analysis run | `RecordAnalysisRunAsync` | `Ac6Ac7Ac8ComposeDiffAndAnalysisRiskSurfaces` |
| AC#9 Separate Save/Validate/Submit/Approve/Bind/Deploy | `PoliciesViewModel` commands | `Ac9Ac10SeparateActionsAndNoSaveAndDeploySurface` |
| AC#10 No Save and Deploy | command surface | `Ac9Ac10SeparateActionsAndNoSaveAndDeploySurface` |
| AC#11 Approved/InReview read-only | `PolicyRevisionPanelState.IsReadOnly` | `Ac11ApprovedRevisionIsReadOnly` |
| Desktop RPC wiring (Contracts-only) | `IPolicyServiceClient` | `DesktopClientsCoverInventorySnapshotCompareZoneAndPolicyRpcs` |

**Residuals:** Typed `PolicyDocument.Tests` still opaque JSON text box. Full NODE_EFFECTIVE / per-device analysis hashes need device context — Desktop reuses logical-effective/content hash slots for `RecordAnalysisRun` wiring. Deploy button present with `CanExecute=false` (N1-06). Composer/RouterOS writes remain out of scope (M3/M4).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~PolicyObjectJsonWriter|FullyQualifiedName~PolicyAuthoringReview|FullyQualifiedName~PolicyProtoContract|FullyQualifiedName~PolicyDesktop|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — RouterOS filter artifact model (M3-01)

Compiler Spec §6–§7 / §24 + Issue Set M3-01 → Domain immutable artifact (no Application compile orchestration yet; no RouterOS writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 address lists + chains + anchor targets | `RouterOsFilterArtifact.Create` | `Ac1Ac9CreateSealsImmutableAddressListsChainsAndAnchors` |
| AC#2 no API commands | `FilterRuleArtifact` / identity guards | `Ac2Ac3RejectApiCommandsAndRouterOsId` |
| AC#3 no RouterOS `.id` | matcher/key guards | `Ac2Ac3RejectApiCommandsAndRouterOsId` |
| AC#4 deterministic physical semantics hash | `HashPhysicalSemantics` | `Ac4Ac5Ac6Ac7IdentityHashesAreDeterministicAndExcludeTimestamps` |
| AC#5 artifact_id = first 16 hex of seed | `ComputeArtifactId` | `Ac4Ac5Ac6Ac7…`; `Ac10CanonicalTestVectorsAreFixed` |
| AC#6 resource_hash = SHA256(MFC-CJ1 bytes) | `HashResourceDocument` | `Ac4Ac5Ac6Ac7…`; `Ac10…` |
| AC#7 timestamps not in hash preimage | `PhysicalSemanticsMaterial` fields | `Ac4Ac5Ac6Ac7…` |
| AC#8 description-only does not change artifact | semantics exclude descriptions | `Ac8DescriptionOnlyChangeDoesNotAlterPhysicalSemanticsOrArtifact` |
| AC#9 payload immutable | frozen lists + sealed bytes | `Ac1Ac9CreateSealsImmutableAddressListsChainsAndAnchors` |
| AC#10 canonical test vectors | fixed digests + JSON shape | `Ac10CanonicalTestVectorsAreFixed` |
| Deterministic sort order | Create sorting | `SortingIsDeterministicRegardlessOfInputOrder` |

**Residuals:** Matcher mapping (M3-05 DONE), FastTrack pair emission (M3-06), per-device compile orchestration (M3-07), and any RouterOS write path remain out of scope. Compiler still must not run without current PASS analysis (§6) — gate lands with orchestration. Address-list compilation is M3-03.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~RouterOsFilterArtifact"
```

## Living Specification — managed chain namespace and layout (M3-02)

Compiler Spec §8 / §11 + Issue Set M3-02 → Domain layout builder on M3-01 artifact types (no Application orchestration; no RouterOS writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 mfc4 / mfc6 namespaces | `ManagedChainNamespace` | `Ac1NamespacesAreMfc4AndMfc6` |
| AC#2 one root per family/chain | `ManagedChainLayoutBuilder` | `Ac2OneRootPerFamilyChain` |
| AC#3 max three deny chains | layout roles dc/ds/dn | `Ac3Ac4MaxThreeDenyChainsAndEmptyDenyOmitsChainAndJump` |
| AC#4 empty deny → no chain / no jump | omit empty deny bodies | `Ac3Ac4…` |
| AC#5 root order = Pipeline v1 | stage/jump/terminal order | `Ac5RootStageOrderMatchesPipelineV1` |
| AC#6 deny ends with unconditional return | structural return rule | `Ac6DenyChainEndsWithUnconditionalReturn` |
| AC#7 root explicit terminal | `mfc:s:terminal` | `Ac7Ac8RootHasExplicitTerminalAndAcceptImpossible` |
| AC#8 default accept impossible | `ChainDefaultDisposition` + terminal map | `Ac7Ac8…` |
| AC#9 management guard not in artifact | guard comment reject | `Ac9ManagementGuardRejectedFromArtifact` |
| AC#10 no physical anchor creation | desired targets only; reject anchor-marked bodies | `Ac10CompilerEmitsDesiredTargetNotPhysicalAnchorRules` |

**Residuals:** Matcher mapping (M3-05 DONE), FastTrack pair emission (M3-06), per-device compile orchestration (M3-07), deny-stage exception-before-deny ordering at full compile, and any RouterOS write path remain out of scope. Address-list compilation is M3-03.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ManagedChainLayout"
```

## Living Specification — content-addressed address lists (M3-03)

Compiler Spec §8.4 / §16 / §17 / §27 + Issue Set M3-03 → Domain `AddressListCompileSession` (no Application orchestration; no RouterOS writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 include/exclude exact | `AddressSelectorResolver` + `AddressPrefixEncoder` + compile | `Ac1IncludeExcludeResolveIsExactAgainstResolverAndEncoder` |
| AC#2 positive → one list matcher | `AddressListCompileSession` | `Ac2PositiveSelectorUsesOneListMatcher` |
| AC#3 universe-minus-exclusions → negated matcher; list = exclude union | compile + `!mfc{4\|6}.a.*` | `Ac3UniverseMinusExclusionsUsesNegatedMatcherAndExcludeUnionContent` |
| AC#4 empty selector → `ADDRESS_SELECTOR_EMPTY`; no partial lists | `PolicyCompilerCodes` | `Ac4EmptySelectorBlocksCompilationWithoutPartialLists`; `Ac4FailedSecondSelectorDoesNotInternFirstDraft` |
| AC#5 same content interned | content-hash intern; `ReferencedLists` ≠ `InternedLists` | `Ac5IdenticalContentReusesSameList`; `Ac8…` |
| AC#6 entries deterministic + sorted | encode + ordinal sort | `Ac6EntriesAreDeterministicAndSortedRegardlessOfInputOrder` |
| AC#7 no timeout | `AddressListEntryArtifact` address-only | `Ac7TimeoutIsNotUsedOnEntries` |
| AC#8 bounded names `mfc{4\|6}.a.<16-hex>` | `ManagedChainNamespace.AddressListName` | `Ac8GeneratedNamesAreBoundedMfcFamilyAContentHash` |
| AC#9 ≤1 src and ≤1 dst matcher | matcher keys | `Ac9SourceAndDestinationUseAtMostOneMatcherEach` |
| AC#10 list/entry limits | `AddressListCompileLimits` clamp to layout v1 | `Ac10ListAndEntryLimitsAreEnforced`; `LayoutV1LimitsRejectOutOfRangeCaps` |
| Truncated-name collision | `RESOURCE_NAME_COLLISION` | intern key remains full SHA-256; truncated RouterOS name fail-closed |
| Prefix encode hosts / universe | `AddressPrefixEncoder` | `AddressPrefixEncoderOmitsHostSlashAndEncodesUniverse` |

**Residuals:** Matcher mapping (M3-05 DONE), FastTrack pair emission (M3-06), per-device compile orchestration (M3-07), and any RouterOS write path remain out of scope. Compiler still must not run without current analysis (§6). Zone/service variants are M3-04.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~AddressListCompiler"
```

## Living Specification — zone and service variants (M3-04)

Compiler Spec §14 / §18 / §19 / §27 + Issue Set M3-04 → Domain `ZoneServiceVariantCompiler` (no Application orchestration; no RouterOS writes; no connection-state/effect mapping):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 exact interface-list used directly | `in-interface-list` / `out-interface-list` | `Ac1ExactInterfaceListBindingIsUsedDirectly` |
| AC#2 other selectors → finite interfaces | `in-interface` / `out-interface` | `Ac2OtherZoneSelectorsExpandToFiniteInterfaces` |
| AC#3 ingress×egress Cartesian bounded | §14 key + `ZONE_EXPANSION_LIMIT` / `RULE_VARIANT_LIMIT` | `Ac3IngressEgressCartesianProductIsBounded` |
| AC#4 service terms canonicalized | `ServiceSelectorResolver` + `ServiceObject.CanonicalizeTerms` | `Ac4ServiceTermsAreCanonicalized` |
| AC#5 ICMP selectors → separate variants | `icmp-options` | `Ac5IcmpSelectorsCreateSeparateVariants` |
| AC#6 port matcher bounded encoded size | `PortMatcherEncoder` + `SERVICE_TERM_TOO_LARGE` | `Ac6PortMatcherHasBoundedEncodedSize` |
| AC#7 variant order deterministic | service×ingress×egress×icmp | `Ac7VariantOrderIsDeterministic` |
| AC#8 running state unused | observation has Name+Dynamic only | `Ac8InterfaceRunningStateIsNotACompileInput` |
| AC#9 current active WAN unused | `ActiveWanName` ignored | `Ac9CurrentActiveWanDoesNotChangeVariants` |
| AC#10 empty/stale zone blocks | `ZONE_*` / `COMPILER_ANALYSIS_STALE` | `Ac10EmptyOrStaleZoneBlocksCompilation` |

**Residuals:** Matcher/effect compile is M3-05. FastTrack pair emission (M3-06), per-device compile orchestration (M3-07), and any RouterOS write path remain out of scope. Compiler still must not run without current analysis (§6).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ZoneServiceVariantCompiler"
```

## Living Specification — filter matchers and regular effects (M3-05)

Compiler Spec §15 / §20 / §23 / §27 + Issue Set M3-05 → Domain `FilterMatcherEffectCompiler` + `RouterOsCompilerProfile` (no Application orchestration; no RouterOS writes; no FastTrack pair):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 exact matcher mapping | `RouterOsCompilerProfile` + address/zone compilers | `Ac1NormativeMatchersHaveExactMapping` |
| AC#2 unsupported token → compile error | `TryNormalizeMatcher` / `UNSUPPORTED_MATCHER` | `Ac2UnsupportedTokenIsCompileError` |
| AC#3 ACCEPT/DROP/REJECT exact | effect map | `Ac3AcceptDropRejectCompileExactly` |
| AC#4 REJECT never becomes DROP | `reject` + `reject-with` | `Ac4RejectIsNeverReplacedWithDrop` |
| AC#5 exception → `action=return` | `EXEMPT_DENY_STAGE` + `:ex` | `Ac5ExceptionCompilesAsReturn` |
| AC#6 structural comments deterministic | `CompilerComments` ≡ layout builder | `Ac6StructuralJumpsHaveDeterministicComments` |
| AC#7 variants adjacent | one logical rule, then next | `Ac7LogicalRuleVariantsAreAdjacent` |
| AC#8 no logical-rule reorder | input-list order, not ordinal | `Ac8CompilerDoesNotReorderLogicalRules` |
| AC#9 duplicates not deleted | two identical ACCEPT rules | `Ac9CompilerDoesNotDeleteDuplicates` |
| AC#10 comments have no user metadata | `mfc:r:<uuid>:<index>` | `Ac10GeneratedCommentsContainNoUserMetadata` |
| FastTrack out of scope | `FASTTRACK_CONTEXT_UNSUPPORTED` | `FastTrackFailsClosedWithoutEmittingAPair` |
| 20 000 physical rules / family+chain | `FILTER_RULE_LIMIT` | `FilterRuleLimitIsEnforcedPerFamilyChain` |

**Residuals:** FastTrack pair emission is M3-06. Per-device compile orchestration (M3-07), and any RouterOS write path remain out of scope. Compiler still must not run without current analysis (§6).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~FilterMatcherEffectCompiler"
```

## Living Specification — FastTrack pairs and terminal rules (M3-06)

Compiler Spec §21 / §22 / §23 + Issue Set M3-06 → Domain `FilterMatcherEffectCompiler` FastTrack pair + `ChainTerminalCompiler` (no Application orchestration; no RouterOS writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 one variant → exactly two rules | FastTrack pair emit | `Ac1OneLogicalVariantCreatesExactlyTwoRules` |
| AC#2 fasttrack-connection + accept adjacent | pair order | `Ac2FastTrackAndAcceptAreAdjacent` |
| AC#3 pair matchers identical | shared matcher map | `Ac3PairMatchersAreIdentical` |
| AC#4 `hw-offload=no` | action parameter | `Ac4HwOffloadIsNo` |
| AC#5 FastTrack logging forbidden | `FASTTRACK_LOGGING_UNSUPPORTED` | `Ac5FastTrackLoggingIsForbidden` |
| AC#6 comments `:ft` / `:ac` | `CompilerComments` | `Ac6PairCommentsHaveFtAndAcSuffixes` |
| AC#7 terminal matches contract | `ChainTerminalCompiler` | `Ac7ChainTerminalMatchesContract` |
| AC#8 RETURN_TO_UNMANAGED → explicit return | terminal map | `Ac8ReturnToUnmanagedCompilesAsExplicitReturn` |
| AC#9 exactly one root terminal | layout + terminal | `Ac9RootChainHasExactlyOneTerminalRule` |
| AC#10 unsupported FastTrack context blocks | topology / allowlist gate | `Ac10UnsupportedFastTrackContextBlocksCompilation` |

**Residuals:** Per-device compile orchestration is M3-07. Any RouterOS write path remains out of scope. Compiler still must not run without current analysis (§6).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~FastTrackTerminalCompiler"
```

## Living Specification — per-device compile + artifact storage (M3-07)

Compiler Spec §4–§7 / §28 / §33.4–§33.5 + Issue Set M3-07 → Domain `DeviceFilterCompiler` + Application store/RPC (no RouterOS writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 approved PASS analysis only | compile gates | `Ac1CompilerRequiresApprovedPassAnalysis` |
| AC#2 shared logical effective hash | `CompileNode` | `Ac2LogicalEffectiveHashSharedAcrossVrrpMembers` |
| AC#3 device-resolved includes zones | `DeviceResolvedPolicyHasher` | `Ac3DeviceResolvedHashIncludesPhysicalZoneResolution` |
| AC#4 VRRP role excluded | request shape | `Ac4VrrpRoleIsNotAnInput` |
| AC#5 active WAN ignored | `ActiveWanName=null` | `Ac5ActiveWanDoesNotAffectArtifact` |
| AC#6–7 content-addressed / stable | `resource_hash` | `Ac6Ac7ResourceHashIsContentAddressedAndStable` |
| AC#8 partial Node ≠ success | `CompileNode` fail-closed | `Ac8PartialNodeCompileIsNotSuccess` |
| AC#9 semantic summary only | `FilterArtifactSemanticSummary` | `Ac9SummaryHasNoRouterOsCommands` |
| AC#10 stale analysis/capability blocks | gate codes | `Ac10StaleAnalysisOrCapabilityBlocksCompilation` |
| Zone bindings must fully resolve | `TryCaptureResolvedZones` | `UnresolvedZoneBindingBlocksCompilation` |

**Residuals:** Full topology acceptance matrix is M3-08 (DONE). RouterOS write path remains out of scope.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeviceFilterCompiler"
```

## Living Specification — compiler acceptance / M3 CLOSED (M3-08)

Compiler Spec §32–§33 + Issue Set M3-08 → Domain `DeviceFilterCompiler` acceptance vectors (no RouterOS writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Standalone IPv4 INPUT allow | Spec §33.1 | `Ac1StandaloneIpv4InputAllow` |
| AC#2 Dual-stack | `mfc4`/`mfc6` roots | `Ac2DualStackCompilation` |
| AC#3 Multi-WAN ≠ active route | WAN variants | `Ac3MultiWanIndependentOfActiveRoute` |
| AC#4 VRRP shared logical hash | `CompileNode` | `Ac4VrrpMembersShareLogicalHash` |
| AC#5 Split-master not an input | request shape | `Ac5SplitMasterRoleIsNotAnInput` |
| AC#6 Switch FORWARD forbidden | `SWITCH_FORWARD_COMPILATION_FORBIDDEN` | `Ac6SwitchForwardCompilationIsForbidden` |
| AC#7 Address content dedup | address-list intern | `Ac7SameAddressContentIsDeduplicated` |
| AC#8 Exception deny layout | company-deny body | `Ac8ExceptionChainLayoutIsCorrect` |
| AC#9 FastTrack pair | final artifact | `Ac9FastTrackPairIsCorrect` |
| AC#10 Root + deny terminals | layout terminals | `Ac10RootAndDenyTerminalsPresent` |
| AC#11 Description-only ≠ resource hash | physical semantics | `Ac11DescriptionOnlyChangeDoesNotAlterResourceHash` |
| AC#12 Deterministic compile | resource_hash / canonical | `Ac12CompileIsDeterministic` |

**Residuals:** RouterOS write / deploy path is M4+. Onboarding domain/persistence is M5-01 (DONE); prerequisites start at M5-02.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeviceFilterCompilerAcceptanceTests"
```

## Living Specification — onboarding domain model and persistence (M5-01)

Onboarding Spec §4–§5 / §18 / §23 / §25–§26 / §48 / §52 / §54 + Issue Set M5-01 → Domain + EF:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 All 14 operation states + happy path | `OnboardingOperation` | `Ac1AllOnboardingStatesAreImplementedAndHappyPathTransitions` |
| AC#2 Node/Device UNMANAGED\|MANAGED\|RECOVERY_REQUIRED | `ManagementState` | `Ac2NodeAndDeviceHaveIndependentManagementStates` |
| AC#3 VRRP targets whole Node | `OnboardingPlan` | `Ac3VrrpOnboardingTargetsTheWholeNode` |
| AC#4 Plan immutable + 30 min lifetime | `OnboardingPlan` / `OnboardingCodes.DefaultPlanLifetime` | `Ac4PlanIsImmutableWithBoundedLifetime` |
| AC#5 Plan hash covers Spec §25 deps | `OnboardingPlanHasher` | `Ac5PlanHashCoversSpec25DependenciesAndExcludesVrrpRoleAndActiveWan` |
| AC#6 One nonterminal per Node | unique index + gate | `Ac6OneNonterminalOnboardingPerNode` |
| AC#7 Write-ahead step journal | `OnboardingStep` | `Ac7WriteAheadStepJournalIsImplemented` |
| AC#8 Completed operation immutable | terminal freeze | `Ac8CompletedOperationIsImmutable` |
| AC#9 Transitions row-versioned / transactional store | `RowVersion` + EF | `Ac9StateTransitionsAreRowVersioned` / `OnboardingPersistTests` |
| AC#10 Invalid transition rejected | `OnboardingOperationGate` | `Ac10InvalidTransitionIsRejected` |
| Bootstrap §23 seed hash | `BootstrapArtifact` | `BootstrapArtifactMatchesSpec23SeedHashAndChainNames` |
| Persistence schema `m5-01` | migration `OnboardingSchemaM501` | `MigrateCreatesOnboardingTablesAndSchemaMetadata` |

**Residuals:** Prerequisite validation / RouterOS reads are M5-02+. No RouterOS writes, gRPC, or Desktop in M5-01.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~Onboarding|FullyQualifiedName~InventoryDomain"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~OnboardingPersistTests"
```

## Living Specification — onboarding prerequisite validation (M5-02)

Onboarding Spec §7–§11 / §58 + Issue Set M5-02 → Domain validator (no RouterOS writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Exact supported build | `OnboardingPrerequisiteValidator` | `Ac1ExactSupportedBuildIsRequired` |
| AC#2 Plain API 8728 disabled | plain `api` service | `Ac2PlainApi8728MustBeDisabled` |
| AC#3 API-SSL certificate | `api-ssl` + capability flag | `Ac3ApiSslCertificateIsMandatory` |
| AC#4 Separate read/deploy accounts | account names | `Ac4ReadAndDeploymentAccountsMustBeSeparated` |
| AC#5 Default groups rejected | Spec §10.2 | `Ac5DefaultRouterOsGroupsAreRejected` |
| AC#6 Required/forbidden policies | Spec §10.1–§10.2 | `Ac6RequiredAndForbiddenPoliciesAreChecked` |
| AC#7 Source address restrictions | Spec §10.3 | `Ac7SourceAddressRestrictionsAreChecked` |
| AC#8 scheduler=yes | Spec §11 | `Ac8SchedulerYesIsRequired` |
| AC#9 flagged=no | Spec §11 | `Ac9FlaggedNoIsRequired` |
| AC#10 No mutate users/services/device-mode | validator shape + no `Mfc.RouterOs.Write` | `Ac10ControllerDoesNotExposeMutatorsForUsersServicesOrDeviceMode` |
| AC#11 All VRRP members | Node members | `Ac11AllVrrpMembersMustPassPrerequisites` |
| AC#12 Stable Spec §58 codes | `OnboardingCodes` | `Ac12FindingsUseStableSpec58Codes` |

**Residuals:** Management guard verification is M5-03 (DONE). Live RouterOS fact adapters / credential probes remain later M5 steps. No gRPC/Desktop in M5-02.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingPrerequisite"
```

## Living Specification — management guard verification (M5-03)

Onboarding Spec §13–§17 / §58 + Issue Set M5-03 → Domain verifier (no RouterOS writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Typed GuardProfile | `GuardProfile` / `GuardProfileId` | `Ac1GuardProfileIsTyped` |
| AC#2 Input/output markers | `GuardMarker` + verifier | `Ac2InputAndOutputGuardMarkersAreChecked` |
| AC#3 Static/valid/enabled | `OnboardingGuardVerifier` | `Ac3GuardRulesMustBeStaticValidAndEnabled` |
| AC#4 Predicate ≤ profile | breadth check | `Ac4PredicateMustNotBeWiderThanProfile` |
| AC#5 Reject `0.0.0.0/0` / `::/0` | profile + rule | `Ac5DefaultRoutesAreRejected` |
| AC#6 Before planned anchors | placements + live anchors | `Ac6GuardMustPrecedePlannedAnchors` |
| AC#7 Dynamic list / unsupported | Spec §16.3 | `Ac7DynamicListAndUnsupportedMatchersAreRejected` |
| AC#8 NEW API-SSL through guard | input connection-state | `Ac8NewApiSslConnectionThroughGuardPasses` |
| AC#9 Guard hash in plan | `GuardProfileHasher` / `ExpectedGuardHash` | `Ac9GuardHashEntersPlan` |
| AC#10 Controller does not create/modify | shape + candidate comments | `Ac10ControllerDoesNotCreateOrModifyGuard` |

**Residuals:** Explicit anchor placement planning is M5-04. Live RouterOS discovery adapters remain later M5 steps. No gRPC/Desktop in M5-03.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingGuard"
```

## Living Specification — snapshot/diff gRPC (M1-26)

Vertical Slice §9.3 + Canonical Spec §30 / Initial Issue Set M1-26 AC → module → tests (Issue Spec = Vertical Slice wire names; Issue Set `CaptureSnapshot`/`WatchSnapshotCapture`/`ListSnapshots` are aliases):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| VS §9.3 RPC surface | `snapshots.proto` / `SnapshotGrpcService` | `SnapshotServiceDescriptorExposesVerticalSliceRpcs` |
| Capture progress streaming | `CaptureProgressHub` + WatchCapture | `StartWatchListSectionCompareAndCancel` |
| Section paging (no unbounded payload) | `GetSnapshotSectionUseCase` | host GetSnapshotSection pages |
| Pagination / continuation | ListCaptures + DiffPage + section page | host list/section/diff pages |
| Watch cancellation without hang | WatchCapture + CTS | cancel mid-flight in host test |
| Viewer ≠ unredacted raw | SnapshotService has no raw RPC | descriptor + section records only |
| Diff stable ordering | `CompareSnapshotsUseCase` / SemanticDiffEngine | CompareSnapshots entries ordered |
| Hashes as 32-byte Sha256 | `SnapshotProtoMapper.HexToSha256Bytes` | summary hashes length 32 |
| Unknown enum forward-compat | DiffEntry proto3 | `DiffEntryWithUnknownEnumValuesRoundTrips` |
| No EF/RouterOS DTO on wire | Contracts only | mapper from Application views |
| Contract serialization | Uuid/Sha256/SnapshotSectionPage | `SnapshotProtoContractTests` |
| No password fields | response descriptors | `SnapshotResponseMessagesHaveNoPasswordFields` |

Filter: `dotnet test --filter FullyQualifiedName~Snapshot`.

Full Living Spec index: [`ROADMAP.md`](../../ROADMAP.md) §5.

## PostgreSQL integration

Integration tests start **PostgreSQL 18** via Testcontainers (Docker required). They do not use SQLite.

## Coverage thresholds (bootstrap)

After unit tests with XPlat coverage:

```bash
python3 scripts/ci/verify-coverage-thresholds.py /path/to/coverage
```

Domain/Application line ≥ 85%, branch ≥ 75% when `lines_valid > 0` (Bootstrap Plan §13.3).

## CHR live matrix

Not enabled until an isolated self-hosted runner exists. Skeleton contracts run in `routeros-integration` workflow and in `Mfc.RouterOs.IntegrationTests`.
