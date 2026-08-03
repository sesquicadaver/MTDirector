MikroTik Firewall Controller

Policy Model, Composition and Static Analysis Specification v0.1

Дата: 3 серпня 2026 року
Статус: нормативна специфікація M2 — Policy Core


---

1. Призначення

Документ визначає модель firewall-політик, їх ієрархічну композицію та статичний аналіз для централізованого керування MikroTik у межах однієї компанії.

Система повинна підтримувати:

Company baseline
        ↓
Site overlay
        ↓
Node overlay
        ↓
Temporary exception
        ↓
Node-resolved effective policy
        ↓
Static analysis
        ↓
Approval
        ↓
Подальший compile/deployment

Політика застосовується до логічного вузла:

standalone router;

один router із multi-WAN failover;

один router із балансуванням;

VRRP-вузол із декількох фізичних router;

MikroTik CRS у межах management plane.



---

2. Межі керування

У першій managed-версії система створює і змінює лише:

/ip firewall filter
/ipv6 firewall filter
/ip firewall address-list — власні статичні списки
/ipv6 firewall address-list — власні статичні списки
controller-owned chains
controller-owned anchors

Система читає та аналізує, але не змінює:

NAT
RAW
Mangle
routing tables
routing rules
routes
interface lists
VRRP
bridge/VLAN
switch-chip state
IP settings
IP services

Не входять до цієї специфікації:

RouterOS command generation;

API write path;

staging;

activation;

watchdog;

rollback;

campaign execution;

switch-chip ACL;

NAT, RAW, Mangle або routing writes.



---

3. RouterOS execution constraints

RouterOS filter має окремі predefined chains:

input   — traffic до самого router;
forward — routed traffic через router;
output  — traffic, створений самим router.

Rules виконуються зверху вниз. Terminal action припиняє обробку поточної chain; якщо жодне правило не спрацювало, packet приймається. Тому policy engine не має права покладатися на неявний fallthrough як на deny-by-default. 

Для routed traffic RAW і Mangle prerouting, connection tracking та destination NAT виконуються до filter forward; Mangle forward також виконується до filter forward. Для локального traffic відповідні filter input і output розташовані після попередніх packet-processing facilities. Через це filter policy не можна аналізувати ізольовано від RAW, Mangle, NAT та routing dependencies. 

RouterOS address lists використовуються одночасно у Filter, NAT і Mangle та можуть змінюватися динамічно firewall actions. Controller-owned static lists тому повинні мати окремий namespace і не можуть видалятися без перевірки unmanaged references. 

Interface-list membership визначається у порядку:

include
→ exclude
→ explicit static members

Записи /interface/list/member не містять members, отриманих через include та exclude. 

RAW обробляє packets до connection tracking і може переводити traffic у untracked. NAT застосовує правило лише до першого packet connection, після чого результат зберігає connection tracking. 

IPv4 FastTrack обходить filter, Mangle, частину connection tracking, IPsec і VRF assignment; FastTrack rule зазвичай потребує наступного еквівалентного accept, оскільки не кожний packet FastTracked connection проходить fast path. 

VRRP використовує IP protocol 112, IPv4 multicast 224.0.0.18 або IPv6 multicast ff02::12, TTL/Hop Limit 255. Один Device може бути master для одного VRID і backup для іншого. 

Hardware-switched bridge traffic не обов’язково проходить RouterOS IP firewall. Увімкнення use-ip-firewall змінює packet path і споживання CPU, а hardware-offloaded traffic може залишатися поза software forwarding path. 


---

4. Нормативні уточнення попередніх документів

4.1. Фіксований pipeline замість довільних sections

Попередня модель дозволяла company baseline довільно визначати набір і порядок sections.

Ця специфікація замінює її на фіксований Policy Pipeline v1.

Причини:

довільний порядок scope-level rules створює неоднозначне precedence;

звичайний accept-виняток може випадково обійти інші deny rules;

зміна section order може змінити firewall semantics без зміни самих rules;

статичний аналіз довільного pipeline значно складніший і менш доказовий.


Company baseline може змінювати rules і default disposition, але не порядок stage classes.

4.2. Exception не є ACCEPT

Temporary exception має effect:

EXEMPT_DENY_STAGE

Він пропускає конкретний deny stage, після чого traffic продовжує проходити всі наступні stages.

Виняток до company deny не обходить:

site deny;

node deny;

mandatory deny;

default disposition.


4.3. Rule order

order_key замінюється на:

ordinal: uint32

У межах revision, family, chain і stage ordinals:

починаються з 0;

не мають пропусків;

не повторюються;

визначають повний порядок.


4.4. FastTrack abstraction

Policy model не містить прямого RouterOS action:

fasttrack-connection

Використовується policy-level effect:

FASTTRACK_ACCEPT

Compiler повинен реалізувати його як узгоджену пару:

fasttrack-connection
accept


---

5. Основні інваріанти

1. Політика є декларативною, а не набором RouterOS commands.


2. Approved revision immutable.


3. Виправлення створює нову revision.


4. Scope нижчого рівня не змінює rule вищого рівня.


5. Не існує implicit override за однаковою назвою.


6. Всі references використовують UUID.


7. Rule identity не залежить від ordinal.


8. Composition не видаляє duplicate rules автоматично.


9. Composition не використовує last-write-wins.


10. Company mandatory deny не має exceptions.


11. Site policy не може послабити company deny.


12. Node policy не може послабити company або site deny.


13. Exception не є фінальним allow.


14. Default action ACCEPT заборонений.


15. Непідтримуваний matcher не може бути managed.


16. Unknown analysis result для safety flow є blocker.


17. IPv4 та IPv6 аналізуються окремо.


18. Усі фізичні members VRRP-вузла отримують одну logical policy.


19. Поточна VRRP role не впливає на склад desired policy.


20. Multi-WAN policy враховує всі uplinks, а не лише поточний active route.


21. Switch management policy не оголошується transit firewall policy.


22. Policy approval не запускає deployment.


23. Завершення строку exception не запускає deployment автоматично.


24. Policy tests виконуються Controller.


25. Desktop не реалізує власний policy evaluator.




---

6. Основні сутності

Policy
PolicyRevision
PolicyBinding
AddressObject
ServiceObject
ZoneDefinition
NodeZoneBinding
PolicyRule
PolicyTestCase
PolicyAnalysisRun
PolicyFinding
PolicyApproval

Окремі сутності для кожного matcher або section не створюються.


---

7. Policy

Policy {
    id: PolicyId
    name: NonEmptyString
    kind:
        COMPANY_BASELINE |
        SITE_OVERLAY |
        NODE_OVERLAY |
        EXCEPTION
    owner_scope:
        COMPANY |
        SITE |
        NODE
    owner_id: UUID?
    status:
        ACTIVE |
        ARCHIVED
}

Вимоги:

COMPANY_BASELINE належить COMPANY;

SITE_OVERLAY належить конкретному SITE;

NODE_OVERLAY належить конкретному NODE;

EXCEPTION має target SITE або NODE;

company-wide temporary exception заборонений — для цього створюється нова baseline revision.



---

8. PolicyRevision

PolicyRevision {
    id: PolicyRevisionId
    policy_id: PolicyId
    revision_number: uint32
    schema_version: uint32
    content_hash: Hash256
    parent_context_hash: Hash256?
    state:
        DRAFT |
        VALIDATED |
        IN_REVIEW |
        APPROVED |
        REJECTED |
        SUPERSEDED |
        REVOKED
    created_by: UserId
    created_at: UTC
    approved_at: UTC?
}

