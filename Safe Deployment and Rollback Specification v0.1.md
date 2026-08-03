MikroTik Firewall Controller

Safe Deployment and Rollback Specification v0.1

Дата: 3 серпня 2026 року
Статус: нормативна специфікація M4 — Safe Deployment


---

1. Призначення

Документ визначає мінімальний write-path, необхідний для безпечного застосування вже:

затвердженої;

проаналізованої;

скомпільованої;

прив’язаної до конкретного Node


firewall-політики.

Approved policy
    ↓
Verified RouterOS artifact
    ↓
Detached staging
    ↓
Local rollback watchdog
    ↓
Anchor activation
    ↓
Runtime verification
    ↓
Commit або rollback

Deployment виконується для одного логічного Node:

standalone router;

single-router multi-WAN;

VRRP-вузол;

MikroTik CRS лише для management-plane policy.



---

2. Межі специфікації

Safe Deployment v1 змінює лише:

controller-owned IPv4 filter rules;
controller-owned IPv6 filter rules;
controller-owned static address lists;
controller-owned anchor jump-target;
тимчасові RouterOS script/scheduler resources watchdog.

Не змінюються:

NAT;
RAW;
Mangle;
routing;
routing rules;
VRRP;
interfaces;
interface lists;
bridge/VLAN;
switch ACL;
IP services;
management guard;
RouterOS users;
device-mode.

У цій версії не реалізуються:

кампанії одночасного розгортання на багато Node;

автоматичне очищення старих filter artifacts;

автоматичне виправлення довільного drift;

Safe Mode automation;

roll-forward recovery;

довільне виконання RouterOS script;

OOB як заміна локального watchdog;

forced WAN failover test;

firmware update;

bootstrap management guard і anchors.


Один deployment target — один Node.


---

3. Нормативні спрощення

3.1. Rollback-only recovery

Будь-який незавершений deployment після activation відновлюється до попереднього artifact.

unknown intermediate state
    → rollback old artifact

Controller v1 не приймає автоматичне рішення завершити roll-forward.

3.2. Watchdog обов’язковий

У Safe Deployment v1 кожний Device повинен підтримувати RouterOS Scheduler.

Наявність OOB-доступу:

підвищує відновлюваність;

не скасовує watchdog;

не створює окремий deployment mode.


3.3. Без Safe Mode

RouterOS Safe Mode є console/WinBox session mechanism. Він залежить від живої інтерактивної сесії, а floating undo обмежений історією, яка наразі утримує до 100 останніх дій. Тому він не використовується як основа API deployment або multi-device rollback. 

3.4. Без зміни active chains

Active controller-owned chains ніколи не редагуються.

Rollback виконується лише поверненням anchor на попередній root chain.


---

4. Основні інваріанти

1. До activation активний traffic використовує повний старий artifact.


2. Після activation він використовує повний новий artifact.


3. Частково створений artifact ніколи не стає active.


4. Active chain не редагується in-place.


5. Старий artifact не видаляється до завершення deployment.


6. Новий artifact повністю перевіряється до зміни anchor.


7. Watchdog встановлюється до першої зміни anchor.


8. Watchdog знає точні старі та нові anchor targets.


9. Watchdog не змінює anchor із невідомим target.


10. Watchdog не може відкотити пізніший deployment.


11. Кожна RouterOS mutation має read-back verification.


12. Відповідь API !done сама по собі не є доказом бажаного стану.


13. Невідомий результат write-команди перевіряється читанням фактичного стану.


14. Один Device має не більше одного активного writer.


15. Один Node має не більше одного незавершеного deployment.


16. Ручні RouterOS зміни під час deployment є конфліктом.


17. Current VRRP role не змінює desired policy.


18. Current active WAN не змінює desired artifact.


19. Будь-який критичний verification failure запускає rollback.


20. COMMITTED можливий лише після деактивації watchdog на всіх Device.


21. Deployment не має прихованого force apply.


22. Partial success Node не вважається committed.


23. Усі write operations є типізованими й allowlisted.


24. У production API відсутній довільний RouterOS command executor.


25. Втрата Controller не повинна залишити нову policy active після watchdog deadline.




---

5. Компоненти

DeploymentPlanner
DeploymentCoordinator
RouterOsDeploymentSession
ArtifactStager
WatchdogCompiler
WatchdogManager
AnchorActivator
DeploymentVerifier
RollbackCoordinator
DeploymentRecoveryService

Це логічні компоненти modular monolith, а не окремі deployable services.

Не потрібні:

message broker;

workflow engine;

distributed transaction coordinator;

окремий deployment microservice.



---

6. Типізований RouterOS write adapter

Після завершення read-only M1 дозволяється namespace:

Mfc.RouterOs.Write

Він не є універсальним RouterOS writer.

public interface IRouterOsDeploymentSession : IAsyncDisposable
{
    Task<ActualManagedState> ReadManagedStateAsync(
        CancellationToken cancellationToken);

    Task AddAddressListEntryAsync(
        AddressListEntryWrite write,
        CancellationToken cancellationToken);

    Task AddFilterRuleAsync(
        FilterRuleWrite write,
        CancellationToken cancellationToken);

    Task SetAnchorTargetAsync(
        AnchorTargetWrite write,
        CancellationToken cancellationToken);

    Task AddRollbackScriptAsync(
        RollbackScriptWrite write,
        CancellationToken cancellationToken);

