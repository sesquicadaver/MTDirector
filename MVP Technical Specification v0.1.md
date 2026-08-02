MikroTik Firewall Controller

MVP Technical Specification v0.1

Дата: 2 серпня 2026 року
Статус: нормативне ТЗ першої production-ready версії

У документі:

MUST / ОБОВ’ЯЗКОВО — вимога без винятків;

SHOULD / РЕКОМЕНДОВАНО — відхилення потребує документованого обґрунтування;

MAY / ДОЗВОЛЕНО — необов’язкова можливість.



---

1. Мета системи

Система повинна централізовано керувати firewall-політиками MikroTik у межах однієї компанії з багатьма філіями та різною топологією вузлів:

окремі маршрутизатори;

маршрутизатори з декількома WAN у режимі failover або балансування;

VRRP-вузли з двох або більше маршрутизаторів;

MikroTik CRS під RouterOS;

MikroTik-комутатори як елементи топології.


Оператор працює з філією, логічним вузлом і політикою, а не з окремими командами RouterOS.

Система повинна забезпечувати:

1. Єдине джерело істини для керованих firewall-правил.


2. Детерміновану генерацію конфігурації.


3. Безпечне застосування без проміжного неповного ruleset.


4. Автоматичний rollback при втраті доступу.


5. Топологічно коректне розгортання на VRRP та multi-WAN.


6. Виявлення ручних змін і конфліктів.


7. Повну відтворюваність кожної операції.


8. Незмінність некерованої конфігурації RouterOS.




---

2. Межі MVP

2.1. Керовані конфігурації

MVP повинен записувати:

RouterOS facility	Режим

/ip firewall filter	Повне керування власними chains
/ipv6 firewall filter	Повне керування власними chains
/ip firewall address-list	Лише статичні списки системи
/ipv6 firewall address-list	Лише статичні списки системи
Jump anchors у built-in chains	Повне керування
Management guard	Окремий захищений контур
Rollback watchdog	Окремий захищений контур


MVP повинен читати й аналізувати, але не змінювати:

RouterOS facility	Призначення

NAT	Перевірка залежностей
RAW	Семантичний аналіз
Mangle	Перевірка marks, PCC і policy routing
Routing tables і routes	Multi-WAN validation
Routing rules	Multi-WAN validation
Interface lists	Компіляція логічних зон
VRRP	Виявлення складу та ролей
Bridge/VLAN	Топологічний контекст
Switch-chip ACL	Capability analysis
IP services	Management-path validation


Firewall RouterOS має окремі input, forward та output chains для трафіку до маршрутизатора, через маршрутизатор і від маршрутизатора відповідно. IPv4 та IPv6 конфігурації є окремими доменами. 

2.2. Поза MVP

У першій версії заборонено:

змінювати routing, VRRP, DHCP, DNS, VPN і QoS;

змінювати NAT, RAW і Mangle;

записувати switch-chip ACL;

автоматично виправляти drift;

оновлювати RouterOS;

підтримувати сторонніх виробників;

виконувати довільні RouterOS CLI/API-команди;

автоматично імпортувати всі наявні правила в managed policy;

автоматично розгортати політику після її затвердження;

автоматично розгортати зміни після завершення строку винятку;

використовувати desktop-клієнт для прямого підключення до маршрутизаторів;

створювати multi-tenant модель.



---

3. Базова архітектура

┌───────────────────────────────────────┐
│ Desktop Client                       │
│ Avalonia UI / MVVM                   │
│                                       │
│ • Inventory                           │
│ • Policy editor                       │
│ • Validation                          │
│ • Semantic diff                       │
│ • Deployment monitor                  │
│ • Drift and audit                     │
└──────────────────┬────────────────────┘
                   │ gRPC + TLS/mTLS
┌──────────────────▼────────────────────┐
│ Central Controller                    │
│ ASP.NET Core modular monolith         │
│                                       │
│ • Authentication / RBAC               │
│ • Inventory                           │
│ • Topology                            │
│ • Policy compiler                     │
│ • Static analyzer                     │
│ • Snapshot / drift engine             │
│ • Deployment coordinator              │
│ • RouterOS adapter                    │
│ • Audit                               │
└───────────────┬─────────────┬─────────┘
                │             │
                │             └──────────────┐
                ▼                            ▼
        PostgreSQL                    RouterOS API-SSL
                                      через management VPN

Архітектура повинна складатися рівно з трьох основних deployable-компонентів:

1. Desktop client.


2. Central controller service.


3. PostgreSQL.



Не потрібні:

мікросервіси;

message broker;

distributed cache;

окремий workflow engine;

прямий доступ desktop-клієнта до PostgreSQL;

локальна база політик у desktop-клієнті.


Відмова central controller не повинна впливати на поточну роботу firewall: після застосування правила повністю виконуються RouterOS автономно.


---

4. Технологічний стек

Компонент	Технологія

Runtime	.NET 10 LTS
Desktop GUI	Avalonia 12.x
GUI pattern	MVVM
Controller	ASP.NET Core
GUI API	gRPC
Database	PostgreSQL 18
ORM/data access	EF Core + Npgsql або прямий Npgsql для критичних транзакцій
RouterOS transport	Власний типізований API-SSL client
Serialization	Protobuf для API, JSONB для політик і snapshots
Logging	Структурований JSON
Integration lab	MikroTik CHR
Desktop packaging	MSI/MSIX
Controller packaging	Windows Service або systemd service


.NET 10 є активним LTS-релізом до листопада 2028 року. Avalonia 12 є актуальною основною гілкою документації, а PostgreSQL 18 — поточною підтримуваною версією. Точні patch-версії повинні бути зафіксовані dependency lock-файлами. 


---

5. RouterOS transport

5.1. Основний канал

Controller повинен використовувати:

RouterOS native API-SSL
TCP 8729
TLS 1.2 або вище
обов’язкова перевірка сертифіката

RouterOS API використовує TCP 8728, а захищений api-ssl — TCP 8729. За наявності призначеного сертифіката API-SSL встановлює TLS-сесію. Пароль у самому API login-повідомленні передається відкритим значенням, тому підключення без перевіреного TLS-сертифіката заборонене. 

Заборонено:

API 8728;

anonymous Diffie–Hellman без сертифіката;

certificate validation bypass;

trust-on-every-connection;

plaintext credential logging;

зберігання RouterOS credentials у desktop-клієнті.


5.2. API client

Клієнт повинен підтримувати:

RouterOS sentence encoding;

.tag для кореляції запитів;

!re;

!done;

!empty;

!trap;

!fatal;

/cancel;

command timeout;

connection timeout;

cancellation;

reconnect;

bounded read buffer;

максимальний розмір відповіді;

redaction чутливих полів;

детермінований parsing незалежно від порядку attributes.


RouterOS API підтримує одночасні tagged-команди й окрему команду /cancel; помилки повертаються через !trap, завершення — через !done. 

Записи на один пристрій повинні виконуватися послідовно. Паралельні read-команди дозволені з обмеженою concurrency.

5.3. Обмеження доступу

