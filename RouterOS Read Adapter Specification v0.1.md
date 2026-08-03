MikroTik Firewall Controller

RouterOS Read Adapter Specification v0.1

Дата: 3 серпня 2026 року
Статус: нормативна специфікація M1


---

1. Призначення

Mfc.RouterOs є спеціалізованим read-only адаптером для отримання конфігурації, яка потрібна централізованому контролеру firewall-політик MikroTik.

Адаптер повинен надавати достовірні дані про:

IPv4 та IPv6 firewall;

address lists;

інтерфейси та interface lists;

NAT, RAW і Mangle як залежності firewall;

routing і multi-WAN;

VRRP;

bridge/VLAN;

hardware-offload та switch context;

management services;

версію та можливості RouterOS.


Адаптер не є:

універсальним SDK RouterOS;

CLI-клієнтом;

NMS;

терміналом виконання команд;

засобом конфігурації routing, DHCP, DNS або VPN;

джерелом write-команд для RouterOS.


У M1 production-код адаптера виконує лише:

/login
/cancel
allowlisted /print operations


---

2. Межі відповідальності

Mfc.Application
      │
      │ закритий RouterSectionId
      ▼
Mfc.RouterOs
      │
      │ API-SSL
      ▼
RouterOS

Mfc.Application визначає:

які секції потрібні для capture;

порядок capture phases;

stable-read coordination;

повторні спроби повного capture;

canonicalization;

snapshot composition.


Mfc.RouterOs визначає:

binary framing;

TLS;

authentication;

RouterOS session;

command allowlist;

.proplist;

query words;

typed mapping;

redaction;

command-level limits;

RouterOS error classification.


Адаптер не приймає від Application:

command path
command words
query words
.proplist
RouterOS expression
CLI text
script


---

3. Незмінні інваріанти

1. Підключення виконується лише через API-SSL.


2. Перевірку TLS-сертифіката не можна вимкнути.


3. RouterOS command path є compile-time constant.


4. Усі /print мають явну .proplist.


5. Query words формуються лише command registry.


6. Production-код не містить generic ExecuteCommandAsync(string command).


7. RouterOS credentials ніколи не передаються до Application або Desktop.


8. Login sentence не логуються і не зберігаються.


9. Один session не відновлюється після transport failure.


10. Після втрати connection весь поточний capture attempt відкидається.


11. Відповіді не зіставляються за порядком запитів — лише за .tag.


12. .id не використовується як persistent identity.


13. Attribute order не має семантичного значення.


14. Query word order має семантичне значення.


15. Невідома відповідь не ігнорується.


16. Невідомий RouterOS field не втрачається мовчки.


17. Усі buffers, queues, retries і pending commands обмежені.


18. Адаптер не виконує автоматичний reconnect.


19. Адаптер не виконує network scan.


20. RouterOS 6 authentication flow не підтримується.


21. Адаптер не читає /export.


22. Адаптер не запитує show-sensitive.


23. Адаптер не читає private keys або passwords із RouterOS configuration.


24. Counters firewall не збираються.


25. Dynamic connection table не збирається.




---

4. Структура assembly

src/Mfc.RouterOs/
├── Protocol/
│   ├── Framing/
│   │   ├── RosLengthCodec.cs
│   │   ├── RosWordReader.cs
│   │   └── RosWordWriter.cs
│   ├── Sentences/
│   │   ├── RosSentenceParser.cs
│   │   ├── RosSentenceLease.cs
│   │   ├── RosReplyKind.cs
│   │   └── RosAttributeSequence.cs
│   └── Errors/
│       └── RosProtocolError.cs
│
├── Transport/
│   ├── RosTlsConnector.cs
│   ├── CertificateValidator.cs
│   ├── SpkiPinValidator.cs
│   └── RosTransport.cs
│
├── Session/
│   ├── RosSession.cs
│   ├── RosSessionState.cs
│   ├── RosPendingCommand.cs
│   ├── RosTagGenerator.cs
│   └── RosCommandCollector.cs
│
├── Commands/
│   ├── RosReadCommandId.cs
│   ├── RosReadCommandRegistry.cs
│   ├── RosReadCommandDefinition.cs
│   ├── RosPropertyProfile.cs
│   └── RosQueryProfile.cs
│
├── Mapping/
│   ├── System/
│   ├── Interfaces/
│   ├── Firewall/
│   ├── Routing/
│   ├── Vrrp/
│   ├── Bridge/
│   └── Values/
│
├── Compatibility/
│   ├── CompatibilityManifest.cs
│   ├── CompatibilityResolver.cs
│   └── EmbeddedManifests/
│
├── Redaction/
│   ├── SensitiveFieldRegistry.cs
│   ├── RosTrapSanitizer.cs
│   └── SecretLease.cs
│
└── Diagnostics/
    └── RouterOsEventIds.cs

Заборонені namespaces:

Mfc.RouterOs.Write
Mfc.RouterOs.Scripting
Mfc.RouterOs.Terminal
Mfc.RouterOs.GenericCommands

Architecture test повинен блокувати появу таких namespaces або RouterOS write paths.


---

5. Application ports

5.1. Session factory

public interface IRouterOsReadSessionFactory
{
    Task<IRouterOsReadSession> OpenAsync(
        RouterOsReadTarget target,
        CancellationToken cancellationToken);
}

RouterOsReadTarget {
    device_id
    management_host
    management_port
    username
    secret_reference
    trust_profile
    connect_timeout
    login_timeout
    command_timeout
    response_limits
}

RouterOsReadTarget не містить plaintext password.


---

5.2. Read session

public interface IRouterOsReadSession : IAsyncDisposable
{
    RouterOsSessionMetadata Metadata { get; }

    Task<RouterOsSectionResult> ReadAsync(
        RouterSectionId sectionId,
        CapturePassKind pass,
        CancellationToken cancellationToken);
}

RouterSectionId є закритим типом:

SystemIdentity
SystemResource
SystemRouterboard
SystemPackages
IpServices

Interfaces
Ipv4Addresses
Ipv6Addresses
InterfaceLists
InterfaceListMembers

Ipv4Filter
Ipv6Filter
Ipv4AddressLists
Ipv6AddressLists
Ipv4Nat
Ipv6Nat
Ipv4Raw
Ipv6Raw
Ipv4Mangle
Ipv6Mangle

RoutingTables
RoutingRules
Ipv4StaticRoutes
Ipv6StaticRoutes
Ipv4DefaultRouteState
Ipv6DefaultRouteState
Ipv4Settings
Ipv6Settings

VrrpInterfaces

Bridges
BridgePorts
BridgeSettings
BridgeVlans
EthernetSwitches
EthernetSwitchPorts

Новий RouterSectionId додається лише через зміну коду, tests і application release.


---

6. RouterOS binary framing

RouterOS API передає sentences, складені з words. Кожне word містить encoded length та payload, а sentence завершується zero-length word. Length передається у network byte order. Перший byte від 0xF8 зарезервований як control byte. 

6.1. Нормативне кодування length

Діапазон	Розмір prefix	Формула

0x00000000..0x0000007F	1 byte	len
0x00000080..0x00003FFF	2 bytes	len | 0x8000
0x00004000..0x001FFFFF	3 bytes	len | 0xC00000
0x00200000..0x0FFFFFFF	4 bytes	len | 0xE0000000
0x10000000..0xFFFFFFFF	5 bytes	0xF0 + uint32 len


