MikroTik Firewall Controller

Managed Device Onboarding and Anchor Bootstrap Specification v0.1

Дата: 3 серпня 2026 року
Статус: нормативна специфікація M5 — Managed Device Onboarding


---

1. Призначення

Onboarding переводить уже зареєстрований і перевірений RouterOS Node зі стану:

UNMANAGED

у стан:

MANAGED

Для цього Controller створює тільки мінімальний постійний контур, необхідний Safe Deployment:

pass-through bootstrap root chains
        ↓
permanent disabled anchors
        ↓
onboarding rollback watchdog
        ↓
enable anchors
        ↓
перевірка відсутності зміни firewall semantics
        ↓
MANAGED

Після onboarding активні anchors спрямовані на початкові root chains, які безумовно виконують return. Вони не змінюють verdict пакетів, а лише повертають обробку до built-in chain після anchor.

У RouterOS custom chains викликаються через jump, а return повертає керування до chain, з якої виконано перехід. Rules обробляються згори вниз; якщо пакет не збігся з жодною rule, RouterOS приймає його, тому початкові root chains повинні містити явний return, а не бути порожніми. 


---

2. Межі onboarding

Controller під час onboarding може змінювати лише:

/ip firewall filter
/ipv6 firewall filter
/system script
/system scheduler

Дозволені зміни:

1. Створення bootstrap root chains.


2. Створення permanent anchors у disabled-стані.


3. Створення тимчасового onboarding watchdog.


4. Увімкнення permanent anchors.


5. Вимкнення та видалення watchdog.


6. Видалення власних bootstrap-ресурсів при rollback.



Controller не змінює:

RouterOS users;
RouterOS user groups;
credentials;
api-ssl service;
certificates;
management guard;
device-mode;
routing;
NAT;
RAW;
Mangle;
VRRP;
interfaces;
interface lists;
bridge;
VLAN;
switch-chip configuration;
RouterOS packages.

Ці prerequisites налаштовуються адміністратором поза onboarding write-path і лише перевіряються Controller.


---

3. Поза scope

У цій версії не реалізуються:

автоматичне створення RouterOS service accounts;

автоматичне налаштування api-ssl;

автоматичне встановлення сертифіката;

автоматична зміна device-mode;

автоматичне створення management guard;

імпорт або adoption довільних існуючих MFC-ресурсів;

автоматичне переміщення існуючих firewall rules;

зміна unmanaged firewall rules;

автоматичне виправлення конфліктів namespace;

onboarding через VRRP virtual address;

RouterOS REST або SSH fallback;

multi-vendor onboarding;

bulk onboarding багатьох незалежних Nodes;

discovery мережі скануванням.


Onboarding запускається для одного явно визначеного Node.


---

4. Спрощена модель станів

4.1. Стан Node

UNMANAGED
MANAGED
RECOVERY_REQUIRED

Проміжні стани зберігаються у OnboardingOperation, а не в основній моделі Node.

4.2. Стан Device

UNMANAGED
MANAGED
RECOVERY_REQUIRED

Node може бути MANAGED лише тоді, коли всі його активні Devices мають стан MANAGED.


---

5. OnboardingOperation

OnboardingOperation {
    id: OnboardingOperationId
    node_id: NodeId
    plan_id: OnboardingPlanId

    state:
        CREATED |
        PRECHECKING |
        STAGING_BOOTSTRAP_ROOTS |
        STAGING_DISABLED_ANCHORS |
        ARMING_WATCHDOGS |
        ENABLING_ANCHORS |
        VERIFYING |
        DISARMING_WATCHDOGS |
        COMMITTED |
        ROLLBACK_PENDING |
        ROLLING_BACK |
        ROLLED_BACK |
        BLOCKED |
        RECOVERY_REQUIRED

    created_by: UserId
    started_at: UTC?
    completed_at: UTC?
    error_code: string?
}

Довільний запис стану заборонений. Дозволені переходи визначаються однією state machine.


---

6. Одиниця onboarding

Onboarding виконується на рівні Node.

Node kind	Devices в одній операції

ROUTER	1
VRRP	Усі active members
SWITCH	1


Для VRRP заборонено onboard лише один фізичний router.

Якщо onboarding хоча б одного Device не завершився, весь Node rollback-иться до UNMANAGED.


---

7. RouterOS prerequisite profile

Кожний Device повинен пройти:

RouterOsOnboardingPrerequisites {
    supported_build
    api_ssl
    read_account
    deployment_account
    management_guard
    scheduler_capability
    device_mode
    namespace_cleanliness
    anchor_placement
}

Відсутність будь-якого prerequisite блокує створення executable onboarding plan.


---

8. RouterOS build prerequisites

Обов’язково:

1. RouterOS major version 7.


2. Exact build присутній у compatibility manifest.


3. Release channel дозволений production policy.


4. Architecture підтримується.


5. IPv4 filter menu доступне.


6. IPv6 filter menu доступне, якщо Node використовує IPv6.


7. Scheduler profile протестований для цього build.


8. capability_hash актуальний.


9. compatibility_hash не містить blocking findings.


10. RouterOS version однакова для members одного VRRP group або має explicit approved compatibility record.



Невідомий RouterOS build не отримує onboarding write support.


---

9. API-SSL prerequisites

Обов’язково:

api service:
    disabled = yes

api-ssl service:
    disabled = no
    certificate != none
    valid trusted certificate
    expected port
    expected VRF
    max-sessions >= 2

Controller повинен перевірити:

certificate chain або SPKI pin;

SAN із management hostname/IP;

validity period;

API authentication;

service port;

service VRF;

source restrictions.


RouterOS API-SSL використовує TCP 8729. Без призначеного сертифіката він може працювати через anonymous Diffie–Hellman, тому такий стан для Controller заборонений. 

Поле /ip service address обмежує доступ на рівні сервісу, але не відкидає пакет мережевим firewall. MikroTik рекомендує додатково використовувати firewall для недовірених джерел. 

Controller не змінює /ip service під час onboarding.


---

