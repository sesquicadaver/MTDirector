MikroTik Firewall Controller

MVP End-to-End Workflow and Acceptance Specification v0.1

Дата: 3 серпня 2026 року
Статус: нормативна специфікація M6 — MVP Integration


---

1. Призначення

Документ зводить попередні специфікації в один наскрізний workflow без додавання нових підсистем:

Inventory
    ↓
Read-only capture
    ↓
Topology validation
    ↓
Managed-device onboarding
    ↓
Policy authoring
    ↓
Composition і static analysis
    ↓
Approval і desired binding
    ↓
Compilation
    ↓
Safe deployment
    ↓
Verification
    ↓
Drift detection або rollback

Кінцевий результат MVP — централізоване керування firewall-політиками MikroTik у межах однієї компанії з багатьма філіями та такими типами вузлів:

один RouterOS router;

один router із декількома WAN;

VRRP-вузол із декількох фізичних router;

MikroTik CRS у межах management-plane firewall.



---

2. Нормативна база

Ця специфікація інтегрує:

Repository Bootstrap Plan v0.1
Read-Only Vertical Slice Technical Design v0.1
RouterOS Read Adapter Specification v0.1
Canonical Snapshot and Semantic Diff Specification v0.1
Policy Model, Composition and Static Analysis Specification v0.1
Policy Compiler and Managed Chain Layout Specification v0.1
Safe Deployment and Rollback Specification v0.1
Managed Device Onboarding and Anchor Bootstrap Specification v0.1

Низькорівневі контракти цих документів не дублюються тут.

При суперечності застосовується пізніша профільна специфікація.


---

3. Межі MVP

3.1. MVP реалізує

централізований inventory;
RouterOS API-SSL read path;
immutable snapshots;
semantic snapshot diff;
standalone, multi-WAN і VRRP topology projection;
managed-device onboarding;
company/site/node firewall policies;
temporary deny-stage exceptions;
static analysis;
approval;
deterministic compilation;
safe per-Node deployment;
local watchdog rollback;
drift detection;
RBAC;
audit;
desktop GUI.

3.2. MVP не реалізує

deployment campaigns;
одночасний rollout багатьох незалежних Nodes;
автоматичне застосування approved policy;
автоматичне виправлення drift;
автоматичне створення RouterOS users;
автоматичне налаштування API-SSL;
автоматичне встановлення certificates;
автоматичне створення management guard;
автоматичну зміну device-mode;
NAT, RAW, Mangle або routing writes;
VRRP configuration writes;
interface-list writes;
bridge/VLAN writes;
switch-chip ACL;
SwOS configuration;
інші виробники;
web-клієнт;
мобільний клієнт;
довільний RouterOS command console;
мікросервіси;
message broker;
Redis;
Kubernetes.

Одна effectful операція MVP працює рівно з одним логічним Node.


---

4. Deployable-компоненти

MVP має рівно три основні компоненти:

Mfc.Desktop
Mfc.Controller
PostgreSQL

Додаткові deployable services не створюються.

┌──────────────────────┐
│ Desktop              │
│ Avalonia             │
└──────────┬───────────┘
           │ gRPC + TLS
┌──────────▼───────────┐
│ Controller           │
│ ASP.NET Core         │
└───────┬──────────────┘
        │
        ├── PostgreSQL
        │
        └── RouterOS API-SSL

Desktop:

не підключається до RouterOS;

не підключається до PostgreSQL;

не отримує RouterOS credentials;

не виконує policy analysis;

не компілює policy;

не формує RouterOS commands.



---

5. Підтримувані топології

Топологія	Capture	Policy	Deployment

Standalone IPv4 router	Так	Так	Так
Standalone dual-stack router	Так	Так	Так
Один router із WAN failover	Так	Так	Так
Один router із WAN balancing/PCC	Так	Так	Так
Один router із mixed WAN topology	Так	Так	Так
VRRP active/passive	Так	Так	Так
VRRP split-master	Так	Так	Так
CRS RouterOS management plane	Так	INPUT/OUTPUT	Так
CRS transit firewall	Read-only context	Ні	Ні
SwOS	Inventory metadata	Ні	Ні
RouterOS 6	Read-only/unsupported legacy	Ні	Ні


Write support надається лише exact RouterOS 7 builds, внесеним у signed compatibility manifest.


---

6. Основні стани Node

Node має два незалежні виміри стану.

6.1. Operational status

DRAFT
ACTIVE
MAINTENANCE
DISABLED

6.2. Management state

UNMANAGED
MANAGED
RECOVERY_REQUIRED

ACTIVE + MANAGED означає, що Node може бути target production deployment.

