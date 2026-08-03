MikroTik Firewall Controller

Policy Compiler and Managed Chain Layout Specification v0.1

Дата: 3 серпня 2026 року
Статус: нормативна специфікація M3 — Policy Compiler


---

1. Призначення

Compiler перетворює вже:

скомпоновану;

проаналізовану;

затверджену;

прив’язану до конкретного Device


firewall-політику у детермінований RouterOS filter artifact.

Approved effective policy
        ↓
Device zone resolution
        ↓
Policy compiler
        ↓
Immutable RouterOS filter artifact

Compiler не підключається до RouterOS і не виконує deployment.


---

2. Межі етапу

Compiler формує лише:

/ip firewall filter rules
/ipv6 firewall filter rules
/ip firewall address-list entries
/ipv6 firewall address-list entries
desired anchor targets

Compiler не формує:

NAT
RAW
Mangle
routing
VRRP
bridge
VLAN
interface lists
switch ACL
management guard
watchdog
scripts
scheduler
deployment commands
rollback commands

Не створюються:

універсальний IR;

plugin architecture;

multi-vendor abstraction;

rule optimizer;

generic RouterOS command builder;

окремий expression language;

runtime compiler plugins.


Compiler є звичайною детермінованою pure function.


---

3. RouterOS execution model

RouterOS має predefined chains input, forward та output, але підтримує custom chains. Rules обробляються зверху вниз. jump передає керування custom chain, а return повертає його до chain, яка виконала jump. Через те, що незбіг з усіма rules може завершитися прийняттям packet, кожна controller-owned chain повинна мати явний terminal rule. 

RouterOS дозволяє лише один src-address-list та один dst-address-list matcher в одній rule. Тому довільний AddressSelector не компілюється у декілька list matchers — Controller попередньо формує один результуючий content-addressed list. RouterOS також підтримує окремі interface та interface-list matchers і приймає protocol за ім’ям або numeric protocol ID. 

Для FastTrack типовий RouterOS ruleset використовує суміжні fasttrack-connection та accept rules для одного predicate. Compiler повинен завжди генерувати цю пару як єдиний logical effect. 


---

4. Compiler input

PolicyCompilerInput {
    device_id: DeviceId

    logical_effective_policy_hash: Hash256
    device_resolved_policy_hash: Hash256
    analysis_bundle_hash: Hash256

    capability_hash: Hash256
    compiler_profile_hash: Hash256

    effective_policy: EffectivePolicy
    resolved_zones: ResolvedZoneSet
    device_capabilities: FilterCapabilities

    management_guard_context_hash: Hash256
    anchor_context_hash: Hash256
}

Compiler запускається лише коли:

analysis result = PASS
analysis context актуальний
capability profile підтримується
zone bindings повністю resolved
management guard valid
anchors valid

Draft або непроаналізована policy не компілюється.


---

5. Compiler output

PolicyCompilerResult {
    artifact: RouterOsFilterArtifact
    provenance: CompilationProvenance
}

CompilationProvenance {
    device_id
    logical_effective_policy_hash
    device_resolved_policy_hash
    analysis_bundle_hash
    capability_hash
    compiler_profile_hash
    compiler_version
    compiled_at
}

compiled_at не входить до artifact hash.


---

6. Artifact model

RouterOsFilterArtifact {
    schema_version
    layout_version
    artifact_id

    address_lists[]
    chains[]
    anchor_targets[]

    resource_hash
}

AddressListArtifact {
    family
    name
    content_hash
    entries[]
}

ChainArtifact {
    family
    built_in_context
    name
    role
    rules[]
}

FilterRuleArtifact {
    ordinal
    logical_rule_id?
    variant_index?
    structural_role?
    matchers
    action
    action_parameters
    log
    log_prefix?
    comment
}

AnchorTarget {
    family
    built_in_chain
    expected_anchor_comment
    desired_jump_target
}

Artifact не містить:

RouterOS .id;

API commands;

add, set, move або remove;

current Device state;

credentials;

operator descriptions;

usernames;

ticket references.



---

7. Artifact identity

7.1. Physical semantics hash

Compiler спочатку формує canonical physical semantics:

rule UUIDs
resolved predicates
resolved zones
actions
logging
chain contracts
compiler profile
layout version

