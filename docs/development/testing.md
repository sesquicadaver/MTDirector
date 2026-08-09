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
