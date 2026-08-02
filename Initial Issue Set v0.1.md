MikroTik Firewall Controller

Initial Issue Set v0.1

Дата: 3 серпня 2026 року
Статус: нормативний набір початкових задач


---

1. Призначення

Документ визначає атомарні GitHub Issues для двох перших milestones:

M0 — Repository Bootstrap
M1 — Read-Only Vertical Slice

Після завершення M1 система повинна виконувати наскрізний read-only сценарій:

Desktop
  → gRPC
  → Controller
  → RouterOS API-SSL
  → Discovery
  → Stable snapshot
  → Canonicalization
  → Hash
  → PostgreSQL
  → Semantic diff
  → Desktop

У межах M0 і M1 production-код не повинен змінювати конфігурацію RouterOS.


---

2. Правила формування Issues

Кожний issue повинен:

1. Мати один перевірюваний результат.


2. Завершуватися одним pull request.


3. Містити реалізацію, тести й необхідну документацію.


4. Не залишати заглушок, TODO, NotImplementedException або вимкнених тестів.


5. Не розширювати scope без нового issue.


6. Не додавати залежностей без обґрунтування в PR.


7. Не змішувати функціональні зміни з масовим форматуванням.


8. Не містити окремого issue «додати тести» для вже реалізованої функції.


9. Не створювати довільних Manager, Helper, Utils, Common або Shared.


10. Не додавати RouterOS write path до завершення окремого нормативного етапу.



Логічний ідентифікатор наводиться в заголовку issue:

[M0-01] Initialize repository governance
[M1-06] Implement RouterOS word codec

Фактичний GitHub issue number визначається GitHub.


---

3. Definition of Ready

Issue готовий до реалізації, коли:

визначено результат;

визначено acceptance criteria;

завершено всі залежності;

немає невирішеного архітектурного рішення;

доступні потрібні синтетичні fixtures;

зрозуміло, які assemblies змінюються;

визначено security impact;

визначено, чи потрібна migration;

scope не містить прихованої write-функціональності.



---

4. Загальний Definition of Done

Кожний issue вважається виконаним лише за одночасного виконання таких умов:

1. dotnet restore --locked-mode успішний.


2. dotnet build -c Release завершується без warnings.


3. Усі релевантні unit, architecture та integration tests проходять.


4. Новий код покритий тестами на нормальні, граничні та помилкові сценарії.


5. CancellationToken передається через усі нові I/O-виклики.


6. Усі retries, queues, buffers і concurrency мають обмеження.


7. У логах, fixtures і test output немає credentials або sensitive payloads.


8. Public contracts документовані.


9. Зміна не порушує assembly dependency rules.


10. Git working tree після build і tests чистий.


11. PR title відповідає Conventional Commits.


12. Acceptance criteria issue підтверджені в PR.


13. Жоден production assembly не містить довільного RouterOS command execution.


14. Для database change додано migration та PostgreSQL integration test.


15. Для security-sensitive change виконано окреме review.




---

5. Порядок виконання

M0-01
  ↓
M0-02
  ↓
M0-03
  ├──────────────┬──────────────┐
  ↓              ↓              ↓
M0-04          M0-05          M0-07
                 ↓
               M0-06
  └──────────────┴──────────────┘
                 ↓
               M0-08
                 ↓
               M0-09
                 ↓
               M0-10
                 ↓
              M0 CLOSED

Після M0 паралельно запускаються два основні потоки:

Domain/Persistence:
M1-01 → M1-02 → M1-03 → M1-04 → M1-05

RouterOS Protocol:
M1-06 → M1-07 → M1-08 → M1-09 → M1-10

Після об’єднання потоків:

Discovery:
M1-11 … M1-16
      ↓
Capabilities/Topology:
M1-17 → M1-18
      ↓
Snapshots:
M1-19 → M1-20 → M1-21 → M1-22 → M1-23
      ↓
Diff:
M1-24
      ↓
Controller API:
M1-25 → M1-26
      ↓
Desktop:
M1-27 → M1-28 → M1-29
      ↓
CHR Acceptance:
M1-30 → M1-31 → M1-32 → M1-33
      ↓
M1-34
      ↓
M1 CLOSED


---

6. Milestone M0 — Repository Bootstrap

[M0-01] Initialize repository governance

Labels: type:chore, area:security
Залежності: немає
PR title: chore(repo): initialize repository governance

Результат

Створений Git-репозиторій із базовими правилами роботи та захистом main.

Scope

.gitignore;

.gitattributes;

README.md;

CONTRIBUTING.md;

SECURITY.md;

CHANGELOG.md;

pull request template;

issue templates;

CODEOWNERS;

базові labels;

milestones M0 і M1;

branch protection.


Acceptance criteria

1. Основна гілка має назву main.


2. Direct push і force push у main заборонені.


3. Merge дозволений лише через pull request.


4. Увімкнено linear history і squash merge.


5. PR template містить:

мету;

scope;

ризики;

тести;

database impact;

contract impact;

security impact;

rollback.



6. CODEOWNERS охоплює security, RouterOS, deployment, migrations і CI.


7. Створені тільки labels, визначені bootstrap plan.


8. У репозиторії немає secrets, binaries або generated artifacts.


9. README.md не містить недостовірної інформації про ще не реалізовані функції.




---

[M0-02] Pin .NET toolchain and package management

Labels: type:build, area:application
Залежності: M0-01
PR title: build(dotnet): add pinned toolchain and package management

Результат

Створене детерміноване середовище збірки.

Scope

global.json;

Directory.Build.props;

Directory.Packages.props;

NuGet.config;

.editorconfig;

.config/dotnet-tools.json;

package lock policy.


Acceptance criteria

1. SDK зафіксований конкретною версією.


2. Preview SDK заборонений.


3. Nullable reference types увімкнені.


4. Warnings обробляються як errors.


5. Deterministic build увімкнений.


6. Central Package Management активний.


7. Версії NuGet packages відсутні у .csproj.


8. Floating package versions відсутні.


9. packages.lock.json комітиться.