6.2. Обов’язкові test vectors

Length	Encoded bytes

0	00
1	01
127	7F
128	80 80
16 383	BF FF
16 384	C0 40 00
2 097 151	DF FF FF
2 097 152	E0 20 00 00
268 435 455	EF FF FF FF
268 435 456	F0 10 00 00 00
4 294 967 295	F0 FF FF FF FF


Production limit 256 KiB застосовується після декодування length і до allocation.


---

6.3. Неканонічні encodings

Decoder повинен відхиляти:

двобайтне encoding для length < 0x80;

трибайтне encoding для length < 0x4000;

чотирибайтне encoding для length < 0x200000;

0xF0 encoding для length < 0x10000000;

prefix 0xF1..0xF7;

control prefix 0xF8..0xFF;

truncated prefix;

decoded length вище configured limit;

integer overflow.


Помилки:

API_LENGTH_ENCODING_NON_CANONICAL
API_LENGTH_PREFIX_UNSUPPORTED
API_RESERVED_CONTROL_BYTE
API_LENGTH_TRUNCATED
API_WORD_TOO_LARGE

Outgoing encoder завжди формує найкоротше канонічне encoding.


---

7. Word parser

7.1. Parser state machine

READING_PREFIX
      ↓
READING_LENGTH_TAIL
      ↓
READING_BODY
      ↓
WORD_COMPLETE
      ↓
READING_PREFIX

При length 0:

WORD_COMPLETE
      ↓
SENTENCE_COMPLETE

При помилці:

ANY_STATE
      ↓
FAULTED

FAULTED parser не може бути використаний повторно.


---

7.2. Implementation constraints

Parser повинен використовувати:

System.IO.Pipelines
ReadOnlySequence<byte>
SequenceReader<byte>
MemoryPool<byte>

Заборонено:

читати TCP stream по одному byte;

створювати окремий byte[] для кожного word;

необмежено накопичувати незавершене sentence;

повертати memory після завершення її lease;

декодувати весь network buffer як один UTF-8 string.



---

7.3. Limits parser

maximum word payload:        256 KiB
maximum words per sentence:  256
maximum sentence payload:    2 MiB
maximum empty sentences:     16 consecutive

Перевищення будь-якого limit переводить session у FAULTED.


---

8. Byte-preserving word model

Protocol layer не повинен передчасно припускати, що кожний RouterOS value є коректним UTF-8 text.

RosWord {
    payload: ReadOnlyMemory<byte>
}

Класифікація:

COMMAND
REPLY
ATTRIBUTE
API_ATTRIBUTE
QUERY
UNKNOWN

Правила:

command і reply markers декодуються як strict ASCII;

attribute names декодуються як strict ASCII;

.tag декодується як strict ASCII;

values декодуються відповідним typed parser;

invalid UTF-8 value не замінюється символом �;

invalid UTF-8 value зберігається як binary compatibility material;

invalid UTF-8 переводить Device у NEEDS_REVALIDATION.



---

9. Sentence parser

RouterOS визначає command word як перше word sentence. Attribute words мають формат =name=value, API attributes — .name=value, query words починаються з ?, а reply words — з !. Attribute order не є значущим, але порядок query words є значущим. 

9.1. Incoming reply structure

reply marker
attribute*
API attribute*
zero-length word

Дозволені reply markers:

!re
!done
!empty
!trap
!fatal

!empty підтримується RouterOS починаючи з версії 7.18. Незалежно від проміжних replies, нормальне завершення команди підтверджується !done. 


---

9.2. Attribute parsing

Вхідне word:

=name=value

розділяється за другим символом =:

name  = bytes між першим і другим "="
value = усі наступні bytes

Це зберігає значення, що самі містять =.

Некоректні випадки:

=
=name
==value

повертають:

API_ATTRIBUTE_MALFORMED

Порожнє value дозволене:

=name=


---

9.3. Duplicate attributes

Protocol layer зберігає:

RosAttributeSequence

а не dictionary.

Typed mapping:

scalar field — рівно нуль або одне value;

повтор scalar field — API_DUPLICATE_ATTRIBUTE;

multi-valued field дозволяється лише property profile;

duplicate .tag — session fault;

duplicate reply marker неможливий.


RouterOS не гарантує порядок properties і не визначає поведінку duplicate entries у .proplist, тому outgoing profile не може містити дублікати. 


---

9.4. Memory ownership

RosSentenceLease

володіє pooled memory sentence.

Вимоги:

lease є IDisposable;

mapper повинен завершити копіювання або parsing до Dispose;

RosSentenceLease не виходить за межі RouterOS assembly;

pooled memory очищується для login-related data;

use-after-dispose перевіряється debug assertion.



---

10. Outgoing sentence writer

Нормативний порядок outgoing words:

1. command word
2. .tag
3. command attributes у profile order
4. .proplist
5. query words у визначеному порядку
6. zero-length word

Attribute order формально не є значущим, але адаптер повинен генерувати детермінований порядок для tests і traceability.

Writer повинен:

перевірити command registry;

перевірити кількість attributes;

перевірити outbound sentence size;

записати sentence в PipeWriter;

виконати один контрольований FlushAsync;

не зберігати serialized password після flush.



---

11. RouterOS session

11.1. State machine

DISCONNECTED
      ↓
CONNECTING
      ↓
TLS_HANDSHAKE
      ↓
AUTHENTICATING
      ↓
READY
      ↓
CLOSING
      ↓
CLOSED

Помилка:

ANY_NONTERMINAL_STATE
      ↓
FAULTED

Після FAULTED session закриває transport і завершує всі pending commands.


---

11.2. Session tasks

Один session має рівно:

один read loop;

один serialized writer;

один bounded pending-command registry;

один monotonic tag generator;

один cancellation coordinator.


Заборонено запускати декілька read loops для одного connection.


---

12. Command tags

RouterOS tags дозволяють виконувати декілька команд паралельно: кожна відповідь отримує ту саму .tag, що й відповідний request. /cancel є окремою командою, приймає target =tag=... і може мати власну .tag. Скасована команда зазвичай повертає !trap category=2, після чого !done. 

12.1. Tag format

ulong
→ invariant decimal ASCII

Приклад:

.tag=42

Вимоги:

tag не повторюється в межах session;

0 не використовується;

overflow завершує session;

усі команди після authentication мають tag;

login виконується без concurrency.



---

12.2. Pending registry

maximum pending commands: 16

PendingCommand {
    tag
    command_id
    lifecycle_state
    started_at
    deadline
    record_collector
    traps[]
    completion_source
}

Registry не повинен бути unbounded ConcurrentDictionary.


---

12.3. Reply routing

Reply	Реакція

!re	Передати record collector
!empty	Позначити empty result, очікувати !done
!trap	Зберегти sanitized trap, очікувати !done
!done	Завершити command
!fatal	Завершити session як FAULTED


У стані READY untagged reply заборонений, крім global !fatal.

Unknown tag означає:

API_UNKNOWN_REPLY_TAG

і переводить session у FAULTED.


---

12.4. Result collector

Reader loop не передає кожний !re до окремої asynchronous unbounded queue.

