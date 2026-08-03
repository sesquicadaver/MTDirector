Зафіксовано. Контейнери, VLAN, VETH, bridge, VRF та hardware offload мають входити до topology- і packet-path analysis, але не стають окремими керованими підсистемами.

Оновлений шлях аналізу

Джерело трафіку
    ↓
physical interface / VLAN / VETH / bridge port
    ↓
bridge VLAN admission:
    PVID
    tagged/untagged membership
    ingress filtering
    ↓
рішення: CPU path або hardware-offloaded path
    ↓
RAW prerouting
    ↓
connection tracking / notrack
    ↓
Mangle prerouting
    ↓
dstnat
    ↓
VRF / routing table / routing decision
    ↓
Filter INPUT або FORWARD
    ↓
Mangle/NAT postrouting — лише як залежність вихідного трафіку

RouterOS-контейнер отримує мережеве підключення через VETH, який може бути включений у bridge; один VETH може використовуватись декількома контейнерами. Контейнер також може бути безпосередньо підключений до спільної L2-мережі. 

Що додається до read-only inventory

До allowlist читання додаються лише мережево значущі секції:

/container/print
/app/print                     — якщо підсистема Apps доступна
/interface/veth/print
/interface/vlan/print
/ip/vrf/print

/interface/bridge/print
/interface/bridge/port/print
/interface/bridge/vlan/print

/interface/ethernet/switch/print
/interface/ethernet/switch/port/print
/interface/ethernet/switch/l3hw-settings/print

Не читаються:

container environment variables
mount contents
container shell
filesystem contents
application secrets
container logs

RouterOS Apps можуть автоматично створювати VETH, додавати його до bridge, створювати NAT і port-forwarding rules. Тому фактичні RouterOS resources залишаються джерелом істини, а /app використовується лише для визначення походження таких ресурсів. 

Topology projection

У snapshot формується граф:

Container/App
    ↓ uses
VETH
    ↓ member of
Bridge
    ↓ VLAN membership
Bridge VLAN / PVID / tagged / untagged
    ↓ optionally routed through
VLAN interface
    ↓ assigned to
VRF / routing table
    ↓ affected by
RAW / Mangle / NAT / Filter

Не можна припускати:

1 container = 1 VETH
1 VLAN = 1 interface
bridge traffic = IP firewall traffic
routed traffic = CPU/firewall traffic

Контейнерні потоки

Для контейнерів аналізуються щонайменше три випадки.

Опублікований контейнерний сервіс

WAN
→ RAW prerouting
→ Mangle prerouting
→ dstnat
→ routing decision
→ Filter FORWARD
→ container bridge/VETH

RouterOS-документація використовує саме dstnat для перенаправлення зовнішнього порту на адресу контейнера за VETH. 

Analyzer повинен зберігати:

original destination address/port
translated destination address/port
target container/VETH
target bridge/VLAN
resulting FORWARD verdict

Вихід контейнера

VETH
→ bridge/VLAN
→ RAW prerouting
→ Mangle prerouting
→ routing
→ Filter FORWARD
→ srcnat/postrouting dependency
→ WAN

srcnat аналізується як залежність доступності, але Controller його не змінює.

Контейнер у спільному L2-сегменті

Якщо VETH безпосередньо включений у LAN bridge, контейнер доступний іншим учасникам цього L2-сегмента без обов’язкового port forwarding. Такий трафік може не проходити routed IP firewall. 

Тому Controller не має права заявляти, що managed FORWARD policy ізолює такі контейнери, доки CPU/IP-firewall packet path не доведений.

VLAN

Потрібно розрізняти:

VLAN interface
    L3 endpoint RouterOS

Bridge VLAN table
    L2 tagged/untagged forwarding

Bridge CPU port
    доступ VLAN до самого RouterOS

Physical switch port
    фактичний ingress/egress

Hardware-offloaded VLAN
    traffic може не потрапити до CPU

Bridge VLAN ingress filtering може відкинути frame ще на L2-рівні. Водночас VLAN, якого немає у bridge VLAN table, може бути відкинутий до IP firewall. 

Logical zones продовжують використовувати наявну модель:

Zone CONTAINERS_DMZ
    → VLAN interface vlan120

Zone CONTAINERS_L2
    → bridge interface або interface list

Zone APP_BACKEND
    → VETH set

Окремі сутності ContainerPolicy або VlanPolicy не створюються.

Hardware offload

Для кожної пари ingress/egress analyzer повинен визначити:

CPU_FIREWALL_PATH
HARDWARE_OFFLOADED_PATH
MIXED_PATH
INDETERMINATE

На пристроях із L3 hardware offload routed traffic може оброблятися switch chip і не проходити CPU/firewall. RouterOS також підтримує per-VLAN L3HW, тому різні VLAN одного пристрою можуть мати різний packet path. 

Для managed FORWARD policy:

HARDWARE_OFFLOADED_PATH
або
INDETERMINATE

створює blocker:

PACKET_PATH_BYPASSES_IP_FIREWALL
PACKET_PATH_NOT_PROVEN

Controller не вимикає L2/L3 hardware offload автоматично.

Configuration і observations

До configuration hash входять:

container → VETH binding
VETH addresses і gateways
VLAN interfaces
VLAN IDs і parent interfaces
bridge membership
PVID
tagged/untagged membership
vlan-filtering
VRF assignments
L3HW configuration
RouterOS Apps-generated NAT/firewall resources

До observation hash входять:

container running/stopped
VETH running state
current bridge-port state
current hardware-offload state
current active route

Зміна container running state сама по собі не є configuration drift. Зміна VETH, VLAN, bridge membership, VRF, NAT exposure або hardware path анулює:

static analysis
approval context
compiled artifact readiness
невиконаний deployment plan

Межа відповідальності залишається незмінною

Controller:

читає контейнери;
читає Apps;
читає VETH/VLAN/bridge/VRF;
аналізує їхній вплив на firewall;
виявляє drift;
блокує недоведені deployment.

Controller не:

створює або видаляє контейнери;
запускає або зупиняє контейнери;
оновлює images;
змінює Apps;
створює VLAN;
змінює bridge/VLAN table;
змінює VRF;
змінює hardware offload;
створює NAT для контейнерів.

Новий milestone не потрібний. Зміни інтегруються в наявні M1 read adapter, M2 topology/static analysis, M4 deployment preconditions і M6 drift/E2E acceptance.