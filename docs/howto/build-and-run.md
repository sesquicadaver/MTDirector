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

Стилі оболонки: `App.axaml` (`mfc-panel` / `mfc-toolbar` / …). Розмітка модулів — `MainWindow.axaml` (WrapPanel для тулбарів; вузькі колонки з ellipsis).

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

- `OUT_DIR/controller/` — `Mfc.Controller` (включно з `mfc-controller.service` + `mfc-controller.winsw.xml`, OPS-HOST-BUNDLE-01, + `mfc-controller.env.example`, OPS-HOST-ENV-01, + `mfc-controller.sysusers` + `mfc-controller.tmpfiles`, OPS-HOST-SYSUSERS-01, + `README.md` (OPS-HOST-DOC-01))
- `OUT_DIR/desktop/` (включно з `mfc-desktop.desktop` + `mfc-desktop-start-menu.ps1`, DESK-HOST-BUNDLE-01) + `Mfc.Desktop-linux-x64.zip` (або `.tar.gz`)
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

**Controller (Linux, systemd):** шаблон unit — [`../../packaging/systemd/mfc-controller.service`](../../packaging/systemd/mfc-controller.service); `package-controller.sh` також кладе копію в `$OUT_DIR/controller/mfc-controller.service` (OPS-HOST-BUNDLE-01). Приклад EnvironmentFile — [`../../packaging/systemd/mfc-controller.env.example`](../../packaging/systemd/mfc-controller.env.example) (також у `$OUT_DIR/controller/`, OPS-HOST-ENV-01). Sysusers/tmpfiles — [`../../packaging/systemd/mfc-controller.sysusers`](../../packaging/systemd/mfc-controller.sysusers) + [`../../packaging/systemd/mfc-controller.tmpfiles`](../../packaging/systemd/mfc-controller.tmpfiles) (також у `$OUT_DIR/controller/`, OPS-HOST-SYSUSERS-01). Operator README — [`../../packaging/doc/mfc/README.md`](../../packaging/doc/mfc/README.md) → `$OUT_DIR/controller/README.md` → `/usr/share/doc/mfc/README.md` (OPS-HOST-DOC-01; matches unit `Documentation=`). Типово: `systemd-sysusers` + `systemd-tmpfiles --create`, скопіювати publish tree у `/opt/mfc/controller`, встановити unit у `/etc/systemd/system/`, скопіювати env.example → `/etc/mfc/controller.env` і заповнити `MFC__…`, потім `systemctl enable --now mfc-controller.service`. Деталі — [`../operations/installation.md`](../operations/installation.md).

**Controller (Windows, WinSW):** шаблон — [`../../packaging/windows/mfc-controller.winsw.xml`](../../packaging/windows/mfc-controller.winsw.xml); `package-controller.sh` також кладе копію в `$OUT_DIR/controller/mfc-controller.winsw.xml` (OPS-HOST-BUNDLE-01). Типово: `MFC_RELEASE_RID=win-x64` publish → `C:\mfc\controller\`, скопіювати XML як `mfc-controller.xml` поруч із WinSW `mfc-controller.exe`, виставити `MFC__…`, потім `mfc-controller.exe install/start`. Деталі — [`../operations/installation.md`](../operations/installation.md).

**Desktop (Linux):** шаблон freedesktop desktop-entry — [`../../packaging/linux/mfc-desktop.desktop`](../../packaging/linux/mfc-desktop.desktop) (DESK-HOST-LINUX-01); `package-desktop.sh` також кладе копію в `$OUT_DIR/desktop/mfc-desktop.desktop` (DESK-HOST-BUNDLE-01). Типово: розпакувати publish zip у `/opt/mfc`, встановити `.desktop` з артефакту або з `packaging/linux/` у `/usr/share/applications/` або `~/.local/share/applications/`, потім запускати з меню або `/opt/mfc/desktop/Mfc.Desktop`.

```bash
unzip "$OUT_DIR/Mfc.Desktop-linux-x64.zip" -d /opt/mfc
# відредагувати desktop/appsettings.json → ControllerEndpoint
sudo install -m 0644 packaging/linux/mfc-desktop.desktop /usr/share/applications/mfc-desktop.desktop
/opt/mfc/desktop/Mfc.Desktop
```

**Desktop (Windows):** шаблон Start Menu shortcut — [`../../packaging/windows/mfc-desktop-start-menu.ps1`](../../packaging/windows/mfc-desktop-start-menu.ps1) (DESK-HOST-WIN-01); `package-desktop.sh` також кладе копію в `$OUT_DIR/desktop/mfc-desktop-start-menu.ps1` (DESK-HOST-BUNDLE-01). Типово: розпакувати `Mfc.Desktop-win-x64.zip` у `C:\mfc\desktop\`, виставити `ControllerEndpoint`, потім `powershell -File C:\mfc\desktop\mfc-desktop-start-menu.ps1 -InstallRoot C:\mfc\desktop` (або з `packaging\windows\`).

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
4. Systemd unit template for framework-dependent Controller: [`../../packaging/systemd/mfc-controller.service`](../../packaging/systemd/mfc-controller.service) (OPS-HOST-SYSTEMD-01).
5. Windows Service (WinSW) template for framework-dependent Controller: [`../../packaging/windows/mfc-controller.winsw.xml`](../../packaging/windows/mfc-controller.winsw.xml) (OPS-HOST-WINSVC-01).
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