На RouterOS api-ssl повинен бути обмежений одночасно:

1. Полем /ip service address.


2. Firewall-правилами.


3. Management VPN або окремою management-мережею.


4. Полем address RouterOS-користувача.


5. Внутрішнім CA або certificate pinning.



RouterOS дозволяє обмежувати IP services за source prefix, але документація окремо рекомендує використовувати firewall для блокування недовірених джерел. 


---

6. Модель інфраструктури

6.1. Основні сутності

Site
Node
Device
VrrpGroup
VrrpMember
Uplink
ZoneBinding
Policy
PolicyRevision
PolicyBinding
Snapshot
Deployment
NodeDeployment
DriftEvent
AuditEvent

Сутність Organization не потрібна: система обслуговує одну компанію.

6.2. Site

Site представляє філію або фізичний майданчик.

Site {
    id: UUID
    code: string
    name: string
    timezone: IANA timezone
    description: string?
    status: ACTIVE | DISABLED
}

code повинен бути унікальним і незмінним після створення.

6.3. Node

Node є мінімальною одиницею атомарності deployment.

Node {
    id: UUID
    site_id: UUID
    name: string
    kind: ROUTER | VRRP | SWITCH
    uplink_mode: SINGLE | FAILOVER | BALANCED | MIXED | NONE
    management_mode: OOB | WATCHDOG
    status: ACTIVE | MAINTENANCE | DISABLED
}

kind	Умова

ROUTER	Рівно один RouterOS router
VRRP	Два або більше RouterOS routers
SWITCH	Один MikroTik switch


6.4. Device

Device {
    id: UUID
    node_id: UUID
    identity: string
    management_host: IPAddress
    management_port: uint16
    routeros_version: string
    update_channel: string
    model: string
    serial_number: string?
    architecture: string
    device_role: ROUTER | L3_SWITCH | L2_SWITCH | UNKNOWN
    support_state:
        SUPPORTED |
        READ_ONLY |
        NEEDS_REVALIDATION |
        UNSUPPORTED
    capability_hash: SHA256
    credential_ref: SecretReference
    last_seen_at: UTC timestamp
}

Кожний VRRP router повинен мати власну фізичну management-адресу. Використання лише VRRP virtual IP для керування кластером заборонене.

6.5. VRRP

Один VRRP node може містити декілька VRRP groups.

VrrpGroup {
    id: UUID
    node_id: UUID
    family: IPv4 | IPv6
    vrid: uint8
    interface_key: string
    virtual_addresses: AddressPrefix[]
    advertisement_interval: Duration
    preemption: bool
}

VrrpMember {
    group_id: UUID
    device_id: UUID
    configured_priority: uint8
    configured_owner: bool
    observed_state: MASTER | BACKUP | INIT | UNKNOWN
    observed_at: UTC timestamp
}

Не дозволено зводити роль пристрою до одного поля master/backup: один router може бути master для одного VRID і backup для іншого.

VRRP у RouterOS формує логічний Virtual Router із VRID та virtual addresses; один router є master, інші — backup. MikroTik рекомендує однакову версію RouterOS для пристроїв одного VRID. 

6.6. Uplink

Uplink {
    id: UUID
    node_id: UUID
    key: string
    mode: PRIMARY | BACKUP | BALANCED | TRANSIT
    zone_key: string
    routing_table: string?
    source_address: IPAddress?
    probe_profile: ProbeProfile?
}

Controller не керує uplink routing. Він зберігає модель лише для компіляції та валідації firewall.

6.7. ZoneBinding

Policy не повинна посилатися на ether1, sfp-sfpplus1 або іншу фізичну назву безпосередньо.

ZoneBinding {
    node_id: UUID
    zone_key: string
    family: IPv4 | IPv6 | DUAL
    binding_type: INTERFACE_LIST | INTERFACE_SET
    binding_value: string[]
    resolved_members: string[]
    dependency_hash: SHA256
}

Приклади зон:

MGMT
LAN
DMZ
WAN_PRIMARY
WAN_BACKUP
WAN_BALANCED
SERVER
GUEST
VPN

Переважним binding є наявний RouterOS interface list. Controller не змінює interface lists у MVP. RouterOS interface lists можуть включати й виключати інші списки, а їх resolved membership не збігається лише з елементами /interface list member; тому controller повинен зберігати саме обчислену множину інтерфейсів. 


---

7. RouterOS capability model

7.1. Політика підтримки

Платформа	MVP support

RouterOS 7 stable/long-term із перевіреним manifest	Read/write
RouterOS 7 testing/development	Read-only
Нова невідома RouterOS version	NEEDS_REVALIDATION
RouterOS 6.49.x	Read-only
CHR RouterOS 7	Read/write
CRS під RouterOS 7	Management-plane only
SwOS	Inventory metadata only


Підтримка не повинна визначатися лише порівнянням номера версії.

Кожний release controller повинен містити versioned compatibility manifest:

RouterOS version
architecture
required menus
required properties
known incompatibilities
tested device classes
supported compiler schema

7.2. Capability profile

Під час discovery controller повинен визначати:

RouterOS version і channel;

architecture;

model/board;

packages;

device mode;

доступність IPv6;

підтримувані firewall properties;

наявність API-SSL certificate;

доступність scheduler/script watchdog;

switch-chip model;

hardware offload state;

наявність routing tables;

наявність VRRP;

підтримку необхідних commands.


Capability profile повинен бути хешований. Будь-яка його зміна скасовує всі ще не виконані deployment plans.


---

8. Discovery і snapshots

8.1. Discovery

Discovery виконується лише за явно заданими management addresses або prefixes. Безумовне сканування всієї корпоративної мережі не допускається.

Controller повинен отримати:

Категорія	Дані

System	identity, version, architecture, model, serial, uptime
Services	api-ssl state, port, certificate, allowed sources
Interfaces	type, state, MAC, addresses
Interface lists	definitions і resolved membership
VRRP	VRID, family, virtual IP, state, priority
Routing	tables, rules, active routes
Firewall	IPv4/IPv6 filter, NAT, RAW, Mangle
Address lists	static і dynamic entries окремо
Switching	bridge, ports, VLANs, hardware offload
Device capabilities	device-mode, packages, switch chip


Заборонено виконувати повний /export show-sensitive.

API-запити повинні використовувати явну .proplist, коли це підтримується, щоб не завантажувати зайві або чутливі поля.

8.2. Stable-read алгоритм

RouterOS не надає глобальний transactional snapshot усієї конфігурації. Тому controller повинен:

1. Прочитати hashes критичних menus.


2. Прочитати повний набір потрібних даних.


3. Повторно прочитати hashes критичних menus.


4. Прийняти snapshot лише за умови збігу першого й останнього наборів.


5. При розбіжності повторити операцію з bounded retry.


6. Після вичерпання retry створити SNAPSHOT_UNSTABLE.



Критичні menus:

filter
address-list
interface list
VRRP
routes/routing rules
NAT
Mangle
RAW
IP services
managed anchors

8.3. Canonical snapshot

Canonicalization повинна:

видаляти .id як persistent identity;