10. Restore у locked mode проходить на чистому середовищі.


11. Code style перевіряється під час build.


12. Глобальні NoWarn відсутні.




---

[M0-03] Create solution and enforce project references

Labels: type:feature, area:application
Залежності: M0-02
PR title: feat(skeleton): add solution and project boundaries

Результат

Створена solution із нормативними assemblies та дозволеними залежностями.

Production projects

Mfc.Domain
Mfc.Application
Mfc.RouterOs
Mfc.Infrastructure
Mfc.Contracts
Mfc.Controller
Mfc.Desktop

Test projects

Mfc.UnitTests
Mfc.IntegrationTests
Mfc.RouterOs.IntegrationTests

Acceptance criteria

1. Solution збирається в Release.


2. Mfc.Domain не має project references.


3. Mfc.Application залежить лише від Mfc.Domain.


4. Mfc.Infrastructure залежить лише від Application і Domain.


5. Mfc.RouterOs залежить лише від Application і Domain.


6. Mfc.Desktop залежить лише від Mfc.Contracts.


7. Mfc.Controller є composition root.


8. Відсутні проєкти Common, Shared, Utils або Core.


9. Відсутня domain чи application logic у composition root.


10. Відсутні RouterOS write namespaces.


11. Порожні assemblies не містять фіктивної production-функціональності.




---

[M0-04] Add architecture boundary tests

Labels: type:test, area:application
Залежності: M0-03
PR title: test(architecture): enforce assembly dependency rules

Результат

Порушення архітектурних меж автоматично блокуються CI.

Acceptance criteria

Architecture tests перевіряють:

Domain !→ Application
Domain !→ Infrastructure
Domain !→ RouterOs
Domain !→ Controller
Domain !→ Desktop

Application !→ Infrastructure
Application !→ RouterOs
Application !→ Controller
Application !→ Desktop

Infrastructure !→ Controller
RouterOs !→ Infrastructure

Desktop !→ Domain
Desktop !→ Application
Desktop !→ Infrastructure
Desktop !→ RouterOs

Додатково:

1. Domain не використовує EF Core, ASP.NET Core, Avalonia, gRPC або Npgsql.


2. Desktop не містить RouterOS protocol types.


3. Application не використовує IServiceProvider.


4. Жоден production project не посилається на test project.


5. Виявлення порушення доведене негативним тестом або контрольованою fixture.


6. Architecture tests не залежать від порядку виконання.




---

[M0-05] Add health-only controller host

Labels: type:feature, area:controller
Залежності: M0-03
PR title: feat(controller): add health-only controller host

Результат

Controller запускається як окремий process і надає захищений health endpoint через gRPC.

Scope

ASP.NET Core host;

gRPC health contract;

configuration binding;

configuration validation;

TLS;

structured logging;

graceful shutdown.


Acceptance criteria

1. Controller запускається без desktop-клієнта.


2. gRPC health check повертає process health.


3. Production bind без TLS блокується.


4. Development authentication дозволена лише на loopback.


5. Startup із некоректною configuration завершується помилкою.


6. Shutdown завершує активні запити в межах заданого deadline.


7. Не використовується async void.


8. Відсутні fire-and-forget tasks.


9. Health endpoint не розкриває secrets або stack traces.


10. Controller не містить RouterOS client.




---

[M0-06] Add desktop controller connection shell

Labels: type:feature, area:desktop
Залежності: M0-05
PR title: feat(desktop): add controller connection shell

Результат

Desktop-клієнт підключається до controller і показує фактичний стан з’єднання.

Acceptance criteria

1. Avalonia application запускається на Windows.


2. GUI не виконує network I/O в UI thread.


3. Endpoint controller задається через configuration.


4. Health check має timeout і cancellation.


5. GUI відображає стани:

Connecting;

Connected;

Disconnected;

AuthenticationFailed;

TlsError.



6. Повторне підключення bounded і не створює паралельних циклів.


7. Desktop не має залежності від Domain, Application, Infrastructure або RouterOs.


8. Desktop не зберігає RouterOS credentials.


9. Закриття application коректно завершує gRPC channel.




---

[M0-07] Add PostgreSQL bootstrap persistence

Labels: type:feature, area:persistence, risk:high
Залежності: M0-03
PR title: feat(persistence): add PostgreSQL bootstrap migration

Результат

Controller використовує PostgreSQL і має контрольований migration workflow.

Bootstrap tables

controller_instances
schema_metadata
audit_events
encrypted_secrets
idempotency_records

Acceptance criteria

1. PostgreSQL є єдиною підтримуваною production database.


2. SQLite не використовується.


3. Початкова migration застосовується до порожньої БД.


4. Повторна перевірка schema не створює змін.


5. Controller не виконує migration автоматично під час normal startup.


6. Підтримується режим:



Mfc.Controller --migrate-only

7. Controller не запускається при відсутній обов’язковій migration.


8. Timestamps зберігаються в UTC.


9. Secrets не мають plaintext column.


10. Audit table не має update/delete application path.


11. Integration tests працюють із реальною PostgreSQL.


12. Connection string не потрапляє в logs.




---

[M0-08] Add deterministic CI pipelines

Labels: type:ci, area:security, risk:high
Залежності: M0-04, M0-05, M0-06, M0-07
PR title: ci: add deterministic validation pipelines

Результат

Кожний pull request проходить однаковий контрольований build і test pipeline.

Acceptance criteria

CI виконує:

restore --locked-mode
format verification
Release build
unit tests
architecture tests
PostgreSQL integration tests
package vulnerability scan

Додатково:

1. Linux і Windows jobs розділені.


2. Desktop build виконується на Windows.


3. GitHub Actions зафіксовані commit SHA.


4. Default permissions дорівнюють contents: read.


5. Untrusted PR не виконується на privileged runner.


6. Build artifacts мають обмежений retention.


7. CI не використовує production secrets.


8. CI failure блокує merge.


9. Generated files не змінюють working tree.


10. Build і test commands збігаються з локальною документацією.




---

