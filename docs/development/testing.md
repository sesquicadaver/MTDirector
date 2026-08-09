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
| `tests/Mfc.UnitTests` | Unit + architecture boundary tests + coverage (incl. Inventory, Snapshots/Capabilities, RouterOS discovery + capability + N1 topology/path class, node topology validation, stable-read, raw snapshot, canonicalization, menu projector, capture idempotency/audit, semantic diff M1-24, inventory proto contracts M1-25) |
| `tests/Mfc.IntegrationTests` | Controller health + Inventory gRPC host (M1-25), Desktop connection, PostgreSQL bootstrap + inventory/snapshot persist (Testcontainers) |
| `tests/Mfc.RouterOs.IntegrationTests` | RouterOS markers + CHR skeleton contracts (no live CHR required) |

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
| Contract round-trip | `Uuid` / `Site` | `InventoryProtoContractTests` |

Filter: `dotnet test --filter FullyQualifiedName~Inventory`.

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
