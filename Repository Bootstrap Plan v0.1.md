MikroTik Firewall Controller

Repository Bootstrap Plan v0.1

Дата: 2 серпня 2026 року
Статус: нормативний план створення репозиторію


---

1. Мета bootstrap

Bootstrap повинен створити відтворювану основу проєкту, яка:

компілюється на чистому середовищі;

фіксує межі модулів;

не допускає неправильних залежностей;

забезпечує детерміноване керування пакетами;

містить CI, тести, правила Git і базову документацію;

запускає desktop-клієнт, controller і PostgreSQL;

не містить RouterOS write-функціональності;

готує основу для першого read-only вертикального зрізу.


Bootstrap не є прототипом. Усі створені структури мають бути придатні для подальшого production-коду.


---

2. Основні рішення

Питання	Рішення

Модель репозиторію	Monorepo
Архітектура controller	Модульний моноліт
Кількість solution	Одна
Основна гілка	main
Git workflow	Trunk-based із короткоживучими branches
Merge strategy	Squash merge
Версіонування	Semantic Versioning
Dependency management	Central Package Management
Package restore	Lock files, locked mode у CI
Database migrations	Forward-only, виконуються окремою командою
CI	GitHub Actions
RouterOS integration tests	Ізольований self-hosted CHR runner
Production secrets	Поза репозиторієм і desktop-клієнтом


Заборонено створювати:

develop branch;

окремий репозиторій для кожного модуля;

мікросервіси;

message broker;

generic repository;

generic unit of work;

service locator;

MediatR;

AutoMapper;

runtime dependency scanning;

довільний Utils або Helpers модуль;

окремий shared-проєкт без визначеної відповідальності.



---

3. Назва репозиторію

mikrotik-firewall-controller

Solution:

MikroTikFirewallController.sln

Кореневий namespace:

Mfc

Назви assemblies:

Mfc.Domain
Mfc.Application
Mfc.RouterOs
Mfc.Infrastructure
Mfc.Contracts
Mfc.Controller
Mfc.Desktop

Mfc використовується лише як технічний namespace. У GUI застосовується повна назва продукту.


---

4. Структура репозиторію

mikrotik-firewall-controller/
├── .config/
│   └── dotnet-tools.json
│
├── .github/
│   ├── CODEOWNERS
│   ├── dependabot.yml
│   ├── pull_request_template.md
│   ├── ISSUE_TEMPLATE/
│   │   ├── bug.yml
│   │   └── task.yml
│   └── workflows/
│       ├── ci.yml
│       ├── integration.yml
│       ├── routeros-integration.yml
│       └── release.yml
│
├── docs/
│   ├── architecture/
│   │   ├── overview.md
│   │   └── adr/
│   │       ├── 0001-modular-monolith.md
│   │       ├── 0002-routeros-api-ssl.md
│   │       ├── 0003-node-deployment-atomicity.md
│   │       ├── 0004-postgresql-source-of-truth.md
│   │       └── 0005-no-direct-desktop-routeros-access.md
│   ├── development/
│   │   ├── local-environment.md
│   │   ├── testing.md
│   │   └── git-workflow.md
│   └── operations/
│       ├── controller-configuration.md
│       ├── database-migrations.md
│       └── recovery.md
│
├── schemas/
│   └── policy/
│       └── v1/
│           ├── policy.schema.json
│           └── test-vectors/
│
├── src/
│   ├── Mfc.Domain/
│   ├── Mfc.Application/
│   ├── Mfc.RouterOs/
│   ├── Mfc.Infrastructure/
│   ├── Mfc.Contracts/
│   ├── Mfc.Controller/
│   └── Mfc.Desktop/
│
├── tests/
│   ├── Mfc.UnitTests/
│   ├── Mfc.IntegrationTests/
│   └── Mfc.RouterOs.IntegrationTests/
│
├── testlab/
│   ├── postgres/
│   │   └── compose.yml
│   └── chr/
│       ├── fixtures/
│       ├── topologies/
│       ├── manifest.example.json
│       └── README.md
│
├── .editorconfig
├── .gitattributes
├── .gitignore
├── CHANGELOG.md
├── CONTRIBUTING.md
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── NuGet.config
├── README.md
├── SECURITY.md
└── MikroTikFirewallController.sln