parent_context_hash фіксує active parent revisions, від яких залежить overlay:

SITE_OVERLAY:
    company baseline hash

NODE_OVERLAY:
    company baseline hash
    site overlay hash?

EXCEPTION:
    company baseline hash
    site overlay hash?
    node overlay hash?
    waived rule hash


---

9. Lifecycle revision

DRAFT
  ↓
VALIDATED
  ↓
IN_REVIEW
  ├──→ REJECTED
  ↓
APPROVED
  ├──→ SUPERSEDED
  └──→ REVOKED

Правила:

1. Редагується лише DRAFT.


2. Validation прив’язана до exact content_hash.


3. Зміна draft анулює попередній validation.


4. APPROVED не редагується.


5. SUPERSEDED залишається доступним для audit і rollback.


6. REVOKED не може бути новим desired binding.


7. Видалення revision заборонене.


8. Повторне відкриття approved revision заборонене.


9. Clone approved revision створює новий DRAFT.


10. Rule, що логічно продовжує попередню rule, зберігає UUID.




---

10. PolicyBinding

PolicyBinding {
    id: PolicyBindingId
    scope:
        COMPANY |
        SITE |
        NODE |
        EXCEPTION
    scope_id: UUID?
    desired_revision_id: PolicyRevisionId
    state:
        ACTIVE |
        DISABLED |
        EXPIRED_PENDING_RECONCILIATION
    valid_from: UTC?
    valid_until: UTC?
    row_version: uint64
}

Кардинальність:

COMPANY:  рівно одна active baseline revision
SITE:     не більше однієї active overlay revision
NODE:     не більше однієї active overlay revision
EXCEPTION: довільна bounded кількість

Exception після valid_until переходить у:

EXPIRED_PENDING_RECONCILIATION

Фактична deployed policy не змінюється до окремого deployment.


---

11. Visibility

11.1. Address, service і zone objects

Owner	Доступність

Company	Company, усі Sites і Nodes
Site	Цей Site та його Nodes
Node	Лише цей Node
Exception	Лише ця exception revision


References угору за hierarchy заборонені:

Company → Site object      заборонено
Site → Node object         заборонено
Node → Site object         дозволено
Node → Company object      дозволено

11.2. Імена

Name унікальний лише в межах:

owner scope
+ object type

Rule references використовують UUID, тому однакові display names у різних Sites не створюють ambiguity.


---

12. Policy Pipeline v1

Для кожної пари:

family:
    IPv4 | IPv6

chain:
    INPUT | FORWARD | OUTPUT

використовується однаковий logical pipeline:

1. PROTECTED_CONTROL_PLANE
2. MANDATORY_PRE_STATE_DENY
3. STATE_PRELUDE

4. COMPANY_DENY_EXEMPTIONS
5. COMPANY_DENY

6. SITE_DENY_EXEMPTIONS
7. SITE_DENY

8. NODE_DENY_EXEMPTIONS
9. NODE_DENY

10. COMPANY_ALLOW
11. SITE_ALLOW
12. NODE_ALLOW

13. DEFAULT_DISPOSITION


---

13. Stage permissions

Stage	Owner	Effects

PROTECTED_CONTROL_PLANE	Company	ACCEPT
MANDATORY_PRE_STATE_DENY	Company	DROP, REJECT
STATE_PRELUDE	Company	ACCEPT, DROP, FASTTRACK_ACCEPT
COMPANY_DENY_EXEMPTIONS	Exception	EXEMPT_DENY_STAGE
COMPANY_DENY	Company	DROP, REJECT
SITE_DENY_EXEMPTIONS	Exception	EXEMPT_DENY_STAGE
SITE_DENY	Site	DROP, REJECT
NODE_DENY_EXEMPTIONS	Exception	EXEMPT_DENY_STAGE
NODE_DENY	Node	DROP, REJECT
COMPANY_ALLOW	Company	ACCEPT
SITE_ALLOW	Site	ACCEPT
NODE_ALLOW	Node	ACCEPT


Stage order не конфігурується через GUI або database.


---

14. Stage semantics

14.1. PROTECTED_CONTROL_PLANE

Призначений для точних flows, необхідних для роботи node:

VRRP;

management control protocols;

node-local routing control protocols, якщо вони входять до scope;

explicitly registered protected services.


Цей stage не повинен використовуватися як загальний management allow-list. Controller API захищається окремим management guard до managed anchor.

14.2. MANDATORY_PRE_STATE_DENY

Deny rules, які мають діяти також на established traffic і не можуть бути обійдені exception.

Приклади:

безумовно заборонені адресні діапазони;

company-wide emergency blocks;

traffic classes, які не повинні бути дозволені state prelude.


14.3. STATE_PRELUDE

Stateful fast path:

established/related acceptance;

invalid drop;

FastTrack за доведених умов;

untracked handling.


Broad policy access rules у цьому stage заборонені.

14.4. Deny stages

Кожний deny stage має окрему logical subchain:

exception rules
→ deny rules
→ return to root pipeline

EXEMPT_DENY_STAGE повертає control до root після відповідного deny stage.

14.5. Allow stages

Allow stages є terminal:

ACCEPT

Allow нижчого scope не може обійти deny вищого або свого scope, оскільки deny stages розташовані раніше.


---

15. ChainContract

Company baseline повинна визначити contract для кожної enabled family/chain:

ChainContract {
    family: IPv4 | IPv6
    chain: INPUT | FORWARD | OUTPUT
    default_disposition:
        DROP |
        REJECT |
        RETURN_TO_UNMANAGED
    reject_mode: RejectMode?
}

Вимоги:

1. ACCEPT як default заборонений.


2. Contract визначає лише company baseline.


3. Site і Node не можуть змінити contract.


4. RETURN_TO_UNMANAGED має risk class CRITICAL.


5. RETURN_TO_UNMANAGED дозволений лише у migration/coexistence режимі.


6. Post-anchor rules при RETURN_TO_UNMANAGED повинні бути повністю проаналізовані.


7. Неявний RouterOS fallthrough не використовується.


8. Compiler повинен створити explicit terminal rule.




---

16. AddressObject

AddressObject {
    id: AddressObjectId
    owner_scope: COMPANY | SITE | NODE | EXCEPTION
    owner_id: UUID?
    name: NonEmptyString
    family: IPv4 | IPv6
    entries: AddressEntry[]
    description: string?
}

AddressEntry:

HOST
PREFIX
RANGE

Обмеження:

Family	HOST	PREFIX	RANGE

IPv4	Так	Так	Так
IPv6	Так	Так	Ні


У managed policy v1 заборонені:

DNS/FQDN entries;

dynamic entries;

timeout;

RouterOS-resolved names;

automatic DNS lookup;

references на unmanaged address lists;

вкладені address objects.



---

16.1. Address normalization

Canonicalization:

1. Перевести host у single-address interval.


2. Маскувати host bits у prefix.


3. Перевірити range.


4. Перетворити entries у disjoint intervals.


5. Відсортувати.


6. Об’єднати overlap.


7. Об’єднати adjacent ranges, коли це не змінює semantics.


8. Видалити duplicates.



Порожній resolved object є blocker.


---

17. AddressSelector

AddressSelector {
    include: AddressObjectId[]
    exclude: AddressObjectId[]
}

Semantics:

include порожній:
    Universe(family)