Для кожної команди створюється bounded collector:

RosCommandCollector {
    max_records
    max_payload_bytes
    record_mapper
}

Mapper виконується без блокувального I/O.

При перевищенні limits:

1. command переходить у LIMIT_EXCEEDED;


2. надсилається target-specific /cancel;


3. решта session закривається, якщо command не завершилася протягом cancel grace period.




---

13. Cancellation і timeout

13.1. Скасування однієї команди

caller cancellation
       ↓
mark CANCEL_REQUESTED
       ↓
send:
    /cancel
    =tag=<target-tag>
    .tag=<cancel-command-tag>
       ↓
wait target !trap/!done
       ↓
wait cancel-command !done

Заборонено надсилати unscoped /cancel, оскільки він скасовує всі активні команди.


---

13.2. Deadlines

connect timeout:            5 s
TLS + login timeout:       10 s
default command timeout:   30 s
cancel grace period:        2 s
full capture timeout:     120 s

Після закінчення cancel grace period connection примусово закривається.


---

13.3. Retry

RosSession не виконує retry або reconnect.

Retry виконується лише на рівні повного capture attempt:

open new connection
→ login
→ read complete pass
→ failure
→ discard all attempt data
→ bounded retry

Заборонено об’єднувати sections, прочитані з різних sessions, в один complete capture.


---

14. TLS transport

RouterOS використовує TCP 8729 для API-SSL. Без призначеного сертифіката API-SSL може використовувати anonymous Diffie–Hellman; такий режим для контролера заборонений. Password у RouterOS API login надсилається як звичайне значення всередині TLS-сеансу, тому коректна перевірка сертифіката є обов’язковою. 

14.1. Transport

Реалізація:

TcpClient/Socket
→ NetworkStream
→ SslStream
→ PipeReader/PipeWriter

Дозволені TLS versions:

TLS 1.2
TLS 1.3

Заборонені:

SSL 2.0
SSL 3.0
TLS 1.0
TLS 1.1


---

14.2. Trust mode INTERNAL_CA

Перевіряються:

1. Повний certificate chain.


2. Довірений internal root.


3. Certificate validity interval.


4. SAN DNS або SAN IP відповідно до management host.


5. Extended Key Usage serverAuth.


6. Mandatory revocation policy із CA profile.


7. Відсутність weak signature algorithm за policy controller.



CA profile не зберігається у RouterOS connection record як private key.


---

14.3. Trust mode SPKI_PIN

Перевіряються:

1. SHA-256 над DER SubjectPublicKeyInfo.


2. Точний збіг із configured pin.


3. Certificate validity interval.


4. SAN DNS/IP.


5. serverAuth EKU.


6. Допустимий signature algorithm.



Недостатньо лише збігу public key без перевірки target identity.


---

14.4. Certificate metadata

Дозволено зберігати:

subject
issuer
serial number
not-before
not-after
SAN names
SPKI SHA-256
negotiated TLS version
negotiated cipher suite

Заборонено зберігати:

private key
exported certificate private material
session secrets
TLS master secret


---

15. Authentication

15.1. Supported login

/login
=name=<username>
=password=<secret>
<zero-length word>

Успіх:

!done

Помилка:

!trap
...
!done

15.2. Legacy flow

Якщо login response містить:

=ret=<challenge>

session завершується:

UNSUPPORTED_LEGACY_AUTH_FLOW

Challenge/MD5 authentication RouterOS 6 не реалізується.


---

15.3. Secret handling

Plaintext password існує лише в:

SecretLease
→ temporary UTF-8 buffer
→ TLS writer

Після flush:

temporary buffer очищується;

SecretLease.Dispose() очищує backing memory;

password не створюється як interpolated string;

password не входить у RosSentence;

password не передається structured logger;

exception не містить password.



---

16. Command registry

RosReadCommandDefinition {
    id
    fixed_path
    section_id
    result_shape
    requirement
    property_profile_id
    query_profile_id
    pass_policy
    max_records
    max_payload_bytes
}

16.1. Result shapes

SINGLETON
UNORDERED_COLLECTION
ORDERED_COLLECTION
DIGESTED_COLLECTION

16.2. Requirement

REQUIRED
CONDITIONAL
OPTIONAL

16.3. Pass policy

PASS_1_ONLY
BOTH_PASSES
STABILITY_GUARD


---

17. Нормативний command catalogue

Command ID	Fixed path	Shape	Pass

SystemIdentity	/system/identity/print	Singleton	Both
SystemResource	/system/resource/print	Singleton	Guard
SystemRouterboard	/system/routerboard/print	Singleton	Both
SystemPackages	/system/package/print	Unordered	Both
IpServices	/ip/service/print	Unordered	Both
Interfaces	/interface/print	Unordered	Both
Ipv4Addresses	/ip/address/print	Unordered	Both
Ipv6Addresses	/ipv6/address/print	Unordered	Both
InterfaceLists	/interface/list/print	Unordered	Both
InterfaceListMembers	/interface/list/member/print	Unordered	Both
Ipv4Filter	/ip/firewall/filter/print	Ordered	Both
Ipv6Filter	/ipv6/firewall/filter/print	Ordered	Both
Ipv4AddressLists	/ip/firewall/address-list/print	Digested	Both
Ipv6AddressLists	/ipv6/firewall/address-list/print	Digested	Both
Ipv4Nat	/ip/firewall/nat/print	Ordered	Both
Ipv6Nat	/ipv6/firewall/nat/print	Ordered	Both
Ipv4Raw	/ip/firewall/raw/print	Ordered	Both
Ipv6Raw	/ipv6/firewall/raw/print	Ordered	Both
Ipv4Mangle	/ip/firewall/mangle/print	Ordered	Both
Ipv6Mangle	/ipv6/firewall/mangle/print	Ordered	Both
RoutingTables	/routing/table/print	Unordered	Both
RoutingRules	/routing/rule/print	Ordered	Both
Ipv4StaticRoutes	/ip/route/print	Unordered	Both
Ipv6StaticRoutes	/ipv6/route/print	Unordered	Both
Ipv4DefaultRouteState	/ip/route/print	Unordered	Pass 1
Ipv6DefaultRouteState	/ipv6/route/print	Unordered	Pass 1
Ipv4Settings	/ip/settings/print	Singleton	Both
Ipv6Settings	/ipv6/settings/print	Singleton	Both
VrrpInterfaces	/interface/vrrp/print	Unordered	Both
Bridges	/interface/bridge/print	Unordered	Both
BridgePorts	/interface/bridge/port/print	Unordered	Both
BridgeSettings	/interface/bridge/settings/print	Singleton	Both
BridgeVlans	/interface/bridge/vlan/print	Unordered	Both
EthernetSwitches	/interface/ethernet/switch/print	Unordered	Both
EthernetSwitchPorts	/interface/ethernet/switch/port/print	Unordered	Both


EthernetSwitches і EthernetSwitchPorts є optional та активуються лише hardware capability profile.


---

18. Query profiles

Query words мають значущий порядок, тому кожен query profile є незмінною ordered sequence. 

18.1. AllRows

fixed print argument:
    =all=

Використовується для ordered firewall facilities, коли compatibility fixture підтверджує повернення static і dynamic rules в effective order.

Якщо конкретна RouterOS version не пройшла цей test:

support_state = NEEDS_REVALIDATION


---

18.2. StaticRoutes

Version-specific ordered query:

?static=true
?dynamic=false
?#&

Фактичний query profile повинен бути перевірений на конкретній RouterOS version.


---

18.3. Ipv4DefaultRoutes

?dst-address=0.0.0.0/0

18.4. Ipv6DefaultRoutes

?dst-address=::/0

18.5. Заборони

Заборонені:

query words із GUI;

regular expressions;

user-defined query stack;

runtime modification query profile;

query values із policy editor;

query without command-level limit.



---

19. Property profile model

RosPropertyDefinition {
    routeros_name
    canonical_name
    value_parser
    classification
    cardinality
    requiredness
    redaction_policy
}

19.1. Classification

CONFIG_TYPED
CONFIG_OPAQUE
OBSERVATION_TYPED
OBSERVATION_DIGESTED
CAPABILITY_TYPED
TRANSIENT_EXCLUDED
RAW_ONLY
FORBIDDEN

CONFIG_TYPED

Поле має повністю відому семантику й тип.

CONFIG_OPAQUE

Поле входить у configuration hash як точне lossless value, але не підтримується редактором або compiler.

OBSERVATION_TYPED

Runtime state.

OBSERVATION_DIGESTED

Runtime data не зберігається повністю, але впливає на digest.

TRANSIENT_EXCLUDED

Counters, timestamps або технічні поля, які не входять у canonical snapshot.

RAW_ONLY

Зберігається лише в sanitized raw snapshot.

FORBIDDEN

Не запитується та не зберігається.


---

20. .proplist rules

Для кожної команди:

1. .proplist обов’язкова.


2. Property names у profile унікальні.


3. Property order фіксований.


4. Property list генерується application build.


5. Database не може додати property.


6. GUI не може додати property.


7. Відсутній optional property не є помилкою.


8. Відсутній required property — profile mismatch.


9. Додатковий returned property потрапляє в compatibility material.


10. .proplist без profile ID заборонена.



RouterOS може повертати додаткові properties навіть за наявності .proplist; порядок properties не визначений. Водночас відсутність .proplist може спричинити повернення дорогих або небажаних даних. 

Це означає, що адаптер не намагається «відкрити всі поля» RouterOS. Сумісність визначається signed compatibility manifest і перевіреною RouterOS version, а не небезпечним unrestricted print.


---

21. System property profiles

21.1. SystemIdentity

CONFIG_TYPED:
    name

21.2. SystemResource

CAPABILITY_TYPED:
    version
    build-time
    architecture-name
    board-name
    platform

OBSERVATION_TYPED:
    uptime

Не запитуються CPU load, memory counters або disk counters.

21.3. SystemRouterboard

CAPABILITY_TYPED:
    routerboard
    model
    serial-number
    firmware-type
    factory-firmware
    current-firmware
    upgrade-firmware

serial-number не логуються.

21.4. SystemPackages

RAW_ONLY:
    .id

CAPABILITY_TYPED:
    name
    version
    build-time
    scheduled
    disabled

21.5. IpServices

RAW_ONLY:
    .id

CONFIG_TYPED:
    name
    port
    address
    available-from
    certificate
    tls-version
    vrf
    max-sessions
    disabled

OBSERVATION_TYPED:
    dynamic
    invalid

address і available-from є version-specific aliases. Concrete profile повинен вибрати коректне поле, але не обидва довільно.


---

22. Interface property profiles

22.1. Interfaces

RAW_ONLY:
    .id

CONFIG_TYPED:
    name
    default-name
    type
    mtu
    mac-address
    disabled

OBSERVATION_TYPED:
    actual-mtu
    l2mtu
    max-l2mtu
    dynamic
    running
    slave
    invalid
    last-link-up-time
    last-link-down-time
    link-downs

22.2. Ipv4Addresses

RAW_ONLY:
    .id

CONFIG_TYPED:
    address
    network
    interface
    disabled
    comment

OBSERVATION_TYPED:
    actual-interface
    dynamic
    invalid
    slave

22.3. Ipv6Addresses

RAW_ONLY:
    .id

CONFIG_TYPED:
    address
    from-pool
    interface
    advertise
    eui-64
    no-dad
    disabled
    comment

OBSERVATION_TYPED:
    actual-interface
    dynamic
    global
    invalid
    link-local
    slave


---

23. Interface-list profiles

23.1. InterfaceLists

RAW_ONLY:
    .id

CONFIG_TYPED:
    name
    include
    exclude
    comment

OBSERVATION_TYPED:
    dynamic

23.2. InterfaceListMembers

RAW_ONLY:
    .id

CONFIG_TYPED:
    list
    interface
    disabled
    comment

OBSERVATION_TYPED:
    dynamic

Interface lists можуть включати й виключати інші lists, а /interface/list/member не відображає всі membership, отримані через include та exclude. Тому adapter має читати обидві секції, а resolved membership обчислює Application. 

Нормативний resolution order:

1. members із include
2. видалення members із exclude
3. explicit members

Cycle не замінюється порожнім результатом:

INTERFACE_LIST_CYCLE


---

24. Common firewall profile

RouterOS IPv4 та IPv6 firewall мають широкий набір stateless і stateful matchers; їхній набір відрізняється між address families. 

24.1. Rule metadata

RAW_ONLY:
    .id

CONFIG_TYPED:
    chain
    action
    disabled
    comment
    log
    log-prefix

OBSERVATION_TYPED:
    dynamic
    invalid

TRANSIENT_EXCLUDED:
    bytes
    packets

bytes і packets не додаються до .proplist.


---

24.2. Common match fields

protocol

src-address
dst-address
src-address-list
dst-address-list
src-address-type
dst-address-type

src-port
dst-port
port

in-interface
out-interface
in-interface-list
out-interface-list

in-bridge-port
out-bridge-port
in-bridge-port-list
out-bridge-port-list

src-mac-address

connection-state
connection-nat-state
connection-mark
connection-type
connection-bytes
connection-limit
connection-rate

packet-mark
routing-mark

tcp-flags
tcp-mss
icmp-options

ipsec-policy
helper
tls-host
layer7-protocol
content

dscp
priority
ingress-priority
packet-size

limit
dst-limit
time
random
nth
per-connection-classifier

Fields із складною граматикою можуть бути CONFIG_OPAQUE до реалізації точного parser, але не можуть бути втрачені.


---

24.3. IPv4-only match fields

fragment
ipv4-options
ttl
psd
hotspot
p2p
realm

24.4. IPv6-only match fields

ipv6-header
hop-limit


---

25. Filter property profiles

25.1. Action-specific fields

jump-target
reject-with
address-list
address-list-timeout
hw-offload

address-list та address-list-timeout використовуються для actions:

add-src-to-address-list
add-dst-to-address-list


---

25.2. Static/dynamic split

Для static rule:

semantic fields входять у configuration;

static_ordinal входить у configuration;

invalid входить в observations.


Для dynamic rule:

semantic fields входять в observations;

effective_ordinal входить в observations;

rule не входить у configuration hash.



---

25.3. Effective order

Raw response order є effective firewall order.

Кожний rule отримує:

effective_ordinal
static_ordinal?

Configuration hash використовує static-relative order:

static_ordinal