    Task AddRollbackSchedulerAsync(
        RollbackSchedulerWrite write,
        CancellationToken cancellationToken);

    Task DisableRollbackSchedulerAsync(
        RouterOsItemId schedulerId,
        CancellationToken cancellationToken);

    Task RemoveRollbackSchedulerAsync(
        RouterOsItemId schedulerId,
        CancellationToken cancellationToken);

    Task RemoveRollbackScriptAsync(
        RouterOsItemId scriptId,
        CancellationToken cancellationToken);

    Task<RouterPingResult> PingAsync(
        RouterPingRequest request,
        CancellationToken cancellationToken);
}

Методів із параметрами:

string command
string menu
string script
Dictionary<string, string> attributes

не існує.


---

7. Write-command allowlist

7.1. Filter і address lists

/ip/firewall/address-list/add
/ipv6/firewall/address-list/add

/ip/firewall/filter/add
/ipv6/firewall/filter/add

/ip/firewall/filter/set
/ipv6/firewall/filter/set

filter/set дозволяє змінювати лише:

.id
jump-target

і лише для anchor із валідним ownership marker.

Заборонено через production writer:

filter remove
filter move
filter enable
filter disable
filter set для звичайної managed rule
address-list set
address-list remove

7.2. Watchdog

/system/script/add
/system/script/remove

/system/scheduler/add
/system/scheduler/set
/system/scheduler/remove

system/scheduler/set дозволяє лише:

.id
disabled=yes

system/script/run заборонений.

7.3. Verification

/ping

Дозволений лише bounded ICMP probe із типізованими параметрами.


---

8. RouterOS item lookup

API set не приймає query expression. Controller спочатку виконує print із .proplist=.id,..., отримує точний item ID, а вже потім виконує set за .id. 

Алгоритм для кожної mutation існуючого ресурсу:

1. Print за deterministic ownership identity.
2. Вимагати рівно один результат.
3. Перевірити всі immutable properties.
4. Отримати .id.
5. Виконати typed set.
6. Повторно прочитати ресурс.
7. Перевірити фактичний результат.

RouterOS .id не зберігається як довгострокова identity у PostgreSQL.


---

9. DeploymentPlan

DeploymentPlan {
    id: DeploymentPlanId
    node_id: NodeId

    logical_policy_hash: Hash256
    analysis_bundle_hash: Hash256
    topology_projection_hash: Hash256

    device_plans: DeviceDeploymentPlan[]

    activation_order: DeviceId[]
    rollback_order: DeviceId[]

    created_by: UserId
    created_at: UTC
    expires_at: UTC
    plan_hash: Hash256
}

DeviceDeploymentPlan {
    device_id: DeviceId

    expected_routeros_version: string
    expected_capability_hash: Hash256
    expected_configuration_hash: Hash256
    expected_compatibility_hash: Hash256

    expected_guard_context_hash: Hash256
    expected_anchor_context_hash: Hash256

    old_artifact_hash: Hash256
    old_anchor_targets: AnchorTargetSet

    new_artifact_hash: Hash256
    new_anchor_targets: AnchorTargetSet

    artifact: RouterOsFilterArtifact

    anchor_activation_order: AnchorKey[]
    anchor_rollback_order: AnchorKey[]

    transition_state_hashes: Hash256[]

    rollback_ttl: Duration
    probes: DeploymentProbe[]
}

Plan immutable.


---

10. Plan validity

Plan анулюється при зміні:

approved policy;
analysis bundle;
artifact;
Node membership;
RouterOS version;
capability;
configuration snapshot;
compatibility state;
management guard;
anchor position або properties;
zone binding;
interface-list membership;
NAT/RAW/Mangle dependency;
routing configuration;
VRRP configuration;
compiler version;
deployment schema version.

Operational observation, наприклад current active WAN, не анулює plan автоматично, але повторно перевіряється перед activation.


---

11. Deployment preconditions

Перед staging Controller повинен довести:

1. Plan не прострочений.


2. Plan hash збігається.


3. Revision залишається approved.


4. Analysis bundle актуальний.


5. Node не DISABLED.


6. У Node немає іншого active deployment.


7. Усі Device доступні через physical management addresses.


8. TLS і RouterOS authentication успішні.


9. RouterOS version не змінилася.


10. Capability hash не змінився.


11. Configuration hash не змінився.


12. Management guard валідний.


13. Кожний anchor існує рівно один раз.


14. Anchor має правильний chain, action і comment.


15. Кожний anchor вказує на expected old target.


16. Старий artifact існує й має expected hash.


17. Нові chain names не мають unmanaged references.


18. Нові address-list names не конфліктують з unmanaged resources.


19. Немає критичного drift.


20. Scheduler дозволений device-mode.


21. Device не має flagged=yes.


22. Немає active MFC watchdog іншого deployment.


23. RouterOS clock читається й парситься.


24. Залишається достатній deployment timeout.


25. Artifact не перевищує configured limits.



RouterOS device-mode може повністю заборонити /system/scheduler; у flagged state RouterOS також обмежує створення або активацію scheduler entries. Controller не змінює device-mode автоматично, а блокує deployment. 


---

12. NO_CHANGES

Коли для всіх Device:

active artifact hash == desired artifact hash
active anchor targets == desired targets
actual resource hash == compiled resource hash