RECOVERY_REQUIRED блокує:

onboarding;

policy deployment;

zone-binding changes;

іншу effectful операцію.



---

7. Похідний workflow-стан

NodeWorkflowStatus не зберігається як authoritative поле. Він обчислюється з фактичного стану:

INVENTORY_INCOMPLETE
CONNECTION_INVALID
CAPTURE_REQUIRED
TOPOLOGY_BLOCKED
ONBOARDING_REQUIRED
ONBOARDING_IN_PROGRESS
POLICY_REQUIRED
ANALYSIS_REQUIRED
ANALYSIS_BLOCKED
PENDING_DEPLOYMENT
DEPLOYMENT_IN_PROGRESS
SYNCHRONIZED
DRIFTED
RECOVERY_REQUIRED

Пріоритет:

RECOVERY_REQUIRED
    >
активна effectful operation
    >
DRIFTED
    >
readiness blockers
    >
PENDING_DEPLOYMENT
    >
SYNCHRONIZED


---

8. Desired, committed і actual state

Для кожного Device Controller зберігає:

desired_policy_hash
desired_artifact_hash

last_committed_policy_hash
last_committed_artifact_hash

actual_managed_resource_hash

8.1. Синхронізований стан

desired_artifact_hash
    ==
last_committed_artifact_hash
    ==
actual_managed_resource_hash

Результат:

SYNCHRONIZED

8.2. Очікує deployment

desired_artifact_hash
    !=
last_committed_artifact_hash

actual_managed_resource_hash
    ==
last_committed_artifact_hash

Результат:

PENDING_DEPLOYMENT

Це не drift.

8.3. Drift

actual_managed_resource_hash
    !=
last_committed_artifact_hash

Результат:

DRIFTED

8.4. Невизначений actual state

Якщо Controller не може однозначно визначити anchor або active artifact:

RECOVERY_REQUIRED


---

9. Ролі

Використовуються лише раніше визначені ролі:

Viewer
PolicyEditor
Reviewer
Deployer
Administrator
Auditor

Операція	Мінімальна роль

Перегляд inventory/snapshots	Viewer
Створення Site/Node/Device	Administrator
Connection profile	Administrator
Запуск capture	Viewer
Zone bindings	Administrator
Створення policy draft	PolicyEditor
Validation preview	PolicyEditor
Approval	Reviewer
Desired policy binding	Reviewer
Створення deployment plan	Deployer
Запуск deployment	Deployer
Rollback	Deployer
Onboarding	Administrator + Deployer permissions
Перегляд audit	Auditor
Export audit	Auditor


High-risk і Critical policy revisions не може одноосібно затвердити їх автор.


---

10. Наскрізний workflow

1. Підготувати Controller і PostgreSQL.
2. Підготувати RouterOS prerequisites.
3. Створити Site.
4. Створити Node.
5. Зареєструвати фізичні Devices.
6. Додати connection profiles.
7. Перевірити API-SSL connections.
8. Виконати Node capture.
9. Перевірити topology.
10. Задати zone bindings.
11. Перевірити management guard і account prerequisites.
12. Виконати onboarding.
13. Створити company baseline.
14. За потреби створити Site/Node overlays.
15. Запустити composition і static analysis.
16. Затвердити revision.
17. Активувати desired binding.
18. Скомпілювати artifacts.
19. Створити Node deployment plan.
20. Запустити safe deployment.
21. Перевірити commit.
22. Періодично перевіряти drift.

Кожний етап має явні preconditions і не виконує функції наступного етапу.


---

11. Platform bootstrap

До першої роботи з RouterOS повинні бути завершені:

controller installation;
PostgreSQL migration;
server TLS;
corporate OIDC;
master-key provider;
audit storage;
desktop-controller connection;
health checks.

Controller не запускається, якщо:

database schema неактуальна;

TLS configuration невалідна;

master-key provider недоступний;

production authentication не налаштована;

configuration містить необмежені timeout або concurrency values.


Database migration виконується окремою командою:

Mfc.Controller --migrate-only

Normal startup не застосовує migrations автоматично.


---

12. RouterOS prerequisites

До реєстрації Device адміністратор RouterOS повинен підготувати:

RouterOS 7 supported build;
API 8728 disabled;
API-SSL enabled;
valid server certificate;
read-only API account;
deployment API account;
source restrictions;
management firewall guard;
scheduler-enabled device-mode;
physical management IP.

Controller тільки перевіряє ці умови.

Він не виправляє їх автоматично.


---

13. Inventory workflow

13.1. Site

Оператор створює:

code
name
timezone

13.2. Node

