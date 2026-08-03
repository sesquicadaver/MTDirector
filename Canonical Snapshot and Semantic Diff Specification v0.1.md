MikroTik Firewall Controller

Canonical Snapshot and Semantic Diff Specification v0.1

Дата: 3 серпня 2026 року
Статус: нормативна специфікація M1


---

1. Призначення

Документ визначає єдине канонічне представлення конфігурації RouterOS, механізм її хешування, незмінне зберігання snapshots і детерміноване семантичне порівняння.

Цей рівень потрібен для основної задачі проєкту:

фактична конфігурація MikroTik
        ↓
канонічний стан
        ↓
порівняння з корпоративною firewall-політикою
        ↓
безпечний deployment
        ↓
rollback і drift detection

Специфікація охоплює:

standalone routers;

один router із multi-WAN failover;

один router із балансуванням каналів;

VRRP active/passive;

VRRP split-master;

RouterOS CRS management plane;

firewall-related routing, NAT, RAW і Mangle dependencies.


Специфікація не створює:

універсальну модель усієї RouterOS;

NMS;

систему зберігання traffic statistics;

редактор routing;

RouterOS write path;

універсальний конфігураційний diff усіх підсистем пристрою.



---

2. Нормативні уточнення попередніх документів

2.1. Зберігання snapshots

Попередня модель містила дублювання:

configuration_payload_hash
configuration_hash

observation_payload_hash
observation_hash

У цій специфікації дублювання усувається.

Hash канонічного payload одночасно є:

content-addressed storage key;

logical section/document hash;

integrity checksum.


Окремі *_payload_hash і *_hash для одного й того самого payload не створюються.


---

2.2. Рівень зберігання

Snapshots зберігаються по секціях, а не одним великим canonical blob.

Додається таблиця:

snapshot_capture_sections

Це потрібно для:

section-level deduplication;

bounded pagination;

часткового читання;

section-level integrity verification;

швидкого semantic diff;

відсутності необхідності розпаковувати весь snapshot.



---

2.3. Diff contract

Один record може одночасно бути:

MODIFIED
MOVED

Тому DiffEntry.change замінюється на:

repeated DiffChange changes

Назва stable_key замінюється на:

record_key

Оскільки ключ не завжди є доведеною міжsnapshotною identity. Рівень достовірності задає MatchConfidence.


---

3. Основні інваріанти

1. Однакова конфігурація створює ідентичні canonical bytes.


2. Ідентичні canonical bytes створюють ідентичний SHA-256.


3. Capture timestamps не впливають на configuration hash.


4. RouterOS runtime state не впливає на configuration hash.


5. Configuration state не змішується з observations.


6. Counters не входять до canonical snapshot.


7. RouterOS .id не входить до canonical snapshot.


8. Порядок firewall rules зберігається.


9. Порядок елементів множини нормалізується.


10. Відсутнє поле не підміняється null, false, 0 або порожнім рядком.


11. Unknown property не ігнорується.


12. Invalid value не підміняється default value.


13. Invalid UTF-8 не проходить lossy replacement.


14. Unmanaged rules не отримують вигаданої identity.


15. Однозначно не зіставлені records повертаються як REMOVED і ADDED.


16. Dynamic firewall rules не створюють configuration drift.


17. Dynamic address-list addresses не зберігаються у відкритому вигляді.


18. VRRP role change змінює observations, але не configuration.


19. Active route change змінює observations, але не configuration.


20. Static route change змінює configuration.


21. Hash обчислюється до compression.


22. Compression не впливає на hash.


23. Completed snapshot immutable.


24. Snapshot із пошкодженим payload не повертається користувачу.


25. Semantic diff виконується лише Controller, а не Desktop.


26. Node topology projection є похідним результатом, а не джерелом істини.


27. Жоден canonical record не містить RouterOS credentials.


28. Raw і canonical payload мають окремі права доступу.


29. Diff має обмеження часу та пам’яті.


30. При перевищенні algorithm limit Controller не переходить до unbounded алгоритму.




---

4. Домени snapshot

Кожний completed Device snapshot складається з чотирьох незалежних canonical documents:

Configuration
Observations
Capabilities
Compatibility

4.1. Configuration

Містить відомі значення, які визначають конфігурацію пристрою:

firewall rules
static address lists
interfaces configuration
IP addresses configuration
interface lists
NAT
RAW
Mangle
routing tables
routing rules
static routes
IP settings
VRRP configuration
bridge/VLAN configuration
management services


---

4.2. Observations

Містить runtime state:

interface running state
dynamic interfaces
dynamic IP addresses
active default routes
route reachability
VRRP roles
dynamic firewall rules
dynamic address-list digests
hardware-offload state
bridge runtime membership
invalid/inactive flags


---

4.3. Capabilities

Містить:

RouterOS version
architecture
model
board
packages
available menus
available fields
IPv4/IPv6 support
VRRP support
bridge support
switch-chip visibility
compatibility manifest hash
support state


---

4.4. Compatibility

Містить:

unknown properties
unknown enum values
missing required properties
unsupported sections
parse failures
malformed controller ownership markers
duplicate controller rule IDs
section profile mismatches
redacted unknown values


---

5. Канонічний формат MFC-CJ1

Canonical bytes використовують спеціально обмежений JSON-профіль:

MFC Canonical JSON v1
MFC-CJ1

Використання звичайного JSON serializer без canonical writer заборонене.

5.1. Загальні правила

Encoding:                 UTF-8
BOM:                      заборонений
Whitespace:               заборонений
Trailing newline:         заборонений
JSON numbers:             заборонені
null:                     заборонений
Duplicate object keys:    заборонені
Object key order:         фіксований схемою
Array order:              визначений схемою
Hash text:                lowercase hexadecimal

JSON booleans дозволені:

true
false

Integer values серіалізуються як decimal strings:

["distance","u64","10"]

Це виключає втрату точності в JSON-клієнтах.


---

5.2. String encoding

Canonical writer:

" серіалізує як \";

\ серіалізує як \\;

/ не екранує;

\b, \f, \n, \r, \t використовує стандартні JSON escapes;

інші control characters U+0000..U+001F серіалізує як \u00xx;

hex digits в \u00xx — lowercase;

non-ASCII characters записує безпосередньо UTF-8;

Unicode normalization не виконує.


Таким чином два різні Unicode byte sequences не підміняються одним значенням.


---

6. Canonical section

Формат секції:

{
  "schema": "mfc.canonical-section/1",
  "domain": "configuration",
  "section": "firewall.ipv4.filter",
  "version": "1",
  "ordered": true,
  "records": []
}

Фактичний canonical payload не містить whitespace:

{"schema":"mfc.canonical-section/1","domain":"configuration","section":"firewall.ipv4.filter","version":"1","ordered":true,"records":[]}

Порядок полів є нормативним:

schema
domain
section
version
ordered
records


---

7. Canonical record

7.1. Unordered section

{
  "key": "service|api-ssl",
  "fields": [
    ["name", "str", "api-ssl"],
    ["port", "u64", "8729"],
    ["disabled", "bool", false]
  ]
}

Canonical bytes:

{"key":"service|api-ssl","fields":[["name","str","api-ssl"],["port","u64","8729"],["disabled","bool",false]]}