10. RouterOS service accounts

Потрібні два окремі локальні service accounts:

read account
deployment account

Controller не створює їх і не змінює їх passwords.


---

10.1. Read account

Custom group повинна мати точний набір policies:

api
read

Заборонені зайві login policies:

local
telnet
ssh
ftp
winbox
web
rest-api
romon

Заборонені config policies:

write
policy
test
reboot
password
sniff
sensitive


---

10.2. Deployment account

Custom group повинна мати:

api
read
write
test

test потрібна лише для bounded RouterOS /ping probes. Production writer не отримує generic доступ до інших test-команд.

Заборонені:

local
telnet
ssh
ftp
winbox
web
rest-api
romon
reboot
policy
password
sniff
sensitive

RouterOS write є широким дозволом зміни конфігурації, а test охоплює не лише ping, а й інші diagnostic operations. Тому реальне обмеження deployment account забезпечується одночасно custom group, source-address restriction, API-only login і закритим command allowlist Controller. 

Default RouterOS groups не приймаються: навіть default read містить reboot, test, sniff, sensitive, WinBox, Web і REST API. MikroTik прямо рекомендує створювати custom groups для реально обмежених облікових записів. 


---

10.3. User source restriction

Обидва service accounts повинні мати:

address = Controller source prefix або prefixes

Фактична адреса API session повинна входити до configured user address set.

RouterOS підтримує обмеження адрес, з яких конкретному користувачу дозволено входити. 


---

10.4. Credential checks

Controller перевіряє окремо:

read account:
    login
    required reads
    відсутність write capability probe

deployment account:
    login
    required reads
    scheduler capability
    bootstrap write capability

Controller не виконує небезпечний тест довільного write access. Перевіряються лише MFC-owned test resources.


---

11. Device-mode prerequisites

Обов’язково:

scheduler = yes
flagged = no

Якщо scheduler=no, Controller не намагається змінити device-mode.

Зміна device-mode вимагає фізичного підтвердження кнопкою або power cycle і призводить до reboot. У flagged-стані RouterOS забороняє створення або активацію нових scheduler entries. 

Finding:

DEVICE_MODE_SCHEDULER_DISABLED
DEVICE_FLAGGED

обидва мають severity:

BLOCKER


---

12. Scheduler execution proof

Перевірки лише scheduler=yes недостатньо. До створення onboarding plan Controller повинен довести фактичне виконання one-shot scheduler.


---

12.1. Test resources

script:
    mfc-cap-s-<token>

scheduler:
    mfc-cap-d-<token>

Script source є фіксованим no-op:

:local mfcCapabilityProbe true;

Script:

policy=read,write
dont-require-permissions=no

Scheduler:

policy=read,write
interval=0
start-time=RouterOS clock + 5 seconds
on-event=<script-name>


---

12.2. Proof algorithm

1. Перевірити відсутність name collision.
2. Створити fixed no-op script.
3. Прочитати script і перевірити source hash.
4. Створити one-shot scheduler.
5. Очікувати не більше 15 секунд.
6. Перевірити scheduler run-count == 1.
7. Видалити scheduler.
8. Видалити script.
9. Перевірити відсутність обох resources.

RouterOS Scheduler із interval=0 виконується один раз у заданий момент, а run-count збільшується після запуску. 

dont-require-permissions=yes категорично заборонений, оскільки він дозволяє виконання script без достатніх прав caller і послаблює permission model RouterOS. 


---

13. Management guard

Management guard:

налаштовується адміністратором до onboarding;

не належить звичайній policy;

не створюється Controller;

не змінюється Controller;

обов’язково розміщується до permanent MFC anchors.


Guard забезпечує незалежний API-SSL path, який не залежить від майбутнього managed artifact.


---

14. GuardProfile

GuardProfile {
    id: GuardProfileId
    device_id: DeviceId

    family: IPv4 | IPv6
    controller_source_prefixes: IpPrefix[]
    management_destination: IpAddress
    api_ssl_port: uint16

    ingress_interface_set: string[]
    input_rule_markers: string[]
    output_rule_markers: string[]

    canonical_hash: Hash256
}

Для dual-stack management можуть існувати два profiles.


---

15. Guard markers

mfc:guard:v1:<profile-id>:4:i:<ordinal>
mfc:guard:v1:<profile-id>:4:o:<ordinal>

mfc:guard:v1:<profile-id>:6:i:<ordinal>
mfc:guard:v1:<profile-id>:6:o:<ordinal>

profile-id:

16 lowercase hexadecimal characters

Guard marker:

починається з першого символу comment;

унікальний у межах Device;

не містить display names;

не містить IP addresses;

не містить usernames.



---

16. Guard rule constraints

16.1. Input guard

Повинен дозволяти лише:

protocol = tcp
source ∈ controller source prefixes
destination = physical management address
destination port = api-ssl port
connection state ∈ new, established
action = accept

Додатково дозволений точний:

in-interface

або static-resolved:

in-interface-list

16.2. Output guard

Повинен дозволяти:

protocol = tcp
source = physical management address
source port = api-ssl port
destination ∈ controller source prefixes
connection state ∈ established, related
action = accept

16.3. Заборони

Guard v1 не може використовувати:

dynamic address list;
FQDN;
packet mark;
connection mark;
routing mark;
layer7;
content;
tls-host;
random;
nth;
PCC;
time matcher;
dynamic rule;
unknown matcher;
unknown jump chain.

Префікси:

0.0.0.0/0
::/0

заборонені.


---

17. Guard verification

Для кожного Device Controller доводить:

1. Усі guard rules існують.


2. Markers унікальні.


3. Rules enabled.


4. Rules static.


5. Rules valid.


6. Rules мають action=accept.


7. Predicate не ширший за GuardProfile.


8. Guard розташований до planned anchor.


9. Жодна unmanaged pre-guard rule не блокує path.


10. API service restriction дозволяє Controller source.


11. RouterOS user restriction дозволяє Controller source.


12. RAW notrack не робить management analysis indeterminate.


13. Output reply path доведений.


