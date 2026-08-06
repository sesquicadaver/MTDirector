Аналіз routing має бути окремим базовим контуром, а не побічною частиною multi-WAN або prerouting.

Оновлений пріоритет:

1. Network configuration control
2. Routing assurance
3. Packet-path assurance
4. Latency and reachability assurance
5. Endpoint mobility
6. Wazuh/Suricata correlation
7. Incident enforcement

1. Оновлена структура M7

M7.1 Network Rule and Routing Assurance
    firewall
    RAW/Mangle/NAT
    VLAN/bridge
    routing tables
    routing rules
    VRF
    dynamic routing
    route selection
    recursive next-hop resolution
    ECMP
    hardware offload
    latency/reachability

M7.2 Endpoint Presence and Mobility
    endpoint identity
    current Site/Node/VLAN/VRF
    migration history
    concurrent presence
    location loss

M7.3 Wazuh–Suricata Correlation
    event normalization
    endpoint attribution
    route active at event time
    sensor observation point
    packet-path reconstruction

M7.4 Incident Enforcement
    restrictive overlay
    existing approval
    existing compiler
    safe deployment
    rollback

Окремий deployable-компонент для routing не потрібний.

2. Routing як авторитетний домен

Для кожного Device формується:

RoutingAssuranceState {
    configuration
    operational_state
    route_expectations
    route_findings
    resolution_traces
}

Configuration

routing tables
routing decision order
routing rules
VRF definitions
VRF interface bindings
static routes
route filters
routing protocol configuration
BFD/check-gateway configuration
DHCP/VPN-generated route dependencies
L3HW configuration

Operational state

active routes
inactive routes
selected best routes
ECMP next-hop sets
recursive gateway resolution
immediate gateway
egress interface
gateway reachability
dynamic route origin
routing protocol sessions
hardware-offloaded routes
current default routes

RouterOS FIB використовує source address, destination address, source interface і routing mark. Спочатку застосовується policy-routing logic, далі виконується lookup у вибраній таблиці; за однаково кращих маршрутів RouterOS може сформувати ECMP route. Порядок routing-decision stages конфігурується через /routing/settings policy-rules, тому його потрібно читати й аналізувати, а не вважати сталим. 

3. Обов’язкові read-only секції

/routing/table
/routing/settings
/routing/rule

/ip/vrf

/ip/route
/ipv6/route

/routing/filter/rule
/routing/filter/select-rule

За наявності відповідних протоколів додатково читаються лише їхня конфігурація та стан сесій:

BGP
OSPF
RIP
BFD
VPN-derived routes
DHCP-client routes

Controller не повинен керувати routing у поточному write-domain.

VRF не є лише міткою: кожний активний VRF має пов’язану routing table, а overlapping prefixes у різних VRF є різними маршрутними просторами. 

4. Routing decision trace

Для кожного критичного потоку, probe або інциденту формується:

RouteResolutionTrace {
    family

    source_address
    destination_address
    ingress_interface?
    initial_vrf?
    routing_mark?

    routing_decision_order[]

    matched_mangle_rule?
    matched_routing_rule?
    routing_rule_action?

    selected_vrf
    selected_table

    matched_prefix
    route_candidates[]
    selected_routes[]

    recursive_resolution[]
    immediate_next_hops[]

    egress_interfaces[]
    preferred_source?

    decision:
        LOCAL_DELIVERY |
        FORWARD |
        BLACKHOLE |
        PROHIBIT |
        UNREACHABLE |
        NO_ROUTE |
        INDETERMINATE

    execution_path:
        CPU |
        HARDWARE |
        MIXED |
        INDETERMINATE

    certainty
}

Це має бути основою packet-path analysis:

RAW prerouting
→ connection tracking
→ Mangle prerouting
→ dstnat
→ routing policy decision
→ routing table lookup
→ recursive next-hop resolution
→ INPUT або FORWARD

Саме routing decision визначає, чи пакет буде локально доставлений, відкинутий або відправлений через конкретний next hop та interface. 

5. Аналіз policy routing

Необхідно враховувати одночасно:

routing-mark із Mangle
/routing/rule
/routing/settings policy-rules
VRF lookup
local lookup
main-table fallback