відокремлювати dynamic entries;

виключати packet/byte counters;

нормалізувати CIDR;

сортувати множини;

об’єднувати port intervals;

нормалізувати порожні/default values;

зберігати порядок firewall rules;

зберігати unknown properties у raw snapshot;

створювати SHA-256 canonical hash.


RouterOS internal .id дозволено використовувати лише в межах одного короткочасного API-сеансу.


---

9. Policy model

9.1. Ієрархія

Company baseline
        ↓
Site overlay
        ↓
Node overlay
        ↓
Approved temporary exceptions

Ефективна політика:

EffectivePolicy(node) =
    Compose(
        CompanyRevision,
        SiteRevision?,
        NodeRevision?,
        ActiveExceptions[]
    )

Policy revisions після затвердження є незмінними. Будь-яке виправлення створює нову revision.

9.2. Policy sections

Company baseline визначає набір і порядок sections.

Приклад:

010-system
100-state
200-control-plane
300-company-policy
400-site-policy
500-node-policy
600-exceptions
900-terminal

Кожна section задає:

PolicySection {
    key: string
    order: uint16
    allowed_scopes: PolicyScope[]
    required: bool
    terminal_allowed: bool
}

Overlay:

не може створювати нові sections;

не може змінювати їх порядок;

не може додавати правила у section, яка не дозволяє відповідний scope;

не може змінювати company rule;

не може видаляти company rule;

може посилатися лише на доступні object UUID.


9.3. Rule

Rule {
    id: UUID
    family: IPv4 | IPv6
    chain: INPUT | FORWARD | OUTPUT
    section_key: string
    order_key: uint32
    enabled: bool
    match: MatchExpression
    action: Action
    log: bool
    log_prefix: string?
    description: string
}

Комбінація:

family + chain + section_key + order_key

повинна бути унікальною в межах revision.

При однаковому order_key compiler не повинен використовувати неявний UUID tie-breaker — revision повинна бути відхилена.

9.4. Matchers MVP

Matcher	Запис

Source/destination address	Так
Address object	Так
Source/destination zone	Так
Protocol name/number	Так
Source/destination ports	Так
Connection state	Так
Connection NAT state	Так
TCP flags	Так
ICMP/ICMPv6 type і code	Так
Source/destination address type	Так
IPsec policy	Так
In/out interface list через zone	Так
Packet/connection mark	Read-only analysis
Connection limit/rate	Read-only analysis
Layer7/content	Read-only analysis
Random/Nth/PCC	Read-only analysis
Dynamic address-list actions	Read-only analysis


9.5. Actions MVP

ACCEPT
DROP
REJECT
JUMP
RETURN
FASTTRACK_CONNECTION

Обмеження:

JUMP і RETURN дозволені лише company baseline або compiler-generated rules;

циклічні jumps заборонені;

FASTTRACK_CONNECTION дозволений лише у FORWARD;

FastTrack rule може бути створений лише company baseline;

overlays не можуть створювати FastTrack;

REJECT повинен мати типізований reject-with;

RouterOS actions, які не підтримує schema, залишаються unmanaged.


9.6. Address objects

AddressObject {
    id: UUID
    name: string
    family: IPv4 | IPv6
    entries:
        HOST |
        PREFIX |
        RANGE
}

В MVP заборонено:

DNS/FQDN entries;

address-list timeout;

dynamic mutation;

add-src-to-address-list;

add-dst-to-address-list.


Статичні address objects повинні компілюватися у versioned або content-addressed RouterOS lists:

fwc.a.<hash12>
fwc6.a.<hash12>

RouterOS address lists можуть використовуватися у filter, NAT і Mangle, тому controller повинен перевіряти unmanaged references перед видаленням старого списку. 

9.7. Service objects

ServiceObject {
    id: UUID
    name: string
    protocol: Protocol
    source_ports: PortInterval[]
    destination_ports: PortInterval[]
    icmp_type: uint8?
    icmp_code: uint8?
}

Compiler повинен:

сортувати intervals;

об’єднувати intervals, що перекриваються;

відхиляти ports для protocol без port semantics;

відхиляти ICMP fields для не-ICMP protocol;

розділяти IPv4 ICMP та ICMPv6.


9.8. Policy tests

Policy revision повинна підтримувати test cases:

PolicyTestCase {
    id: UUID
    family: IPv4 | IPv6
    chain: INPUT | FORWARD | OUTPUT
    source_address: IPAddress
    destination_address: IPAddress
    source_zone: string?
    destination_zone: string?
    protocol: Protocol
    source_port: uint16?
    destination_port: uint16?
    connection_state: ConnectionState?
    expected_action: ACCEPT | DROP | REJECT
}

Обов’язкові системні tests:

доступ controller до API-SSL;

доступ з дозволених management prefixes;

заборона доступу з WAN до management service;

VRRP protocol traffic;

необхідні health-check flows;

доступність management VPN;

IPv4 та IPv6 окремо, якщо обидва активні.



---

10. Policy bindings

PolicyBinding {
    id: UUID
    scope: COMPANY | SITE | NODE | EXCEPTION
    scope_id: UUID?
    desired_revision_id: UUID
    valid_from: UTC timestamp?
    valid_until: UTC timestamp?
    state: DRAFT | APPROVED | ACTIVE | EXPIRED | DISABLED
}

Вимоги:

1. Одночасно може існувати лише одна активна company baseline revision.


2. Site і node можуть мати не більше однієї активної overlay revision кожного типу.


3. Exceptions можуть бути множинними.


4. Exception обов’язково має:

автора;

причину;

ticket/reference;

строк дії;

scope;

reviewer, якщо цього вимагає RBAC policy.



5. Завершення valid_until не запускає автоматичний deployment.


6. Після завершення строку створюється стан EXPIRED_PENDING_RECONCILIATION.




---

11. Effective policy hash

Для кожного node controller повинен обчислювати:

effective_policy_hash = SHA256(
    compiler_version
    + policy_schema_version
    + company_revision_hash
    + site_revision_hash?
    + node_revision_hash?
    + ordered_exception_hashes
    + topology_hash
    + zone_binding_hash
    + capability_hash
)

Deployment plan повинен бути недійсним після зміни будь-якого компонента.

Кожний пристрій може отримати окремий artifact_hash, оскільки фізичні interface bindings можуть відрізнятися.


---

12. Межа керування RouterOS

12.1. Namespace

Усі керовані ресурси повинні мати namespace fwc.

Приклади:

fwc.in.<revision-hash>
fwc.fwd.<revision-hash>
fwc.out.<revision-hash>

fwc6.in.<revision-hash>
fwc6.fwd.<revision-hash>
fwc6.out.<revision-hash>

fwc.a.<object-hash>
fwc6.a.<object-hash>

Коментар правила:

fwc:rule:<rule-uuid>:<revision-hash>

Коментар anchor:

fwc:anchor:ipv4:input
fwc:anchor:ipv4:forward
fwc:anchor:ipv4:output

12.2. Заборона змін unmanaged configuration

Controller не має права автоматично:

видаляти unmanaged rule;