---

7.2. Ordered section

{
  "key": "fw-rule|ipv4|filter|550e8400-e29b-41d4-a716-446655440000",
  "ordinal": "0",
  "fields": []
}

Порядок record fields:

key
ordinal
fields

ordinal:

використовується лише в ordered section;

починається з 0;

не має пропусків;

серіалізується як unsigned decimal string.



---

8. Типи canonical values

Token	Значення

str	UTF-8 string
bool	JSON boolean
i64	Signed decimal string
u64	Unsigned decimal string
enum	Нормативний lowercase token
bytes	Base64url без padding
ip	Canonical IP address
prefix	Canonical network prefix
ifaddr	Interface address із prefix length
range	Canonical IP range
mac	Lowercase MAC address
duration-us	Signed microseconds як decimal string
symbol	auto, none, never, infinite тощо
list	Ordered typed sequence
set	Sorted unique typed values
tuple	Fixed-position typed values
opaque-text	Lossless UTF-8 value без семантичної інтерпретації
opaque-bytes	Lossless binary value
hash256	Lowercase 64-character SHA-256
uuid	Lowercase canonical UUID



---

8.1. List

["dst-port","list",[["u64","80"],["u64","443"]]]

Порядок list зберігається.


---

8.2. Set

["connection-state","set",[["enum","established"],["enum","related"]]]

Set:

1. не містить duplicates;


2. сортується за canonical encoded bytes елементів;


3. не зберігає початковий RouterOS order.




---

8.3. Tuple

["pcc","tuple",[["enum","both-addresses-and-ports"],["u64","2"],["u64","0"]]]


---

8.4. Opaque value

["layer7-protocol","opaque-text","custom-pattern"]

Opaque value:

входить до configuration hash;

доступне semantic diff як точне значення;

не може редагуватись policy editor;

не може автоматично компілюватись назад у RouterOS.



---

9. Section registry

Section registry має версію:

mfc.section-registry/1

Порядок секцій є частиною hash contract.

Order	Section ID	Ordered	Основний домен

010	system.identity	Ні	Configuration
020	system.resource	Ні	Capabilities / Observations
030	system.routerboard	Ні	Capabilities
040	system.packages	Ні	Capabilities
050	management.ip-services	Ні	Configuration / Observations
100	network.interfaces	Ні	Configuration / Observations
110	network.ipv4.addresses	Ні	Configuration / Observations
120	network.ipv6.addresses	Ні	Configuration / Observations
130	network.interface-lists	Ні	Configuration / Observations
200	firewall.ipv4.filter	Так	Configuration / Observations
210	firewall.ipv6.filter	Так	Configuration / Observations
220	firewall.ipv4.address-lists	Ні	Configuration / Observations
230	firewall.ipv6.address-lists	Ні	Configuration / Observations
240	firewall.ipv4.nat	Так	Configuration / Observations
250	firewall.ipv6.nat	Так	Configuration / Observations
260	firewall.ipv4.raw	Так	Configuration / Observations
270	firewall.ipv6.raw	Так	Configuration / Observations
280	firewall.ipv4.mangle	Так	Configuration / Observations
290	firewall.ipv6.mangle	Так	Configuration / Observations
300	routing.tables	Ні	Configuration / Observations
310	routing.rules	Так	Configuration / Observations
320	routing.ipv4.static-routes	Ні	Configuration
330	routing.ipv6.static-routes	Ні	Configuration
340	routing.ipv4.default-state	Ні	Observations
350	routing.ipv6.default-state	Ні	Observations
360	network.ipv4.settings	Ні	Configuration / Observations
370	network.ipv6.settings	Ні	Configuration / Observations
400	ha.vrrp	Ні	Configuration / Observations
500	bridge.instances	Ні	Configuration / Observations
510	bridge.ports	Ні	Configuration / Observations
520	bridge.settings	Ні	Configuration / Observations
530	bridge.vlans	Ні	Configuration / Observations
600	switch.instances	Ні	Configuration / Capabilities
610	switch.ports	Ні	Configuration / Observations
900	capabilities.device	Ні	Capabilities
910	compatibility.findings	Ні	Compatibility


Новий section додається лише через нову registry version.


---

10. Section versioning

Кожна секція має незалежну version:

firewall.ipv4.filter / version 1
ha.vrrp / version 1

Зміна section version обов’язкова при:

зміні canonical field order;

зміні типу поля;

зміні default normalization;

зміні record identity;

зміні static/dynamic classification;

зміні list/set semantics;

зміні canonical key generation.


Порівняння різних section versions дозволене лише за наявності pure deterministic upgrader.

Без upgrader:

SNAPSHOT_SCHEMA_INCOMPATIBLE

Original snapshot не модифікується. Upgraded representation є тимчасовим або окремим derived cache.


---

11. Record key encoding

record_key є ASCII string.

Composite key:

prefix|component-1|component-2|...

Компоненти:

1. перетворюються у UTF-8;


2. дозволяють без escape лише:



A-Z a-z 0-9 . _ ~ -

3. усі інші bytes кодують як %HH;


4. hex digits у percent encoding — uppercase;


5. символ | завжди кодується %7C.



Приклад:

interface name:
WAN | PRIMARY

component:
WAN%20%7C%20PRIMARY


---

12. Record identity

12.1. Рівні identity

CONTROLLER_ID
NATURAL_KEY
EXACT_FINGERPRINT
SEQUENCE_POSITION
NONE

Рівень	Значення

CONTROLLER_ID	Доведений UUID системи
NATURAL_KEY	Унікальний RouterOS logical key
EXACT_FINGERPRINT	Повністю однаковий canonical record
SEQUENCE_POSITION	Однозначне sequence зіставлення
NONE	Identity не доведена



---

12.2. Record fingerprint

record_fingerprint =
SHA256(
    canonical record fields
    без key
    без ordinal
)

Fingerprint включає:

comment;

disabled;

усі configuration fields;

opaque fields.


Fingerprint не включає:

runtime observations;

RouterOS .id;

capture timestamps;

counters.



---

13. Stable keys за секціями

Section	Record key

system.identity	singleton
system.resource	singleton
system.routerboard	singleton
system.packages	package|<name>
management.ip-services	service|<name>
network.interfaces	interface|<name>
IPv4/IPv6 addresses	address|<family>|<address>|<interface>
Interface list	iflist|<name>
Interface-list member	iflist-member|<list>|<interface>
Managed firewall rule	fw-rule|<family>|<facility>|<uuid>
Unmanaged firewall rule	fw-fingerprint|<hash>|<rank>
Static address-list entry	address-list|<family>|<list>|<address>|<rank>
Routing table	routing-table|<name>
Routing rule	routing-rule-fingerprint|<hash>|<rank>
Static route	route|<family>|<table>|<destination>|<kind>|<rank>
Default route observation	default-route|<family>|<table>|<gateway>|<rank>
IP settings	singleton
VRRP instance	vrrp|<family>|<vrid>|<interface>
Bridge	bridge|<name>
Bridge port	bridge-port|<bridge>|<interface>
Bridge settings	singleton
Bridge VLAN	bridge-vlan|<bridge>|<vlan-set>
Switch	switch|<name>
Switch port	switch-port|<switch>|<name>
Compatibility finding	finding|<section>|<code>|<context-hash>


