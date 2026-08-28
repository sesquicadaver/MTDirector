> **ARCHIVED / IMPLEMENTED** — next-2 (incident correlation). Не відкритий блок ТЗ. Див. [`README.md`](README.md). Оригінальний шлях у корені видалено.

Ключове рішення

Контролер не треба перетворювати на SIEM/NDR/SOAR. Його роль в аналітичному комплексі:

авторитетний мережевий контекст
+ аналіз фактичного packet path
+ перевірка можливості реагування
+ безпечне застосування firewall-відповіді
+ підтвердження або rollback

Зовнішній комплекс збирає й корелює події. Контролер отримує нормалізований інцидент або намір реагування, збагачує його даними RouterOS і виконує тільки дозволені firewall-дії.

SIEM / NDR / EDR / SOC
        ↓ IncidentSignal
MikroTik Firewall Controller
        ↓ topology + policy + packet-path analysis
        ↓ ResponsePlan
existing safe deployment
        ↓
ResponseResult
        ↑
SIEM / SOAR

Що додатково потрібно обробляти

Домен	Необхідні дані	Призначення

Події RouterOS	firewall, authentication, configuration change, interface, routing, VRRP, VPN, DHCP, container, reboot, clock	Визначення факту та початкової причини інциденту
Потоки і сесії	IPFIX/NetFlow, connection tracking, NAT tuples, FastTrack/HW-offload flags	Встановлення фактичного network flow і можливості його блокування
Ідентифікація endpoint	IP, MAC, VLAN, bridge port, DHCP lease, VPN identity, container/VETH	Встановлення, який фізичний або логічний об’єкт стоїть за IP
Control plane	VRRP, BGP/OSPF sessions, routes, VPN peers, WAN state	Відокремлення атаки від відмови routing/HA
Network exposure	dstnat, container publishing, VPN routes, VLAN/VRF, interface lists	Визначення фактичної доступності сервісу
Історичний стан	policy hash, artifact hash, topology hash на момент події	Відповідь на питання «чому цей пакет був дозволений»
Якість спостереження	source health, clock offset, gaps, CPU/HW path, confidence	Заборона необґрунтованих висновків
Результат реагування	applied, verified, rolled back, not enforceable, residual risk	Зворотний зв’язок аналітичному комплексу


1. RouterOS logs

RouterOS може передавати системні події на remote syslog, а починаючи з RouterOS 7.18 підтримує CEF і мілісекундні timestamps. Логи слід надсилати безпосередньо в корпоративний collector/SIEM, а не зберігати та індексувати всередині firewall controller. 

Контролеру достатньо отримувати нормалізовані події:

AUTHENTICATION_FAILURE
CONFIGURATION_CHANGED
FIREWALL_MATCH
INTERFACE_STATE_CHANGED
ROUTE_STATE_CHANGED
VRRP_ROLE_CHANGED
VPN_SESSION_CHANGED
DHCP_ANOMALY
CONTAINER_STATE_CHANGED
DEVICE_REBOOTED
CLOCK_CHANGED
RESOURCE_EXHAUSTION

Для managed firewall rules, на яких потрібна event-кореляція, log-prefix повинен містити стабільний RuleId, наприклад:

mfc:r:<rule-uuid>

Логування всіх пакетів не потрібне. Логуються лише визначені security events, бажано в remote collector, а не на локальний NAND.

2. Flow і connection context

IPFIX/Traffic Flow потрібний для:

5-tuple;

напрямку потоку;

обсягу;

TCP flags;

ingress/egress interface;

виявлення сканування, lateral movement і нестандартних outbound connections.


Але RouterOS Traffic Flow бачить тільки трафік, оброблений CPU. Hardware-offloaded bridge traffic у нього не потрапляє. Тому кожний аналітичний висновок повинен мати visibility_status, а не припускати повне покриття. 

Connection tracking слід читати на вимогу для конкретного інциденту, а не постійно копіювати всю таблицю. Потрібні:

protocol
original source/destination/ports
reply source/destination/ports
connection state
srcnat/dstnat
fasttrack
hw-offload
connection mark
routing mark
timeout

RouterOS connection table містить original/reply tuples, NAT, FastTrack і HW-offload flags, що дозволяє встановити, чи може filter policy реально вплинути на вже активну сесію. 

3. Endpoint attribution

Для інциденту IP-адреси недостатньо. Потрібний resolver:

IP
→ MAC
→ VLAN
→ bridge
→ physical port
→ interface
→ VETH/container
→ VPN peer/user
→ Site/Node/Device

Джерела:

DHCP leases
DHCP snooping bindings
ARP
IPv6 Neighbor Discovery
bridge host table
VLAN table
WireGuard/IPsec/PPP active sessions
container/VETH mapping

Bridge host table надає MAC, VLAN ID і physical interface. DHCP snooping binding database додатково пов’язує MAC, IP, VLAN, lease та порт, що є критичним для коректної атрибуції endpoint. 

WireGuard надає current endpoint і last handshake, а IPsec — active peer state, remote address та session counters. Ці дані дозволяють пов’язати подію з конкретним tunnel peer, а не лише із внутрішньою IP-адресою. 

4. Історичний контекст

Подія повинна аналізуватися не відносно поточної конфігурації, а відносно стану, активного в момент події.

Потрібна похідна timeline:

ActiveStateInterval {
    device_id
    valid_from
    valid_until
    policy_hash
    artifact_hash
    configuration_hash
    topology_hash
    certainty:
        PROVEN |
        PARTIAL |
        UNKNOWN
}

Це дозволить встановити:

яка policy була active;
який rule міг match;
де стояв anchor;
які NAT/Mangle/routes діяли;
якою була VLAN/container topology;
чи існував drift.

Без цього комплекс зможе сказати лише «зараз правило виглядає так», але не «чому traffic пройшов о 14:37».

5. Control-plane і service state

Потрібно обробляти runtime state:

VRRP role vector
BGP/OSPF neighbor/session state
active/default routes
DHCP client/server state
IPsec active peers і SAs
WireGuard handshakes
PPP active sessions
container lifecycle
interface link state
SFP/PoE/temperature/resource state
RouterOS reboot і uptime

Це не треба перетворювати на повноцінний NMS. Health metrics можуть надходити з наявної monitoring-системи через нормалізовані events; SNMP-клієнт усередині Controller не є необхідним. RouterOS SNMP уже придатний для зовнішнього моніторингу пристроїв. 

Нормалізований event contract

Мінімальний вхідний контракт:

IncidentSignal {
    event_id: UUID
    source_event_id: string
    occurred_at: UTC
    received_at: UTC

    source_type:
        SIEM |
        NDR |
        EDR |
        IDS |
        ROUTEROS_LOG |
        FLOW_ANALYZER |
        MONITORING

    category: string
    severity: INFO | LOW | MEDIUM | HIGH | CRITICAL
    confidence: 0..100

    site_id: UUID?
    node_id: UUID?
    device_id: UUID?

    entities: EntityReference[]
    flow: FlowTuple?
    original_flow: FlowTuple?
    translated_flow: FlowTuple?

    vlan_id: uint16?
    interface: string?
    vrf: string?
    container_id: string?
    vpn_identity: string?

    indicators: Indicator[]
    evidence_refs: string[]

    deduplication_key: string
    raw_event_ref: string?
}

Controller не довіряє переданим topology або policy hashes. Він сам встановлює історичний стан за occurred_at, device_id і власним audit/deployment timeline.

Мінімальна модель реагування

Для реального реагування поточній policy model бракує одного вузького поняття:

INCIDENT_DENY_OVERLAY

Воно має бути:

тільки restrictive;

тільки DROP;

scoped до одного Node;

прив’язане до incident_id;

з exact selector;

з reason та evidence references;

із граничним строком;

розташоване після PROTECTED_CONTROL_PLANE, але до STATE_PRELUDE;