deployment завершується:

NO_CHANGES

Без:

staging;

watchdog;

RouterOS mutation.



---

13. Deployment state machine

CREATED
   ↓
PRECHECKING
   ├──→ BLOCKED
   ↓
STAGING
   ↓
STAGED
   ↓
ARMING_WATCHDOG
   ↓
WATCHDOG_ARMED
   ↓
ACTIVATING
   ↓
VERIFYING
   ↓
DISARMING_WATCHDOG
   ↓
COMMITTED

Failure path:

STAGING
ARMING_WATCHDOG
ACTIVATING
VERIFYING
DISARMING_WATCHDOG
        ↓
ROLLBACK_PENDING
        ↓
ROLLING_BACK
        ├──→ ROLLED_BACK
        └──→ RECOVERY_REQUIRED

Додаткові terminal states:

NO_CHANGES
BLOCKED
CANCELED
FAILED
COMMITTED
ROLLED_BACK
RECOVERY_REQUIRED


---

14. DeviceDeployment states

PENDING
PRECHECKED
STAGING
STAGED
WATCHDOG_ARMED
ACTIVATING
ACTIVE_UNVERIFIED
VERIFIED
WATCHDOG_DISARMED
COMMITTED
ROLLING_BACK
ROLLED_BACK
RECOVERY_REQUIRED

Node не отримує COMMITTED, поки всі його Device не committed.


---

15. Durable deployment lock

DeploymentLock {
    node_id
    deployment_id
    owner_instance_id
    acquired_at
    heartbeat_at
    expires_at
}

Вимоги:

unique lock на Node;

lock зберігається в PostgreSQL;

heartbeat bounded;

прострочений lock не видаляється без recovery inspection;

новий Controller instance спочатку читає actual RouterOS state;

lock не є доказом відсутності ручних WinBox/CLI changes.



---

16. Write-ahead step journal

Перед кожним RouterOS effect Controller записує:

DeploymentStep {
    id
    deployment_id
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
    started_at
    completed_at?
    sanitized_error?
}

Порядок:

1. Persist INTENT_RECORDED.
2. Execute RouterOS operation.
3. Read actual state.
4. Persist VERIFIED або FAILED.

DB не позначає effect успішним до read-back.


---

17. Staging order

На кожному Device:

1. Address lists.
2. Company deny chains.
3. Site deny chains.
4. Node deny chains.
5. Root chains.
6. Повний artifact read-back.

Root chain створюється останньою, оскільки вона посилається на deny chains.

Усі staged filter rules:

enabled;

detached від active anchor;

додаються у physical artifact order.


Вимикати staged rules не потрібно: detached chain не бере участі в packet path.


---

18. Address-list create-or-verify

Для кожного content-addressed list:

1. Прочитати всі entries із list=<generated-name>.
2. Розділити MFC-owned і unmanaged entries.
3. Якщо list відсутній — додати всі entries.
4. Якщо існує точний desired set — reuse.
5. Якщо існує правильний subset — додати відсутні entries.
6. Якщо є extra або mismatched entry — collision.
7. Повторно прочитати set.
8. Порівняти content hash.

Entry order не має значення.

Blind retry /add після втрати connection заборонений. Спочатку читається фактичний стан.


---

19. Filter-chain create-or-verify

Для кожної detached chain:

1. Прочитати всі rules цієї chain у фактичному порядку.
2. Перевірити ownership comments.
3. Якщо chain порожня — додати всі rules послідовно.
4. Якщо фактичні rules дорівнюють desired rules — reuse.
5. Якщо фактичні rules є точним desired prefix — додати suffix.
6. При будь-якій іншій розбіжності — collision.
7. Повторно прочитати chain.
8. Порівняти canonical chain hash.

Compiler і CHR compatibility tests повинні підтвердити append order для кожної підтримуваної RouterOS version.

Writer не використовує:

move;
place-before;
set звичайної rule;
remove divergence.


---

20. Staging collisions

Deployment блокується при:

unmanaged rule у generated chain;
неправильний ownership comment;
правильний comment із відмінним content;
extra rule у content-addressed chain;
duplicate rule marker;
address list із extra entry;
unmanaged reference на detached chain;
неочікуваний jump у staged chain;
resource name із відмінним artifact content.

Controller не намагається автоматично виправити collision.


---

21. Artifact verification

Після staging Controller повторно читає:

усі generated address-list entries;
усі generated filter rules;
chain order;
comments;
disabled/invalid state;
jump targets усередині root chain.

Після canonicalization:

actual resource hash == compiled resource hash

Додатково:

усі rules disabled=no;

усі rules invalid=no;

всі address-list entries static;

staged root chain не є target active anchor;

unmanaged rules не посилаються на staged chain.


Лише після цього Device отримує STAGED.


---

22. Watchdog model

Watchdog складається з трьох тимчасових RouterOS resources:

1 rollback script;
1 deadline scheduler;
1 startup scheduler.

Призначення:

deadline scheduler виконує rollback після TTL;

startup scheduler виконує rollback після reboot;

обидва запускають той самий fixed rollback script.


RouterOS Scheduler із interval=0 виконує task один раз у заданий момент. Scheduler із start-time=startup та interval=0 виконується після кожного запуску RouterOS. 


---

23. Watchdog names

deployment_token =
first 16 hex chars of SHA256(deployment_id + device_id)

