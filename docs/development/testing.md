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
| Unmanaged jump into `fwc.*` | INDETERMINATE | `UnmanagedJumpIntoManagedIsIndeterminate` |
| Canonical mapper | `ActualFilterContextMapper` | `CanonicalFilterRecordsMapToDomainRulesAndDetectPreAnchorAccept` |
| Discovery mapper (dynamic + unknown) | `ActualFilterRuleMapper` | `DiscoveryMapsDynamicJumpAndUnknownMatchers` |
| `ACTUAL_FILTER_*` / `PRE_ANCHOR_*` trailer | FailedPrecondition, retryable=false | `SequenceAndActualFilterBlockersAreFailedPreconditionNotRetryable` |

**Residuals:** Desktop OUT; no new RPC; compose-on-read stays logical (actual CFG is analysis level 6, not wired into `ComposeEffectivePolicy`). Witness packets N/A for actual CFG. Management-path safety is M2-13. Canonical filter sections still omit dynamics; RouterOs discovery mapper is the dynamic path. Jump into managed `fwc.*`/`mfc.*` from controller-owned comments is an opaque `ManagedPipeline` node (candidate policy remains M2-11). Walk continues miss-path after terminals so later unmanaged pre-anchor rules stay visible; the anchor itself still stops post-anchor unless `RETURN_TO_UNMANAGED`. M2-13 must not treat `Graph.Edges` as the only reachability oracle — findings come from the walk.

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