Observation hash використовує:

dynamic rules
effective_sequence_digest

Тому додавання RouterOS dynamic rule:

не створює configuration drift;

змінює observation hash;

не приховує її позицію від майбутнього deployment validator.



---

26. NAT property profiles

Застосовуються common firewall match fields і:

to-addresses
to-ports
same-not-by-dst
randomise-ports
socksify-service
socks5-server
socks5-port

Для IPv4 та IPv6 використовуються окремі property profiles.

NAT у M1:

читається повністю;

канонізується;

порівнюється;

використовується для multi-WAN evidence;

не редагується;

не компілюється;

не застосовується.



---

27. RAW property profiles

Застосовуються stateless firewall fields і action-specific fields:

jump-target
address-list
address-list-timeout

Stateful fields, яких конкретний RAW profile не підтримує, не додаються до .proplist.

RAW у M1 є read-only dependency domain.


---

28. IPv4 Mangle profile

Додаткові action fields:

new-connection-mark
new-packet-mark
new-routing-mark
new-mss
new-dscp
new-priority
new-ttl
passthrough
sniff-target
sniff-target-port
sniff-id
route-dst

Ці поля потрібні для виявлення:

routing marks;

packet marks;

connection marks;

PCC;

policy routing;

multi-WAN dependencies.



---

29. IPv6 Mangle profile

Додаткові fields:

new-connection-mark
new-packet-mark
new-routing-mark
new-mss
new-dscp
new-priority
new-hop-limit
passthrough
sniff-target
sniff-target-port
sniff-id
src-prefix
dst-prefix

IPv4 new-ttl і IPv6 new-hop-limit є різними canonical fields.


---

30. Address-list profiles

Firewall address lists можуть бути статичними або динамічними; dynamic entries можуть створюватися filter, NAT або Mangle actions. 

30.1. Requested fields

RAW_ONLY:
    .id

CONFIG_TYPED або OBSERVATION_TYPED:
    list
    address
    timeout
    disabled
    comment
    dynamic

TRANSIENT_EXCLUDED:
    creation-time

30.2. Static entries

Зберігаються повністю:

list
address
disabled
comment

timeout для static entry перевіряється за profile semantics.

30.3. Dynamic entries

Повні dynamic addresses не зберігаються у snapshot.

Для кожного list формується:

DynamicAddressListSummary {
    list_name
    family
    entry_count
    sorted_entry_digest
}

Алгоритм:

1. Parse dynamic address.
2. Canonicalize.
3. SHA256 canonical entry.
4. Зберегти лише 32-byte digest.
5. Відсортувати digests.
6. SHA256 ordered digests.
7. Видалити plaintext address.

Це дозволяє:

виявляти зміну dynamic membership;

не перетворювати controller на threat-feed database;

не зберігати великий runtime address corpus.



---

31. Routing profiles

31.1. RoutingTables

RAW_ONLY:
    .id

CONFIG_TYPED:
    name
    fib
    disabled

OBSERVATION_TYPED:
    dynamic
    invalid
    used

31.2. RoutingRules

RAW_ONLY:
    .id

CONFIG_TYPED:
    action
    src-address
    dst-address
    interface
    routing-mark
    table
    min-prefix
    disabled
    comment

OBSERVATION_TYPED:
    dynamic
    inactive
    invalid


---

31.3. Static routes

RAW_ONLY:
    .id

CONFIG_TYPED:
    dst-address
    gateway
    routing-table
    pref-src
    distance
    scope
    target-scope
    check-gateway
    type
    blackhole
    unreachable
    prohibit
    suppress-hw-offload
    disabled
    comment

OBSERVATION_TYPED:
    active
    inactive
    connect
    dynamic
    static
    ecmp
    hw-offloaded
    immediate-gw
    gateway-status
    local-address

Static route command не повинен завантажувати повну BGP або OSPF route table.


---

31.4. Default-route state

Зберігаються лише runtime data default routes:

dst-address
routing-table
gateway
distance
active
inactive
dynamic
static
immediate-gw
gateway-status
pref-src

Ця секція:

входить лише в observations;

не впливає на configuration hash;

використовується для multi-WAN current-state view.



---

32. IP settings profiles

32.1. IPv4

CONFIG_TYPED:
    ip-forward
    send-redirects
    accept-source-route
    accept-redirects
    secure-redirects
    rp-filter
    tcp-syncookies
    max-neighbor-entries
    arp-timeout
    icmp-rate-limit
    icmp-errors-use-inbound-interface-address
    allow-fast-path
    route-cache

OBSERVATION_TYPED:
    ipv4-fast-path-active
    ipv4-fasttrack-active

Counters fast-path/fasttrack не збираються.

32.2. IPv6

CONFIG_TYPED:
    disable-ipv6
    forward
    accept-redirects
    accept-router-advertisements
    accept-router-advertisements-on
    max-neighbor-entries
    multipath-hash-policy
    disabled-link-local-address


---

33. VRRP profile

VRRP Virtual Router визначається VRID та набором IPv4 або IPv6 addresses. Однаковий VRID для IPv4 й IPv6 утворює два різні Virtual Routers. Один фізичний router також може бути master для одного VRID і backup для іншого, тому global device role неприпустима. 

33.1. Requested fields

RAW_ONLY:
    .id

CONFIG_TYPED:
    name
    interface
    vrid
    version
    v3-protocol
    priority
    interval
    preemption-mode
    authentication
    group-authority
    sync-connection-tracking
    connection-tracking-mode
    connection-tracking-port
    remote-address
    arp
    arp-timeout
    comment
    disabled

OBSERVATION_TYPED:
    invalid
    running
    master
    backup
    failure
    grp-authority
    grp-member
    mtu

Current RouterOS VRRP exposes configuration for grouping, connection-tracking synchronization, family, version and priority, а також read-only flags master, backup, failure, grp-authority та grp-member. 


---

33.2. Forbidden VRRP fields

password
on-master
on-backup
on-fail

VRRP password є sensitive RouterOS property. 

Transition script bodies у M1 не читаються.


---

33.3. Derived role

IF failure:
    FAILURE
ELSE IF master:
    MASTER
ELSE IF backup:
    BACKUP
ELSE IF invalid:
    INVALID
ELSE IF running:
    INITIALIZING
ELSE:
    INACTIVE

Одночасні master=true і backup=true є:

VRRP_ROLE_INCONSISTENT


---

34. Bridge profiles

34.1. Bridges

RAW_ONLY:
    .id

CONFIG_TYPED:
    name
    admin-mac
    auto-mac
    ageing-time
    arp
    arp-timeout
    protocol-mode
    priority
    pvid
    vlan-filtering
    frame-types
    ingress-filtering
    dhcp-snooping
    igmp-snooping
    fast-forward
    mtu
    disabled

OBSERVATION_TYPED:
    dynamic
    running
    invalid
    mac-address
    actual-mtu
    l2mtu
    root-bridge
    root-port
    root-path-cost


---

34.2. BridgePorts

RAW_ONLY:
    .id

CONFIG_TYPED:
    bridge
    interface
    pvid
    frame-types
    ingress-filtering
    tag-stacking
    horizon
    hw
    path-cost
    internal-path-cost
    priority
    edge
    point-to-point
    learn
    flood-unknown-unicast
    multicast-router
    restricted-role
    restricted-tcn
    bpdu-guard
    trusted
    disabled