Не включаються:

policy description
rule description
test descriptions
review metadata
ticket references
timestamps

physical_semantics_hash =
SHA256(canonical physical semantics)

7.2. Artifact ID

artifact_seed =
SHA256(
    "mfc.filter.compiler.v1"
    + compiler_profile_hash
    + physical_semantics_hash
    + device_id
)

artifact_id =
first 16 lowercase hexadecimal characters of artifact_seed

artifact_id використовується лише у RouterOS resource names.

7.3. Resource hash

Після побудови всіх physical resources:

resource_hash =
SHA256(MFC-CJ1 canonical resource document)

Зміна лише Controller-side description не змінює physical artifact.


---

8. Namespace RouterOS

8.1. Family codes

IPv4 → mfc4
IPv6 → mfc6

8.2. Chain codes

input   → i
forward → f
output  → o

8.3. Chain names

Root:

mfc4.i.r.<artifact-id>
mfc4.f.r.<artifact-id>
mfc4.o.r.<artifact-id>

mfc6.i.r.<artifact-id>
mfc6.f.r.<artifact-id>
mfc6.o.r.<artifact-id>

Deny stages:

mfc4.i.dc.<artifact-id>   company deny
mfc4.i.ds.<artifact-id>   site deny
mfc4.i.dn.<artifact-id>   node deny

Та сама схема застосовується до інших family/chain.

8.4. Address lists

mfc4.a.<list-id>
mfc6.a.<list-id>

list_id =
first 16 hex characters of full content hash

Усі згенеровані імена:

lowercase;

ASCII;

детерміновані;

не містять display names;

не містять site або device names.



---

9. Anchor contract

Для кожної enabled family/chain існує один постійний anchor у predefined RouterOS chain.

Приклад:

chain=input
action=jump
jump-target=mfc4.i.r.<active-artifact-id>
comment=mfc:anchor:v1:4:i

Anchor markers:

mfc:anchor:v1:4:i
mfc:anchor:v1:4:f
mfc:anchor:v1:4:o

mfc:anchor:v1:6:i
mfc:anchor:v1:6:f
mfc:anchor:v1:6:o

Compiler:

не створює anchor;

не визначає його physical position;

не переміщує anchor;

не змінює management guard;

лише вказує потрібний jump-target.


Anchor bootstrap і перемикання target належать deployment engine.


---

10. Management guard boundary

Management guard розташований до anchor і не є частиною artifact.

built-in input
    ├── management guard
    ├── unmanaged pre-anchor rules
    ├── MFC anchor
    └── unmanaged post-anchor rules

Compiler не має права:

дублювати management guard;

генерувати API-SSL allow замість guard;

змінювати source prefixes guard;

змінювати position guard;

включати guard у root chain.


Artifact зберігає лише:

management_guard_context_hash
anchor_context_hash

Вони використовуються як deployment preconditions, але не входять до RouterOS resources.


---

11. Мінімальний chain layout

Для кожної family/chain формується:

одна root chain;

до трьох deny-stage chains.


Інших compiler-generated chains немає.

11.1. Root chain

ROOT
 ├── PROTECTED_CONTROL_PLANE
 ├── MANDATORY_PRE_STATE_DENY
 ├── STATE_PRELUDE
 ├── jump COMPANY_DENY, якщо stage не порожній
 ├── jump SITE_DENY, якщо stage не порожній
 ├── jump NODE_DENY, якщо stage не порожній
 ├── COMPANY_ALLOW
 ├── SITE_ALLOW
 ├── NODE_ALLOW
 └── explicit default disposition

11.2. Deny chain

DENY_STAGE
 ├── exception variants → return
 ├── deny rule variants → drop/reject
 └── unconditional return

Явний завершальний return обов’язковий.

11.3. Порожній stage

Якщо stage не має deny rules:

chain не створюється;

jump у root chain не створюється.


Exception без target deny rule є validation error і до compiler не доходить.


---

12. Root chain example

mfc4.f.r.a1b2c3d4e5f60708

0  protected control-plane rule
1  mandatory deny rule
2  fasttrack rule
3  fallback accept rule
4  jump → mfc4.f.dc.a1b2c3d4e5f60708
5  jump → mfc4.f.ds.a1b2c3d4e5f60708
6  company allow rule
7  site allow rule
8  node allow rule
9  default drop

