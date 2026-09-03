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
| `tests/Mfc.UnitTests` | Unit + architecture boundary tests + coverage (incl. Inventory, Snapshots/Capabilities, RouterOS discovery + capability + N1 topology/path class, node topology validation, stable-read, raw snapshot, canonicalization, menu projector, capture idempotency/audit, semantic diff M1-24, inventory/snapshot proto contracts M1-25/M1-26, Desktop inventory tree M1-27, Desktop snapshot viewer M1-28, Desktop semantic diff viewer M1-29, fault-injection matrix M1-33, MVP release acceptance M6-09) |
| `tests/Mfc.IntegrationTests` | Controller health + Inventory/Snapshot gRPC host (M1-25/M1-26/ListNodes M1-27), Desktop connection, PostgreSQL bootstrap + inventory/snapshot persist (Testcontainers), standalone/multi-WAN/VRRP vertical-slice acceptance M1-30/M1-31/M1-32, fault-injection acceptance M1-33, onboarding topology acceptance M5-10, standalone/dual-stack E2E inventory→capture→onboarding M6-05, multi-WAN capture slice M1-31 (M6-06 reuse), VRRP capture slice M1-32 (M6-07 reuse), security backup/restore pg_dump/restore M6-08 |
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
| W2.3 VRRP labels from last capture | `GetNodeUseCase` + `DeviceVrrpRoleLabelProjector` | `VrrpRoleLabelsLivingSpecTests` + `GetNodeMapsVrrpRoleLabelsFromLastCaptureObservations` |
| W6-01 last-capture version/model | `DeviceLastCaptureFacts` + GetNode | `DeviceLastCaptureFactsTests` + `GetNodeMapsVrrpRoleLabelsFromLastCaptureObservations` |
| W6-02 VRRP pair consistency RPC | `ValidateVrrpPairConsistency` + Desktop Node | `NeighborCandidatesLivingSpecTests` (RPC name) + `VrrpPairConsistencyAnalyzerTests` + `NodeDetailViewModelTests` |
| Refresh cancellation | `InventoryTreeService.RefreshAsync` | `CancellationStopsRefresh` |
| No parallel overlapping refresh | single-flight coalesce | `ParallelRefreshDoesNotStartTwoOverlappingLoads` |
| Large inventory paged | `ListSites`/`ListNodes` page loops | `ListNodesPaginates…` + `GrpcInventoryTreeClient` |
| Error keeps last successful tree | `InventoryTreeService` | `FailedRefreshPreservesPreviousTreeAndSetsCached` |
| Cached state marked | `IsCached` / UI badge | failed-refresh test + MainWindow badge |
| No RouterOS/SQL in ViewModel | presentation DTOs only | assembly + architecture Desktop bans |
| GUI/state tests | unit (no Avalonia headless) | `InventoryTreeServiceTests` |
| W3.2 ValidateDeviceConnection Probe | Desktop Inventory/Add router | `Ac2cInventoryAndAddRouterProbeValidateDeviceConnection` + `AddRouterWizardViewModelTests` |

Filter: `dotnet test --filter FullyQualifiedName~InventoryTree`.

## Living Specification — VRRP pair consistency (W6-02)