[M0-09] Add isolated CHR testlab skeleton

Labels: type:test, area:testlab, area:routeros
Залежності: M0-08
PR title: test(routeros): add isolated CHR lab skeleton

Результат

Створена безпечна основа для майбутніх RouterOS integration tests.

Acceptance criteria

1. У Git відсутні CHR images і license files.


2. Створено manifest.example.json.


3. Визначені topology directories:

standalone;

multi-WAN failover;

multi-WAN balanced;

VRRP active/passive;

VRRP split-master.



4. Runner не має маршруту до production network.


5. Test CA генерується окремо для кожного test environment.


6. Test credentials не повторно використовуються.


7. Визначено reset procedure.


8. Визначено cleanup procedure.


9. Production RouterOS exports заборонені.


10. Test fixtures використовують синтетичні addresses і names.


11. RouterOS integration workflow поки не виконує production code write-операцій.




---

[M0-10] Record initial ADRs and development documentation

Labels: type:docs, area:application
Залежності: M0-03, M0-05, M0-07, M0-09
PR title: docs(architecture): record initial architecture decisions

Результат

Критичні архітектурні рішення зафіксовані до початку функціональної реалізації.

Обов’язкові ADR

0001-modular-monolith
0002-routeros-api-ssl
0003-node-deployment-atomicity
0004-postgresql-source-of-truth
0005-no-direct-desktop-routeros-access

Acceptance criteria

1. Кожний ADR має статус Accepted.


2. ADR містить context, decision і consequences.


3. Документація локального середовища відтворювана.


4. Команди build і tests перевірені.


5. Database migration workflow документований.


6. CHR lab isolation документована.


7. README не дублює нормативне ТЗ.


8. Відсутні інструкції, що передбачають production credentials.


9. M0 acceptance checklist повністю пройдений.




---

7. Milestone M0 — критерії закриття

M0 закривається лише коли:

виконані M0-01—M0-10;

main захищена;

CI проходить;

desktop підключається до controller;

controller перевіряє PostgreSQL schema;

production assemblies не містять RouterOS client;

CHR runner ізольований;

усі ADR прийняті;

tag v0.1.0-bootstrap створюється тільки після повного acceptance review.



---

8. Milestone M1 — Read-Only Vertical Slice

[M1-01] Implement inventory domain model

Labels: type:feature, area:domain
Залежності: M0
PR title: feat(inventory): add inventory domain model

Результат

Domain містить мінімальну модель корпоративної мережі.

Типи

Site
Node
Device
Uplink
ZoneBinding
VrrpGroup
VrrpMember

Acceptance criteria

1. Site.code валідований і незмінний.


2. ROUTER node не може містити більше одного device.


3. VRRP node не може вважатися валідним із менш ніж двома devices.


4. Management endpoint типізований.


5. IP addresses не зберігаються як довільні strings.


6. NodeKind, UplinkMode, DeviceRole є закритими enum/value types.


7. Domain не залежить від persistence або RouterOS DTO.


8. Інваріанти покриті unit tests.


9. Немає generic base entity.


10. Немає setters, що дозволяють обійти aggregate invariants.




---

[M1-02] Implement snapshot and capability domain types

Labels: type:feature, area:domain
Залежності: M1-01
PR title: feat(snapshot): add snapshot and capability domain types

Результат

Визначені типи для результатів discovery, capability і snapshot metadata.

Типи

SnapshotId
SnapshotStatus
ConfigurationHash
ObservationHash
SnapshotHash
CapabilityHash
CapabilityProfile
RouterOsVersion
SupportState
TopologyObservation

Acceptance criteria

1. Hash має фіксований алгоритм і довжину.


2. Hash не створюється з невалідного текстового значення.


3. Configuration і runtime observations розділені.


4. SupportState має значення:

SUPPORTED;

READ_ONLY;

NEEDS_REVALIDATION;

UNSUPPORTED.



5. Snapshot не містить RouterOS credentials.


6. Domain не зберігає raw API payload.


7. Equality value objects детермінована.


8. Серіалізація не впливає на domain equality.


9. Усі граничні випадки покриті тестами.




---

[M1-03] Add inventory and snapshot persistence schema

Labels: type:feature, area:persistence
Залежності: M1-01, M1-02, M0-07
PR title: feat(persistence): add inventory and snapshot schema

Результат

PostgreSQL зберігає inventory, topology metadata та immutable snapshots.

Таблиці

sites
nodes
devices
uplinks
zone_bindings
vrrp_groups
vrrp_members
device_capabilities
snapshots

Acceptance criteria

1. Migration застосовується до bootstrap schema.


2. Management endpoint унікальний серед active devices.


3. sites.code має unique constraint.


4. (node_id, zone_key) має unique constraint.


5. (node_id, family, vrid, interface_key) має unique constraint.


6. Snapshots не оновлюються після створення.


7. Raw і canonical payloads зберігаються окремо.


8. Snapshot metadata містить schema version.


9. Cascade delete не видаляє snapshots або audit history.


10. Реалізації persistence не використовують generic repository.


11. PostgreSQL integration tests перевіряють constraints.


12. Migration із попереднього schema state протестована.




---

[M1-04] Add secure RouterOS connection profiles

Labels: type:feature, area:security, area:persistence, risk:high
Залежності: M1-03, M0-07
PR title: feat(security): add RouterOS connection profiles

Результат

Controller безпечно зберігає connection metadata і read-only credentials.

Connection profile

management host
API-SSL port
credential reference
trusted CA reference або certificate pin
connection timeout
command timeout
maximum response size

Acceptance criteria

1. Password шифрується до запису в PostgreSQL.


2. Desktop ніколи не отримує password.


3. Password не присутній у logs, audit або exception details.


4. Certificate validation bypass відсутній.


5. Підтримується internal CA trust.


6. Підтримується явний certificate fingerprint pin.


7. Зміна pin є audit event.


8. Connection profile не містить довільного RouterOS command.


9. Rotation замінює secret без зміни device identity.


10. Unit tests перевіряють redaction.


