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