скомпільоване через наявний compiler;

застосоване через наявний watchdog-protected deployment;

без нових RouterOS write paths.


PROTECTED_CONTROL_PLANE
        ↓
INCIDENT_PRE_STATE_DENY
        ↓
MANDATORY_PRE_STATE_DENY
        ↓
STATE_PRELUDE
        ↓
звичайна policy

Це дозволяє блокувати інцидентний traffic, не змінюючи company/site/node policy і не створюючи універсальний SOAR-механізм.

У MVP одна реакція залишається однією Node deployment operation. Multi-Node campaign не додається.

Завершення TTL не повинно виконувати прихований RouterOS write. Воно створює обов’язковий removal plan, який проходить той самий safe-deployment workflow.

Response intent

Аналітичний комплекс передає не команду RouterOS, а типізований намір:

ResponseIntent {
    incident_id: UUID
    node_id: UUID

    action:
        TEMPORARY_PRE_STATE_DENY |
        REVOKE_TEMPORARY_EXCEPTION |
        RESTORE_COMMITTED_POLICY

    selector: TrafficSelector
    expires_at: UTC?
    urgency: NORMAL | EMERGENCY

    evidence_refs: string[]
    requested_by: Principal
    idempotency_key: UUID
}

Controller повертає:

ResponseAssessment {
    feasibility:
        FULLY_ENFORCEABLE |
        NEW_CONNECTIONS_ONLY |
        NOT_ENFORCEABLE_BY_IP_FILTER |
        INDETERMINATE

    affected_devices
    affected_rules
    packet_paths
    blockers
    expected_effect
    residual_risk
    plan_hash?
}

Обов’язкова оцінка можливості реагування

Фактичний packet path	Результат

CPU filter path, без bypass	FULLY_ENFORCEABLE
Existing FastTracked connection	NEW_CONNECTIONS_ONLY або INDETERMINATE
Routed L3 HW-offload	NOT_ENFORCEABLE_BY_IP_FILTER
L2 traffic у тому самому bridge/VLAN	NOT_ENFORCEABLE_BY_IP_FILTER
Container через routed VETH/FORWARD	FULLY_ENFORCEABLE, якщо path доведений
Невідомий chip/path	INDETERMINATE


Для гарантованого припинення вже наявних FastTracked connections у майбутньому знадобиться окрема, жорстко типізована операція видалення точно визначених connection-tracking entries. До її реалізації Controller не повинен неправдиво заявляти про повне припинення таких сесій.

Зворотний зв’язок

Аналітичний комплекс має отримувати:

RESPONSE_PLANNED
RESPONSE_BLOCKED
RESPONSE_STARTED
RESPONSE_APPLIED
RESPONSE_VERIFIED
RESPONSE_ROLLED_BACK
RESPONSE_RECOVERY_REQUIRED
RESPONSE_EXPIRED

Кожна відповідь містить:

incident_id
node_id
device_ids
policy_hash
artifact_hash
plan_hash
verification results
rollback status
residual risk
correlation_id

Що не потрібно додавати в Controller

Не потрібно реалізовувати:

власне сховище та пошук raw syslog;
власний correlation engine;
власну IOC/threat-intelligence database;
повноцінний NetFlow collector;
packet-capture repository;
SNMP/NMS;
EDR або IDS;
автоматичне рішення «event → block»;
універсальний SOAR;
multi-Node campaign engine.

Нормальна межа відповідальності:

зовнішній комплекс:
    збирає
    корелює
    оцінює
    формує ResponseIntent

MikroTik Firewall Controller:
    збагачує network context
    доводить packet path
    оцінює enforceability
    формує safe plan
    застосовує firewall response
    перевіряє
    rollback-ить
    повертає результат

Отже, обов’язкові наступні доповнення — event integration contract, історичний state resolver, endpoint attribution, on-demand session context, response feasibility та restrictive incident overlay. Інші аналітичні функції повинні залишатися за межами цього проєкту.