11. Integration tests доводять відсутність plaintext у БД.




---

[M1-05] Define read-only application ports and use cases

Labels: type:feature, area:application
Залежності: M1-01—M1-04
PR title: feat(application): add read-only inventory use cases

Результат

Application layer визначає повний read-only сценарій без залежності від RouterOS implementation.

Use cases

CreateSite
CreateNode
RegisterDevice
UpdateConnectionProfile
DiscoverDevice
CaptureSnapshot
GetSnapshot
ListSnapshots
CompareSnapshots

Acceptance criteria

1. RouterOS access представлений application port.


2. Persistence представлена окремими вузькими ports.


3. IServiceProvider не використовується.


4. Expected failures повертаються типізованими errors.


5. Усі I/O methods приймають CancellationToken.


6. Use case не повертає persistence entity.


7. Device discovery не змінює RouterOS.


8. Capture snapshot є idempotent щодо однакового snapshot hash.


9. Authorization boundary визначений, але не дублює controller logic.


10. Application tests використовують hand-written fakes.




---

[M1-06] Implement RouterOS word-length codec

Labels: type:feature, area:routeros
Залежності: M0
PR title: feat(routeros): implement API word-length codec

Результат

Реалізовано коректне кодування та декодування довжини RouterOS API words.

Acceptance criteria

1. Підтримані всі стандартні формати довжини RouterOS API.


2. Перевірені boundary values для кожної довжини prefix.


3. Decoder підтримує fragmented input.


4. Некоректний prefix повертає protocol error.


5. Надмірний word length відхиляється до allocation.


6. Maximum word size конфігурований.


7. Відсутні unbounded allocations.


8. Encoding і decoding взаємно зворотні для всього дозволеного діапазону.


9. Property-based tests перевіряють round trip.


10. Little-endian assumptions не залежать від host architecture.




---

[M1-07] Implement RouterOS sentence encoder and parser

Labels: type:feature, area:routeros
Залежності: M1-06
PR title: feat(routeros): implement API sentence codec

Результат

Реалізований streaming parser і encoder RouterOS API sentences.

Acceptance criteria

1. Parser обробляє fragmented TCP frames.


2. Parser обробляє декілька sentences в одному buffer.


3. Empty word коректно завершує sentence.


4. Attributes зберігаються без припущення про порядок.


5. Duplicate attribute policy визначена й протестована.


6. Invalid UTF-8 обробляється детерміновано.


7. Oversized sentence припиняється до повного buffering.


8. Encoder не приймає null command words.


9. Parser не використовує blocking I/O.


10. Test fixtures охоплюють:

!re;

!done;

!empty;

!trap;

!fatal;

malformed sentence;

connection close mid-word.





---

[M1-08] Implement asynchronous tagged RouterOS session

Labels: type:feature, area:routeros, risk:high
Залежності: M1-07
PR title: feat(routeros): implement tagged API session

Результат

Одна API connection підтримує контрольовані паралельні read-команди.

Acceptance criteria

1. Кожна команда отримує унікальний .tag.


2. Replies маршрутизуються відповідному caller.


3. Out-of-order tagged replies обробляються правильно.


4. Untagged unexpected reply створює protocol error.


5. /cancel підтриманий для активної tagged command.


6. Timeout завершує command і звільняє її state.


7. Connection close завершує всі pending commands.


8. Pending command map має bounded size.


9. Read loop єдиний для connection.


10. Write serialization не допускає interleaving words.


11. Disposal є idempotent.


12. Session не містить reconnect loop.


13. Немає fire-and-forget tasks.


14. Race conditions перевірені stress tests.




---

[M1-09] Implement authenticated TLS RouterOS connection

Labels: type:feature, area:routeros, area:security, risk:high
Залежності: M1-04, M1-08
PR title: feat(routeros): add authenticated API-SSL connection

Результат

Controller створює перевірену TLS-сесію і виконує RouterOS authentication.

Acceptance criteria

1. Використовується лише API-SSL.


2. Plain API transport у production code відсутній.


3. Certificate chain або configured pin перевіряється.


4. Hostname/IP SAN перевіряється.


5. Expired certificate відхиляється.


6. Certificate mismatch має окремий error code.


7. Login payload не потрапляє в logs.


8. Authentication timeout обмежений.


9. Authentication failure не запускає нескінченний retry.


10. Secret очищується з тимчасових buffers настільки, наскільки дозволяє runtime.


11. Session повертається caller лише після успішного login.


12. Reconnect виконується тільки application-level policy.


13. Tests використовують локальну test CA.




---

[M1-10] Add typed allowlisted RouterOS read executor

Labels: type:feature, area:routeros
Залежності: M1-09, M1-05
PR title: feat(routeros): add typed read command executor

Результат

Discovery використовує тільки типізовані read-команди з явним allowlist.

Acceptance criteria

1. Command paths є compile-time constants.


2. Caller не може передати довільний RouterOS path.


3. .proplist задається явно для кожної команди.


4. Команди конфігураційного запису відсутні.


5. Дозволені лише read operations і /cancel.


6. !trap перетворюється на типізований error.


7. !fatal інвалідовує session.


8. Unknown attributes зберігаються в окремому raw property bag.


9. Sensitive properties мають централізовану redaction policy.


10. Query filters не формуються з raw UI input.


11. Architecture test блокує появу Write namespace.


12. Allowlist покритий unit tests.




---

[M1-11] Implement system and service discovery

Labels: type:feature, area:routeros
Залежності: M1-10
PR title: feat(discovery): read RouterOS system metadata

Результат

Controller читає базову ідентифікацію та management-service metadata пристрою.

Дані

system identity
RouterOS version
architecture
board/model
serial number, якщо доступний
packages
system clock
API-SSL service state
API-SSL port
assigned certificate
allowed source prefixes

Acceptance criteria

1. Discovery не використовує /export.


2. show-sensitive не використовується.


3. Відсутні user passwords і private keys.


4. Missing optional property не руйнує discovery.


5. Unsupported property зберігається в raw property bag.


6. Runtime uptime не входить у configuration hash.


