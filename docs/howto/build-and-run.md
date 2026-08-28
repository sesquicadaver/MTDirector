# HOWTO — збірка, пакування та запуск (Linux / Windows)

Операторський і developer-посібник для **MTDirector** (`v0.2.0` + P2 gates + Desktop Add router).  
Нормативні ТЗ не дублюються — див. [`../specs/README.md`](../specs/README.md).

## Що запускається

| Компонент | Роль | Процес |
|-----------|------|--------|
| `Mfc.Controller` | gRPC API, PostgreSQL, RouterOS adapters | Окремий host-процес |
| `Mfc.Desktop` | Avalonia GUI (Contracts-only клієнт) | Окремий GUI-процес |
| PostgreSQL | Єдине джерело істини | Docker compose або власний інстанс |

Закриття Desktop **не** зупиняє Controller.

Default Dev endpoint: `http://127.0.0.1:5101` (`Desktop:ControllerEndpoint` / `Mfc:Grpc:ListenAddress`).

## Платформи (фактичний стан)

| Платформа | Controller | Desktop | Пакування (`scripts/release`) | CI |
|-----------|------------|---------|--------------------------------|----|
| **Linux x64** | Так (основна Dev/prod host) | Так (Avalonia) | Default `MFC_RELEASE_RID=linux-x64` | Linux validate |
| **Windows x64** | Так (`dotnet`) | Так (окремий job) | `MFC_RELEASE_RID=win-x64` | Windows Desktop build |
| **macOS** | Теоретично `osx-x64` / `osx-arm64` | Avalonia Desktop | RID можна задати, **немає** CI gate і HOWTO smoke | Немає |
| **ARM Linux** | `linux-arm64` можливий через RID | Avalonia | Не перевірено в CI | Немає |

Усі publish-скрипти — **framework-dependent** (`--self-contained false`): на цільовій машині потрібен .NET **10** runtime (ASP.NET Core Shared Framework для Controller).

SDK pin: [`global.json`](../../global.json) (`10.0.302`, `allowPrerelease: false`).

---

## 1. Передумови

### Спільні