Наявність або відсутність Node deny chain не змінює порядок інших stage classes.


---

13. Rule compilation

Для кожної logical rule:

1. Resolve address selectors.
2. Resolve zone selectors.
3. Flatten service objects.
4. Побудувати physical variants.
5. Перевірити variant limit.
6. Map matchers.
7. Map effect.
8. Додати ownership comment.
9. Вставити variants безпосередньо за logical ordinal.

Compiler не:

переставляє logical rules;

об’єднує сусідні rules;

видаляє duplicates;

змінює policy precedence;

виконує performance optimization.


Усі semantic проблеми повинні бути усунені static analyzer до compilation.


---

14. Physical variant ordering

Variant key:

service_atom_index
ingress_interface_index
egress_interface_index
icmp_selector_index

Variants сортуються лексикографічно за цим key.

variant_index: 0..N-1

Всі variants однієї logical rule розміщуються послідовно до переходу до наступної logical rule.


---

15. Matcher mapping

Policy matcher	RouterOS matcher

Source address selector	src-address-list
Destination address selector	dst-address-list
Ingress interface	in-interface
Egress interface	out-interface
Ingress interface list	in-interface-list
Egress interface list	out-interface-list
Protocol	protocol
Source ports	src-port
Destination ports	dst-port
ICMP type/code	icmp-options
Connection states	connection-state
Connection NAT states	connection-nat-state
Source address types	src-address-type
Destination address types	dst-address-type
TCP flags	tcp-flags
IPsec policy	ipsec-policy


Compiler profile визначає exact RouterOS token для:

protocol;

address type;

ICMP/ICMPv6;

reject mode;

connection state;

IPsec policy.


Непідтриманий token є compile error, а не fallback.


---

16. Address selector compilation

16.1. Universe

include = empty
exclude = empty

RouterOS address matcher не генерується.

16.2. Positive set

include != empty

Compiler:

1. об’єднує include objects;


2. віднімає exclude objects;


3. отримує disjoint canonical set;


4. створює один content-addressed list;


5. використовує positive list matcher.



src-address-list=mfc4.a.<list-id>

16.3. Universe minus exclusions

include = empty
exclude != empty

Compiler створює list із виключеними addresses та використовує negated matcher:

src-address-list=!mfc4.a.<list-id>

16.4. Порожній результат

ADDRESS_SELECTOR_EMPTY

Compilation припиняється.


---

17. Address-list artifacts

AddressListArtifact {
    family
    name
    content_hash
    entries
}

Entry:

address
disabled = false
comment = mfc:a:<list-id>

Вимоги:

1. Entries статичні.


2. Timeout не використовується.


3. Entries відсортовані.


4. Duplicates відсутні.


5. List immutable.


6. Однаковий content використовує одне й те саме list name.


7. Different objects із однаковим resolved content використовують один list.


8. Existing unmanaged list із таким ім’ям є collision.


9. Existing MFC list із іншим content є collision.


10. Compiler не створює object-specific copies без потреби.




---

18. Zone compilation

18.1. Direct interface-list use

Коли selector містить:

рівно одну included zone;

жодної excluded zone;

binding типу INTERFACE_LIST;


compiler використовує:

in-interface-list=<RouterOS-list>

або:

out-interface-list=<RouterOS-list>

Це не створює новий interface list.

18.2. Finite interface expansion

В інших випадках selector resolve-иться у точний finite set interface names.

Для кожної interface створюється окремий physical variant:

in-interface=ether1
in-interface=ether2

Якщо одночасно розширюються ingress та egress sets, створюється їх декартів добуток.

18.3. Dependency

Artifact залежить від:

zone binding hash
resolved interface set hash
device interface configuration hash

Будь-яка зміна interface або interface-list membership анулює artifact readiness.

18.4. Заборони

Compilation блокується, коли:

zone не resolved
zone порожня
interface відсутній
dynamic interface використовується у security zone
expansion перевищує limit


---

19. Service compilation

Service objects перетворюються на унікальний ordered set:

ServiceAtom

ServiceAtom {
    protocol
    source_ports?
    destination_ports?
    icmp_selector?
}

19.1. No service selector

Compiler не додає protocol або port matcher.