У RouterOS Mangle routing mark може мати вищий пріоритет за звичайні routing rules. Якщо marked traffic успішно resolve-иться у відповідній таблиці, наступні user routing rules можуть його взагалі не побачити. 

Тому finding:

MANGLE_ROUTING_MARK_PRESENT

недостатній. Потрібний точний результат:

packet
→ matched Mangle rule
→ assigned routing mark
→ selected table
→ selected route
→ next hop

6. Аналіз routing rules

Для кожної ordered routing rule аналізуються:

source address
destination address
ingress interface
routing mark
min-prefix
action
target table
chain
disabled/inactive state
ordinal

Actions повинні моделюватися окремо:

LOOKUP
LOOKUP_ONLY
DROP
UNREACHABLE

lookup може перейти до наступного decision rule, якщо маршрут у таблиці не знайдений; lookup-only завершує lookup невдачею. Routing rule також сама може повернути drop або unreachable, тобто фактично відкинути traffic ще до filter chain. 

7. Аналіз route selection

Для кожного destination prefix потрібно розрізняти:

усі candidate routes
selected best route
active route
ECMP group
inactive alternatives

Аналізуються:

destination prefix
routing table
route source/protocol
distance
scope
target-scope
gateway
immediate gateway
preferred source
check-gateway
disabled
active/inactive
blackhole
prohibit
unreachable
ECMP
suppress-hw-offload
hw-offloaded

Наявність route у RIB не означає, що саме вона використовується для forwarding. Для packet-path потрібен фактичний FIB result.

8. Recursive routing

Для кожної recursive route будується ланцюг:

destination route
→ configured gateway
→ resolving route
→ наступний gateway
→ connected route
→ physical interface

RecursiveResolutionStep {
    table
    target
    resolving_prefix
    scope
    target_scope
    next_hop
    interface
    active
}

Loop, unresolved gateway або невідповідність scope/target-scope створюють blocker:

ROUTE_RECURSION_LOOP
ROUTE_GATEWAY_UNRESOLVED
ROUTE_SCOPE_MISMATCH

RouterOS використовує scope і target-scope для обмеження маршрутів, якими дозволено resolve-ити recursive gateway. 

9. ECMP

Для ECMP аналізується весь набір next hops:

ECMPRouteSet {
    destination
    table
    next_hops[]
    active_next_hops[]
    hardware_offloaded_next_hops[]
    hashing_context
}

Packet-path result для ECMP:

ONE_OF {
    gateway A,
    gateway B,
    gateway C
}

Якщо неможливо визначити конкретний next hop для заданого flow, Controller не повинен вигадувати один маршрут. Він повертає bounded set можливих шляхів.

Для hardware routing потрібно окремо враховувати, що switch chip може підтримувати менше ECMP paths, ніж RouterOS FIB; тоді лише частина next hops може бути offloaded, а решта оброблятиметься CPU. 

10. Dynamic routing

Dynamic protocol не потрібно перетворювати на окрему керовану підсистему. Але для пояснення routing table необхідно знати:

route origin:
    CONNECTED
    STATIC
    DHCP
    VPN
    BGP
    OSPF
    RIP
    OTHER

protocol session state
route attributes, що впливають на selection
routing filter chain
last route-state transition

Routing filters є критичною частиною routing semantics, оскільки вони можуть приймати, відкидати або змінювати route attributes до selection. Їхня зміна повинна анулювати routing analysis і невиконаний deployment plan. 

Повна BGP-таблиця не повинна безумовно завантажуватися в Desktop. Зберігаються:

повний active FIB;
configuration routes;
route summary/hash по таблиці;
детальні competing candidates для:
    critical prefixes;
    default routes;
    management paths;
    міжфіліальних мереж;
    Wazuh/Suricata paths;
    incident flows.

11. Route expectations

Для контролю, а не лише спостереження, потрібні declarative assertions:

RouteExpectation {
    node_id
    family

    source_zone?
    source_address?
    destination_prefix

    expected_vrf?
    expected_table?

    allowed_next_hops[]
    allowed_egress_zones[]
    allowed_egress_interfaces[]

    required_route_types[]
    forbidden_route_types[]

    require_cpu_firewall_path
    require_reverse_path

    critical
}