Оператор задає:

Site
name
declared kind:
    ROUTER
    VRRP
    SWITCH

declared uplink mode:
    NONE
    SINGLE
    FAILOVER
    BALANCED
    MIXED

13.3. Device

Для кожного фізичного RouterOS-пристрою:

display name
physical management host
API-SSL port
connection profile

Для VRRP кожний member реєструється окремо.

VRRP virtual IP не використовується як management endpoint.


---

14. Connection validation

ValidateDeviceConnection виконує:

TCP connection
TLS validation
RouterOS login
system identity read
RouterOS version read
API-SSL service verification
disconnect

Результати:

VALID
DEVICE_UNREACHABLE
TLS_INVALID
AUTHENTICATION_FAILED
ROUTEROS_UNSUPPORTED
API_SSL_INVALID

Connection validation:

не створює snapshot;

не змінює RouterOS;

не запускає onboarding;

не зберігає plaintext credentials.



---

15. Capture workflow

Capture запускається для:

Device
або
Node

Node capture створює окремий Device capture для кожного member.

QUEUED
→ CONNECTING
→ AUTHENTICATING
→ READING_PASS_1
→ CANONICALIZING_PASS_1
→ READING_PASS_2
→ VERIFYING_STABILITY
→ PERSISTING
→ COMPLETED

Capture приймається лише при збігу stability vectors двох read passes.

Partial capture не використовується для:

onboarding plan;

policy approval;

compilation;

deployment plan;

drift decision.



---

16. Topology validation

Після Node capture Controller порівнює declared topology з фактичним RouterOS state.

16.1. Standalone router

Перевіряється:

рівно один Device;
RouterOS firewall доступний;
відсутня суперечлива VRRP configuration;
declared uplink mode узгоджується з evidence.

16.2. Multi-WAN

Перевіряються:

default routes;
route distances;
routing tables;
routing rules;
Mangle routing marks;
PCC;
NAT uplink bindings;
active paths;
rp-filter.

Controller не перемикає WAN і не вимикає interface для перевірки.

16.3. VRRP

Перевіряються:

усі members;
VRIDs;
address families;
virtual addresses;
RouterOS versions;
role vector;
split-master topology;
capture skew.

16.4. Switch

Перевіряються:

board/model;
bridge;
VLAN filtering;
hardware offload;
management IP;
hardware profile.

Transit path не вважається захищеним IP firewall.


---

17. Zone binding workflow

Оператор прив’язує logical zones до фактичних RouterOS interfaces:

INTERFACE_LIST
SINGLE_INTERFACE
EXPLICIT_INTERFACE_SET

Для VRRP прив’язка resolve-иться окремо на кожному member.

Приклад:

Zone MGMT:

Router A:
    bridge-mgmt

Router B:
    ether5

Обидві physical bindings представляють одну logical zone.

Zone binding блокується, якщо:

interface не існує;

interface dynamic;

resolved set порожній;

interface-list membership циклічна;

membership змінилася після analysis.



---

18. Onboarding workflow

Onboarding виконується до першого production policy deployment.

PRECHECK
→ STAGE BOOTSTRAP ROOTS
→ STAGE DISABLED ANCHORS
→ ARM WATCHDOGS
→ ENABLE ANCHORS
→ VERIFY
→ DISABLE WATCHDOGS
→ COMMIT MANAGED

Результат:

permanent anchors
    ↓
pass-through bootstrap roots
    ↓
Node = MANAGED

Bootstrap artifact не змінює firewall verdict.

Для VRRP:

всі members onboard-яться в одній operation;

частковий onboarding заборонений;

watchdog встановлюється на кожному member.


При failure:

enabled anchors disable;
bootstrap resources remove;
Node remains UNMANAGED.


---

19. Policy authoring workflow

Першою обов’язковою policy є:

COMPANY_BASELINE

Site і Node overlays є optional.

Company baseline
        ↓
Site overlay
        ↓
Node overlay
        ↓
Temporary exception

Policy Editor працює тільки з:

address objects;
service objects;
zones;
typed firewall rules;
chain contracts;
policy tests.

Відсутній raw RouterOS syntax editor.


---

20. Policy revision workflow

DRAFT
→ VALIDATED
→ IN_REVIEW
→ APPROVED

Додаткові terminal states:

REJECTED
SUPERSEDED
REVOKED

Зміна draft після validation:

validation result invalidated
→ state повертається до DRAFT

Approved revision immutable.


---

21. Composition workflow

Для одного Node:

approved company baseline
+ active Site overlay
+ active Node overlay
+ active exceptions
+ zone bindings
=
effective logical policy