rank використовується лише для duplicates у межах одного snapshot.


---

14. Managed firewall rule identity

Валідний marker:

fwc:rule:<uuid>:<revision-token>

Нормативний формат:

^fwc:rule:([0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}):([a-z0-9._-]{1,64})(?:\s|$)

Marker:

повинен починатися з першого символу comment;

має lowercase UUID;

має валідний revision token;

дозволяє description після пробілу.


Приклад:

fwc:rule:550e8400-e29b-41d4-a716-446655440000:r104 allow controller


---

14.1. Duplicate controller ID

Якщо один UUID присутній більше одного разу в межах:

device
+ family
+ facility

створюється:

DUPLICATE_MANAGED_RULE_ID

Усі rules із цим UUID:

не отримують CONTROLLER_ID;

обробляються як unmanaged;

не можуть бути автоматично зіставлені як MODIFIED;

блокуватимуть майбутній deployment.



---

14.2. Malformed marker

Comment, який починається з fwc: але не відповідає формату, створює:

MALFORMED_CONTROLLER_MARKER

Rule залишається unmanaged.


---

15. Загальні правила нормалізації полів

15.1. Missing field

Якщо property відсутня:

Profile rule	Результат

Required	Compatibility finding
Optional	Поле не серіалізується
Conditional і умова false	Поле не серіалізується
Default-equivalent, підтверджене manifest	Серіалізується canonical default


Default не виводиться з припущення.


---

15.2. Empty value

Порожній string і відсутнє property — різні стани.

=name=

серіалізується як:

["name","str",""]


---

15.3. Comments

Comment:

зберігається точно;

не trim-иться;

не нормалізує пробіли;

не нормалізує Unicode;

не виводиться в logs.



---

15.4. Enum

Enum token:

переводиться у lowercase лише тоді, коли manifest підтверджує case-insensitive semantics;

інакше зберігається exact;

unknown token потрапляє в Compatibility;

unknown token не перетворюється в UNKNOWN усередині Configuration.



---

15.5. IP prefixes

Network prefix маскує host bits:

192.168.1.19/24
→
192.168.1.0/24

Interface address зберігає host bits:

192.168.1.19/24
→
192.168.1.19/24


---

15.6. IPv6

IPv6:

lowercase;

використовує shortest zero compression;

не містить leading zeros у hextets;

не використовує IPv4-compatible alternate text forms без потреби;

scope identifier зберігається лише для fields, де він дозволений schema.



---

15.7. Port intervals

80,81,82,100-110,105-120

нормалізується:

80-82,100-120

Алгоритм:

1. parse;


2. перевірити 0..65535;


3. sort;


4. merge overlaps;


5. merge adjacent intervals.




---

15.8. Duration

Duration перетворюється на signed microseconds:

["interval","duration-us","1000000"]

Symbolic duration:

["timeout","symbol","none"]

Invalid duration:

не перетворюється на 0;

зберігається в Compatibility;

позначає capability як NEEDS_REVALIDATION.



---

16. Static і dynamic records

Класифікація виконується окремо для кожної секції.

Record type	Configuration	Observations

Static firewall rule	Так	Runtime flags
Dynamic firewall rule	Ні	Так
Static address-list entry	Так	Ні
Dynamic address-list entry	Ні	Digest
Static route	Так	Runtime flags за потреби
Dynamic route	Ні	Лише обмежені default-route observations
Static IP address	Так	Runtime flags
Dynamic IP address	Ні	Так
Static interface	Так	Runtime flags
Dynamic interface	Ні	Так
VRRP configuration	Так	Ні
VRRP current role	Ні	Так
Bridge VLAN config	Так	Ні
Current tagged/untagged membership	Ні	Так



---

17. Ordered firewall representation

Ordered facilities:

filter
NAT
RAW
Mangle
routing rules

17.1. Static sequence

Configuration record містить:

static_ordinal

static_ordinal визначається лише серед static rules.

Dynamic rules не зсувають static ordinals.


---

17.2. Effective sequence

Observations містять:

effective_ordinal

для:

dynamic rules;

static rules, якщо потрібно відтворити їх effective position;

effective sequence digest.


effective_sequence_digest =
SHA256(
    ordered sequence:
        record class
        controller UUID або record fingerprint
)

Це дозволяє побачити появу dynamic rules між static rules без створення configuration drift.


---

17.3. Static rule configuration fields

Record має:

key
ordinal
fields

Приклад:

{
  "key": "fw-rule|ipv4|filter|550e8400-e29b-41d4-a716-446655440000",
  "ordinal": "0",
  "fields": [
    ["chain", "enum", "input"],
    ["action", "enum", "accept"],
    ["protocol", "enum", "tcp"],
    ["dst-port", "list", [["u64", "8729"]]],
    ["disabled", "bool", false],
    ["comment", "str", "fwc:rule:550e8400-e29b-41d4-a716-446655440000:r1"]
  ]
}


---

17.4. Rule field order

Field order визначається section schema:

chain
action
match fields
action-specific fields
log
log-prefix
disabled
comment

RouterOS reply order не впливає на canonical field order.


---

18. Unmanaged firewall rules

Unmanaged rule не має доведеної mutable identity.

18.1. Exact unchanged rule

Якщо exact fingerprint зустрічається рівно один раз у base і target:

EXACT_FINGERPRINT

Такий rule може бути класифікований як:

MOVED

за зміни ordinal.


---

18.2. Changed unmanaged rule

Зміна matcher, action, comment або disabled state змінює fingerprint.

Controller повертає:

REMOVED old rule
ADDED new rule

MODIFIED не генерується.


---

18.3. Duplicate unmanaged rules

Якщо однаковий fingerprint зустрічається декілька разів:

records групуються за fingerprint;

однозначне sequence pairing дозволене лише між незмінними anchors;

інакше matching confidence дорівнює CONSERVATIVE;

неоднозначний move не генерується;

unmatched records повертаються як add/remove.



---

19. Address lists

19.1. Static entries

Canonical configuration:

family
list
address
disabled
comment

Key:

address-list|<family>|<list>|<address>|<rank>

DNS name:

не resolve-иться Controller;

зберігається як lowercase лише за підтверджених DNS semantics;

інакше зберігається exact;

не замінюється фактичними IP.



---

19.2. Dynamic entries

Повні dynamic addresses не зберігаються у canonical snapshot.

Для кожного:

family
list name

створюється:

DynamicAddressListSummary {
    entry_count
    entry_digest
}

Entry digest

Для кожного entry:

entry_bytes =
canonical address
+ disabled state

Не включаються:

remaining timeout;

creation time;

last seen time;

counters;

comment.


entry_hash = SHA256(entry_bytes)

Для list:

1. Відсортувати 32-byte entry hashes.
2. Зберегти кількість entries.
3. SHA256(concatenated sorted hashes).

Canonical observation:

{
  "key": "dynamic-address-list|ipv4|temporary-block",
  "fields": [
    ["entry-count", "u64", "145"],
    ["entry-digest", "hash256", "…"]
  ]
}


---

19.3. Dynamic list diff

Diff показує:

entry count before
entry count after
digest before
digest after