14. Нове API-SSL connection проходить guard.


15. Guard hash відповідає plan.



Guard із ширшим predicate не приймається як «достатній». Він створює:

MANAGEMENT_GUARD_TOO_BROAD


---

18. RequiredAnchorSet

18.1. Router і VRRP

Для кожної підтримуваної address family:

INPUT
FORWARD
OUTPUT

18.2. Switch

Для кожної підтримуваної address family:

INPUT
OUTPUT

FORWARD anchor для Node.kind=SWITCH не створюється.

18.3. Address family

IPv4 anchors обов’язкові.

IPv6 anchors створюються, коли:

IPv6 filter підтримується;

Device має IPv6 configuration;

або company policy явно керує IPv6.


Controller не створює IPv6 anchor на build із непідтримуваним IPv6 filter profile.


---

19. Permanent anchor contract

Anchor:

chain       = input | forward | output
action      = jump
jump-target = bootstrap root chain
disabled    = yes під час staging
comment     = permanent marker

Markers:

mfc:anchor:v1:4:i
mfc:anchor:v1:4:f
mfc:anchor:v1:4:o

mfc:anchor:v1:6:i
mfc:anchor:v1:6:f
mfc:anchor:v1:6:o

На одну family/chain дозволений рівно один permanent anchor.

Marker не містить onboarding operation ID, оскільки anchor залишається постійним після onboarding.


---

20. AnchorPlacement

AnchorPlacement {
    family: IPv4 | IPv6
    chain: INPUT | FORWARD | OUTPUT

    mode:
        BEFORE_STATIC_RULE |
        APPEND

    reference_rule_fingerprint: Hash256?
    reference_occurrence_rank: uint32?

    expected_predecessor_fingerprint: Hash256?
    expected_successor_fingerprint: Hash256?

    expected_anchor_ordinal: uint32
}

Не підтримуються:

AFTER_RULE
absolute RouterOS .id
dynamic-rule reference
automatic best-position selection

Щоб вставити anchor після existing rule:

вибирається наступна static rule як BEFORE_STATIC_RULE;

якщо наступної rule немає — використовується APPEND.



---

21. Anchor placement selection

Позицію anchor явно обирає оператор у GUI на основі актуального ordered snapshot.

Controller перевіряє:

1. Reference rule існує.


2. Reference rule static.


3. Fingerprint і occurrence rank збігаються.


4. Predecessor і successor не змінилися.


5. Guard precedes anchor.


6. Anchor не розташований перед guard.


7. Unmanaged pre-anchor rules повністю проаналізовані.


8. Managed path не перекривається попереднім unconditional terminal rule.


9. Anchor не розміщується всередині controller-невідомого jump context.


10. Позиція придатна для майбутнього managed policy.



Automatic placement заборонений.


---

22. Physical insertion

Anchor додається одразу у потрібну позицію через:

place-before=<current RouterOS item ID>

або append без place-before.

RouterOS place-before дозволяє створити новий item без подальшого move. 

Перед add Controller повторно читає поточний filter і отримує актуальний .id reference rule. API set та інші item-specific операції виконуються тільки після print, оскільки RouterOS API не приймає query безпосередньо в set. 

Команда move під час onboarding не використовується.


---

23. Bootstrap artifact

Onboarding використовує єдиний фіксований pass-through artifact.

bootstrap seed:
    "mfc.bootstrap-artifact.v1"

SHA-256:
    8e40b9d4d67d42d6ff7111669c7a5dea61e691b9155fb804c6e263053f7b702e

bootstrap artifact ID:
    8e40b9d4d67d42d6


---

23.1. Root chain names

mfc4.i.r.8e40b9d4d67d42d6
mfc4.f.r.8e40b9d4d67d42d6
mfc4.o.r.8e40b9d4d67d42d6

mfc6.i.r.8e40b9d4d67d42d6
mfc6.f.r.8e40b9d4d67d42d6
mfc6.o.r.8e40b9d4d67d42d6

Створюються лише roots, що входять до RequiredAnchorSet.


---

23.2. Root chain content

Кожна bootstrap root chain містить рівно одну rule:

chain   = <bootstrap-root-name>
action  = return
disabled = no
comment = mfc:s:bootstrap-return:v1

Заборонені:

matchers;
logging;
jump-target;
address-list action;
dynamic state.


---

23.3. Bootstrap semantics

Виклик:

built-in chain
    → jump bootstrap root
    → unconditional return
    → наступна rule built-in chain

не змінює packet verdict або порядок обробки existing rules після insertion point.

Bootstrap artifact:

не містить address lists;

не містить deny chains;

не містить policy rules;

не містить management guard;

не залежить від active WAN;

не залежить від current VRRP role.



---

24. Initial managed state

Після успішного onboarding:

active_artifact_kind = BOOTSTRAP
active_artifact_id   = 8e40b9d4d67d42d6

Для кожного anchor:

jump-target = відповідна bootstrap root chain
disabled    = no

Ці targets є old_anchor_targets для першого production policy deployment.


---

25. OnboardingPlan

OnboardingPlan {
    id: OnboardingPlanId
    node_id: NodeId

    node_membership_hash: Hash256
    topology_projection_hash: Hash256

    device_plans: DeviceOnboardingPlan[]

    created_by: UserId
    created_at: UTC
    expires_at: UTC

    plan_hash: Hash256
}

DeviceOnboardingPlan {
    device_id: DeviceId

    expected_routeros_version: string
    expected_capability_hash: Hash256
    expected_configuration_hash: Hash256
    expected_compatibility_hash: Hash256

    expected_api_service_hash: Hash256
    expected_read_account_hash: Hash256
    expected_deployment_account_hash: Hash256
    expected_device_mode_hash: Hash256
    expected_guard_hash: Hash256

    required_anchor_set: AnchorKey[]
    anchor_placements: AnchorPlacement[]

    bootstrap_artifact_hash: Hash256
    watchdog_ttl: Duration
}

Plan immutable.


---

26. Plan validity

Plan анулюється при зміні:

Node membership;
RouterOS version;
capability;
configuration snapshot;
compatibility state;
api-ssl service;
read account permissions;
deployment account permissions;
user source restrictions;
device-mode;
flagged state;
management guard;
firewall rule order;
anchor placement reference;
VRRP configuration;
interface-list configuration;
management route;
Controller source address.

Operational зміна current VRRP role або active WAN не анулює plan автоматично, але перевіряється перед commit.

Default plan lifetime:

30 хвилин


---

27. Onboarding writer allowlist

Onboarding write adapter має окремий закритий allowlist.

27.1. Filter

/ip/firewall/filter/add
/ipv6/firewall/filter/add

/ip/firewall/filter/set
/ipv6/firewall/filter/set

/ip/firewall/filter/remove
/ipv6/firewall/filter/remove

27.2. Script і scheduler

/system/script/add
/system/script/remove

/system/scheduler/add
/system/scheduler/set
/system/scheduler/remove


---

27.3. Filter operation constraints

add

Дозволено лише:

1. Bootstrap return rule.


2. Permanent anchor у disabled=yes.



set

Для permanent anchor дозволено лише:

disabled=yes
disabled=no

jump-target під час onboarding не змінюється.

remove

Дозволено лише:

disabled permanent anchor, створений поточною onboarding operation;

bootstrap return rule після видалення всіх references.


Звичайні firewall rules не видаляються.


---

28. Precheck

Перед першим write Controller повторно доводить:

1. Plan не expired.


2. Plan hash точний.


3. Node досі UNMANAGED.


4. У Node немає іншої onboarding/deployment operation.


5. Усі Devices доступні.


6. Read account valid.


7. Deployment account valid.


8. API-SSL configuration valid.


9. Scheduler capability proof пройдений.


10. device-mode.scheduler=yes.


11. flagged=no.


12. Management guard valid.


13. Configuration hash не змінився.


14. Capability hash не змінився.


15. Compatibility hash не змінився.


16. Required anchor markers відсутні.


17. Bootstrap chain names не зайняті.


18. MFC onboarding watchdog names не зайняті.


19. Anchor placement references актуальні.


20. Жодний unmanaged rule не посилається на bootstrap chain names.


21. Node topology consistent.


22. Для VRRP доступні всі members.



При виявленні будь-якого existing MFC permanent anchor onboarding блокується. Automatic adoption не виконується.


---

29. Namespace cleanliness

До початку onboarding повинні бути відсутні:

mfc:anchor:v1:*
mfc:s:bootstrap-return:v1

mfc4.*.r.8e40b9d4d67d42d6
mfc6.*.r.8e40b9d4d67d42d6

mfc-ob-s-*
mfc-ob-d-*
mfc-ob-b-*

Виняток — exact resources поточної nonterminal operation під час crash recovery.

Unknown MFC-like resource створює:

MFC_NAMESPACE_COLLISION

Controller не перейменовує і не видаляє його автоматично.


---

30. Staging bootstrap roots

Для кожної required root chain:

1. Прочитати rules із generated chain name.
2. Якщо chain відсутня — додати unconditional return rule.
3. Якщо існує exact rule — reuse.
4. Якщо є будь-яка інша rule — collision.
5. Повторно прочитати chain.
6. Перевірити exact canonical hash.
7. Перевірити disabled=no та invalid=no.

Root chain не повинна мати unmanaged references до моменту permanent anchor creation.


---

31. Staging permanent anchors

Для кожного planned anchor:

1. Повторно прочитати built-in chain.
2. Перевірити placement fingerprint context.
3. Отримати current .id reference rule.
4. Додати anchor із disabled=yes.
5. Використати place-before або append.
6. Повторно прочитати built-in chain.
7. Перевірити:
       exact comment;
       exact ordinal;
       action=jump;
       exact bootstrap target;
       disabled=yes;
       invalid=no.

Disabled anchor не впливає на packet path.


---

32. Onboarding watchdog

До enable першого anchor Controller створює onboarding rollback watchdog.

Watchdog складається з:

1 rollback script;
1 deadline scheduler;
1 startup scheduler.

RouterOS Scheduler може виконати one-shot task при interval=0; start-time=startup з interval=0 виконує script після кожного завантаження RouterOS. 


---

33. Watchdog names

onboarding_token =
first 16 hex characters of
SHA256(onboarding_operation_id + device_id)

script:
    mfc-ob-s-<onboarding-token>

deadline scheduler:
    mfc-ob-d-<onboarding-token>

startup scheduler:
    mfc-ob-b-<onboarding-token>


---

34. Onboarding rollback script

Script генерується з фіксованого template.

Вхідні literals:

exact permanent anchor comments;
exact bootstrap root names.

Script не містить:

user text;
site name;
device name;
description;
ticket;
credentials;
network request;
file operation.


---

34.1. Script permissions

policy=read,write
dont-require-permissions=no

Scheduler також використовує:

policy=read,write


---

34.2. Script behavior

Для кожного planned anchor незалежно:

1. Знайти rules за exact anchor comment.
2. Якщо знайдений рівно один item:
       перевірити built-in chain;
       перевірити action=jump;
       перевірити jump-target=bootstrap root.
3. Якщо всі перевірки пройдені:
       якщо disabled=no:
           set disabled=yes.
4. Якщо target уже не bootstrap:
       нічого не змінювати.
5. Якщо marker duplicate:
       нічого не змінювати для цього anchor.

На відміну від production deployment watchdog, onboarding watchdog не відновлює старий target: до onboarding permanent anchor не існував.

Його rollback-дія:

disable newly created anchor


---

34.3. Stale-watchdog protection

Після першого production deployment anchor target уже не дорівнює bootstrap root.

Тому stale onboarding watchdog:

current target != bootstrap target
    → no-op

Він не може відключити пізніший managed artifact.


---

35. Watchdog timing

minimum TTL:           60 s
default TTL:          180 s
maximum TTL:          600 s
minimum commit margin: 30 s

Deadline розраховується за RouterOS clock.