Composition:

використовує fixed Pipeline v1;

не використовує name-based override;

не видаляє duplicate rules автоматично;

не змінює precedence;

не залежить від current VRRP role;

не залежить від current active WAN.



---

22. Static analysis workflow

Аналіз виконується послідовно:

SCHEMA
→ STRUCTURAL
→ COMPOSITION
→ PREDICATE
→ SEQUENCE
→ ACTUAL ROUTEROS CONTEXT
→ SAFETY
→ POLICY TESTS
→ RISK

Обов’язково перевіряються:

duplicates;
conflicting rules;
shadowing;
unreachable rules;
unmanaged pre-anchor context;
management path;
VRRP control traffic;
VRRP synchronization;
multi-WAN dependencies;
RAW notrack;
NAT dependencies;
Mangle/PCC;
FastTrack;
switch packet-path constraints.

Unknown safety result:

BLOCKER


---

23. Policy approval

Revision може бути approved лише коли:

content hash fixed;
parent contexts current;
impact set complete;
all active target Nodes analyzed;
no blockers;
warnings acknowledged;
mandatory tests passed;
system tests passed;
management paths valid;
VRRP members covered;
multi-WAN dependencies valid;
analysis bundle hash fixed.

Approval:

не змінює RouterOS;

не активує binding;

не запускає deployment.



---

24. Desired binding activation

Після approval Reviewer окремо активує revision як desired binding.

approved revision
    ↓ explicit binding action
desired policy

Binding activation:

не запускає deployment;

створює audit event;

переводить affected synchronized Nodes у PENDING_DEPLOYMENT;

не змінює фактичний firewall.


Для company baseline affected set охоплює всі active managed Nodes, крім явно переведених у MAINTENANCE.

Active unreachable Node не виключається мовчки.


---

25. Rollout без campaign subsystem

MVP не має campaign engine.

Company або Site policy може впливати на багато Nodes, але кожний Node deploy-иться окремою операцією:

Node A → deployment
Node B → deployment
Node C → deployment

GUI показує загальний список:

SYNCHRONIZED
PENDING_DEPLOYMENT
BLOCKED
DRIFTED
RECOVERY_REQUIRED

Оператор запускає deployment окремо для кожного Node.


---

26. Compilation workflow

Для кожного Device Node:

effective logical policy
+ resolved zones
+ capability profile
=
immutable RouterOS artifact

Compiler:

не підключається до RouterOS;

не формує API commands;

не оптимізує rule semantics;

не створює NAT/Mangle/routing;

не змінює management guard;

не залежить від current WAN/VRRP operational state.


VRRP members можуть отримати різні physical artifact hashes через різні interface names, але мають один logical policy hash.


---

27. Deployment plan workflow

Перед effectful operation Deployer створює immutable plan.

Plan містить:

Node ID;
policy hashes;
analysis bundle hash;
topology hash;
per-Device artifacts;
old anchor targets;
new anchor targets;
dependency hashes;
activation order;
rollback order;
watchdog TTL;
probes;
plan hash.

Plan creation:

нічого не змінює на RouterOS;

повторно перевіряє readiness;

показує semantic diff;

показує exact affected Devices;

має обмежений строк дії.



---

28. Safe deployment workflow

PRECHECK
→ STAGE ALL DEVICES
→ VERIFY STAGED ARTIFACTS
→ ARM ALL WATCHDOGS
→ ACTIVATE
→ VERIFY
→ DISABLE WATCHDOGS
→ COMMIT

28.1. Staging

Створюються detached:

content-addressed address lists;
deny chains;
root chains.

Active chain не редагується.

28.2. Activation

Змінюється лише:

permanent anchor jump-target

28.3. Verification

Перевіряються:

artifact hashes;
anchor targets;
new API-SSL connection;
management guard;
Router ping probes;
VRRP state;
multi-WAN dependencies.

28.4. Commit

COMMITTED дозволений лише після доведеного disabling watchdogs на всіх Devices.


---

29. Deployment cancellation

До activation

Оператор може скасувати deployment.

Результат:

CANCELED

Detached staged artifacts можуть залишитися для подальшого exact reuse.

Після activation першого anchor

Звичайне скасування заборонене.

Операція переходить у:

ROLLBACK_PENDING

і відновлює попередній artifact.


---

30. Deployment rollback

Rollback:

restore old anchor targets
→ verify old artifact
→ open new API-SSL connection
→ run old-state probes
→ disable watchdogs
→ commit ROLLED_BACK

Після rollback:

last committed artifact = old artifact
desired artifact = new artifact

Node отримує:

PENDING_DEPLOYMENT