7. API-SSL state доступний validator.


8. Sanitized snapshot fixture додана.


9. CHR smoke test проходить.




---

[M1-12] Implement interface and address discovery

Labels: type:feature, area:routeros
Залежності: M1-10
PR title: feat(discovery): read interfaces and address bindings

Результат

Controller читає фізичні, логічні інтерфейси, IP addresses та interface lists.

Дані

interfaces
interface type
running/disabled state
MAC
IPv4 addresses
IPv6 addresses
interface lists
interface list members
list include/exclude relationships
resolved list membership

Acceptance criteria

1. IPv4 та IPv6 не змішуються.


2. CIDR нормалізується.


3. Dynamic addresses відокремлюються від static.


4. Resolved interface-list membership детермінований.


5. Include/exclude cycles виявляються.


6. Missing interface reference створює validation finding.


7. Runtime running state не входить у configuration hash.


8. Configuration і observations розділені.


9. Tests охоплюють nested include/exclude.


10. Порядок API replies не впливає на resolved membership.




---

[M1-13] Implement firewall and address-list discovery

Labels: type:feature, area:routeros
Залежності: M1-10
PR title: feat(discovery): read firewall filters and address lists

Результат

Controller читає IPv4/IPv6 filter rules і address lists без інтерпретаційної втрати підтримуваних полів.

Acceptance criteria

1. IPv4 і IPv6 filter menus читаються окремо.


2. Порядок rules зберігається.


3. .id не використовується як persistent identity.


4. Counters не входять у configuration hash.


5. Disabled state зберігається.


6. Comments зберігаються.


7. Dynamic address-list entries відокремлені від static.


8. Timeout entries позначаються як dynamic/runtime.


9. Unknown matchers зберігаються в raw property bag.


10. fwc: ownership marker розпізнається, але нічого не змінює.


11. FastTrack rule не втрачає action-specific properties.


12. Fixtures містять unmanaged і fwc: rules.




---

[M1-14] Implement routing and firewall-dependency discovery

Labels: type:feature, area:routeros
Залежності: M1-10
PR title: feat(discovery): read routing and firewall dependencies

Результат

Controller читає дані, необхідні для подальшого аналізу multi-WAN.

Дані

routing tables
routes
routing rules
NAT
RAW
Mangle
packet/connection/routing marks
IP settings
reverse-path filter mode

Acceptance criteria

1. Active route state відокремлений від route configuration.


2. Route distance, scope і target-scope типізовані.


3. Routing table references валідовані.


4. NAT, RAW і Mangle зберігають порядок.


5. Controller не компілює і не змінює ці facilities.


6. PCC/nth/random matchers позначаються як unsupported-for-editing.


7. rp-filter доступний topology validator.


8. Dynamic routes не змішуються зі static configuration.


9. Runtime gateway reachability входить до observations.


10. Snapshot не містить credentials VPN peers.




---

[M1-15] Implement VRRP discovery

Labels: type:feature, area:routeros
Залежності: M1-10, M1-12
PR title: feat(discovery): read VRRP configuration and state

Результат

Controller визначає всі VRRP groups і роль кожного пристрою для кожного VRID.

Acceptance criteria

1. VRRP groups ідентифікуються за family, VRID та interface.


2. Virtual addresses типізовані.


3. Priority і owner semantics зберігаються.


4. Observed state зберігається окремо від configuration.


5. Один device може бути master одного VRID і backup іншого.


6. Split-master topology не спрощується до одного global master.


7. INIT і unknown state підтримані.


8. Role change не змінює configuration hash.


9. Role change змінює observation hash.


10. Fixtures охоплюють multiple VRIDs і split-master.




---

[M1-16] Implement bridge, VLAN and switch metadata discovery

Labels: type:feature, area:routeros
Залежності: M1-10, M1-12
PR title: feat(discovery): read bridge and switch metadata

Результат

Controller читає topology context MikroTik switches без керування transit ACL.

Дані

bridges
bridge ports
VLAN filtering
bridge VLAN table
hardware offload state
switch-chip model, якщо доступний
L2/L3 role indicators

Acceptance criteria

1. Hardware offload state є observation.


2. Bridge/VLAN configuration має окремий configuration representation.


3. Controller не припускає проходження hardware-switched traffic через IP firewall.


4. SwOS не обробляється як RouterOS API target.


5. Unknown switch chip не отримує write capability.


6. Transit ACL data не компілюється.


7. Switch metadata не створює RouterOS write path.


8. Fixtures охоплюють router, CRS та невідомий board type.




---

[M1-17] Implement RouterOS capability profile

Labels: type:feature, area:routeros, area:application
Залежності: M1-11—M1-16
PR title: feat(capabilities): add RouterOS capability profile

Результат

Кожний пристрій отримує детермінований capability profile і support state.

Acceptance criteria

1. Capability визначається не лише номером RouterOS version.


2. Compatibility manifest має versioned schema.


3. Manifest враховує:

RouterOS version;

architecture;

board class;

required menus;

required properties;

known incompatibilities.



4. Unknown version отримує NEEDS_REVALIDATION.


5. RouterOS 6 отримує лише read-only support.


6. Testing/development channel не отримує write support.


7. Capability hash детермінований.


8. Runtime observations не входять у capability hash.


9. Зміна capability invalidates cached topology validation.


10. Manifest fixtures покриті tests.




---

[M1-18] Implement node topology validation

Labels: type:feature, area:application, area:domain
Залежності: M1-17, M1-01
PR title: feat(topology): validate RouterOS node topology

Результат

Controller перевіряє відповідність налаштованого node фактичній RouterOS topology.

Acceptance criteria

1. Controller не сканує мережу автоматично.


2. Device спочатку явно прив’язується до node.


3. ROUTER node із двома devices відхиляється.


4. VRRP node із одним device відхиляється.


5. VRRP groups порівнюються між усіма members.


6. Version mismatch одного VRID створює blocker.


7. Uplink mode перевіряється за routing/NAT/Mangle observations.