include непорожній:
    Union(include objects)

result:
    include_set - Union(exclude objects)

Вимоги:

всі objects мають ту саму family, що й rule;

object ID не повторюється;

include/exclude intersection дозволена і нормалізується;

порожній result створює RULE_UNSATISFIABLE;

direct inline IP у managed rule заборонений.



---

18. ServiceObject

ServiceObject {
    id: ServiceObjectId
    owner_scope: COMPANY | SITE | NODE | EXCEPTION
    owner_id: UUID?
    name: NonEmptyString
    terms: ServiceTerm[]
    description: string?
}

ServiceTerm {
    protocol: IpProtocol
    source_ports: PortSet?
    destination_ports: PortSet?
    icmp_selectors: IcmpSelectorSet?
}

IpProtocol:

number: uint8
canonical_name: string?

Named protocol є display metadata. Semantics визначає protocol number.


---

18.1. Service validation

1. Port sets дозволені лише для protocol із port semantics.


2. icmp_selectors дозволені лише ICMP або ICMPv6.


3. ICMP і ICMPv6 не змішуються.


4. IPv4 rule не може посилатися на ICMPv6 term.


5. IPv6 rule не може посилатися на IPv4 ICMP term.


6. protocol=any із ports заборонений.


7. Port intervals нормалізуються.


8. Duplicate terms видаляються canonicalization.


9. Overlapping terms об’єднуються, коли це детерміновано.


10. Порожній service object заборонений.




---

19. ServiceSelector

ServiceSelector {
    include: ServiceObjectId[]
}

Semantics:

порожній include:
    Any IP protocol

непорожній:
    Union(all service terms)

Service negation у policy v1 не підтримується.


---

20. ZoneDefinition

ZoneDefinition {
    id: ZoneId
    owner_scope: COMPANY | SITE | NODE
    owner_id: UUID?
    key: ZoneKey
    name: NonEmptyString
    description: string?
}

Типові logical zones:

MGMT
LAN
DMZ
SERVER
GUEST
VPN
WAN_PRIMARY
WAN_BACKUP
WAN_BALANCED
TRANSIT

Назви не є hardcoded. Semantics визначає Node binding.


---

21. NodeZoneBinding

NodeZoneBinding {
    id: NodeZoneBindingId
    node_id: NodeId
    zone_id: ZoneId
    binding:
        INTERFACE_LIST |
        SINGLE_INTERFACE |
        EXPLICIT_INTERFACE_SET
    values: string[]
    expected_dependency_hash: Hash256
    row_version: uint64
}

Правила:

1. INTERFACE_LIST є preferred binding.


2. Controller не змінює interface lists у першій managed-версії.


3. SINGLE_INTERFACE має рівно одне value.


4. EXPLICIT_INTERFACE_SET компілюється bounded expansion.


5. Усі interface names повинні існувати на кожному target Device.


6. Для VRRP members physical names можуть відрізнятися.


7. Binding resolve виконується окремо для кожного Device.


8. Dynamic interface membership у security-relevant zone є blocker.


9. Зміна resolved membership анулює analysis.


10. Порожня zone, яку використовує enabled rule, є blocker.



Interface-list resolution повинна враховувати include, exclude і explicit members у фактичному RouterOS порядку. 


---

22. ZoneSelector

ZoneSelector {
    include: ZoneId[]
    exclude: ZoneId[]
}

Semantics аналогічна AddressSelector.

Chain constraints:

Chain	Ingress zones	Egress zones

INPUT	Дозволено	Заборонено
FORWARD	Дозволено	Дозволено
OUTPUT	Заборонено	Дозволено


Zone overlap дозволений. Analyzer повинен працювати з фактичними resolved interface sets.


---

23. PolicyRule

PolicyRule {
    id: RuleId
    family: IPv4 | IPv6
    chain: INPUT | FORWARD | OUTPUT
    stage: PolicyStage
    ordinal: uint32
    enabled: bool
    predicate: TrafficPredicate
    effect: RuleEffect
    logging: LogSpecification
    exception_eligible: bool
    description: string
}

exception_eligible:

дозволений лише для DROP або REJECT;

заборонений у MANDATORY_PRE_STATE_DENY;

не має effect без окремої approved exception.


Disabled rule:

зберігається у revision;

бере участь у diff;

не входить до active evaluation;

проходить structural validation;

не може бути target active exception.



---

24. TrafficPredicate

TrafficPredicate {
    source_addresses: AddressSelector?
    destination_addresses: AddressSelector?

    ingress_zones: ZoneSelector?
    egress_zones: ZoneSelector?

    services: ServiceSelector?

    connection_states: ConnectionStateSet?
    connection_nat_states: ConnectionNatStateSet?

    source_address_types: AddressTypeSet?
    destination_address_types: AddressTypeSet?

    tcp_flags: TcpFlagConstraint?
    ipsec_policy: IpsecPolicyPredicate?
}

Supported connection states:

NEW
ESTABLISHED
RELATED
INVALID
UNTRACKED

Supported NAT states:

SRCNAT
DSTNAT

Supported TCP flag constraint:

required_present
required_absent

Supported IPsec predicate:

direction:
    IN | OUT

policy:
    IPSEC | NONE


---

25. Unsupported managed matchers

У policy v1 заборонені:

src-mac-address
packet-mark
connection-mark
routing-mark
content
layer7-protocol
tls-host
connection-rate
connection-bytes
connection-limit
limit
dst-limit
random
nth
PCC
time
hotspot
helper
realm
packet-size
DSCP
priority
fragment
IPv4 options
IPv6 header expression

Ці matchers:

читаються з unmanaged RouterOS rules;

враховуються як dependency або unknown;

не редагуються policy editor;

не компілюються з policy v1.



---

26. RuleEffect

RuleEffect {
    kind:
        ACCEPT |
        DROP |
        REJECT |
        FASTTRACK_ACCEPT |
        EXEMPT_DENY_STAGE

    reject_mode: RejectMode?
}

RejectMode:

TCP_RESET
ADMIN_PROHIBITED
PORT_UNREACHABLE

Validation:

TCP_RESET вимагає, щоб predicate відповідав лише TCP;

family-specific RouterOS mapping визначатиме compiler;

EXEMPT_DENY_STAGE не має reject mode;

FASTTRACK_ACCEPT не має reject mode.



---

27. LogSpecification

LogSpecification {
    enabled: bool
    prefix: string?
}

Вимоги:

1. Prefix не більше 32 ASCII characters.


2. Prefix не містить control characters.


3. Compiler додає namespace mfc:.


4. Log payload не конфігурується.


5. Broad catch-all logging створює warning.


6. Logging не змінює traffic verdict.


7. Logging не використовується як окрема passthrough rule.


8. Rule із log=true залишається terminal відповідно до effect.




---

28. Temporary exception

Exception revision містить:

ExceptionMetadata {
    target_scope: SITE | NODE
    target_scope_id: UUID
    target_stage:
        COMPANY_DENY |
        SITE_DENY |
        NODE_DENY
    waived_rule_id: RuleId
    valid_from: UTC
    valid_until: UTC
    reason: NonEmptyString
    ticket_reference: NonEmptyString
    supersedes_exception_id: UUID?
}

Exception rule:

effect = EXEMPT_DENY_STAGE
predicate = дозволений traffic subset


---

28.1. Exception invariants

1. waived_rule_id має існувати в composed policy.


2. Target rule має бути enabled.


