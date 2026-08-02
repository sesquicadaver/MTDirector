Базове архітектурне рішення

Це має бути не «WinBox для багатьох пристроїв», а топологічно обізнаний контролер політик. Оператор працює з логічним вузлом або філією, а система сама визначає, на які фізичні пристрої та в якій послідовності застосовувати зміни.

┌──────────────────────────────┐
│ Desktop GUI                  │
│ політики, diff, аудит, стан  │
└──────────────┬───────────────┘
               │ mTLS
┌──────────────▼───────────────┐
│ Central Controller Service   │
│                              │
│ • Inventory                  │
│ • Topology model             │
│ • Policy compiler            │
│ • Validator                  │
│ • Deployment coordinator     │
│ • Drift detector             │
│ • RBAC / Audit               │
└──────────────┬───────────────┘
               │ API-SSL через management VPN
      ┌────────┼───────────────┐
      ▼        ▼               ▼
  VRRP-вузол  Multi-WAN      MikroTik CRS
  R1 + R2     один роутер     management firewall
                              та окремо switch ACL

Desktop GUI не повинен напряму зберігати паролі та виконувати розгортання. Авторитетний стан, блокування операцій, журнал і rollback мають бути в центральному сервісі.

1. Модель інфраструктури

Основним об’єктом управління має бути логічний мережевий вузол, а не окремий RouterOS-пристрій.

Тип вузла	Склад	Логіка застосування

VRRP_CLUSTER	Два або більше роутерів	Одна політика для всіх учасників; резервні пристрої активуються першими, поточний master — останнім
SINGLE_MULTI_WAN	Один роутер, кілька WAN	Один набір правил із перевіркою всіх WAN, routing tables, NAT і management-path
STANDALONE	Один роутер, один WAN	Звичайне одиночне застосування
L2_SWITCH	MikroTik CRS у режимі комутації	Firewall для management plane; транзитні ACL — окремий домен
L3_SWITCH	CRS з маршрутизацією	Router firewall плюс capability-aware L3HW/switch ACL


RouterOS VRRP об’єднує кілька роутерів у один логічний Virtual Router. MikroTik рекомендує однакову версію RouterOS для пристроїв з однаковим VRID, тому контролер повинен блокувати або щонайменше позначати розгортання на кластер із різними версіями. 

Мінімальна модель даних:

Site
NetworkNode
Device
DeviceInterface
VrrpGroup
Uplink
AddressObject
ServiceObject
Policy
PolicyBinding
PolicyRevision
Rule
Deployment
DeviceSnapshot
DriftEvent
AuditEvent

Окрема сутність Organization не потрібна: система розрахована на одну компанію.

2. Транспорт до RouterOS

Основний транспорт:

RouterOS native API-SSL
TCP 8729
TLS із перевіркою сертифіката

RouterOS має окремий secure API service на TCP 8729; при призначеному сертифікаті використовується нормальна TLS-сесія. 

Вимоги:

api-ssl доступний лише з management-підмереж або management VPN;

сертифікат перевіряється, заборонено skip verify;

адреси джерел обмежуються одночасно через /ip service address і firewall;

звичайний API 8728 не використовується;

HTTP REST не використовується;

REST/HTTPS залишається резервним адаптером, а не основним транспортом.


RouterOS дозволяє обмежити доступ до сервісу за адресами, але MikroTik окремо рекомендує блокувати недовірені мережі firewall-правилами. 

Для контролера створюється окремий RouterOS-користувач. Його права мають бути мінімально необхідними, але слід враховувати, що RouterOS write є широким правом конфігурації, а не дозволом виключно на firewall. Тому credential контролера фактично є привілейованим секретом. 

3. Декларативна модель політик

Джерелом істини є не набір команд RouterOS, а нормалізована політика:

Policy
 ├── IPv4 filter rules
 ├── IPv6 filter rules
 ├── Address objects
 ├── Service objects
 ├── NAT rules
 ├── RAW rules
 └── Mangle rules

Правило:

Rule {
    id: UUID
    family: IPv4 | IPv6
    facility: FILTER | NAT | RAW | MANGLE
    chain: string
    position: integer
    action: typed action
    match: typed match expression
    logging: optional
    enabled: boolean
    description: string
}

Необхідні рівні політики:

Company baseline
        ↓
Site overlay
        ↓
Logical-node overlay
        ↓
Explicit device exception

Порядок злиття детермінований. Device exception не змінює базову політику, а створює явний override із причиною, автором і строком дії.

RouterOS firewall використовує окремі input, forward та output chains. Address lists можуть повторно використовуватись у filter, NAT і mangle, тому вони мають бути окремими об’єктами політики, а не дубльованими IP-полями в кожному правилі. 

4. Межа відповідальності контролера

Контролер не повинен переписувати весь firewall пристрою.

Він володіє лише:

керованими custom chains;
керованими address lists;
одним або кількома jump anchors;
правилами з власним UUID у comment.

Приклад маркування:

comment="fwctl:rule:550e8400-e29b-41d4-a716-446655440000"

Структура:

input
  ├─ незмінний management guard
  └─ jump -> fwctl.in.active

forward
  └─ jump -> fwctl.fwd.active

output
  └─ jump -> fwctl.out.active

Кожна ревізія створюється в окремих detached chains:

fwctl.fwd.r104
fwctl.fwd.r105

Активація відбувається зміною jump-target, а не масовим редагуванням чинного chain.

Правила без fwctl:-ідентифікатора вважаються unmanaged:

контролер їх читає;

враховує під час аналізу;

показує конфлікти;

не видаляє і не переміщує автоматично.


Невідомі або ще не підтримані RouterOS-параметри зберігаються у raw snapshot, але такі правила не можна переводити в managed-стан без повної типізованої підтримки.

5. Безпечний алгоритм застосування

RouterOS-операції потрібно обгорнути в контрольовану псевдотранзакцію.

Алгоритм

1. Отримати ексклюзивний deployment lock на логічний вузол.


2. Повторно прочитати актуальну конфігурацію всіх пристроїв.


3. Побудувати canonical snapshot і checksum.


4. Порівняти:

останній відомий baseline;

поточну конфігурацію;

бажану ревізію.



5. При сторонніх змінах зупинити deployment як DRIFT_CONFLICT.


6. Скомпілювати нові detached chains.


7. Перевірити фактичний результат повторним читанням із RouterOS.


8. Встановити локальний rollback watchdog або підтвердити наявність незалежного management/OOB-шляху.


9. Перемкнути один jump anchor на нову ревізію.


10. Виконати незалежні probes:

API-SSL;

ICMP до management IP;

доступність контрольного маршруту;

стан VRRP;

стан WAN;

наявність нового revision marker.



11. Після успішної перевірки скасувати watchdog.


12. Стару ревізію видаляти лише після завершення grace period.



RouterOS має history, undo/redo і Safe Mode, але контролер не повинен покладатися на них як на єдиний механізм multi-device rollback. 

Обов’язкові блокування

Deployment забороняється, коли:

змінився VRRP master під час підготовки;

поточний checksum не збігається з checksum плану;

немає rollback-каналу для зміни management firewall;

хоча б один член VRRP-кластера недоступний;

виявлено непідтримуваний параметр у managed chain;

нова політика блокує control-plane address;

не пройдено статичну валідацію;

попередній deployment вузла не завершено.


6. VRRP-вузол

VRRP-кластер відображається в GUI як один deployment target.

Послідовність

DISCOVER MEMBERS
      ↓
STAGE ON ALL MEMBERS
      ↓
VERIFY STAGED REVISION
      ↓
RE-READ VRRP ROLES
      ↓
ACTIVATE BACKUPS
      ↓
VERIFY BACKUPS
      ↓
RE-READ VRRP ROLES
      ↓
ACTIVATE MASTER
      ↓
VERIFY CLUSTER
      ↓
COMMIT

Критичні правила:

усі учасники мають отримати ту саму policy revision;

не можна оновлювати лише поточний master;

роль master/backup читається безпосередньо перед кожною активацією;

зміна master під час deployment призводить до контрольованого abort або повторного планування;

резервні пристрої активуються першими;

master активується останнім;

при помилці всі вже активовані учасники повертаються до попереднього anchor;

кластер не може мати статус COMMITTED, поки ревізія не підтверджена на кожному пристрої.


Стан вузла:

CONSISTENT
STAGING
PARTIALLY_STAGED
ACTIVATING
COMMITTED
DRIFTED
PARTIAL_FAILURE
ROLLING_BACK
MANUAL_RECOVERY_REQUIRED

7. Один роутер із балансуванням або backup WAN

Такий вузол не можна моделювати як кілька роутерів. Це один policy target із кількома uplinks.

Компілятор має використовувати логічні об’єкти:

WAN
WAN_PRIMARY
WAN_BACKUP
WAN_BALANCED
LAN
MGMT
DMZ

Фізичні інтерфейси прив’язуються до них у topology inventory. Правило не повинно без необхідності містити ether1, sfp-sfpplus1 тощо.

Перед deployment перевіряються:

interface lists;

активні routing tables;

recursive failover routes;

policy routing;

NAT для кожного WAN;

mangle/PCC marks;

asymmetric routing;

management route через primary та backup;

залежність management VPN від конкретного WAN.


WAN failover у RouterOS може будуватись через recursive routing, тому валідатор не повинен визначати доступність каналу лише за прапором фізичного інтерфейсу. 

8. MikroTik switches

Switch не можна автоматично трактувати як звичайний router firewall target.

Два окремі контури

Management plane

Правила input захищають RouterOS CPU і management services. Їх можна управляти тим самим policy engine.

Transit plane

Трафік між switch ports може оброблятися switch ASIC і не потрапляти в CPU/IP firewall. Підтримка ACL, hardware offload, VLAN filtering та дії правил відрізняється залежно від switch chip. 

Тому:

router firewall rules не копіюються в switch ACL;

switch ACL має окрему модель політики;

перед застосуванням будується capability profile конкретної моделі та switch chip;

unsupported match/action блокується на етапі компіляції;