Resources:

script:
    mfc-rb-s-<deployment-token>

deadline scheduler:
    mfc-rb-d-<deployment-token>

startup scheduler:
    mfc-rb-b-<deployment-token>

Names:

ASCII;

lowercase;

не містять site/device names;

не містять policy metadata;

унікальні для deployment і Device.



---

24. Rollback script contract

Rollback script генерується тільки WatchdogCompiler із фіксованої AST/template.

Вхідними literals є лише:

validated anchor comments;
validated old chain names;
validated new chain names;
rollback order;
deployment token.

User text, description, reason і ticket у script не потрапляють.


---

24.1. Дозволені script operations

/ip/firewall/filter/find
/ip/firewall/filter/get
/ip/firewall/filter/set

/ipv6/firewall/filter/find
/ipv6/firewall/filter/get
/ipv6/firewall/filter/set

Заборонені:

fetch;
file operations;
DNS;
routing changes;
user changes;
scheduler changes;
script changes;
reboot;
logging external data;
network access;
import/export.


---

24.2. Script algorithm

1. Знайти кожний anchor за точним comment.
2. Вимагати рівно один item.
3. Перевірити:
       built-in chain;
       action=jump;
       disabled=no;
       current target ∈ {old target, new target}.
4. Якщо будь-яка перевірка не пройшла:
       не виконувати жодної зміни.
5. Для кожного anchor у rollback order:
       повторно прочитати current target;
       якщо target == new:
           set target = old;
       якщо target == old:
           no-op;
       якщо target інший:
           припинити script.

Це забезпечує compare-before-restore.


---

24.3. Script permissions

policy=read,write
dont-require-permissions=no

dont-require-permissions=yes заборонений.

Source hash обчислюється Controller і перевіряється після створення.


---

25. Watchdog timing

minimum TTL:          60 s
default TTL:         180 s
maximum TTL:         600 s
minimum commit margin: 30 s

rollback_at обчислюється за фактичним RouterOS clock.

Controller читає:

current date;
current time;
time zone;
uptime.

Перед activation перевіряється:

remaining TTL >= planned activation budget + verification budget + commit margin

Якщо margin недостатній:

WATCHDOG_DEADLINE_TOO_CLOSE

Activation не починається.


---

26. Watchdog arming

Для кожного Device:

1. Перевірити відсутність resource-name collision.
2. Створити rollback script.
3. Прочитати script і перевірити source hash.
4. Створити startup scheduler.
5. Створити deadline scheduler.
6. Прочитати обидва scheduler entries.
7. Перевірити:
       script name;
       on-event;
       interval;
       start time/date;
       policies;
       disabled=no.
8. Повторно перевірити remaining TTL.
9. Позначити WATCHDOG_ARMED.

Якщо будь-яка операція має невідомий результат:

read actual state
→ verify exact resource
→ continue або fail


---

27. Watchdog pre-activation rule

Перший anchor заборонено змінювати, доки:

rollback script verified;
deadline scheduler verified;
startup scheduler verified;
all target Device watchdogs armed.

Для VRRP-вузла watchdog має бути armed на всіх members до activation першого member.


---

28. Activation order

Plan визначає порядок anchor changes.

Загальна класифікація:

NON_MANAGEMENT_CRITICAL
MANAGEMENT_CRITICAL

Порядок:

1. Non-management-critical anchors.
2. Management-critical anchors.

Typical direct management path:

FORWARD, якщо він не потрібний management VPN;
OUTPUT;
INPUT останнім.

Порядок не hardcoded: його визначає management-path analysis.


---

29. Transition-state validation

Зміна до шести anchors не є cross-chain atomic operation.

Тому analysis bundle повинен містити всі проміжні стани:

state 0: усі old
state 1: anchor 1 new, інші old
state 2: anchors 1–2 new, інші old
...
state N: усі new

Для кожного state перевіряються:

management guard path;

VRRP protected traffic;

required multi-WAN control flows;

explicit default disposition;

unmanaged pre/post-anchor interaction.


Якщо хоча б один intermediate state не доведений безпечним:

TRANSITION_STATE_UNSAFE

Plan не створюється.


---

30. Anchor activation algorithm

Для кожного anchor:

1. Повторно прочитати anchor за comment.
2. Перевірити exact identity і properties.
3. Якщо target == desired new:
       step already applied.
4. Якщо target != expected old:
       abort.
5. Persist step intent.
6. Виконати typed set jump-target=new.
7. Повторно прочитати anchor.
8. Вимагати target == new.
9. Persist verified step.
10. Перевірити watchdog remaining TTL.

Writes на одному Device виконуються послідовно.


---

31. Втрата API connection під час activation

Втрата connection не означає автоматично, що set не виконався.

Controller:

1. Не повторює set blind.
2. Відкриває новий API-SSL session.
3. Читає anchor.
4. Якщо target == new:
       effect applied.
5. Якщо target == old:
       effect не applied, дозволений один controlled retry.
6. Якщо target інший:
       RECOVERY_REQUIRED.
7. Якщо reconnect неможливий:
       перейти в ROLLBACK_PENDING;
       не деактивувати watchdog.


---

32. Post-activation verification

Verification має чотири рівні:

1. Managed resource integrity.
2. New management connection.
3. Node-specific operational checks.
4. Watchdog readiness.