19.2. TCP/UDP term

Один term компілюється в одну physical rule, якщо його port representation не перевищує profile limit.

protocol=tcp
src-port=1024-65535
dst-port=443,8443

19.3. ICMP

Кожний ICMP type/code selector створює окремий physical variant.

19.4. Multiple terms

Union service terms реалізується декількома physical rules з однаковим effect.

19.5. Заборони

Compiler не:

створює custom service chains;

створює port address lists;

виконує protocol guessing;

додає protocol за наявністю ports;

розбиває oversized port list автоматично.


Oversized term повертає:

SERVICE_TERM_TOO_LARGE


---

20. Effect mapping

Policy effect	RouterOS output

ACCEPT	action=accept
DROP	action=drop
REJECT	action=reject + reject-with
FASTTRACK_ACCEPT	adjacent fasttrack + accept pair
EXEMPT_DENY_STAGE	action=return


20.1. REJECT

Mapping виконує exact compiler profile:

TCP_RESET
ADMIN_PROHIBITED
PORT_UNREACHABLE

За відсутності перевіреного mapping:

REJECT_MODE_UNSUPPORTED

Compiler не замінює REJECT на DROP.

20.2. EXEMPT_DENY_STAGE

Exception variant розташовується на початку відповідної deny chain:

predicate
action=return

Це пропускає лише поточний deny stage і повертає packet у root pipeline після stage jump.


---

21. FASTTRACK_ACCEPT compilation

Один logical variant створює рівно дві фізичні rules:

1. same predicate
   action=fasttrack-connection
   hw-offload=no

2. same predicate
   action=accept

hw-offload=no є обов’язковим у compiler v1. Hardware FastTrack offload не входить до поточного scope.

Вимоги:

family IPv4;

chain FORWARD;

stage STATE_PRELUDE;

analysis підтвердив безпечність;

predicate обмежений supported connection states;

обидві rules суміжні;

accept rule не може бути опущена;

logging для FASTTRACK_ACCEPT заборонене.


Errors:

FASTTRACK_CONTEXT_UNSUPPORTED
FASTTRACK_LOGGING_UNSUPPORTED
FASTTRACK_CAPABILITY_UNSUPPORTED


---

22. Default disposition

Root chain завжди завершується однією explicit rule.

DROP

action=drop

REJECT

action=reject
reject-with=<profile mapping>

RETURN_TO_UNMANAGED

action=return

RETURN_TO_UNMANAGED повертає керування predefined chain після anchor.

Compiler не допускає:

відсутній terminal rule
default accept
implicit custom-chain fallthrough


---

23. Ownership comments

23.1. Logical rule

mfc:r:<rule-uuid>:<variant-index>

FastTrack pair:

mfc:r:<rule-uuid>:<variant-index>:ft
mfc:r:<rule-uuid>:<variant-index>:ac

Exception:

mfc:r:<rule-uuid>:<variant-index>:ex

23.2. Structural rules

mfc:s:jump:company-deny
mfc:s:jump:site-deny
mfc:s:jump:node-deny

mfc:s:return:company-deny
mfc:s:return:site-deny
mfc:s:return:node-deny

mfc:s:terminal

23.3. Address-list entry

mfc:a:<list-id>

Generated comments не містять:

human description;

username;

site name;

ticket;

reason;

IP address;

secret.


Повна metadata зберігається лише у Controller database.


---

24. Canonical artifact format

Artifact resources кодуються через MFC-CJ1.

{
  "schema": "mfc.routeros-filter-artifact/1",
  "layoutVersion": "1",
  "artifactId": "a1b2c3d4e5f60708",
  "addressLists": [],
  "chains": [],
  "anchors": []
}

Нормативний порядок:

schema
layoutVersion
artifactId
addressLists
chains
anchors

Address lists сортуються:

family
name

Chains сортуються:

family
built-in context
role
name

Rules зберігаються за physical ordinal.


---

25. Determinism

За однакового input compiler повинен створювати:

ідентичний artifact ID
ідентичні resource names
ідентичні address-list entries
ідентичний chain layout
ідентичний rule order
ідентичні comments
ідентичні canonical bytes
ідентичний resource hash

Заборонені джерела nondeterminism:

current time;

random UUID;

database row order;