вимикати unmanaged rule;

переміщувати unmanaged rule;

редагувати його comment;

перейменовувати unmanaged chain;

змінювати unmanaged address list;

змінювати interface list.


Unmanaged rule визначається відсутністю валідного fwc: ownership marker.

12.3. Anchors

Для кожної підтримуваної family/chain дозволений рівно один anchor.

built-in chain
    ├── management/safety prelude
    ├── unmanaged pre-anchor rules
    ├── jump -> fwc.<chain>.<active-revision>
    └── unmanaged post-anchor rules

Позиція anchor:

задається під час onboarding;

записується в topology;

контролюється drift detector;

не змінюється звичайним deployment;

повинна враховувати правила до і після anchor.


Managed chain може:

завершити обробку ACCEPT, DROP або REJECT;

виконати RETURN до unmanaged continuation.


Validator повинен показувати, які post-anchor rules стають недосяжними через terminal managed policy.


---

13. Management guard

Management guard є окремим від policy контуром.

Він повинен захищати:

API-SSL;

management VPN;

OOB interface;

controller source addresses;

за потреби SSH recovery channel.


Вимоги:

1. Guard розташований до managed anchor.


2. Source не може дорівнювати 0.0.0.0/0 або ::/0.


3. Guard не змінюється разом зі звичайною policy.


4. Зміна guard має окремий maintenance workflow.


5. Guard має власний audit trail.


6. Controller перевіряє його перед кожним deployment.


7. Видалення або переміщення guard є CRITICAL_DRIFT.


8. Normal deployment не може обійти перевірку guard навіть у emergency mode.




---

14. Onboarding пристрою

Послідовність onboarding:

NEW
 ↓
TLS_TRUST_ESTABLISHED
 ↓
READ_ONLY_DISCOVERY
 ↓
CAPABILITY_VALIDATED
 ↓
TOPOLOGY_ASSIGNED
 ↓
MANAGEMENT_PATH_VALIDATED
 ↓
BOOTSTRAP_PLAN_CREATED
 ↓
BOOTSTRAP_APPLIED
 ↓
BOOTSTRAP_VERIFIED
 ↓
MANAGED

Bootstrap plan може створювати лише:

management guard;

controller-owned root chains;

anchors;

rollback watchdog;

controller-owned static address lists.


Перед bootstrap обов’язково зберігається повний snapshot підтримуваних і dependency menus.

При помилці bootstrap controller повинен повернути кожний уже змінений ресурс до попереднього стану.

Пристрій не отримує MANAGED, доки:

anchor не підтверджений;

management access не підтверджений;

watchdog або OOB не підтверджений;

canonical snapshot не збережений;

capability profile не підтримується.



---

15. Static validation

15.1. Рівні результатів

ERROR
WARNING
INFO

Deployment заборонений за наявності хоча б одного ERROR.

WARNING може бути підтверджений лише для конкретного plan hash. Після зміни plan підтвердження анулюється.

15.2. Structural validation

Перевіряються:

schema version;

UUID;

family;

chain;

section;

order;

action;

protocol;

port ranges;

address family;

object references;

zone references;

jump targets;

унікальність order;

max rule/list limits;

RouterOS capability.


15.3. Semantic validation

Для підтримуваного matcher subset analyzer повинен визначати:

повні дублікати;

часткові дублікати;

shadowed rules;

unreachable rules;

суперечливі accept/drop;

terminal rule, що перекриває наступні rules;

jump cycles;

rules, які не можуть match;

IPv4/IPv6 family mismatch;

FastTrack conflicts;

unmanaged pre-anchor influence;

post-anchor reachability;

невикористовувані objects.


Аналіз повинен бути консервативним. Коли analyzer не може довести безпечність через unsupported matcher, результат має бути UNKNOWN, а не припущення.

15.4. Safety validation

Deployment блокується, коли:

management guard відсутній;

API-SSL path не дозволений;

policy блокує controller source;

management VPN flow стає недоступним;

VRRP control traffic блокується;

health-check traffic блокується;

active anchor не відповідає baseline snapshot;

є unsupported managed property;

один із required zones не має binding;

interface-list dependency змінився;

capability profile змінився;

RouterOS version не входить до compatibility manifest;

rollback channel недоступний.


VRRP використовує IP protocol 112 і multicast control traffic; ці flows повинні входити до обов’язкових control-plane tests. 

15.5. Multi-WAN validation

Для FAILOVER, BALANCED і MIXED перевіряються:

усі uplinks мають zone binding;

жоден backup uplink не пропущений policy;

management route існує щонайменше через один канал;

routing tables не зникли;

routing rules не змінилися після plan;

NAT dependencies не змінилися;

Mangle/PCC dependencies не змінилися;

source-address для route-specific probe валідний;

interface-list membership не змінився;

rp-filter не конфліктує з асиметричною маршрутизацією.


MikroTik зазначає, що strict reverse-path filtering конфліктує зі складною асиметричною маршрутизацією, VRRP і routing tables; для таких конфігурацій використовується loose mode. Controller повинен позначати strict mode як blocker або explicit topology exception. 


---

16. Semantic diff

Diff повинен показувати не набір RouterOS commands, а зміну змісту.

Rules:
  ADDED
  REMOVED
  MODIFIED
  MOVED
  ENABLED
  DISABLED

Objects:
  ADDED
  REMOVED
  ENTRY_ADDED
  ENTRY_REMOVED
  RENAMED

Effects:
  TEST_BECAME_ALLOWED
  TEST_BECAME_BLOCKED
  MANAGEMENT_PATH_CHANGED
  UNMANAGED_RULE_SHADOWED
  FASTTRACK_BEHAVIOR_CHANGED

Для кожного node GUI показує:

effective policy revision;

actual deployed hash;

desired hash;

current snapshot hash;

compiled artifact hash;

number of device operations;

warnings;

blockers;

expected active anchors;

rollback target.


RouterOS command plan доступний лише в окремому read-only technical view.


---

17. Deployment model

17.1. Atomicity boundary

Node є мінімальною одиницею deployment.

Не існує атомарної RouterOS-транзакції між декількома пристроями. Controller реалізує recoverable pseudo-transaction:

complete old revision;

complete staged new revision;

bounded anchor-switch window;

automated rollback;

durable state machine.


17.2. Deployment hierarchy

Deployment
 ├── NodeDeployment
 │    ├── DeviceDeployment
 │    └── DeviceDeployment
 ├── NodeDeployment
 └── NodeDeployment

Deployment представляє campaign по одному або багатьох nodes.

17.3. Deployment states

CREATED
PLANNING
BLOCKED
READY
STAGING
STAGED
ACTIVATING
VERIFYING
COMMITTED
PARTIAL
ABORTED
ROLLBACK_PENDING
ROLLING_BACK
ROLLED_BACK
RECOVERY_REQUIRED
FAILED

Дозволені переходи повинні бути явно задані state machine. Довільне присвоєння state заборонене.

17.4. Locks

Обов’язкові блокування:

один active deployment на node;

один writer на device;

один bootstrap на device;