---

32.1. Managed resource integrity

Перевіряються:

усі active anchor targets;
новий root chain;
deny chains;
address lists;
resource hash;
rule order;
disabled/invalid flags.


---

32.2. New management connection

Потрібно відкрити нове, незалежне API-SSL з’єднання.

Не можна вважати стару established API session достатньою перевіркою, оскільки вона могла залишитися доступною через connection-state handling.

Перевіряються:

TLS handshake;
certificate;
RouterOS authentication;
system identity;
active anchor targets.

Для VRRP перевіряється кожна physical management address.


---

33. Deployment probes

Safe Deployment v1 має тільки два типи probes:

API_SSL
ROUTER_PING

Не реалізуються:

HTTP;
HTTPS;
DNS;
TCP application probes;
bandwidth tests;
traffic generation.


---

33.1. API_SSL probe

target: physical management address
expected: reachable
critical: always

33.2. ROUTER_PING probe

RouterPingProbe {
    destination: IPAddress
    family: IPv4 | IPv6
    source_address: IPAddress?
    routing_table: string?
    interface: string?
}

Fixed execution parameters:

count: 3
bounded interval
bounded timeout
no DNS name
no flood ping

RouterOS ping підтримує src-address, interface і routing-table, що дозволяє перевіряти вже наявні routing paths без зміни routing configuration. 


---

34. Probe result

PASS
FAIL
INCONCLUSIVE
NOT_APPLICABLE

Для critical probe:

FAIL або INCONCLUSIVE
    → rollback

Probe profile є частиною deployment plan hash.


---

35. Standalone deployment

Алгоритм:

LOCK NODE
→ PRECHECK
→ STAGE DEVICE
→ VERIFY ARTIFACT
→ ARM WATCHDOG
→ ACTIVATE ANCHORS
→ VERIFY
→ DISARM WATCHDOG
→ COMMIT

При будь-якій помилці після activation:

ROLLBACK


---

36. Multi-WAN deployment

Multi-WAN Node залишається одним Device deployment.

Compiler artifact не залежить від current active WAN.

Перед activation повторно перевіряються:

routing configuration hash;
routing-rule hash;
NAT hash;
RAW hash;
Mangle hash;
zone resolution;
interface-list membership;
rp-filter;
active route observations.


---

36.1. Runtime verification

Для BALANCED або MIXED, коли uplinks мають окремі routing tables:

один ROUTER_PING probe на кожну required table.

Для failover в одній routing table:

current active path probe обов’язковий;

Controller не вимикає primary WAN;

Controller не додає temporary route;

inactive backup path не тестується примусовим переключенням;

його configuration dependency hashes повинні залишатися незмінними.



---

36.2. Operational route change

Зміна active primary/backup route під час staging не змінює artifact.

Після activation:

якщо required active-path probe проходить — verification може продовжитись;

якщо topology стала incompatible з plan — rollback;

Controller не намагається повернути WAN у попередній стан.



---

37. VRRP deployment

VRRP deployment є recoverable pseudo-transaction, а не справжньою distributed transaction.

Алгоритм:

LOCK NODE
→ PRECHECK ALL MEMBERS
→ STAGE ALL MEMBERS
→ VERIFY ALL ARTIFACTS
→ ARM ALL WATCHDOGS
→ READ ROLE VECTOR
→ ACTIVATE MEMBERS IN ORDER
→ VERIFY EACH MEMBER
→ VERIFY WHOLE NODE
→ DISARM ALL WATCHDOGS
→ COMMIT


---

38. VRRP member classification

STANDBY_ONLY
TRAFFIC_BEARING

STANDBY_ONLY:

не є MASTER для жодного relevant VRRP instance;

не має доведеного independent routed traffic.


Усі інші Device:

TRAFFIC_BEARING

Unknown classification прирівнюється до TRAFFIC_BEARING.


---

39. VRRP activation order

1. STANDBY_ONLY members.
2. TRAFFIC_BEARING members.

Traffic-bearing members сортуються:

кількість MASTER instances ascending
→ Device ID

Перед activation кожного member Controller повторно читає role vector.


---

40. VRRP role change

У v1 діє проста fail-closed policy.

До першої activation

Role vector змінився:

re-read
→ rebuild member order
→ continue, якщо topology consistent

Після першої activation

Будь-яка зміна role vector:

VRRP_ROLE_CHANGED_DURING_DEPLOYMENT
→ rollback усіх activated members

Roll-forward не виконується.


---

41. VRRP member failure

До activation

Недоступний member:

deployment BLOCKED

Після часткової activation

1. Зупинити подальшу activation.
2. Rollback reachable activated members.
3. Не деактивувати watchdog недоступних members.
4. Очікувати їх local watchdog rollback.
5. Перевірити cluster state після відновлення.

Якщо не можна довести однаковий old state:

RECOVERY_REQUIRED


---

42. VRRP node verification

Після activation всіх members:

1. Усі physical management addresses доступні.


2. Усі anchors вказують на new targets.


3. Усі artifacts мають expected resource hash.


4. Усі watchdog entries активні.


5. VRRP configuration не змінилася.


6. Role vector не змінився від activation baseline.


7. Немає FAILURE state.


8. Немає неочікуваного multiple-master state.


9. VRRP control-plane system tests залишаються valid.


10. Required Router ping probes проходять.