Startup scheduler залишається active до успішного commit onboarding.


---

36. Arming watchdogs

Для кожного Device:

1. Створити rollback script.
2. Перевірити script source hash.
3. Створити startup scheduler.
4. Створити deadline scheduler.
5. Перевірити names, policies і on-event.
6. Перевірити disabled=no.
7. Перевірити remaining TTL.
8. Позначити watchdog armed.

Для VRRP Node всі Device watchdogs повинні бути armed до enable першого anchor.


---

37. Anchor enable order

Anchor enable виконується послідовно на кожному Device.

Нормативний порядок:

1. IPv4 FORWARD
2. IPv6 FORWARD
3. IPv4 OUTPUT
4. IPv6 OUTPUT
5. IPv4 INPUT
6. IPv6 INPUT

Відсутні anchors пропускаються.

Для switch:

OUTPUT
INPUT

Device order:

DeviceId ascending

Current VRRP role не впливає на onboarding order, оскільки bootstrap root виконує лише return.


---

38. Enable algorithm

Для кожного anchor:

1. Прочитати anchor за exact comment.
2. Вимагати рівно один result.
3. Перевірити:
       chain;
       action;
       bootstrap jump-target;
       ordinal;
       disabled=yes.
4. Persist write intent.
5. Set disabled=no за current .id.
6. Повторно прочитати anchor.
7. Перевірити disabled=no.
8. Перевірити remaining watchdog TTL.
9. Persist verified step.

Blind retry після невідомого API result заборонений.

Спочатку читається фактичний disabled state.


---

39. Management reconnect during enable

Після enable management-family OUTPUT та INPUT anchors Controller повинен:

1. Відкрити нове API-SSL connection.


2. Перевірити certificate.


3. Увійти deployment account.


4. Прочитати system identity.


5. Прочитати active anchor.


6. Перевірити management guard hash.



Стара API session не є достатнім доказом доступності нового connection path.


---

40. Post-bootstrap verification

Після enable всіх anchors Controller виконує stable capture і перевіряє:

1. Усі required bootstrap roots існують.


2. Кожний root має рівно один unconditional return.


3. Усі permanent anchors існують.


4. Усі anchors enabled.


5. Усі anchors мають exact bootstrap target.


6. Anchor positions відповідають plan.


7. Management guard не змінився.


8. API-SSL service не змінився.


9. RouterOS accounts не змінилися.


10. Device-mode не змінився.


11. Unmanaged filter rules не змінили content.


12. Relative order unmanaged rules не змінився.


13. Єдині filter additions — bootstrap roots та anchors.


14. NAT, RAW і Mangle не змінилися.


15. Routing configuration не змінилася.


16. VRRP configuration не змінилася.


17. Interface-list configuration не змінилася.


18. New API-SSL connection працює.


19. Node system tests проходять.


20. Onboarding watchdog досі active.


21. Remaining TTL достатній для commit.




---

41. Semantic equivalence check

Controller порівнює pre-onboarding і post-onboarding filter control-flow.

Дозволена єдина нова path-вставка:

jump permanent anchor
    → unconditional return
    → original next unmanaged rule

Результат для кожного analyzed traffic class повинен залишитися незмінним.

При:

INDETERMINATE

onboarding rollback-иться.

Finding:

BOOTSTRAP_SEMANTIC_EQUIVALENCE_NOT_PROVEN


---

42. Watchdog disarming

Watchdog вимикається лише після повного verification усіх Devices.

Для кожного Device:

1. Перевірити remaining TTL.
2. Знайти deadline scheduler.
3. Знайти startup scheduler.
4. Перевірити exact identities.
5. Set disabled=yes для обох.
6. Повторно прочитати.
7. Вимагати disabled=yes.
8. Повторно перевірити anchors.

Після підтвердженого disabling:

remove deadline scheduler;
remove startup scheduler;
remove rollback script.

Втрата connection після доведеного disabled=yes не запускає rollback, але створює:

ONBOARDING_WATCHDOG_CLEANUP_INCOMPLETE


---

43. Commit

Node отримує MANAGED лише коли:

усі Devices verified;
усі anchors enabled;
усі targets bootstrap;
усі bootstrap artifacts exact;
усі management reconnects passed;
усі watchdog schedulers disabled;
onboarding lock чинний.

Одна PostgreSQL transaction:

1. Зберігає post-onboarding snapshots.
2. Зберігає permanent anchor identities.
3. Зберігає bootstrap artifact references.
4. Зберігає active artifact hash.
5. Переводить усі Devices у MANAGED.
6. Переводить Node у MANAGED.
7. Записує OnboardingOperation=COMMITTED.
8. Створює audit event.

Лише durable COMMITTED означає завершений onboarding.


---

44. Onboarding rollback

Rollback завжди повертає Node до стану без active MFC anchors.


---

44.1. Controller rollback

Для кожного Device у reverse enable order:

1. Прочитати permanent anchors.
2. Для exact bootstrap anchors:
       set disabled=yes.
3. Повторно прочитати.
4. Вимагати disabled=yes.
5. Відкрити нове API-SSL connection.
6. Перевірити original management path.
7. Видалити disabled anchors поточної operation.
8. Перевірити відсутність references на bootstrap roots.
9. Видалити bootstrap return rules.
10. Disable/remove watchdog.
11. Зберегти rollback snapshot.


---

44.2. Watchdog rollback

Watchdog:

disable exact matching bootstrap anchors

Після reconnect Controller:

1. Перевіряє, що anchors disabled.


2. Перевіряє management access.


3. Видаляє anchors.


4. Видаляє bootstrap roots.


5. Видаляє watchdog resources.


6. Переводить operation у ROLLED_BACK.




---

44.3. Rollback safety

Controller не видаляє resource, коли:

comment не збігається;

chain не збігається;

action не збігається;

target не bootstrap;

resource не входить до current plan;

resource має unmanaged reference;

marker duplicate.


У такому випадку:

RECOVERY_REQUIRED


---

45. Crash recovery

