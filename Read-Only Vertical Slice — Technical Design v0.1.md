MikroTik Firewall Controller

Read-Only Vertical Slice — Technical Design v0.1

Дата: 3 серпня 2026 року
Статус: нормативний технічний дизайн M1


---

1. Незмінна мета проєкту

Система створюється для централізованого керування firewall-політиками MikroTik в одній компанії з багатьма філіями, де використовуються:

окремі маршрутизатори;

один маршрутизатор із декількома WAN у режимі failover або балансування;

VRRP-вузли з декількох маршрутизаторів;

MikroTik CRS як маршрутизатори або комутатори;

MikroTik-комутатори, для яких у межах MVP керується лише management-plane firewall.


M1 не є системою моніторингу, CMDB, NMS або універсальним конфігуратором RouterOS. Його єдине призначення — створити довірений read-path, на якому надалі базуватимуться:

policy composition
→ validation
→ semantic diff
→ deployment planning
→ safe application
→ rollback
→ drift detection

У M1 відсутні будь-які production-операції зміни RouterOS.


---

2. Нормативне спрощення попередніх документів

Попередній Initial Issue Set v0.1 містив надмірну реляційну деталізацію read-only topology.

У M1 не створюються окремі persisted-сутності:

Uplink
ZoneBinding
VrrpGroup
VrrpMember
DeviceCapability
Bridge
BridgePort
Vlan
Route
FirewallRule

Ці дані:

1. читаються з RouterOS;


2. потрапляють у snapshot;


3. канонізуються;


4. використовуються для обчислюваного topology projection;


5. не дублюються в окремих таблицях.



Реляційно зберігаються лише:

Site
Node
Device
DeviceConnectionProfile
CaptureOperation
SnapshotCapture
SnapshotPayload

Це уточнення замінює відповідні частини M1-01, M1-03, M1-17 і M1-18.

Окремі topology-таблиці з’являться лише тоді, коли оператору буде необхідно задавати desired topology bindings для компіляції та deployment. Read-only дані не перетворюються на domain entities без практичної потреби.


---

3. Результат M1

Після завершення M1 система повинна виконувати такий сценарій:

1. Оператор створює Site.
2. Оператор створює Node.
3. Оператор реєструє один або декілька Device.
4. Controller перевіряє TLS та RouterOS credentials.
5. Controller запускає повний read-only capture.
6. RouterOS adapter читає лише allowlisted menus і properties.
7. Отримані дані проходять redaction.
8. Controller виконує повторне читання конфігурації.
9. Snapshot приймається лише при однакових configuration hashes.
10. Raw snapshot зберігається для технічного аудиту.
11. Canonical snapshot розділяється на configuration та observations.
12. PostgreSQL зберігає immutable capture.
13. Desktop показує canonical sections.
14. Наступний capture порівнюється з попереднім.
15. Desktop показує deterministic semantic diff.

Сценарій повинен працювати для:

standalone router
single-router multi-WAN failover
single-router multi-WAN balancing
VRRP active/passive
VRRP split-master
RouterOS CRS management plane


---

4. Інваріанти M1

1. Desktop ніколи не підключається до RouterOS.


2. Desktop ніколи не отримує RouterOS credentials.


3. Production-код не приймає довільний RouterOS command path.


4. Production-код не виконує RouterOS add, set, remove, move, enable, disable, reset, run або import.


5. Єдині спеціальні команди — /login і /cancel.


6. Решта команд — лише зафіксовані /print із явною .proplist.


7. Snapshot одного пристрою або повністю завершений, або має статус failure.


8. Optional section може бути UNSUPPORTED або NOT_APPLICABLE, але не мовчки втраченою.


9. Runtime state не впливає на configuration_hash.


10. Configuration не змішується з runtime observations.


11. RouterOS .id не є persistent identity.


12. Невідоме RouterOS property не ігнорується.


13. Невідоме property не включається до відомої configuration semantics.


14. Наявність невідомого property переводить capability profile у NEEDS_REVALIDATION.


15. Semantic diff не створює вигаданої відповідності між неоднозначними unmanaged rules.


16. Один Device не може мати більше одного активного capture.


17. Capture не є постійним моніторингом і запускається лише явно.


18. RouterOS 6 не входить до write-compatible foundation і в M1 отримує статус UNSUPPORTED_LEGACY.


19. SwOS не є RouterOS API target.


20. Відмова Controller не впливає на поточну роботу firewall.




---

5. Компоненти M1

┌──────────────────────────────────────┐
│ Mfc.Desktop                          │
│                                      │
│ • inventory tree                     │
│ • capture control                    │
│ • snapshot viewer                    │
│ • semantic diff viewer               │
└─────────────────┬────────────────────┘
                  │ gRPC + TLS
┌─────────────────▼────────────────────┐
│ Mfc.Controller                       │
│                                      │
│ • authentication / authorization     │
│ • gRPC endpoints                     │
│ • capture operation recovery         │
│ • bounded operation scheduling       │
└────────┬──────────────────────┬──────┘
         │                      │
         ▼                      ▼
┌────────────────┐     ┌────────────────────┐
│ Mfc.Application│     │ Mfc.Infrastructure │
│                │     │                    │
│ • capture flow │     │ • PostgreSQL       │
│ • topology     │     │ • encrypted secrets│
│ • canonicalize │     │ • audit            │
│ • hashing      │     │ • idempotency      │
│ • semantic diff│     │                    │
└────────┬───────┘     └────────────────────┘
         │
         ▼
┌──────────────────────────────────────┐
│ Mfc.RouterOs                         │
│                                      │
│ • API word/sentence codec            │
│ • TLS session                        │
│ • tagged commands                    │
│ • read command allowlist             │
│ • RouterOS DTO mapping               │
└─────────────────┬────────────────────┘
                  │ API-SSL
                  ▼
              RouterOS


---

6. Мінімальна domain model

6.1. Site

Site {
    id: SiteId
    code: SiteCode
    name: NonEmptyString
    status: DRAFT | ACTIVE | DISABLED
    row_version: uint64
}

Інваріанти:

code унікальний;

code незмінний після переходу в ACTIVE;

code відповідає:


^[A-Z][A-Z0-9_-]{1,31}$


---

6.2. Node

Node {
    id: NodeId
    site_id: SiteId
    name: NonEmptyString
    declared_kind:
        ROUTER |
        VRRP |
        SWITCH
    declared_uplink_mode:
        NONE |
        SINGLE |
        FAILOVER |
        BALANCED |
        MIXED
    status:
        DRAFT |
        ACTIVE |
        DISABLED
    row_version: uint64
}

declared_kind задається оператором. Controller перевіряє його за snapshot, але не замінює автоматично.

Інваріанти для ACTIVE:

Kind	Кількість Device

ROUTER	рівно 1
VRRP	не менше 2
SWITCH	рівно 1



---

6.3. Device

Device {
    id: DeviceId
    node_id: NodeId
    display_name: NonEmptyString
    management_host: HostNameOrIp
    management_port: uint16
    enabled: bool
    last_support_state: SupportState?
    last_completed_capture_id: SnapshotCaptureId?
    row_version: uint64
}

У VRRP-вузлі кожний фізичний маршрутизатор має окрему management address. VRRP virtual address не використовується як єдина адреса керування.


---

6.4. DeviceConnectionProfile

DeviceConnectionProfile {
    device_id: DeviceId
    username: NonEmptyString
    secret_ref: SecretReference
    trust_mode:
        INTERNAL_CA |
        SPKI_PIN
    ca_profile_ref: string?
    pinned_spki_sha256: Hash256?
    connect_timeout: Duration
    command_timeout: Duration
    max_response_bytes: uint64
    row_version: uint64
}