3. Target effect має бути DROP або REJECT.


4. Target rule має exception_eligible=true.


5. Exception family і chain збігаються з target.


6. Exception predicate є доведеним subset target predicate.


7. Exception target stage відповідає stage target rule.


8. Exception predicate не перетинає іншу non-target deny rule у тому самому stage.


9. Exception не обходить mandatory deny.


10. Exception не є final ACCEPT.


11. Exception має кінцевий valid_until.


12. valid_until > valid_from.


13. Exception не може бути company-wide.


14. Зміна target rule анулює exception analysis.


15. Один exception rule waives рівно одну deny rule.


16. Для декількох target rules створюються окремі exception rules.




---

28.2. Logical execution

root chain:
    jump company-deny-stage

company-deny-stage:
    exception predicate → RETURN
    target deny         → DROP/REJECT
    other deny rules
    RETURN

Exception bypasses тільки весь target stage для свого traffic.

Саме тому analyzer повинен довести, що exception не intersect-ить інші deny rules цього stage.


---

29. Composition input

Для одного Node:

Company baseline revision — required
Site overlay revision      — optional
Node overlay revision      — optional
Active exception revisions — zero or more
Node zone bindings

Для VRRP Node logical composition виконується один раз, а zone resolution — окремо для кожного physical Device.


---

30. Composition algorithm

1. Load approved company baseline.
2. Load active Site overlay.
3. Load active Node overlay.
4. Select non-expired active exceptions for Node.
5. Verify parent_context_hash for every child revision.
6. Build visible object namespace.
7. Resolve all UUID references.
8. Validate scope ownership.
9. Validate stage ownership.
10. Validate family/chain/effect constraints.
11. Validate ordinals.
12. Remove disabled rules from active evaluation.
13. Insert exceptions into target deny substages.
14. Preserve all ordinary rules without deduplication.
15. Build fixed Pipeline v1 for every family/chain.
16. Canonicalize objects, predicates and rules.
17. Calculate logical effective policy hash.

Будь-яка помилка припиняє composition.


---

31. Deterministic ordering

Rules сортуються:

family:
    IPv4
    IPv6

chain:
    INPUT
    FORWARD
    OUTPUT

stage:
    fixed Pipeline v1 order

within stage:
    ordinal ascending

Exception substage:

target stage
→ exception revision ID
→ exception rule ordinal
→ rule UUID

Rule UUID є лише deterministic tie-breaker для exceptions із різних revisions. У звичайному revision duplicate ordinal є blocker.


---

32. Object resolution

Resolution order:

1. Company objects
2. Site objects
3. Node objects
4. Current exception objects

ID collisions заборонені незалежно від scope.

Names не використовуються для resolution.

Object, який не використовується жодною enabled rule або test case, створює:

UNUSED_POLICY_OBJECT

Severity:

INFO


---

33. Policy canonical representation

Policy revisions використовують canonical writer MFC-CJ1, визначений для snapshots.

Canonical revision document містить:

schema
policy kind
owner scope
chain contracts
zone definitions
address objects
service objects
rules
tests
exception metadata

Revision hash:

policy_revision_hash =
SHA256(exact canonical revision bytes)


---

34. Effective policy hashes

34.1. Logical effective policy hash

Не залежить від physical interface names.

logical_effective_hash =
SHA256(
    policy schema version
    + pipeline version
    + company revision hash
    + site revision hash?
    + node revision hash?
    + ordered exception hashes
    + canonical composed objects
    + canonical composed rules
    + chain contracts
)

34.2. Device-resolved policy hash

device_resolved_hash =
SHA256(
    logical_effective_hash
    + device_id
    + zone binding definitions
    + resolved interface sets
    + capability hash
)

34.3. Analysis context hash

analysis_context_hash =
SHA256(
    device_resolved_hash
    + actual configuration hash
    + compatibility hash
    + relevant observation hash
    + anchor context hash
    + management path profile hash
    + analyzer version
)

34.4. Analysis bundle hash

Для Node:

analysis_bundle_hash =
SHA256(
    logical_effective_hash
    + ordered per-device analysis result hashes
    + topology projection hash
    + impact set hash
)


---

35. Static analysis input

PolicyAnalysisInput {
    composed logical policy
    per-device resolved zones
    current completed snapshots
    capability profiles
    topology projection
    actual RouterOS filter context
    anchor and guard context
    management access profiles
    protected flows
    policy tests
    analyzer version
}

Для approval active Node snapshots повинні походити з одного consistent Node capture operation.

Latest-known snapshots із різних capture operations можуть використовуватися лише для preview.


---

36. Analysis levels

1. SCHEMA
2. STRUCTURAL
3. COMPOSITION
4. PREDICATE
5. SEQUENCE
6. ACTUAL_CONTEXT
7. SAFETY
8. POLICY_TESTS
9. RISK

Кожний рівень виконується лише після успішного завершення попередніх рівнів, крім незалежних informational checks.


---

37. Predicate algebra

Managed predicate нормалізується у bounded union:

NormalizedPredicate =
    AtomicTrafficCube[]

AtomicTrafficCube:

family
chain
source address interval set
destination address interval set
ingress interface set
egress interface set
protocol set
protocol-specific source port set
protocol-specific destination port set
ICMP type/code set
connection state set
connection NAT state set
source address type set
destination address type set
TCP flag constraint
IPsec policy set


---

37.1. Exact representations

Dimension	Representation

IPv4 address	Disjoint uint32 intervals
IPv6 address	Disjoint uint128 intervals
Interfaces	Sorted finite ID set
Protocol	256-bit set
Ports	Disjoint uint16 intervals
Connection state	Bit set
NAT state	Bit set
Address type	Bit set
TCP flags	Required-present / required-absent
ICMP type/code	Disjoint pair set
IPsec	Finite enum set



---

37.2. Relations

Analyzer повинен обчислювати:

EMPTY
EQUAL
DISJOINT
SUBSET
SUPERSET
PARTIAL_OVERLAP
INDETERMINATE

Для managed predicates INDETERMINATE не допускається.

Для unmanaged RouterOS rules INDETERMINATE дозволений, але safety result стає blocker.


---

37.3. Expansion limits

maximum atomic variants per rule:        128
maximum residual fragments per analysis: 4096
maximum predicate dimensions:            fixed by schema

Перевищення:

PREDICATE_COMPLEXITY_LIMIT

Без переходу до unbounded алгоритму.


---

38. Structural validation

Обов’язкові checks:

schema version
policy kind
scope ownership
UUID format
global UUID uniqueness
family
chain
stage
effect
ordinal
object references
object visibility
zone references
service compatibility
address family
port range
ICMP family
TCP flag compatibility
IPsec direction
reject mode
logging prefix
rule count limits
test case validity
exception metadata
chain contracts

Помилка structural validation є blocker.


---

39. Predicate satisfiability

Rule є unsatisfiable, якщо:

resolved source set порожній;

resolved destination set порожній;

ingress zone set порожній;

egress zone set порожній;

service union порожній;

port matcher не має допустимого protocol;

TCP flags одночасно required present і absent;

ICMP selector не відповідає family;

chain не має зазначеного interface direction;

IPsec direction несумісний chain;

combination connection states порожня;

address types взаємно виключні;

selector exclusions повністю видалили universe.


Finding:

RULE_UNSATISFIABLE
severity: BLOCKER


---

40. Duplicate analysis

40.1. Exact duplicate

Дві rules мають:

equal normalized predicate
equal effect
equal logging