зміни, що відключають hardware offload, повинні явно показувати прогнозований performance impact;

у першій версії switch ACL має бути read-only;

management input firewall на CRS підтримується відразу.


9. Валідація правил

До RouterOS не повинна потрапляти політика, що не пройшла:

Структурну перевірку

типи полів;

IPv4/IPv6 family;

допустимість action для facility;

наявність chain;

коректність protocol/port;

наявність address/service/interface objects;

відсутність циклічних jump;

унікальність UUID.


Семантичну перевірку

повні дублікати;

shadowed rules;

unreachable rules після terminal action;

catch-all rule перед management allow;

конфлікт accept/drop для однакового match;

зміна поведінки established/related;

FastTrack-конфлікти;

правила, що ніколи не match;

перетин NAT і filter expectations;

невідповідність між WAN policy та interface topology.


Operational safety

management API не блокується;

management VPN не втрачає маршрут;

VRRP advertisements не блокуються;

health-check traffic не блокується;

deployment не створює тимчасового «вікна» без фільтрації.


10. Drift detection

Перед кожним deployment і періодично у фоні виконується reconciliation:

desired policy
      ↕
last committed snapshot
      ↕
actual RouterOS configuration

Класи drift:

Клас	Реакція

Змінено managed rule	Critical, deployment блокується
Видалено jump anchor	Critical
Змінено management guard	Critical
Додано unmanaged rule	Warning або Critical після semantic analysis
Змінено interface list	Critical, якщо її використовує policy
Змінився VRRP member/role	Topology refresh
Змінився RouterOS version	Capability revalidation
Змінився switch chip/HW state	Switch capability revalidation


Автоматичне «повернення до desired state» без аналізу заборонене: воно може перезаписати аварійне ручне виправлення адміністратора.

11. Безпека контролера

Обов’язково:

паролі RouterOS не зберігаються у desktop-клієнті;

секрети в БД лише в зашифрованому вигляді;

master key захищається механізмом ОС;

TLS certificate pinning або довірена внутрішня CA;

окремі облікові записи для read-only discovery та deployment;

app-level RBAC;

append-only audit;

кожна зміна має автора, причину, ticket/reference та diff;

заборонений прихований force apply;

аварійне перевизначення створює окрему audit event;

export логів не повинен містити credentials або sensitive fields.


Ролі:

Viewer
PolicyEditor
Reviewer
Deployer
Administrator
Auditor

12. Технологічний стек

Раціональний стек:

Компонент	Технологія

Desktop GUI	C#/.NET, Avalonia UI, MVVM
Central controller	ASP.NET Core service
GUI ↔ controller	gRPC через mTLS
Database	PostgreSQL
RouterOS adapter	Власний типізований API-SSL client
Background jobs	Вбудований bounded job scheduler
Secrets	OS-protected encryption key
Packaging	Windows MSI/MSIX; Linux package за потреби
Testing RouterOS	CHR-based integration environment


Власний RouterOS-клієнт доцільний, оскільки потрібні:

контроль TLS;

cancellation;

command tags;

bounded timeouts;

повторне підключення;

deterministic parsing;

capability detection;

відсутність залежності від неактивно підтримуваної сторонньої бібліотеки.


13. Стан deployment

DRAFT
  ↓
VALIDATED
  ↓
APPROVED
  ↓
STAGING
  ↓
STAGED
  ↓
ACTIVATING
  ↓
VERIFYING
  ↓
COMMITTED

Помилки:

VALIDATION_FAILED
DRIFT_CONFLICT
STAGING_FAILED
ACTIVATION_FAILED
VERIFY_FAILED
ROLLING_BACK
ROLLED_BACK
MANUAL_RECOVERY_REQUIRED

Кожний перехід зберігається транзакційно в PostgreSQL.

14. Порядок реалізації

Етап	Результат

1	Inventory, discovery, RouterOS API-SSL client, snapshots
2	Topology model: standalone, multi-WAN, VRRP, switches
3	Read-only firewall import, canonicalization, drift detection
4	Policy objects, IPv4/IPv6 filter compiler, static validator
5	Detached chains, diff, safe deployment і rollback для standalone
6	VRRP deployment coordinator
7	Multi-WAN-aware validation
8	NAT, RAW і mangle як окремі policy domains
9	Switch ACL capability engine


15. Критерії готовності першої production-версії

Система вважається придатною до експлуатації лише коли доведено:

1. Повторне застосування тієї самої ревізії не створює змін.


2. Розрив з’єднання під час активації не залишає вузол без management-доступу.


3. VRRP failover після deployment не змінює firewall policy.


4. Зміна VRRP master під час deployment не створює різних активних ревізій.


5. Ручна зміна managed rule виявляється до наступного deployment.


6. Unmanaged rules не видаляються і не переміщуються.


7. Rollback повертає точний попередній anchor і revision.


8. Жоден credential не потрапляє у GUI, лог або audit payload.


9. Switch policy не застосовується без підтвердженого capability profile.


10. Кожна зміна повністю відтворюється за revision, diff, snapshot та audit trail.