OBSERVATION_TYPED:
    dynamic
    inactive
    invalid
    hw-offload
    role
    root-path-cost


---

34.3. BridgeSettings

CONFIG_TYPED:
    use-ip-firewall
    use-ip-firewall-for-vlan
    use-ip-firewall-for-pppoe
    allow-fast-path

OBSERVATION_TYPED:
    bridge-fast-path-active

Bridge та switch-chip forwarding не можна автоматично вважати traffic path через CPU/IP firewall. Hardware-offloaded L2 або L3 traffic може оброблятися switch chip, а можливості hardware offload залежать від конкретного пристрою й chip. 


---

34.4. BridgeVlans

RAW_ONLY:
    .id

CONFIG_TYPED:
    bridge
    vlan-ids
    tagged
    untagged
    disabled
    comment

OBSERVATION_TYPED:
    dynamic
    current-tagged
    current-untagged


---

35. Switch profiles

35.1. Generic EthernetSwitches

RAW_ONLY:
    .id

CAPABILITY_TYPED:
    name
    type

CONFIG_TYPED:
    l3-hw-offloading

35.2. Generic EthernetSwitchPorts

RAW_ONLY:
    .id

CONFIG_TYPED:
    name
    switch
    default-vlan-id
    vlan-mode
    vlan-header
    l3-hw-offloading

Додаткові switch-chip-specific fields дозволяються лише embedded hardware manifest:

board model
switch-chip type
RouterOS version
exact property profile
hardware test result

Невідомий chip не отримує implicit profile.


---

36. RouterOS value parsers

36.1. Parser tiers

TYPED_STABLE
STRUCTURED_READ_ONLY
OPAQUE_LOSSLESS

TYPED_STABLE

Тип має повну canonical semantics.

STRUCTURED_READ_ONLY

Тип розібраний для аналізу, але не дозволений policy compiler.

OPAQUE_LOSSLESS

Значення зберігається точно, входить у hash, але не інтерпретується.


---

36.2. Обов’язкові parsers

RosBooleanParser
RosSignedIntegerParser
RosUnsignedIntegerParser
RosEnumParser
RosEnumSetParser
RosDurationParser
RosIpAddressParser
RosIpPrefixParser
RosInterfaceAddressParser
RosAddressRangeParser
RosMacAddressParser
RosPortSetParser
RosProtocolParser
RosNegatedValueParser
RosTcpFlagsParser
RosIcmpOptionsParser
RosIpsecPolicyParser
RosPccParser
RosStringListParser
RosInterfaceNameParser
RosCommentParser


---

36.3. Boolean

Accepted RouterOS tokens визначає profile:

yes / no
true / false

Canonical:

true
false

Unknown token не підміняється false.


---

36.4. Integer

Вимоги:

invariant culture;

checked arithmetic;

заборона floating point;

per-field range;

відсутність silent clamp;

invalid value переходить у compatibility material.



---

36.5. IP values

Prefix

192.168.1.19/24
→ 192.168.1.0/24

Interface address

192.168.1.19/24
→ 192.168.1.19/24

IPv6

lowercase;

RFC-style compression;

host bits masked лише для prefix type;

zone identifiers дозволені тільки profile, де вони семантично допустимі.



---

36.6. Address-list value

IP address
IP prefix
IPv4 range
DNS name
opaque RouterOS-supported token

DNS name:

не resolve-иться;

не замінюється IP;

зберігається losslessly;

не викликає network request controller.



---

36.7. Duration

RouterOS duration перетворюється на:

signed int64 microseconds

Підтримуються profile-defined forms:

1w2d3h4m5s
00:05:00
500ms
10us

Special values:

auto
none
never
infinite

мають окремі typed variants.


---

36.8. Port set

80,81,82,100-110,105-120
→
80-82,100-120

Алгоритм:

1. Parse.


2. Validate 0..65535.


3. Sort.


4. Merge overlaps.


5. Merge adjacent intervals.


6. Serialize canonical intervals.




---

36.9. Negation

RouterOS values із !:

!192.0.2.0/24
!ether1
!syn

представляються:

Negated<T> {
    is_negated
    value
}

Негативний marker не залишається частиною opaque string для підтримуваного типу.


---

36.10. TCP flags

syn,!ack,!fin

Canonical model:

required_present: [syn]
required_absent:  [ack, fin]

Sets сортуються за нормативним enum order.


---

36.11. PCC

per-connection-classifier розбирається як:

classifier
denominator
remainder

Наприклад:

both-addresses-and-ports:2/0

PCC у M1 є STRUCTURED_READ_ONLY.


---

36.12. Ambiguous lists

Не допускається один generic Split(',') для всіх RouterOS properties.

Кожне list field має власну grammar:

interface list
VLAN IDs
TCP flags
connection states
routing gateways
address sets

Якщо grammar не доведена fixture:

OPAQUE_LOSSLESS
+ NEEDS_REVALIDATION


---

37. Unknown properties і values

37.1. Unknown returned property

1. Перевірити sensitive registry.


2. Не логувати value.


3. Зберегти sanitized property у raw snapshot.


4. Додати до compatibility material.


5. Не додавати до known configuration model.


6. Перевести support state у NEEDS_REVALIDATION.



37.2. Unknown enum token

1. Зберегти exact bytes.


2. Не підміняти найближчим enum.


3. Не використовувати default.


4. Додати compatibility finding.


5. Включити у compatibility hash.



37.3. Важливе обмеження

Оскільки adapter використовує .proplist, він не може довести, що RouterOS version не має інших нових properties, які не були запитані.

Тому джерелом write-compatibility надалі буде:

exact RouterOS build
+ embedded compatibility manifest
+ fixtures
+ CHR/hardware tests

а не відсутність unknown fields у конкретній відповіді.


---

38. Compatibility manifest

CompatibilityManifest {
    schema_version
    profile_id
    supported_routeros_builds[]
    allowed_channels[]
    architectures[]
    board_classes[]
    command_profiles[]
    property_profiles[]
    query_profiles[]
    known_trap_signatures[]
    known_incompatibilities[]
    manifest_hash
}

38.1. Обмеження

Manifest:

є embedded assembly resource;

входить у signed release;

не редагується через GUI;

не завантажується з PostgreSQL;

не може створити новий command path;

не може активувати write command;

не може змінити global limits вище compiled maximum.


38.2. Unknown RouterOS build

Для невідомої версії:

1. Виконати minimal identity profile.


2. Встановити NEEDS_REVALIDATION.


3. Дозволити compatibility capture через nearest same-major read profile.


4. Не надавати майбутній write support.


5. Required section trap робить capture partial або failed.


6. Operator бачить точну невідповідність.



RouterOS 6 отримує:

UNSUPPORTED_LEGACY


---

39. Trap classification

RouterOS !trap може містити message і category. Документовані category: 0 — missing item/command, 1 — argument failure, 2 — interrupted, 3 — scripting failure, 4 — general failure, 5 — API failure, 6 — TTY failure, 7 — return value. 

39.1. Mapping

Category	Adapter classification