Не дозволяється:

trust-all
skip-certificate-validation
anonymous-DH
plaintext secret

API-SSL RouterOS використовує secure port 8729. Без призначеного сертифіката сервіс може працювати через anonymous Diffie–Hellman, тому Controller повинен вимагати RouterOS certificate і перевірений TLS-сеанс. Сам API login містить password як звичайне значення всередині API sentence, що робить коректну TLS-перевірку обов’язковою. 


---

7. Capture model

7.1. CaptureOperation

CaptureOperation описує один запит оператора.

CaptureOperation {
    id: CaptureOperationId
    target:
        DeviceId |
        NodeId
    requested_by: UserId
    idempotency_key: UUID
    status:
        QUEUED |
        RUNNING |
        COMPLETED |
        PARTIAL |
        FAILED |
        CANCELED
    started_at: UTC?
    completed_at: UTC?
    error_code: string?
}

Для target NodeId створюється по одному SnapshotCapture для кожного активного Device.

Окрема сутність NodeSnapshot не потрібна. Device captures об’єднуються спільним CaptureOperationId.


---

7.2. SnapshotCapture

SnapshotCapture {
    id: SnapshotCaptureId
    operation_id: CaptureOperationId
    device_id: DeviceId
    status:
        QUEUED |
        CONNECTING |
        AUTHENTICATING |
        READING_PASS_1 |
        CANONICALIZING_PASS_1 |
        READING_PASS_2 |
        VERIFYING_STABILITY |
        PERSISTING |
        COMPLETED |
        FAILED |
        CANCELED
    attempt_count: uint8
    capture_started_at: UTC
    pass_1_completed_at: UTC?
    pass_2_completed_at: UTC?
    capture_completed_at: UTC?
    configuration_hash: Hash256?
    observation_hash: Hash256?
    capability_hash: Hash256?
    compatibility_material_hash: Hash256?
    snapshot_hash: Hash256?
    raw_payload_hash: Hash256?
    section_results: SectionResult[]
    error: CaptureError?
}

Після COMPLETED запис є immutable.


---

8. PostgreSQL schema M1

8.1. Нові таблиці

sites
nodes
devices
device_connection_profiles
capture_operations
snapshot_captures
snapshot_payloads

audit_events, encrypted_secrets та idempotency_records створюються в bootstrap migration.


---

8.2. sites

CREATE TABLE sites (
    id              uuid PRIMARY KEY,
    code            text NOT NULL,
    name            text NOT NULL,
    status          smallint NOT NULL,
    row_version     bigint NOT NULL DEFAULT 1,
    created_at      timestamptz NOT NULL,
    updated_at      timestamptz NOT NULL,

    CONSTRAINT uq_sites_code UNIQUE (code),
    CONSTRAINT ck_sites_code
        CHECK (code ~ '^[A-Z][A-Z0-9_-]{1,31}$'),
    CONSTRAINT ck_sites_name
        CHECK (length(btrim(name)) BETWEEN 1 AND 128),
    CONSTRAINT ck_sites_row_version
        CHECK (row_version > 0)
);


---

8.3. nodes

CREATE TABLE nodes (
    id                      uuid PRIMARY KEY,
    site_id                 uuid NOT NULL REFERENCES sites(id),
    name                    text NOT NULL,
    declared_kind           smallint NOT NULL,
    declared_uplink_mode    smallint NOT NULL,
    status                  smallint NOT NULL,
    row_version             bigint NOT NULL DEFAULT 1,
    created_at              timestamptz NOT NULL,
    updated_at              timestamptz NOT NULL,

    CONSTRAINT uq_nodes_site_name UNIQUE (site_id, name),
    CONSTRAINT ck_nodes_name
        CHECK (length(btrim(name)) BETWEEN 1 AND 128),
    CONSTRAINT ck_nodes_row_version
        CHECK (row_version > 0)
);

Cardinality перевіряється при переході Node з DRAFT у ACTIVE.


---

8.4. devices

CREATE TABLE devices (
    id                          uuid PRIMARY KEY,
    node_id                     uuid NOT NULL REFERENCES nodes(id),
    display_name                text NOT NULL,
    management_host             text NOT NULL,
    management_host_kind        smallint NOT NULL,
    management_port             integer NOT NULL DEFAULT 8729,
    enabled                     boolean NOT NULL DEFAULT true,
    last_support_state          smallint NULL,
    last_completed_capture_id   uuid NULL,
    row_version                 bigint NOT NULL DEFAULT 1,
    created_at                  timestamptz NOT NULL,
    updated_at                  timestamptz NOT NULL,

    CONSTRAINT ck_devices_name
        CHECK (length(btrim(display_name)) BETWEEN 1 AND 128),
    CONSTRAINT ck_devices_port
        CHECK (management_port BETWEEN 1 AND 65535),
    CONSTRAINT ck_devices_row_version
        CHECK (row_version > 0)
);

CREATE UNIQUE INDEX uq_devices_active_endpoint
    ON devices (management_host, management_port)
    WHERE enabled = true;


---

8.5. device_connection_profiles

CREATE TABLE device_connection_profiles (
    device_id                   uuid PRIMARY KEY REFERENCES devices(id),
    username                    text NOT NULL,
    encrypted_secret_id         uuid NOT NULL REFERENCES encrypted_secrets(id),
    trust_mode                  smallint NOT NULL,
    ca_profile_ref              text NULL,
    pinned_spki_sha256          bytea NULL,
    connect_timeout_ms          integer NOT NULL,
    command_timeout_ms          integer NOT NULL,
    max_response_bytes          bigint NOT NULL,
    row_version                 bigint NOT NULL DEFAULT 1,
    updated_at                  timestamptz NOT NULL,

    CONSTRAINT ck_connection_username
        CHECK (length(username) BETWEEN 1 AND 64),
    CONSTRAINT ck_connection_spki
        CHECK (
            pinned_spki_sha256 IS NULL
            OR octet_length(pinned_spki_sha256) = 32
        ),
    CONSTRAINT ck_connection_connect_timeout
        CHECK (connect_timeout_ms BETWEEN 1000 AND 30000),
    CONSTRAINT ck_connection_command_timeout
        CHECK (command_timeout_ms BETWEEN 1000 AND 120000),
    CONSTRAINT ck_connection_max_response
        CHECK (max_response_bytes BETWEEN 1048576 AND 268435456)
);

INTERNAL_CA вимагає ca_profile_ref.
SPKI_PIN вимагає pinned_spki_sha256.


---

8.6. capture_operations

CREATE TABLE capture_operations (
    id                  uuid PRIMARY KEY,
    target_type         smallint NOT NULL,
    target_id           uuid NOT NULL,
    requested_by        uuid NOT NULL,
    idempotency_key     uuid NOT NULL,
    status              smallint NOT NULL,
    started_at          timestamptz NULL,
    completed_at        timestamptz NULL,
    error_code          text NULL,
    created_at          timestamptz NOT NULL,

    CONSTRAINT uq_capture_operation_idempotency
        UNIQUE (requested_by, idempotency_key)
);


---

8.7. snapshot_payloads

Payloads зберігаються content-addressed.

CREATE TABLE snapshot_payloads (
    payload_hash        bytea PRIMARY KEY,
    payload_kind        smallint NOT NULL,
    schema_version      integer NOT NULL,
    compression         smallint NOT NULL,
    uncompressed_size   bigint NOT NULL,
    compressed_payload  bytea NOT NULL,
    created_at          timestamptz NOT NULL,

    CONSTRAINT ck_snapshot_payload_hash
        CHECK (octet_length(payload_hash) = 32),
    CONSTRAINT ck_snapshot_payload_size
        CHECK (
            uncompressed_size > 0
            AND uncompressed_size <= 268435456
        )
);