Після startup Controller шукає nonterminal onboarding operations.

45.1. Anchors відсутні або disabled

cleanup exact bootstrap resources
→ mark ROLLED_BACK
→ Node remains UNMANAGED

45.2. Anchors enabled, watchdog active

controller-initiated rollback

Watchdog не вимикається до підтвердження disabled anchors.

45.3. Anchors enabled, watchdog disabled, operation nonterminal

rollback

Навіть коли bootstrap technically works.

45.4. Operation committed

keep anchors enabled
verify MANAGED state
cleanup disabled watchdog residue

45.5. Unexpected anchor target

RECOVERY_REQUIRED

Automatic adoption або overwrite не виконуються.


---

46. Recovery decision table

Anchors	Watchdog	DB state	Рішення

Відсутні	Будь-який	Nonterminal	Cleanup, rolled back
Усі disabled/bootstrap	Active	Nonterminal	Cleanup, rolled back
Усі enabled/bootstrap	Active	Nonterminal	Rollback
Усі enabled/bootstrap	Disabled	Nonterminal	Rollback
Enabled/disabled mix	Будь-який	Nonterminal	Disable all, cleanup
Target не bootstrap	Будь-який	Nonterminal	Recovery required
Усі enabled/bootstrap	Disabled	Committed	Keep managed
Anchor missing	Будь-який	Committed	Critical drift
Anchor disabled	Будь-який	Committed	Critical drift



---

47. Idempotency

Onboarding mutation є create-or-verify.

Bootstrap root

відсутній:
    create

exact:
    reuse

divergent:
    collision

Disabled anchor

відсутній:
    create

exact і disabled:
    reuse

exact і enabled у поточній nonterminal operation:
    rollback

divergent:
    collision

Watchdog resource

відсутній:
    create

exact:
    reuse

divergent:
    collision

Onboarding не повертає partial success.


---

48. Standalone router

Послідовність:

PRECHECK
→ STAGE ROOTS
→ STAGE DISABLED ANCHORS
→ ARM WATCHDOG
→ ENABLE ANCHORS
→ VERIFY
→ DISARM WATCHDOG
→ COMMIT

Management address повинна бути фізичною адресою Device.


---

49. Multi-WAN router

Onboarding не змінює routing або WAN state.

Додатково перевіряються:

management route;
current active management path;
routing configuration hash;
routing-rule hash;
NAT hash;
RAW hash;
Mangle hash;
interface-list hash.

Зміна active default route без configuration change:

не змінює bootstrap artifact;

вимагає повторного management reconnect;

не запускає forced failover.


Anchor set не залежить від current primary WAN.


---

50. VRRP Node

Усі physical members входять в одну OnboardingOperation.

Обов’язково:

1. Кожний member має окрему management address.


2. Controller підключається до кожного member без VRRP virtual IP.


3. Усі members проходять prerequisites.


4. Bootstrap roots staged на всіх members.


5. Disabled anchors staged на всіх members.


6. Watchdogs armed на всіх members.


7. Лише після цього anchors enable-яться.


8. Один failed member запускає rollback всіх members.


9. Node не стає MANAGED частково.


10. Поточна role не змінює anchor set.


11. Split-master не змінює onboarding algorithm.


12. VRRP configuration hash не повинен змінитися.



Role change під час onboarding сам по собі не є причиною rollback, оскільки pass-through anchors не залежать від role. Але FAILURE, missing member або multiple-master inconsistency блокує commit.


---

51. MikroTik switch

Для Node.kind=SWITCH:

IPv4 INPUT
IPv4 OUTPUT
IPv6 INPUT, якщо required
IPv6 OUTPUT, якщо required

Не створюються:

FORWARD anchor;
bridge filter;
switch ACL;
hardware offload changes.

Onboarding не змінює transit packet path.

Physical CRS hardware profile повинен бути read-only validated до onboarding.


---

52. Concurrency

Обмеження:

one onboarding operation per Node
one writer per Device
one scheduler capability probe per Device
one bootstrap watchdog set per Device

У межах одного VRRP Node staging різних Devices може бути parallel, але:

maximum parallel Devices = 4
writes per Device = 1

Anchor enable виконується послідовно.


---

53. Durable lock

OnboardingLock {
    node_id
    onboarding_operation_id
    owner_instance_id
    acquired_at
    heartbeat_at
    expires_at
}

Прострочений lock не видаляється без actual-state recovery.

Lock не запобігає ручним WinBox/API changes, тому перед кожною effectful phase Controller повторно перевіряє RouterOS state.


---

54. Write-ahead journal

Перед кожною mutation:

OnboardingStep {
    id
    operation_id
    device_id
    sequence
    operation
    expected_before_hash
    desired_after_hash

    state:
        INTENT_RECORDED |
        EFFECT_SENT |
        VERIFIED |
        FAILED
}

Порядок:

persist intent
→ execute RouterOS effect
→ read actual state
→ persist verified result

API !done без read-back не вважається достатнім підтвердженням.


---

55. Manual RouterOS changes

При будь-якій зовнішній зміні:

firewall order;
guard;
api-ssl service;
users/groups;
device-mode;
bootstrap resource;
watchdog resource;
VRRP configuration;
management route;

Controller зупиняє onboarding.

Якщо anchor enable ще не почався:

rollback staged resources

Якщо хоча б один anchor enabled:

rollback all enabled anchors

Finding:

CONCURRENT_ROUTEROS_CHANGE


---

56. Security requirements

1. Onboarding використовує deployment account, не administrator account.


2. Default RouterOS groups заборонені.


3. Deployment account має API-only login.


4. Deployment account source-restricted.


5. Plain API 8728 disabled.


6. API-SSL certificate mandatory.


7. Management guard створюється поза onboarding.


8. Guard ніколи не змінюється Controller.


9. Device-mode не змінюється Controller.


10. dont-require-permissions=yes заборонений.


11. Script source генерується fixed compiler.


12. Script не виконує network operations.


13. Script не використовує files.


14. Script змінює лише exact permanent anchors.