0	COMMAND_OR_ITEM_NOT_FOUND
1	COMMAND_ARGUMENT_REJECTED
2	COMMAND_INTERRUPTED
3	ROUTEROS_SCRIPTING_FAILURE
4	ROUTEROS_GENERAL_FAILURE
5	ROUTEROS_API_FAILURE
6	ROUTEROS_TTY_FAILURE
7	ROUTEROS_RETURN_VALUE
absent/other	ROUTEROS_UNCLASSIFIED_TRAP


39.2. Contextual classification

Authentication phase

Будь-який login trap:

API_AUTHENTICATION_FAILED

Requested cancel

category=2 для command зі станом CANCEL_REQUESTED:

COMMAND_CANCELED

Optional command

Trap category 0 може означати UNSUPPORTED лише тоді, коли:

command optional;

RouterOS build відомий;

normalized trap signature є у manifest.


Інакше:

SECTION_READ_FAILED

API failure

category=5 переводить session у FAULTED.


---

39.3. Trap limits

maximum traps per command: 4
maximum trap message:      512 UTF-8 bytes

П’ята trap:

API_EXCESSIVE_TRAPS

і session fault.


---

40. Redaction registry

Офіційна документація RouterOS окремо перелічує menus і sensitive parameters, включно з passwords, secrets, private keys і VRRP password. 

40.1. Global forbidden field names

password
secret
passphrase
private-key
private-key-data
auth-key
enc-key
ppk-secret
otp-secret
ipsec-secret
shared-secret
pin
response
ret

ret і response заборонені через legacy authentication flow.


---

40.2. Path-specific forbidden fields

/interface/vrrp:
    password
    on-master
    on-backup
    on-fail

/system/script:
    source

/system/scheduler:
    on-event

/file:
    contents

Останні menus взагалі не входять у command registry.


---

40.3. Comments

comment потрібен для:

rule identity;

operator context;

fwc: ownership markers;

semantic diff.


Тому comment:

зберігається у snapshot;

не виводиться в structured logs;

не входить у exception message;

експортується лише authorized user.



---

40.4. Trap sanitization

Sanitizer повинен:

1. Обмежити length.


2. Замінити control characters.


3. Виявити password=, secret=, response=, ret=.


4. Замінити values на [REDACTED].


5. Не логувати raw binary trap.


6. Зберегти category і command ID окремо.




---

41. Logging

Дозволений event:

{
  "event": "routeros.command.completed",
  "deviceId": "...",
  "commandId": "Ipv4Filter",
  "tag": 42,
  "recordCount": 37,
  "payloadBytes": 18421,
  "durationMs": 116,
  "result": "success",
  "correlationId": "..."
}

Заборонено логувати:

serialized sentence
raw API word
username
password
comments
address-list contents
firewall rule values
certificate private data
connection string
raw trap message
management credentials

Для diagnostics достатньо:

device ID
command ID
tag
duration
record count
byte count
error code


---

42. Per-command limits

Section	Maximum records

System identity	1
System resource	1
Routerboard	1
Packages	128
IP services	64
Interfaces	10 000
IPv4 addresses	50 000
IPv6 addresses	50 000
Interface lists	4 096
Interface-list members	50 000
Filter rules per family	20 000
NAT rules per family	20 000
RAW rules per family	20 000
Mangle rules per family	20 000
Static address-list entries	250 000 per family
Dynamic address-list rows processed	250 000 per family
Routing tables	4 096
Routing rules	20 000
Static routes	50 000 per family
Default route states	1 024 per family
VRRP interfaces	1 024
Bridges	1 024
Bridge ports	20 000
Bridge VLAN rows	20 000
Switch chips	64
Switch ports	10 000


Загальні limits:

maximum raw capture:         256 MiB
maximum command payload:      64 MiB
maximum canonical section:    64 MiB
maximum response word:       256 KiB
maximum response sentence:     2 MiB


---

43. Stable-read integration

43.1. Stability vector

Прийняття capture повинно порівнювати не лише known configuration:

StabilityVector {
    configuration_hash
    capability_hash
    compatibility_material_hash
    routeros_version
}

Capture приймається, коли:

pass1.stability_vector == pass2.stability_vector

Observations не входять у stability comparison.


---

43.2. Boot guard

SystemResource.uptime читається:

на початку pass 1
після pass 1
на початку pass 2
після pass 2

Якщо наступне uptime менше попереднього:

ROUTER_REBOOTED_DURING_CAPTURE

Поточний attempt відкидається.


---

43.3. Session consistency

Обидва passes:

виконуються в одному API-SSL session;

використовують один compatibility profile;

використовують однакові property/query profiles;

не змішуються з іншими captures того самого Device;

мають один overall deadline.



---

44. Ordered firewall capture

Для кожної ordered facility adapter повинен довести:

1. RouterOS повертає rows у фактичному order.


2. Static і dynamic rows прочитані в одному sequence.


3. .id використовується лише всередині capture.


4. Counters не запитуються.


5. Dynamic rule не входить у configuration hash.


6. Static rule order не залежить від dynamic insertions.


7. Effective sequence digest зберігає повний порядок.



Якщо profile не може підтвердити all-row behavior:

ORDERED_FIREWALL_CAPTURE_UNVERIFIED

Capture не може бути використаний як foundation для майбутнього deployment planning.


---

45. Test vectors binary codec

45.1. Complete word

Word /login:

06 2F 6C 6F 67 69 6E

Sentence terminator:

00

Complete minimal sentence:

06 2F 6C 6F 67 69 6E 00

45.2. Invalid vectors

80 7F
    overlong encoding for 127

C0 00 80
    overlong encoding for 128

F0 00 00 00 01
    overlong five-byte encoding

F1
    unsupported prefix

F8
    reserved control byte

80
    truncated two-byte prefix

C0 40
    truncated three-byte prefix

04 61 62
    truncated word body


---

46. Protocol test matrix

Обов’язкові unit tests:

all length boundaries
canonical encode/decode round trip
fragmented length prefix
fragmented payload
multiple words per TCP segment
multiple sentences per TCP segment
zero-length terminator
consecutive empty sentences
word limit
sentence limit
word-count limit
reserved control byte
invalid UTF-8
attribute with "=" in value
empty attribute value
duplicate scalar attribute
duplicate .tag
unknown reply marker
untagged reply in READY
unknown tag
!re followed by !done
!empty followed by !done
!trap followed by !done
!fatal
connection close mid-prefix
connection close mid-word
connection close mid-sentence

Property-based invariant:

Decode(Encode(payload)) == payload

для всіх payloads у production limit.


---

47. Session concurrency tests

16 parallel tagged commands
17th command rejected by bound
out-of-order replies
interleaved !re records
trap on one command without affecting others
fatal affecting all pending commands
caller cancellation
command timeout
cancel-command timeout
target completes before /cancel
connection close during cancel
disposal with pending commands
tag overflow simulation
repeated open/close without leaks

Після кожного test:

pending_command_count == 0
active_reader_count == 0
active_writer_count == 0
undisposed_sentence_leases == 0


---

48. TLS tests

trusted internal CA
unknown CA
expired server certificate
not-yet-valid certificate
DNS SAN match
DNS SAN mismatch
IP SAN match
IP SAN mismatch
missing serverAuth EKU
valid SPKI pin
invalid SPKI pin
same key, wrong SAN
valid certificate, wrong management host
TLS 1.0-only server
server without certificate
connection timeout
TLS close during login