із позначкою останньої невдалої спроби.

Global desired policy binding автоматично не повертається на стару revision.


---

31. Crash recovery

Після restart Controller перевіряє всі nonterminal operations.

Правило production deployment:

тільки durable COMMITTED зберігає new artifact

Якщо deployment не committed:

rollback old artifact

Правило onboarding:

тільки durable COMMITTED зберігає enabled permanent anchors

Nonterminal onboarding rollback-иться до UNMANAGED.

Unexpected anchor target:

RECOVERY_REQUIRED

Controller не переписує його автоматично.


---

32. Drift detection

Drift detection використовує той самий capture і canonicalization path, що й read-only workflow.

Окремий monitoring protocol не створюється.

32.1. Запуск

Capture для drift може бути:

manual;
periodic background.

MVP має одну глобальну bounded polling configuration, а не індивідуальні складні schedules для кожного Device.

32.2. Порівняння

last committed managed state
        ↕
actual RouterOS state

Desired state не використовується як baseline drift, якщо deployment ще не виконаний.


---

33. Drift classes

Drift	Severity

Managed rule changed	Critical
Managed rule reordered	Critical
Managed rule missing	Critical
Anchor missing	Critical
Anchor disabled	Critical
Anchor target changed	Critical
Anchor position changed	Critical
Management guard changed	Critical
Managed address-list changed	Critical
Interface-list membership changed	Critical
Zone resolution changed	Critical
RouterOS version changed	Critical
Capability changed	Critical
VRRP membership/config changed	Critical
NAT/RAW/Mangle dependency changed	Critical
Routing configuration changed	Critical
New unmanaged pre-anchor rule	Critical/Warning
New unmanaged post-anchor rule	Warning
VRRP role changed	Observation
Active WAN changed	Observation
Interface running state changed	Observation
Counters changed	Ignored



---

34. Drift workflow

При Critical drift:

1. Позначити Node DRIFTED.
2. Заблокувати новий deployment.
3. Показати semantic diff.
4. Зберегти audit event.
5. Вимагати нового capture і analysis.

Доступні дії MVP:

recapture;
переглянути diff;
створити normal restoration deployment;
acknowledge observation-only finding.

Не реалізуються:

automatic repair;
silent desired-state enforcement;
автоматичне видалення unmanaged rules;
автоматичний import drift у policy.


---

35. Exception expiration workflow

Після valid_until exception binding переходить у:

EXPIRED_PENDING_RECONCILIATION

Це:

не змінює RouterOS;

не запускає deployment;

змінює desired policy;

переводить affected Nodes у PENDING_DEPLOYMENT;

потребує нового analysis і deployment.



---

36. Мінімальні GUI-модулі

Desktop MVP має сім модулів:

1. Inventory
2. Node
3. Snapshots
4. Policies
5. Operations
6. Drift
7. Audit

Окремий Dashboard не є обов’язковим.


---

37. Inventory GUI

Показує:

Site
 └── Node
      ├── Device
      └── Device

Дозволені дії:

створити/редагувати Site;
створити/редагувати Node;
зареєструвати Device;
редагувати connection profile;
validate connection;
запустити capture.

Відображаються:

operational status;
management state;
workflow status;
RouterOS version;
model;
support state;
last capture;
reachability;
desired/committed/actual hashes.


---

38. Node GUI

Node view містить:

Topology
Devices
Zone bindings
Management prerequisites
Onboarding
Current policy
Deployment readiness

Topology

Показує:

declared topology;

observed topology;

findings;

multi-WAN evidence;

VRRP role vector;

switch hardware context.


Zone bindings

Дозволяє задавати лише:

existing interface list;
single interface;
explicit static interface set.

Onboarding

Показує:

prerequisite checklist;
management guard state;
account state;
scheduler capability;
anchor placements;
operation progress;
recovery state.


---

39. Snapshot GUI

Дозволяє:

переглядати completed snapshots;
порівнювати два snapshots;
фільтрувати configuration/observations;
переглядати section findings.

Не дозволяє:

змінювати RouterOS;

редагувати raw snapshot;

відправляти snapshot назад на Device;

показувати credentials.


Raw sanitized snapshot доступний лише окремому Administrator/Auditor permission.


---

40. Policy GUI

Policy Editor містить:

Address objects
Service objects
Rules
Chain contracts
Tests
Revision metadata

Не містить:

RouterOS CLI;
API command editor;
raw matcher strings;
script editor;
manual chain editor.

Workflow:

Save draft
Validate
Review findings
Submit for review
Approve або reject
Activate desired binding

Кнопки:

Save
Approve
Bind
Deploy