Issue [#354](https://github.com/sesquicadaver/MTDirector/issues/354) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Admin-critical `ha.vrrp` fields MUST agree (same family+VRID) | `VrrpPairConsistencyAnalyzer` | `VrrpPairConsistencyAnalyzerTests` |
| Equal priorities → Finding (not Blocker) | same | same |
| Logical firewall filter digest agreement | same (`firewall.ipv4/ipv6.filter`) | same |
| Desired logical hash divergence → Blocker | same | same |
| Last completed captures (read-only) | `VrrpPairConsistencyLoader` + `ValidateVrrpPairConsistencyUseCase` | Domain + Application wiring |
| Inventory RPC | `ValidateVrrpPairConsistency` | `NeighborCandidatesLivingSpecTests` method name |
| Desktop Node findings + Capture-all path | `NodeDetailViewModel` | `NodeDetailViewModelTests` + MainWindow Node panel |
| Gate Onboarding Validate | merge findings; missing captures are FINDING until captures exist | `ValidateOnboardingPrerequisitesWorkflowUseCase` |
| Gate Deploy/Onboarding CreatePlan | `VrrpPairPlanGate` (onboarding allows incomplete captures; deploy is strict) | CreatePlan use cases Conflict on config/FW blockers |

Filter: `dotnet test --filter FullyQualifiedName~VrrpPairConsistency`.

## Living Specification — StartCapture node_id (W6-03)

Issue [#356](https://github.com/sesquicadaver/MTDirector/issues/356) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| `StartCapture(node_id)` fans out members | `CaptureNodeSnapshotsUseCase` + SnapshotGrpc | `CaptureNodeSnapshotsUseCaseTests` + `SnapshotGrpcHostTests` |
| One WatchCapture stream / terminal COMPLETED | `CaptureProgressHub` device override | host Watch progress |
| Desktop Capture-all uses node_id | `NodeDetailViewModel` + `ISnapshotViewerClient.StartNodeCaptureAsync` | wiring Living Spec |

Filter: `dotnet test --filter FullyQualifiedName~CaptureNodeSnapshots`.

## Living Specification — Onboarding Rollback Watch (W6-04)

Issue [#358](https://github.com/sesquicadaver/MTDirector/issues/358) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Hub Watch after Committed+RolledBack returns full history | `OnboardingProgressHub` | `Ac3bWatchReplaysRollbackEventsAfterCommittedTerminal` |
| Desktop Rollback prefers Watch over Timeline | `OnboardingViewModel.RollbackAndWatchAsync` | `OnboardingViewModelTests` + `Ac6gOnboardingRollbackWatchesProgress` |

Filter: `dotnet test --filter "FullyQualifiedName~OnboardingViewModelTests|FullyQualifiedName~Ac3bWatchReplaysRollbackEventsAfterCommittedTerminal|FullyQualifiedName~Ac6gOnboardingRollbackWatchesProgress"`.

## Living Specification — GetNode Reachability (W6-05 / W6-08)

Issue [#360](https://github.com/sesquicadaver/MTDirector/issues/360) / [#366](https://github.com/sesquicadaver/MTDirector/issues/366) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| LastSupportState → Reachable on GetNode | `DeviceReachabilityProjector` + `ViewMapper` / `GetNodeUseCase` | `DeviceReachabilityProjectorTests` + Inventory GetNode assertions |
| Connectivity probe failure → Unreachable observation | `DiscoverDeviceUseCase` + in-memory store | projector + observation override assertion |
| Probe refreshes inventory tree | `AddRouterWizardViewModel` | `Ac2e…` + `ProbeUsesSelectedDevice…` |
| Unreachable durable without process-local store (W6-08) | `Device.LastObservedReachability` + EF | `DiscoverDevicePersistsUnreachableAcrossEmptyObservationStore` |

Filter: `dotnet test --filter "FullyQualifiedName~DeviceReachabilityProjectorTests|FullyQualifiedName~DiscoverDevicePersistsUnreachable|FullyQualifiedName~Ac2eInventoryProbe|FullyQualifiedName~ProbeUsesSelectedDevice|FullyQualifiedName~GetNodeReturnsDevices"`.


## Living Specification — Policies typed Diff rows (W6-06)

Issue [#362](https://github.com/sesquicadaver/MTDirector/issues/362) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Typed kind/detail rows from PolicyRevisionDiff | `PolicyPanelService.DiffAsync` | `PolicyDesktopServiceTests` |
| Desktop binds DiffRows (DiffLines secondary) | `PoliciesViewModel` + MainWindow | `Ac5gPoliciesRevisionDiffBindsTypedKindDetailRows` |

Filter: `dotnet test --filter "FullyQualifiedName~Ac5gPoliciesRevisionDiff|FullyQualifiedName~PolicyDesktopServiceTests"`.


## Living Specification — Policies Diff baseline catalog (W6-07)

Issue [#364](https://github.com/sesquicadaver/MTDirector/issues/364) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Catalog picker fills baseline UUID without LoadRevision | `PoliciesViewModel.DiffBaselineCatalogItem` | `DiffBaselineCatalogItemFillsBaselineUuidWithoutLoadingRevision` |
| Axaml binds DiffBaselineCatalogItem | MainWindow Policies Diff toolbar | `Ac5hPoliciesDiffBaselinePicksFromCatalogWithoutUuidRitual` |

Filter: `dotnet test --filter "FullyQualifiedName~DiffBaselineCatalogItem|FullyQualifiedName~Ac5hPoliciesDiffBaseline"`.

## Living Specification — Policies Move up/down reorder (W6-09)

Issue [#369](https://github.com/sesquicadaver/MTDirector/issues/369) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Move down builds contiguous stage order without UUID paste | `PoliciesViewModel.MoveRuleDown` | `MoveRuleDownBuildsStageOrderWithoutUuidPaste` |
| Boundary at first skips RPC | `MoveRuleUp` | `MoveRuleUpAtFirstReportsBoundaryWithoutRpc` |
| Axaml binds Move commands (not ReorderRuleIdsText) | MainWindow Policies rules toolbar | `Ac5iPoliciesReorderMovesSelectedRuleWithoutUuidPaste` |

Filter: `dotnet test --filter "FullyQualifiedName~MoveRule|FullyQualifiedName~Ac5iPoliciesReorder"`.

## Living Specification — Reject system actor gRPC spoof (SEC-01) + principal bind (W7-02)

Issues [#371](https://github.com/sesquicadaver/MTDirector/issues/371) / [#402](https://github.com/sesquicadaver/MTDirector/issues/402) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| `x-mfc-actor` = SystemActor → Unauthorized | `GrpcRequestActorResolver` | `GrpcRequestActorResolverTests.RejectsReservedSystemActorViaMetadata` |
| Development non-system metadata accepted | same | `DevelopmentAllowsNonSystemActorMetadata` |
| Production metadata without principal → Unauthorized | same | `ProductionRejectsMetadataWithoutPrincipal` |
| Production peer identity principal used | same | `ProductionUsesPeerIdentityPrincipal` |
| Metadata ≠ principal → Unauthorized | same | `ProductionRejectsMetadataThatDisagreesWithPrincipal` |
| SystemActor via principal rejected | same | `RejectsSystemActorViaPrincipal` |
| `AllowMetadataActor` forbidden outside Development | `ControllerOptionsValidator` | `ProductionRejectsAllowMetadataActor` |
| In-process SystemActor still authorized | `SystemActorAuthorizationBoundary` | `SystemActorBoundaryStillAllowsInProcessJobActor` |

Filter: `dotnet test --filter "FullyQualifiedName~GrpcRequestActorResolverTests|FullyQualifiedName~ProductionRejectsAllowMetadataActor"`.

## Living Specification — Deploy artifact materializer + observed hash (SEC-02)

Issue [#372](https://github.com/sesquicadaver/MTDirector/issues/372) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Canonical filter artifact reader round-trip | `RouterOsFilterArtifactReader` | `Ac1ReaderRoundTripsCanonicalFilterArtifactBody` |
| Staging loads AddressLists/Chains from store | `FilterArtifactStoreDeploymentArtifactMaterializer` | `Ac2MaterializerLoadsListsAndChainsFromStore` |
| Missing artifact fail-closed | same | `Ac3MaterializerFailsClosedWhenArtifactMissing` |
| Observed hash from live lists/chains/anchors | `ObservedManagedResourceHash` | `Ac4ObservedHashMatchesSealedWhenLiveStateAligns` |
| Divergent jump changes hash | same | `Ac5ObservedHashFailsWhenLiveJumpDiverges` |
| WriteEnabled DI not AnchorOnly | `AddRouterOsWriteServices` | `WritePathReadinessLivingSpecTests.Ac3…` |

Filter: `dotnet test --filter "FullyQualifiedName~DeploymentArtifactMaterializerSec02|FullyQualifiedName~WritePathReadinessLivingSpecTests.Ac3"`.

## Living Specification — Audit hash chain (SEC-03)

Issue [#373](https://github.com/sesquicadaver/MTDirector/issues/373) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Hash includes predecessor bytes (same length, different content) | `AuditEventHashing` | `Ac1HashIncludesPreviousEventHashBytesNotOnlyLength` |
| Genesis vs chained | same | `Ac2GenesisAndChainedHashesDifferWithStableIdentity` |
| Event id in preimage | same | `Ac3EventIdIsPartOfPreimage` |
| Append serialization under contention | `EfAuditEventWriter` + unique `PreviousEventHash` | `AuditEventHashChainSec03IntegrationTests.ConcurrentAppendsDoNotForkTipSilently` |

Filter: `dotnet test --filter "FullyQualifiedName~AuditEventHashChainSec03"`.

## Living Specification — INTERNAL_CA trusted CA store (SEC-04)

Issue [#377](https://github.com/sesquicadaver/MTDirector/issues/377) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Directory store loads PEM/DER by CaProfileRef | `DirectoryRouterOsTrustedCaStore` | `Ac1DirectoryStoreLoadsPemForProfileRef` |
| Missing profile → empty (materializer fail-closed) | same | `Ac2MissingProfileOrDirectoryIsEmptyFailClosedMaterial` |
| Path traversal rejected | same | `Ac3PathTraversalCaProfileRefIsRejected` |
| RevocationMode applied (not hardcoded NoCheck) | `ApiSslCertificateValidator` | `Ac4InternalCaRevocationModeIsAppliedNotHardcodedNoCheck` |
| Production DI uses Directory store | `AddMfcSecrets` | `Ac5ProductionDiRegistersDirectoryStoreNotNotConfigured` |
| RevocationMode parser | `TrustedCaRevocationModes` | `Ac6RevocationModeParserDefaultsToOnlineAndRejectsUnknown` |

Filter: `dotnet test --filter "FullyQualifiedName~TrustedCaStoreSec04"`.

## Living Specification — Atomic mutation boundary (SEC-05)

Issue [#378](https://github.com/sesquicadaver/MTDirector/issues/378) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| CreateSite mutation+idempotency+audit in one UoW | `CreateSiteUseCase` + `IUnitOfWork` | `Ac1CreateSiteRunsMutationIdempotencyAndAuditInsideOneUnitOfWork` |
| Failure mid-boundary compensates | same | `Ac2FailureAfterSiteAddRollsBackWhenUnitOfWorkCompensates` |
| Audit joins ambient transaction | `EfAuditEventWriter` | `Ac3AuditWriterJoinsAmbientTransactionInsteadOfNesting` |
| Inventory sources use UoW | `InventoryUseCases` | `Ac4InventoryCreateSiteSourceUsesUnitOfWorkBoundary` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec05"`.

## Living Specification — Desktop MikroTik/Winbox display labels (W7-01)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Section + property labels | `DesktopDisplayLabels` | `DesktopDisplayLabelsTests` |
| Snapshot/Diff bind friendly lines | `SnapshotViewerModels` / `SnapshotDiffModels` / `MainWindow.axaml` | `DesktopMvpWorkflowsLivingSpecTests` |
| Wire export unchanged | `DisplayLine` / export | `SnapshotViewerServiceTests` |

Filter: `dotnet test --filter "FullyQualifiedName~DesktopDisplayLabels|FullyQualifiedName~DesktopMvpWorkflows"`.

## Living Specification — UpsertRoutingAssuranceState UoW (SEC-15)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Upsert in one UoW | `UpsertRoutingAssuranceStateUseCase` | `MutationAtomicitySec15LivingSpecTests.Ac1` |
| Source boundary | `RoutingAssuranceUseCases` | `Ac2` |
| known-limitations | `known-limitations.md` | `Ac3` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec15"`.

## Living Specification — OpenEndpointPresence multi-store UoW (SEC-14)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Assessment+migration in one UoW | `OpenEndpointPresenceUseCase` | `MutationAtomicitySec14LivingSpecTests.Ac1` |
| Source boundary | `EndpointPresenceUseCases` | `Ac2` |
| known-limitations | `known-limitations.md` | `Ac3` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec14"`.

## Living Specification — UpsertDeviceHashState UoW (SEC-13)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Upsert in one UoW | `UpsertDeviceHashStateUseCase` | `MutationAtomicitySec13LivingSpecTests.Ac1` |
| Source boundary | `WorkflowUseCases` | `Ac2` |
| known-limitations | `known-limitations.md` | `Ac3` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec13"`.

## Living Specification — CaptureSnapshot persist+audit UoW (SEC-12)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Persist+audit in one UoW | `CaptureSnapshotUseCase` | `MutationAtomicitySec12LivingSpecTests.Ac1` |
| Capture port outside UoW | same | `Ac2` |
| known-limitations | `known-limitations.md` | `Ac3` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec12"`.

## Living Specification — Drift detect + response-feedback UoW (SEC-11)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Drift event/hash/audit in one UoW | `DetectManagedDriftUseCase` | `MutationAtomicitySec11LivingSpecTests.Ac1` |
| Feedback store+audit in one UoW; delivery outside | `EmitResponseFeedbackUseCase` | `Ac2` / `Ac3` |
| known-limitations | `known-limitations.md` | `Ac4` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec11"`.

## Living Specification — Incident overlay expiry UoW (SEC-10)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Expire binding in one UoW | `ExpireIncidentDenyOverlayBindingUseCase` | `MutationAtomicitySec10LivingSpecTests.Ac1` |
| known-limitations | `known-limitations.md` | `Ac2` |
| No Application idempotency Save outside UoW | `src/Mfc.Application/**/*UseCase*.cs` | `Ac3` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec10"`.

## Living Specification — Onboarding workflow UoW (SEC-09)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Create/start/rollback terminal UoW | `OnboardingWorkflowUseCases` | `MutationAtomicitySec09LivingSpecTests.Ac1` |
| known-limitations | `known-limitations.md` | `Ac2` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec09"`.

## Living Specification — Connection profile + deployment UoW (SEC-08)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Profile upsert + idempotency in one UoW | `UpdateConnectionProfileUseCase` | `MutationAtomicitySec08LivingSpecTests.Ac1` |
| Deployment plan/start/rollback terminal UoW | `DeploymentWorkflowUseCases` | `Ac2` |
| known-limitations residual cleared | `known-limitations.md` | `Ac3` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec08"`.

## Living Specification — Extended atomic mutation boundary (SEC-07)

| ТЗ / AC | Модуль | Тест |
|---------|--------|------|
| Zone create/update/delete + binding upsert/delete in one UoW | `ZoneDefinitionUseCases` / `NodeZoneBindingUseCases` | `MutationAtomicitySec07LivingSpecTests.Ac1` |
| Policy draft/rules/approval/validate/exception metadata use UoW | `PolicyRuleUseCases` / `PolicyApprovalUseCases` / `ValidateRevisionUseCase` / `UpdateExceptionMetadataUseCase` | `Ac2` |
| Documented residual (deploy + connection profile) | `known-limitations.md` | `Ac3` |

Filter: `dotnet test --filter "FullyQualifiedName~MutationAtomicitySec07"`.

## Living Specification — Incident assessment gRPC (SEC-06)

Issue [#380](https://github.com/sesquicadaver/MTDirector/issues/380) AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| incident.proto + MapGrpcService | Contracts + `Program` | `Ac1IncidentProtoAndMapGrpcServiceAreRegistered` |
| Ingest with authz | `IncidentGrpcService` | `Ac2IngestIncidentSignalSucceedsWithAuthz` |
| Ingest fail-closed | same | `Ac3IngestFailsClosedWithoutPermission` |
| Bind assessment + authz | same | `Ac4BindAssessmentSucceedsAndAuthzFailsClosed` |

Filter: `dotnet test --filter "FullyQualifiedName~IncidentGrpcSec06"`.

## Living Specification — desktop snapshot viewer (M1-28)

Initial Issue Set M1-28 AC → module → tests:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Sections + capture status | `SnapshotSummary.sections` / `ListSectionDescriptorsAsync` | host GetSnapshotSummary sections + `SnapshotSummarySectionsRoundTrip…` |
| Config vs observations | `SnapshotViewerService.LoadSectionAsync` | `LoadDeviceLoadsSummarySectionsAndSeparatesDomains` |
| Three hashes + schema version | summary header mapping | same unit test |
| Unknown props technical-only | `ShowTechnicalView` / `IsTechnicalOnly` | `ExportIncludesTechnicalSectionWhenRequested` |
| No credentials in copy/export | field filter + export | domain test strips `password` |
| W1.2 full record fields | `SnapshotRecordListItem.Fields` + selected-record detail | `Ac4cSnapshotRecordDetailShowsAllFields` + `LoadSectionMapsAllRecordFieldsNotOnlySummaryLine` |
| Virtualization / off-UI load | ListBox `VirtualizingStackPanel` + `Task.Run` | UI wiring + service loads on background |
| Record lists stay read-only | no Desktop edit of snapshot records | viewer load/export surface |
| W3.1 StartCapture + WatchCapture | Snapshots Capture + `CaptureProgressText` | `Ac4dSnapshotCaptureStartsAndWatchesProgress` + `SnapshotViewerViewModelTests` |

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
| Desktop CRUD + blockers | `ZonePanelService` / Zones tab | `ZonesDesktopServiceTests` + `ZonesViewModelTests` |
| W3.5 Update zone + Resolve device | `UpdateZoneDefinition` / `ResolveZonesForDevice` | `Ac2dZonesEditDefinitionAndResolveDevice` + `ZonesViewModelTests` |
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

**Known residual (N1-05 / zone slice):** With `Mfc:RouterOs:Enabled=false` (default), `ISnapshotCapturePort` resolves to `NotConfiguredSnapshotCapturePort`. With `Enabled=true`, production capture uses `RouterOsSnapshotCapturePort` (P2-05…P2-06). `DiscoveryCanonicalProjector` (M1-22) is on the production capture path via `RouterOsSnapshotCapturePort`. Marker expansion works whenever LOCK-2 sections are present in a persisted snapshot.

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
| W3.6 Update/Delete/Ack/Compile | `PoliciesViewModel` + `IPolicyServiceClient` | `Ac5cPoliciesMutateRulesAckWarningsAndCompile` + `PoliciesViewModelTests` + `PolicyDesktopServiceTests` |
| W5-01 ListPolicies catalog | `ListPoliciesUseCase` + Policies catalog ListBox | `Ac5dPoliciesCatalogBrowseListPoliciesThenSelectLoadsRevision` + `ListPoliciesUseCaseTests` + `PoliciesViewModelTests` |
| W5-02 ManagementPath / FastTrack | `GetDevicePolicySafetyAnalysisUseCase` + Policies safety panel | `Ac5ePoliciesShowManagementPathAndFastTrackAnalysis` + `GetDevicePolicySafetyAnalysisUseCaseTests` + `PoliciesViewModelTests` |
| W5-03 Typed deploy semantic diff | `DeploymentSemanticDiffEntry` + Desktop `SemanticDiffRows` | `Ac6fDeploymentPlanBindsTypedSemanticDiffRows` + `DeploymentWorkflowLivingSpecTests` + `DeploymentViewModelTests` |
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

**Residuals:** Desktop OUT; no new RPC; logical compose unchanged (device packet-path is not a company document). N1-03 still attaches discovery hints; Domain is the analysis BLOCKER authority. MIXED is not `PACKET_PATH_NOT_PROVEN` (next-1 names only HW + INDETERMINATE). Controller never disables L2/L3 hardware offload. Live capture still omits N1-05 projector membership (M1-22 seam). Deploy gating of these blockers is N1-06 (DONE).

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
| W1.3 catalog lists + Compose ← Node | `PoliciesViewModel` + MainWindow Policies | `Ac5bPoliciesBindCatalogListsAndComposeFromSelectedNode` + `PoliciesViewModelTests` |
| W3.6 Update/Delete/Ack/Compile | `UpdateRule` / `DeleteRule` / `AcknowledgeWarning` / `CompileNodeFilterArtifacts` | `Ac5cPoliciesMutateRulesAckWarningsAndCompile` + `PoliciesViewModelTests` + `PolicyDesktopServiceTests` |

**Residuals:** Typed `PolicyDocument.Tests` still opaque JSON text box. Full NODE_EFFECTIVE / per-device analysis hashes need device context — Desktop reuses logical-effective/content hash slots for `RecordAnalysisRun` wiring. Compile is semantic summary only (`CompileNodeFilterArtifacts`); capability hash is entered from Snapshots (not invented). Deploy button present with `CanExecute=false` (M4-12; packet-path Domain gate is N1-06 DONE). RouterOS writes remain out of scope (M4 / WriteEnabled).

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

**Residuals:** Explicit anchor placement planning is M5-04 (DONE). Live RouterOS discovery adapters remain later M5 steps. No gRPC/Desktop in M5-03.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingGuard"
```

## Living Specification — explicit anchor placement (M5-04)

Onboarding Spec §20–§21 / §58 + Issue Set M5-04 → Domain planner (no RouterOS writes, no auto-position):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Only BEFORE_STATIC_RULE and APPEND | `AnchorPlacementIntent` / `AnchorPlacementMode` | `Ac1OnlyBeforeStaticRuleAndAppendAreSupported` |
| AC#2 Dynamic cannot be reference | planner | `Ac2DynamicRuleCannotBeReference` |
| AC#3 Fingerprint + occurrence rank | `FilterRuleFingerprint` | `Ac3FingerprintAndOccurrenceRankAreFixed` |
| AC#4 Predecessor/successor context | freeze + revalidate | `Ac4PredecessorAndSuccessorContextAreChecked` |
| AC#5 Placement before guard forbidden | `ANCHOR_BEFORE_GUARD` | `Ac5PlacementBeforeGuardIsForbidden` |
| AC#6 After unconditional terminal blocked | `ANCHOR_UNREACHABLE` | `Ac6PlacementAfterUnconditionalTerminalIsBlocked` |
| AC#7 No automatic best-position | no Suggest/Auto | `Ac7AutomaticBestPositionSelectionIsAbsent` |
| AC#8 RouterOS `.id` not stored | fingerprint preimage | `Ac8RouterOsIdIsNotStored` |
| AC#9 Filter order change invalidates | `ANCHOR_PLACEMENT_STALE` | `Ac9FilterOrderChangeInvalidatesPlan` |
| AC#10 Exact before/after position | `AnchorPlacementPreview` | `Ac10DesktopPreviewExposesExactBeforeAfterPosition` |

**Residuals:** WinUI onboarding workflow / gRPC is M5-09. Bootstrap writer is M5-05 (DONE). No RouterOS writes in M5-04.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingAnchorPlacement"
```

## Living Specification — onboarding bootstrap writer (M5-05)

Onboarding Spec §23 / §27 + Issue Set M5-05 → closed writer (no generic `Mfc.RouterOs.Write`):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Compile-time allowlisted paths | `OnboardingWritePath` | `Ac1WritePathsAreCompileTimeAllowlisted` |
| AC#2 Single unconditional return | `OnboardingBootstrapWrite` | `Ac2BootstrapRootContainsExactlyOneUnconditionalReturn` |
| AC#3 Artifact ID matches Spec §23 | `BootstrapArtifact` | `Ac3BootstrapArtifactIdMatchesSpec` |
| AC#4 Anchor created disabled | writer add | `Ac4PermanentAnchorIsCreatedDisabled` |
| AC#5 Jump-target = bootstrap root | add attributes | `Ac5AnchorTargetIsBootstrapRoot` |
| AC#6 place-before or append | no `/move` | `Ac6PlaceBeforeOrAppendIsUsed` |
| AC#7 move unused | path enum | `Ac7MoveIsNotUsed` |
| AC#8 set only disabled | `/set` attributes | `Ac8SetAllowsOnlyAnchorDisabled` |
| AC#9 remove exact onboarding resources | remove + read-back | `Ac9RemoveAllowsOnlyExactOnboardingResources` |
| AC#10 Read-back after each write | `ApplyAsync` | `Ac10EachWriteHasActualStateReadBack` |
| AC#11 No generic command method | writer shape | `Ac11GenericCommandMethodIsAbsent` |
| AC#12 Namespace collision blocks | planner | `Ac12NamespaceCollisionBlocksOperation` |

**Residuals:** Live session adapter over `RosSession` can wrap `IOnboardingWriteChannel`. No gRPC/Desktop in M5-05. Scheduler proof / watchdog is M5-06 (DONE).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingBootstrapWriter"
```

## Living Specification — onboarding watchdog (M5-06)

Onboarding Spec §12 / §27.2 / §32–§36 + Issue Set M5-06 → closed proof + watchdog (no generic `Mfc.RouterOs.Write`):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Fixed no-op proof script | `SchedulerCapabilityProof` | `Ac1OneShotProofUsesFixedNoOpScript` |
| AC#2 run-count==1 | `OnboardingWatchdogWriter` | `Ac2RunCountMustEqualOne` |
| AC#3 Proof resources removed | writer remove + print | `Ac3ProofResourcesAreRemoved` |
| AC#4 Deadline + startup schedulers | `OnboardingWatchdogBundle` | `Ac4WatchdogHasDeadlineAndStartupSchedulers` |
| AC#5 Fixed watchdog template | `OnboardingWatchdogScript` | `Ac5ScriptSourceUsesFixedTemplate` |
| AC#6 dont-require-permissions=no | planner attributes | `Ac6DontRequirePermissionsIsNo` |
| AC#7 Disable exact bootstrap anchors only | `ShouldDisable` | `Ac7ScriptMayOnlyDisableExactBootstrapAnchors` |
| AC#8 No user input in script | literals / charset | `Ac8UserInputDoesNotEnterScript` |
| AC#9 Stale watchdog no-op | jump-target ≠ bootstrap | `Ac9StaleWatchdogIsNoOpForNonBootstrapTarget` |
| AC#10 Source hash after add | read-back hash | `Ac10SourceHashIsCheckedAfterAdd` |
| AC#11 TTL + commit margin | 60–600s / 30s | `Ac11TtlAndCommitMarginAreBounded` |
| AC#12 Collision blocks | `ONBOARDING_WATCHDOG_COLLISION` | `Ac12CollisionBlocksOperation` |

**Residuals:** Live `RosSession` wrapper for script/scheduler print can implement `PrintSystemAsync`. No gRPC/Desktop in M5-06.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingWatchdog"
```

## Living Specification — onboarding execution (M5-07)

Onboarding Spec §37–§43 + Issue Set M5-07 → stage / arm / enable / verify / disarm / commit:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Roots staged before anchors | `ExecuteOnboardingBootstrapUseCase` | `Ac1RootsAreStagedBeforeAnchors` |
| AC#2 Anchors staged disabled | writer + timeline | `Ac2AllAnchorsAreStagedDisabled` |
| AC#3 VRRP watchdogs armed first | timeline arm vs enable | `Ac3VrrpWatchdogsAreArmedBeforeFirstEnable` |
| AC#4 Normative enable order | `OnboardingEnableOrder` | `Ac4AnchorEnableOrderIsNormative` |
| AC#5 Enable read-back | `SetAnchorDisabled` | `Ac5EachEnableHasReadBack` |
| AC#6 Reconnect after management anchors | session reconnect | `Ac6NewApiConnectionOpensAfterManagementAnchors` |
| AC#7 Stable post-bootstrap capture | `CaptureStableAsync` | `Ac7StablePostBootstrapCaptureRuns` |
| AC#8 Unmanaged order unchanged | `OnboardingPassThroughEquivalence` | `Ac8UnmanagedRulesAndRelativeOrderAreUnchanged` |
| AC#9 NAT/RAW/Mangle/routing/VRRP frozen | `OnboardingAuxiliarySnapshot` | `Ac9NatRawMangleRoutingVrrpAreUnchanged` |
| AC#10 Pass-through equivalence | jump→return | `Ac10SemanticEquivalencePassThroughIsProven` |
| AC#11 Indeterminate → rollback pending | `BOOTSTRAP_SEMANTIC_EQUIVALENCE_NOT_PROVEN` | `Ac11IndeterminateEquivalenceStartsRollback` |
| AC#12 Watchdogs disabled before commit | `DisarmWatchdogAsync` | `Ac12WatchdogsAreDisabledBeforeDurableCommit` |
| AC#13 Node MANAGED only fully | Device then Node | `Ac13NodeBecomesManagedOnlyFully` |

**Residuals:** Onboarding API / Desktop workflow is M5-09. No gRPC/Desktop in M5-07.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingExecution"
```

## Living Specification — onboarding rollback (M5-08)

Onboarding Spec §44–§46 + Issue Set M5-08 → disable-first rollback, exact-resource remove, crash recovery:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Enabled anchors disabled first | `RollbackOnboardingBootstrapUseCase` | `Ac1EnabledAnchorsAreDisabledFirst` |
| AC#2 Reconnect after disable | session reconnect | `Ac2ManagementAccessIsCheckedAfterDisabling` |
| AC#3 Exact operation resources only | writer remove | `Ac3OnlyExactOperationResourcesAreRemoved` |
| AC#4 Roots after references | timeline | `Ac4BootstrapRootsAreRemovedAfterAnchorReferences` |
| AC#5 Watchdog cleanup idempotent | `CleanupWatchdogAsync` | `Ac5WatchdogResidueCleanupIsIdempotent` |
| AC#6 Nonterminal after restart | `RecoverOnboardingUseCase` | `Ac6NonterminalOperationIsRolledBackAfterRestart` |
| AC#7 Unexpected target | `ONBOARDING_UNEXPECTED_ANCHOR_TARGET` | `Ac7UnexpectedAnchorTargetRequiresRecovery` |
| AC#8 No automatic adoption | `OnboardingRecoveryDecision` | `Ac8AutomaticAdoptionIsAbsent` |
| AC#9 VRRP all members | dual sessions | `Ac9PartialVrrpOnboardingRollsBackAllMembers` |
| AC#10 No leftover enabled anchors | post-rollback print | `Ac10FailedOnboardingLeavesNoEnabledAnchors` |
| AC#11 Recovery decision table | Spec §46 | `Ac11RecoveryDecisionTableIsComplete` |

**Residuals:** Onboarding API / Desktop is M5-09. No gRPC/Desktop in M5-08.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingRollback"
```

## Living Specification — onboarding workflow (M5-09)

Issue Set M5-09 → separate RPCs, plan_hash at start, streaming progress, Desktop checklist/placement, no script source, exact recovery facts, mutation idempotency, audit:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Separate RPCs | `onboarding.proto` / `OnboardingGrpcService` | `Ac1SeparateRpcsExistOnTheContract` |
| AC#2 plan_hash required | `StartOnboardingUseCase` | `Ac2StartRequiresExactPlanHash` |
| AC#3 Server-streaming progress | `OnboardingProgressHub` | `Ac3WatchReplaysServerStreamingProgressUntilTerminal` |
| AC#4 Prerequisite checklist | Desktop `Findings` | `Ac4To7DesktopChecklistPlacementAndNoWriteSurface` |
| AC#5 Anchor placement | Desktop `Placements` | `Ac4To7DesktopChecklistPlacementAndNoWriteSurface` |
| W1.4 bind Placements | MainWindow Onboarding «Anchor placements» | `Ac6bOperationsShowsPlanCollectionsNotOnlyHashDelta` |
| AC#6 No script source | proto + `HasScriptSource` | `ContractHasNoScriptSourceOrArbitraryWriteSurface` |
| AC#7 No arbitrary writes | proto + `HasArbitraryWriteControls` | `Ac4To7DesktopFlagsAreCompileTimeFalse` |
| AC#8 Recovery facts exact | `GetOnboardingRecoveryStatusUseCase` | `Ac8RecoveryFactsMatchStoredOperation` |
| AC#9 Mutation idempotency | CreatePlan/Start/Rollback | `Ac9MutationRpcsAreIdempotent` |
| AC#10 All operations audited | workflow use cases | `Ac10EveryWorkflowOperationIsAudited` |

**Residuals:** Topology acceptance is M5-10 (DONE). Default `NotConfiguredOnboardingRuntime` does not fake RouterOS commits.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingWorkflow"
```

## Living Specification — onboarding integration acceptance (M5-10)

Issue Set M5-10 + Onboarding Spec §61–§64 → exact bootstrap/rollback on every MVP topology (isolated sessions; live CHR optional):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Standalone IPv4 | `ExecuteOnboardingBootstrapUseCase` | `Ac1StandaloneIpv4OnboardingCommitsFully` |
| AC#2 Dual-stack | IPv6 required set | `Ac2DualStackOnboardingCommitsIpv4AndIpv6Anchors` |
| AC#3 Multi-WAN states | Failover/Balanced/One | `Ac3MultiWanOperationalStatesDoNotMutateAuxiliary` |
| AC#4 VRRP active/passive | whole Node | `Ac4VrrpActivePassiveOnboardsEveryMember` |
| AC#5 VRRP split-master | role not an input | `Ac5VrrpSplitMasterRoleIsNotAnOnboardingInput` |
| AC#6 CRS INPUT/OUTPUT | Switch plan | `Ac6CrsInputOutputOnboardingOmitsForward` |
| AC#7 Switch FORWARD absent | dual-stack switch | `Ac7SwitchForwardAnchorIsAbsentIncludingDualStack` |
| AC#8 Scheduler-disabled / flagged | `OnboardingPrerequisiteValidator` | `Ac8SchedulerDisabledAndFlaggedDevicesAreBlocked` |
| AC#9 Deadline/startup rollback | watchdog fire + `RecoverOnboardingUseCase` | `Ac9DeadlineAndStartupWatchdogRollbackLeaveNodeUnmanaged` |
| AC#10 Crash after effectful phases | Spec §46 recover | `Ac10CrashAfterEachEffectfulPhaseLeavesNoPartialManagedNode` |
| AC#11 Guard + namespace collision | verifier + blocked staging | `Ac11GuardAndNamespaceCollisionsBlockWithoutManagedResidue` |
| AC#12 No partial managed Node | VRRP member reconnect fail | `Ac12FailedMemberLeavesWholeNodeUnmanaged` |
| Topology contracts + gRPC | testlab + `OnboardingService` | `OnboardingTopologyAcceptanceTests` |

**Residuals:** Live CHR matrix stays optional (`MFC_CHR_*`). Safe deploy plan/persistence is M4-01 (DONE). Default `NotConfiguredOnboardingRuntime` does not fake RouterOS commits.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~OnboardingIntegrationAcceptance"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~OnboardingTopologyAcceptance"
```

## Living Specification — deployment plan, states and persistence (M4-01)

Safe Deployment Spec §9–§16 + Issue Set M4-01 → Domain + EF (no RouterOS writer, no campaign, no gRPC/Desktop):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Target is one Node | `DeploymentPlan` / `DeploymentOperation` | `Ac1DeploymentTargetIsASingleNode` |
| AC#2 No campaign state | Domain types | `Ac2CampaignStateIsAbsent` |
| AC#3 Old/new artifacts + anchors | `DeviceDeploymentPlan` | `Ac3PlanContainsOldNewArtifactsAndAnchorTargets` |
| AC#4 Immutable plan + expiry | `DeploymentPlan` | `Ac4PlanIsImmutableAndBoundedByExpiry` |
| AC#5 Device plans cover members | VRRP cardinality | `Ac5DevicePlansCoverEveryMember` |
| AC#6 Activation/rollback order | reverse DeviceId order | `Ac6ActivationAndRollbackOrderAreFixed` |
| AC#7 Durable exclusive Node lock | `DeploymentLock` | `Ac7DurableNodeLockIsExclusive` |
| AC#8 Write-ahead step journal | `DeploymentStep` | `Ac8WriteAheadStepJournalIsOrdered` |
| AC#9 Invalid transition rejected | `DeploymentOperation` | `Ac9InvalidStateTransitionIsRejected` |
| AC#10 Completed deployment immutable | terminal freeze | `Ac10CompletedDeploymentIsImmutable` |
| AC#11 `NO_CHANGES` is terminal | PRECHECKING → NO_CHANGES | `Ac11NoChangesIsTerminalWithoutMutationPath` |
| AC#12 Plan hash preconditions | `DeploymentPlanHasher` | `Ac12PlanHashIncludesNormativePreconditions` |
| Persistence schema `m4-01` | migration `DeploymentSchemaM401` | `MigrateCreatesDeploymentTablesAndSchemaMetadata` |

**Residuals:** Probes are M4-07+. Packet-path deploy gate is N1-06 (DONE). Restricted writer is M4-02 (DONE). Address-list staging is M4-03 (DONE). Detached chain staging is M4-04 (DONE). Watchdog is M4-05 (DONE). Anchor activation is M4-06 (DONE).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeploymentLivingSpecTests"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~DeploymentPersistTests"
```

## Living Specification — packet-path deploy gate (N1-06)

next-1 + Safe Deployment PRECHECKING → BLOCKED + ROADMAP N1-06 → Domain gate (no RouterOS writer, no new RPC, no offload writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 HW offload blocks deploy | `PACKET_PATH_BYPASSES_IP_FIREWALL` | `Ac1HardwareOffloadBlocksDeployWithBypassesCode` |
| AC#2 INDETERMINATE blocks deploy | `PACKET_PATH_NOT_PROVEN` | `Ac2IndeterminateBlocksDeployWithNotProvenCode` |
| AC#3 CPU path allows start | no blocker | `Ac3CpuFirewallPathAllowsDeployStart` |
| AC#4 MIXED does not block | next-1 mapping | `Ac4MixedPathDoesNotBlockDeploy` |
| AC#5 Empty pairs on Router | fail-closed NOT_PROVEN | `Ac5EmptyPairsOnRouterAreNotProven` |
| AC#6 Switch no FORWARD proof | ignore HW pairs | `Ac6SwitchDoesNotRequireForwardPacketPathProof` |
| AC#7 VRRP is whole Node | any HW pair | `Ac7VrrpHardwareOffloadBlocksTheWholeNode` |
| AC#8 PRECHECKING → BLOCKED | no STAGING | `Ac8PacketPathBlockersFinishPrecheckAsBlockedWithoutStaging` |
| AC#9 Proven path does not auto-stage | stay CREATED | `Ac9ProvenPathAllowsPrecheckWithoutEnteringStaging` |
| AC#10 No offload writes / FailedPrecondition codes | Domain ↛ RouterOs | `Ac10GateDoesNotReferenceRouterOsOrOffloadWrites` |
| Canonical mapper path | `DeploymentPacketPathPrecheck` | `CanonicalHardwareOffloadBlocksDeployWithoutReclassification` |

**Residuals:** Activate / probes / gRPC Deploy are M4-06+. Desktop Deploy command stays `CanExecute=false` (no Save and Deploy). Controller never disables L2/L3 hardware offload.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeploymentPacketPath|FullyQualifiedName~DeploymentLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — restricted deployment writer (M4-02)

Safe Deployment Spec §6–§8 / §33.2 / §55 + Issue Set M4-02 → Application contracts + `Mfc.RouterOs.Deployment` (not `Mfc.RouterOs.Write`):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Compile-time allowlisted paths | `DeploymentWritePath` / `DeploymentWritePaths` | `Ac1WritePathsAreCompileTimeAllowlisted` |
| AC#2 Filter set = `.id` + `jump-target` | `SetAnchorTargetAsync` | `Ac2FilterSetAllowsOnlyAnchorJumpTarget` |
| AC#3 Ordinary rules not mutated via set | anchor ownership match | `Ac3OrdinaryActiveRulesAreNotChangedBySet` |
| AC#4 No `/move` | paths + typed write | `Ac4MoveIsNotUsed` |
| AC#5 No filter remove on deployment path | allowlist + interface | `Ac5FilterRemoveIsAbsentFromDeploymentPath` |
| AC#6 No address-list set/remove | allowlist + interface | `Ac6AddressListSetAndRemoveAreAbsent` |
| AC#7 Typed script + scheduler APIs | add/disable allowlist | `Ac7ScriptAndSchedulerApisAreTyped` |
| AC#8 Typed bounded ping | count=3; timeout bounds | `Ac8PingParametersAreTypedAndBounded` |
| AC#9 Lookup via print/read | `PrintAsync` before set | `Ac9ResourceLookupUsesPrintRead` |
| AC#10 `.id` session-only | `RouterOsItemId` | `Ac10ItemIdIsSessionScopedOnly` |
| AC#11 Every write has read-back | `DeploymentWriteExecutionResult.ReadBack` | `Ac11EachWriteHasReadBack` |
| AC#12 No generic writer | `RouterOsDeploymentSession` in Deployment | `Ac12GenericWriterIsAbsent` |

**Residuals:** Activate / probes / gRPC Deploy are M4-06+. No live RouterOS transport binding in this slice (channel is injectable).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeploymentWriterLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — address-list create-or-verify staging (M4-03)

Safe Deployment Spec §18 + Compiler Spec §26–§27 + Issue Set M4-03 → Domain planner + Application staging over M4-02 writer:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Exact list reuse | `AddressListCreateOrVerify` / `StageAddressListUseCase` | `Ac1ExistingExactListIsReused` |
| AC#2 Exact subset → add missing | `AddMissing` | `Ac2ExactSubsetIsSupplementedWithMissingEntries` |
| AC#3 Extra/divergent → collision | `STAGING_RESOURCE_COLLISION` | `Ac3ExtraOrDivergentEntryCreatesCollision` |
| AC#4 Unmanaged entry blocks | foreign comment | `Ac4UnmanagedEntryInGeneratedListBlocksStaging` |
| AC#5 No blind add retry | read-before-add | `Ac5BlindAddRetryAfterConnectionLossIsAbsent` |
| AC#6 Actual state before retry | `ReadBeforeWriteCount` | `Ac6ActualStateIsReadBeforeRetry` |
| AC#7 Unordered content hash | `TryVerifyContentHash` | `Ac7FinalUnorderedContentHashIsVerified` |
| AC#8 Dynamic/timeout blocks | `STAGING_RULE_INVALID` | `Ac8DynamicEntryInGeneratedListBlocksStaging` |
| AC#9 No in-place edit | no AL set/remove | `Ac9ActiveListsAreNotEditedInPlace` |
| AC#10 Record/payload limits | `AddressListCompileLimits` | `Ac10RecordAndPayloadLimitsAreApplied` |

**Residuals:** Probes / gRPC Deploy are M4-07+. Detached chain staging is M4-04 (DONE). Watchdog is M4-05 (DONE). Anchor activation is M4-06 (DONE).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~AddressListStagingLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — detached chain staging (M4-04)

Safe Deployment Spec §17 / §19 + Compiler Spec §26 + Issue Set M4-04 → Domain planner + Application staging over M4-02 writer:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Deny before root | `OrderForStaging` | `Ac1DenyChainsAreStagedBeforeRootChains` |
| AC#2 Exact chain reuse | `FilterChainCreateOrVerify` | `Ac2ExistingExactChainIsReused` |
| AC#3 Exact prefix → append suffix | `AppendSuffix` | `Ac3ExactDesiredPrefixIsExtendedWithSuffix` |
| AC#4 Other divergence → collision | `STAGING_*` | `Ac4AnyOtherDivergenceCreatesCollision` |
| AC#5 Unmanaged rule blocks | ownership comments | `Ac5UnmanagedRuleInGeneratedChainBlocksStaging` |
| AC#6 Rule order verified | ordinal prefix | `Ac6RuleOrderIsVerified` |
| AC#7 Disabled/invalid blocks | `STAGING_RULE_INVALID` | `Ac7DisabledOrInvalidRuleBlocksStaging` |
| AC#8 Active root not staging target | managed name gate | `Ac8ActiveRootChainIsNotUsedAsStagingTarget` |
| AC#9 Canonical chain hash | `HashChainContent` | `Ac9FinalCanonicalChainHashMatches` |
| AC#10 Partial ≠ STAGED | `ArtifactStaged=false` | `Ac10PartialArtifactDoesNotReceiveStaged` |
| AC#11 Reconnect create-or-verify | read-before-add | `Ac11StagingReconnectRecoversWithCreateOrVerify` |

**Residuals:** Probes / gRPC Deploy are M4-07+. Watchdog is M4-05 (DONE). Anchor activation is M4-06 (DONE).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DetachedChainStagingLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — production rollback watchdog (M4-05)

Safe Deployment Spec §22–§27 + Issue Set M4-05 → Domain planner/script + RouterOs writer over M4-02 session:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Script + deadline + startup | `DeploymentWatchdogBundle` | `Ac1WatchdogHasScriptDeadlineAndStartupSchedulers` |
| AC#2 Fixed template | `DeploymentWatchdogScript` | `Ac2ScriptUsesFixedTemplate` |
| AC#3 Old/new target set | `DecideRestore` | `Ac3ScriptChecksOldAndNewTargetSet` |
| AC#4 Unknown third target | Abort | `Ac4UnknownThirdTargetIsNotChanged` |
| AC#5 Stale later artifact | Abort | `Ac5StaleWatchdogDoesNotRollBackLaterArtifact` |
| AC#6 No user text | literal gate | `Ac6UserTextDoesNotEnterScript` |
| AC#7 dont-require-permissions=no | attributes | `Ac7DontRequirePermissionsIsNo` |
| AC#8 Source hash verified | arm read-back | `Ac8ScriptSourceHashIsVerified` |
| AC#9 TTL + commit margin | `DeploymentCodes` | `Ac9TtlAndCommitMarginAreBounded` |
| AC#10 All devices armed (VRRP) | `EnsureAllDevicesArmed` | `Ac10AllDeviceWatchdogsMustBeArmedBeforeVrrpActivation` |
| AC#11 Disable read-back | `DisarmWatchdogAsync` | `Ac11SchedulerDisablingHasReadBack` |
| AC#12 Cleanup idempotent | `CleanupWatchdogAsync` | `Ac12CleanupIsIdempotent` |

**Residuals:** Probes / gRPC Deploy are M4-07+. Anchor activation is M4-06 (DONE).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeploymentWatchdogLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — transition validation and anchor activation (M4-06)

Safe Deployment Spec §28–§31 + Issue Set M4-06 → Domain transition/order/decision + Application activation over M4-02 session:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 All intermediate old/new states | `TransitionStateValidator` | `Ac1AllIntermediateOldNewCombinationsAreAnalyzed` |
| AC#2 Unsafe state blocks plan | `TRANSITION_STATE_UNSAFE` | `Ac2UnsafeStateBlocksPlan` |
| AC#3 Management-critical last | `DeploymentAnchorOrder` | `Ac3ManagementCriticalAnchorsAreActivatedLast` |
| AC#4 Re-read before each set | `ActivateAnchorsUseCase` | `Ac4AnchorIsReReadBeforeEverySet` |
| AC#5 Target = old or new | `AnchorActivationPlanner` | `Ac5CurrentTargetMustEqualExpectedOldOrDesiredNew` |
| AC#6 Unknown target → recovery | `RECOVERY_REQUIRED` | `Ac6UnknownTargetStartsRecovery` |
| AC#7 Unknown set verified by read | Spec §31 classify | `Ac7UnknownSetResultIsVerifiedByRead` |
| AC#8 No blind set retry | controlled retry only if old | `Ac8BlindSetRetryIsAbsent` |
| AC#9 Sequential writes per Device | journal order | `Ac9WritesPerDeviceAreSequential` |
| AC#10 Watchdog margin after each | `MinCommitMargin` | `Ac10WatchdogMarginIsCheckedAfterEachAnchor` |
| AC#11 Journal intent + verified | `AnchorActivationJournalEntry` | `Ac11StepJournalRecordsIntentAndVerifiedResult` |

**Residuals:** Node coordinator / gRPC Deploy are M4-08+. Probes/verification are M4-07 (DONE).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~AnchorActivationLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — probes and post-activation verification (M4-07)

Safe Deployment Spec §32–§34 + Issue Set M4-07 → Domain integrity/probe gates + Application fresh-session verification:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Managed resource hash | `VerifyManagedResourceHash` | `Ac1ManagedResourceHashIsVerified` |
| AC#2 Active anchor targets | `VerifyActiveAnchors` | `Ac2ActiveAnchorTargetsAreVerified` |
| AC#3 New API-SSL connection | `IDeploymentFreshSessionFactory` | `Ac3OpensNewApiSslConnection` |
| AC#4 Old session insufficient | `ReferenceEquals` gate | `Ac4EstablishedSessionIsNotSufficient` |
| AC#5 Only API_SSL + ROUTER_PING | `DeploymentProbeKind` | `Ac5OnlyApiSslAndRouterPingAreSupported` |
| AC#6 No hostname | `ProbeHostnameForbidden` | `Ac6PingDoesNotAcceptHostname` |
| AC#7 Bounded count/timeout | `FixedPingCount` / timeout | `Ac7CountIntervalAndTimeoutAreBounded` |
| AC#8 Typed src/table/iface | `DeploymentProbe` | `Ac8SourceAddressTableAndInterfaceAreTyped` |
| AC#9 Critical FAIL/INCONCLUSIVE → rollback | `ClassifyCriticalProbeOutcome` | `Ac9CriticalFailOrInconclusiveTriggersRollback` |
| AC#10 Probe profile in plan hash | `DeploymentPlanHasher` | `Ac10ProbeProfileIsPartOfPlanHash` |
| AC#11 Watchdog readiness before commit | `VerifyWatchdogReadiness` | `Ac11WatchdogReadinessIsCheckedBeforeCommit` |

**Residuals:** Multi-WAN / VRRP / gRPC Deploy are M4-09+. Standalone coordinator is M4-08 (DONE).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeploymentVerificationLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — standalone Node deployment coordinator (M4-08)

Safe Deployment Spec §35 + Issue Set M4-08 → Domain policy + Application coordinator over M4-02…M4-07 ports:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Preconditions rechecked | `RecheckPreconditions` | `Ac1PreconditionsAreRechecked` |
| AC#2 NO_CHANGES no writes | `IsNoChanges` | `Ac2NoChangesPerformsNoWrites` |
| AC#3 Staging ≠ active cut-over | `stage:detached-only` | `Ac3StagingDoesNotCutOverActiveTraffic` |
| AC#4 Watchdog before activation | timeline order | `Ac4WatchdogIsArmedBeforeActivation` |
| AC#5 Verify fail → rollback | `RollbackAfterActivation` | `Ac5FailedVerificationTriggersRollback` |
| AC#6 Disarm before commit | timeline order | `Ac6WatchdogDisabledBeforeDurableCommit` |
| AC#7 Old artifact retained | `CommitSnapshot.OldArtifactHash` | `Ac7OldArtifactRemainsForRollback` |
| AC#8 Detached kept on failure | no remove | `Ac8NewDetachedArtifactIsNotRemovedOnFailure` |
| AC#9 Commit snapshot | `DeploymentCommitSnapshot` | `Ac9CommitSnapshotIsStored` |
| AC#10 Same artifact → NO_CHANGES | `IsNoChanges` | `Ac10RedeploySameArtifactReturnsNoChanges` |

**Residuals:** fault/security acceptance (M4-13). Multi-WAN/VRRP/rollback/API are DONE.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~StandaloneDeploymentLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — multi-WAN deployment verification (M4-09)

Safe Deployment Spec §36 + Issue Set M4-09 → Domain gates + Application use case (no forced failover / no routing writes):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Routing/NAT/RAW/Mangle recheck | `RecheckDependencyHashes` | `Ac1RoutingNatRawMangleHashesAreRechecked` |
| AC#2 Zone/interface-list recheck | `RecheckDependencyHashes` | `Ac2ZoneAndInterfaceListDependenciesAreRechecked` |
| AC#3 Active route ≠ artifact | `ArtifactHashIgnoringActiveRoute` | `Ac3ActiveRouteStateDoesNotChangeArtifact` |
| AC#4 Per-table ping (balanced) | `PlanRuntimeProbes` | `Ac4PerTablePingRequiredForBalanced` |
| AC#5 Active-path ping (failover) | `PlanRuntimeProbes` | `Ac5CurrentActivePathCheckedForFailover` |
| AC#6 No primary WAN disable | `RejectForbiddenOperationalIntents` | `Ac6ControllerDoesNotDisablePrimaryWan` |
| AC#7 No temporary route | `RejectForbiddenOperationalIntents` | `Ac7ControllerDoesNotCreateTemporaryRoute` |
| AC#8 No forced failover | `PlanRuntimeProbes` | `Ac8BackupPathNotTestedByForcedFailover` |
| AC#9 Dependency drift → rollback | `RecheckDependencyHashes` | `Ac9DependencyChangeBlocksOrRollsBack` |
| AC#10 No routing/NAT/Mangle writes | `EnsureFilterOnlyWriteSurface` | `Ac10ControllerDoesNotChangeRoutingNatMangle` |

**Residuals:** fault/security acceptance (M4-13).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~MultiWanDeploymentVerificationLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — VRRP deployment coordinator (M4-10)

Safe Deployment Spec §37–§42 + Issue Set M4-10 → Domain classification/order + Application pseudo-transaction coordinator:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 All members prechecked | `ExecuteVrrpDeploymentUseCase` | `Ac1AllMembersArePrechecked` |
| AC#2 Stage all before activation | timeline order | `Ac2AllArtifactsStagedBeforeActivation` |
| AC#3 All watchdogs armed | `EnsureAllDevicesArmed` | `Ac3AllWatchdogsArmedBeforeActivation` |
| AC#4 Standby-only first | `VrrpActivationOrderPlanner` | `Ac4StandbyOnlyMembersActivateFirst` |
| AC#5 Traffic-bearing last | `VrrpActivationOrderPlanner` | `Ac5TrafficBearingMembersActivateLast` |
| AC#6 Unknown → traffic-bearing | `VrrpMemberClassifier` | `Ac6UnknownRoleClassifiesAsTrafficBearing` |
| AC#7 RoleVector before each member | timeline | `Ac7RoleVectorIsReadBeforeEachMember` |
| AC#8 Role change → rollback | coordinator | `Ac8RoleChangeAfterFirstActivationTriggersRollback` |
| AC#9 Unreachable blocks | `EnsureAllMembersReachable` | `Ac9UnreachableMemberBeforeActivationBlocks` |
| AC#10 Partial activation rollback | `PlanPartialFailureActions` | `Ac10PartialActivationRollsBackReachableMembers` |
| AC#11 Unreachable keeps watchdog | `PlanPartialFailureActions` | `Ac11UnreachableMemberKeepsWatchdog` |
| AC#12 Split-master not simplified | `EnsureNoSplitMasterSimplification` | `Ac12SplitMasterIsNotSimplified` |
| AC#13 No partial commit | `EnsureFullCommitAllowed` | `Ac13PartialCommitIsImpossible` |

**Residuals:** fault/security acceptance (M4-13).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~VrrpDeploymentLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — deployment rollback and crash recovery (M4-11)

Safe Deployment Spec §46–§49 + Issue Set M4-11 → Domain decision table + Application rollback/recover use cases:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Reverse activation order | `DeviceRollbackOrder` | `Ac1RollbackUsesReverseActivationOrder` |
| AC#2 Anchor target old or new | `ClassifyAnchors` | `Ac2AnchorTargetMustBeOldOrNew` |
| AC#3 Old artifact hash verified | `ExecuteDeploymentRollbackUseCase` | `Ac3OldArtifactHashIsVerified` |
| AC#4 Fresh API-SSL after rollback | `ExecuteDeploymentRollbackUseCase` | `Ac4NewApiConnectionOpensAfterRollback` |
| AC#5 Old-state probes pass | `ExecuteDeploymentRollbackUseCase` | `Ac5OldStateProbesPass` |
| AC#6 Mixed → all-old | `ExecuteDeploymentRollbackUseCase` | `Ac6MixedOldNewCompletesToAllOld` |
| AC#7 Third target → RECOVERY_REQUIRED | `ExecuteDeploymentRollbackUseCase` | `Ac7ThirdTargetCreatesRecoveryRequired` |
| AC#8 Watchdog rollback recognized | `RecoverDeploymentUseCase` | `Ac8WatchdogRollbackIsRecognized` |
| AC#9 Nonterminal after restart → rollback | `RecoverDeploymentUseCase` | `Ac9NonterminalAfterRestartIsRolledBack` |
| AC#10 Crash after disarm before commit | `RecoverDeploymentUseCase` | `Ac10CrashAfterWatchdogDisableBeforeCommitRollsBack` |
| AC#11 Only durable COMMITTED keeps new | `MayRetainNewArtifact` / `Decide` | `Ac11OnlyDurableCommittedKeepsNewState` |
| AC#12 Recovery decision table complete | `DeploymentRecoveryDecision.Decide` | `Ac12RecoveryDecisionTableIsComplete` |

**Residuals:** fault/security acceptance (M4-13).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeploymentRollbackRecoveryLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```




## Living Specification — deployment workflow (M4-12)

Safe Deployment + Issue Set M4-12 → gRPC DeploymentService + Desktop Deploy panel:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Separate RPCs | `deployment.proto` | `Ac1SeparateRpcsExistOnTheContract` |
| AC#2 Start exact plan_hash | `StartDeploymentUseCase` | `Ac2StartRequiresExactPlanHash` |
| AC#3 Watch server-streaming | `DeploymentProgressHub` | `Ac3WatchReplaysServerStreamingProgressUntilTerminal` |
| AC#4–7 GUI surfaces | `DeploymentViewModel` | `Ac4To7DesktopSurfacesDiffArtifactsOrderProbesAndNoForceApply` |
| W1.4 bind plan collections | MainWindow Deploy lists (not hash-delta only) | `Ac6bOperationsShowsPlanCollectionsNotOnlyHashDelta` |
| AC#8 Cancel→rollback | `StartDeploymentUseCase` | `Ac8CancellationAfterActivationBecomesRollback` |
| AC#9 No ForceApply | proto contract | `Ac9ForceApplyAbsentFromContract` |
| AC#10 No raw ROS commands | Desktop flags | `Ac10NoRawRouterOsCommandsOnDesktop` |
| AC#11 Audit | workflow use cases | `Ac11EveryWorkflowOperationIsAudited` |

**Residuals:** CHR live acceptance (optional lab, not required for merge).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeploymentWorkflowLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — deployment fault and security acceptance (M4-13 / **M4 CLOSED**)

Issue Set M4-13 + Safe Deployment §55/§59–§62 → full fault-injection, watchdog/crash recovery, and security-boundary acceptance:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Standalone commit | `ExecuteStandaloneDeploymentUseCase` | `Ac1SuccessfulStandaloneDeploymentCommits` |
| AC#2 No writes for no-changes | `StandaloneDeploymentPolicy.NO_CHANGES` | `Ac2NoChangesPerformsNoWrites` |
| AC#3 Multi-WAN filter-only write surface | `VerifyMultiWanDeploymentUseCase` | `Ac3MultiWanFailoverAndBalancedProbesPass` |
| AC#4 VRRP active/passive full commit | `ExecuteVrrpDeploymentUseCase` | `Ac4VrrpActivePassiveCommitsAllMembers` |
| AC#5 VRRP split-master prevention | `VrrpDeploymentPolicy.EnsureNoSplitMasterSimplification` | `Ac5VrrpSplitMasterIsNotSimplified` |
| AC#6 Fault at effectful point → allowed terminal | `ExecuteStandaloneDeploymentUseCase` + `RecoverDeploymentUseCase` | `Ac6DisconnectAfterEffectfulPointsLeavesAllowedTerminal` (Theory) + `Ac6RecoverAfterCrashAtActivatingIsDeterministic` |
| AC#7 Deadline watchdog rollback recognized | `DeploymentRecoveryDecision` | `Ac7DeadlineWatchdogRollbackIsRecognized` |
| AC#8 Startup watchdog rollback recognized | `DeploymentRecoveryDecision` | `Ac8StartupWatchdogRollbackIsRecognized` |
| AC#9 Third-anchor target → RecoveryRequired | `DeploymentRecoveryDecision` | `Ac9ManualAnchorChangeCreatesRecoveryRequired` |
| AC#10 Crash recovery deterministic | `RecoverDeploymentUseCase` | `Ac10CrashRecoveryIsDeterministic` (Theory × 5) |
| AC#11 Credentials/scripts do not leak | proto descriptor + `DeploymentViewModel` + `DeploymentWatchdogScript` | `Ac11CredentialsAndScriptsDoNotLeak` |
| AC#12 Path/command injection impossible | `DeploymentWritePaths` + `ArchitectureBoundaryTests` | `Ac12ArbitraryCommandAndPathInjectionImpossible` |
| AC#13 Decision table only old/exact recovery | `DeploymentRecoveryDecision` | `Ac13OnlyOldCommittedOrExactRecoveryAllowed` |

**Harness:** `DeploymentAcceptanceHarness.cs` (sibling) — `FakeRuntime`, `RecordingChannel` (FailFilterSetsAfter), `ScriptedWatchdog`, `ScriptedCluster`, `ScriptedMember`, `ScriptedRollbackRuntime`, `NullFreshSessionFactory`.

**Residuals:** live CHR deployment acceptance (optional lab, not required for merge). **M4 CLOSED.**

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeploymentFaultSecurityAcceptanceLivingSpecTests|FullyQualifiedName~ArchitectureBoundary|FullyQualifiedName~DeploymentProtoContractTests"
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

## Living Specification — desired/committed/actual projection (M6-01)

Issue Set M6-01 + E2E Workflow Spec §7–§8 → derived Node workflow status from per-Device hashes:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Persist desired/committed/actual | `IDeviceHashStateStore` + Upsert/Get use cases | `Ac1PersistsDesiredCommittedAndActualHashes` |
| AC#2 SYNCHRONIZED | `DeviceHashStateClassifier` | `Ac2SynchronizedWhenDesiredCommittedAndActualMatch` |
| AC#3 PENDING ≠ drift | `DeviceHashStateClassifier` | `Ac3PendingDeploymentIsNotDrift` |
| AC#4 Actual divergence → DRIFTED | `DeviceHashStateClassifier` | `Ac4ActualDivergenceIsDrifted` |
| AC#5 Unknown anchor/actual → RECOVERY | `DeviceHashStateClassifier` | `Ac5UnknownAnchorOrActualIsRecoveryRequired` |
| AC#6 Status derived (not Node column) | `NodeEntity` + projector | `Ac6WorkflowStatusIsDerivedNotPersistedOnNodeEntity` |
| AC#7 Priority ordering | `NodeWorkflowStatusProjector` | `Ac7PriorityOrderingMatchesE2ESpec` |
| AC#8 VRRP keeps per-device rows | `NodeWorkflowStatusProjector` | `Ac8VrrpAggregatesWithoutDroppingPerDeviceState` |
| AC#9 Deterministic projection | `NodeWorkflowStatusProjector` | `Ac9ProjectionIsDeterministicAcrossInputPermutation` |
| AC#10 Desktop three hash states | `InventoryTreeItem` / `InventoryNodeViewModel` | `Ac10DesktopSurfacesDesiredCommittedAndActualHashes` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DeviceStateProjectionLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — managed drift detection (M6-02)

Issue Set M6-02 + E2E Workflow Spec §32–§34 → compare actual managed state to last committed artifact:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Baseline = last committed | `ManagedDriftDetector` | `Ac1BaselineIsLastCommittedArtifactNotDesired` |
| AC#2 Desired ≠ actual baseline | `ManagedDriftDetector` | `Ac2DesiredPolicyIsNotUsedAsActualBaseline` |
| AC#3 Managed rule → Critical | `DriftClassifier` | `Ac3ManagedRuleChangesAreCritical` |
| AC#4 Anchor → Critical | `DriftClassifier` | `Ac4AnchorChangesAreCritical` |
| AC#5 Guard / managed list → Critical | `DriftClassifier` | `Ac5GuardAndManagedListChangesAreCritical` |
| AC#6 Dependency config → Critical | `DriftClassifier` | `Ac6DependencyConfigurationChangesAreCritical` |
| AC#7 Observation VRRP/WAN/IF/counters ≠ config drift | `ManagedDriftDetector` | `Ac7ObservationOnlyVrrpWanInterfaceCountersAreNotConfigurationDrift` |
| AC#8 Semantic diff stored | `DetectManagedDriftUseCase` + store | `Ac8SemanticDiffIsStored` |
| AC#9 Drift blocks deploy | `DeploymentOperationGate` | `Ac9DriftBlocksNewDeployment` |
| AC#10 No automatic repair | Application/Domain surface | `Ac10AutomaticRepairIsAbsent` |
| AC#11 Restore via normal deploy only | API surface | `Ac11RestorationIsNormalDeploymentPathOnly` |
| AC#12 Immutable + audited | `DriftEvent` + audit | `Ac12DriftEventsAreImmutableAndAudited` |
| W1.5 Desktop findings list | `DriftEventListItem.Findings` (list RPC) | `Ac7bDriftShowsFindingsFromListResponseNotOnlySemanticDiff` + `DriftViewModelTests` |
| W3.7 GetDriftEvent detail | `DriftViewModel` selection → `GetDriftEvent` | `Ac7cDriftLoadsGetDriftEventForSelectedPayload` + `DriftViewModelTests` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ManagedDriftDetectionLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — bounded operational jobs (M6-03)

Issue Set M6-03 + E2E Workflow Spec §49–§50 → in-process bounded background jobs (no broker):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Queues bounded (reject when full) | `BoundedWorkBag` | `Ac1QueuesAreBoundedAndRejectWhenFull` |
| AC#2 Capture concurrency ≤ max (default 16) | `OperationalJobsOptions` / scheduler gates | `Ac2CaptureConcurrencyHonorsConfiguredMaxDefault16` |
| AC#3 One global drift poll config | `OperationalJobTickPlanner` | `Ac3DriftPollingUsesOneGlobalBoundedConfiguration` |
| AC#4 No per-device schedules | Options / Jobs surface | `Ac4NoPerDeviceComplexSchedules` |
| AC#5 Expired exception → zero RouterOS writes | `ReconcileExpiredExceptionBindingsJobUseCase` | `Ac5ExpiredExceptionPathHasZeroRouterOsWritePorts` |
| AC#6 Cleanup ≠ firewall artifacts | `WatchdogResidueCleanupPolicy` | `Ac6CleanupCannotDeleteFirewallArtifacts` |
| AC#7 Cleanup ≠ snapshots/audit | `WatchdogResidueCleanupPolicy` | `Ac7CleanupCannotDeleteSnapshotsOrAudit` |
| AC#8 Recovery priority > drift | `OperationalJobKind` / bag order | `Ac8RecoveryPriorityHigherThanDriftPolling` |
| AC#9 Shutdown cancels cleanly | `OperationalJobSchedulerHostedService` | `Ac9ShutdownCancelsJobsCleanly` |
| AC#10 No Hangfire/Quartz/broker | Architecture + HostedService | `Ac10NoMessageBrokerOrJobFramework` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~BoundedOperationalJobsLivingSpecTests|FullyQualifiedName~ArchitectureBoundary"
```

## Living Specification — Desktop MVP workflows (M6-04)

Issue Set M6-04 + E2E Workflow Spec §37–§43 → seven unified Desktop modules (Inventory, Node, Snapshots, Policies, Operations, Drift, Audit):

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Єдина навігаційна модель | `ShellNavigationModule` + `ShellViewModel.SelectedModule` | `Ac1SingleNavigationModelExposesExactlySevenModules` |
| AC#2 Inventory workflow status | `InventoryNodeViewModel.WorkflowStatusText` + MainWindow | `Ac2InventorySurfacesWorkflowStatusVisibly` |
| AC#2b Inventory Add Router wizard | `AddRouterWizardViewModel` + `IInventoryTreeClient` write RPCs | `Ac2bInventoryAddRouterWizardCoversCreateRegisterConnectionPath` + `AddRouterWizardViewModelTests` |
| W3.2 ValidateDeviceConnection Probe | Inventory/Add router Probe + `ProbeResultText` | `Ac2cInventoryAndAddRouterProbeValidateDeviceConnection` + `AddRouterWizardViewModelTests` |
| W4.3 VRRP create Node + two devices | Add router `CreateAsVrrpPair` → `NodeKind.Vrrp` + 2× RegisterDevice | `Ac2eAddRouterWizardCreatesVrrpNodeAndRegistersTwoDevices` + `AddRouterWizardViewModelTests` |
| CONT-02 Neighbor apply VRRP member b | Apply neighbor → `PairMemberB*` when pair mode | `Ac2fAddRouterNeighborApplyFillsVrrpMemberB` + `AddRouterWizardViewModelTests` |
| W3.5 Zones Update + Resolve device | `UpdateZoneCommand` / `ResolveDeviceCommand` | `Ac2dZonesEditDefinitionAndResolveDevice` + `ZonesViewModelTests` + `ZonesDesktopServiceTests` |
| Seed MikroTik neighbors (#314) | `ListNeighborCandidatesUseCase` + `/ip/neighbor` allowlist + Desktop Load/Apply | `NeighborCandidatesLivingSpecTests` + `ListNeighborCandidatesUseCaseTests` + `NeighborDiscoveryAllowlistTests` |
| AC#3 Node topology/zones/onboarding/readiness | `NodeDetailViewModel` | `Ac3NodeViewContainsTopologyZonesOnboardingAndReadiness` |
| W1.6 Inventory/Node device fields | reachability/model/ROS/VRRP(when present)/last snapshot | `Ac3bInventoryAndNodeShowExplicitDeviceFields` + `InventoryNodeViewModelTests` + `NodeDetailViewModelTests` |
| W6-05 GetNode Reachability from probe | LastSupportState → Reachable; Unreachable observation; Probe refresh | `DeviceReachabilityProjectorTests` + `Ac2eInventoryProbeRefreshesTreeAfterValidateDeviceConnection` |
| W6-08 Durable Unreachable | LastObservedReachability on Device; GetNode without in-memory store | `DiscoverDevicePersistsUnreachableAcrossEmptyObservationStore` + projector durable tests |
| W6-09 Policies Move up/down reorder | SelectedRule → ReorderRulesInStage; no UUID paste | `Ac5iPoliciesReorderMovesSelectedRuleWithoutUuidPaste` + `MoveRuleDownBuildsStageOrderWithoutUuidPaste` |
| W6-06 Policies typed Diff rows | KindText/DetailText DiffRows; DiffLines secondary | `Ac5gPoliciesRevisionDiffBindsTypedKindDetailRows` + `PolicyDesktopServiceTests` |
| W6-07 Policies Diff baseline catalog | DiffBaselineCatalogItem → baseline UUID; no LoadRevision | `Ac5hPoliciesDiffBaselinePicksFromCatalogWithoutUuidRitual` + `PoliciesViewModelTests` |
| W3.4 GetNodeWorkflow | Node `WorkflowDeviceLines` + canonical readiness | `Ac3cNodeLoadsGetNodeWorkflowInsteadOfAdHocReadinessMashup` + `NodeDetailViewModelTests` |
| W4.1 VRRP Node members table | Node a/b members: role / mgmt host / last capture | `Ac3dVrrpNodeShowsMemberTableRoleHostAndLastCapture` + `NodeDetailViewModelTests` + `InventoryTreeServiceTests` + `InventoryNodeViewModelTests` |
| AC#4 Snapshot configuration/observations | `SnapshotViewerViewModel` | `Ac4SnapshotViewShowsConfigurationAndObservations` |
| W1.1 Diff FieldLines + Warnings | `SnapshotDiffViewModel` + MainWindow Semantic diff | `Ac4bSemanticDiffShowsFieldLinesAndWarnings` + `SnapshotDiffServiceTests` |
| W2.1 Diff Before/After + warning truncate | selected entry record sides; VisibleWarnings cap 12 | `Ac4fSemanticDiffShowsBeforeAfterRecordsAndTruncatesWarnings` + `SnapshotDiffServiceTests` + `SnapshotDiffViewModelTests` |
| W1.2 Snapshot record Fields | `SnapshotViewerViewModel` selected-record detail + `Fields.DisplayLine` | `Ac4cSnapshotRecordDetailShowsAllFields` + `SnapshotViewerServiceTests` |
| W3.1 StartCapture + WatchCapture | Snapshots Capture button + progress | `Ac4dSnapshotCaptureStartsAndWatchesProgress` + `SnapshotViewerViewModelTests` |
| W4.4 VRRP pair capture / compare guidance | per-member Capture; Compare shows why a-against-b is forbidden | `Ac4eVrrpPairCaptureIsPerMemberAndCompareShowsCrossDeviceForbidWhy` + `SnapshotViewerViewModelTests` + `SnapshotDiffViewModelTests` + `InventoryOpsSelectionTests` |
| AC#5 Policy authoring/review/binding | `PoliciesViewModel` | `Ac5PolicyViewSupportsAuthoringReviewAndBinding` |
| W1.3 Policies catalog lists + Compose selection | `PoliciesViewModel` Address/Service/Contracts/`DiffLines` + Compose ← Node | `Ac5bPoliciesBindCatalogListsAndComposeFromSelectedNode` + `PoliciesViewModelTests` |
| W3.6 Policy Update/Delete/Ack/Compile | `UpdateRuleCommand` / `DeleteRuleCommand` / `AcknowledgeWarningCommand` / `CompileCommand` | `Ac5cPoliciesMutateRulesAckWarningsAndCompile` + `PoliciesViewModelTests` + `PolicyDesktopServiceTests` |
| W5-01 ListPolicies catalog browse | `ListPolicies` RPC + catalog select → LoadRevision | `Ac5dPoliciesCatalogBrowseListPoliciesThenSelectLoadsRevision` + `ListPoliciesUseCaseTests` + `PoliciesViewModelTests` + `PolicyDesktopServiceTests` |
| W5-02 ManagementPath / FastTrack Desktop | `GetDevicePolicySafetyAnalysis` RPC + hashes/findings/witnesses bind | `Ac5ePoliciesShowManagementPathAndFastTrackAnalysis` + `GetDevicePolicySafetyAnalysisUseCaseTests` + `PoliciesViewModelTests` + `PolicyDesktopServiceTests` |
| W6-01 operator-readable Diff/Snapshot + captured filter | fingerprint not list identity; default `firewall.ipv4.filter`; empty catalog hint | `Ac4gOperatorReadableDiffAndFirewallSectionDefault` + `Ac5fEmptyPolicyCatalogPointsAtCapturedFilter` + `SnapshotPresentationIdentityTests` + `SnapshotDiffServiceTests` |
| W6-02 VRRP pair consistency | last captures + logical FW; Node UI; CreatePlan/Validate gates | `VrrpPairConsistencyAnalyzerTests` + Inventory `ValidateVrrpPairConsistency` |
| W5-03 Typed deploy semantic policy diff | `semantic_diff` kind/path/before/after; hash delta secondary | `Ac6fDeploymentPlanBindsTypedSemanticDiffRows` + `DeploymentWorkflowLivingSpecTests` + `DeploymentViewModelTests` + `DeploymentProtoContractTests` |
| AC#6 Operations onboarding/deploy/recovery | Onboarding + Deployment VMs | `Ac6OperationsViewSupportsOnboardingDeploymentAndRecovery` |
| W1.4 Deploy/Onboarding plan collections | `ArtifactLines` / `OrderLines` / `ProbeAndWatchdogLines` / `Placements` | `Ac6bOperationsShowsPlanCollectionsNotOnlyHashDelta` |
| W3.3 Onboarding/Deploy Watch | Start + Watch → `ProgressLines` | `Ac6cOperationsStartWatchesOnboardingAndDeploymentProgress` + `OnboardingViewModelTests` + `DeploymentViewModelTests` |
| CONT-01 Deployment Rollback Watch | Rollback + Watch → `ProgressLines` | `Ac6eDeploymentRollbackWatchesProgress` + `DeploymentViewModelTests` + `Ac3bWatchReplaysRollbackEventsAfterCommittedTerminal` |
| W6-04 Onboarding Rollback Watch | Rollback + Watch → `ProgressLines`; hub replay past Committed | `Ac6gOnboardingRollbackWatchesProgress` + `OnboardingViewModelTests` + Onboarding `Ac3bWatchReplaysRollbackEventsAfterCommittedTerminal` |
| W5-03 Typed deploy semantic diff | `SemanticDiffRows` kind/path/before/after; `SemanticDiffLines` secondary | `Ac6fDeploymentPlanBindsTypedSemanticDiffRows` + `DeploymentViewModelTests` |
| W4.2 VRRP ops not silent first Device | Create plan / Validate all Node members | `Ac6dOperationsTargetVrrpNodePairNotSilentFirstDevice` + `InventoryOpsSelectionTests` + `DeploymentViewModelTests` + `OnboardingViewModelTests` |
| AC#7 Drift без automatic fix | `DriftViewModel` + `DriftService` | `Ac7DriftViewHasNoAutomaticFix` |
| W1.5 Drift findings зі list | `DriftEventListItem.Findings` + `SelectedEventFindings` | `Ac7bDriftShowsFindingsFromListResponseNotOnlySemanticDiff` + `DriftViewModelTests` |
| W3.7 Drift GetDriftEvent | selection loads full hashes / desired / semantic_diff_hash | `Ac7cDriftLoadsGetDriftEventForSelectedPayload` + `DriftViewModelTests` |
| AC#8 Audit read-only | `AuditViewModel` + `AuditService` | `Ac8AuditIsReadOnly` |
| AC#9 UI thread без remote I/O | Drift/Audit/Shell `Task.Run` | `Ac9UiThreadNeverPerformsRemoteIo` |
| AC#10 Cached state позначений | Inventory Cached badge | `Ac10CachedStateIsClearlyMarked` |
| AC#11 Desktop без RouterOS/SQL | Contracts-only refs | `Ac11DesktopHasNoRouterOsOrSqlDependencies` |
| AC#12 Keyboard + virtualization | KeyBindings + VirtualizingStackPanel | `Ac12KeyboardNavigationAndLargeListVirtualization` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DesktopMvpWorkflowsLivingSpecTests|FullyQualifiedName~AddRouterWizardViewModelTests|FullyQualifiedName~PoliciesViewModelTests|FullyQualifiedName~PolicyDesktopServiceTests|FullyQualifiedName~DriftViewModelTests|FullyQualifiedName~InventoryNodeViewModelTests|FullyQualifiedName~NodeDetailViewModelTests|FullyQualifiedName~InventoryOpsSelectionTests|FullyQualifiedName~SnapshotViewerViewModelTests|FullyQualifiedName~OnboardingViewModelTests|FullyQualifiedName~DeploymentViewModelTests|FullyQualifiedName~ZonesViewModelTests|FullyQualifiedName~ZonesDesktopServiceTests|FullyQualifiedName~ArchitectureBoundary|FullyQualifiedName~DriftProtoContractTests|FullyQualifiedName~AuditProtoContractTests"
```

## Living Specification — standalone / dual-stack E2E (M6-05)

Issue Set M6-05 + E2E Workflow Spec §53–§54. Live CHR matrix OFF — scripted/fake runtimes + in-process Controller + Postgres (same pattern as M5-10 / M4-13).

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Inventory→capture→onboarding→policy→deploy | Onboarding + `ExecuteStandaloneDeploymentUseCase` + Integration host | `Ac1InventoryOnboardingPolicyDeploymentEndToEnd` + `InventoryCaptureOnboardingSucceedsForStandaloneAndDualStack` |
| AC#2 Management reconnect | `VerifyDeploymentActivationUseCase` fresh API-SSL | `Ac2ManagementReconnectSucceeds` |
| AC#3 IPv4 / IPv6 artifacts independent | dual-stack plan + address-list staging | `Ac3Ipv4AndIpv6ArtifactsAreIndependent` |
| AC#4 IPv6 failure rolls back Node | `FailIpv6FilterSets` harness | `Ac4Ipv6FailureRollsBackNodeDeployment` |
| AC#5 Repeated deploy → NO_CHANGES | `StandaloneDeploymentPolicy` | `Ac5RepeatedDeploymentReturnsNoChanges` |
| AC#6 Manual managed-rule → Critical drift | `DetectManagedDriftUseCase` | `Ac6ManualManagedRuleChangeCreatesDrift` |
| AC#7 Restoration deployment | clear drift + standalone commit | `Ac7RestorationDeploymentWorks` |
| AC#8 Exception expiry → pending, no ROS write | `ExpireExceptionBindingUseCase` | `Ac8ExceptionExpirationCreatesPendingDeploymentWithoutRouterOsWrite` |
| AC#9 Audit reproduces lifecycle | `ListAuditEventsUseCase` | `Ac9AuditFullyReproducesLifecycle` |
| AC#10 Restart in each nonterminal phase | `RecoverDeploymentUseCase` | `Ac10ControllerRestartInEachNonterminalPhaseIsHandled` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~StandaloneDualStackE2ELivingSpecTests"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~StandaloneDualStackE2EAcceptanceTests"
```

## Living Specification — multi-WAN E2E (M6-06)

Issue Set M6-06 + E2E Workflow Spec §55–§56. Live CHR matrix OFF — scripted/fake runtimes (same pattern as M6-05 / M4-13). Inventory/capture multi-WAN slice remains `MultiWanVerticalSliceAcceptanceTests` (`--Mfc:OperationalJobs:Enabled=false`).

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Failover primary active | `VerifyMultiWanDeploymentUseCase` + standalone filter deploy | `Ac1FailoverWithPrimaryActiveSucceeds` |
| AC#2 Failover backup active | active-path probe selection | `Ac2FailoverWithBackupActiveSucceeds` |
| AC#3 Artifact identical across operational states | `ArtifactHashIgnoringActiveRoute` | `Ac3ArtifactIdenticalForBothOperationalStates` |
| AC#4 PCC topology succeeds | Balanced tables + PCC facts | `Ac4PccTopologySucceeds` |
| AC#5 Per-table probes | Mixed/Balanced `PlanRuntimeProbes` | `Ac5PerTableProbesSucceed` |
| AC#6 FastTrack unsafe blocked | `FastTrackAnalysis` PCC/balanced/mixed | `Ac6FastTrackUnsafeCaseIsBlocked` |
| AC#7 Routing/NAT/Mangle unchanged | `EnsureFilterOnlyWriteSurface` + write allowlist | `Ac7RoutingNatMangleAreNotChanged` |
| AC#8 Forced failover not performed | `MultiWanForcedFailoverForbidden` + reflection | `Ac8ForcedFailoverIsNotPerformed` |
| AC#9 Dependency change voids plan | `RecheckDependencyHashes` RequiresRollback | `Ac9DependencyChangeVoidsOrCancelsThePlan` |
| AC#10 Active route ≠ configuration drift | `ManagedDriftDetector` + `ActiveWanChanged` | `Ac10ActiveRouteChangeDoesNotCreateConfigurationDrift` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~MultiWanE2ELivingSpecTests"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~MultiWanVerticalSliceAcceptanceTests"
```

## Living Specification — VRRP / CRS E2E (M6-07)

Issue Set M6-07. Live CHR / live physical CRS OFF — scripted/fake runtimes + deterministic CRS lab fixtures (same pattern as M6-05 / M6-06). Inventory/capture VRRP slice remains `VrrpVerticalSliceAcceptanceTests` (`--Mfc:OperationalJobs:Enabled=false`). AC#11 documents scripted DoD for physical CRS hardware fixture while CHR remains OFF.

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 VRRP active/passive lifecycle | onboard + `ExecuteVrrpDeploymentUseCase` | `Ac1VrrpActivePassiveLifecycleSucceeds` |
| AC#2 VRRP split-master lifecycle | `VrrpDeploymentPolicy` + deploy fail-closed | `Ac2VrrpSplitMasterLifecycleSucceeds` |
| AC#3 All members onboard together | `ExecuteOnboardingBootstrapUseCase` | `Ac3AllMembersOnboardTogether` |
| AC#4 All members deploy together | stage/arm-all before activate; `commit:all` | `Ac4AllMembersDeployTogether` |
| AC#5 Role change after activation → rollback | ScriptedMember role flip | `Ac5RoleChangeAfterActivationTriggersRollback` |
| AC#6 Partial commit impossible | `EnsureFullCommitAllowed` + happy path | `Ac6PartialCommitIsImpossible` |
| AC#7 Physical management addresses | `ManagementPathAnalysis` VIP-only gate | `Ac7PhysicalManagementAddressesAreUsed` |
| AC#8 CRS INPUT/OUTPUT lifecycle | Switch onboard + standalone deploy | `Ac8CrsInputOutputLifecycleSucceeds` |
| AC#9 CRS FORWARD rejected | topology + `DeviceFilterCompiler` Switch gate | `Ac9CrsForwardPolicyIsRejected` |
| AC#10 Bridge/VLAN/HW offload unchanged | `DeploymentWritePaths` allowlist | `Ac10BridgeVlanHardwareOffloadAreNotChanged` |
| AC#11 Physical CRS hardware fixture | `crs-switch` topology + sanitized CRS fixture + `BoardClass.Crs` | `Ac11PhysicalCrsHardwareFixtureSucceeds` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~VrrpCrsE2ELivingSpecTests"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~VrrpVerticalSliceAcceptanceTests"
```

## Living Specification — security / backup / restore (M6-08)

Issue Set M6-08 / E2E Spec §47 + §52. No live CHR; Development master key only. AC 1–10 are pure/domain/desktop/reflection Living Spec; AC 11–14 use Integration Postgres fixture with in-container `pg_dump`/`pg_restore` (`--Mfc:OperationalJobs:Enabled=false`).

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Invalid CA / SAN / SPKI rejected | `ApiSslCertificateValidator` | `Ac1InvalidCaSanAndSpkiAreRejected` |
| AC#2 Plain API blocked | `AuthenticatedRosConnection` + `OnboardingPrerequisiteValidator` | `Ac2PlainApiIsBlocked` |
| AC#3 Default RouterOS group rejected | `OnboardingPrerequisiteValidator` | `Ac3DefaultRouterOsGroupIsRejected` |
| AC#4 Desktop never receives credentials | `ConnectionProfileView` + Desktop refs + proto | `Ac4DesktopNeverReceivesCredentials` |
| AC#5 DB no plaintext credentials | `EncryptedSecretEntity` | `Ac5DbEntityHasNoPlaintextCredentials` (+ Integration ciphertext) |
| AC#6 Logs/audit contain no secrets | redaction + audit payload forbid | `Ac6LogsAndAuditContainNoSecrets` |
| AC#7 RBAC bypass impossible | `ListAuditEventsUseCase` + `DenyAllAuthorizationBoundary` | `Ac7RbacBypassIsImpossible` |
| AC#8 Path injection impossible | `DeploymentWritePaths` + RouterOs namespaces | `Ac8ArbitraryRouterOsPathInjectionIsImpossible` |
| AC#9 Script injection impossible | Deployment proto + watchdog script | `Ac9ScriptInjectionIsImpossible` |
| AC#10 Audit tampering detected | hash-chain preimage (+ Bootstrap update/delete) | `Ac10AuditTamperingIsDetected` |
| AC#11 PostgreSQL backup/restore | `PostgresFixture.DumpAndRestoreAsync` | `Ac11PostgresBackupRestoreSucceeds` |
| AC#12 Snapshots hash verification | `BrotliPayloadCodec.DecodeAndVerify` | `Ac12SnapshotsAfterRestorePassHashVerification` |
| AC#13 Active artifact references | `device_hash_states` → `filter_artifacts` | `Ac13ActiveArtifactReferencesAreRestored` |
| AC#14 Nonterminal ops → recovery | `RecoverDeploymentUseCase` after restore | `Ac14NonterminalOperationsAfterRestoreGoThroughRecovery` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~SecurityBackupRestoreLivingSpecTests"
dotnet test tests/Mfc.IntegrationTests -c Release --filter "FullyQualifiedName~SecurityBackupRestoreAcceptanceTests"
```

## Living Specification — MVP production acceptance (M6-09)

Issue Set M6-09. **M6 CLOSED**. Live CHR / live physical CRS OFF — E2E Living Specs (M6-05…M6-07 + N1-07) are the DoD substitute; live lab is optional residual only. No git release tag in this PR (AC16).

| AC / вимога | Модуль | Тест / артефакт |
|-------------|--------|-----------------|
| AC#1 M0–M6 issues closed | ROADMAP / ISSUES / `mvp-acceptance.md`; §3.C NEXT = SEC-05 | `Ac1M0ThroughM6IssuesAreClosedInRoadmap` |
| AC#2 Release gates executed | `docs/release/release-gates.md` | `Ac2ReleaseGatesChecklistExists` |
| AC#3 CHR matrix green | E2E Living Specs (substitute) | `Ac3ChrMatrixSubstitutedByE2ELivingSpecs` |
| AC#4 Physical CRS green | `VrrpCrsE2E` + `crs-switch` | `Ac4PhysicalCrsSubstitutedByScriptedFixture` |
| AC#5 Fault-injection green | FaultInjection suites | `Ac5FaultInjectionSuiteExists` |
| AC#6 Security suite green | M6-08 Living Spec | `Ac6SecuritySuiteExists` |
| AC#7 Backup/restore green | M6-08 Integration | `Ac7BackupRestoreSuiteExists` |
| AC#8 Dependency scan | `run-dependency-scan.sh` + CI | `Ac8DependencyScanPolicyAndScriptExist` |
| AC#9 Controller package | `package-controller.sh` | `Ac9ControllerPackageCreatedInDryRun` |
| AC#10 Desktop installer | `package-desktop.sh` (zip/tar) | `Ac10DesktopInstallerCreatedInDryRun` |
| AC#11 Migration bundle | `create-migration-bundle.sh` | `Ac11MigrationBundleCreatedInDryRun` |
| AC#12 SBOM + SHA-256 | `generate-sbom-and-checksums.sh` | `Ac12SbomAndSha256ChecksumsCreatedInDryRun` |
| AC#13 Signed artifacts | `SHA256SUMS` + `RELEASE_SIGNING.md` | `Ac13ReleaseArtifactsSignedViaChecksumAttestation` |
| AC#14 Known limitations | `known-limitations.md` | `Ac14KnownLimitationsMatchActualScope` |
| AC#15 Clean work tree | script isolation + CI gate | `Ac15PackagingDoesNotDirtyGitWorkTree` |
| AC#16 Tag after review | docs; no `git tag` in scripts | `Ac16ReleaseTagOnlyAfterAcceptanceReview` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~MvpReleaseAcceptanceLivingSpecTests"
```

## Living Specification — path-class E2E / drift (N1-07 / **MVP CLOSED**)

Issue Set N1-07 + Living Spec `PathClassE2EDriftLivingSpecTests`. **MVP CLOSED**. Live CHR matrix OFF — scripted topology/analysis/drift fixtures only.

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Topology Container→VETH→Bridge→VLAN→VRF | `PacketPathTopologyDiscovery` | `Ac1TopologyGraphPathClassesAreProven` |
| AC#2 Published service path analyzed / fail-closed | `TopologyDependencyAnalysis` + `PacketPathAnalysis` + deploy gate | `Ac2PublishedContainerServicePathAnalyzedOrFailClosed` |
| AC#3 Container egress path analyzed | NAT srcnat facts + packet-path pairs | `Ac3ContainerEgressPathIsAnalyzed` |
| AC#4 No 1:1 / bridge≠firewall assumptions | shared VETH + topology flags | `Ac4OneToOneAndBridgeFirewallAssumptionsAreRejected` |
| AC#5 Container running = observation | `ManagedDriftDetector` + `ContainerRunningStateChanged` | `Ac5ContainerRunningStateIsObservationNotConfigurationDrift` |
| AC#6 Path-class config Critical + void readiness | `PathClassConfigDriftVoiding` + deploy gate | `Ac6PathClassConfigChangesAreCriticalAndVoidReadiness` |
| AC#7 Observation fields ≠ config drift | VETH/bridge-port/HW-offload/active route | `Ac7PathClassObservationsDoNotCreateConfigurationDrift` |
| AC#8 No path-class write APIs | ArchitectureBoundary + `DeploymentWritePaths` | `Ac8ControllerHasNoPathClassWriteApis` |
| AC#9 Packet-path blockers fail-close | `DeploymentPacketPathGate` | `Ac9PacketPathBlockersFailCloseDeployment` |
| AC#10 Zone `container:`/`app:` markers | `ZoneResolveEngine` | `Ac10ZoneResolveContainerAppMarkersWork` |
| AC#11 Critical drift blocks deploy | M6-02 `DeploymentOperationGate` | `Ac11PathClassCriticalDriftBlocksNewDeployment` |
| AC#12 Deterministic / no live CHR | Living Spec source + `testing.md` | `Ac12DeterministicLivingSpecNoLiveChr` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~PathClassE2EDriftLivingSpecTests"
```

## Living Specification — route resolution trace (M7.1-03)

Issue Set M7.1-03 / Network Rule M7.1 Spec §4–§9. Deterministic scripted fixtures only; no live CHR; no routing writes.

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Main table forward route | `RouteResolutionTraceEngine` | `Ac1MainTableForwardRouteResolvesNextHop` |
| AC#2 Policy rule LOOKUP → non-main table | routing rule matching | `Ac2RoutingRuleLookupSelectsNonMainTable` |
| AC#3 Routing mark from probe | mangle mark + rule/table selection | `Ac3RoutingMarkFromProbeSelectsMarkedTable` |
| AC#4 DROP / UNREACHABLE rule actions | `RoutingRuleActions` | `Ac4DropAndUnreachableRuleDecisions` |
| AC#5 Recursive gateway chain | `RecursiveResolutionStep` | `Ac5RecursiveGatewayResolutionChain` |
| AC#6 ECMP ONE_OF / INDETERMINATE | equal-cost next hops | `Ac6EcmpReturnsOneOfSetWithIndeterminateCertainty` |
| AC#7 NO_ROUTE on LOOKUP_ONLY miss | `LOOKUP_ONLY` | `Ac7NoRouteWhenLookupOnlyFails` |
| AC#8 LOCAL_DELIVERY connected | connected route kind | `Ac8LocalDeliveryForConnectedRoute` |
| AC#9 Persistence round-trip | upsert + `FakeRoutingAssuranceStateStore` | `Ac9PersistenceRoundTripStoresResolutionTraces` |
| AC#10 No routing writes | `RosReadCommandRegistry` | `Ac10NoRoutingWriteApisOpened` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~RouteResolutionTraceLivingSpecTests"
```

## Living Specification — ECMP ONE_OF bounded next-hop sets (M7.1-04)

Issue Set M7.1-04 / Network Rule M7.1 Spec §9. Deterministic scripted fixtures only; no live CHR; no routing writes.

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Multi-path ECMP populates `EcmpRouteSet` | `EcmpRouteSetBuilder` | `Ac1MultiPathEcmpPopulatesEcmpRouteSetFields` |
| AC#2 `next_hops` align with `ImmediateNextHops` ONE_OF | trace + set correlation | `Ac2NextHopsAlignWithImmediateOneOfHops` |
| AC#3 Inactive equal-cost route excluded | operational `Active` flag | `Ac3InactiveEqualCostRouteExcludedFromEcmpSet` |
| AC#4 Partial HW offload subset + MIXED path | `HardwareOffloadedNextHops` | `Ac4PartialHardwareOffloadListsSubsetAndMixedExecutionPath` |
| AC#5 Deterministic `hashing_context` flow-key shell | `EcmpHashingContext.FlowKeyMaterial` | `Ac5HashingContextDeterministicFromQueryFields` |
| AC#6 Single-hop convention (`EcmpRouteSet` null) | non-ECMP forward | `Ac6SingleHopForwardLeavesEcmpRouteSetNull` |
| AC#7 ONE_OF selector + INDETERMINATE certainty | ECMP > 1 | `Ac7EcmpUsesOneOfSelectorAndIndeterminateCertainty` |
| AC#8 Persistence round-trip | `ResolutionTracesJson` | `Ac8PersistenceRoundTripIncludesEcmpRouteSet` |
| AC#9 No routing writes | `RosReadCommandRegistry` | `Ac9NoRoutingWriteApisOpened` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~EcmpRouteSetLivingSpecTests"
```

## Living Specification — dynamic route origins read-only (M7.1-05)

Issue Set M7.1-05 / Network Rule M7.1 Spec §10. Deterministic scripted fixtures only; no live CHR; no routing writes; full BGP table never loaded.

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Static configured route → STATIC | `RouteOriginClassifier` | `Ac1StaticConfiguredRouteClassifiesAsStatic` |
| AC#2 Connected interface gateway → CONNECTED | gateway heuristic | `Ac2ConnectedInterfaceGatewayClassifiesAsConnected` |
| AC#3 Dynamic `type` bgp/ospf/dhcp/vpn | `RouteOriginClassifier` | `Ac3DynamicRouteTypeMapsProtocolOrigin` |
| AC#4 Active dynamic default in facts | `DynamicRouteOriginAnalyzer` | `Ac4DynamicDefaultRouteIncludedInActiveDynamicFacts` |
| AC#5 Unknown dynamic type → OTHER | fallback | `Ac5UnknownDynamicTypeFallsBackToOther` |
| AC#6 Per-table origin summary | `DynamicRouteOriginTableSummary` | `Ac6PerTableSummaryCountsOrigins` |
| AC#7 No routing writes | `RosReadCommandRegistry` | `Ac7NoRoutingWriteApisOpened` |
| AC#8 Persistence round-trip | operational jsonb | `Ac8PersistenceRoundTripIncludesDynamicRouteOriginAnalysis` |

Discovery + trace coverage: `DiscoveryMapsRouteTypeAndIncludesDynamicRoutesInOperationalObservations`, `TraceExposesOriginOnSelectedRouteWhenObservationPresent`.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DynamicRouteOriginLivingSpecTests"
```

## Living Specification — RouteExpectation evaluation (M7.1-06)

Issue Set M7.1-06 / Network Rule M7.1 Spec §11. Deterministic scripted fixtures only; no live CHR; no routing writes; egress zones matched via interface-name proxy (no zone engine).

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Expected table/VRF pass/fail | `RouteExpectationEvaluator` | `Ac1ExpectedTableAndVrfMatchPassAndFail` |
| AC#2 Allowed next hop / egress interface | intersection checks | `Ac2AllowedNextHopAndEgressInterfaceViolations` |
| AC#3 Forbidden BLACKHOLE/UNREACHABLE | decision + forbidden types | `Ac3ForbiddenBlackholeAndUnreachableDecisions` |
| AC#4 Required origin type | `RouteOrigins` on selected routes | `Ac4RequiredOriginTypeMustBePresent` |
| AC#5 CPU firewall path | execution path CPU/MIXED | `Ac5CpuFirewallPathRequirementFailsOnHardwareOnly` |
| AC#6 Reverse path missing | `ReversePathSymmetryAnalyzer` via evaluator | `Ac6ReversePathMissingProducesFinding` |
| AC#7 Critical vs warning codes | `RouteExpectationCodes` | `Ac7CriticalExpectationsUseCriticalFindingCodes` |
| AC#8 Persistence round-trip | expectations + findings jsonb | `Ac8PersistenceRoundTripStoresExpectationsAndFindings` |
| AC#9 No routing writes | `RosReadCommandRegistry` | `Ac9NoRoutingWriteApisOpened` |
| AC#10 ECMP ONE_OF allowed hop | any allowed hop suffices | `Ac10EcmpOneOfAllowedHopSetPassesWhenAnyHopAllowed` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~RouteExpectationLivingSpecTests"
```

## Living Specification — reverse-path symmetry analysis (M7.1-07)

Issue Set M7.1-07 / Network Rule M7.1 Spec §12. Deterministic scripted fixtures only; no live CHR; no routing writes.

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Symmetric forward/reverse pair | table/VRF/egress/decision match | `Ac1SymmetricPairMatchesTableVrfEgressAndDecision` |
| AC#2 Reverse path missing | `REVERSE_PATH_MISSING` + evaluator finding | `Ac2ReversePathMissingWhenReturnRouteAbsent` |
| AC#3 Asymmetric expected | `ExpectAsymmetricReversePath` flag | `Ac3AsymmetricExpectedWhenFlagSet` |
| AC#4 Asymmetric unexpected | evaluator `ASYMMETRIC_UNEXPECTED` finding | `Ac4AsymmetricUnexpectedProducesEvaluatorFinding` |
| AC#5 Indeterminate missing source | incomplete probe endpoints | `Ac5IndeterminateWhenForwardSourceMissing` |
| AC#6 Multi-WAN different egress | policy-routed forward vs main reverse | `Ac6MultiWanDifferentEgressInterfacesAreAsymmetric` |
| AC#7 No routing writes | `RosReadCommandRegistry` | `Ac7NoRoutingWriteApisOpened` |
| AC#8 Persistence round-trip | `RouteResolutionTrace.ReversePathSymmetry` jsonb | `Ac8PersistenceRoundTripStoresReversePathSymmetryOnTrace` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ReversePathSymmetryLivingSpecTests"
```

## Living Specification — network path profile latency probes (M7.1-08)

Issue Set M7.1-08 / Network Rule M7.1 Spec §13. Scripted `LatencyMeasurement` fixtures only; no live ping; no routing writes.

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| AC#1 Bind table/VRF/interface from trace | profile hints ignored for probe params | `Ac1BindTableVrfAndInterfaceFromTraceNotProfileHints` |
| AC#2 Probe destination from profile | `NetworkPathProfileBinder` | `Ac2ProbeDestinationComesFromProfile` |
| AC#3 Path change + latency regression | `ROUTE_PATH_CHANGED_WITH_LATENCY_REGRESSION` | `Ac3PathChangeWithLatencyRegressionEmitsCombinedFinding` |
| AC#4 High latency without path change | isolated RTT finding | `Ac4HighLatencyWithoutPathChangeEmitsIsolatedFinding` |
| AC#5 Route expectation pass-through | prefix/next hop/egress/execution path | `Ac5RouteExpectationsPassThroughOnTrace` |
| AC#6 No routing writes | `RosReadCommandRegistry` | `Ac6NoRoutingWriteApisOpened` |
| AC#7 Persistence round-trip | `RouteResolutionTrace.NetworkPathProbeBindings` jsonb | `Ac7PersistenceRoundTripStoresBoundProbeOnTrace` |
| AC#8 Path fingerprint helper | prefix/next hop/egress change detection | `Ac8PathFingerprintHelperDetectsPrefixNextHopAndEgressChanges` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~NetworkPathProfileLivingSpecTests"
```

## Living Specification — routing configuration vs operational drift (M7.1-09)

Issue Set M7.1-09 / Network Rule M7.1 Spec §14. Scripted hash-material snapshots only; no live CHR; no routing writes.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| AC#1 Routing table FIB/disabled change → configuration drift | `RoutingDriftAnalyzer` + `RoutingDriftClassifier` | `Ac1RoutingTableFibChangeIsConfigurationDrift` |
| AC#2 Active/gateway-status only → operational, not config drift | hash-material diff + classifier | `Ac2ActiveAndGatewayStatusChangeOnlyIsOperationalNotConfigurationDrift` |
| AC#3 Static route distance/scope → configuration drift | config material keys `route.*.distance/scope` | `Ac3StaticRouteDistanceScopeChangeIsConfigurationDrift` |
| AC#4 Default route gateway change (ops) → default WAN changed | `default.*` operational keys | `Ac4DefaultRouteGatewayChangeIsOperationalDefaultWanChanged` |
| AC#5 Config hash unchanged + ops changed → operational only | `RoutingDriftClassification` flags | `Ac5ConfigHashUnchangedOpsHashChangedIsOperationalOnly` |
| AC#6 Config hash changed → configuration drift even if ops also changed | combined diff | `Ac6ConfigHashChangedIsConfigurationDriftEvenWhenOpsAlsoChanged` |
| AC#7 Upsert round-trip persists drift findings | `UpsertRoutingAssuranceStateUseCase` merge | `Ac7UpsertRoundTripPersistsDriftFindings` |
| AC#8 No routing write APIs | `RosReadCommandRegistry` | `Ac8NoRoutingWriteApisOpened` |

Branch coverage helper: `RoutingDriftCoverageTests` (target ≥75% on M7.1-09 modules).

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~RoutingDrift"
```

## Living Specification — Desktop routing assurance viewers (M7.1-10)

Issue Set M7.1-10 / Network Rule M7.1 Spec §10–§11. Read-only summaries; no full BGP table on Desktop; no routing writes. W2.2 binds next-hop/subject fields.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| AC#1 gRPC proto + service registered | `RoutingAssuranceService` + `RoutingAssuranceGrpcService` | `Ac1GrpcProtoAndServiceRegistered` |
| AC#2 ViewModel exposes expectations/findings/traces | `RoutingAssuranceViewModel` collections | `Ac2ViewModelExposesExpectationsFindingsAndTraceCollections` |
| AC#3 No routing write surface on Desktop | `HasRoutingWriteControls` = false | `Ac3NoRoutingWriteSurfaceOnDesktop` |
| AC#4 MainWindow routing assurance section | `MainWindow.axaml` Node sub-panel | `Ac4MainWindowContainsRoutingAssuranceSection` |
| AC#5 Get use case returns detail view | `RoutingAssuranceDetailView` | `Ac5GetUseCaseReturnsDetailViewWithExpectationsAndFindings` |
| AC#6 Trace summary bounded | `RouteResolutionTraceSummary` proto/view | `Ac6TraceSummaryBoundedWithoutFullRouteTableDump` |
| AC#7 Desktop architecture boundary | no Domain/RouterOs refs | `Ac7DesktopHasNoDomainOrRouterOsReferences` |
| AC#8 Seven MVP modules unchanged | `ShellNavigationModule` count | `Ac8SevenMvpModulesRemainUnchanged` |
| W2.2 next-hop / subject fields | typed rows `AllowedNextHopsText` / `SubjectText` / `NextHopGatewaysText` | `Ac9RoutingAssuranceBindsNextHopAndSubjectFields` + `RoutingAssuranceViewModelTests` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~DesktopRoutingAssuranceLivingSpecTests"
```

## Living Specification — CHR routing assurance acceptance (M7.1-11 / **M7.1 CLOSED**)

Issue Set M7.1-11 / Network Rule M7.1 Spec §15. Scripted in-process fixtures ONLY; live CHR matrix remains OFF; chains M7.1 modules via `UpsertRoutingAssuranceStateUseCase`; no routing writes. Suite: `RoutingAssuranceChrAcceptanceLivingSpecTests`.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| AC#1 Multi-WAN recursive mark → table → recursive gateway | `RouteResolutionTraceEngine` + upsert | `Ac1MultiWanRecursivePolicyMarkTableLookupAndRecursiveGatewaySucceeds` |
| AC#2 Balanced/per-table multi-WAN traces | per-table routing rules + traces | `Ac2BalancedPerTableMultiWanTracesResolvePerRoutingTable` |
| AC#3 ECMP ONE_OF + allowed next-hop expectation | `EcmpRouteSet` + `RouteExpectationEvaluator` | `Ac3EcmpOneOfExpectationPassesWhenAnyMemberMatches` |
| AC#4 Corp VRF trace + expectation | VRF selection + evaluator | `Ac4CorpVrfTraceAndExpectationPasses` |
| AC#5 Expectation fail (table/VRF/egress) critical | `RouteExpectationEvaluator` | `Ac5ExpectationFailWrongTableVrfEgressProducesCriticalFindings` |
| AC#6 Operational route change ≠ config drift | `RoutingDriftAnalyzer` on upsert | `Ac6OperationalRouteChangeProducesOperationalDriftNotConfigurationDrift` |
| AC#7 Reverse-path symmetry + network path probe binding | `ReversePathSymmetryAnalyzer` + `NetworkPathProfileBinder` | `Ac7ReversePathSymmetryAndNetworkPathProbeBindingOnBranchToHqTrace` |
| AC#8 Full upsert round-trip persistence | `UpsertRoutingAssuranceStateUseCase` + get | `Ac8FullUpsertRoundTripPersistsExpectationsFindingsTracesAndDrift` |
| AC#9 No routing write APIs | `RosReadCommandRegistry` | `Ac9NoRoutingWriteApisOpened` |
| AC#10 Deterministic / no live CHR | Living Spec source + `testing.md` + testlab README | `Ac10DeterministicLivingSpecNoLiveChr` |

Testlab skeleton (optional live CHR): `testlab/chr/topologies/routing-assurance-multiwan` + `scripts/provision-routing-assurance.sh`.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~RoutingAssuranceChrAcceptance"
```

## Living Specification — endpoint attribution resolver (M7.2-01)

Issue Set M7.2-01 / next-2 §3. Scripted snapshot fixtures ONLY; no live CHR; Domain pure (no RouterOS types in resolver); no routing/firewall writes. Suite: `EndpointAttributionLivingSpecTests` + `EndpointAttributionCoverageTests`.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| AC#1 LAN IP via DHCP + bridge host | `EndpointAttributionResolver` | `Ac1LanIpResolvesThroughDhcpAndBridgeHost` |
| AC#2 Container IP via VETH mapping | VETH/container hops | `Ac2ContainerIpResolvesThroughVethMapping` |
| AC#3 VPN internal IP → WireGuard/IPsec peer | `VpnSessionFact` match | `Ac3VpnInternalIpResolvesToWireGuardPeer` |
| AC#4 Ambiguous MAC → PARTIAL + finding | `EndpointAttributionCodes.MacAmbiguous` | `Ac4AmbiguousMacSourcesProducePartialAndFinding` |
| AC#5 Unknown IP → UNKNOWN | fail-closed | `Ac5UnknownIpProducesUnknownCertainty` |
| AC#6 IPv6 ND path | ND before bridge host | `Ac6Ipv6NeighborDiscoveryPathResolvesMac` |
| AC#7 No routing/firewall write APIs | `EndpointAttributionAllowlist` + `RosReadCommandRegistry` | `Ac7NoRoutingOrFirewallWriteApisOpened` |
| AC#8 Inventory anchors site/node/device | query context hops | `Ac8InventoryAnchorsAttachedWhenProvided` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~EndpointAttribution"
```

## Living Specification — endpoint presence interval and routing context (M7.2-02)

Issue Set M7.2-02 / M7.1 §15. Scripted attribution + route-trace fixtures ONLY; no live CHR; Domain pure (no RouterOS types); no routing/firewall writes. Suite: `EndpointPresenceLivingSpecTests` + `EndpointPresenceCoverageTests`.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| AC#1 Build presence from attribution | `EndpointPresenceBuilder` site/node/vlan/vrf | `Ac1BuildPresenceIntervalFromAttributionResult` |
| AC#2 Routing context stores trace triple | `EndpointRoutingContextBuilder` | `Ac2RoutingContextStoresCorporateInternetAndWazuhTraces` |
| AC#3 Active presence valid_from + null valid_until | `EndpointPresenceInterval.IsActive` | `Ac3ActivePresenceHasValidFromAndNullValidUntil` |
| AC#4 Migration closes prior + new presence_id | `EndpointPresenceInterval.Open` | `Ac4MigrationClosesPreviousIntervalAndOpensNewPresence` |
| AC#5 Persistence round-trip | `OpenEndpointPresenceUseCase` + `FakeEndpointPresenceStore` | `Ac5PersistenceRoundTripStoresPresenceAndRoutingContext` |
| AC#6 Attribution certainty preserved | presence interval field | `Ac6AttributionCertaintyPreservedOnPresence` |
| AC#7 No routing write APIs | `RosReadCommandRegistry` guard | `Ac7NoRoutingWriteApisOpened` |
| AC#8 As-of query returns correct interval | `GetEndpointRoutingContextUseCase` | `Ac8AsOfQueryReturnsCorrectActiveInterval` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~EndpointPresence"
```

## Living Specification — endpoint mobility (M7.2-03)

Issue Set M7.2-03 / M7.1 §15. Scripted attribution + routing-assurance fixtures ONLY; no live CHR; Domain pure; no routing/firewall writes; no auto-deploy. Suite: `EndpointMobilityLivingSpecTests` + mobility branches in `EndpointPresenceCoverageTests`.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| AC#1 Mobility detected on anchor change | `EndpointMobilityHandler.IsMobilityEvent` | `Ac1MobilityDetectedWhenRoutingAnchorsChange` |
| AC#2 Active assessment invalidated | `ResponseAssessment.Invalidate` | `Ac2ActiveAssessmentInvalidatedOnIncidentMobility` |
| AC#3 Route traces recomputed | `RouteResolutionTraceEngine` via handler | `Ac3RouteTracesRecomputedForNewPresenceContext` |
| AC#4 Enforcement node from opened presence | `ResolveEnforcementNode` | `Ac4EnforcementNodeResolvedFromOpenedPresence` |
| AC#5 Auto-deploy suppressed | `AutoDeploySuppressed` flag | `Ac5AutoDeploySuppressedOnIncidentMobility` |
| AC#6 Mobility without incident keeps command traces | `EndpointMobilityCoordinator` | `Ac6MobilityWithoutActiveIncidentKeepsCommandTraces` |
| AC#7 No routing write APIs | `RosReadCommandRegistry` guard | `Ac7NoRoutingWriteApisOpened` |
| AC#8 Use-case round-trip | `OpenEndpointPresenceUseCase` + stores | `Ac8UseCaseRoundTripInvalidatesAssessmentAndStoresRecomputedContext` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~EndpointMobility"
```

## Living Specification — CHR endpoint migration acceptance (M7.2-04 / **M7.2 CLOSED**)

Issue Set M7.2-04 / M7.1 §15. Scripted in-process fixtures ONLY; live CHR matrix remains OFF; chains M7.2-01…03 via attribution, presence, mobility, and assessment stores; no routing writes. Suite: `EndpointMobilityChrAcceptanceLivingSpecTests`.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| AC#1 Attribution at branch A | `EndpointAttributionResolver` | `Ac1AttributionResolvesBranchAEndpointAnchors` |
| AC#2 Open presence + routing context | `OpenEndpointPresenceUseCase` | `Ac2OpenPresenceAtBranchAStoresRoutingContext` |
| AC#3 Active incident assessment | `ResponseAssessment` | `Ac3ActiveIncidentAssessmentBoundToEndpoint` |
| AC#4 Migration closes A, opens B | `EndpointPresenceInterval.Open` | `Ac4MigrationClosesBranchAAndOpensBranchBPresence` |
| AC#5 Invalidate + recompute traces | `EndpointMobilityHandler` | `Ac5IncidentMobilityInvalidatesAssessmentAndRecomputesTraces` |
| AC#6 Enforcement node at branch B | `ResolveEnforcementNode` | `Ac6EnforcementNodeFollowsOpenedPresenceAtBranchB` |
| AC#7 Auto-deploy suppressed | mobility outcome | `Ac7AutoDeploySuppressedOnIncidentMobilityPath` |
| AC#8 As-of historical context | `GetEndpointRoutingContextUseCase` | `Ac8AsOfQueryReturnsBranchAHistoricalRoutingContext` |
| AC#9 No routing write APIs | `RosReadCommandRegistry` | `Ac9NoRoutingWriteApisOpened` |
| AC#10 Deterministic / no live CHR | Living Spec + testlab README | `Ac10DeterministicLivingSpecNoLiveChr` |

Testlab skeleton (optional live CHR): `testlab/chr/topologies/endpoint-mobility-migration` + `scripts/provision-endpoint-mobility.sh`.

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~EndpointMobilityChrAcceptance"
```

## Living Specification — IncidentSignal ingress contract (M7.3-01)

Issue Set M7.3-01 / next-2 §IncidentSignal. Domain-only validation + application ingress use case; no raw syslog store; no signal persistence port; no live CHR. Suite: `IncidentSignalLivingSpecTests` + `IncidentSignalCoverageTests`.

| AC | Requirement | Test |
|----|-------------|------|
| AC#1 Valid minimal signal accepted | `IncidentSignal.Create` | `Ac1ValidMinimalSignalAccepted` |
| AC#2 Required fields enforced | `IncidentSignal.Create` | `Ac2RequiredFieldsEnforced` |
| AC#3 Confidence bounded 0–100 | `IncidentSignal.Create` | `Ac3ConfidenceBoundedZeroToOneHundred` |
| AC#4 Flow tuple validation | `FlowTuple.Create` | `Ac4FlowTupleRequiresAtLeastOneFieldAndValidPorts` |
| AC#5 Entity reference validation | `EntityReference.Create` | `Ac5EntityReferenceRequiresKindAndValue` |
| AC#6 Forbidden ingress field names | `IncidentSignalIngressGuard` | `Ac6ForbiddenIngressFieldNamesRejected` |
| AC#7 Inline raw syslog rejected | `IncidentSignalIngressGuard` | `Ac7InlineRawSyslogRejectedInReferences` |
| AC#8 ROUTEROS_LOG requires normalized category | `IncidentSignalIngressGuard` | `Ac8RouterOsLogRequiresNormalizedCategory` |
| AC#9 Ingest use case returns view; no persistence port | `IngestIncidentSignalUseCase` | `Ac9IngestUseCaseReturnsNormalizedViewWithoutPersistencePort` |
| AC#10 Unauthorized ingest rejected | `IngestIncidentSignalUseCase` | `Ac10UnauthorizedIngestRejected` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~IncidentSignal"
```

## Living Specification — Historical ActiveStateInterval resolver (M7.3-02)

Issue Set M7.3-02 / next-2 §4. Scripted deployment/audit transition timeline ONLY; Domain pure; no live CHR; no routing writes. Suite: `ActiveStateIntervalLivingSpecTests` + `ActiveStateIntervalCoverageTests`.

| AC | Requirement | Test |
|----|-------------|------|
| AC#1 Ordered non-overlapping intervals | `ActiveStateIntervalBuilder` | `Ac1BuildsOrderedNonOverlappingIntervals` |
| AC#2 Resolve occurred_at inside interval | `ActiveStateIntervalResolver` | `Ac2ResolvesOccurredAtInsideMiddleInterval` |
| AC#3 valid_from inclusive | `ActiveStateInterval.Contains` | `Ac3ValidFromIsInclusiveAtBoundary` |
| AC#4 Active tail covers later instant | `ActiveStateIntervalResolver` | `Ac4ActiveTailIntervalCoversLaterOccurredAt` |
| AC#5 Fail-closed before first transition | `ActiveStateIntervalResolver` | `Ac5OccurredBeforeFirstTransitionFailsClosed` |
| AC#6 PROVEN certainty | `ActiveStateIntervalClassifier` | `Ac6ProvenCertaintyRequiresAllHashesAndKnownFlags` |
| AC#7 PARTIAL certainty | `ActiveStateIntervalClassifier` | `Ac7PartialCertaintyWhenHashesIncomplete` |
| AC#8 UNKNOWN certainty | `ActiveStateIntervalClassifier` | `Ac8UnknownCertaintyWhenNoHashesPresent` |
| AC#9 Device-scoped timeline | `ActiveStateIntervalResolver` | `Ac9ResolverIgnoresOtherDeviceTransitions` |
| AC#10 Use case + auth | `ResolveActiveStateIntervalUseCase` | `Ac10UseCaseReturnsViewAndRejectsUnauthorized` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ActiveStateInterval"
```

## Living Specification — On-demand session context (M7.3-03)

Issue Set M7.3-03 / next-2 §2. Scripted connection-tracking reads ONLY; on-demand flow match; no full-table persistence; no live CHR. Suite: `IncidentSessionContextLivingSpecTests` + `IncidentSessionContextCoverageTests` + `ConnectionTrackingAllowlistTests`.

| AC | Requirement | Test |
|----|-------------|------|
| AC#1 Exact original flow resolves session | `IncidentSessionContextResolver` | `Ac1ExactOriginalFlowResolvesSession` |
| AC#2 Missing session → NotObserved | `IncidentSessionContextResolver` | `Ac2MissingSessionReturnsNotObserved` |
| AC#3 Ambiguous matches fail-closed | `IncidentSessionContextResolver` | `Ac3AmbiguousMatchesFailClosed` |
| AC#4 HW-offload → partial visibility | `IncidentSessionContextResolver` | `Ac4HwOffloadLimitsVisibilityToPartial` |
| AC#5 FastTrack → partial visibility | `IncidentSessionContextResolver` | `Ac5FastTrackLimitsVisibilityToPartial` |
| AC#6 NAT flags surfaced | `IncidentSessionContextResolver` | `Ac6NatFlagsAreSurfaced` |
| AC#7 Reply tuple mapped | `IncidentSessionContextResolver` | `Ac7ReplyTupleMappedFromSnapshot` |
| AC#8 Mapper parses ROS rows | `ConnectionTrackingSnapshotMapper` | `Ac8SnapshotMapperParsesRouterOsConnectionRows` |
| AC#9 Allowlist read-only print paths | `ConnectionTrackingAllowlist` | `Ac9ConnectionTrackingAllowlistIsReadOnlyPrintPaths` |
| AC#10 Use case + auth | `ResolveIncidentSessionContextUseCase` | `Ac10UseCaseReturnsViewAndRejectsUnauthorized` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~IncidentSessionContext|FullyQualifiedName~ConnectionTrackingAllowlist"
```

## Living Specification — Sensor observation correlation (M7.3-04)

Issue Set M7.3-04 / M7.1 §16. Scripted `RouteResolutionTrace` ONLY; Domain pure; no live CHR; no routing writes. Suite: `SensorObservationCorrelationLivingSpecTests` + `SensorObservationCorrelationCoverageTests`.

| AC | Requirement | Test |
|----|-------------|------|
| AC#1 Prerouting aligned | `SensorObservationCorrelationResolver` | `Ac1PreroutingAlignedWhenFlowAndIngressMatchTrace` |
| AC#2 Missing trace → Indeterminate | `SensorObservationCorrelationResolver` | `Ac2MissingRouteTraceReturnsIndeterminate` |
| AC#3 HW-offload → SensorBypassed | `SensorObservationCorrelationResolver` | `Ac3HardwareOffloadMarksSensorBypassed` |
| AC#4 Post-dstnat aligned | `SensorObservationCorrelationResolver` | `Ac4PostDstNatAlignedWhenTranslatedDestinationMatchesTrace` |
| AC#5 Post-dstnat without translated flow | `SensorObservationCorrelationResolver` | `Ac5PostDstNatWithoutTranslatedFlowIsIndeterminate` |
| AC#6 Egress mismatch (alternate WAN) | `SensorObservationCorrelationResolver` | `Ac6EgressMismatchDetectsAlternateWanPath` |
| AC#7 VRF mismatch | `SensorObservationCorrelationResolver` | `Ac7VrfMismatchAtPostRoutingReturnsMismatched` |
| AC#8 Routing mark mismatch | `SensorObservationCorrelationResolver` | `Ac8RoutingMarkMismatchAtPreroutingReturnsMismatched` |
| AC#9 Post-routing aligned | `SensorObservationCorrelationResolver` | `Ac9PostRoutingAlignedWhenTableAndDestinationMatchTrace` |
| AC#10 Use case + auth | `CorrelateSensorObservationUseCase` | `Ac10UseCaseReturnsViewAndRejectsUnauthorized` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~SensorObservationCorrelation"
```

## Living Specification — Response assessment visibility/confidence (M7.3-05)

Issue Set M7.3-05 / next-2. Scripted observation inputs ONLY; Domain pure; no live CHR; no routing writes. Suite: `ResponseAssessmentQualityLivingSpecTests` + `ResponseAssessmentQualityCoverageTests`.

| AC | Requirement | Test |
|----|-------------|------|
| AC#1 Full observation → high confidence | `ResponseAssessmentQualityEvaluator` | `Ac1FullyEnforceableWithFullObservationYieldsHighConfidence` |
| AC#2 HW-offload route trace | `ResponseAssessmentQualityEvaluator` | `Ac2HardwareOffloadedRouteTraceLimitsVisibility` |
| AC#3 Session not observed | `ResponseAssessmentQualityEvaluator` | `Ac3SessionNotObservedFailsClosedToNotObserved` |
| AC#4 Indeterminate feasibility | `ResponseAssessmentQualityEvaluator` | `Ac4IndeterminateFeasibilityReducesConfidence` |
| AC#5 Partial session visibility | `ResponseAssessmentQualityEvaluator` | `Ac5PartialSessionVisibilityDowngradesAssessment` |
| AC#6 HW-offloaded packet path | `ResponseAssessmentQualityEvaluator` | `Ac6HardwareOffloadedPacketPathDowngradesVisibility` |
| AC#7 Mixed packet path | `ResponseAssessmentQualityEvaluator` | `Ac7MixedPacketPathDowngradesVisibility` |
| AC#8 CreateActive embeds quality | `ResponseAssessment` | `Ac8CreateActiveEmbedsEvaluatedQuality` |
| AC#9 View emits quality | `ResponseAssessmentView` | `Ac9AssessmentViewEmitsVisibilityAndConfidence` |
| AC#10 Use case + auth | `EvaluateResponseAssessmentQualityUseCase` | `Ac10UseCaseReturnsViewAndRejectsUnauthorized` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ResponseAssessmentQuality"
```

## Living Specification — IncidentSignal ↔ ResponseAssessment contract (M7.3-06)

Issue Set M7.3-06 / next-2. Scripted contract bind ONLY; Domain pure; no live CHR; no routing writes; **M7.3 CLOSED**. Suite: `IncidentResponseAssessmentContractLivingSpecTests` + `IncidentResponseAssessmentContractCoverageTests`.

| AC | Requirement | Test |
|----|-------------|------|
| AC#1 event_id → incident_id | `IncidentResponseAssessmentContract` | `Ac1EventIdMapsOneToOneToIncidentId` |
| AC#2 original_flow preferred | `IncidentResponseAssessmentContract` | `Ac2OriginalFlowPreferredOverFlowForCorrelation` |
| AC#3 Missing flow fail-closed | `IncidentResponseAssessmentContract` | `Ac3MissingCorrelationFlowFailsClosed` |
| AC#4 CPU path + full session | `IncidentResponseAssessmentContract` | `Ac4CpuPathWithFullSessionYieldsFullyEnforceableAssessment` |
| AC#5 HW-offload path | `IncidentResponseAssessmentContract` | `Ac5HardwareOffloadedPathYieldsNotEnforceableAssessment` |
| AC#6 Partial session → NEW_CONNECTIONS_ONLY | `IncidentResponseFeasibilityClassifier` | `Ac6PartialSessionVisibilityYieldsNewConnectionsOnly` |
| AC#7 Signal confidence vs assessment | `IncidentResponseAssessmentContract` | `Ac7HighSignalConfidenceMayExceedAssessmentWhenVisibilityLimited` |
| AC#8 Assessment incident_id | `IncidentResponseAssessmentContract` | `Ac8AssessmentCarriesMappedIncidentId` |
| AC#9 View round-trip | `IncidentResponseAssessmentBindingView` | `Ac9BindingViewRoundTripsContractFields` |
| AC#10 Use case + auth | `BindIncidentResponseAssessmentUseCase` | `Ac10UseCaseReturnsViewAndRejectsUnauthorized` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~IncidentResponseAssessment"
```

## Living Specification — INCIDENT_PRE_STATE_DENY / INCIDENT_DENY_OVERLAY (M7.4-01)

Issue Set M7.4-01 / next-2. Pipeline stage + overlay kind; deploy path in M7.4-03. Suite: `IncidentDenyOverlayLivingSpecTests` + `IncidentDenyOverlayCoverageTests`.

| AC | Requirement | Test |
|----|-------------|------|
| AC#1 stage order | `PolicyPipelineV1.OrderedStages` | `Ac1PipelineStageFollowsProtectedControlPlaneBeforeMandatoryPreStateDeny` |
| AC#2 DROP-only incident stage | `PolicyPipelineV1.AllowedEffects` | `Ac2IncidentStageAllowsDropOnlyForIncidentDenyOverlay` |
| AC#3 reject/accept forbidden | `IsOwnerEffectAllowed` | `Ac3RejectAndAcceptAreForbiddenInIncidentStage` |
| AC#4 overlay metadata | `IncidentDenyOverlayMetadata` | `Ac4OverlayMetadataRequiresIncidentNodeReasonEvidenceAndExpiry` |
| AC#5 document guard | `IncidentDenyOverlayDocumentGuard` | `Ac5OverlayDocumentRequiresIncidentPreStateDenyDropRules` |
| AC#6 wrong stage | `IncidentDenyOverlayDocumentGuard` | `Ac6WrongStageFailsValidation` |
| AC#7 canonical round-trip | `PolicyCanonicalWriter` / `PolicyDocumentReader` | `Ac7CanonicalRoundTripPreservesOverlayMetadata` |
| AC#8 managed layout order | `ManagedChainLayoutBuilder` | `Ac8ManagedLayoutPlacesIncidentRulesAfterProtectedControlPlane` |
| AC#9 policy kind owner | `Policy.Create` | `Ac9PolicyKindIncidentDenyOverlayRequiresNodeOwner` |
| AC#10 use case + auth | `ValidateIncidentDenyOverlayUseCase` | `Ac10UseCaseReturnsViewAndRejectsUnauthorized` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~IncidentDenyOverlay"
```

## Living Specification — ResponseIntent feasibility matrix (M7.4-02)

Issue Set M7.4-02 / next-2. Scripted observation inputs ONLY; Domain pure; no deploy path. Suite: `ResponseIntentFeasibilityLivingSpecTests` + `ResponseIntentFeasibilityCoverageTests`.

| AC | Requirement | Test |
|----|-------------|------|
| AC#1 TEMPORARY_PRE_STATE_DENY requires expiry | `ResponseIntent.Create` | `Ac1TemporaryDenyRequiresFiniteExpiresAt` |
| AC#2 CPU firewall path | `ResponseIntentFeasibilityMatrix` | `Ac2CpuFirewallPathYieldsFullyEnforceable` |
| AC#3 HW-offload path | `ResponseIntentFeasibilityMatrix` | `Ac3HardwareOffloadedPathYieldsNotEnforceable` |
| AC#4 L2 bridge/VLAN bypass | `ResponseIntentFeasibilityMatrix` | `Ac4L2BridgeVlanBypassYieldsNotEnforceable` |
| AC#5 FastTrack session | `ResponseIntentFeasibilityMatrix` | `Ac5FastTrackSessionYieldsNewConnectionsOnly` |
| AC#6 Unknown path | `ResponseIntentFeasibilityMatrix` | `Ac6UnknownPacketPathYieldsIndeterminate` |
| AC#7 Proven container forward | `ResponseIntentFeasibilityMatrix` | `Ac7ProvenContainerForwardYieldsFullyEnforceable` |
| AC#8 Revoke action | `ResponseIntentFeasibilityMatrix` | `Ac8RevokeTemporaryExceptionIsFullyEnforceable` |
| AC#9 View round-trip | `ResponseIntentFeasibilityView` | `Ac9ViewRoundTripsIntentAndFeasibility` |
| AC#10 Use case + auth | `AssessResponseIntentFeasibilityUseCase` | `Ac10UseCaseReturnsViewAndRejectsUnauthorized` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ResponseIntentFeasibility"
```

## Living Specification — incident overlay compile/deploy via M3/M4 (M7.4-03)

Issue Set M7.4-03 / next-2. Bound overlay rules merged at compile; deploy orchestrates existing M3/M4 (one Node). Suite: `IncidentDenyOverlayCompileDeployLivingSpecTests` + `IncidentDenyOverlayCompileDeployCoverageTests`.

| AC | Requirement | Test |
|----|-------------|------|
| AC#1 no overlays | `IncidentDenyOverlayCompileMerge` | `Ac1MergeWithoutOverlaysPreservesComposedRules` |
| AC#2 expired skip | `IncidentDenyOverlayCompileMerge` | `Ac2ExpiredOverlayIsSkippedAtMerge` |
| AC#3 UUID collision | `IncidentDenyOverlayCompileMerge` | `Ac3RuleUuidCollisionFailsClosed` |
| AC#4 invalid document | `IncidentDenyOverlayDocumentGuard` | `Ac4InvalidOverlayDocumentFailsMerge` |
| AC#5 stage order | `IncidentDenyOverlayCompileMerge` | `Ac5MergeOrdersIncidentRulesByPipelineStage` |
| AC#6 compile merge | `CompileNodeFilterArtifactsUseCase` | `Ac6BoundOverlayIncreasesCompiledRuleCount` |
| AC#7 node match | `DeployIncidentDenyOverlayUseCase` | `Ac7DeployRejectsOverlayPolicyForWrongNode` |
| AC#8 overlay kind | `DeployIncidentDenyOverlayUseCase` | `Ac8DeployRequiresIncidentDenyOverlayKind` |
| AC#9 M3/M4 orchestration | `DeployIncidentDenyOverlayUseCase` | `Ac9DeployOrchestratesCompilePlanAndStartForOneNode` |
| AC#10 use case + auth | `DeployIncidentDenyOverlayUseCase` | `Ac10DeployUseCaseRejectsUnauthorizedActor` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~IncidentDenyOverlayCompileDeploy"
```

## Living Specification — incident overlay TTL removal plan (M7.4-04)

Issue Set M7.4-04 / next-2. TTL expiry creates mandatory removal plan via M4 without silent RouterOS write. Suite: `IncidentDenyOverlayRemovalLivingSpecTests` + `IncidentDenyOverlayRemovalCoverageTests`.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| 1 | Expire pending removal requires INCIDENT_DENY_OVERLAY scope | `PolicyDesiredBinding.ExpirePendingRemoval` | `Ac1ExpirePendingRemovalRequiresIncidentOverlayScope` |
| 2 | Gate rejects binding before valid_until | `PolicyBindingGate.EvaluateIncidentOverlayExpiry` | `Ac2EvaluateIncidentOverlayExpiryRejectsBeforeValidUntil` |
| 3 | Gate allows past-due ACTIVE binding | `PolicyBindingGate.EvaluateIncidentOverlayExpiry` | `Ac3EvaluateIncidentOverlayExpiryAllowsPastDueBinding` |
| 4 | Store lists due ACTIVE overlay bindings | `IPolicyApprovalStore.ListDueIncidentDenyOverlayBindingsAsync` | `Ac4ListDueIncidentDenyOverlayBindingsReturnsPastDueActiveOnly` |
| 5 | Expire use case has zero RouterOS dependencies | `ExpireIncidentDenyOverlayBindingUseCase` | `Ac5ExpireUseCaseHasZeroRouterOsDependencies` |
| 6 | Expire → EXPIRED_PENDING_RECONCILIATION, no deploy | `ExpireIncidentDenyOverlayBindingUseCase` | `Ac6ExpireTransitionsBindingWithoutDeploymentStart` |
| 7 | Compile after expire excludes overlay rules | `CompileNodeFilterArtifactsUseCase` | `Ac7CompileAfterExpireExcludesOverlayRules` |
| 8 | Plan removal creates plan without StartDeployment | `PlanIncidentDenyOverlayRemovalUseCase` | `Ac8PlanRemovalCreatesPlanWithoutStartDeployment` |
| 9 | Plan audit records deployment_started=false | `PlanIncidentDenyOverlayRemovalUseCase` | `Ac9PlanAuditRecordsDeploymentStartedFalse` |
| 10 | Unauthorized actor rejected | `PlanIncidentDenyOverlayRemovalUseCase` | `Ac10PlanRejectsUnauthorizedActor` |
| 11 | Reconcile job expires due bindings without RouterOS | `ReconcileExpiredIncidentDenyOverlayBindingsJobUseCase` | `Ac11ReconcileJobExpiresDueOverlayBindingsWithoutRouterOs` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~IncidentDenyOverlayRemoval"
```

## Living Specification — RESPONSE_* feedback events (M7.4-05)

Issue Set M7.4-05 / next-2. Outbound feedback to external analytics complex. Suite: `ResponseFeedbackLivingSpecTests` + `ResponseFeedbackCoverageTests`.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| 1 | All eight RESPONSE_* codes stable | `ResponseFeedbackEventCodes` | `Ac1EventCodesMapToAllEightKinds` |
| 2 | Domain create validates correlation_id | `ResponseFeedbackEvent.Create` | `Ac2DomainCreateRequiresConcreteCorrelationId` |
| 3 | Emit persists immutable event | `EmitResponseFeedbackUseCase` | `Ac3EmitPersistsImmutableEvent` |
| 4 | Configured delivery port receives event | `IResponseFeedbackDeliveryPort` | `Ac4ConfiguredDeliveryPortReceivesEvent` |
| 5 | Not-configured delivery still persists | `NotConfiguredResponseFeedbackDeliveryPort` | `Ac5NotConfiguredDeliveryStillPersistsEvent` |
| 6 | List requires auth | `ListResponseFeedbackEventsUseCase` | `Ac6ListByIncidentRequiresAuth` |
| 7 | List returns persisted events | `IResponseFeedbackEventStore` | `Ac7ListByIncidentReturnsPersistedEvents` |
| 8 | Not-enforceable assess emits BLOCKED | `AssessResponseIntentFeasibilityUseCase` | `Ac8AssessNotEnforceableEmitsBlockedFeedback` |
| 9 | Emit audit records code + delivery | `EmitResponseFeedbackUseCase` | `Ac9EmitAuditRecordsEventCodeAndDelivery` |
| 10 | Unauthorized emit rejected | `EmitResponseFeedbackUseCase` | `Ac10EmitRejectsUnauthorizedActor` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~ResponseFeedback"
```

## Living Specification — incident response E2E (M7.4-06)

Issue Set M7.4-06 / next-2. Scripted E2E for enforceable / not-enforceable / rollback / residual risk paths. Suite: `IncidentResponseE2ELivingSpecTests` + `IncidentResponseE2ECoverageTests`.

| AC | Requirement | Module | Test |
|----|-------------|--------|------|
| 1 | Enforceable assess → FullyEnforceable | `AssessResponseIntentFeasibilityUseCase` | `Ac1EnforceableAssessReturnsFullyEnforceable` |
| 2 | Deploy emits PLANNED + STARTED feedback | `DeployIncidentDenyOverlayUseCase` | `Ac2EnforceableDeployEmitsPlannedAndStartedFeedback` |
| 3 | Committed deploy → APPLIED + VERIFIED | `ReportIncidentDeploymentOutcomeUseCase` | `Ac3CommittedDeploymentEmitsAppliedAndVerifiedFeedback` |
| 4 | Not-enforceable → BLOCKED, no STARTED | `AssessResponseIntentFeasibilityUseCase` | `Ac4NotEnforceableAssessEmitsBlockedWithoutDeploy` |
| 5 | Rollback → ROLLED_BACK feedback | `ReportIncidentDeploymentOutcomeUseCase` | `Ac5FailedDeploymentRollbackEmitsRolledBackFeedback` |
| 6 | Recovery → RECOVERY_REQUIRED feedback | `ReportIncidentDeploymentOutcomeUseCase` | `Ac6RecoveryRequiredEmitsRecoveryFeedback` |
| 7 | Partial enforceability records residual_risk | `AssessResponseIntentFeasibilityUseCase` | `Ac7PartialEnforceabilityRecordsResidualRisk` |
| 8 | TTL expiry → EXPIRED + removal PLANNED | `PlanIncidentDenyOverlayRemovalUseCase` | `Ac8TtlExpiryEmitsExpiredAndRemovalPlannedFeedback` |
| 9 | Full lifecycle queryable by incident_id | `ListResponseFeedbackEventsUseCase` | `Ac9FullEnforceableLifecycleQueryableByIncident` |
| 10 | Unauthorized assess rejected | `AssessResponseIntentFeasibilityUseCase` | `Ac10UnauthorizedAssessRejected` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~IncidentResponseE2E"
```

## CHR live matrix

Not enabled until an isolated self-hosted runner exists. Skeleton contracts run in `routeros-integration` workflow and in `Mfc.RouterOs.IntegrationTests`. For M6-09 / N1-07 DoD, scripted E2E Living Specs replace the live CHR matrix.

## Living Specification — P2 RouterOS read path (P2-04…P2-06)

Production RouterOS probe + capture + Controller DI gate → `Mfc.RouterOs` + `Program.cs`:

| AC / вимога | Модуль | Тест |
|-------------|--------|------|
| Disabled → probe/capture stubs | `AddMfcRouterOs` | `PilotReadinessLivingSpecTests.Ac1DisabledByDefaultResolvesProbeOnlyAndNotConfiguredPorts` |
| Enabled → production ports (scoped) | `RouterOsReadPort`, `RouterOsSnapshotCapturePort` | `Ac2EnabledResolvesProductionPortsFromScope` |
| Enabled → stable-read coordinator | `RouterOsStableReadCoordinatorPort` | `Ac3EnabledRegistersStableReadCoordinatorPort` |
| ROADMAP references production DI | docs | `Ac4RoadmapReferencesAddRouterOsProductionServices` |
| Config section path | `RouterOsServiceCollectionExtensions.ConfigurationSectionPath` | `Ac5ConfigurationSectionPathIsDocumented` |
| Pilot runbook present | `docs/operations/pilot-runbook.md` | `Ac6PilotRunbookExists` |
| Live API-SSL probe | `RouterOsReadPort` | `RouterOsReadPortLivingSpecTests` |
| Stable-read capture + persist | `RouterOsSnapshotCapturePort` | `RouterOsSnapshotCapturePortLivingSpecTests` + Integration `RouterOsSnapshotCaptureIntegrationTests` |

Filter:
```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/Mfc.UnitTests -c Release --filter "FullyQualifiedName~PilotReadiness|FullyQualifiedName~RouterOsReadPortLivingSpec|FullyQualifiedName~RouterOsSnapshotCapturePortLivingSpec"
```