11. Remaining watchdog TTL достатній для disarm.




---

43. Watchdog disarming

Disarming виконується лише після повного verification.

Для кожного Device:

1. Перевірити remaining TTL >= commit margin.
2. Знайти обидва scheduler entries за name.
3. Перевірити їх source/script binding.
4. Set disabled=yes для deadline scheduler.
5. Set disabled=yes для startup scheduler.
6. Повторно прочитати обидва entries.
7. Вимагати disabled=yes.
8. Перевірити anchors досі вказують на new targets.
9. Позначити WATCHDOG_DISARMED.

Для VRRP Node disarming виконується паралельно, але не більше однієї write operation на Device.


---

44. Watchdog cleanup

Після підтвердженого disabled=yes:

1. Remove deadline scheduler.
2. Remove startup scheduler.
3. Remove rollback script.

Cleanup є idempotent.

Втрата connection після підтвердженого disabling:

не викликає rollback;

створює warning WATCHDOG_CLEANUP_INCOMPLETE;

не залишає active scheduler.


COMMITTED не потребує доведеного фізичного видалення resources, але потребує доведеного disabled=yes.


---

45. Commit

Node отримує COMMITTED лише коли:

усі Device verified;
усі anchors new;
усі artifacts exact;
усі critical probes passed;
усі watchdog schedulers disabled;
VRRP state consistent, якщо applicable;
deployment lock чинний.

Після цього одна PostgreSQL transaction:

1. Зберігає post-deployment snapshots.
2. Оновлює active artifact references.
3. Оновлює actual deployed policy hash.
4. Записує COMMITTED state.
5. Створює audit event.


---

46. Controller-initiated rollback

Rollback order:

1. Device — reverse activation order.
2. Anchor — plan-defined rollback order.

На кожному Device:

1. Прочитати current anchors.
2. Вимагати target ∈ {old, new}.
3. Для кожного new target:
       set old target.
4. Повторно прочитати всі anchors.
5. Вимагати exact old target set.
6. Перевірити old artifact hash.
7. Відкрити нову API-SSL connection.
8. Виконати old-state probes.
9. Disable watchdog schedulers.
10. Cleanup watchdog resources.

Новий detached artifact не видаляється.


---

47. Watchdog-initiated rollback

Після reconnect Controller визначає watchdog rollback за фактичним станом:

anchors == old targets
AND deployment не committed

run-count використовується лише як додаткова діагностика, оскільки RouterOS скидає його після reboot. 

Controller:

1. Перевіряє old artifact.
2. Перевіряє management connection.
3. Disable/remove watchdog resources.
4. Зберігає post-rollback snapshot.
5. Позначає ROLLED_BACK.


---

48. Partial rollback

Якщо anchors є сумішшю:

old targets;
new targets;

і всі вони відповідають поточному deployment plan:

Controller завершує rollback до old.

Якщо хоча б один anchor має третій target:

RECOVERY_REQUIRED

Controller не змінює такий anchor автоматично.


---

49. Crash recovery

Після startup Controller шукає всі nonterminal deployments.

49.1. До activation

Якщо:

усі anchors old

то:

deployment позначається FAILED або CANCELED;

watchdog resources, якщо є, деактивуються;

detached artifacts залишаються;

наступний deployment може їх reuse.


49.2. Після activation

Якщо deployment не має durable COMMITTED:

recovery policy = rollback old artifact

Навіть коли всі anchors уже new.

Це виключає неоднозначне рішення після crash.

49.3. Crash після watchdog disabling, до DB commit

Controller після restart:

rollback old artifact

Це може скасувати технічно успішний deployment, але зберігає однозначну semantics:

> тільки durable COMMITTED означає прийнятий новий стан.




---

50. Recovery decision table

Actual anchors	Watchdog	DB state	Дія

Усі old	Будь-який	Nonterminal	Mark rolled back/failed
Усі new	Active	Nonterminal	Rollback
Усі new	Disabled	Nonterminal	Rollback
Old/new mix	Active	Nonterminal	Complete rollback
Old/new mix	Disabled	Nonterminal	Complete rollback
Неочікуваний target	Будь-який	Будь-який	Recovery required
Усі new	Disabled	Committed	Keep new
Усі old	Disabled	Committed	Critical drift



---

51. Невизначений результат API write

Add

read deterministic resource identity

Результат:

exact resource існує → success;

resource відсутній → retry allowed;

divergent resource → collision.


Set anchor

read jump-target

target new → success;

target old → retry allowed;

target other → recovery required.


Disable scheduler

read disabled

yes → success;

no → retry, якщо margin достатній;

missing → already cleaned;

divergent identity → recovery required.


Remove

read resource by exact name

absent → success;

exact resource present → retry;

divergent resource → collision.



---

52. Manual RouterOS changes

Controller не може фізично заборонити паралельну WinBox, CLI або API session.

Тому перед кожною effectful phase повторно перевіряються:

configuration hash;
anchor context;
management guard;
artifact resources;
watchdog resources;
VRRP configuration;
zone dependencies.

При ручній зміні:

CONCURRENT_ROUTEROS_CHANGE

Deployment:

не продовжується;

переходить у rollback, якщо activation уже почалася;

не перезаписує ручну зміну автоматично.



---

53. Error model

Preconditions