8. FAILOVER, BALANCED і MIXED не визначаються лише кількістю interfaces.


9. SWITCH node не отримує transit firewall capability.


10. Невпевнена класифікація повертає explicit finding, а не припущення.


11. Validation result детермінований.


12. Тести охоплюють standalone, failover, PCC і split-master VRRP.




---

[M1-19] Implement stable-read snapshot coordinator

Labels: type:feature, area:application, area:routeros
Залежності: M1-11—M1-18
PR title: feat(snapshot): add stable-read coordinator

Результат

Snapshot приймається лише за відсутності конфігураційних змін під час читання.

Алгоритм

read configuration fingerprints
→ read complete discovery dataset
→ read configuration fingerprints again
→ compare
→ accept або bounded retry

Acceptance criteria

1. Runtime state не використовується для stable-read decision.


2. Critical configuration menus мають fingerprint.


3. Retry count обмежений.


4. Retry використовує bounded delay.


5. Cancellation припиняє весь snapshot.


6. Після вичерпання retry повертається SNAPSHOT_UNSTABLE.


7. Частковий snapshot не зберігається як complete.


8. Кожна RouterOS command має timeout.


9. Parallel reads мають bounded concurrency.


10. Controlled concurrent configuration change в CHR виявляється.


11. Coordinator не містить write command.




---

[M1-20] Implement raw snapshot assembly and redaction

Labels: type:feature, area:application, area:security
Залежності: M1-19
PR title: feat(snapshot): assemble and redact raw snapshots

Результат

Усі discovery results формуються в один versioned raw snapshot без secrets.

Acceptance criteria

1. Raw snapshot має schema version.


2. Кожний section містить source menu і capture status.


3. Partial section error не маскується.


4. Passwords, tokens, private keys і sensitive fields відсутні.


5. Redaction централізована.


6. Unknown properties зберігаються після redaction.


7. Raw snapshot не містить API login sentence.


8. Capture timestamps зберігаються окремо від configuration data.


9. Serialization детермінована в межах raw schema.


10. Максимальний snapshot size обмежений.


11. Oversized snapshot повертає типізовану помилку.


12. Sanitized fixtures перевіряються secret scanner test.




---

[M1-21] Implement canonicalization primitives

Labels: type:feature, area:application
Залежності: M1-20
PR title: feat(snapshot): add canonicalization primitives

Результат

Створений єдиний механізм нормалізації та hashing.

Acceptance criteria

1. IP addresses і prefixes мають канонічну форму.


2. Множини сортуються.


3. Ordered collections зберігають порядок.


4. Empty і default values нормалізуються за schema.


5. Numbers серіалізуються invariant culture.


6. JSON property order детермінований.


7. .id не входить у canonical configuration.


8. Counters не входять у configuration hash.


9. Configuration і observations мають окремі hashes.


10. Повний snapshot hash включає обидва hashes і schema version.


11. Canonicalize(Canonicalize(x)) == Canonicalize(x).


12. Один input завжди дає однакові bytes і hash.




---

[M1-22] Implement menu-specific canonical snapshots

Labels: type:feature, area:application
Залежності: M1-21
PR title: feat(snapshot): canonicalize RouterOS discovery data

Результат

Усі підтримувані RouterOS sections мають canonical representation.

Sections

system
services
interfaces
addresses
interface lists
IPv4 filter
IPv6 filter
address lists
NAT
RAW
Mangle
routing
VRRP
bridge
VLAN
switch metadata
capabilities
topology validation

Acceptance criteria

1. Firewall rule order зберігається.


2. Route active state відокремлений від route configuration.


3. VRRP role відокремлена від VRRP configuration.


4. Dynamic address-list entries входять лише до observations.


5. Interface running state входить лише до observations.


6. Unknown properties не втрачаються з raw snapshot.


7. Unknown properties не впливають на supported configuration hash без schema rule.


8. Два snapshots без змін мають однакові hashes.


9. API reply order не впливає на unordered sections.


10. Контрольована конфігураційна зміна змінює configuration hash.


11. Контрольована runtime-зміна змінює лише observation hash.




---

[M1-23] Persist snapshots and detect identical captures

Labels: type:feature, area:persistence, area:application
Залежності: M1-03, M1-22
PR title: feat(snapshot): persist canonical RouterOS snapshots

Результат

Controller надійно зберігає snapshots і не дублює незмінені payloads.

Acceptance criteria

1. Snapshot metadata, raw payload і canonical payload зберігаються атомарно.


2. Complete snapshot не перезаписується.


3. Повторний однаковий capture не дублює canonical payload.


4. Подія capture все одно журналюється.


5. Configuration і observation hashes індексуються.


6. Snapshot retrieval підтримує pagination.


7. Snapshot creation має idempotency key.


8. DB failure не залишає orphan metadata.


9. Compression не змінює canonical hash.


10. Snapshot можна відтворити після restart controller.


11. Raw payload не повертається звичайному Viewer без окремого права.




---

[M1-24] Implement deterministic semantic snapshot diff

Labels: type:feature, area:application
Залежності: M1-22, M1-23
PR title: feat(diff): add semantic RouterOS snapshot comparison

Результат

Controller показує змістовну різницю між двома snapshots.

Категорії

ADDED
REMOVED
MODIFIED
MOVED
STATE_CHANGED
CAPABILITY_CHANGED
TOPOLOGY_CHANGED

Acceptance criteria

1. Configuration і observation differences розділені.


2. Rule з однаковим canonical content і новою позицією визначається як MOVED.


3. Rule з однаковим валідним fwc: marker визначається за marker.


4. Unmanaged rules без stable identity зіставляються консервативно.


5. Для неоднозначної unmanaged modification повертається remove+add, а не вигаданий MODIFIED.


6. Ordered rules порівнюються order-aware алгоритмом.


7. Address-list entries порівнюються як множини.


8. Interface-list resolved membership показує entry-level diff.


9. VRRP role change показується як STATE_CHANGED.


10. Diff детермінований.


11. Diff однакових snapshots порожній.