є окремими діями.

Команда «Save and Deploy» заборонена.


---

41. Operations GUI

Один модуль відображає:

Onboarding operations
Deployment operations
Rollback
Recovery

Перед deployment показуються:

Node;
Devices;
semantic policy diff;
actual RouterOS diff;
old/new artifacts;
warnings;
blockers;
activation order;
probes;
watchdog TTL;
plan hash.

Під час operation GUI отримує server-streaming progress.

Desktop не прогнозує state transition локально.


---

42. Drift GUI

Показує:

last committed state;
actual state;
drift class;
severity;
first observed time;
last confirmed time;
semantic diff;
affected resources;
blocked operations.

Observation-only changes відображаються окремо від configuration drift.

GUI не має кнопки:

Fix all automatically

Restoration виконується тільки через normal plan і deployment workflow.


---

43. Audit GUI

Audit підтримує пошук за:

time range;
actor;
Site;
Node;
Device;
policy revision;
operation ID;
event type;
result.

Audit є read-only.

Export:

створює audit event;

не містить credentials;

не містить raw passwords;

не містить private keys;

не містить RouterOS login sentences.



---

44. API boundary

Desktop використовує тільки gRPC services Controller.

Усі mutation RPC мають:

authentication;
authorization;
idempotency key;
optimistic concurrency;
correlation ID;
deadline;
cancellation;
audit.

Відсутні RPC:

ExecuteRouterOsCommand
RunScript
OpenTerminal
ExecuteSql
ForceApply
SkipValidation
IgnoreWatchdog


---

45. Error presentation

Користувач отримує:

stable error code;
severity;
affected resource;
sanitized description;
correlation ID;
допустиму recovery action.

Desktop не показує як основне повідомлення:

raw RouterOS trap;

stack trace;

SQL exception;

TLS private details;

command sentence.


Для RECOVERY_REQUIRED показуються exact recovery facts:

actual anchor targets;
expected old targets;
expected new targets;
reachable Devices;
watchdog state;
last durable step.


---

46. Concurrency

Обмеження MVP:

одна onboarding operation на Node;
один deployment на Node;
один writer на Device;
один active capture на Device;
один active policy analysis для однакового context hash.

Дозволено паралельно:

read-only captures різних Devices;
analysis різних policies;
staging різних Devices одного VRRP Node у bounded режимі.

Заборонено одночасно на одному Node:

onboarding + deployment;
deployment + zone-binding edit;
deployment + policy binding change;
deployment + manual restoration.


---

47. Security requirements

1. RouterOS credentials зберігаються лише Controller.


2. Read і deployment accounts розділені.


3. API 8728 заборонений.


4. API-SSL certificate validation обов’язкова.


5. Default RouterOS groups не використовуються.


6. RouterOS deployment writer має закритий command allowlist.


7. Desktop не має RouterOS SDK.


8. Management guard не змінюється Controller.


9. User input не потрапляє у RouterOS scripts.


10. Arbitrary script execution відсутній.


11. Approved policy immutable.


12. High/Critical зміни мають separation of duties.


13. Watchdog обов’язковий.


14. Кожна effectful операція має immutable plan hash.


15. Кожний write має read-back.


16. Unknown state обробляється fail-closed.


17. Secrets не потрапляють у logs або audit.


18. Completed snapshots і operations immutable.


19. Database backups зашифровані.


20. CHR runner ізольований від production network.




---

48. Data integrity

Обов’язково:

canonical hash verification;
content-addressed snapshot storage;
immutable completed captures;
immutable approved policy revisions;
immutable deployment plans;
write-ahead operation journal;
tamper-evident audit chain.

PostgreSQL application role не має права змінювати:

approved policy payload;
completed snapshot payload;
committed deployment history;
completed onboarding history;
audit events.


---

49. Background jobs

Controller MVP має лише необхідні background jobs:

operation recovery;
periodic drift capture;
expired-exception reconciliation;
durable lock heartbeat;
bounded cleanup тимчасових disabled watchdog resources.

Cleanup не видаляє:

old firewall artifacts;
snapshots;
audit;
approved revisions.

Окремий job framework або message broker не використовується.


---

50. Performance і масштаб

MVP повинен підтримувати щонайменше:

1000 RouterOS Devices;
500 Sites;
100 concurrent Desktop sessions;
20 000 physical rules на family/chain;
250 000 static address entries на family;
16 concurrent Device captures;
8 concurrent Node write operations;
1 write operation per Device.

Target latency:

Операція	Target