Дозволені payload_kind:

RAW_SANITIZED
CANONICAL_CONFIGURATION
CANONICAL_OBSERVATIONS
CANONICAL_CAPABILITIES
CANONICAL_COMPATIBILITY_MATERIAL

Hash обчислюється над нестисненими canonical bytes.

Compression M1:

BROTLI


---

8.8. snapshot_captures

CREATE TABLE snapshot_captures (
    id                              uuid PRIMARY KEY,
    operation_id                    uuid NOT NULL
                                        REFERENCES capture_operations(id),
    device_id                       uuid NOT NULL REFERENCES devices(id),
    status                          smallint NOT NULL,
    attempt_count                   smallint NOT NULL,
    capture_started_at              timestamptz NOT NULL,
    pass_1_completed_at             timestamptz NULL,
    pass_2_completed_at             timestamptz NULL,
    capture_completed_at            timestamptz NULL,

    raw_payload_hash                bytea NULL,
    configuration_payload_hash      bytea NULL,
    observation_payload_hash        bytea NULL,
    capability_payload_hash         bytea NULL,
    compatibility_payload_hash      bytea NULL,

    configuration_hash              bytea NULL,
    observation_hash                bytea NULL,
    capability_hash                 bytea NULL,
    compatibility_material_hash     bytea NULL,
    snapshot_hash                   bytea NULL,

    section_results                 jsonb NOT NULL DEFAULT '[]'::jsonb,
    error_code                      text NULL,
    error_details                   jsonb NULL,

    CONSTRAINT uq_snapshot_capture_device_operation
        UNIQUE (operation_id, device_id)
);

UPDATE і DELETE completed captures забороняються правами PostgreSQL application role.


---

9. gRPC contracts

9.1. Загальні типи

syntax = "proto3";

package mfc.v1;

import "google/protobuf/timestamp.proto";

message Uuid {
  bytes value = 1; // рівно 16 bytes
}

message Sha256 {
  bytes value = 1; // рівно 32 bytes
}

message PageRequest {
  uint32 page_size = 1;
  string page_token = 2;
}

message ErrorDetail {
  string code = 1;
  bool retryable = 2;
  Uuid correlation_id = 3;
  optional Uuid target_id = 4;
  optional string section_id = 5;
  optional string sanitized_detail = 6;
}

Правила:

UUID передається у network byte order;

page_token opaque і підписаний Controller;

page_size обмежений сервером;

raw RouterOS error не передається без sanitization.



---

9.2. InventoryService

service InventoryService {
  rpc ListSites(ListSitesRequest) returns (ListSitesResponse);
  rpc CreateSite(CreateSiteRequest) returns (Site);
  rpc CreateNode(CreateNodeRequest) returns (Node);
  rpc GetNode(GetNodeRequest) returns (NodeDetails);
  rpc RegisterDevice(RegisterDeviceRequest) returns (Device);
  rpc UpdateDevice(UpdateDeviceRequest) returns (Device);
  rpc UpdateDeviceConnection(UpdateDeviceConnectionRequest)
      returns (DeviceConnectionSummary);
  rpc ValidateDeviceConnection(ValidateDeviceConnectionRequest)
      returns (ValidateDeviceConnectionResponse);
}

ValidateDeviceConnection виконує лише:

TCP connect
TLS validation
RouterOS login
system identity read
RouterOS version read
api-ssl service verification
disconnect

Він не створює snapshot.

Окремий DiscoverDevice не потрібний. Повне discovery виконує StartCapture.


---

9.3. SnapshotService

service SnapshotService {
  rpc StartCapture(StartCaptureRequest)
      returns (StartCaptureResponse);

  rpc WatchCapture(WatchCaptureRequest)
      returns (stream CaptureProgress);

  rpc ListCaptures(ListCapturesRequest)
      returns (ListCapturesResponse);

  rpc GetSnapshotSummary(GetSnapshotSummaryRequest)
      returns (SnapshotSummary);

  rpc GetSnapshotSection(GetSnapshotSectionRequest)
      returns (SnapshotSectionPage);

  rpc CompareSnapshots(CompareSnapshotsRequest)
      returns (DiffPage);
}


---

9.4. StartCapture

message StartCaptureRequest {
  oneof target {
    Uuid device_id = 1;
    Uuid node_id = 2;
  }

  Uuid idempotency_key = 3;
}

message StartCaptureResponse {
  Uuid operation_id = 1;
  bool deduplicated = 2;
}

Node capture:

створює Device captures для всіх активних members;

запускає їх із bounded parallelism;

не приховує недоступний member;

завершується PARTIAL, якщо хоча б один member не завершений.



---

9.5. Capture progress

enum CaptureStage {
  CAPTURE_STAGE_UNSPECIFIED = 0;
  CAPTURE_STAGE_QUEUED = 1;
  CAPTURE_STAGE_CONNECTING = 2;
  CAPTURE_STAGE_AUTHENTICATING = 3;
  CAPTURE_STAGE_READING_PASS_1 = 4;
  CAPTURE_STAGE_CANONICALIZING_PASS_1 = 5;
  CAPTURE_STAGE_READING_PASS_2 = 6;
  CAPTURE_STAGE_VERIFYING_STABILITY = 7;
  CAPTURE_STAGE_PERSISTING = 8;
  CAPTURE_STAGE_COMPLETED = 9;
  CAPTURE_STAGE_FAILED = 10;
  CAPTURE_STAGE_CANCELED = 11;
}

message CaptureProgress {
  Uuid operation_id = 1;
  Uuid capture_id = 2;
  Uuid device_id = 3;
  CaptureStage stage = 4;
  optional string current_section = 5;
  optional ErrorDetail error = 6;
  google.protobuf.Timestamp occurred_at = 7;
}

Процент виконання не передається: кількість і вартість RouterOS sections відрізняються між пристроями, тому такий процент був би недостовірним.


---

9.6. Generic canonical record contract

message CanonicalList {
  repeated CanonicalValue values = 1;
}

message CanonicalValue {
  oneof kind {
    string string_value = 1;
    sint64 signed_integer = 2;
    uint64 unsigned_integer = 3;
    bool boolean_value = 4;
    bytes binary_value = 5;
    CanonicalList list_value = 6;
  }
}

message CanonicalField {
  string name = 1;
  CanonicalValue value = 2;
}

message SnapshotRecord {
  string stable_key = 1;
  optional uint32 ordinal = 2;
  repeated CanonicalField configuration = 3;
  repeated CanonicalField observations = 4;
}

message SnapshotSectionPage {
  Uuid capture_id = 1;
  string section_id = 2;
  bool ordered = 3;
  repeated SnapshotRecord records = 4;
  string next_page_token = 5;
}

google.protobuf.Struct не використовується, оскільки його numeric representation не забезпечує потрібної точності для всіх uint64 та canonical integer fields.


---

9.7. Diff contract

enum DiffDomain {
  DIFF_DOMAIN_UNSPECIFIED = 0;
  DIFF_DOMAIN_CONFIGURATION = 1;
  DIFF_DOMAIN_OBSERVATION = 2;
  DIFF_DOMAIN_CAPABILITY = 3;
  DIFF_DOMAIN_COMPATIBILITY = 4;
}

enum DiffChange {
  DIFF_CHANGE_UNSPECIFIED = 0;
  DIFF_CHANGE_ADDED = 1;
  DIFF_CHANGE_REMOVED = 2;
  DIFF_CHANGE_MODIFIED = 3;
  DIFF_CHANGE_MOVED = 4;
  DIFF_CHANGE_STATE_CHANGED = 5;
  DIFF_CHANGE_SECTION_STATUS_CHANGED = 6;
}