1. Встановити [.NET SDK 10](https://dotnet.microsoft.com/download) відповідно до `global.json`.
2. Docker (PostgreSQL для Controller і Testcontainers).
3. Git.

Перевірка:

```bash
dotnet --info
# SDK version має відповідати / rollForward latestPatch від global.json
```

### Linux (Debian/Ubuntu приклад)

```bash
# SDK — офіційний install script або пакет дистрибутива з версією 10.x
export PATH="$HOME/.dotnet:$PATH"
sudo apt-get install -y docker.io zip   # zip опційно для release-архіву
```

Для Avalonia GUI на headless CI не потрібно; на desktop-сесії потрібні звичайні X11/Wayland залежності дистрибутива.

### Windows

1. Install [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) (для PG / тестів).
3. PowerShell 7+ або cmd; bash-скрипти `scripts/release/*.sh` зручніше з Git Bash / WSL.

WSL2: Controller + PG у WSL; Desktop можна збирати в Windows host (`win-x64`) або в WSL (Linux RID) — не змішуйте endpoint без явного `127.0.0.1`/port publish.

---

## 2. Збірка з джерела (обидві платформи)

```bash
git clone https://github.com/sesquicadaver/MTDirector.git
cd MTDirector
dotnet tool restore
dotnet restore MikroTikFirewallController.sln --locked-mode
dotnet build MikroTikFirewallController.sln -c Release
```

Тести (потрібен Docker для Integration):

```bash
dotnet test tests/Mfc.UnitTests -c Release
dotnet test tests/Mfc.IntegrationTests -c Release
```

Лише Desktop:

```bash
dotnet build src/Mfc.Desktop/Mfc.Desktop.csproj -c Release
```

---

## 3. Локальний запуск (Development)

Детальніше PG: [`../development/local-environment.md`](../development/local-environment.md).

### PostgreSQL

```bash
docker compose -f testlab/postgres/compose.yml up -d
```

Host port за замовчуванням: `127.0.0.1:5432` (контейнер `mfc-postgres-dev`).

### Міграції + Controller

```bash
# Linux / macOS / Git Bash
export PATH="$HOME/.dotnet:$PATH"
dotnet run --project src/Mfc.Controller -- --environment Development --migrate-only
dotnet run --project src/Mfc.Controller -- --environment Development
```

```powershell
# Windows PowerShell
dotnet run --project src/Mfc.Controller -- --environment Development --migrate-only
dotnet run --project src/Mfc.Controller -- --environment Development
```

Development дозволяє `http://127.0.0.1:5101` лише з `AllowInsecureLoopback=true` (уже в `appsettings.Development.json`). Production вимагає `https://…`.

RouterOS adapters fail-closed, доки не увімкнете:

```bash
export MFC__RouterOs__Enabled=true          # read / capture
export MFC__RouterOs__WriteEnabled=true     # onboarding / deploy (лише lab)
```

Checklist: [`../operations/pilot-runbook.md`](../operations/pilot-runbook.md).

### Desktop

```bash
dotnet run --project src/Mfc.Desktop
```

1. **Connect** → endpoint з `src/Mfc.Desktop/appsettings.json` (`http://127.0.0.1:5101`).
2. **Inventory → Add router** — Site → Node → Device + credentials ([`../development/connection-profiles.md`](../development/connection-profiles.md)). Optional: select a seed Device → **Load MikroTik neighbors** → Apply → credentials → Submit.

Зупинка Controller (Linux приклад): знайти процес, що слухає `5101`, і завершити його окремо від Desktop.

---

## 4. Release-пакування (framework-dependent)

Скрипти: [`../../scripts/release/`](../../scripts/release/). Нотатки: [`../release/packaging.md`](../release/packaging.md).

### Linux x64 (default)

```bash
export PATH="$HOME/.dotnet:$PATH"
OUT_DIR="$(mktemp -d)"
export OUT_DIR
./scripts/release/package-controller.sh
./scripts/release/package-desktop.sh
./scripts/release/create-migration-bundle.sh
./scripts/release/generate-sbom-and-checksums.sh
ls -la "$OUT_DIR"
```

Артефакти:

- `OUT_DIR/controller/` — `Mfc.Controller`
- `OUT_DIR/desktop/` + `Mfc.Desktop-linux-x64.zip` (або `.tar.gz`)
- `OUT_DIR/migrations/mfc-ef-migrations`
- `OUT_DIR/SHA256SUMS`

### Windows x64

У Git Bash / WSL (рекомендовано для `.sh`):

```bash
export MFC_RELEASE_RID=win-x64
export OUT_DIR="/c/temp/mfc-rel"   # або інший шлях
mkdir -p "$OUT_DIR"
./scripts/release/package-controller.sh
./scripts/release/package-desktop.sh
```

Або еквівалент без bash (ручний publish):

```powershell
$Out = "C:\temp\mfc-rel"
New-Item -ItemType Directory -Force -Path "$Out\controller","$Out\desktop" | Out-Null
dotnet publish src\Mfc.Controller\Mfc.Controller.csproj -c Release -r win-x64 --self-contained false -o "$Out\controller"
dotnet publish src\Mfc.Desktop\Mfc.Desktop.csproj -c Release -r win-x64 --self-contained false -o "$Out\desktop"
```

На цільовій Windows-машині: [.NET 10 Desktop + ASP.NET Core Runtime](https://dotnet.microsoft.com/download).

### Запуск з пакету

**Controller (Linux):**

```bash
cd "$OUT_DIR/controller"
# налаштувати MFC__Database__ConnectionString, TLS, RouterOs flags
./Mfc.Controller
# або спочатку: ./mfc-ef-migrations  (шлях до migrations bundle)
```

**Desktop (Linux):**

```bash
unzip "$OUT_DIR/Mfc.Desktop-linux-x64.zip" -d /opt/mfc
# відредагувати desktop/appsettings.json → ControllerEndpoint
/opt/mfc/desktop/Mfc.Desktop
```

**Desktop (Windows):** розпакувати `Mfc.Desktop-win-x64.zip`, запустити `Mfc.Desktop.exe`, виставити `ControllerEndpoint`.

Офіційна install-нотатка (коротша): [`../operations/installation.md`](../operations/installation.md).

---

## 5. Типові збої

| Симптом | Дія |
|---------|-----|
| SDK mismatch | Звірити `dotnet --list-sdks` з `global.json` |
| Desktop «не коннектиться» | Controller слухає? Порт 5101? Endpoint у `appsettings.json` |
| PG connection refused | `docker compose … up -d`; connection string / remap порту |
| Production HTTP bind rejected | Потрібен HTTPS або лише Dev + loopback + `AllowInsecureLoopback` |
| RouterOS probe fail-closed | `Mfc:RouterOs:Enabled=true` + connection profile |
| Порт 5101 зайнятий після закриття Desktop | Зупинити orphan Controller |

---

## 6. Відомі прогалини HOWTO / packaging (не дефекти MVP DoD)

1. Немає native MSI / AppImage / `.dmg` — лише zip/tar publish ([`../release/known-limitations.md`](../release/known-limitations.md)).
2. macOS / `linux-arm64` не покриті CI; RID можна спробувати, без гарантії.
3. Self-contained single-file publish **не** є default у release-скриптах.
4. Немає systemd unit / Windows Service шаблонів у репо (ручний host або власний unit).
5. CHR lab images не в Git — окремо [`../development/chr-lab.md`](../development/chr-lab.md).

---

## Пов’язані документи

| Документ | Коли читати |
|----------|-------------|
| [`../development/local-environment.md`](../development/local-environment.md) | Dev workstation / PG |
| [`../operations/installation.md`](../operations/installation.md) | Deploy з release артефактів |
| [`../operations/controller-configuration.md`](../operations/controller-configuration.md) | `Mfc:*` keys |
| [`../operations/pilot-runbook.md`](../operations/pilot-runbook.md) | Увімкнення RouterOS read/write |
| [`../release/packaging.md`](../release/packaging.md) | Деталі скриптів release |
| [`../development/testing.md`](../development/testing.md) | Living Spec / test filters |