Приклади:

MGMT → Wazuh:
    table = main
    egress = WAN_PRIMARY або WAN_BACKUP
    blackhole заборонений
    reverse path required

BRANCH_LAN → HQ:
    VRF = corp
    table = corp
    egress = IPsec/WireGuard
    internet WAN заборонений

GUEST → corporate networks:
    routing decision = unreachable або drop

12. Reverse-path analysis

Для stateful firewall і asymmetric multi-WAN потрібно аналізувати обидва напрямки:

forward route:
    A → B

reverse route:
    B → A

Результат:

SYMMETRIC
ASYMMETRIC_EXPECTED
ASYMMETRIC_UNEXPECTED
REVERSE_PATH_MISSING
INDETERMINATE

Це безпосередньо впливає на:

connection tracking
invalid-state rules
FastTrack
rp-filter
management reconnect
Suricata sensor visibility
latency probes

13. Routing і latency

Latency profile повинен бути прив’язаний не лише до destination, а до routing result:

NetworkPathProfile {
    source_device
    source_address?
    source_interface?
    routing_table?
    vrf?

    destination

    expected_route_prefix
    expected_next_hops[]
    expected_egress_interfaces[]
    expected_execution_path

    max_loss
    max_rtt
    max_jitter
    max_regression
}

Перевірка:

route resolution
→ route expectation
→ ping у конкретній table/VRF
→ latency result

Якщо RTT змінився разом зі зміною next hop, finding має бути:

ROUTE_PATH_CHANGED_WITH_LATENCY_REGRESSION

а не просто LATENCY_HIGH.

14. Routing drift

Configuration drift

routing table created/removed/disabled
FIB flag changed
routing decision order changed
routing rule changed/reordered
VRF binding changed
static route changed
route filter changed
protocol configuration changed
check-gateway changed
scope/target-scope changed
suppress-hw-offload changed

Operational routing change

active route changed
gateway became unreachable
ECMP member changed
dynamic best path changed
protocol session changed
route moved CPU ↔ hardware
default WAN changed

Operational change не завжди є configuration drift, але може:

порушити RouteExpectation;
анулювати incident assessment;
змінити latency baseline;
заблокувати deployment verification.

15. Routing і міграція endpoint

При переході ноутбука між філіями потрібно перераховувати:

Endpoint
→ current Site
→ current VLAN/VRF
→ source routing context
→ route до corporate services
→ route до Wazuh
→ route до internet
→ reverse path

Міграція створює новий EndpointPresenceInterval і новий routing context.

EndpointRoutingContext {
    endpoint_id
    presence_id

    site_id
    node_id
    vlan_id
    vrf
    source_address

    corporate_route_trace
    internet_route_trace
    wazuh_route_trace

    valid_from
    valid_until?
}

Якщо endpoint має активний інцидент, міграція:

анулює старий ResponseAssessment;
перебудовує route trace;
визначає новий enforcement Node;
не запускає deployment автоматично.

16. Routing і Wazuh/Suricata

Для кожного Suricata flow потрібно зіставити:

sensor observation point
→ packet tuple
→ Site/VRF
→ prerouting transformations
→ selected routing table
→ route/next hop
→ filter chain
→ egress path

Це дозволяє відрізнити:

packet побачений до routing decision;
packet побачений після dstnat;
packet пішов через інший WAN;
packet пішов через VPN/VRF;
packet обійшов очікуваний sensor;
packet був hardware-offloaded.

Incident response target визначається не просто за Site, а за Node, який реально контролює route для поточного endpoint presence.

17. Оновлений головний принцип

Network configuration defines possible paths.

Routing tables and policy routing select the actual L3 path.

Packet-processing rules transform and filter traffic along that path.

Latency confirms operational quality of the selected path.

Endpoint mobility changes the source routing context.

Wazuh and Suricata correlate incidents with the route active at event time.

Enforcement applies only where the selected path is provably controlled.

Отже, M7.1 потрібно перейменувати на:

Network Rule, Routing and Path Assurance

Routing table analysis стає обов’язковим центральним доменом, а не додатковою перевіркою multi-WAN.