Конкретні dynamic IP addresses не відновлюються і не показуються.


---

20. Interfaces та interface lists

20.1. Interface identity

interface|<name>

Перейменування interface повертається як:

REMOVED old interface
ADDED new interface

Controller не намагається зіставити interface за MAC, оскільки:

MAC може змінюватись;

virtual interface може мати inherited MAC;

однаковий MAC не гарантує ту саму logical interface.



---

20.2. Interface-list configuration

Canonical record містить:

name
include
exclude
explicit members
resolved members
comment

include, exclude, explicit members і resolved members є sets.


---

20.3. Resolution

Нормативний порядок:

1. Recursively resolve included lists.
2. Remove recursively resolved excluded lists.
3. Add explicit members.

Explicit member має вищий пріоритет за exclude.

Cycle:

A includes B
B includes A

створює:

INTERFACE_LIST_CYCLE

resolved members у такому разі не підміняються порожнім set.


---

21. Routing

21.1. Routing tables

Identity:

routing-table|<name>

Зміна fib або disabled:

MODIFIED


---

21.2. Routing rules

Routing rules є ordered і не мають надійної mutable identity в M1.

Застосовуються ті самі правила, що для unmanaged firewall rules:

exact unchanged fingerprint може бути moved;

змінений rule повертається remove+add;

fuzzy matching за схожими address fields заборонений.



---

21.3. Static routes

Natural group key:

family
routing table
destination
route kind

Key:

route|<family>|<table>|<destination>|<kind>|<rank>

Однозначний route

Якщо в group один route у base й один у target:

gateway change → MODIFIED;

distance change → MODIFIED;

check-gateway change → MODIFIED;

disabled change → MODIFIED.


Duplicate routes

За наявності декількох routes з однаковим group key:

1. exact fingerprints зіставляються;


2. однозначні залишки можуть бути matched лише за унікальним gateway set;


3. інші залишки повертаються add/remove.




---

21.4. Default route observations

Активні default routes:

не входять у configuration hash;

мають окрему observation section;

показують active/inactive state;

використовуються multi-WAN projection.


Failover primary route change:

OBSERVATION / STATE_CHANGED

Static distance change:

CONFIGURATION / MODIFIED


---

22. VRRP

22.1. Local VRRP instance identity

family
VRID
interface

Key:

vrrp|ipv4|10|lan-vrrp

Family визначається configuration profile, а не припущенням із VRID.


---

22.2. Configuration fields

Canonical configuration містить:

family
interface
VRID
version
priority
advertisement interval
preemption
group authority
checksum mode
connection tracking synchronization
disabled
comment
virtual address set

Password і transition scripts не включаються.


---

22.3. Observation fields

role
running
invalid
failure
group role
observed timestamp metadata

Role:

MASTER
BACKUP
FAILURE
INITIALIZING
INACTIVE
INVALID
INCONSISTENT


---

22.4. Role change

MASTER → BACKUP

повертається:

domain: OBSERVATION
changes: [STATE_CHANGED]

Configuration hash не змінюється.


---

22.5. Virtual addresses

Virtual address set:

формується з IPv4/IPv6 address configuration, прив’язаної до VRRP interface;

сортується;

не включає dynamic address;

є частиною VRRP configuration.



---

23. Bridge і switch context

23.1. Bridge identity

bridge|<name>

23.2. Bridge port identity

bridge-port|<bridge>|<interface>

23.3. Bridge VLAN identity

bridge-vlan|<bridge>|<normalized-vlan-set>

tagged та untagged є sets.


---

23.4. Hardware offload

Hardware-offload:

входить в observations;

не вважається доказом проходження traffic через IP firewall;

використовується topology projection;

не змінюється Controller у M1.



---

23.5. Unknown switch chip

Unknown switch chip створює:

SWITCH_HARDWARE_UNVALIDATED

Він:

залишається доступним для read-only inventory;

не отримує transit firewall capability;

не отримує майбутній write profile автоматично.



---

24. Capabilities document

capabilities.device має singleton record.

{
  "key": "singleton",
  "fields": [
    ["routeros-version", "str", "…"],
    ["architecture", "enum", "arm64"],
    ["board", "str", "…"],
    ["support-state", "enum", "supported"],
    ["manifest-hash", "hash256", "…"],
    ["available-sections", "set", []]
  ]
}

Package records:

package|<name>

Package order не має значення.


---

25. Compatibility document

Compatibility finding:

code
severity
source section
record context
property name
value classification
sanitized value або value hash
message key

Приклад:

{
  "key": "finding|firewall.ipv4.filter|unknown-property|…",
  "fields": [
    ["code", "enum", "unknown-property"],
    ["severity", "enum", "error"],
    ["source-section", "str", "firewall.ipv4.filter"],
    ["property", "str", "new-routeros-field"],
    ["value-disposition", "enum", "stored-sanitized"],
    ["value", "opaque-text", "…"]
  ]
}


---

25.1. Potentially sensitive unknown value

Коли unknown field:

має sensitive name;

містить secret-like pattern;

повернутий із sensitive context;


canonical Compatibility зберігає лише:

value length
SHA-256 value
redacted = true

Plaintext не зберігається.


---

25.2. Compatibility hash

Будь-яка зміна:

unknown properties
parse failures
section support status
manifest mismatch

змінює compatibility hash.

Configuration hash при цьому може залишитися незмінним.


---

26. Canonical document

Кожний domain має manifest:

{
  "schema": "mfc.canonical-document/1",
  "domain": "configuration",
  "registryVersion": "1",
  "sections": [
    ["system.identity", "<section-sha256>"],
    ["firewall.ipv4.filter", "<section-sha256>"]
  ]
}

Canonical bytes:

{"schema":"mfc.canonical-document/1","domain":"configuration","registryVersion":"1","sections":[["system.identity","…"],["firewall.ipv4.filter","…"]]}

Sections:

сортуються за Section Registry order;

не сортуються алфавітно;

не дублюються;

містять lowercase SHA-256;

включають empty supported sections;

не включають unsupported/not-applicable sections.


Unsupported status зберігається в Compatibility і Capabilities.


---

27. Hash contracts

Використовується:

SHA-256

27.1. Section hash

section_hash =
SHA256(exact canonical section bytes)

Section canonical bytes уже містять:

schema
domain
section ID
section version
ordered flag
records

Додатковий domain separator не потрібний.


---

27.2. Document hash

document_hash =
SHA256(exact canonical document manifest bytes)

Отримуються:

configuration_hash
observation_hash
capability_hash
compatibility_hash


---

27.3. Snapshot manifest

{
  "schema": "mfc.snapshot-manifest/1",
  "registryVersion": "1",
  "configuration": "<sha256>",
  "observations": "<sha256>",
  "capabilities": "<sha256>",
  "compatibility": "<sha256>"
}

Порядок полів нормативний.

snapshot_hash =
SHA256(exact snapshot manifest bytes)


---

27.4. Hash exclusions

Не впливають на hashes:

capture ID
device ID
operation ID
user ID
timestamps
database IDs
compression
database row order
API tags
RouterOS .id
network packet fragmentation


---

28. Content-addressed storage

28.1. snapshot_payloads