один management-guard workflow на device.


Lock повинен бути durable:

resource_id
owner_instance_id
acquired_at
heartbeat_at
expires_at

Після crash lock не можна просто видалити. Новий controller instance спочатку виконує reconciliation з RouterOS.


---

18. Deployment plan

Plan повинен містити:

DeploymentPlan {
    id: UUID
    created_at: UTC timestamp
    created_by: UserId
    target_nodes: UUID[]
    policy_binding_hash: SHA256
    compiler_version: string
    per_node:
        precondition_snapshot_hash
        topology_hash
        capability_hash
        desired_effective_hash
        current_artifact_hash
        desired_artifact_hash
        staged_resources
        activation_order
        verification_checks
        rollback_target
    plan_hash: SHA256
}

Plan є незмінним.

Start deployment дозволений лише з точним plan_hash. Будь-яка зміна topology, snapshot, policy або capability анулює plan.


---

19. Staging

Staging виконується без впливу на active traffic.

Послідовність:

1. Створити immutable address lists.


2. Створити detached revision chains.


3. Додати всі rules у правильному порядку.


4. Повторно прочитати створені ресурси.


5. Canonicalize.


6. Порівняти з compiled artifact hash.


7. Перевірити, що active anchors не змінилися.


8. Позначити device як STAGED.



Заборонено редагувати active managed chain in-place.

На будь-якому етапі пакет повинен потрапляти або в повну стару revision, або в повну нову revision.


---

20. Rollback watchdog

20.1. Призначення

Watchdog повинен повернути anchors на попередню revision, якщо controller:

втратив API connection;

завершився аварійно;

не підтвердив deployment;

не завершив verification до deadline.


20.2. Вимоги

Watchdog:

встановлюється під час onboarding;

має фіксований controller-owned script;

може змінювати лише fwc: anchors;

не має права змінювати іншу RouterOS configuration;

активується як one-shot scheduler;

містить deployment UUID;

містить expected old і new targets;

виконує compare-before-restore;

не повинен відкотити пізніший deployment;

після виконання створює RouterOS log event;

після успішного commit деактивується controller.


RouterOS scheduler може виконувати script один раз у заданий момент при interval=0, що дозволяє створити локальний rollback deadline. 

Controller повинен обчислювати deadline відносно поточного часу самого RouterOS, а не покладатися на однаковий UTC clock.

Рекомендований діапазон watchdog TTL:

minimum: 60 seconds
default: 120 seconds
maximum: 600 seconds

Конкретне значення задається node profile.

Safe Mode не використовується як єдиний rollback mechanism: він прив’язаний до сесії, має обмежену history і після перевищення ліміту не гарантує автоматичне undo. 

20.3. OOB mode

Watchdog дозволено не використовувати лише тоді, коли node має незалежний перевірений OOB path, який:

не проходить через змінювані chains;

дає доступ до кожного фізичного пристрою;

був перевірений безпосередньо перед deployment.



---

21. Activation

Загальний порядок:

PRECONDITION RECHECK
        ↓
ARM WATCHDOG
        ↓
ACTIVATE NON-MANAGEMENT CHAINS
        ↓
ACTIVATE MANAGEMENT-AFFECTING CHAIN LAST
        ↓
POST-READ
        ↓
VERIFY
        ↓
COMMIT OR ROLLBACK

Activation order будується з management dependency graph.

Для звичайного direct API connection типовий порядок:

forward
output
input

Але він не повинен бути hardcoded: management VPN може залежати від forward, RAW, Mangle або routing.

Перед кожною зміною anchor controller повинен повторно перевіряти:

anchor ownership comment;

current jump target;

rule position;

current snapshot fingerprint;

watchdog state.


External RouterOS session не може бути фізично заблокована controller. Тому ручні зміни під час deployment є забороненою операційною практикою; post-read повинен виявити будь-яку розбіжність.


---

22. Verification

22.1. Control-plane checks

Обов’язково:

TLS handshake;

RouterOS certificate validation;

API authentication;

expected active anchor targets;

expected chain hashes;

expected address-list hashes;

management guard presence;

watchdog state;

no unexpected drift.


22.2. Data-plane checks

Node profile повинен містити набір probes:

ICMP
TCP connect
HTTPS GET
DNS
router-originated route-specific ping

Probe:

Probe {
    source: CONTROLLER | ROUTER
    destination: address/hostname
    protocol: ICMP | TCP | HTTPS | DNS
    port: uint16?
    routing_table: string?
    source_address: IPAddress?
    expected: REACHABLE | UNREACHABLE
    critical: bool
}

INCONCLUSIVE для critical probe прирівнюється до failure.

22.3. Commit

Commit дозволений лише коли:

усі critical probes успішні;

anchors відповідають plan;

artifact hashes збігаються;

topology не змінилася;

capability не змінилася;

VRRP state стабільний;

watchdog ще активний і має достатній remaining TTL.


Після цього:

1. Зберігається committed snapshot.


2. Оновлюється actual deployed hash.


3. Watchdog деактивується.


4. Deployment отримує COMMITTED.


5. Стара revision залишається для rollback.




---

23. Rollback

Rollback виконується шляхом повернення anchor target, а не повторного створення старих rules.

Порядок:

1. Повернути management-affecting anchors.


2. Перевірити API access.


3. Повернути інші anchors.


4. Перевірити old artifact hash.


5. Деактивувати watchdog.


6. Зберегти rollback snapshot.


7. Позначити deployment ROLLED_BACK.



При невдалому rollback:

RECOVERY_REQUIRED

Система повинна показати:

affected device;

current anchor;

expected anchor;

доступні management paths;

останню успішну revision;

точні manual recovery commands.


Manual recovery commands генеруються лише для конкретного plan і не повинні містити credentials.


---

24. Standalone router deployment

Для Node.kind=ROUTER:

1. Перевірити, що node містить рівно один device.


2. Оновити snapshot.


3. Перевірити topology/capability hashes.


4. Stage.


5. Arm watchdog.


6. Activate.


7. Verify.


8. Commit або rollback.



Повторне застосування того самого artifact hash повинно завершуватися як:

NO_CHANGES

без RouterOS write-команд.


---

25. Multi-WAN deployment

Multi-WAN node залишається одним RouterOS device і одним transaction target.

Controller не повинен трактувати окремі uplinks як окремі devices.

Перед activation перевіряються:

active routes;

backup routes;

recursive failover dependencies;

routing tables;

routing rules;

Mangle/PCC marks;

NAT per uplink;

interface-list membership;

management route;

probe source addresses;

асиметрична маршрутизація.


Після activation:

виконується management probe;

виконується probe через кожний доступний uplink;

controller не перемикає failover примусово;

штучне відключення primary WAN не входить у production deployment;

реальний failover перевіряється в CHR integration tests і під час окремого maintenance test.



---

26. VRRP deployment protocol

26.1. Класифікація devices

Device є:

STANDBY_ONLY — backup для всіх relevant VRRP groups і не маршрутизує незалежний трафік;

TRAFFIC_BEARING — master хоча б для одного relevant group або має незалежний routed traffic;