Finding:

RULE_EXACT_DUPLICATE
severity: WARNING

Duplicate не видаляється автоматично.

40.2. Same predicate, different effect

RULE_CONFLICTING_DUPLICATE
severity: BLOCKER

Раніша rule повністю визначає результат, а пізніша недосяжна.


---

41. Shadowing analysis

Для кожної active rule обчислюється residual traffic space після всіх попередніх terminal rules.

Алгоритм:

residual = rule predicate

FOR each previous terminal rule:
    residual = residual - previous predicate

    IF fragment limit exceeded:
        return INDETERMINATE

IF residual empty:
    fully shadowed
ELSE IF residual differs from original:
    partially shadowed

Canonical dimension split order:

family
chain
protocol
ingress interfaces
egress interfaces
connection states
NAT states
source addresses
destination addresses
source ports
destination ports
ICMP
TCP flags
IPsec
address types


---

41.1. Findings

RULE_FULLY_SHADOWED
    BLOCKER

RULE_PARTIALLY_SHADOWED
    WARNING

SHADOW_ANALYSIS_INDETERMINATE
    BLOCKER для safety/control-plane
    WARNING для звичайної policy

Enabled fully shadowed rule не може бути approved.


---

42. Overlap analysis

Для intersecting rules:

Earlier effect	Later effect	Result

ACCEPT	ACCEPT	Redundancy
DROP	DROP	Redundancy
REJECT	DROP/REJECT	Order-dependent deny
ACCEPT	DROP/REJECT	Earlier allow bypasses later deny
DROP/REJECT	ACCEPT	Earlier deny constrains later allow
FASTTRACK_ACCEPT	Будь-який	FastTrack dependency
EXEMPT stage	Deny	Exception validation


Findings:

ORDER_DEPENDENT_OVERLAP
EARLIER_ALLOW_BYPASSES_DENY
REDUNDANT_OVERLAP
FASTTRACK_OVERLAP

Partial overlap не є автоматично помилкою, але має structured witness traffic.


---

43. Witness generation

Для кожної:

enabled rule;

overlap;

shadow;

failed test;

safety finding;


analyzer повинен, коли це математично можливо, створити concrete witness:

family
chain
source IP
destination IP
ingress interface
egress interface
protocol
ports
connection state
NAT state
TCP flags
ICMP type/code
IPsec state

Witness:

не є real traffic capture;

не містить credentials;

генерується з normalized predicate;

показується у technical analysis view.



---

44. Actual RouterOS filter context

Policy-only analysis недостатній. Для Node approval виконується analysis фактичного execution path:

management guard
→ unmanaged pre-anchor rules
→ managed anchor
→ candidate managed pipeline
→ unmanaged post-anchor rules
→ RouterOS built-in fallthrough

Оскільки RouterOS обробляє rules послідовно, а unmatched traffic у built-in chain приймається, unmanaged context після RETURN_TO_UNMANAGED є частиною фактичної security semantics. 


---

45. Actual filter control-flow graph

Analyzer будує bounded graph:

chain
rule
jump
return
terminal action
fallthrough

Supported unmanaged actions:

ACCEPT
DROP
REJECT
FASTTRACK_CONNECTION
JUMP
RETURN
LOG
PASSTHROUGH

Unsupported stateful side effects:

ADD_SRC_TO_ADDRESS_LIST
ADD_DST_TO_ADDRESS_LIST
TARPIT
unknown action

При їх можливому впливі результат:

ACTUAL_FILTER_ANALYSIS_INDETERMINATE


---

45.1. Graph limits

maximum chains:       1024
maximum jump depth:   16
maximum graph nodes:  50 000

Findings:

ACTUAL_FILTER_JUMP_CYCLE
ACTUAL_FILTER_DEPTH_LIMIT
ACTUAL_FILTER_UNKNOWN_ACTION
ACTUAL_FILTER_UNKNOWN_MATCHER

Safety-related finding має severity BLOCKER.


---

45.2. Pre-anchor rules

Unmanaged pre-anchor rule може повністю обійти managed policy.

Обов’язкові findings:

PRE_ANCHOR_ACCEPT_BYPASSES_POLICY
PRE_ANCHOR_DROP_SHADOWS_POLICY
PRE_ANCHOR_FASTTRACK_BYPASSES_POLICY
PRE_ANCHOR_DYNAMIC_RULE_PRESENT
PRE_ANCHOR_ANALYSIS_INDETERMINATE

Controller не переміщує такі rules автоматично.


---

45.3. Post-anchor rules

Post-anchor analysis потрібний лише коли candidate може виконати:

RETURN_TO_UNMANAGED

При terminal DROP або REJECT post-anchor rules недосяжні з managed path.


---

46. Management-path safety validation

Management guard є окремим захищеним контуром і не входить до звичайної policy revision.

Для кожного physical Device визначається:

ManagementAccessProfile {
    controller_source_prefixes
    management_destination
    API-SSL port
    expected ingress zone/interface
    expected egress zone/interface
    trust profile
    OOB status
}


---

46.1. Mandatory checks

1. api-ssl enabled.


2. Actual port відповідає profile.


3. Controller source дозволений IP service restriction.


4. Management guard існує.


5. Guard розташований до managed anchor.


6. Guard ownership marker валідний.


7. Guard дозволяє TCP NEW до API-SSL.


8. Reply traffic ESTABLISHED дозволений у output path.


9. Unmanaged pre-anchor rule не блокує path.


10. Candidate policy не залежить від RouterOS default accept.


11. Candidate не змінює guard.


12. VRRP virtual address не є єдиною management address.


13. Кожний VRRP member перевіряється окремо.


14. OOB path перевіряється незалежно.


15. Unknown result є blocker.



Findings:

MANAGEMENT_GUARD_MISSING
MANAGEMENT_GUARD_MOVED
MANAGEMENT_SERVICE_DISABLED
MANAGEMENT_SOURCE_NOT_ALLOWED
MANAGEMENT_INPUT_BLOCKED
MANAGEMENT_OUTPUT_BLOCKED
MANAGEMENT_PATH_INDETERMINATE


---

47. VRRP safety validation

Для кожного VRRP instance Controller генерує protected flows.

47.1. Advertisement flows

IPv4:

protocol: 112
destination: 224.0.0.18
TTL: 255
chain: INPUT / OUTPUT
interface: VRRP parent interface

IPv6:

protocol: 112
destination: ff02::12
Hop Limit: 255
chain: INPUT / OUTPUT
interface: VRRP parent interface

RouterOS VRRP використовує саме ці multicast destinations і protocol number, а VRID для IPv4 та IPv6 описує різні Virtual Routers. 


---

47.2. Connection tracking synchronization

Коли sync-connection-tracking=yes, Controller також захищає configured UDP synchronization flow між VRRP members.

Default RouterOS connection-tracking synchronization port — UDP 8275; active-active VRRP groups можуть використовувати окремі configured ports. 


---

47.3. Node-level checks

1. Усі members доступні.


2. Усі members мають consistent configuration snapshots.


3. Same VRID members мають compatible RouterOS versions.


4. Protected advertisement flows accepted.


5. Synchronization flows accepted, якщо enabled.


6. Policy однакова для master і backup.


7. Split-master role vector не спрощується до global role.


8. Current role не визначає target devices.


9. Missing member є blocker.


10. Observation skew не використовується для хибного split-brain finding.


11. VRRP parent interface входить у правильну zone.