enum MatchConfidence {
  MATCH_CONFIDENCE_UNSPECIFIED = 0;
  MATCH_CONFIDENCE_EXACT_IDENTITY = 1;
  MATCH_CONFIDENCE_NATURAL_KEY = 2;
  MATCH_CONFIDENCE_EXACT_SEQUENCE = 3;
  MATCH_CONFIDENCE_CONSERVATIVE = 4;
}

message DiffEntry {
  string section_id = 1;
  DiffDomain domain = 2;
  DiffChange change = 3;
  MatchConfidence confidence = 4;
  string stable_key = 5;
  optional uint32 before_ordinal = 6;
  optional uint32 after_ordinal = 7;
  optional SnapshotRecord before = 8;
  optional SnapshotRecord after = 9;
}


---

10. RouterOS API protocol layer

RouterOS API передає послідовності words, об’єднані в sentences. Sentence завершується zero-length word. Attribute order не є значущим, а .proplist може змінювати склад і порядок повернених attributes. Replies класифікуються як !re, !done, !empty, !trap та !fatal. RouterOS tags дозволяють одночасні команди, а /cancel завершує команду за її tag. 

10.1. Protocol types

RosWord
RosSentence
RosCommandSentence
RosReplySentence
RosReplyKind
RosCommandTag
RosAttribute
RosAttributeSequence
RosTrap

На protocol layer values залишаються byte sequences або UTF-8 strings. Типізація IP, duration, integer та boolean виконується mapping layer.


---

10.2. Duplicate attributes

Protocol parser:

зберігає attributes у вихідній послідовності;

не перетворює їх одразу на dictionary;

не втрачає duplicate attributes.


Typed mapper:

приймає одне значення для scalar property;

повертає API_PROTOCOL_ERROR при неоднозначному duplicate scalar;

дозволяє duplicate values лише для property, явно позначених як multi-valued у schema.


Duplicate entries у .proplist забороняються validator, оскільки RouterOS не визначає їх стабільну обробку. 


---

10.3. Session state machine

DISCONNECTED
    ↓
CONNECTING
    ↓
TLS_HANDSHAKE
    ↓
AUTHENTICATING
    ↓
READY
    ↓
CLOSING
    ↓
CLOSED

Помилки переводять session у:

FAULTED

FAULTED session не використовується повторно.


---

10.4. Tagged command execution

Для кожної команди:

1. генерується унікальний monotonic tag;


2. створюється bounded pending-command entry;


3. sentence записується через єдиний serialized writer;


4. єдиний read loop маршрутизує replies за tag;


5. !done завершує command;


6. !trap зберігається і command очікує завершальний !done;


7. !fatal завершує всі pending commands;


8. timeout запускає tagged /cancel;


9. після cancel pending state видаляється лише після завершення command або закриття connection.




---

10.5. Transport limits

Початкові значення:

Limit	Значення

Connect timeout	5 s
TLS + login timeout	10 s
Default command timeout	30 s
Full capture timeout	120 s
Parallel commands per Device	8
Maximum pending commands	16
Maximum word size	256 KiB
Maximum sentence size	2 MiB
Maximum raw capture	256 MiB
Maximum retry attempts	3


Жодний limit не може бути 0, infinite або необмеженим.


---

10.6. Retry policy

Transport session самостійно не виконує reconnect.

Retry виконує CaptureCoordinator для цілого capture attempt.

Не дозволяється:

отримати половину sections із connection A
→ reconnect
→ отримати решту з connection B
→ сформувати один complete snapshot

Після втрати connection attempt відкидається.


---

11. RouterOS read-command allowlist

Командний шлях задається production-кодом через закритий RosReadCommandId.

Runtime manifest не може додати новий command path.

11.1. System і management

Command ID	RouterOS path	Required

SystemIdentity	/system/identity/print	Так
SystemResource	/system/resource/print	Так
SystemRouterboard	/system/routerboard/print	Ні
SystemPackages	/system/package/print	Так
IpServices	/ip/service/print	Так


IpServices читає:

name
port
address
certificate
disabled
tls-version
vrf
max-sessions
dynamic

Поле /ip service address є додатковим service-level обмеженням, а не network firewall; MikroTik рекомендує використовувати firewall для блокування недовірених джерел. 


---

11.2. Interfaces і addresses

Command ID	RouterOS path

Interfaces	/interface/print
Ipv4Addresses	/ip/address/print
Ipv6Addresses	/ipv6/address/print
InterfaceLists	/interface/list/print
InterfaceListMembers	/interface/list/member/print


Interface-list resolution виконується в такому порядку:

1. Додати members із include.
2. Видалити members із exclude.
3. Додати explicit static members.

/interface list member не містить members, отриманих через include та exclude, тому зберігати лише цю таблицю недостатньо. 


---

11.3. IPv4 firewall

Command ID	RouterOS path

Ipv4Filter	/ip/firewall/filter/print
Ipv4AddressLists	/ip/firewall/address-list/print
Ipv4Nat	/ip/firewall/nat/print
Ipv4Raw	/ip/firewall/raw/print
Ipv4Mangle	/ip/firewall/mangle/print



---

11.4. IPv6 firewall

Command ID	RouterOS path

Ipv6Filter	/ipv6/firewall/filter/print
Ipv6AddressLists	/ipv6/firewall/address-list/print
Ipv6Nat	/ipv6/firewall/nat/print
Ipv6Raw	/ipv6/firewall/raw/print
Ipv6Mangle	/ipv6/firewall/mangle/print


Unsupported menu повертає section status UNSUPPORTED, якщо command позначений optional для capability profile.


---

11.5. Routing і multi-WAN evidence

Command ID	RouterOS path

RoutingTables	/routing/table/print
RoutingRules	/routing/rule/print
Ipv4StaticRoutes	/ip/route/print
Ipv6StaticRoutes	/ipv6/route/print
Ipv4DefaultRouteState	/ip/route/print
Ipv6DefaultRouteState	/ipv6/route/print
Ipv4Settings	/ip/settings/print
Ipv6Settings	/ipv6/settings/print


Повний dynamic routing table не читається.

Ipv4StaticRoutes і Ipv6StaticRoutes використовують version-tested static-only query profile.

Ipv4DefaultRouteState та Ipv6DefaultRouteState читають лише default routes і потрібні runtime fields.

Це виключає завантаження великих BGP/OSPF tables, які не належать до задачі firewall controller.


---

11.6. VRRP

Command ID	RouterOS path

VrrpInterfaces	/interface/vrrp/print


Обов’язкові configuration fields:

name
interface
vrid
version
priority
interval
preemption-mode
group-authority
v3-checksum-as-v2
disabled

Observations:

running
invalid
state

Virtual addresses визначаються через IPv4/IPv6 addresses, призначені відповідному VRRP interface.

VRRP identity не можна зводити лише до vrid: однаковий VRID може використовуватись окремо для IPv4 та IPv6, а RouterOS розглядає їх як різні Virtual Routers. Role також повинна зберігатися для кожного VRRP instance, а не як один global master/backup стан Device. 


---

11.7. Bridge і switch metadata

Command ID	RouterOS path

Bridges	/interface/bridge/print
BridgePorts	/interface/bridge/port/print
BridgeVlans	/interface/bridge/vlan/print
EthernetSwitches	/interface/ethernet/switch/print
EthernetSwitchPorts	/interface/ethernet/switch/port/print


EthernetSwitches і EthernetSwitchPorts є optional.

M1 не читає та не керує switch ACL.

Hardware-switched bridge traffic не можна автоматично вважати таким, що проходить через RouterOS IP firewall. Management-plane traffic до CPU та transit traffic через switch ASIC повинні залишатися різними доменами. 