UNKNOWN — роль не визначена.


UNKNOWN блокує activation.

26.2. Алгоритм

LOCK NODE
   ↓
READ ALL MEMBERS
   ↓
VALIDATE GROUP CONSISTENCY
   ↓
STAGE ALL MEMBERS
   ↓
VERIFY ALL STAGED ARTIFACTS
   ↓
READ ROLE VECTOR
   ↓
ACTIVATE STANDBY_ONLY MEMBERS
   ↓
VERIFY
   ↓
READ ROLE VECTOR AGAIN
   ↓
ACTIVATE TRAFFIC_BEARING MEMBERS ONE BY ONE
   ↓
VERIFY EACH MEMBER
   ↓
VERIFY CLUSTER
   ↓
COMMIT ALL

26.3. Обов’язкові інваріанти

усі members доступні перед staging;

усі members мають staged revision;

old revision не видаляється;

roles читаються перед кожною activation;

router може бути active для одного VRID і standby для іншого;

split-master VRRP розглядається як active-active;

жодний device не вважається пасивним лише через нижчий priority;

VRRP configuration controller не змінює;

різні RouterOS versions одного VRID блокують deployment, якщо немає explicit compatibility exception;

cluster не отримує COMMITTED, доки всі devices не підтвердили new revision.


26.4. Role change під час deployment

При зміні role vector controller переходить у:

RECONCILING

Далі:

1. Якщо ще не активовано жодного нового artifact — повторно класифікувати devices і продовжити.


2. Якщо хоча б один new artifact активований і всі members staged та доступні — завершити roll-forward.


3. Якщо не всі members staged або доступні — rollback усіх уже активованих members.


4. Якщо ні roll-forward, ні rollback не доведені — RECOVERY_REQUIRED.



Примусова VRRP role зміна системою заборонена.


---

27. Deployment campaigns

Deployment по багатьох філіях виконується campaign batches.

RolloutPolicy {
    canary_nodes: UUID[]
    batch_size: uint16
    max_parallel_nodes: uint16
    failure_action:
        STOP |
        ROLLBACK_CURRENT_BATCH |
        ROLLBACK_ALL_CAMPAIGN_NODES
}

Вимоги:

target list незмінний після start;

жодний node не пропускається мовчки;

blocked node показується до start;

один node failure автоматично rollback-иться локально;

між batches перевіряється campaign stop condition;

committed node не rollback-иться автоматично без заданої failure_action;

campaign може завершитися PARTIAL;

policy revision під час campaign не редагується;

новий target потребує нового campaign.



---

28. Drift detection

28.1. Порівняння

Desired effective policy
           ↕
Last committed artifact
           ↕
Current RouterOS state

28.2. Drift classes

Клас	Severity	Deployment

Managed rule changed	Critical	Block
Managed rule reordered	Critical	Block
Anchor missing	Critical	Block
Anchor moved	Critical	Block
Anchor target changed	Critical	Block
Management guard changed	Critical	Block
Managed address object changed	Critical	Block
Zone binding changed	Critical	Block
Interface-list resolved members changed	Critical	Block
RouterOS version changed	Critical	Revalidate
Capability changed	Critical	Revalidate
VRRP membership changed	Critical	Rebuild topology
New unmanaged pre-anchor rule	Critical/Warning	Analyze
New unmanaged post-anchor rule	Warning	Analyze
Counters changed	None	Ignore
Dynamic address-list entry changed	None	Store separately
Device uptime changed	None	Informational


Drift не виправляється автоматично.

Доступні дії:

ACKNOWLEDGE
IMPORT_AS_NEW_POLICY_DRAFT
RESTORE_DESIRED_STATE_VIA_DEPLOYMENT
MARK_AS_AUTHORIZED_UNMANAGED_CHANGE

Кожна дія створює audit event.


---

29. MikroTik switches

29.1. RouterOS CRS

У MVP дозволено:

inventory;

bridge/VLAN topology reading;

hardware-offload status reading;

management-plane input policy;

drift detection;

capability reporting.


Заборонено:

керувати transit forward policy як звичайним router firewall;

припускати, що hardware-switched traffic проходить CPU firewall;

автоматично вимикати hardware offload;

записувати switch ACL.


Різні MikroTik switch chips мають різні набори можливостей, тому capability не можна визначати лише за загальною ознакою CRS. 

29.2. SwOS

SwOS device:

може бути записаний в inventory;

не є RouterOS API target;

не є firewall deployment target;

не впливає на campaign, крім topology warnings.



---

30. Authentication і RBAC

30.1. User authentication

Production authentication:

Corporate OIDC provider
MFA controlled by corporate IdP
short-lived access token
refresh token protected by OS credential store

Local account дозволений лише як controller break-glass administrator.

30.2. Ролі

Роль	Права

Viewer	Read inventory, policy, deployments
PolicyEditor	Створення і редагування drafts
Reviewer	Validation review і approval
Deployer	Створення plan і запуск deployment
Administrator	Inventory, topology, credentials, RBAC
Auditor	Read-only audit і export


Один користувач може мати декілька ролей.

Separation of duties повинна бути конфігурованою:

звичайна зміна може дозволяти self-approval;

high-risk policy може вимагати окремого Reviewer;

emergency deployment може обходити separation of duties, але не safety validation.


30.3. RouterOS accounts

Рекомендовано два окремі service accounts:

fwc-discovery
fwc-deploy

Вони повинні мати:

custom user groups;

мінімальні RouterOS policies;

source address restrictions;

окремі credentials;

rotation metadata.


Стандартні RouterOS groups мають ширші права, ніж випливає з назв; навіть default read містить низку чутливих policies. Тому controller повинен перевіряти custom group, а не покладатися на default groups. 


---

31. Secrets

RouterOS credentials:

не передаються desktop-клієнту;

не повертаються через gRPC;

не зберігаються в audit;

не потрапляють у logs;

не входять у snapshots;

не зберігаються plaintext у PostgreSQL.


Зберігання:

secret plaintext
   ↓
random per-secret DEK
   ↓
authenticated encryption
   ↓
DEK wrapped by server master key
   ↓
ciphertext in PostgreSQL

Master key:

зберігається поза PostgreSQL;

не входить у application settings;

не входить в repository;

захищається OS secret facility, TPM або зовнішнім corporate secret store.



---

32. Audit

Audit має бути append-only на application layer і tamper-evident.

AuditEvent {
    id: UUID
    timestamp: UTC
    actor_id: UUID?
    action: string
    target_type: string
    target_id: UUID?
    correlation_id: UUID
    request_id: UUID?
    payload: JSON
    previous_hash: SHA256
    event_hash: SHA256
}

Audit повинен містити:

login і authorization failures;

inventory changes;

topology changes;

policy draft changes;

validation;

approval;

binding changes;

plan creation;

deployment start;

кожний state transition;

RouterOS effect summary;

verification;

rollback;

drift;

credential rotation metadata;

emergency actions.


Audit не повинен містити:

passwords;

private keys;

session tokens;

повний certificate private material;

sensitive RouterOS fields.