15. Anchor спочатку створюється disabled.


16. Anchor move не використовується.


17. User text не потрапляє в RouterOS resources.


18. Credentials не потрапляють у logs або audit.


19. Raw RouterOS command sentence не логуються.


20. Onboarding потребує exact immutable plan hash.


21. Node не отримує MANAGED до durable commit.


22. Unknown MFC namespace resource не adopt-иться.


23. Unexpected target не переписується.


24. Failed onboarding не залишає active MFC anchor.




---

57. Audit events

onboarding.plan.created
onboarding.started
onboarding.precheck.passed
onboarding.precheck.blocked

onboarding.scheduler_probe.started
onboarding.scheduler_probe.passed
onboarding.scheduler_probe.failed

onboarding.bootstrap_root.created
onboarding.bootstrap_root.reused
onboarding.anchor.created
onboarding.watchdog.armed
onboarding.anchor.enabled

onboarding.verification.passed
onboarding.verification.failed

onboarding.watchdog.disabled
onboarding.committed

onboarding.rollback.started
onboarding.anchor.disabled
onboarding.anchor.removed
onboarding.bootstrap_root.removed
onboarding.rollback.completed

onboarding.recovery.started
onboarding.recovery.required

Audit містить:

Node ID;
Device ID;
operation ID;
plan hash;
configuration hashes;
anchor marker;
anchor ordinal;
bootstrap target;
watchdog token;
result code;
actor;
correlation ID.

Audit не містить:

password;
script source;
management source addresses;
firewall rule contents;
raw RouterOS replies.


---

58. Error model

Prerequisites

ONBOARDING_ROUTEROS_UNSUPPORTED
ONBOARDING_API_SSL_INVALID
ONBOARDING_PLAIN_API_ENABLED
ONBOARDING_READ_ACCOUNT_INVALID
ONBOARDING_DEPLOY_ACCOUNT_INVALID
ONBOARDING_ACCOUNT_SOURCE_INVALID
DEVICE_MODE_SCHEDULER_DISABLED
DEVICE_FLAGGED
SCHEDULER_CAPABILITY_TEST_FAILED
MANAGEMENT_GUARD_MISSING
MANAGEMENT_GUARD_TOO_BROAD
MANAGEMENT_GUARD_INVALID
MANAGEMENT_PATH_INDETERMINATE

Placement

ANCHOR_PLACEMENT_STALE
ANCHOR_REFERENCE_MISSING
ANCHOR_REFERENCE_DYNAMIC
ANCHOR_BEFORE_GUARD
ANCHOR_UNREACHABLE
ANCHOR_CONTEXT_INDETERMINATE

Staging

MFC_NAMESPACE_COLLISION
BOOTSTRAP_ROOT_COLLISION
BOOTSTRAP_ROOT_HASH_MISMATCH
ANCHOR_MARKER_COLLISION
ANCHOR_POSITION_MISMATCH
ANCHOR_STAGING_FAILED

Watchdog

ONBOARDING_WATCHDOG_COLLISION
ONBOARDING_WATCHDOG_INVALID
ONBOARDING_WATCHDOG_ARM_FAILED
ONBOARDING_WATCHDOG_DEADLINE_TOO_CLOSE
ONBOARDING_WATCHDOG_DISABLE_FAILED
ONBOARDING_WATCHDOG_CLEANUP_INCOMPLETE

Verification

BOOTSTRAP_MANAGEMENT_RECONNECT_FAILED
BOOTSTRAP_SEMANTIC_EQUIVALENCE_NOT_PROVEN
BOOTSTRAP_CONFIGURATION_DRIFT
BOOTSTRAP_ANCHOR_INVALID
BOOTSTRAP_ROOT_INVALID
BOOTSTRAP_NODE_INCONSISTENT

Recovery

ONBOARDING_ROLLBACK_FAILED
ONBOARDING_UNEXPECTED_ANCHOR_TARGET
ONBOARDING_RESIDUE_PRESENT
RECOVERY_REQUIRED


---

59. Timeouts і bounds

Операція	Limit

Plan lifetime	30 min
RouterOS connect	5 s
Login	10 s
Read command	30 s
Write command	15 s
Read-back	15 s
Scheduler proof	15 s
Bootstrap root staging	30 s
Anchor staging	30 s
Anchor enable	15 s
Management reconnect	20 s
Stable verification capture	120 s
Rollback одного Device	60 s
Watchdog TTL default	180 s
Parallel Device staging	4
Writes per Device	1
Controller write retry	1 після actual-state read
Controller source prefixes у GuardProfile	16


Необмежені timeout або retry заборонені.


---

60. Unit tests

Обов’язкові:

onboarding state machine;
Node/Device managed-state invariants;
plan hashing;
plan invalidation;
RequiredAnchorSet;
bootstrap artifact hash;
bootstrap chain names;
guard marker parsing;
guard predicate validation;
account policy validation;
anchor placement context;
namespace collision;
root create-or-verify;
disabled anchor create-or-verify;
scheduler capability proof;
watchdog source generation;
watchdog stale-target protection;
anchor enable order;
semantic equivalence;
rollback order;
crash recovery decision table.


---

61. CHR integration tests

standalone IPv4 onboarding;
standalone dual-stack onboarding;
successful scheduler proof;
scheduler disabled by device-mode;
flagged Device;
plain API enabled;
api-ssl without certificate;
invalid certificate;
default RouterOS group rejected;
invalid deployment account policies;
invalid account source restriction;
missing management guard;
overbroad management guard;
anchor before guard;
anchor after unconditional drop;
bootstrap root collision;
anchor marker collision;
manual firewall reorder during onboarding;
management reconnect after each input/output anchor;
watchdog deadline rollback;
watchdog startup rollback;
Controller crash after root staging;
Controller crash after disabled anchors;
Controller crash after first enabled anchor;
Controller crash after all enabled anchors;
Controller crash after watchdog disabling;
rollback cleanup;


---

62. VRRP tests