dictionary iteration order;

RouterOS .id;

current VRRP role;

current active WAN;

thread execution order;

locale;

display names.



---

26. Immutable staging contract

Compiler не реалізує staging, але artifact повинен бути придатний для create-or-verify semantics.

Вимоги до майбутнього deployment engine:

1. Address list із певним name ніколи не редагується in-place.


2. Revision chain не редагується після повного створення.


3. Existing identical resource повторно не створюється.


4. Existing resource з відмінним content створює collision.


5. Active chain не використовується як staging target.


6. Root chain стає active лише після перемикання anchor.


7. Старий artifact залишається незмінним для rollback.



Compiler не генерує RouterOS mutation sequence для цих операцій.


---

27. Resource limits

Ресурс	Limit

Physical variants однієї logical rule	256
Physical filter rules однієї family/chain	20 000
Address lists одного artifact	4 096
Address-list entries однієї family	250 000
Interface variants одного selector	64
Service atoms однієї rule	128
Custom chains одного Device	24
Encoded port matcher	1 024 bytes
Generated comment	128 ASCII bytes


Перевищення limit є compile error.

Compiler не підвищує limit автоматично.


---

28. Compiler errors

COMPILER_INPUT_NOT_APPROVED
COMPILER_ANALYSIS_STALE
COMPILER_CAPABILITY_STALE
COMPILER_PROFILE_UNSUPPORTED

ADDRESS_SELECTOR_EMPTY
ADDRESS_LIST_LIMIT_EXCEEDED
ADDRESS_ENTRY_LIMIT_EXCEEDED

ZONE_NOT_RESOLVED
ZONE_EMPTY
ZONE_INTERFACE_MISSING
ZONE_DYNAMIC_INTERFACE
ZONE_EXPANSION_LIMIT

SERVICE_TERM_TOO_LARGE
RULE_VARIANT_LIMIT
FILTER_RULE_LIMIT

REJECT_MODE_UNSUPPORTED
FASTTRACK_CONTEXT_UNSUPPORTED
FASTTRACK_LOGGING_UNSUPPORTED
FASTTRACK_CAPABILITY_UNSUPPORTED

RESOURCE_NAME_COLLISION
DUPLICATE_GENERATED_RULE
ARTIFACT_SIZE_LIMIT

Compiler не повертає partial artifact.


---

29. Standalone router invariants

Для Node.kind=ROUTER:

artifact створюється для одного Device;

усі enabled family/chain компілюються;

zone bindings resolve для цього Device;

management guard залишається поза artifact;

multi-WAN mode не змінює chain layout.



---

30. Multi-WAN invariants

Для FAILOVER, BALANCED і MIXED:

logical policy не компілюється окремо для current active WAN;

усі WAN zones входять у resolved bindings;

inactive backup interface не видаляється з artifact;

current route state не впливає на artifact hash;

route, NAT і Mangle configuration hashes входять у analysis context, але не в artifact;

compiler не створює routing marks;

compiler не створює NAT rules;

compiler не перемикає WAN.



---

31. VRRP invariants

Для Node.kind=VRRP:

1. Logical effective policy однакова для всіх members.


2. Compiler запускається окремо для кожного Device.


3. Physical interface names можуть відрізнятися.


4. Address objects і service semantics залишаються однаковими.


5. Device artifacts можуть мати різні hashes через zone resolution.


6. Поточна role MASTER/BACKUP не впливає на compilation.


7. Усі members повинні мати artifact до початку deployment.


8. Один member не може бути пропущений.


9. Split-master не змінює layout.


10. VRRP protocol rules компілюються як звичайні protected control-plane rules.




---

32. Switch invariants

Для Node.kind=SWITCH:

INPUT  — дозволено
OUTPUT — дозволено
FORWARD — заборонено

Compiler не:

створює bridge filter;

створює switch ACL;

змінює use-ip-firewall;

вимикає hardware offload;

оголошує transit traffic захищеним IP firewall.


Спроба compile FORWARD:

SWITCH_FORWARD_COMPILATION_FORBIDDEN


---

33. Мінімальні compiler tests

33.1. Simple INPUT allow

Policy:

IPv4 INPUT
source = MGMT
service = TCP/8729
effect = ACCEPT

Expected:

one address list, якщо MGMT має address selector
one root chain
one accept rule
one explicit terminal rule
one desired anchor target

33.2. Company deny with exception

Expected company deny chain:

exception variant → return
target deny → drop
unconditional return

Root:

jump company deny
continue allow stages
terminal

33.3. FastTrack

Expected:

fasttrack-connection
accept

Rules мають:

однакові matchers;

суміжні ordinals;

один logical rule UUID;

різні suffixes ft та ac.


33.4. VRRP members

Одна logical policy, але:

Device A zone MGMT → ether5
Device B zone MGMT → bridge-mgmt

Expected:

same logical effective hash
different device-resolved hashes
different resource hashes
same rule UUIDs

33.5. Multi-WAN

Zone WAN resolve-иться у:

WAN_PRIMARY
WAN_BACKUP

Expected:

обидва interface variants присутні
current active route не впливає на output


---

34. Test invariants

Compile(input) == Compile(input)

Description-only change:
    same physical artifact

Rule ordinal change:
    different artifact

Rule UUID change:
    different artifact

Current VRRP role change:
    same artifact

Current active WAN change:
    same artifact

Interface-list membership change:
    different device-resolved artifact

Same address set from different objects:
    same address-list artifact

FASTTRACK_ACCEPT:
    always exactly two adjacent rules

Every root chain:
    exactly one terminal rule

Every deny chain:
    exactly one final unconditional return


---

35. Acceptance criteria

Специфікація реалізована лише коли:

1. Compiler є pure function.


2. Compiler не має RouterOS transport dependency.


3. Compiler не формує API commands.


4. Відсутній generic intermediate representation.


5. Відсутній optimizer.


6. Відсутня multi-vendor abstraction.


7. Використовується одна root chain на family/chain.


8. Використовується не більше трьох deny chains на root.


9. Exception компілюється як return.


10. Deny chain має explicit final return.


11. Root chain має explicit terminal rule.


12. Default ACCEPT неможливий.


13. Management guard не входить в artifact.


14. Anchor не створюється compiler.


15. Artifact визначає лише desired anchor target.


16. Address selector використовує не більше одного source і destination list matcher.


17. Address lists content-addressed.


18. Address lists immutable.


19. Interface lists не створюються.


20. Explicit interface sets розгортаються bounded variants.


21. Service union розгортається bounded variants.


22. Unsupported matcher не компілюється.


23. REJECT не замінюється DROP.


24. FASTTRACK_ACCEPT завжди створює pair.


25. FastTrack hardware offload вимкнений у compiler v1.


26. Current WAN state не впливає на artifact.


27. Current VRRP role не впливає на artifact.


28. Усі VRRP members компілюються окремо.


29. Switch FORWARD не компілюється.


30. Description-only change не створює новий physical artifact.


31. Compiler output детермінований.


32. Resource collisions виявляються.


33. Partial artifact не повертається.


34. Усі limits bounded.


35. Build і tests не змінюють Git working tree.




---

36. Уточнення попередньої специфікації

Попереднє рішення	Уточнення

Policy Pipeline має 13 stages	Physical layout використовує root + максимум 3 deny chains
Exception є окремим stage	Фізично exception є return на початку deny chain
FASTTRACK_ACCEPT	Компілюється у fasttrack-connection + accept
Hardware FastTrack	Вимкнений у compiler v1
Address selectors	Компілюються в один content-addressed list matcher
Zone selectors	Використовують existing interface list або bounded interface expansion
Policy descriptions	Не передаються в RouterOS
Artifact hash	Обчислюється лише над physical resources
Anchor	Не є частиною staged artifact
Management guard	Повністю ізольований від compiler



---

37. Результат етапу

Після реалізації цього етапу система матиме простий і достатній pipeline:

approved policy
    ↓
device resolution
    ↓
deterministic compiler
    ↓
immutable address lists
    ↓
detached root/deny chains
    ↓
desired anchor target

Наступний нормативний документ:

MikroTik Firewall Controller
Safe Deployment and Rollback Specification v0.1

Він визначатиме лише необхідний write-path:

typed RouterOS writes
deployment preconditions
create-or-verify staging
anchor activation
verification
rollback watchdog
standalone deployment
multi-WAN verification
VRRP coordination
crash recovery