Періодично формується signed audit checkpoint для зовнішнього зберігання або SIEM.


---

33. Controller API

33.1. Загальні правила

Усі mutation RPC повинні підтримувати:

authentication;

authorization;

idempotency key;

optimistic concurrency;

correlation ID;

audit;

explicit error code;

cancellation;

deadline.


GUI не має API для довільної RouterOS command execution.

33.2. Services

Service	Основні RPC

InventoryService	ListSites, GetNode, AddDevice, DiscoverDevice, RefreshNode
TopologyService	UpdateNode, BindZone, DefineUplink, ConfirmVrrp
PolicyService	CreateDraft, UpdateDraft, Validate, Submit, Approve
BindingService	SetDesiredRevision, AddException, DisableException
DiffService	GetPolicyDiff, GetNodeEffectiveDiff
DeploymentService	CreatePlan, Start, Abort, Rollback, GetStatus
DriftService	ListDrift, GetDrift, Acknowledge
AuditService	Search, Export
CredentialService	AddCredential, RotateCredential, TestCredential


33.3. Deployment RPC

CreatePlanRequest {
    node_ids: UUID[]
    binding_set_hash: SHA256
    idempotency_key: UUID
}

StartDeploymentRequest {
    plan_id: UUID
    plan_hash: SHA256
    rollout_policy: RolloutPolicy
    reason: string
    ticket_reference: string?
    idempotency_key: UUID
}

WatchDeployment {
    deployment_id: UUID
}

WatchDeployment повинен бути server-streaming RPC.

33.4. Optimistic concurrency

Редаговані ресурси мають:

row_version: uint64

Update без актуального row_version повертає:

CONCURRENCY_CONFLICT


---

34. Схема PostgreSQL

34.1. Основні таблиці

Таблиця	Основні поля

sites	id, code, name, timezone, status, row_version
nodes	id, site_id, name, kind, uplink_mode, management_mode, status
devices	id, node_id, management_host, version, model, capability_hash
vrrp_groups	id, node_id, family, vrid, interface_key
vrrp_members	group_id, device_id, priority, observed_state
uplinks	id, node_id, key, mode, zone_key, routing_table
zone_bindings	id, node_id, zone_key, binding_type, dependency_hash
policies	id, name, scope, status
policy_revisions	id, policy_id, revision_no, schema_version, content_hash, state
policy_sections	revision_id, key, order_no, allowed_scopes
policy_rules	revision_id, rule_id, family, chain, section_key, order_key
address_objects	revision_id, object_id, name, family, entries_jsonb
service_objects	revision_id, object_id, name, definition_jsonb
policy_tests	revision_id, test_id, input_jsonb, expected_action
policy_bindings	id, scope, scope_id, desired_revision_id, validity
snapshots	id, device_id, canonical_hash, canonical_jsonb, raw_blob
deployments	id, plan_hash, rollout_policy, state
node_deployments	id, deployment_id, node_id, state, hashes
device_deployments	id, node_deployment_id, device_id, state
deployment_steps	id, device_deployment_id, sequence, operation, result
drift_events	id, device_id, class, severity, hashes, state
audit_events	id, timestamp, actor, action, payload, hash chain
idempotency_records	key, actor, operation, request_hash, response_ref
encrypted_secrets	id, ciphertext, wrapped_dek, algorithm, rotated_at


34.2. Обмеження

Обов’язкові constraints:

sites.code unique;

один management endpoint не може належати двом active devices;

ROUTER node має рівно один device;

VRRP node має щонайменше два devices;

(node_id, family, vrid, interface_key) unique;

(node_id, zone_key) unique;

(revision_id, family, chain, section_key, order_key) unique;

approved revision immutable;

один active company binding;

один active site overlay на site;

один active node overlay на node;

один nonterminal node deployment на node;

timestamps зберігаються в UTC;

audit і deployment history не видаляються cascade;

snapshots не перезаписуються;

secrets не мають plaintext column.



---

35. Desktop GUI

35.1. Модулі

Dashboard
Inventory
Topology
Policies
Validation
Diff
Deployments
Drift
Audit
Administration

35.2. Inventory

Відображення:

Company
 └── Site
      └── Node
           ├── Device
           └── Device

Для node показуються:

reachable devices;

RouterOS versions;

support status;

actual/desired policy;

drift;

VRRP role vector;

uplinks;

last snapshot;

active deployment.


35.3. Policy editor

Editor повинен підтримувати:

table view;

deterministic drag/reorder;

object picker;

zone picker;

protocol/port validation;

copy rule;

enable/disable;

section visibility;

policy tests;

inline validation;

immutable approved revision view.


RouterOS syntax не є основним форматом редагування.

35.4. Deployment screen

Перед запуском показуються:

target nodes;

affected devices;

semantic diff;

warnings;

blockers;

campaign batches;

canary;

rollback policy;

management path;

estimated RouterOS operations;

plan hash;

reason/ticket.


Не допускається кнопка, яка одночасно зберігає draft і застосовує його.

35.5. Offline behavior

При втраті controller connection desktop:

переходить у read-only cached mode;

не дозволяє deployment;

не дозволяє approval;

не зберігає RouterOS credentials;

чітко показує час останнього актуального server state.



---

36. Error model

Error {
    code: string
    severity: INFO | WARNING | ERROR | CRITICAL
    retryable: bool
    correlation_id: UUID
    target_type: string?
    target_id: UUID?
    message: localized string
    technical_details: sanitized JSON?
}

Основні codes:

AUTHENTICATION_FAILED
AUTHORIZATION_DENIED
CONCURRENCY_CONFLICT
DEVICE_UNREACHABLE
TLS_CERTIFICATE_INVALID
TLS_CERTIFICATE_EXPIRED
API_AUTHENTICATION_FAILED
API_TIMEOUT
API_TRAP
API_PROTOCOL_ERROR
UNSUPPORTED_ROUTEROS_VERSION
CAPABILITY_CHANGED
SNAPSHOT_FAILED
SNAPSHOT_UNSTABLE
TOPOLOGY_CHANGED
ZONE_BINDING_CHANGED
POLICY_INVALID
POLICY_TEST_FAILED
DRIFT_CONFLICT
MANAGEMENT_GUARD_INVALID
MANAGEMENT_PATH_UNSAFE
WATCHDOG_UNAVAILABLE
WATCHDOG_ARM_FAILED
VRRP_INCONSISTENT
VRRP_ROLE_CHANGED
STAGING_FAILED
ARTIFACT_HASH_MISMATCH
ACTIVATION_FAILED
VERIFICATION_FAILED
ROLLBACK_FAILED
RECOVERY_REQUIRED
CAMPAIGN_PARTIAL

Raw RouterOS error не повинен безпосередньо показуватися звичайному оператору без sanitization.


---

37. Нефункціональні вимоги

37.1. Надійність

Controller crash не повинен залишати partially edited active chain.

Усі deployment state transitions зберігаються транзакційно.

Після restart controller відновлює незавершені deployments через reconciliation.

Повторення idempotent step не повинно створювати дублікати.