12. Strict rp-filter із VRRP створює blocker або explicit approved infrastructure exception.




---

48. Multi-WAN safety validation

Multi-WAN analyzer використовує:

declared uplink mode
routes
routing tables
routing rules
Mangle marks
PCC
NAT rules
interface lists
zone bindings
active default routes
rp-filter
protected probe profiles

Policy routing у RouterOS може використовувати routing tables, routing rules і Mangle marking, тому firewall policy не може припускати, що весь traffic використовує тільки main table або один WAN interface. 


---

48.1. Uplink coverage

Для mode:

FAILOVER
BALANCED
MIXED

обов’язково:

кожний uplink має zone binding;

primary uplink присутній;

backup uplinks присутні;

balanced uplinks присутні;

rules не залежать лише від поточного active interface без explicit reason;

management route має щонайменше один validated path;

health-check flow має output permission;

NAT dependencies не змінилися після analysis snapshot.



---

48.2. Asymmetric routing

Strict reverse-path filtering не сумісний із routing tables і є проблемним для asymmetric routing та VRRP; RouterOS рекомендує loose mode для складної/asymmetric topology. 

Findings:

STRICT_RPF_WITH_ROUTING_TABLES
STRICT_RPF_WITH_VRRP
STRICT_RPF_WITH_ASYMMETRIC_ROUTING

Severity:

BLOCKER

за відсутності explicit infrastructure exception.


---

48.3. Invalid-state drop

RouterOS documentation окремо застерігає, що blanket drop connection-state=invalid може блокувати asymmetric traffic. 

Analyzer повинен створювати:

INVALID_DROP_WITH_ASYMMETRIC_ROUTING

коли одночасно:

Node має asymmetric/balanced evidence;

rule drops INVALID;

predicate охоплює affected forwarding paths;

немає доведеного exemption.



---

48.4. Backup-path tests

Для кожного backup uplink створюються tests:

outbound health-check traffic
router-originated DNS/ICMP/TCP probe
management return path, якщо applicable
filter permission через backup zone

Controller не відключає primary WAN під час policy analysis.


---

49. RAW dependencies

RAW може виконувати notrack до connection tracking. У такому разі stateful filter matchers не мають звичайної connection-tracking semantics. 

Analyzer повинен:

1. Знайти RAW notrack predicates.


2. Порівняти їх із managed stateful predicates.


3. Визначити intersection.


4. Перевірити handling UNTRACKED.


5. Заблокувати policy, якщо результат indeterminate.



Findings:

RAW_NOTRACK_INTERSECTS_STATEFUL_RULE
RAW_NOTRACK_TRAFFIC_NOT_HANDLED
RAW_DEPENDENCY_INDETERMINATE


---

50. NAT dependencies

NAT rule застосовується до першого packet connection, а подальша translation зберігається connection tracking. Тому зміна filter policy не повинна інтерпретувати поточні established NAT connections як доказ поведінки нових connections. 

Checks:

1. Rule із connection-nat-state=DSTNAT вимагає доступний NAT snapshot.


2. Відсутність relevant dstnat rules створює warning.


3. Unknown NAT matchers створюють indeterminate dependency.


4. NAT rules не змінюються policy compiler.


5. NAT configuration hash входить до analysis context.


6. Зміна NAT invalidates node analysis.


7. Existing connections не використовуються як єдиний acceptance criterion.



Findings:

DSTNAT_MATCH_WITHOUT_NAT_EVIDENCE
NAT_DEPENDENCY_CHANGED
NAT_DEPENDENCY_INDETERMINATE


---

51. Mangle dependencies

Mangle може встановлювати connection, packet і routing marks, які використовуються routing, NAT та іншими facilities. 

Managed policy v1 не match-ить marks, але analyzer повинен визначати:

PCC;

routing-mark generation;

connection-mark dependencies;

packet-mark dependencies;

policy-routing paths;

FastTrack conflicts.


Findings:

MANGLE_PCC_PRESENT
MANGLE_ROUTING_MARK_PRESENT
MANGLE_DEPENDENCY_CHANGED
MANGLE_ANALYSIS_INDETERMINATE

Наявність marks сама по собі не блокує звичайну filter policy. Вона блокує unsafe FastTrack і assumptions про один routing path.


---

52. FastTrack constraints

FASTTRACK_ACCEPT дозволений лише коли виконані всі умови:

family = IPv4
chain = FORWARD
stage = STATE_PRELUDE
owner = Company
protocol subset = TCP або UDP
connection-state subset = ESTABLISHED, RELATED

Compiler повинен створити еквівалентну ACCEPT fallback rule.


---

52.1. FastTrack blockers

IPv6
INPUT або OUTPUT
PCC
routing marks
packet marks, потрібні після FastTrack point
non-main routing tables для matched traffic
VRF dependency
IPsec dependency
HotSpot dependency
global queue-tree dependency
unknown Mangle dependency
unknown pre-anchor FastTrack
untracked traffic
missing connection tracking

FastTrack обходить filter/Mangle, IPsec і VRF assignment, тому його використання на таких Nodes дозволяється лише після повного доказу disjoint traffic space. 


---

52.2. Multi-WAN

Mode	FastTrack

SINGLE	Можливий після validation
FAILOVER тільки main table	Можливий після validation
FAILOVER із routing marks/tables	Block
BALANCED	Block
MIXED	Block
Unknown topology	Block



---

52.3. Existing unmanaged FastTrack

Unmanaged FastTrack до managed anchor створює:

PRE_ANCHOR_FASTTRACK_BYPASSES_POLICY

Candidate deny rule, який intersect-ить такий traffic, не вважається ефективним.


---

53. Switch policy constraints

Для Node.kind=SWITCH managed policy v1 дозволяє лише:

IPv4 INPUT
IPv6 INPUT
IPv4 OUTPUT
IPv6 OUTPUT

FORWARD заборонений незалежно від наявності RouterOS filter table.

Причина: bridge або hardware-offloaded transit traffic може не проходити software IP firewall. 

Findings:

SWITCH_FORWARD_POLICY_UNSUPPORTED
SWITCH_HARDWARE_PROFILE_UNKNOWN
SWITCH_TRANSIT_PATH_NOT_PROVEN

Physical CRS hardware profile обов’язковий для production support конкретної моделі.


---

54. PolicyTestCase

PolicyTestCase {
    id: PolicyTestId
    name: NonEmptyString
    origin:
        USER |
        SYSTEM
    execution_mode:
        MANAGED_ONLY |
        NODE_EFFECTIVE
    packet: TestPacket
    expected:
        ACCEPT |
        DROP |
        REJECT |
        FASTTRACK_ACCEPT |
        RETURN_TO_UNMANAGED
    expected_rule_id: RuleId?
}

TestPacket {
    family
    chain
    source_address
    destination_address
    ingress_interface?
    egress_interface?
    protocol
    source_port?
    destination_port?
    connection_state?
    connection_nat_state?
    source_address_type?
    destination_address_type?
    tcp_flags?
    icmp_type?
    icmp_code?
    ipsec_policy?
}


---

55. Mandatory user tests

1. Кожна enabled ACCEPT rule повинна мати щонайменше один positive test або generated proven witness.


2. Кожна FASTTRACK_ACCEPT rule має positive test і fallback accept test.


3. Кожний exception має:

positive test;

negative boundary test;

test, що доводить дію іншого deny stage.



4. Зміна default disposition має terminal test.