Жодний test mode не повинен додавати production flag SkipCertificateValidation.


---

49. Sanitized RouterOS fixtures

49.1. Layout

tests/
└── Mfc.RouterOs.IntegrationTests/
    └── Fixtures/
        └── RouterOs/
            └── <exact-version>/
                └── <command-id>/
                    └── <case-name>/
                        ├── metadata.json
                        ├── request.json
                        ├── reply.bin
                        ├── expected-protocol.json
                        ├── expected-mapped.json
                        └── SHA256SUMS

49.2. Metadata

{
  "fixtureSchema": 1,
  "routerOsVersion": "exact-version",
  "architecture": "x86_64",
  "boardClass": "CHR",
  "commandId": "Ipv4Filter",
  "source": "synthetic|chr|hardware",
  "containsSensitiveData": false
}

49.3. Fixture requirements

exact RouterOS version;

exact command registry version;

exact property profile;

exact query profile;

request words;

binary response;

expected decoded reply;

expected mapped records;

SHA-256 кожного fixture file.



---

49.4. Заборонені fixture data

production exports
production IP addresses
real company site names
real serial numbers
real MAC addresses
real usernames
real passwords
real public certificates
real address-list contents
real comments

Використовуються лише:

192.0.2.0/24
198.51.100.0/24
203.0.113.0/24
2001:db8::/32
synthetic MAC addresses
synthetic identities


---

50. Fixture acquisition

50.1. CHR fixtures

Процедура:

1. Reset CHR.
2. Apply synthetic fixture configuration поза production adapter.
3. Generate isolated test certificate.
4. Capture binary API exchange.
5. Sanitize metadata.
6. Run secret scanner.
7. Generate expected mapping.
8. Commit fixture.

50.2. Hardware fixtures

Потрібні для:

CRS board detection
switch-chip type
bridge hardware offload
switch port properties
L3HW observations

CHR не замінює physical switch-chip tests.


---

51. Fault injection

Fault injection transport повинен підтримувати:

split after every byte
delay between words
delay before !done
drop after N bytes
duplicate sentence
inject unknown tag
inject untagged reply
replace prefix
oversize length
invalid UTF-8
TLS close
socket reset
database cancellation propagated to session

Кожна fault point повинна завершуватися одним із визначених станів:

command completed
command canceled
session faulted
capture attempt discarded

Невизначене зависання заборонене.


---

52. Performance requirements

На типовому controller host:

1 000 firewall rules:
    protocol parse < 100 ms
    typed mapping < 200 ms

20 000 firewall rules:
    bounded memory
    no LOH growth proportional to retry count

250 000 address-list entries:
    dynamic digest without persistence of plaintext values
    peak working memory within configured capture budget

Parser benchmark повинен вимірювати:

bytes/s
allocations/word
allocations/sentence
peak pooled memory
fragmented-frame overhead

Target:

zero allocation per small word after pool warm-up,
крім final immutable mapped values.


---

53. Security review gates

Окреме security review потрібне для змін у:

Protocol/
Transport/
Session/
Commands/
Compatibility/
Redaction/

PR блокується, якщо:

додано RouterOS path;

розширено .proplist;

змінено sensitive registry;

змінено TLS validation;

змінено login handling;

збільшено global limits;

змінено unknown-property handling;

змінено trap sanitization.


Для нового command ID PR повинен містити:

обґрунтування для firewall controller
official property reference
property profile
redaction analysis
limits
unit fixtures
CHR/hardware fixture
canonicalization contract


---

54. Acceptance criteria

Специфікація реалізована лише коли доведено:

1. Encoder формує всі нормативні length encodings.


2. Decoder відхиляє overlong encodings.


3. Decoder відхиляє reserved control bytes.


4. Word allocation відбувається лише після перевірки limit.


5. Parser коректно працює з довільною TCP fragmentation.


6. Sentence завершується лише zero-length word.


7. Attribute values можуть містити =.


8. Duplicate scalar attributes не втрачаються мовчки.


9. Invalid UTF-8 не проходить lossy replacement.


10. Всі READY-state commands мають .tag.


11. Interleaved replies правильно маршрутизуються.


12. Unknown tag fault-ить session.


13. !trap не завершує command до !done.


14. !fatal завершує всі pending commands.


15. Targeted /cancel не скасовує сторонні commands.


16. Timeout має bounded cancel grace period.


17. Session не виконує reconnect.


18. API-SSL без RouterOS certificate відхиляється.


19. Invalid SAN відхиляється.


20. Invalid SPKI pin відхиляється.


21. Plaintext password відсутній у logs і snapshots.


22. Legacy login challenge відхиляється.


23. Application не передає raw RouterOS command.


24. Desktop не залежить від RouterOS adapter.


25. Registry містить лише read commands.


26. Усі /print використовують .proplist.


27. .proplist не містить duplicate fields.


28. Query words не надходять із GUI або БД.


29. Unknown returned property створює compatibility finding.


30. Missing required property створює profile mismatch.


31. Firewall counters не збираються.


32. Static firewall rule order зберігається.


33. Dynamic firewall rules входять лише в observations.


34. Effective firewall sequence має digest.


35. Dynamic address-list plaintext не зберігається.


36. Зміна dynamic address-list contents змінює observation digest.


37. Full dynamic route table не завантажується.


38. Static routes і active default routes розділені.


39. VRRP password не запитується.


40. VRRP role визначається для кожного instance.


41. Split-master topology не спрощується до global role.


42. Bridge use-ip-firewall* settings читаються.


43. Hardware-offload state не прирівнюється до CPU firewall.


44. Unknown switch chip не отримує implicit profile.


45. Per-command record limits працюють.


46. Raw capture limit працює.


47. Після protocol failure pending command count дорівнює нулю.


48. Після cancellation pooled buffers повернуті.


49. CHR fixtures не містять production data.


50. Physical CRS fixture існує щонайменше для одного supported hardware profile.


51. Build і tests не створюють незакомічених файлів.


52. Production assembly не містить RouterOS write path.




---

55. Порядок реалізації

1. RosLengthCodec
2. RosWordReader / RosWordWriter
3. RosSentenceParser
4. RosSentenceLease
5. Reply classification
6. Tagged RosSession
7. Cancellation
8. TLS certificate validation
9. Login
10. Command registry
11. Property profiles
12. Value parsers
13. Redaction
14. System readers
15. Interface readers
16. Firewall readers
17. Routing readers
18. VRRP reader
19. Bridge/switch readers
20. Compatibility manifests
21. Binary fixtures
22. CHR integration
23. Hardware CRS integration
24. Fault injection
25. Performance validation

Жодний наступний етап не починається до завершення tests поточного рівня.


---

56. Наступний нормативний документ

MikroTik Firewall Controller
Canonical Snapshot and Semantic Diff Specification v0.1

Він повинен визначити:

canonical binary/JSON representation
section schemas
field-level normalization
stable keys
static/dynamic separation
ordered firewall representation
address-list digests
configuration and observation hashes
compatibility material
content-addressed payload storage
record matching
managed and unmanaged rule identity
ordered diff algorithm
diff complexity bounds
node-level topology projection
VRRP member comparison
multi-WAN evidence comparison
server-side pagination contracts
snapshot and diff test vectors