all members onboard successfully;
one member unreachable before staging;
one member scheduler-disabled;
one member root collision;
one member watchdog arm failure;
one member management reconnect failure;
rollback all members;
split-master Node;
role swap during onboarding;
member enters FAILURE state;
Node commit only after all members verified;


---

63. Multi-WAN tests

single WAN;
failover primary active;
failover backup active;
active route changes during onboarding;
PCC balanced Node;
management through non-main routing table;
routing configuration changes during onboarding;
NAT/Mangle hash changes during onboarding;
no forced WAN switch.


---

64. Switch tests

CRS IPv4 INPUT/OUTPUT anchors;
CRS dual-stack anchors;
FORWARD anchor absent;
bridge/VLAN unchanged;
hardware offload unchanged;
unknown hardware profile blocked.


---

65. Watchdog tests

all anchors disabled → no-op;
one enabled bootstrap anchor → disable;
all enabled bootstrap anchors → disable all;
anchor target changed to managed artifact → no-op;
duplicate marker → no write for duplicate;
missing anchor → continue with other exact anchors;
wrong action → no write;
wrong chain → no write;
startup execution;
deadline execution;
script tampered;
scheduler points to wrong script;
stale watchdog after first deployment;


---

66. Fault-injection points

Connection розривається:

після scheduler test script add;
після scheduler test execution;
після bootstrap root add;
після disabled anchor add;
після all anchors staged;
після rollback script add;
після deadline scheduler add;
після watchdogs armed;
перед first anchor enable;
після first anchor enable до reply;
після output anchor;
після input anchor;
під час new API reconnect;
під час stable verification capture;
після first watchdog disable;
після all watchdog disables;
перед database commit;
після database commit.

Для кожного point допустимі лише:

Node UNMANAGED;
Node MANAGED;
Node RECOVERY_REQUIRED з точним діагнозом.

Невизначений internal state заборонений.


---

67. Acceptance criteria

Специфікація реалізована лише коли:

1. Onboarding target — один Node.


2. VRRP Node onboard-иться всіма members.


3. Controller не створює RouterOS users.


4. Controller не змінює user groups.


5. Controller не змінює credentials.


6. Controller не змінює API-SSL service.


7. Controller не встановлює certificates.


8. Controller не змінює device-mode.


9. Controller не створює management guard.


10. Read і deployment accounts розділені.


11. Default RouterOS groups відхиляються.


12. Plain API 8728 повинен бути disabled.


13. API-SSL certificate обов’язковий.


14. Management guard розташований до anchor.


15. Guard із 0.0.0.0/0 або ::/0 відхиляється.


16. Scheduler capability доводиться actual one-shot execution.


17. dont-require-permissions=yes не використовується.


18. Existing MFC namespace collision блокує onboarding.


19. Automatic resource adoption відсутній.


20. Anchor position обирає оператор.


21. Dynamic rule не може бути placement reference.


22. Anchor створюється через place-before або append.


23. move не використовується.


24. Bootstrap root містить рівно один unconditional return.


25. Bootstrap artifact не містить policy rules.


26. Permanent anchor спочатку створюється disabled.


27. Disabled anchor перевіряється до watchdog arming.


28. Усі VRRP watchdogs armed до enable першого anchor.


29. Watchdog rollback disables anchors.


30. Stale watchdog не впливає на managed artifact.


31. Anchor enable має read-back.


32. New API connection відкривається після management anchors.


33. Unmanaged rules не змінюються.


34. Relative unmanaged rule order не змінюється.


35. NAT, RAW, Mangle і routing не змінюються.


36. VRRP configuration не змінюється.


37. Active WAN не впливає на bootstrap artifact.


38. Current VRRP role не впливає на bootstrap artifact.


39. Switch не отримує FORWARD anchor.


40. Semantic equivalence bootstrap path доведена.


41. Indeterminate equivalence запускає rollback.


42. Node не стає MANAGED частково.


43. Watchdogs disabled до durable commit.


44. Nonterminal onboarding після crash rollback-иться.


45. Unexpected anchor target не переписується.


46. Rollback видаляє лише exact resources поточної operation.


47. Failed onboarding не залишає enabled anchors.


48. First production deployment використовує bootstrap roots як old targets.


49. Audit відтворює кожну effectful operation.


50. Build і tests не змінюють Git working tree.




---

68. Уточнення попередніх специфікацій

Попереднє рішення	Нормативне уточнення

Controller може створити management guard під час bootstrap	Guard налаштовується поза Controller і лише перевіряється
Controller може налаштувати API-SSL	API-SSL є prerequisite, write заборонений
Controller може створити service accounts	Accounts створюються адміністратором
Bootstrap anchor одразу active	Anchor спочатку створюється disabled
Onboarding rollback видаляє anchor	Watchdog лише disables; Controller потім видаляє
Початковий old artifact не визначений	Визначений fixed pass-through artifact 8e40b9d4d67d42d6
Empty root chain	Заборонена; використовується explicit unconditional return
Anchor placement може визначатись автоматично	Позицію явно вибирає оператор
Можливе використання move	Використовується тільки place-before або append
Scheduler capability визначається полем device-mode	Додано actual one-shot execution proof
VRRP onboarding залежить від master role	Role не впливає на pass-through onboarding
OOB може замінити watchdog	Watchdog залишається обов’язковим



---

69. Результат етапу

Після onboarding кожний managed Device має мінімальний постійний контур:

verified management guard
        ↓
permanent MFC anchors
        ↓
fixed pass-through bootstrap roots
        ↓
known initial active artifact
        ↓
готовність до Safe Deployment

При цьому Controller не бере на себе зайве керування RouterOS users, services, certificates, device-mode, routing або іншими підсистемами.

Наступний необхідний документ:

MikroTik Firewall Controller
MVP End-to-End Workflow and Acceptance Specification v0.1

Він має звести без додавання нових підсистем:

inventory;
read-only capture;
policy creation;
analysis;
approval;
compilation;
onboarding;
safe deployment;
rollback;
drift detection;
мінімальні GUI workflows;
повний production acceptance matrix.