CREATE TABLE snapshot_payloads (
    payload_hash        bytea PRIMARY KEY,
    payload_format      smallint NOT NULL,
    schema_id           text NOT NULL,
    compression         smallint NOT NULL,
    uncompressed_size   bigint NOT NULL,
    compressed_payload  bytea NOT NULL,
    created_at          timestamptz NOT NULL,

    CONSTRAINT ck_snapshot_payload_hash
        CHECK (octet_length(payload_hash) = 32),

    CONSTRAINT ck_snapshot_payload_size
        CHECK (
            uncompressed_size > 0
            AND uncompressed_size <= 67108864
        )
);

payload_format:

RAW_SECTION_JSON
CANONICAL_SECTION_JSON
CANONICAL_DOCUMENT_MANIFEST
SNAPSHOT_MANIFEST


---

28.2. snapshot_capture_sections

CREATE TABLE snapshot_capture_sections (
    capture_id                  uuid NOT NULL
                                    REFERENCES snapshot_captures(id),
    section_id                  text NOT NULL,
    section_version             integer NOT NULL,
    status                      smallint NOT NULL,
    ordered                     boolean NOT NULL,

    configuration_record_count  integer NOT NULL DEFAULT 0,
    observation_record_count    integer NOT NULL DEFAULT 0,
    capability_record_count     integer NOT NULL DEFAULT 0,
    compatibility_record_count  integer NOT NULL DEFAULT 0,

    raw_hash                    bytea NULL,
    configuration_hash          bytea NULL,
    observation_hash            bytea NULL,
    capability_hash             bytea NULL,
    compatibility_hash          bytea NULL,

    PRIMARY KEY (capture_id, section_id)
);

Кожний hash є foreign key до snapshot_payloads.payload_hash.


---

28.3. snapshot_captures

Completed capture містить:

raw manifest hash
configuration hash
observation hash
capability hash
compatibility hash
snapshot hash

Дублюючі *_payload_hash поля видаляються.


---

28.4. Compression

M1 використовує:

Brotli

Compression:

виконується після hash;

не входить у hash;

має bounded output size;

не змінює canonical bytes;

перевіряється після decompression.



---

28.5. Insert algorithm

1. Побудувати canonical bytes.
2. Обчислити SHA-256.
3. Стиснути bytes.
4. INSERT payload ON CONFLICT DO NOTHING.
5. При conflict перевірити schema ID і uncompressed size.
6. Створити section mappings.
7. Створити document manifests.
8. Створити snapshot manifest.
9. Оновити SnapshotCapture до COMPLETED.
10. Commit PostgreSQL transaction.

До commit snapshot недоступний як completed.


---

28.6. Integrity verification

Після читання payload:

1. decompress;


2. перевірити declared size;


3. повторно обчислити SHA-256;


4. порівняти з storage key.



При розбіжності:

SNAPSHOT_PAYLOAD_INTEGRITY_FAILED

Payload не повертається Desktop.


---

28.7. Garbage collection

Automatic payload deletion у M1 не реалізується.

Причини:

completed snapshots immutable;

audit history не видаляється;

premature GC може зруйнувати відтворюваність.


Retention і GC визначатимуться окремою operational policy після появи реальних обсягів.


---

29. Semantic diff model

Semantic diff порівнює два completed snapshots одного Device.

29.1. Preconditions

Обов’язково:

same Device ID
completed status
valid payload hashes
compatible canonical schemas
authorization for both snapshots

Інакше:

SNAPSHOTS_FROM_DIFFERENT_DEVICES
SNAPSHOT_NOT_COMPLETED
SNAPSHOT_SCHEMA_INCOMPATIBLE
SNAPSHOT_PAYLOAD_INTEGRITY_FAILED


---

29.2. Diff domains

CONFIGURATION
OBSERVATION
CAPABILITY
COMPATIBILITY


---

29.3. Change types

ADDED
REMOVED
MODIFIED
MOVED
STATE_CHANGED
SECTION_STATUS_CHANGED

Один entry може мати декілька changes:

[MODIFIED, MOVED]

Нормативний порядок changes:

ADDED
REMOVED
MODIFIED
MOVED
STATE_CHANGED
SECTION_STATUS_CHANGED


---

29.4. Match confidence

CONTROLLER_ID
NATURAL_KEY
EXACT_FINGERPRINT
EXACT_SEQUENCE
CONSERVATIVE

MODIFIED дозволений лише для:

CONTROLLER_ID
NATURAL_KEY

MOVED дозволений для:

CONTROLLER_ID
NATURAL_KEY
EXACT_FINGERPRINT
EXACT_SEQUENCE


---

30. Updated gRPC diff contract

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
  MATCH_CONFIDENCE_CONTROLLER_ID = 1;
  MATCH_CONFIDENCE_NATURAL_KEY = 2;
  MATCH_CONFIDENCE_EXACT_FINGERPRINT = 3;
  MATCH_CONFIDENCE_EXACT_SEQUENCE = 4;
  MATCH_CONFIDENCE_CONSERVATIVE = 5;
}

message FieldDiff {
  string field_name = 1;
  optional CanonicalValue before = 2;
  optional CanonicalValue after = 3;
  repeated CanonicalValue added_values = 4;
  repeated CanonicalValue removed_values = 5;
}

message DiffEntry {
  string section_id = 1;
  DiffDomain domain = 2;
  repeated DiffChange changes = 3;
  MatchConfidence confidence = 4;
  string record_key = 5;

  optional uint32 before_ordinal = 6;
  optional uint32 after_ordinal = 7;

  optional SnapshotRecord before = 8;
  optional SnapshotRecord after = 9;

  repeated FieldDiff field_diffs = 10;
}


---

31. Section comparison algorithm

IF base document hash == target document hash:
    return empty domain diff

FOR each section by registry order:

    IF base section hash == target section hash:
        continue

    IF section exists only in target:
        emit ADDED records

    IF section exists only in base:
        emit REMOVED records

    IF section versions differ:
        upgrade або return schema error

    match records
    compare matched records
    emit unmatched records


---

32. Record matching phases

Phase 1 — Controller IDs

Зіставляються unique valid controller UUIDs.

O(n)

Duplicate UUIDs виключаються з цієї фази.


---

Phase 2 — Natural keys

Зіставляються unique natural keys:

interface name
service name
routing table name
bridge name
VRRP local key

При duplicate natural key:

records не matched цією фазою;

створюється compatibility finding;

застосовується exact fingerprint matching.



---

Phase 3 — Exact fingerprints

Унікальний fingerprint у base і target:

EXACT_FINGERPRINT

Дозволяє визначити unchanged record або move.


---

Phase 4 — Ordered sequence matching

Застосовується лише ordered sections.

Unmatched exact fingerprints аналізуються з урахуванням sequence anchors.


---

Phase 5 — Conservative output

Усе, що залишилось unmatched:

base → REMOVED
target → ADDED

Жодного fuzzy matcher.


---

33. Ordered diff algorithm

Для firewall і routing rules використовується:

unique anchor discovery
→ sequence partitioning
→ bounded Myers diff
→ conservative fallback

33.1. Anchor discovery

Anchor:

має unique controller ID або natural key;

або exact fingerprint зустрічається рівно один раз у кожній sequence.


Anchors сортуються за before ordinal і after ordinal.