12. Алгоритм має bounded memory для заданих MVP limits.


13. Unit tests охоплюють duplicate unmanaged rules.




---

[M1-25] Add inventory and discovery gRPC services

Labels: type:feature, area:controller, area:application
Залежності: M1-05, M1-18, M1-23
PR title: feat(controller): expose inventory and discovery API

Результат

Desktop може керувати inventory metadata і запускати read-only discovery.

RPC

ListSites
CreateSite
GetNode
CreateNode
RegisterDevice
UpdateConnectionProfile
DiscoverDevice
GetDiscoveryStatus

Acceptance criteria

1. Mutation RPC мають idempotency key.


2. Inventory update використовує optimistic concurrency.


3. RouterOS credentials не повертаються.


4. Discovery має deadline і cancellation.


5. Одночасний discovery одного device дедуплікується або відхиляється.


6. Error mapping використовує нормативні codes.


7. Raw RouterOS error sanitizes.


8. RPC не приймає довільний RouterOS command.


9. Authorization перевіряється до application use case.


10. Кожна mutation створює audit event.


11. Pagination застосовується до списків.


12. Contract compatibility tests додані.




---

[M1-26] Add snapshot and diff gRPC services

Labels: type:feature, area:controller, area:application
Залежності: M1-24, M1-25
PR title: feat(controller): expose snapshot and diff API

Результат

Desktop отримує snapshots і semantic diff через стабільні контракти.

RPC

CaptureSnapshot
WatchSnapshotCapture
ListSnapshots
GetSnapshotSummary
GetSnapshotSection
CompareSnapshots

Acceptance criteria

1. Capture progress передається server-streaming RPC.


2. Snapshot payload завантажується секціями.


3. Large payload не передається одним необмеженим message.


4. Pagination і continuation token валідовані.


5. Client cancellation припиняє capture.


6. Viewer не отримує unredacted raw data.


7. Diff response має stable ordering.


8. Hashes передаються як fixed-length bytes або валідований string contract.


9. Unknown enum values обробляються forward-compatible.


10. API не експортує EF entities або RouterOS DTO.


11. Contract tests перевіряють backward-compatible serialization.




---

[M1-27] Add desktop inventory tree

Labels: type:feature, area:desktop
Залежності: M1-25
PR title: feat(desktop): add inventory tree

Результат

GUI відображає фактичну структуру компанії.

Представлення

Site
 └── Node
      ├── Device
      └── Device

Acceptance criteria

1. Tree використовує server data.


2. UI не створює domain objects локально.


3. Відображаються:

reachability;

RouterOS version;

model;

support state;

node kind;

uplink mode;

VRRP role vector;

last snapshot time.



4. Refresh має cancellation.


5. Повторний refresh не створює паралельних requests.


6. Large inventory завантажується посторінково.


7. Error state не знищує останні успішні дані.


8. Cached state чітко позначений.


9. ViewModel не містить RouterOS або SQL logic.


10. GUI tests перевіряють основні стани.




---

[M1-28] Add desktop snapshot viewer

Labels: type:feature, area:desktop
Залежності: M1-26, M1-27
PR title: feat(desktop): add snapshot viewer

Результат

Оператор переглядає canonical snapshot конкретного пристрою.

Acceptance criteria

1. Snapshot поділений за sections.


2. Configuration і observations відображаються окремо.


3. Показуються всі три hashes.


4. Показується snapshot schema version.


5. Показується capture status кожної section.


6. Unknown properties доступні лише в sanitized technical view.


7. Large rulesets використовують virtualization.


8. UI не блокується під час завантаження.


9. Raw secret fields не відображаються.


10. Copy/export не включає credentials.


11. Snapshot viewer є read-only.




---

[M1-29] Add desktop semantic diff viewer

Labels: type:feature, area:desktop
Залежності: M1-24, M1-26, M1-28
PR title: feat(desktop): add semantic snapshot diff

Результат

Оператор бачить точну різницю між двома snapshots.

Acceptance criteria

1. Користувач вибирає base і target snapshot.


2. Diff групується за sections.


3. Відображаються ADDED, REMOVED, MODIFIED, MOVED, STATE_CHANGED.


4. Configuration changes відокремлені від runtime changes.


5. Rule order показується явно.


6. Address-list diff відображається на рівні entries.


7. Порожній diff має окремий стан No differences.


8. Unsupported/unknown properties не маскуються.


9. Diff rows virtualized.


10. GUI не перераховує semantic diff локально.


11. Відображений diff відповідає server response без інтерпретаційної зміни.




---

[M1-30] Add standalone CHR vertical-slice acceptance test

Labels: type:test, area:testlab, area:routeros
Залежності: M1-11—M1-29
PR title: test(routeros): verify standalone read-only vertical slice

Результат

Повний read-only сценарій доведений на standalone CHR.

Acceptance criteria

1. CHR має перевірений API-SSL certificate.


2. Controller підключається через API-SSL.


3. Discovery отримує всі підтримувані sections.


4. Два captures без змін мають однакові hashes.


5. Контрольована зміна filter rule змінює configuration hash.


6. Semantic diff показує точну зміну.


7. Зміна interface running state змінює лише observation hash.


8. Snapshot зберігається після restart controller.


9. Desktop відображає inventory, snapshot і diff.


10. Product code не виконує RouterOS write command.


11. Testlab provisioning виконується поза production RouterOS adapter.




---

[M1-31] Add multi-WAN CHR vertical-slice acceptance test

Labels: type:test, area:testlab, area:routeros
Залежності: M1-30
PR title: test(routeros): verify multi-WAN discovery and diff

Результат

Read-only вертикальний зріз доведений для failover і balanced nodes.

Acceptance criteria

1. Перевірена topology multi-wan-failover.


2. Перевірена topology multi-wan-balanced.


3. Routing tables читаються.


4. Routing rules читаються.


5. NAT dependencies читаються.


6. Mangle/PCC dependencies читаються.


7. Primary і backup uplinks не змішуються.


8. Route active-state change не змінює configuration hash.