DEPLOYMENT_PLAN_EXPIRED
DEPLOYMENT_PLAN_STALE
DEPLOYMENT_LOCKED
DEVICE_UNREACHABLE
DEVICE_FLAGGED
SCHEDULER_DISABLED_BY_DEVICE_MODE
MANAGEMENT_GUARD_INVALID
ANCHOR_INVALID
OLD_ARTIFACT_INVALID
DRIFT_CONFLICT

Staging

STAGING_RESOURCE_COLLISION
STAGING_PREFIX_DIVERGED
STAGING_ARTIFACT_HASH_MISMATCH
STAGING_RULE_INVALID
STAGING_LIMIT_EXCEEDED

Watchdog

WATCHDOG_SCRIPT_COLLISION
WATCHDOG_SCRIPT_INVALID
WATCHDOG_SCHEDULER_COLLISION
WATCHDOG_ARM_FAILED
WATCHDOG_DEADLINE_TOO_CLOSE
WATCHDOG_DISABLE_FAILED
WATCHDOG_CLEANUP_INCOMPLETE

Activation

ANCHOR_PRECONDITION_FAILED
ANCHOR_SET_FAILED
ANCHOR_READBACK_FAILED
TRANSITION_STATE_UNSAFE
CONCURRENT_ROUTEROS_CHANGE

Verification

MANAGEMENT_RECONNECT_FAILED
ACTIVE_ARTIFACT_HASH_MISMATCH
DEPLOYMENT_PROBE_FAILED
DEPLOYMENT_PROBE_INCONCLUSIVE
VRRP_ROLE_CHANGED_DURING_DEPLOYMENT
VRRP_STATE_INVALID
MULTIWAN_DEPENDENCY_CHANGED

Rollback

ROLLBACK_ANCHOR_UNEXPECTED
ROLLBACK_SET_FAILED
ROLLBACK_ARTIFACT_INVALID
ROLLBACK_MANAGEMENT_FAILED
RECOVERY_REQUIRED


---

54. Timeouts і bounds

Операція	Limit

Node deployment lock heartbeat	5 s
RouterOS connect	5 s
Login	10 s
Read command	30 s
Single write	15 s
Write read-back	15 s
Staging Device	120 s
Activation anchor	15 s
Management reconnect	20 s
Router ping probe	10 s
Watchdog disable	15 s
Rollback Device	60 s
Global simultaneous Device staging	8
Concurrent writes per Device	1
API write retries	1 після actual-state read
Deployment recovery attempts	3


Жоден timeout не може бути infinite.


---

55. Security requirements

1. Writer credentials відокремлені від read-only credentials.


2. Writer credentials не передаються Desktop.


3. Writer commands мають compile-time paths.


4. Write attributes мають command-specific allowlists.


5. Filter set не приймає fields, крім jump-target.


6. Script source генерується fixed compiler.


7. User text не потрапляє у script.


8. dont-require-permissions=yes заборонений.


9. Script не виконує network operations.


10. Script не має sensitive policy.


11. Watchdog resources мають deterministic names.


12. Deployment reason і ticket зберігаються лише в audit.


13. Raw command sentence не логуються.


14. Script source не виводиться у звичайні logs.


15. RouterOS trap проходить sanitization.


16. Start deployment потребує Deployer.


17. High/Critical policy має чинне required approval.


18. Start request містить exact plan hash.


19. Recovery operations створюють audit events.


20. Немає API для довільного manual command execution.




---

56. Audit

Audit події:

deployment.plan.created
deployment.started
deployment.precheck.passed
deployment.precheck.blocked

deployment.staging.started
deployment.resource.reused
deployment.resource.created
deployment.staging.verified

deployment.watchdog.armed
deployment.anchor.activated
deployment.verification.passed
deployment.verification.failed

deployment.watchdog.disabled
deployment.committed

deployment.rollback.started
deployment.anchor.restored
deployment.rollback.completed
deployment.watchdog.executed

deployment.recovery.started
deployment.recovery.required

Audit payload містить:

deployment ID;
Node/Device ID;
plan hash;
old/new artifact hashes;
anchor marker;
before/after target;
probe result;
error code;
actor;
correlation ID.

Не містить:

credentials;
script source;
firewall address contents;
raw RouterOS sentences;
raw trap text.


---

57. Unit tests

Обов’язкові domains:

deployment state machine;
plan hashing;
lock lifecycle;
step journal;
address-list create-or-verify;
chain prefix recovery;
artifact verification;
watchdog source generation;
watchdog identity;
watchdog timing;
anchor activation;
unknown write outcome;
probe evaluation;
rollback order;
recovery decision table;
VRRP member ordering;
VRRP role-change handling.


---

58. CHR integration tests

standalone successful deployment;
standalone no changes;
standalone management-blocking candidate;
partial address-list staging;
partial chain staging;
staging reconnect;
anchor set reconnect;
watchdog deadline rollback;
watchdog startup rollback;
Controller crash before activation;
Controller crash after first anchor;
Controller crash after all anchors;
Controller crash during verification;
Controller crash during watchdog disarm;
rollback after failed ping;
manual anchor modification;
manual staged-chain modification;
scheduler disabled by device-mode;
flagged Device;
multi-WAN active route change;
VRRP active/passive;
VRRP split-master;
VRRP role change during activation;
VRRP member disconnect;


---

59. Fault-injection points

Connection примусово розривається:

після DB step intent;
перед address-list add;
після add, до reply;
після reply, до read-back;
після останньої staged rule;
після rollback script add;
після першого scheduler add;
після watchdog arm;
перед anchor set;
після anchor set, до reply;
після першого anchor;
після останнього anchor;
під час new management reconnect;
під час ping;
після першого scheduler disable;
після всіх scheduler disables;
перед DB commit;
після DB commit.

Для кожної fault point доводиться один terminal outcome:

old state;
new committed state;
recovery required з точним діагнозом.


---

60. Watchdog-specific tests

anchor old → no-op;
anchor new → restore old;
old/new mixed → restore all old;
unknown target → no writes;
missing anchor → no writes;
duplicate anchor marker → no writes;
wrong chain → no writes;
wrong action → no writes;
startup execution;
deadline execution;
script source tampered;
scheduler points to another script;
scheduler disabled before commit;
run-count reset after reboot;
stale watchdog після нового deployment;

Stale watchdog не повинен відкотити новий deployment, оскільки його expected new target не збігається з target пізнішого artifact.


---

61. VRRP fault tests

усі members staged;
один member staging failed;
один watchdog arm failed;
role vector змінився до activation;
role vector змінився після standby activation;
member став unreachable після activation;
split-master activation;
watchdog rollback на одному member;
watchdog rollback на всіх members;
Controller crash під час parallel disarm.


---

62. Acceptance criteria

Специфікація реалізована лише коли:

1. Deployment target — один Node.


2. Відсутня campaign logic.


3. Writer має закритий command allowlist.


4. Відсутній generic RouterOS writer.


5. Active rules не редагуються.


6. Filter move не використовується.


7. Звичайні filter rules не видаляються deployment path.


8. Detached staging не впливає на active traffic.


9. Address-list staging є create-or-verify.


10. Chain staging підтримує exact-prefix recovery.


11. Divergent staged resource не виправляється автоматично.


12. Artifact read-back hash збігається до activation.


13. Старий artifact перевірений до deployment.


14. Watchdog обов’язковий.


15. Safe Mode не використовується.


16. Watchdog має deadline і startup trigger.


17. Watchdog script генерується fixed compiler.


18. Watchdog не використовує user text.


19. Watchdog змінює лише MFC anchors.


20. Watchdog перевіряє current target.


21. Stale watchdog не відкотить пізніший artifact.


22. Усі Device watchdogs armed до VRRP activation.


23. Anchor змінюється лише після exact precondition read.


24. set завжди виконується за фактичним .id.


25. Кожний write має read-back.


26. Unknown add outcome не повторюється blind.


27. Unknown anchor-set outcome перевіряється читанням.


28. Transition states попередньо проаналізовані.


29. Нове API connection відкривається після activation.


30. Стара established session не є достатнім verification.


31. Critical failed probe запускає rollback.


32. Forced WAN failover не виконується.


33. Current active WAN не впливає на artifact.


34. Multi-WAN dependencies повторно перевіряються.


35. Current VRRP role не впливає на artifact.


36. VRRP standby members активуються першими.


37. Role change після першої activation запускає rollback.


38. Roll-forward recovery відсутній.


39. Nonterminal deployment після crash rollback-иться.


40. Лише durable COMMITTED зберігає new state.


41. Watchdog disabled на всіх Device до commit.


42. Watchdog cleanup є idempotent.


43. Partial VRRP success не є committed.


44. Unexpected anchor target не переписується автоматично.


45. Controller не змінює management guard.


46. Controller не змінює NAT/RAW/Mangle/routing/VRRP.


47. Усі write operations bounded.


48. Fault injection не залишає невизначений internal state.


49. Audit відтворює кожну effectful operation.


50. Build і tests не змінюють Git working tree.




---

63. Уточнення попередніх специфікацій

Попереднє рішення	Нормативне уточнення

OOB може замінити watchdog	У v1 watchdog обов’язковий
Safe Mode згадувався як додатковий механізм	Не використовується
Можливий roll-forward після role change	У v1 завжди rollback
Watchdog як один scheduler	Використовується script + deadline scheduler + startup scheduler
Watchdog commit через видалення	Достатньо доведеного disabled=yes; видалення є cleanup
Multi-node campaign	Поза цією специфікацією
Runtime verification із багатьма probe types	Залишено лише API-SSL і bounded Router ping
Forced backup-WAN verification	Не виконується
Staging divergence може очищатися	Автоматичне destructive cleanup заборонене
Recovery може продовжити activation	Nonterminal state rollback-иться



---

64. Результат етапу

Після реалізації Safe Deployment v1 система матиме повний мінімальний production path:

approved policy
    ↓
compiled immutable artifact
    ↓
exact precondition validation
    ↓
detached create-or-verify staging
    ↓
local deadline/reboot watchdog
    ↓
bounded anchor switching
    ↓
new management connection
    ↓
Node-specific verification
    ↓
commit або deterministic rollback

Наступний необхідний нормативний документ:

MikroTik Firewall Controller
Managed Device Onboarding and Anchor Bootstrap Specification v0.1

Він має визначити лише prerequisite-ресурси для deployment:

management guard verification;
bootstrap root chains;
permanent anchors;
initial old targets;
RouterOS deployment account requirements;
scheduler capability check;
onboarding rollback;
перехід Device у стан MANAGED.