---

12. Заборонені RouterOS paths

Production assembly не містить command definitions для:

/export
/import
/file/*
/user/*
/certificate/export*
/system/backup/*
/system/script/*
/system/scheduler/*
/system/history/undo
/system/history/redo
/tool/*

Також заборонені:

*/add
*/set
*/remove
*/move
*/enable
*/disable
*/reset
*/run
*/renew
*/release
*/upgrade

listen у M1 не використовується. Capture є point-in-time операцією, а не постійним RouterOS subscription.


---

13. Property profiles

Кожна allowlisted command має versioned property profile.

PropertyProfile {
    command_id
    profile_version
    properties[]
}

PropertyDefinition {
    routeros_name
    canonical_name
    type
    classification:
        CONFIGURATION |
        OBSERVATION |
        TRANSIENT |
        FORBIDDEN
    cardinality:
        SCALAR |
        LIST
    required:
        ALWAYS |
        OPTIONAL |
        CONDITIONAL
}

Кожний /print викликається з явною .proplist. RouterOS рекомендує задавати .proplist, оскільки без неї можуть повертатися дорогі для отримання properties; порядок повернення не гарантований, і RouterOS може повернути додаткові properties. 

13.1. FORBIDDEN properties

Заборонено запитувати або зберігати:

password
secret
private-key
private-key-data
passphrase
contents
sensitive
response

Однієї перевірки імені недостатньо. Кожний property повинен бути явно внесений до profile.


---

13.2. Unknown properties

Якщо RouterOS повернув property, якого немає у profile:

1. property проходить redaction;


2. зберігається в raw snapshot;


3. включається до compatibility material;


4. не включається до відомої configuration semantics;


5. Device отримує NEEDS_REVALIDATION;


6. snapshot залишається придатним для read-only перегляду;


7. майбутній deployment для такого Device блокується.




---

14. Compatibility manifest

Manifest визначає підтримку конкретного RouterOS build.

{
  "schemaVersion": 1,
  "profileId": "routeros-7-base-001",
  "match": {
    "majorVersion": 7,
    "releaseChannels": ["stable", "long-term"],
    "architectures": ["arm", "arm64", "mipsbe", "mmips", "tile", "x86_64"]
  },
  "commands": {
    "SystemIdentity": "required",
    "SystemRouterboard": "optional",
    "Ipv6Filter": "conditional",
    "EthernetSwitches": "optional"
  },
  "knownIncompatibilities": []
}

Manifest:

вбудовується в signed application release;

не редагується через GUI;

не завантажується з RouterOS;

не може визначати новий command path;

має власний hash;

входить до capability_hash.


Підтримка нової RouterOS version вимагає:

property fixture
→ parser test
→ canonicalization test
→ CHR або hardware integration test
→ manifest update
→ application release


---

15. Raw snapshot schema

Raw snapshot призначений для:

відтворення mapping defects;

capability analysis;

аудиту невідомих properties;

повторної canonicalization після schema update.


Він не є GUI-моделлю.

{
  "schema": "mfc.raw-snapshot/1",
  "captureId": "uuid",
  "deviceId": "uuid",
  "captureWindow": {
    "startedAt": "UTC",
    "completedAt": "UTC"
  },
  "transport": {
    "api": "api-ssl",
    "serverCertificateSpkiSha256": "hex"
  },
  "sections": [
    {
      "id": "firewall.ipv4.filter",
      "status": "ok",
      "commandProfile": "routeros-7-base-001",
      "startedAt": "UTC",
      "completedAt": "UTC",
      "records": [
        {
          "ordinal": 0,
          "attributes": [
            {
              "name": ".id",
              "value": "*1"
            },
            {
              "name": "chain",
              "value": "input"
            }
          ]
        }
      ]
    }
  ]
}

15.1. Raw section statuses

OK
UNSUPPORTED
NOT_APPLICABLE
FAILED

FAILED required section робить весь Device capture failed.


---

15.2. Raw data rules

attribute order зберігається;

record order зберігається;

.id дозволений;

API tags не зберігаються;

capture timestamps зберігаються;

credentials не зберігаються;

raw login sentence не зберігається;

TLS private material не зберігається;

RouterOS trap message зберігається лише після sanitization.



---

16. Canonical snapshot schema

Canonical snapshot складається з чотирьох окремих payloads:

Configuration
Observations
Capabilities
Compatibility material

16.1. Configuration

Містить лише відомі значення, які визначають firewall-relevant поведінку:

system identity
management services configuration
interfaces configuration
IP address configuration
interface-list definitions
static interface-list members
firewall rules
static address-list entries
NAT/RAW/Mangle rules
routing tables
routing rules
static routes
IP settings
VRRP configuration
bridge/VLAN configuration


---

16.2. Observations

Містить runtime state:

uptime
interface running state
invalid state
actual interface
dynamic addresses
active default routes
route reachability
VRRP role
bridge port active state
hardware-offload state
current tagged/untagged VLAN membership

Firewall counters і traffic counters у M1 не збираються.

Dynamic address-list contents у M1 не зберігаються повністю. Зберігається лише:

dynamic entries present
list names with dynamic entries
count, якщо отримання count підтримане profile

Це не перетворює controller на threat-feed або runtime-monitoring систему.


---

16.3. Capabilities

RouterOS version
architecture
board/model
installed package set
available command profiles
available properties
IPv6 support
VRRP support
bridge support
switch-chip visibility
support state
compatibility manifest hash


---

16.4. Compatibility material

Містить:

unknown properties
unknown enum values
unsupported sections
missing required optional properties
section traps classified as unsupported
raw property names excluded from known semantics

Зміна compatibility material змінює snapshot_hash, навіть коли відомий configuration_hash не змінився.


---

17. Canonicalization rules

17.1. Загальні правила

1. Encoding — UTF-8 без BOM.


2. Property order — фіксований schema order.


3. Map order — bytewise ordering canonical keys.


4. Unordered collections — sort by stable key.


5. Ordered firewall tables — зберігають RouterOS order.


6. Відсутнє значення не серіалізується.


7. Порожній string зберігається, якщо має semantics.


8. Boolean — true або false.


9. Integer — decimal без leading zeros.


10. Floating-point values не використовуються.


11. Enum — lowercase canonical token.


12. Unicode text зберігається без прихованої normalization.


13. Capture timestamps не входять до configuration payload.


14. .id не входить до canonical payload.


15. Counters не входять до canonical payload.




---

17.2. IP values

IPv4 address:
    canonical dotted decimal

IPv6 address:
    lowercase compressed representation

IpPrefix:
    host bits masked

IpInterfaceAddress:
    host address preserved

Address range:
    canonical start-end

Приклад:

IpPrefix:
192.168.1.19/24 → 192.168.1.0/24

IpInterfaceAddress:
192.168.1.19/24 → 192.168.1.19/24


---

17.3. Durations

RouterOS duration перетворюється на:

signed int64 microseconds

Непарсене значення:

зберігається в compatibility material;

не підміняється zero;

переводить capability profile у NEEDS_REVALIDATION.



---

17.4. Port sets

Port ranges:

1. перевіряються на 0..65535;


2. сортуються;


3. overlapping ranges об’єднуються;


4. adjacent ranges об’єднуються;


5. canonical representation використовує intervals.



80,81,82,100-110,105-120
→
80-82,100-120


---

17.5. Interface-list resolution

Canonical configuration містить:

declared include lists
declared exclude lists
explicit static members
resolved members
resolution findings

Cycle:

A includes B
B includes A

не підміняється порожнім списком. Section отримує validation finding:

INTERFACE_LIST_CYCLE