5. Rule із REJECT має test reject mode.


6. Site/Node overlay має test, що higher-scope deny не обходиться.


7. Critical address object change має impact tests для dependent rules.



Generated witness не замінює manually specified business-critical test.


---

56. Mandatory system tests

Controller генерує:

controller API-SSL access
controller API reply path
management guard reachability
management denial з WAN
VRRP IPv4 advertisements
VRRP IPv6 advertisements
VRRP connection-tracking synchronization
uplink health checks
backup uplink output
protected management VPN flows
chain default disposition
unmatched WAN input
unmatched WAN forward

System test не можна видалити або disable.


---

57. Test evaluator

Evaluator повертає:

PolicyTestResult {
    test_id
    outcome
    matched_path[]
    matched_rule_id?
    matched_stage?
    final_disposition
    proof:
        PROVEN |
        INDETERMINATE
    failure_code?
}

matched_path містить:

management guard
unmanaged rule
managed stage
managed rule
exception return
default disposition
post-anchor rule
built-in fallthrough

Safety test із INDETERMINATE вважається failed.


---

58. PolicyFinding

PolicyFinding {
    code: string
    severity:
        BLOCKER |
        WARNING |
        INFO
    proof:
        PROVEN |
        CONSERVATIVE |
        INDETERMINATE
    scope:
        REVISION |
        NODE |
        DEVICE |
        RULE |
        OBJECT |
        TEST
    target_id: UUID?
    family?
    chain?
    stage?
    related_rule_ids: RuleId[]
    witness: TestPacket?
    structured_details: JSON
}

Natural-language message не є єдиним джерелом finding semantics.


---

59. Finding severity

BLOCKER

Забороняє:

approval;

binding;

deployment plan.


WARNING

Потребує explicit acknowledgment конкретним reviewer для exact analysis hash.

INFO

Не потребує acknowledgment.

Після зміни analysis hash усі warning acknowledgments анулюються.


---

60. Risk classification

RiskLevel {
    NONE
    LOW
    MEDIUM
    HIGH
    CRITICAL
}


---

60.1. Semantic change classes

NO_EFFECTIVE_CHANGE
RESTRICTIVE
PERMISSIVE
MIXED
CONTROL_PLANE
FASTTRACK
EXCEPTION
DEFAULT_DISPOSITION
ZONE_BINDING

60.2. Initial risk mapping

Change	Minimum risk

Comment only	LOW
Disabled unused rule	LOW
Add deny rule	MEDIUM
Remove allow rule	MEDIUM
Add allow rule	HIGH
Remove deny rule	HIGH
Expand address object used by allow	HIGH
Shrink address object used by deny	HIGH
Temporary exception	HIGH
FastTrack change	HIGH
Management path	CRITICAL
VRRP protected flow	CRITICAL
Default DROP → RETURN	CRITICAL
Zone binding used by many rules	CRITICAL
Unknown analysis result	CRITICAL


Final risk є maximum усіх findings і semantic effects.


---

61. Revision diff

Policy semantic diff повинен показувати:

rules added
rules removed
rules modified
rules moved
rules enabled/disabled

address objects changed
service objects changed
zones changed
chain contracts changed
tests changed
exceptions added/expired/revoked

effective packet-space changes:
    newly accepted
    newly denied
    changed reject behavior
    changed stage precedence effect

Rule із тим самим UUID:

MODIFIED
MOVED
ENABLED
DISABLED

Rule із новим UUID:

ADDED

Rule без UUID match:

REMOVED

Fuzzy matching policy rules за similar fields заборонений.


---

62. Impact analysis

Для revision визначається immutable impact set:

Company baseline:
    усі active managed Nodes

Site overlay:
    усі active managed Nodes Site

Node overlay:
    конкретний Node

Exception:
    target Site або Node

Для кожного Node результат:

PASS
BLOCKED
EXCLUDED_DISABLED
EXCLUDED_MAINTENANCE

Active unreachable Node не може бути silently excluded.


---

63. Approval prerequisites

Policy revision може отримати APPROVED лише коли:

1. Content hash зафіксований.


2. Parent context hashes актуальні.


3. Structural validation пройдена.


4. Composition пройдена.


5. Predicate validation пройдена.


6. Немає blockers.


7. Усі warnings acknowledged.


8. Усі mandatory tests пройдені.


9. System safety tests пройдені.


10. Impact set повний.


11. Усі active target Nodes мають valid analysis.


12. Zone bindings resolved.


13. Capability profiles supported.


14. Compatibility findings не містять unknown managed semantics.


15. Management guards valid.


16. VRRP members повністю охоплені.


17. Multi-WAN dependencies проаналізовані.


18. Actual anchor context не drifted.


19. Analysis bundle hash зафіксований.


20. Reviewer має потрібну role.


21. High/Critical change reviewed окремою особою.


22. Exception має reason, ticket і expiry.


23. Approval створює audit event.




---

64. Approval invalidation

Approval context стає stale при зміні:

company baseline binding
site overlay binding
node overlay binding
active exceptions
zone binding
Node membership
RouterOS configuration hash
capability hash
compatibility hash
management access profile
anchor/guard context
analyzer version
policy schema version
pipeline version

Runtime observation change не змінює revision approval автоматично, але може блокувати deployment readiness.

Приклади:

VRRP role change:
    approval не анулюється
    deployment readiness rechecked

active WAN route change:
    approval не анулюється
    deployment probes rechecked

interface-list configuration change:
    approval stale

RouterOS version change:
    approval stale


---

65. Separation of duties

Risk	Approval

LOW	Один Reviewer
MEDIUM	Один Reviewer
HIGH	Reviewer, відмінний від автора
CRITICAL	Два reviewers, один із security/network owner group


Emergency workflow може обійти звичайний review count, але не може обійти:

management safety
VRRP safety
unsupported capability
predicate indeterminacy
rollback prerequisites


---

66. Persistence model

Policy storage є document-centric.

Основні таблиці:

policies
policy_revisions
policy_bindings
node_zone_bindings
policy_analysis_runs
policy_findings
policy_test_results
policy_approvals
warning_acknowledgments

Rules, objects і tests усередині immutable revision не дублюються в окремих authoritative relational tables.

policy_revisions зберігає:

canonical content hash
compressed canonical payload
schema version
state
parent context hash
metadata

Search/index projections можуть створюватися окремо, але не є джерелом істини.


---

67. Immutability

Database application role не має права:

UPDATE approved revision payload
DELETE approved revision
UPDATE approval record
DELETE approval record
UPDATE completed analysis run

Зміни lifecycle state виконуються окремими constrained operations.

Audit history не cascade-delete-иться.


---

68. Resource limits

Resource	Limit

Rules в одній revision	5 000
Effective rules на family/chain	20 000
Address objects на revision	2 000
Entries в address object	100 000
Total resolved address entries на Node/family	250 000
Service objects на revision	2 000
Terms у service object	32
Port intervals у service object	128
Zones на Node	128
Interfaces у resolved zone	1 024
Active exceptions на Node	256
Policy tests на revision	10 000
Atomic predicate variants на rule	128
Residual fragments	4 096
Actual filter graph nodes	50 000
Jump depth	16


Перевищення limit є blocker, а не приводом до автоматичного збільшення.


---

69. Performance requirements

На reference Controller host:

Operation	Target

Compose 1 000 rules	до 500 ms
Normalize 1 000 rules	до 1 s
Analyze 1 000 rules	до 2 s
Execute 10 000 tests	до 3 s
Analyze 20 000-rule chain	до 10 s або bounded conservative result
Compare two 5 000-rule revisions	до 3 s