Невідомий стан трактується fail-closed.

Старі revision chains не видаляються до завершення grace period.

Cleanup не виконується під час active deployment.


37.2. Bounded resources

Заборонені:

unbounded queues;

необмежена кількість RouterOS sessions;

необмежені retries;

необмежені snapshot payloads;

необмежені background tasks.


Початкові production limits:

max concurrent device reads: 32
max concurrent node writes: 8
max writes per device: 1
connect timeout: 5 s
default command timeout: 30 s
snapshot timeout: 120 s
bounded retry count: 3

Значення повинні бути конфігурованими.

37.3. Масштаб

MVP повинен підтримувати щонайменше:

1000 devices
500 sites
10000 policy rules across all active revisions
100 concurrent desktop sessions

Без деградації GUI через виконання RouterOS operations у UI thread.

37.4. Performance

cached inventory query: до 1 секунди;

policy validation для 1000 rules: до 2 секунд;

semantic diff для 1000 rules: до 2 секунд;

GUI не блокується remote operations;

snapshot jobs використовують jitter;

PostgreSQL queries для списків мають pagination.


37.5. Observability

Controller повинен мати:

structured logs;

correlation ID;

deployment ID;

node/device ID;

metrics;

health endpoint;

DB connection health;

RouterOS connection metrics;

deployment duration metrics;

drift counters;

watchdog activation counters.


Logs не містять secrets.

37.6. Backup

Backup повинен охоплювати:

PostgreSQL;

wrapped secrets;

controller configuration;

CA trust configuration;

audit checkpoints.


Відновлення backup повинно регулярно перевірятися integration test.


---

38. Test strategy

38.1. Unit tests

Обов’язкові domains:

canonicalization;

policy composition;

object resolution;

port normalization;

rule ordering;

compiler;

hash generation;

state transitions;

RBAC;

error mapping.


38.2. Property-based tests

Інваріанти:

Canonicalize(Canonicalize(x)) == Canonicalize(x)

Compile(policy, topology, capabilities)
always produces the same artifact hash

ApplySameRevisionTwice
produces zero second-run mutations

Rollback(Activate(old, new))
returns active target to old

38.3. RouterOS API tests

Перевіряються:

fragmented TCP frames;

multiple replies;

out-of-order tagged replies;

!trap;

!fatal;

timeout;

/cancel;

reconnect;

malformed length;

oversized response;

connection loss during write;

certificate mismatch.


38.4. CHR integration matrix

Standalone IPv4
Standalone dual-stack
Single router dual-WAN failover
Single router PCC balancing
VRRP active/passive
VRRP with multiple VRIDs
VRRP split-master
VRRP role change during staging
VRRP role change during activation
Unreachable cluster member
RouterOS version mismatch
Manual rule drift
Anchor movement
Controller crash
Database restart
Watchdog expiry

38.5. Fault injection

Connection повинно примусово розриватися після кожного deployment step:

after lock
after snapshot
after first staged object
after complete staging
after watchdog arm
after first anchor switch
during verification
before watchdog cancel
after DB commit

Для кожної точки має бути доведений кінцевий стан:

old committed
new committed
rolled back
recovery required with exact instructions

Невизначений стан заборонений.

38.6. Security tests

TLS MITM;

invalid certificate;

expired certificate;

stolen desktop token;

privilege escalation;

direct gRPC mutation without role;

secret extraction from DB;

credential presence in logs;

arbitrary RouterOS command injection;

policy JSON injection;

oversized payload;

audit hash modification;

replay idempotency key.



---

39. Acceptance criteria

MVP вважається готовим лише після виконання всіх критеріїв:

1. Однаковий policy input завжди створює однаковий artifact hash.


2. Повторний deployment тієї самої revision не створює RouterOS changes.


3. Unmanaged rules не видаляються, не переміщуються і не редагуються.


4. Active chain ніколи не редагується in-place.


5. Розрив API під час activation запускає watchdog rollback.


6. Controller crash під час deployment не залишає node без визначеного стану.


7. Втрата management access призводить до автоматичного повернення old anchors.


8. Manual change managed rule виявляється до наступного deployment.


9. Manual change anchor блокує deployment.


10. Зміна interface-list membership анулює plan.


11. Нова RouterOS version не отримує write support автоматично.


12. VRRP failover після успішного deployment не змінює active policy revision.


13. VRRP role change під час deployment коректно переходить у reconciliation.


14. Усі VRRP members мають однаковий committed effective revision.


15. Split-master VRRP не помилково класифікується як active/passive.


16. Multi-WAN policy перевіряє primary, backup і balanced uplinks.


17. Switch transit policy не записується через router firewall adapter.


18. Жодний RouterOS credential не потрапляє до desktop, logs або audit.


19. Кожна effectful операція має actor, reason, plan hash і audit event.


20. Deployment відновлюється після restart controller.


21. Database restore відновлює policies, bindings, deployments і audit chain.


22. Unsupported matcher у managed policy блокує compilation.


23. Policy test failure блокує approval або deployment.


24. Campaign не пропускає blocked node мовчки.


25. Rollback повертає точний попередній anchor target, а не реконструйовану приблизну конфігурацію.




---

40. Roadmap реалізації MVP

Етап	Результат	Exit gate

1. Foundation	Domain model, contracts, PostgreSQL, RBAC	Міграції й state invariants протестовані
2. RouterOS read path	API-SSL client, discovery, snapshots	CHR snapshots детерміновані
3. Canonicalization	Typed RouterOS model, hashes, drift	Повторний snapshot має той самий hash
4. Policy core	Editor model, objects, composition	Effective policy детермінована
5. Validation	Static analyzer, tests, semantic diff	Unsafe policy не компілюється
6. Standalone deployment	Staging, anchors, watchdog, rollback	Fault injection пройдений
7. Multi-WAN	Dependency analysis, uplink probes	Failover/PCC CHR tests пройдені
8. VRRP	Cluster coordinator, role reconciliation	Усі VRRP scenarios пройдені
9. Campaigns	Canary, batching, stop/rollback policy	Partial failure обробляється
10. Hardening	Security, backup, observability, packaging	Усі acceptance criteria виконані



---

41. Перший вертикальний зріз

Першою реалізацією має бути повністю read-only ланцюг:

Desktop GUI
    ↓
Controller gRPC
    ↓
RouterOS API-SSL
    ↓
Discovery
    ↓
Raw snapshot
    ↓
Canonical snapshot
    ↓
Hash
    ↓
Semantic representation
    ↓
GUI diff між двома snapshots

У цьому зрізі заборонено:

створювати rules;

змінювати anchors;

встановлювати watchdog;

виконувати deployment;

автоматично виправляти drift.


Критерій завершення першого зрізу:

> Controller підключається до standalone, multi-WAN і VRRP CHR-вузлів, правильно визначає topology, отримує повний підтримуваний snapshot, канонізує його, повторно отримує той самий hash за відсутності змін і показує точний diff після контрольованої зміни RouterOS.



Наступний нормативний документ: MikroTik Firewall Controller — Repository Bootstrap Plan v0.1.