---

18. Hash contracts

Використовується SHA-256.

Hash256 завжди зберігається і передається як 32 raw bytes. Hex використовується лише в GUI та logs.

18.1. Section hash

section_hash =
SHA256(
    "mfc.section.v1\0"
    + section_id
    + "\0"
    + canonical_section_bytes
)


---

18.2. Configuration hash

configuration_hash =
SHA256(
    "mfc.configuration.v1\0"
    + ordered(
        section_id
        + "\0"
        + section_configuration_hash
    )
)


---

18.3. Observation hash

observation_hash =
SHA256(
    "mfc.observations.v1\0"
    + ordered(
        section_id
        + "\0"
        + section_observation_hash
    )
)


---

18.4. Capability hash

capability_hash =
SHA256(
    "mfc.capabilities.v1\0"
    + canonical_capability_bytes
)


---

18.5. Compatibility material hash

compatibility_material_hash =
SHA256(
    "mfc.compatibility.v1\0"
    + canonical_compatibility_bytes
)


---

18.6. Snapshot hash

snapshot_hash =
SHA256(
    "mfc.snapshot.v1\0"
    + configuration_hash
    + observation_hash
    + capability_hash
    + compatibility_material_hash
)

Compression, database row IDs і capture timestamps не впливають на hashes.


---

19. Stable-read protocol

RouterOS не надає Controller глобальної транзакційної snapshot-операції для всіх необхідних menus. Тому M1 використовує подвійне читання configuration domain.

19.1. Алгоритм

FOR attempt = 1..MAX_ATTEMPTS:

    establish new API-SSL session

    PASS 1:
        read configuration sections
        read observation sections
        canonicalize configuration
        calculate configuration_hash_1

    PASS 2:
        read configuration sections again
        canonicalize configuration
        calculate configuration_hash_2

    IF configuration_hash_1 == configuration_hash_2:
        assemble complete snapshot
        persist atomically
        return COMPLETED

    close session
    apply bounded retry delay

return SNAPSHOT_UNSTABLE


---

19.2. Правила

Pass 1 і Pass 2 виконуються в одній RouterOS session.

Втрата connection відкидає обидва pass.

Observation sections повторно не читаються.

Зменшення RouterOS uptime між pass означає reboot і відкидання attempt.

Зміна RouterOS version між pass відкидає attempt.

Optional unsupported section має бути однаково unsupported у двох pass.

Configuration section order фіксований.

Parallel read дозволений лише для незалежних sections.

Maximum parallelism per Device — 8.

Retry count — не більше 3.

Retry delay має jitter і верхню межу.

Нестабільний snapshot не зберігається як complete.


Це забезпечує observational stability, але не оголошується повною RouterOS transaction isolation. Перед майбутнім write deployment виконуватиметься окрема precondition validation для конкретних керованих ресурсів.


---

20. Read schedule

20.1. Phase A — identity

Послідовно:

SystemIdentity
SystemResource
SystemPackages
IpServices
SystemRouterboard

На цій фазі визначається compatibility profile.


---

20.2. Phase B — topology base

Паралельно:

Interfaces
Ipv4Addresses
Ipv6Addresses
InterfaceLists
InterfaceListMembers
Bridges
BridgePorts
BridgeVlans
EthernetSwitches
EthernetSwitchPorts
VrrpInterfaces


---

20.3. Phase C — firewall

Паралельно за family, але послідовно в межах одного ordered menu:

IPv4 Filter
IPv4 Address Lists
IPv4 NAT
IPv4 RAW
IPv4 Mangle

IPv6 Filter
IPv6 Address Lists
IPv6 NAT
IPv6 RAW
IPv6 Mangle


---

20.4. Phase D — routing evidence

RoutingTables
RoutingRules
Ipv4StaticRoutes
Ipv6StaticRoutes
Ipv4DefaultRouteState
Ipv6DefaultRouteState
Ipv4Settings
Ipv6Settings


---

21. Topology projection

Topology projection — pure function:

TopologySummary =
Project(
    declared Node configuration,
    canonical Device snapshots
)

Він не створює RouterOS changes і не зберігається як окрема authoritative модель.

21.1. Результат

TopologySummary {
    node_id
    declared_kind
    declared_uplink_mode
    device_summaries[]
    vrrp_groups[]
    uplink_evidence[]
    switch_evidence?
    findings[]
    projection_hash
}


---

21.2. Standalone router

Умови valid projection:

рівно один active Device;

RouterOS 7;

firewall menus доступні;

API-SSL налаштований;

пристрій не має VRRP configuration, несумісної з ROUTER.


Наявність одного локального VRRP interface не переводить Node автоматично у VRRP. Створюється finding:

DECLARED_ROUTER_HAS_VRRP


---

21.3. Multi-WAN

Controller не намагається самостійно остаточно класифікувати WAN topology.

Він формує evidence:

кількість static/default routes
кількість routing tables
routing rules
mangle routing marks
PCC matchers
NAT rules за out-interface/out-interface-list
кількість активних default routes
route distances
recursive gateway evidence
rp-filter state

Результат:

VERIFIED
PARTIALLY_VERIFIED
CONTRADICTED
INSUFFICIENT_EVIDENCE

Приклади:

declared FAILOVER
+ кілька default routes із різними distance
→ VERIFIED або PARTIALLY_VERIFIED

declared BALANCED
+ PCC/routing marks/routing tables
→ VERIFIED

declared SINGLE
+ два active default routes і PCC
→ CONTRADICTED

Controller не перемикає WAN і не тестує failover шляхом вимкнення interface.


---

21.4. VRRP

Локальний VRRP instance key:

family
+ vrid
+ parent_interface

Міжпристроєвий VRRP group key:

family
+ vrid
+ sorted virtual_address_set

Якщо virtual address sets не збігаються, group не вважається consistent.

Перевіряються:

RouterOS versions;

family;

VRID;

virtual addresses;

advertisement interval;

version;

preemption mode;

checksum mode;

group authority;

observed state;

capture timestamp spread.


Role vector:

Device A:
    VRID 10 IPv4 → MASTER
    VRID 20 IPv4 → BACKUP

Device B:
    VRID 10 IPv4 → BACKUP
    VRID 20 IPv4 → MASTER

Такий вузол класифікується як split-master, а не як active/passive.


---

21.5. Switch

Switch projection показує:

board/model
bridge configuration
VLAN filtering
bridge ports
hardware-offload observations
visible switch chip
management IP interfaces
input firewall

Він не робить висновку, що transit traffic контролюється IP firewall.


---

22. Semantic diff

22.1. Загальний алгоритм

IF snapshot_hash equal:
    return empty

FOR each section:
    IF section hash equal:
        skip

    match records
    classify change
    order output deterministically


---

22.2. Stable identities

Section	Identity

System identity	singleton
IP service	service name
Interface	interface name
Interface list	list name
Interface-list member	list + interface
IPv4/IPv6 address	family + address + interface
Managed firewall rule	валідний fwc:rule:<uuid>
Unmanaged firewall rule	exact canonical fingerprint
Static address-list entry	family + list + address
Routing table	table name
Routing rule	natural canonical key
Static route	family + table + destination + gateway set
VRRP instance	family + VRID + parent interface
Bridge	bridge name
Bridge port	bridge + interface
Bridge VLAN	bridge + normalized VLAN set
Switch chip	switch name + chip identity



---

22.3. Managed firewall rules

Для comment:

fwc:rule:<uuid>:<revision>

stable identity:

uuid

Зміни інших fields повертають:

MODIFIED

Зміна ordinal без semantic зміни:

MOVED


---

22.4. Unmanaged firewall rules

Для unmanaged rule:

1. обчислюється exact semantic fingerprint;


2. унікальний однаковий fingerprint у base і target означає ту саму rule;


3. зміна ordinal означає MOVED;


4. rule зі зміненим matcher/action не вважається автоматично MODIFIED;


5. вона повертається як REMOVED + ADDED.



Це виключає хибну відповідність.

Однакові дублікати:

зіставляються лише при однозначній sequence correspondence;

при неоднозначності отримують MATCH_CONFIDENCE_CONSERVATIVE;

MODIFIED не генерується.



---

22.5. Ordered diff

Для firewall rules використовується:

unique exact matches
→ bounded Myers sequence diff
→ conservative fallback

Limits:

maximum records per ordered section: 20 000
maximum edit-distance work: configurable bounded value

При перевищенні limit:

DIFF_COMPLEXITY_LIMIT

Controller не переходить до квадратичного unbounded алгоритму.


---

22.6. Configuration та observations

Приклади:

interface disabled=no → disabled=yes
    CONFIGURATION / MODIFIED

interface running=yes → running=no
    OBSERVATION / STATE_CHANGED

VRRP MASTER → BACKUP
    OBSERVATION / STATE_CHANGED

static route gateway changed
    CONFIGURATION / MODIFIED або remove+add

active default route disappeared
    OBSERVATION / STATE_CHANGED


---

22.7. Diff ordering

Результат сортується:

section schema order
→ domain
→ stable key
→ before ordinal
→ after ordinal
→ change type

Один і той самий input завжди створює однаковий ordered diff.


---

23. Capture operation state machine

QUEUED
  ↓
CONNECTING
  ↓
AUTHENTICATING
  ↓
READING_PASS_1
  ↓
CANONICALIZING_PASS_1
  ↓
READING_PASS_2
  ↓
VERIFYING_STABILITY
  ↓
PERSISTING
  ↓
COMPLETED

Помилкові переходи:

* → FAILED
* → CANCELED

23.1. Durable recovery

Після restart Controller:

Persisted state	Дія

QUEUED	повернути в queue
CONNECTING—READING_PASS_2	позначити failed як interrupted
VERIFYING_STABILITY	позначити failed
PERSISTING	перевірити наявність payloads і завершити або rollback DB transaction
COMPLETED	не змінювати


Незавершений API capture не продовжується після restart із середини.


---

23.2. Idempotency

Однаковий:

actor
+ idempotency_key
+ request hash

повертає той самий CaptureOperationId.

Повторне використання key з іншим request hash повертає:

IDEMPOTENCY_KEY_CONFLICT


---

24. Desktop behavior

24.1. Inventory tree

Site
 └── Node
      ├── Device
      └── Device

Показуються:

declared node kind
declared uplink mode
device reachability
RouterOS version
model
support state
last completed capture
configuration hash
observation hash
topology findings


---

24.2. Snapshot viewer

Sections:

System
Management services
Interfaces
Addresses
Interface lists
IPv4 firewall
IPv6 firewall
Routing
VRRP
Bridge/VLAN
Switch metadata
Capabilities
Compatibility findings

Configuration та observations показуються окремо.

Raw snapshot у звичайному GUI не показується.


---

24.3. VRRP node view

Для кожного VRRP group:

family
VRID
virtual addresses
member devices
configured priority
observed role
observation timestamp
consistency findings

Якщо capture members виконувалися неодночасно, GUI показує timestamp кожного observation.


---

24.4. Diff viewer

Фільтри:

configuration only
observations only
section
change type
device

REMOVED + ADDED не об’єднуються GUI у MODIFIED, якщо сервер не повернув таку класифікацію.


---

25. CHR testlab contract

CHR придатний для перевірки API, firewall, routing, multi-WAN та VRRP. CHR є віртуалізованою x86_64 редакцією RouterOS; поведінка фізичного switch ASIC не може бути повністю перевірена на CHR. Це означає, що switch-chip discovery потребує окремого hardware test target. 

25.1. Адресний простір testlab

Management: 192.0.2.0/24
WAN-A:      198.51.100.0/24
WAN-B:      203.0.113.0/24
LAN:        10.10.0.0/16
IPv6:       2001:db8::/32


---

25.2. Topologies

standalone-dual-stack

controller
   |
management
   |
CHR-R1
 ├─ WAN
 └─ LAN

Перевіряє:

IPv4/IPv6 firewall;

interface lists;

static address lists;

canonical hashing;

configuration/observation split.



---

multi-wan-failover

┌─ WAN-A
CHR-R1 ──────┤
             └─ WAN-B

Перевіряє:

routes із різними distance;

NAT per uplink;

active default route observations;

failover topology evidence.



---

multi-wan-pcc

Перевіряє:

routing tables;

routing rules;

mangle marks;

PCC matchers;

декілька active routes;

balanced topology evidence.



---

vrrp-active-passive

CHR-R1 ─┐
        ├─ shared LAN
CHR-R2 ─┘

Перевіряє:

однаковий VRID;

один master;

один backup;

role change;

незмінний configuration hash при failover;

змінений observation hash.



---

vrrp-split-master

VRID 10:
    R1 master
    R2 backup

VRID 20:
    R1 backup
    R2 master

Перевіряє відсутність помилкової global role classification.


---

25.3. CRS hardware test

До production support switches потрібен ізольований фізичний CRS target.

Мінімум:

один supported CRS model
один відомий switch-chip profile
bridge VLAN filtering
hardware offload enabled
management IP
input firewall

Hardware test перевіряє:

board і switch-chip discovery;

bridge port observations;

hardware-offload state;

management-plane firewall snapshot;

відсутність хибного твердження про transit IP firewall.


До проходження hardware test:

CRS support state = READ_ONLY_UNVALIDATED_HARDWARE


---

25.4. Fixture manifest

{
  "fixtureSchema": 1,
  "name": "vrrp-active-passive",
  "routerOsVersion": "exact-version",
  "devices": [
    {
      "name": "r1",
      "configurationScriptSha256": "hex"
    },
    {
      "name": "r2",
      "configurationScriptSha256": "hex"
    }
  ],
  "expectedSections": [],
  "expectedConfigurationHashes": {},
  "expectedTopologyFindings": []
}

Provisioning scripts знаходяться тільки в testlab і не використовують production RouterOS adapter.


---

26. Resource limits

Resource	Limit M1

Global simultaneous Device captures	16
Simultaneous captures per Device	1
Commands per RouterOS session	8 concurrent
Firewall rules per section	20 000
Static routes per family	50 000
Static address-list entries	250 000
Interfaces	10 000
Snapshot uncompressed size	256 MiB
gRPC section page	500 records
gRPC message target size	до 2 MiB
Capture queue	1 000 operations
Snapshot retry attempts	3


Перевищення повертає типізовану помилку. Controller не збільшує limit автоматично.


---

27. Error contracts

27.1. Protocol і transport

TLS_CERTIFICATE_INVALID
TLS_CERTIFICATE_EXPIRED
TLS_NAME_MISMATCH
TLS_PIN_MISMATCH
API_AUTHENTICATION_FAILED
API_PROTOCOL_ERROR
API_UNTAGGED_REPLY
API_DUPLICATE_ATTRIBUTE
API_TRAP
API_FATAL
API_TIMEOUT
API_RESPONSE_TOO_LARGE
API_WORD_TOO_LARGE
API_SENTENCE_TOO_LARGE
DEVICE_UNREACHABLE

RouterOS !trap містить category і message. Controller зберігає category, але зовнішній message проходить sanitization. 


---

27.2. Capture