9. Зміна static route змінює configuration hash.


10. Strict rp-filter відображається topology finding.


11. Diff показує окремо configuration і operational route changes.


12. Controller не перемикає WAN і не змінює routing.




---

[M1-32] Add VRRP CHR vertical-slice acceptance test

Labels: type:test, area:testlab, area:routeros
Залежності: M1-30
PR title: test(routeros): verify VRRP discovery and role tracking

Результат

Read-only вертикальний зріз доведений для VRRP active/passive і split-master.

Acceptance criteria

1. Controller підключається до кожного фізичного member address.


2. Усі VRRP groups правильно визначаються.


3. Role vector формується окремо для кожного VRID.


4. Active/passive topology визначається правильно.


5. Split-master topology не класифікується як один global master.


6. Role switch не змінює configuration hash.


7. Role switch змінює observation hash.


8. Version mismatch створює topology blocker.


9. Недоступний member не маскується.


10. Snapshot кожного member зберігається окремо.


11. Node-level view агрегує members без втрати device-level даних.


12. Controller не змінює VRRP configuration.




---

[M1-33] Add protocol and snapshot fault-injection suite

Labels: type:test, area:routeros, risk:high
Залежності: M1-30—M1-32
PR title: test(routeros): add read-path fault injection

Результат

Доведена відсутність зависань, витоків і неконтрольованого споживання ресурсів.

Fault scenarios

fragmented length prefix
fragmented word
interleaved tagged replies
!trap
!fatal
timeout
/cancel
TLS close
connection close mid-sentence
oversized word
oversized sentence
oversized snapshot
controller cancellation
database failure during persistence
controller restart after capture
unstable configuration during snapshot

Acceptance criteria

1. Кожний сценарій завершується визначеним error code.


2. Pending commands після failure дорівнюють нулю.


3. Connection і buffers звільняються.


4. Retry count не перевищує configuration.


5. Snapshot не зберігається як complete після partial failure.


6. DB transaction не залишає orphan records.


7. Повторний capture після recovery успішний.


8. Test execution має bounded timeout.


9. Fault tests не залежать від production network.


10. Відсутні flaky tests у серії повторних запусків.


11. Memory usage не зростає лінійно з кількістю повторних failures.




---

[M1-34] Complete read-only vertical-slice acceptance

Labels: type:docs, area:application, area:testlab
Залежності: M1-01—M1-33
PR title: docs(read-path): complete vertical-slice acceptance

Результат

Milestone M1 формально завершений і готовий до release review.

Scope

документація локального запуску;

документація connection profile;

процедура додавання synthetic CHR device;

snapshot schema description;

canonicalization rules;

semantic diff semantics;

support manifest procedure;

troubleshooting;

acceptance report.


Acceptance criteria

1. Standalone test matrix пройдена.


2. Multi-WAN test matrix пройдена.


3. VRRP test matrix пройдена.


4. Fault-injection suite пройдена.


5. Повторний snapshot без змін має той самий configuration hash.


6. Runtime role/route changes не створюють config drift.


7. Desktop відображає реальні server data.


8. У production assemblies відсутні RouterOS write commands.


9. У desktop відсутні RouterOS credentials.


10. У fixtures відсутні production data.


11. Dependency scan не має unresolved critical findings.


12. Architecture tests проходять.


13. Database restore smoke test проходить.


14. CHANGELOG.md містить повний запис milestone.


15. Документований список відомих обмежень.


16. Усі issues M1-01—M1-33 закриті.


17. Відсутні відкриті blockers.


18. Release candidate відтворюється на чистому середовищі.




---

9. Milestone M1 — кінцеві критерії

M1 закривається лише за доведеного сценарію:

1. Оператор створює Site.
2. Оператор створює Node.
3. Оператор реєструє Device.
4. Controller отримує credentials через захищений connection profile.
5. Controller перевіряє RouterOS certificate.
6. Controller виконує read-only discovery.
7. Controller перевіряє topology.
8. Controller отримує stable snapshot.
9. Controller формує raw і canonical representations.
10. Controller обчислює configuration, observation і snapshot hashes.
11. Snapshot зберігається в PostgreSQL.
12. Desktop показує inventory і snapshot.
13. Після контрольованої зміни створюється новий snapshot.
14. Controller формує deterministic semantic diff.
15. Desktop показує точний diff.

Сценарій повинен працювати для:

standalone router
single-router multi-WAN failover
single-router multi-WAN balancing
VRRP active/passive
VRRP split-master
RouterOS CRS management topology


---

10. Заборонений scope M0–M1

У межах початкового issue set заборонено реалізовувати:

додавання, зміну або видалення RouterOS firewall rules;

створення chains;

створення jump anchors;

management guard;

rollback watchdog;

Safe Mode automation;

policy editor;

policy compiler;

policy approval;

deployment state machine;

drift auto-remediation;

NAT, RAW або Mangle writes;

routing changes;

VRRP changes;

interface-list changes;

switch ACL writes;

API 8728;

RouterOS REST;

SSH/CLI fallback;

довільний RouterOS command console;

automatic network scanning;

background auto-discovery всієї мережі;

multi-tenant model;

microservices;

message broker;

Redis;

Kubernetes;

production auto-update.


Єдині дозволені RouterOS команди production-коду:

/login
/cancel
allowlisted read/print operations


---

11. Кількість Issues

Milestone	Кількість

M0 — Repository Bootstrap	10
M1 — Read-Only Vertical Slice	34
Разом	44


Додаткові issues не створюються замість розширення acceptance criteria вже визначеної задачі. Новий issue створюється лише для функціонально окремого результату або виявленого дефекту.


---

12. Наступний нормативний документ

MikroTik Firewall Controller
Read-Only Vertical Slice — Technical Design v0.1

Він повинен формально визначити:

gRPC protobuf contracts
PostgreSQL schema M1
RouterOS read-command allowlist
API protocol types
raw snapshot schema
canonical snapshot schema
configuration/observation hash contracts
stable-read protocol
semantic diff matching rules
error contracts
CHR fixture contracts