Вимоги:

bounded parallelism;

cancellation;

no unbounded task creation;

no UI-thread analysis;

deterministic result незалежно від parallel execution;

cache key включає analysis context hash;

cache не використовується після dependency change.



---

70. Unit tests

Обов’язкові domains:

revision lifecycle
scope visibility
fixed pipeline ordering
object resolution
address normalization
service normalization
zone resolution
rule validation
exception subset proof
exception overlap detection
composition hashing
predicate intersection
predicate subset
predicate subtraction
witness generation
duplicate detection
shadowing
partial shadowing
actual chain graph
jump/return
risk classification
approval invalidation


---

71. Property-based tests

Compose(input) завжди deterministic

Normalize(Normalize(predicate))
    == Normalize(predicate)

Intersect(A, B)
    == Intersect(B, A)

A - A
    == Empty

A subset A
    == true

Exception subset target
    never expands target packet space

Changing ordinal only
    does not change rule identity

Changing rule UUID
    always changes rule identity

Node deny
    cannot be bypassed by Node allow

Site deny
    cannot be bypassed by Node allow

Company deny
    cannot be bypassed by Site або Node allow

Mandatory deny
    cannot be bypassed by any exception


---

72. Static-analysis test matrix

empty address object
fully excluded address selector
incompatible IPv4/IPv6 object
port without transport protocol
TCP flags with UDP
ICMPv6 in IPv4 rule
ingress zone in OUTPUT
egress zone in INPUT
duplicate rule
same predicate, different action
fully shadowed rule
partially shadowed rule
unconditional terminal rule
managed rule moved
managed rule modified
broad logging
exception subset
exception not subset
exception overlaps another deny
exception targets mandatory deny
expired exception
stale parent hash
RETURN_TO_UNMANAGED
unknown pre-anchor matcher
pre-anchor accept
pre-anchor drop
pre-anchor FastTrack
jump cycle
dynamic pre-anchor rule


---

73. Management and topology test matrix

standalone management access
standalone management output reply
VRRP member A access
VRRP member B access
VRRP role swap
VRRP split-master
VRRP connection-sync port
missing VRRP member
RouterOS version mismatch
single WAN
failover primary active
failover backup active
PCC balanced
mixed routing
strict rp-filter
RAW notrack
DSTNAT dependency
unknown NAT matcher
unknown Mangle dependency
switch INPUT policy
switch FORWARD rejection


---

74. FastTrack test matrix

valid IPv4 FORWARD established TCP
valid IPv4 FORWARD related UDP
missing fallback accept
IPv6 FastTrack
INPUT FastTrack
NEW connection FastTrack
non-TCP/UDP FastTrack
PCC present
routing mark present
VRF evidence
IPsec evidence
unmanaged pre-anchor FastTrack
FastTrack overlapping mandatory deny
FastTrack overlapping site deny


---

75. Acceptance criteria

Специфікація реалізована лише коли:

1. Policy Pipeline v1 є fixed.


2. Stage order не редагується через GUI/DB.


3. Approved revisions immutable.


4. Composition не використовує name-based override.


5. Scope hierarchy дотримується.


6. Company deny не обходиться Site/Node allow.


7. Site deny не обходиться Node allow.


8. Mandatory deny не обходиться exception.


9. Exception не є final accept.


10. Exception bypasses лише один deny stage.


11. Exception predicate є proven subset target rule.


12. Exception не intersect-ить інші rules target stage.


13. Expired exception не застосовується до нового desired policy.


14. Expiration не запускає deployment.


15. Address objects не використовують DNS/FQDN.


16. Address sets нормалізуються у disjoint intervals.


17. Service terms мають коректний protocol.


18. Zone bindings resolve окремо для кожного VRRP member.


19. Dynamic zone membership блокує deployment readiness.


20. Managed matchers повністю типізовані.


21. Unsupported matchers не можуть бути managed.


22. Rule satisfiability перевіряється.


23. Exact duplicates визначаються.


24. Conflicting duplicates блокуються.


25. Fully shadowed enabled rule блокуються.


26. Partial shadowing має witness.


27. Predicate analysis bounded.


28. Actual pre-anchor context аналізується.


29. Unknown pre-anchor effect блокує approval.


30. RouterOS default accept не використовується як managed disposition.


31. Management guard перевіряється для кожного Device.


32. VRRP protocol flows перевіряються.


33. VRRP connection synchronization перевіряється.


34. Split-master не спрощується до одного master.


35. Multi-WAN аналізує всі uplinks.


36. Strict rp-filter із routing tables/VRRP блокується.


37. RAW notrack враховується.


38. NAT dependency входить до context hash.


39. Mangle/PCC dependency входить до analysis.


40. FastTrack представлений як FASTTRACK_ACCEPT.


41. FastTrack compiler contract вимагає fallback accept.


42. FastTrack блокується на unsafe multi-WAN.


43. Switch FORWARD policy у v1 заборонена.


44. System tests не можна disable.


45. Semantic diff виконує Controller.


46. Approval прив’язаний до exact analysis bundle hash.


47. Warning acknowledgment анулюється після зміни hash.


48. Active unreachable Node не виключається мовчки.


49. High/Critical зміни мають separation of duties.


50. Build і tests не змінюють Git working tree.




---

76. Уточнення попередніх специфікацій

Попереднє рішення	Нормативне уточнення

Company baseline визначає довільні sections	Замінено fixed Policy Pipeline v1
Exception rule використовує ACCEPT	Замінено EXEMPT_DENY_STAGE
Exception може target кілька rules	Один exception rule target-ить одну deny rule
order_key	Замінено contiguous ordinal
Direct FASTTRACK_CONNECTION action	Замінено FASTTRACK_ACCEPT
Address objects можуть містити general address values	Managed v1 дозволяє лише static IP host/prefix/range
ZoneBinding як частина read-only topology	Тепер окремий desired Node binding
Chain default implicit	Вимагається explicit DROP, REJECT або RETURN_TO_UNMANAGED
Static analysis лише composed policy	Додано actual guard/anchor/unmanaged context
VRRP safety лише protocol 112	Додано connection-tracking synchronization flows
Multi-WAN validation загальна	Додано RAW/NAT/Mangle/RPF dependency analysis



---

77. Результат етапу

Після реалізації специфікації Controller матиме:

immutable policy revisions
        ↓
fixed hierarchical pipeline
        ↓
company/site/node composition
        ↓
stage-scoped temporary exceptions
        ↓
resolved address/service/zone objects
        ↓
node- and device-specific policy model
        ↓
bounded symbolic packet-space analysis
        ↓
actual RouterOS chain-context analysis
        ↓
management, VRRP і multi-WAN safety tests
        ↓
deterministic approval bundle

Наступний нормативний документ:

MikroTik Firewall Controller
Policy Compiler and Managed Chain Layout Specification v0.1

Він має визначити:

RouterOS chain namespace
root і deny-stage chain layout
anchor contract
management guard boundary
policy rule expansion
service-term expansion
zone expansion
content-addressed address lists
FASTTRACK_ACCEPT compilation
EXEMPT_DENY_STAGE compilation
explicit terminal rules
RouterOS comment markers
artifact canonical format
artifact hash
idempotent staging model
resource limits
compiler test vectors
standalone, multi-WAN і VRRP compilation invariants