CAPTURE_ALREADY_RUNNING
CAPTURE_CANCELED
CAPTURE_INTERRUPTED
CAPTURE_REQUIRED_SECTION_FAILED
SNAPSHOT_UNSTABLE
SNAPSHOT_TOO_LARGE
SNAPSHOT_PERSISTENCE_FAILED
CAPABILITY_PROFILE_UNKNOWN
UNSUPPORTED_ROUTEROS_VERSION


---

27.3. Topology

NODE_CARDINALITY_INVALID
DECLARED_KIND_CONTRADICTED
DECLARED_ROUTER_HAS_VRRP
VRRP_MEMBER_MISSING
VRRP_CONFIGURATION_MISMATCH
VRRP_VERSION_MISMATCH
VRRP_ROLE_INCONSISTENT
MULTIWAN_EVIDENCE_INSUFFICIENT
MULTIWAN_MODE_CONTRADICTED
SWITCH_HARDWARE_UNVALIDATED
INTERFACE_LIST_CYCLE


---

27.4. Diff

SNAPSHOTS_FROM_DIFFERENT_DEVICES
SNAPSHOT_SCHEMA_INCOMPATIBLE
DIFF_COMPLEXITY_LIMIT
SNAPSHOT_SECTION_UNAVAILABLE


---

28. Security requirements

1. RouterOS password шифрується до persistence.


2. Desktop отримує лише connection summary.


3. Controller не повертає username звичайному Viewer без потреби.


4. Raw snapshot недоступний за замовчуванням.


5. API login sentence не логуються.


6. RouterOS reply logging вимкнений за замовчуванням.


7. Debug fixture logging дозволений лише після redaction.


8. TLS SPKI pin має 32 bytes SHA-256.


9. Pin change створює audit event.


10. Internal CA profile змінюється лише Administrator.


11. Capture permission не надає permission змінювати inventory.


12. Snapshot export не включає credentials.


13. gRPC request має correlation ID.


14. Усі mutation RPC використовують idempotency key.


15. Command allowlist не конфігурується з PostgreSQL.


16. Compatibility manifest входить до signed release artifact.


17. Controller service account PostgreSQL не має UPDATE/DELETE на completed snapshots.


18. CHR runner не має маршруту до production management network.




---

29. Test strategy

29.1. Protocol tests

all word-length boundaries
fragmented length prefix
fragmented word
multiple sentences in one frame
empty sentence
!re
!done
!empty
!trap
!fatal
interleaved tags
/cancel
connection close mid-word
invalid UTF-8
duplicate attributes
oversized word
oversized sentence


---

29.2. Canonicalization tests

API attribute order changes
unordered record order changes
firewall order changes
IPv4 normalization
IPv6 normalization
prefix normalization
duration normalization
port interval normalization
interface-list include/exclude
duplicate firewall rules
unknown property
missing optional property
dynamic vs static records

Обов’язковий property test:

Canonicalize(Canonicalize(x)) == Canonicalize(x)


---

29.3. Hash tests

same known configuration → same configuration_hash
runtime state change → same configuration_hash
runtime state change → different observation_hash
RouterOS version change → different capability_hash
unknown property change → different compatibility_material_hash
any component change → different snapshot_hash
compression change → same payload hash


---

29.4. Stable-read tests

no change between passes → completed
firewall rule changed between passes → retry
route changed between passes → retry
interface list changed between passes → retry
configuration changes on all attempts → SNAPSHOT_UNSTABLE
connection loss in pass 1 → failed attempt
connection loss in pass 2 → failed attempt
RouterOS reboot between passes → retry
cancellation → no completed snapshot


---

29.5. Diff tests

identical snapshots
managed rule modified
managed rule moved
unmanaged exact rule moved
unmanaged rule modified → remove + add
duplicate unmanaged rules
address-list entry added
interface-list member removed
VRRP role changed
active route changed
unknown property changed
section unsupported → supported


---

29.6. Persistence tests

empty database migration
upgrade from bootstrap migration
snapshot atomic persistence
content deduplication
completed capture immutability
operation idempotency
optimistic concurrency
controller restart recovery
payload compression/decompression
hash verification after read


---

30. Acceptance criteria M1

M1 завершений лише коли доведено:

1. Controller підключається лише через API-SSL.


2. Certificate validation не можна вимкнути configuration flag.


3. Desktop не отримує RouterOS credentials.


4. Production RouterOS command paths є закритим allowlist.


5. У production assembly відсутні write commands.


6. Усі /print використовують явну .proplist.


7. Два capture без configuration changes мають однаковий configuration_hash.


8. VRRP role change не змінює configuration_hash.


9. VRRP role change змінює observation_hash.


10. Active default route change не створює configuration drift.


11. Static route change створює configuration drift.


12. Firewall rule order зберігається.


13. Unmanaged rule modification не отримує недоведену identity.


14. Unknown property не зникає.


15. Unknown property переводить profile у NEEDS_REVALIDATION.


16. Нестабільне подвійне читання не створює complete snapshot.


17. Часткова DB failure не залишає completed capture.


18. Node capture не приховує недоступний VRRP member.


19. Split-master VRRP відображається як role vector.


20. Multi-WAN evidence не підміняється автоматичною конфігурацією.


21. CHR standalone acceptance пройдений.


22. CHR failover acceptance пройдений.


23. CHR PCC acceptance пройдений.


24. CHR VRRP active/passive acceptance пройдений.


25. CHR split-master acceptance пройдений.


26. Physical CRS read-only test пройдений для хоча б одного зафіксованого hardware profile.


27. Snapshot viewer працює з server-side canonical data.


28. Diff viewer не обчислює semantic diff локально.


29. Fault-injection suite не залишає pending API commands.


30. Build, tests і migrations не змінюють Git working tree.




---

31. Уточнення Initial Issue Set

Issue	Нормативна зміна

M1-01	Domain містить Site, Node, Device; Uplink, ZoneBinding, VrrpGroup, VrrpMember не є persisted aggregates
M1-02	Capability та snapshot залишаються value objects
M1-03	Таблиці topology замінені на capture_operations, snapshot_captures, snapshot_payloads
M1-05	DiscoverDevice прибрано; повне discovery виконує StartCapture
M1-17	Capability profile обчислюється з snapshot і manifest
M1-18	Topology є pure projection, а не окремим persisted aggregate
M1-25	API містить ValidateDeviceConnection, а не окремий full discovery RPC
M1-26	Snapshot sections передаються paged generic canonical records
M1-30	Standalone CHR залишається обов’язковим
M1-31	Multi-WAN CHR залишається обов’язковим
M1-32	VRRP CHR залишається обов’язковим
Нове уточнення	CRS production support потребує physical hardware test; CHR цього не замінює



---

32. Порядок реалізації

1. RouterOS word codec
2. RouterOS sentence parser
3. Tagged session
4. TLS і authentication
5. Закритий command allowlist
6. Property profiles
7. Raw snapshot schema
8. Section readers
9. Canonicalization primitives
10. Menu-specific canonicalizers
11. Hash contracts
12. PostgreSQL capture schema
13. Stable-read coordinator
14. Topology projection
15. Semantic diff
16. gRPC contracts
17. Desktop inventory
18. Desktop snapshot viewer
19. Desktop diff viewer
20. CHR acceptance
21. Physical CRS acceptance
22. Fault injection

Жодний RouterOS write-path не створюється до повного завершення цього порядку.


---

33. Наступний нормативний документ

MikroTik Firewall Controller
RouterOS Read Adapter Specification v0.1

Він повинен зафіксувати:

точний binary word codec
sentence parser state machine
tagged command executor
TLS session lifecycle
типізований command registry
property profiles для кожного menu
RouterOS value parsers
trap classification
redaction registry
per-section record limits
protocol test vectors
sanitized RouterOS fixtures