CHR images, database dumps, certificates, private keys і production-конфігурації до репозиторію не додаються.


---

5. Межі assemblies

5.1. Mfc.Domain

Містить чисту предметну модель:

Inventory/
Topology/
Policies/
Deployments/
Drift/
Audit/
Primitives/

Відповідальність:

entities;

value objects;

aggregates;

invariants;

policy model;

deployment state machine;

domain errors;

pure domain services.


Залежності:

Mfc.Domain → жодного проєкту solution

Заборонені залежності:

EF Core;

ASP.NET Core;

Avalonia;

gRPC;

Npgsql;

RouterOS transport;

filesystem;

network;

environment variables;

system clock напряму.


Для часу використовується переданий TimeProvider.


---

5.2. Mfc.Application

Містить прикладні сценарії та абстракції зовнішніх систем:

Inventory/
Topology/
Policies/
Deployments/
Drift/
Audit/
Abstractions/
Validation/

Відповідальність:

use cases;

orchestration;

policy composition;

compiler;

static analysis;

semantic diff;

deployment planning;

interfaces для persistence, RouterOS, secrets, identity і locks;

application error model.


Залежності:

Mfc.Application → Mfc.Domain

Application не знає про:

PostgreSQL;

EF Core;

Avalonia;

gRPC transport;

конкретний RouterOS API client.



---

5.3. Mfc.RouterOs

Містить адаптер RouterOS:

Protocol/
Transport/
Commands/
Mapping/
Discovery/
Snapshots/
Capabilities/

Відповідальність:

API sentence encoding;

API response parsing;

TLS connection;

authentication;

tagged requests;

timeout і cancellation;

typed RouterOS commands;

mapping RouterOS responses;

read-only discovery;

canonical snapshot source data;

надалі — effectful commands через окремо контрольований writer.


Залежності:

Mfc.RouterOs → Mfc.Application
Mfc.RouterOs → Mfc.Domain

Заборонено:

прямий доступ до PostgreSQL;

виклик GUI;

application workflow;

зберігання credentials;

довільний API command endpoint;

формування policy decisions.


Read і write namespaces повинні бути фізично розділені:

Mfc.RouterOs.Read
Mfc.RouterOs.Write

Під час bootstrap Write не реалізується.


---

5.4. Mfc.Infrastructure

Містить реалізації application ports:

Persistence/
Identity/
Security/
Secrets/
Audit/
Locking/
Configuration/

Відповідальність:

PostgreSQL;

EF Core mappings;

migrations;

durable locks;

encrypted secrets;

OIDC integration;

audit persistence;

idempotency records;

controller instance state.


Залежності:

Mfc.Infrastructure → Mfc.Application
Mfc.Infrastructure → Mfc.Domain

Mfc.Infrastructure не повинен залежати від Mfc.Controller.


---

5.5. Mfc.Contracts

Містить gRPC-контракти:

Protos/
Generated/

Protobuf package:

mfc.v1

Відповідальність:

RPC messages;

RPC services;

stable error envelope;

pagination contracts;

streaming deployment status contracts.


У protobuf не повинні безпосередньо експортуватися:

EF entities;

domain aggregates;

RouterOS DTO;

secret values;

internal database IDs без явного контрактного призначення.


Generated C# файли не комітяться. Вони генеруються під час build.


---

5.6. Mfc.Controller

Composition root і process host:

Grpc/
Background/
Authorization/
Configuration/
Health/
Program.cs

Відповідальність:

dependency injection;

gRPC endpoints;

authentication;

authorization;

error mapping;

hosted services;

scheduled discovery;

drift polling;

deployment recovery;

health endpoints;

structured logging.


Залежності:

Mfc.Controller
 ├── Mfc.Application
 ├── Mfc.Infrastructure
 ├── Mfc.RouterOs
 └── Mfc.Contracts