Crossing anchors не використовуються одночасно.


---

33.2. Bounded Myers

Limits M1:

maximum records per ordered section: 20 000
maximum edit distance:               4 096
maximum frontier operations:         8 000 000
maximum temporary memory:            64 MiB

При перевищенні:

DIFF_COMPLEXITY_LIMIT


---

33.3. Conservative fallback

Fallback:

1. зберігає всі доведені matches;


2. не виконує fuzzy matching;


3. unmatched base records повертає REMOVED;


4. unmatched target records повертає ADDED;


5. додає warning:



DIFF_DEGRADED_TO_CONSERVATIVE

Diff залишається повним, але може не визначити деякі moves.


---

34. Field-level diff

Для matched record поля порівнюються за section schema order.

34.1. Scalar field

before != after
→ FieldDiff(before, after)


---

34.2. Set field

Повертаються:

added_values
removed_values

Приклад:

interface-list resolved members:
    added: ether5
    removed: ether4


---

34.3. Ordered list

Для ordered list повертаються повні:

before
after

Окремий list edit script у M1 не створюється.


---

34.4. Opaque field

Opaque field порівнюється byte-for-byte.

У GUI показується:

value changed

або sanitized value, якщо користувач має відповідне право.


---

34.5. Observation change

Для observation domain використовується:

STATE_CHANGED

а не MODIFIED.


---

35. Diff ordering

Diff entries сортуються:

1. Section Registry order
2. Domain order
3. Before ordinal, якщо є
4. After ordinal, якщо є
5. Record key
6. Change order

Domain order:

CONFIGURATION
OBSERVATION
CAPABILITY
COMPATIBILITY

Один input завжди формує однаковий ordered output.


---

36. Node-level topology projection

Node projection будується з:

declared Node metadata
+ Device membership
+ completed Device snapshots

Він не зберігається як authoritative configuration.

TopologyProjection =
PureFunction(
    Node declaration,
    Device snapshots
)


---

36.1. Consistent node capture

Переважний input:

всі Device captures з одного CaptureOperation

Заборонено непомітно змішувати:

новий snapshot Router A
+ старий snapshot Router B


---

36.2. Latest-known projection

Latest-known mode дозволений лише для перегляду.

Він повинен показувати:

INCONSISTENT_CAPTURE_SET

та timestamps кожного Device.

Latest-known projection не використовується для майбутнього deployment plan.


---

36.3. Projection structure

TopologyProjection {
    node_id
    node_row_version
    declared_kind
    declared_uplink_mode

    devices[]
    vrrp_groups[]
    multiwan_evidence[]
    switch_evidence?
    findings[]

    configuration_projection_hash
    observation_projection_hash
}


---

36.4. Projection hash

Configuration projection hash включає:

Node declared configuration
Device membership
Device configuration hashes
Device capability hashes

Observation projection hash включає:

Device observation hashes
VRRP role vectors
active route evidence
hardware-offload state
capture skew findings


---

37. VRRP node comparison

37.1. Cross-device VRRP group identity

Міжпристроєвий key:

family
VRID
sorted virtual address set

Parent interface name не входить до cross-device key, оскільки фізичні routers можуть мати різні interface names.


---

37.2. Configuration consistency

Порівнюються:

family
VRID
virtual addresses
VRRP version
advertisement interval
preemption
checksum mode
group authority
RouterOS compatibility

Результат:

CONSISTENT
CONFIGURATION_MISMATCH
MEMBER_MISSING
INCONCLUSIVE


---

37.3. Role vector

Приклад:

VRID 10 IPv4:
    R1 → MASTER
    R2 → BACKUP

VRID 20 IPv4:
    R1 → BACKUP
    R2 → MASTER

Node класифікується як:

SPLIT_MASTER

а не global active/passive.


---

37.4. Capture skew

VRRP role observations вважаються співставними, коли:

observation skew <=
max(
    5 seconds,
    3 × maximum advertisement interval
)

Верхня межа:

30 seconds

За більшого skew:

VRRP_ROLE_VECTOR_INCONCLUSIVE

Controller не оголошує split-brain лише за несинхронними snapshots.


---

37.5. Role consistency

Для одного VRRP group:

Стан	Результат

1 master, ≥1 backup	HEALTHY
0 master, backups present	NO_MASTER_OBSERVED
>1 master	MULTIPLE_MASTERS_OBSERVED
Failure member	MEMBER_FAILURE
Observation skew exceeded	INCONCLUSIVE
Missing member capture	INCOMPLETE


Це лише observation. Controller у M1 не змінює VRRP.


---

38. Multi-WAN evidence projection

Controller не створює власну конфігурацію WAN і не перемикає канали.

Він формує evidence.

38.1. Configuration evidence

default static routes
route distances
routing tables
routing rules
check-gateway
recursive gateway relationships
Mangle routing marks
connection marks
PCC
NAT out-interface
NAT out-interface-list
interface-list membership
rp-filter


---

38.2. Observation evidence

active default routes
inactive default routes
immediate gateways
gateway status
active routing tables
interface running state


---

38.3. Projection status

VERIFIED
PARTIALLY_VERIFIED
CONTRADICTED
INSUFFICIENT_EVIDENCE

FAILOVER

Evidence:

декілька default routes;

різні distances або explicit recursive failover;

не більше одного active primary path за звичайного стану.


BALANCED

Evidence:

декілька routing tables або ECMP;

routing marks;

PCC або equivalent traffic distribution;

декілька active paths.


MIXED

Evidence одночасно вказує на:

балансування частини traffic;

backup/failover paths.



---

38.4. Multi-WAN diff

Configuration diff показує:

new/removed routing table
routing rule change
PCC change
route distance change
NAT uplink binding change
rp-filter change

Observation diff показує:

primary route became inactive
backup became active
gateway status changed
WAN interface state changed


---

39. Switch projection

Switch evidence:

board/model
switch-chip profile
bridge configuration
bridge ports
VLAN filtering
hardware offload
management IP
input firewall
bridge use-ip-firewall settings

Projection не робить висновок:

transit traffic protected by IP firewall

якщо це не доведено конкретним packet-path і hardware profile.


---

40. Server-side pagination

40.1. Section pagination

Ordered section:

ordinal ascending

Unordered section:

record_key ascending bytewise

Page size:

minimum: 1
default: 200
maximum: 500

Server додатково обмежує response:

до 2 MiB

Якщо 500 records перевищують limit, фактична кількість зменшується.


---

40.2. Page token

Token містить:

version
actor ID
capture ID
section ID
domain
section hash
next record index
page size
normalized filter hash
expiration

Token:

HMAC-SHA-256 signed;

opaque для Desktop;

не містить credentials;

не містить IP addresses або rule contents;

має строк дії 15 хвилин;

повторно перевіряє authorization.



---

40.3. Invalid token

PAGE_TOKEN_INVALID
PAGE_TOKEN_EXPIRED
PAGE_TOKEN_SCOPE_MISMATCH
PAGE_TOKEN_PAYLOAD_MISMATCH

Offset pagination не використовується.


---

40.4. Decode cache

Controller може використовувати bounded LRU cache:

maximum total uncompressed size: 512 MiB
maximum one item:                 64 MiB

Вимоги:

single-flight decompression;

immutable cached data;

cache miss не впливає на semantics;

cache не містить raw secret data;

eviction не впливає на snapshot availability.



---

41. Diff pagination

Diff cursor містить:

base snapshot hash
target snapshot hash
section ID
domain
normalized filters
next diff index
actor ID
expiration

Зміна filters вимагає нового diff request.

Controller не зберігає повний diff у Desktop cache як authoritative result.


---

42. Security requirements

1. Canonical payloads вважаються чутливими network configuration data.


2. Production PostgreSQL storage повинен використовувати encryption at rest.


3. Backups snapshots повинні бути зашифровані.


4. Raw snapshot доступний лише Administrator/Auditor із окремим permission.


5. Viewer отримує тільки canonical sections.


6. Comments не логуються.


7. Address-list entries не логуються.


8. Firewall source/destination values не логуються.


9. Page tokens не містять configuration values.


10. Payload hash verification виконується до десеріалізації.


11. Decompression має size limit.


12. JSON parser має depth limit.


13. Duplicate JSON keys відхиляються.


14. Canonical parser не дозволяє floating point.


15. Unknown binary values не конвертуються в text.


16. Compatibility redaction виконується до persistence.


17. Snapshot export створює audit event.


18. Export не містить RouterOS credentials.


19. Completed payload не оновлюється.


20. Application DB role не має UPDATE або DELETE на completed snapshot mappings.




---

43. Error model

Canonicalization

CANONICAL_ENCODING_FAILED
CANONICAL_DUPLICATE_RECORD_KEY
CANONICAL_DUPLICATE_FIELD
CANONICAL_FIELD_ORDER_INVALID
CANONICAL_VALUE_INVALID
CANONICAL_SCHEMA_UNKNOWN
CANONICAL_SECTION_VERSION_UNKNOWN
CANONICAL_RECORD_LIMIT_EXCEEDED
CANONICAL_SECTION_SIZE_EXCEEDED

Identity

DUPLICATE_MANAGED_RULE_ID
MALFORMED_CONTROLLER_MARKER
DUPLICATE_NATURAL_KEY
UNMANAGED_RULE_IDENTITY_AMBIGUOUS

Persistence

SNAPSHOT_PAYLOAD_INTEGRITY_FAILED
SNAPSHOT_PAYLOAD_MISSING
SNAPSHOT_PERSISTENCE_FAILED
SNAPSHOT_IMMUTABILITY_VIOLATION
SNAPSHOT_MANIFEST_INVALID

Diff

SNAPSHOTS_FROM_DIFFERENT_DEVICES
SNAPSHOT_SCHEMA_INCOMPATIBLE
SNAPSHOT_SECTION_UNAVAILABLE
DIFF_COMPLEXITY_LIMIT
DIFF_DEGRADED_TO_CONSERVATIVE

Pagination

PAGE_TOKEN_INVALID
PAGE_TOKEN_EXPIRED
PAGE_TOKEN_SCOPE_MISMATCH
PAGE_TOKEN_PAYLOAD_MISMATCH
PAGE_SIZE_INVALID

Topology

INCONSISTENT_CAPTURE_SET
VRRP_CONFIGURATION_MISMATCH
VRRP_MEMBER_MISSING
VRRP_ROLE_VECTOR_INCONCLUSIVE
MULTIWAN_EVIDENCE_INSUFFICIENT
MULTIWAN_MODE_CONTRADICTED
SWITCH_HARDWARE_UNVALIDATED


---

44. Нормативні test vectors

44.1. System identity section

Canonical bytes:

{"schema":"mfc.canonical-section/1","domain":"configuration","section":"system.identity","version":"1","ordered":false,"records":[{"key":"singleton","fields":[["name","str","BRANCH-001"]]}]}

SHA-256:

e9337366e349fc9fcbc1463a75cddfb00dfb28ade1cd2db299b7a7bfd5f22918


---

44.2. Empty IPv4 filter section

Canonical bytes:

{"schema":"mfc.canonical-section/1","domain":"configuration","section":"firewall.ipv4.filter","version":"1","ordered":true,"records":[]}

SHA-256:

f4d189ce0b2cc3e67e840b25ec7db2e4c520661c4be9d2b6b2fc53fd73d6da65


---

44.3. Managed firewall rule

Canonical bytes:

{"schema":"mfc.canonical-section/1","domain":"configuration","section":"firewall.ipv4.filter","version":"1","ordered":true,"records":[{"key":"fw-rule|ipv4|filter|550e8400-e29b-41d4-a716-446655440000","ordinal":"0","fields":[["chain","enum","input"],["action","enum","accept"],["protocol","enum","tcp"],["dst-port","list",[["u64","8729"]]],["disabled","bool",false],["comment","str","fwc:rule:550e8400-e29b-41d4-a716-446655440000:r1"]]}]}

SHA-256:

e0c0d6a2bfcb9d57a55b64b3753c894de8bf64b00b552d1e84e89b6bd25226f6


---

44.4. Rule action changed

accept замінено на drop.

SHA-256:

6dafa32173068e80391f36ed7f472019054e4ddfa0648c6f7eae4a92a6adb52e

Expected diff:

domain:       CONFIGURATION
changes:      MODIFIED
confidence:   CONTROLLER_ID
field:
    action
    before: accept
    after:  drop


---

44.5. Rule moved

ordinal змінено з 0 на 1.

SHA-256:

857a8f32383d54de504486191eaef60420042909eb4050e3e9e8ec2d00a61478

Expected diff:

domain:       CONFIGURATION
changes:      MOVED
confidence:   CONTROLLER_ID
before:       0
after:        1


---

44.6. Configuration document

Canonical bytes:

{"schema":"mfc.canonical-document/1","domain":"configuration","registryVersion":"1","sections":[["system.identity","e9337366e349fc9fcbc1463a75cddfb00dfb28ade1cd2db299b7a7bfd5f22918"],["firewall.ipv4.filter","f4d189ce0b2cc3e67e840b25ec7db2e4c520661c4be9d2b6b2fc53fd73d6da65"]]}

SHA-256:

32bf27668f69d4dbe74399b793da3159edf13abac941902b234178e7a0bcf85f


---

44.7. Empty documents

Observations:
01829ed5f3c2a11e2b65a93081a9bd135415c5f20fa785d95290116a5ab5af71

Capabilities:
1c3995c20f57aa02b2238ba547c11b9a06e76355a31ba44be0a0db50d2a07b71

Compatibility:
24e62f37195633e4a9f71482b46e597b99becb5b5315c5733f3717b23071de1b


---

44.8. Snapshot manifest

Canonical bytes:

{"schema":"mfc.snapshot-manifest/1","registryVersion":"1","configuration":"32bf27668f69d4dbe74399b793da3159edf13abac941902b234178e7a0bcf85f","observations":"01829ed5f3c2a11e2b65a93081a9bd135415c5f20fa785d95290116a5ab5af71","capabilities":"1c3995c20f57aa02b2238ba547c11b9a06e76355a31ba44be0a0db50d2a07b71","compatibility":"24e62f37195633e4a9f71482b46e597b99becb5b5315c5733f3717b23071de1b"}

SHA-256:

89906a5b56539b0bbd5f880c1ef739c325d59eba2e924b6b09ee3d33c77d551d


---

45. Canonicalization test matrix

Обов’язкові tests:

same attributes in different RouterOS order
same unordered records in different API order
firewall order changed
missing optional property
missing required property
empty string vs absent property
IPv4 prefix host-bit masking
IPv4 interface-address preservation
IPv6 normalization
MAC normalization
duration normalization
symbolic duration
port interval merge
set sorting
list order preservation
duplicate set element
invalid integer
integer overflow
invalid UTF-8
opaque binary value
duplicate record key
duplicate managed UUID
malformed fwc marker
dynamic/static split
dynamic address-list digest
interface-list include/exclude
interface-list cycle
VRRP role change
active route change
static route change
unknown property
unknown enum token

Property invariant:

Canonicalize(Canonicalize(x)) == Canonicalize(x)


---

46. Hash test matrix

same canonical bytes
    → same hash

different whitespace in source RouterOS value
    → same або different залежно від field semantics

different JSON writer whitespace
    → неможливо, canonical writer не створює whitespace

attribute order change
    → same hash

unordered record order change
    → same hash

ordered firewall record order change
    → different configuration hash

interface running change
    → same configuration hash
    → different observation hash

VRRP role change
    → same configuration hash
    → different observation hash

RouterOS version change
    → different capability hash

unknown property change
    → different compatibility hash

compression level change
    → same payload hash

database row order change
    → same document hash


---

47. Semantic diff test matrix

identical snapshots
managed rule modified
managed rule moved
managed rule modified and moved
duplicate managed UUID
unmanaged exact rule moved
unmanaged rule changed
duplicate unmanaged exact rules
rule inserted before duplicates
static address-list entry added
static address-list entry disabled
dynamic address-list digest changed
interface renamed
interface running state changed
interface-list member added
interface-list cycle appeared
routing table changed
routing rule reordered
static route gateway changed
active default route changed
VRRP priority changed
VRRP role changed
VRRP virtual address changed
bridge VLAN set changed
hardware offload state changed
unknown property appeared
section became unsupported
section version mismatch
diff complexity fallback


---

48. Persistence tests

section payload deduplication
document manifest deduplication
snapshot manifest deduplication
compression/decompression
hash verification after decompression
corrupted payload detection
missing section payload
atomic completed capture
transaction rollback
completed capture immutability
duplicate capture content
same payload referenced by multiple captures
controller restart
page reading after restart
database backup/restore


---

49. Performance requirements

На типовому Controller host:

Операція	Межа

Canonicalize 1 000 firewall rules	до 250 ms
Diff 1 000 firewall rules	до 500 ms
Canonicalize 20 000 firewall rules	до 3 s
Diff 20 000 firewall rules	до 5 s або conservative fallback
Read cached section page	до 500 ms
Read uncached 64 MiB section page	до 2 s
Compare equal snapshots	до 100 ms
Hash 64 MiB payload	bounded streaming


Вимоги:

відсутні unbounded allocations;

canonical writer працює streaming;

hash обчислюється streaming;

payload compression працює streaming;

duplicate payload не зберігається повторно;

diff не копіює весь snapshot більше одного разу;

decode cache має жорстку межу.



---

50. Acceptance criteria

Специфікація вважається реалізованою лише коли:

1. Існує єдиний canonical writer.


2. Generic JSON serializer не використовується для hash bytes.


3. Canonical JSON не містить numbers або null.


4. Object key order детермінований.


5. Field order визначений section schema.


6. Set і list мають різні semantics.


7. Invalid UTF-8 не втрачається.


8. Unknown property не ігнорується.


9. Missing required property створює finding.


10. RouterOS .id не входить до canonical payload.


11. Counters не входять до snapshots.


12. Firewall static order зберігається.


13. Dynamic firewall rules не входять до configuration.


14. Effective firewall sequence digest обчислюється.


15. Dynamic address-list plaintext не зберігається.


16. Dynamic address-list change змінює observation hash.


17. Active default route change не змінює configuration hash.


18. Static route change змінює configuration hash.


19. VRRP role change не змінює configuration hash.


20. VRRP role change змінює observation hash.


21. Split-master VRRP зберігається як role vector.


22. Unmanaged changed rule повертається remove+add.


23. Managed UUID дозволяє MODIFIED.


24. Duplicate managed UUID не використовується як identity.


25. Rule може одночасно бути modified і moved.


26. Semantic diff не виконується Desktop.


27. Diff має bounded complexity.


28. Conservative fallback не приховує records.


29. Section payload hash дорівнює SHA-256 canonical bytes.


30. Compression не змінює hash.


31. Snapshot зберігається по секціях.


32. Completed snapshot immutable.


33. Corrupted payload не повертається користувачу.


34. Pagination не використовує offset.


35. Page token перевіряє snapshot hash.


36. Raw snapshots мають окреме permission.


37. Node projection не змішує Device snapshots мовчки.


38. Multi-WAN classification спирається на evidence.


39. Switch projection не прирівнює hardware switching до IP firewall.


40. Усі нормативні test vectors дають зафіксовані SHA-256.


41. CHR standalone snapshots детерміновані.


42. CHR multi-WAN snapshots детерміновані.


43. CHR VRRP snapshots детерміновані.


44. Physical CRS snapshot проходить canonicalization.


45. Build і tests не змінюють Git working tree.




---

51. Уточнення Initial Issue Set

Issue	Нормативна зміна

M1-21	Реалізує MFC-CJ1, canonical writer і typed values
M1-22	Реалізує Section Registry, section versions і canonical sections
M1-23	Використовує section-level content-addressed storage
M1-23	Додає snapshot_capture_sections
M1-23	Прибирає дублюючі *_payload_hash поля
M1-24	Використовує phased conservative matching
M1-24	Підтримує декілька change flags на один record
M1-24	Не генерує MODIFIED для зміненого unmanaged rule
M1-26	Використовує cursor pagination
M1-26	Повертає typed canonical values, а не JSON numbers
M1-29	GUI не об’єднує remove+add у modified
M1-31	Перевіряє configuration/observation separation multi-WAN
M1-32	Перевіряє VRRP role vector і capture skew
M1-33	Перевіряє corruption, diff bounds і payload integrity



---

52. Результат етапу

Після реалізації цієї специфікації Controller матиме достовірну основу:

RouterOS API-SSL
        ↓
typed read adapter
        ↓
stable double-read
        ↓
MFC-CJ1 canonical sections
        ↓
configuration / observation separation
        ↓
content-addressed immutable storage
        ↓
deterministic semantic diff
        ↓
topology-aware Node projection

На цьому read-only технічний дизайн M1 завершений.

Наступний нормативний документ переводить проєкт безпосередньо до його основної функції:

MikroTik Firewall Controller
Policy Model, Composition and Static Analysis Specification v0.1

Він має визначити:

company baseline
site overlay
node overlay
temporary exceptions
address objects
service objects
logical zones
rule sections і порядок
policy revision lifecycle
deterministic composition
typed firewall matchers/actions
policy tests
shadowing analysis
unreachable rule analysis
management-path safety validation
VRRP control-plane validation
multi-WAN dependency validation
FastTrack constraints
policy semantic diff
approval prerequisites