Cached inventory	до 1 s
Snapshot list	до 1 s
Policy validation 1000 rules	до 2 s
Semantic diff 1000 rules	до 2 s
Compilation 1000 rules	до 1 s
Equal-snapshot comparison	до 100 ms
GUI response to user input	без блокування UI thread



---

51. Observability

Controller експортує:

health;
structured logs;
capture metrics;
analysis duration;
deployment duration;
rollback count;
watchdog execution count;
drift count;
RouterOS connection failures;
DB health;
active locks.

Logs містять:

correlation ID;
operation ID;
Node ID;
Device ID;
event code;
duration;
result.

Logs не містять firewall content або credentials.


---

52. Backup і restore acceptance

Backup охоплює:

PostgreSQL;
encrypted secrets;
master-key references;
Controller configuration;
OIDC configuration;
internal CA trust;
audit checkpoints.

Restore test повинен довести:

1. Inventory відновлено.


2. Connection profiles відновлено.


3. Policies і approvals відновлено.


4. Snapshots читаються і проходять hash verification.


5. Active artifact references відновлено.


6. Audit chain валідна.


7. Controller після restore визначає actual RouterOS state.


8. Nonterminal operations проходять recovery.


9. Secrets розшифровуються лише з правильним master key.




---

53. End-to-end acceptance: standalone IPv4

Сценарій повинен довести:

1. Створення Site/Node/Device.
2. API-SSL validation.
3. Stable capture.
4. Topology verification.
5. Zone binding.
6. Onboarding.
7. Company baseline creation.
8. Static analysis.
9. Approval.
10. Desired binding.
11. Compilation.
12. Safe deployment.
13. New management connection.
14. Commit.
15. Repeated deployment → NO_CHANGES.
16. Manual managed-rule change → Critical drift.
17. Normal restoration deployment.


---

54. End-to-end acceptance: dual-stack

Повинно бути доведено:

IPv4 та IPv6 snapshots розділені;
IPv4 та IPv6 policies розділені;
IPv6 anchors створені;
IPv6 management guard valid;
IPv6 ICMP/VRRP matchers family-correct;
IPv4 change не змінює IPv6 artifact;
IPv6 failure запускає rollback усього Node deployment.


---

55. End-to-end acceptance: multi-WAN failover

Сценарій виконується двічі:

primary WAN active;
backup WAN active.

Повинно бути доведено:

1. Той самий desired artifact для обох operational states.


2. Усі WAN zones включені.


3. Current active route не входить до artifact hash.


4. Routing/NAT/Mangle configuration не змінюється.


5. Required active-path Router ping проходить.


6. Controller не вимикає primary WAN.


7. Controller не створює temporary routes.


8. Strict rp-filter блокується.


9. Backup route configuration change анулює plan.




---

56. End-to-end acceptance: balanced/PCC

Повинно бути доведено:

routing tables detected;
routing marks detected;
PCC detected;
all uplinks resolved;
FastTrack blocked;
policy compiles without routing writes;
per-table Router ping probes execute;
Mangle configuration remains unchanged;
active route observations do not create config drift.


---

57. End-to-end acceptance: VRRP active/passive

Повинно бути доведено:

1. Кожний member має physical management address.


2. Node capture охоплює всі members.


3. Onboarding виконується для всіх members.


4. Logical policy однакова.


5. Per-Device artifacts можуть відрізнятись через interfaces.


6. Усі artifacts staged.


7. Усі watchdogs armed.


8. Standby member активується першим.


9. Master активується останнім.


10. Нове API connection проходить на кожному member.


11. VRRP advertisements залишаються дозволеними.


12. Role change після першої activation запускає rollback.


13. Усі members повертаються до old artifact.


14. Частковий commit неможливий.




---

58. End-to-end acceptance: VRRP split-master

Повинно бути доведено:

role зберігається для кожного VRID;
Device не має одного global role;
обидва members класифікуються traffic-bearing;
activation order deterministic;
policy однакова на всіх members;
поточна role не впливає на compilation;
зміна одного VRID observation не створює configuration drift.


---

59. End-to-end acceptance: MikroTik CRS

Повинно бути доведено:

hardware profile validated;
bridge/VLAN snapshot отриманий;
hardware offload state отриманий;
INPUT і OUTPUT anchors onboarded;
FORWARD anchor відсутній;
FORWARD policy відхиляється;
bridge/VLAN не змінюються;
hardware offload не змінюється;
transit traffic не оголошується IP-firewall protected.


---

60. Policy acceptance matrix

Обов’язкові сценарії:

company deny не обходиться Site allow;
company deny не обходиться Node allow;
Site deny не обходиться Node allow;
mandatory deny не обходиться exception;
exception bypasses лише target deny stage;
expired exception змінює desired, але не actual;
duplicate rule detection;
conflicting rule blocker;
full shadow blocker;
partial shadow warning;
management path blocker;
FastTrack valid case;
FastTrack unsafe multi-WAN blocker;
RETURN_TO_UNMANAGED actual-context analysis;
unknown matcher blocker.


---

61. Deployment fault acceptance

Connection розривається після кожної effectful точки:

staging intent;
address-list add;
filter-rule add;
watchdog script add;
scheduler add;
watchdog armed;
anchor set;
first anchor;
last anchor;
management reconnect;
probe;
watchdog disable;
DB commit.

Допустимі кінцеві результати:

old committed state;
new committed state;
recovery required із точним фактичним станом.

Недопустимі:

невідомий internal state;
активний частково створений chain;
committed Node із різними VRRP artifacts;
active watchdog після committed state;
втрата management access без watchdog rollback.


---

62. Security acceptance

Обов’язково перевіряються:

invalid CA;
expired certificate;
SAN mismatch;
SPKI mismatch;
plain API enabled;
default RouterOS group;
overbroad management guard;
credential extraction із DB;
credential presence у logs;
desktop credential access;
gRPC role bypass;
idempotency replay;
audit modification;
script source injection;
arbitrary RouterOS path injection;
untrusted PR на privileged CHR runner.


---

63. Drift acceptance

Обов’язкові сценарії:

managed rule modified;
managed rule reordered;
anchor removed;
anchor disabled;
anchor target changed;
management guard changed;
address-list entry added manually;
interface-list membership changed;
RouterOS version changed;
VRRP member added/removed;
NAT dependency changed;
unmanaged pre-anchor accept added;
unmanaged post-anchor rule added;
VRRP role change;
active WAN change;
interface running change.

Перші тринадцять конфігураційних сценаріїв повинні блокувати deployment.

Останні operational scenarios не повинні створювати configuration drift без додаткової конфігураційної зміни.


---

64. Release gates

Production release заборонений, доки не виконано:

M0 repository acceptance;
M1 read-only acceptance;
policy-core acceptance;
compiler acceptance;
onboarding acceptance;
safe-deployment acceptance;
all topology E2E scenarios;
fault-injection suite;
security suite;
backup/restore test;
physical CRS test;
clean dependency scan;
clean Git working tree.

Жоден acceptance criterion не може бути закритий вручну без test evidence або documented operational validation.


---

65. Реалізаційний порядок

Нормативний порядок реалізації:

M0 — Repository bootstrap
M1 — Read-only capture і semantic diff
M2 — Policy model і static analysis
M3 — Policy compiler
M5 — Onboarding і permanent anchors
M4 — Safe deployment і rollback
M6 — End-to-end integration і acceptance

Onboarding реалізується до production deployment, оскільки Safe Deployment потребує permanent anchors і bootstrap old targets.


---

66. MVP Definition of Done

MVP завершений лише коли оператор може виконати без direct RouterOS interaction:

1. Зареєструвати всі фізичні routers Node.
2. Отримати достовірний snapshot.
3. Побачити VRRP або multi-WAN topology.
4. Onboard Node без зміни firewall semantics.
5. Створити company/site/node policy.
6. Отримати доказовий static analysis.
7. Затвердити policy.
8. Призначити її desired.
9. Скомпілювати Device artifacts.
10. Безпечно застосувати їх.
11. Автоматично rollback-нутися при failure.
12. Виявити manual drift.
13. Побачити повний audit trail.

При цьому система не повинна:

змінювати unmanaged rules;
змінювати routing;
змінювати VRRP;
змінювати NAT/RAW/Mangle;
змінювати interfaces;
змінювати switch transit plane;
приховувати partial failure;
виконувати arbitrary RouterOS commands;
зберігати credentials у Desktop;
покладатися на implicit RouterOS accept;
залишати Node у невизначеному стані без RECOVERY_REQUIRED.


---

67. Результат

Після виконання специфікації MVP забезпечує повний контрольований цикл:

фактична RouterOS конфігурація
        ↓
immutable canonical snapshot
        ↓
централізована корпоративна policy
        ↓
topology-aware analysis
        ↓
deterministic per-Device artifact
        ↓
watchdog-protected deployment
        ↓
verified commit або deterministic rollback
        ↓
continuous drift visibility

Наступний документ:

MikroTik Firewall Controller
M2–M6 Implementation Issue Set v0.1

Він має розкласти реалізацію policy core, compiler, onboarding, deployment і фінальну інтеграцію на атомарні GitHub Issues без розширення функціонального scope.