У gRPC endpoint не повинна міститися domain logic.


---

5.7. Mfc.Desktop

Структура за функціональними модулями:

Shell/
Features/
    Dashboard/
    Inventory/
    Topology/
    Policies/
    Validation/
    Diff/
    Deployments/
    Drift/
    Audit/
Infrastructure/
    Grpc/
    Authentication/
    Storage/

Залежності:

Mfc.Desktop → Mfc.Contracts

Desktop не залежить від:

Domain;

Application;

Infrastructure;

RouterOS;

Npgsql;

EF Core.


Desktop не має RouterOS API client.


---

6. Контроль залежностей

Architecture tests повинні блокувати:

Domain → будь-який інший Mfc assembly
Application → Infrastructure
Application → RouterOs
Application → Controller
Application → Desktop
Infrastructure → Controller
RouterOs → Infrastructure
Desktop → Domain
Desktop → Application
Desktop → Infrastructure
Desktop → RouterOs

Додаткові правила:

Mfc.RouterOs.Read не залежить від Mfc.RouterOs.Write;

domain model не використовує gRPC-generated types;

persistence entities не виходять за межі Infrastructure;

GUI ViewModels не містять SQL, RouterOS commands або domain compiler;

application services не отримують IServiceProvider;

жоден модуль не використовує service locator.


Architecture tests запускаються в кожному pull request.


---

7. Внутрішня організація коду

Код структурується за функціональністю, а не за загальними технічними типами.

Правильно:

Policies/
    Policy.cs
    PolicyRevision.cs
    PolicyCompiler.cs
    ValidatePolicy.cs

Неправильно:

Entities/
Services/
Managers/
Helpers/
Utils/
Models/

Назви Manager, Processor, Handler, Helper допускаються лише тоді, коли вони точно описують відповідальність. Загальні контейнери з різнорідною логікою заборонені.

Один public top-level type — один файл, крім тісно пов’язаних малих типів або generated code.


---

8. .NET toolchain

8.1. global.json

SDK фіксується конкретною feature-band версією:

{
  "sdk": {
    "version": "<exact-sdk-version>",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}

Заборонено:

latest;

preview SDK;

різні SDK у локальному build і CI;

незафіксований SDK у release pipeline.



---

8.2. Directory.Build.props

Обов’язкові параметри:

<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <Deterministic>true</Deterministic>
    <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
  </PropertyGroup>
</Project>

NoWarn дозволено додавати лише:

локально для конкретного проєкту;

із коментарем;

із посиланням на issue;

без глобального вимкнення категорії аналізаторів.



---

8.3. Central Package Management

Усі версії NuGet packages визначаються лише в:

Directory.Packages.props

У .csproj заборонено вказувати Version.

Заборонені:

wildcard versions;

floating versions;

package references із Git branches;

неперевірені локальні .nupkg;

дублювання версій;

пакети без ліцензійного та security review.


packages.lock.json комітиться для кожного проєкту.

CI використовує:

dotnet restore --locked-mode


---

9. Мінімальний набір залежностей

Controller та infrastructure

ASP.NET Core;

gRPC;

EF Core;

Npgsql;

Microsoft OIDC authentication packages;

Protobuf.


Desktop

Avalonia;

Avalonia desktop runtime;

MVVM Toolkit;

gRPC client;

Protobuf.


Tests

xUnit;

.NET test SDK;

coverage collector;

PostgreSQL ephemeral test environment.


На bootstrap-етапі не додаються:

MediatR;

AutoMapper;

FluentValidation;

Polly;

Serilog;

Hangfire;

MassTransit;

Redis client;

generic repository libraries;

mocking frameworks.


Для unit tests використовуються прості hand-written fakes і stubs. Mocking framework додається лише після доведеної необхідності.


---

10. Стандарти C#

Обов’язково:

file-scoped namespaces;

nullable reference types;

immutable policy revisions;

DateTimeOffset для часових значень;

UTC у persistence;

TimeProvider для часу;

CancellationToken для всіх I/O operations;

asynchronous I/O;

structured logging;

explicit timeout;

bounded retries;

bounded queues;

source-generated serialization там, де це практично;

invariant culture для hashes і canonical representations;

явні типи для IP prefix, port interval і hash.


Заборонено:

async void
Task.Result
Task.Wait()
Thread.Sleep()
fire-and-forget Task
catch (Exception) без boundary-рівня
CancellationToken.None у довгих операціях
Task.Run для мережевого I/O
dynamic
Thread.Abort
необмежений Channel
необмежений retry loop

Винятки використовуються для:

programming errors;

failed infrastructure operations;

порушення неможливих внутрішніх станів.


Очікувані domain/application failures повертаються через типізований result/error model.


---

11. Git workflow

11.1. Branches

Основна гілка:

main

Робочі гілки:

feat/<issue>-<short-name>
fix/<issue>-<short-name>
refactor/<issue>-<short-name>
test/<issue>-<short-name>
docs/<issue>-<short-name>
chore/<issue>-<short-name>

Branches повинні бути короткоживучими.

develop, довготривалі release branches і персональні інтеграційні branches не використовуються.


---

11.2. Commits

Conventional Commits:

feat
fix
refactor
perf
test
docs
build
ci
chore
revert

Приклади:

feat(inventory): add site aggregate
fix(routeros): handle fragmented API sentences
test(policy): cover deterministic rule ordering
build(dotnet): enable central package management

Коміт повинен:

містити одну логічну зміну;

не змішувати форматування з функціональною зміною;

не містити generated build artifacts;

не містити секретів;

проходити локальний build і релевантні тести.



---

11.3. Pull requests

PR повинен містити:

Issue
Мета
Зміни
Ризики
Тести
Зміни БД
Зміни контрактів
Security impact
Rollback

Обов’язкові вимоги:

актуальна гілка відносно main;

green CI;

відсутність unresolved comments;

linked issue;

не менше одного review;

не менше двох reviews для high-risk змін;

squash merge;

PR title відповідає Conventional Commits.


High-risk зміни:

RouterOS write path;

deployment engine;

management guard;

rollback watchdog;

secrets;

authentication;

authorization;

audit;

migrations;

CI/release pipeline.



---

11.4. Захист main

Обов’язково:

direct push заборонений;

force push заборонений;

branch deletion заборонений;

PR required;

status checks required;

stale approval скасовується після нового commit;

conversation resolution required;

history linear;

administrator bypass журналюється;

release tags підписуються.



---

12. CODEOWNERS

Обов’язкове окреме review для:

.github/workflows/**
src/Mfc.RouterOs/**
src/Mfc.Application/Deployments/**
src/Mfc.Infrastructure/Security/**
src/Mfc.Infrastructure/Secrets/**
src/Mfc.Infrastructure/Persistence/Migrations/**
schemas/**

RouterOS write, management guard і watchdog зміни повинні мати щонайменше двох власників коду.


---

13. CI pipeline

13.1. ci.yml

Запускається для кожного PR і push у main.

Етапи:

checkout
→ verify repository state
→ setup pinned SDK
→ restore --locked-mode
→ format verification
→ build Release
→ unit tests
→ architecture tests
→ coverage verification
→ package vulnerability scan

Основні команди:

dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build

GitHub Actions dependencies фіксуються повним commit SHA, а не mutable tag.

Job permissions задаються за принципом мінімальних прав. За замовчуванням:

permissions:
  contents: read


---

13.2. Build matrix

Обов’язкові платформи:

Job	ОС

Domain/Application build	Linux
Controller build	Linux
Desktop build	Windows
Unit tests	Linux
PostgreSQL integration	Linux
Desktop smoke test	Windows


Release не може базуватися лише на build однієї ОС.


---

13.3. Coverage

Мінімальні пороги на bootstrap:

Assembly	Line	Branch

Mfc.Domain	85%	75%
Mfc.Application	85%	75%
Mfc.RouterOs.Protocol	90%	80%


Поріг не застосовується механічно до generated code, migrations і Avalonia views.

Coverage не замінює acceptance tests.


---

13.4. integration.yml

Перевіряє:

PostgreSQL schema creation;

застосування всіх migrations з нуля;

idempotency constraints;

optimistic concurrency;

durable locks;

audit hash chain;

JSONB policy persistence;

restart persistence;

database restore smoke test.


SQLite як заміна PostgreSQL заборонений.


---

13.5. routeros-integration.yml

Виконується на ізольованому self-hosted runner з CHR.

Trigger:

pull request із змінами в Mfc.RouterOs;

зміни deployment logic;

зміни canonicalization;

зміни capability detection;

manual run;

nightly run;

release candidate.


Runner не повинен мати доступу до production management network.

Початкові перевірки:

TLS connection
API authentication
fragmented response handling
tag correlation
timeout
cancel
read-only discovery
repeatable canonical snapshot
controlled configuration diff

Жодний тест не використовує production RouterOS credentials або exports.


---

14. Release pipeline

Release запускається тільки annotated signed tag:

v<major>.<minor>.<patch>

Послідовність:

1. Перевірити, що tag вказує на commit у main.


2. Виконати повний CI.


3. Виконати PostgreSQL integration tests.


4. Виконати CHR test matrix.


5. Зібрати controller.


6. Зібрати desktop installer.


7. Створити migration bundle або idempotent migration script.


8. Створити SBOM.


9. Створити SHA-256 checksums.


10. Підписати release artifacts.


11. Сформувати release notes.


12. Опублікувати immutable artifacts.



Release pipeline не виконує автоматичне production deployment.

Версія compiler обов’язково вбудовується в assembly metadata і використовується під час формування artifact hash.


---

15. Версіонування

Потрібні чотири незалежні версії:

Application version
gRPC API version
Policy schema version
Compiler version

Приклад:

Application:     0.1.0
gRPC package:    mfc.v1
Policy schema:   1
Compiler:        0.1.0

Database schema version визначається послідовністю migrations.

До стабілізації контрактів використовується:

0.y.z

Breaking change в protobuf не вноситься в наявний mfc.v1. Для нього створюється новий contract package.


---

16. Database bootstrap

16.1. Початкова migration

Перша migration повинна створювати лише необхідний bootstrap-набір:

controller_instances
schema_metadata
audit_events
encrypted_secrets
idempotency_records

Повна domain schema додається атомарними issues наступного етапу.

16.2. Migration policy

Обов’язково:

migration комітиться разом із кодом;

migration перевіряється на порожній БД;

migration перевіряється на попередній release schema;

startup controller не виконує migration автоматично;

production migration запускається окремою командою;

destructive migration потребує окремого ADR;

rollback schema виконується через restore backup, а не автоматичний Down().


CLI:

Mfc.Controller --migrate-only

Після migration process завершується і не запускає gRPC server.


---

17. Конфігурація

17.1. Джерела

Порядок пріоритету:

appsettings.json
→ appsettings.<Environment>.json
→ environment variables
→ OS/service secret provider
→ command-line overrides

Production secrets не зберігаються в JSON-файлах.

Префікс environment variables:

MFC__

Приклади:

MFC__Database__ConnectionString
MFC__Grpc__ListenAddress
MFC__Security__MasterKeyProvider
MFC__Authentication__Authority

17.2. Startup validation

Controller повинен завершувати startup з помилкою при:

відсутній connection string;

відсутньому master key provider;

invalid TLS certificate;

development authentication у production;

незастосованій обов’язковій migration;

некоректних timeout або concurrency limits;

wildcard gRPC bind без належного TLS.



---

18. Local development

PostgreSQL запускається через:

testlab/postgres/compose.yml

Compose-файл:

не містить production password;

використовує окрему development database;

має persistent volume лише за явною потребою;

прив’язує порт до loopback;

фіксує image version і digest.


Development authentication дозволений лише коли одночасно виконано:

Environment == Development
gRPC bind == loopback
explicit development-auth flag == true

Controller повинен відмовитися запускатися з development authentication на зовнішньому інтерфейсі.

RouterOS credentials для локальної розробки зберігаються через .NET user secrets або environment variables.


---

19. CHR testlab

19.1. Обмеження

До Git не додаються:

CHR disk images;

MikroTik license files;

downloaded RouterOS packages;

VM snapshots;

production exports.


manifest.example.json описує:

{
  "routerosVersion": "<version>",
  "architecture": "x86_64",
  "imageSha256": "<sha256>",
  "requiredTopologies": [
    "standalone",
    "multi-wan",
    "vrrp"
  ]
}

19.2. Початкові топології

standalone/
multi-wan-failover/
multi-wan-balanced/
vrrp-active-passive/
vrrp-split-master/

Кожна topology повинна мати:

isolated management network;

isolated WAN simulators;

deterministic addresses;

generated test credentials;

initial RouterOS fixture;

reset procedure;

expected snapshot hashes;

cleanup procedure.


19.3. Ізоляція

CHR runner:

не має маршруту до production;

не використовує production DNS;

не використовує production PKI;

генерує локальну test CA;

очищує credentials після job;

повертає VMs до clean snapshot;

знищує test artifacts після завершення.



---

20. Test fixtures

Заборонено комітити реальні корпоративні RouterOS exports.

Fixtures повинні бути:

синтетичними;

мінімальними;

детермінованими;

без секретів;

без реальних public IP;

без реальних назв філій;

із поясненням конкретного test case.


Raw API responses дозволено зберігати лише після sanitization.

Для protocol parser обов’язкові fixtures:

single sentence
multiple records
fragmented length field
fragmented word
interleaved tags
trap response
fatal response
empty response
oversized response
invalid encoding
connection close mid-sentence


---

21. Документація

21.1. README.md

Містить тільки:

призначення;

поточний scope;

архітектурну схему;

prerequisites;

build;

test;

local run;

посилання на нормативні документи.


README не дублює повне ТЗ.

21.2. ADR

ADR потрібен для зміни:

меж assemblies;

deployment atomicity;

policy ownership model;

transport до RouterOS;

database technology;

authentication model;

secret storage;

gRPC versioning;

rollback mechanism.


ADR має статус:

Proposed
Accepted
Superseded
Rejected

Accepted ADR не редагується ретроспективно. Зміна оформлюється новим ADR.


---

22. Security baseline

Обов’язково ввімкнути:

branch protection;

secret scanning;

dependency vulnerability scanning;

dependency review;

static analysis;

minimal GitHub Actions permissions;

CODEOWNERS;

signed release tags;

package lock files;

SBOM для release;

checksum release artifacts.


Заборонено:

secrets у repository variables без protected environment;

production keys у self-hosted CHR runner;

reusable production certificates у testlab;

передачу credentials у command-line arguments;

виведення connection strings у logs;

debug logging API authentication payloads;

використання unpinned CI actions;

виконання untrusted PR-коду на runner із production access.



---

23. Logging baseline

Формат:

{
  "timestamp": "UTC",
  "level": "Information",
  "event": "inventory.discovery.completed",
  "correlationId": "...",
  "deviceId": "...",
  "durationMs": 123,
  "result": "success"
}

Не логуються:

passwords;

tokens;

private keys;

connection strings;

full authorization headers;

RouterOS login sentence;

encrypted secret plaintext;

raw configuration без sanitization.


Exception stack trace доступний у controller logs, але не повертається desktop-клієнту як звичайне повідомлення.


---

24. Repository metadata

24.1. Labels

Початковий набір:

type:feature
type:bug
type:refactor
type:test
type:docs

area:domain
area:application
area:routeros
area:controller
area:desktop
area:persistence
area:security
area:testlab

risk:high
blocked
needs-adr

Дублюючі labels не створюються.

24.2. Milestones

На bootstrap створюються лише:

M0 — Repository Bootstrap
M1 — Read-Only Vertical Slice

Наступні milestones створюються після завершення M1.


---

25. Послідовність bootstrap-комітів

Commit 1

chore(repo): initialize repository governance

Містить:

.gitignore;

.gitattributes;

README.md;

CONTRIBUTING.md;

SECURITY.md;

PR template;

issue templates;

CODEOWNERS.


Commit 2

build(dotnet): add pinned SDK and package management

Містить:

global.json;

Directory.Build.props;

Directory.Packages.props;

NuGet.config;

.editorconfig;

tool manifest.


Commit 3

feat(skeleton): add solution and project boundaries

Містить:

solution;

сім production projects;

три test projects;

project references;

minimal buildable types.


Commit 4

test(architecture): enforce assembly dependency rules

Містить:

architecture tests;

forbidden reference tests;

namespace boundary tests.


Commit 5

feat(controller): add health-only controller host

Містить:

ASP.NET Core host;

gRPC health service;

configuration validation;

structured logs;

graceful shutdown.


Commit 6

feat(desktop): add controller connection shell

Містить:

Avalonia application shell;

controller endpoint configuration;

gRPC health check;

connection-state display;

жодної RouterOS логіки.


Commit 7

feat(persistence): add PostgreSQL bootstrap migration

Містить:

DbContext;

bootstrap tables;

migration command;

PostgreSQL integration tests.


Commit 8

ci: add deterministic validation pipelines

Містить:

CI;

integration workflow;

dependency checks;

artifact retention rules.


Commit 9

test(routeros): add isolated CHR lab skeleton

Містить:

testlab manifests;

topology contracts;

test CA procedure;

runner documentation;

read-only smoke-test placeholder.


Commit 10

docs(architecture): record initial architecture decisions

Містить п’ять початкових ADR.

Кожний commit повинен самостійно компілюватися і проходити відповідні тести.


---

26. Bootstrap acceptance criteria

Bootstrap завершений лише коли:

1. Репозиторій клонується на чисте середовище.


2. SDK автоматично визначається через global.json.


3. dotnet restore --locked-mode успішний.


4. Усі package versions визначені централізовано.


5. Відсутні floating dependencies.


6. Solution збирається в Release без warnings.


7. Усі architecture tests проходять.


8. Domain не має зовнішніх infrastructure dependencies.


9. Desktop залежить лише від Contracts.


10. Desktop запускається та показує стан controller connection.


11. Controller запускається як окремий process.


12. gRPC health check працює.


13. Controller коректно завершується через graceful shutdown.


14. PostgreSQL запускається через development compose.


15. Bootstrap migration застосовується до порожньої БД.


16. Повторний запуск migration не змінює schema.


17. Controller не запускається з непройденою migration.


18. Development authentication неможливо запустити на зовнішньому bind.


19. У repository немає secrets.


20. У repository немає CHR image.


21. CI проходить на Linux і Windows.


22. Release build є детермінованим на рівні managed assemblies.


23. main захищена від direct push.


24. CODEOWNERS застосовується до критичних модулів.


25. Жодний production assembly не містить RouterOS write command.


26. CHR runner ізольований від production network.


27. Усі початкові ADR мають статус Accepted.


28. Усі bootstrap issues закриті.


29. CHANGELOG.md містить запис 0.1.0-bootstrap.


30. Git working tree після build і tests залишається чистим.




---

27. Поза bootstrap

На цьому етапі не реалізуються:

inventory domain повністю;

topology discovery;

RouterOS API protocol;

RouterOS authentication;

policy editor;

canonicalization;

snapshots;

diff;

deployment;

watchdog;

VRRP coordinator;

drift detection;

production OIDC;

production installer;

switch management.


Єдина наскрізна функція bootstrap:

Desktop
  → gRPC
  → Controller
  → PostgreSQL health
  → Connection status у GUI

Жодного підключення до RouterOS.


---

28. Результат

Після завершення bootstrap репозиторій має бути готовим до першого функціонального зрізу:

RouterOS API-SSL
→ read-only discovery
→ canonical snapshot
→ hash
→ persistence
→ semantic diff
→ desktop display

Наступний нормативний документ:
MikroTik Firewall Controller — Initial Issue Set v0.1.