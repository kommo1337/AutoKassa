# 16-cicd — CI/CD: сборка, тесты и валидация документации

> Этот файл описывает непрерывную интеграцию AutoKassa: единственный workflow `.NET CI`, его job'ы и шаги, проверки документации (JSON, DocValidator, Markdown-ссылки), локальное воспроизведение проверок и типичные падения. Все шаги сверены с [`ci.yml`](../../.github/workflows/ci.yml) и [`README.md`](../../tools/DocValidator/README.md) DocValidator.

---

## 1. Обзор

- В репозитории **один** workflow: [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml), имя — `.NET CI`. Других workflow (release, scheduled, manual) нет.
- **Триггеры:** `push` в ветку `master` и `pull_request` в ветку `master`. Ручной запуск (`workflow_dispatch`), расписание и релизные триггеры **не настроены**.
- Workflow состоит из **двух независимых job'ов** (между ними нет `needs` — выполняются параллельно):
  - [`build-and-test`](#2-job-build-and-test-windows-latest) — сборка и тесты на `windows-latest`;
  - [`docs`](#3-job-docs-ubuntu-latest) — валидация документации на `ubuntu-latest`.
- PR считается «зелёным», только когда успешны **оба** job'а.
- Явные таймауты (`timeout-minutes`) в workflow не заданы — действует стандартный лимит GitHub Actions (360 минут на job). Артефакты (`actions/upload-artifact`) ни один job не публикует.
- `global.json` в репозитории нет: оба job'а ставят SDK через `actions/setup-dotnet@v4` с `dotnet-version: '10.0.x'` — берётся последний доступный патч .NET 10.
- Приложение Windows-only (WPF), поэтому сборка и тесты идут на `windows-latest`. DocValidator — кроссплатформенное консольное приложение, поэтому job `docs` работает на более дешёвом `ubuntu-latest`.
- В корень решения [`AutoKassa.sln`](../../AutoKassa.sln) входят 4 проекта: `AutoKassa`, `AutoKassa.Tests`, `tools/DocValidator`, `tools/DocValidator.Tests`. Команды `dotnet restore` / `dotnet build` / `dotnet test` в корне покрывают все четыре.

---

## 2. Job `build-and-test` (windows-latest)

| # | Шаг | Action / команда | Что делает |
|---|-----|------------------|-----------|
| 1 | Checkout | `actions/checkout@v4` | Клонирует репозиторий |
| 2 | Setup .NET 10 | `actions/setup-dotnet@v4`, `dotnet-version: '10.0.x'` | Устанавливает .NET SDK 10 |
| 3 | Restore dependencies | `dotnet restore` | Восстанавливает NuGet-пакеты всех проектов решения |
| 4 | Build | `dotnet build --no-restore --configuration Release` | Собирает всё решение в **Release** (не Debug, как локальный дефолт) |
| 5 | Test | `dotnet test --configuration Release --verbosity normal` | Запускает тесты `AutoKassa.Tests` **и** `tools/DocValidator.Tests` |

Особенности:

- Сборка идёт с `--no-restore`, поэтому шаг Restore обязателен — без него Build упадёт.
- `dotnet test` вызван **без** `--no-build`: тестовые проекты пересобираются в Release перед прогоном.
- Любой упавший шаг завершает job с ошибкой; последующие шаги не выполняются.

---

## 3. Job `docs` (ubuntu-latest)

| # | Шаг | Action / команда | Что делает |
|---|-----|------------------|-----------|
| 1 | Checkout | `actions/checkout@v4` | Клонирует репозиторий |
| 2 | Setup .NET 10 | `actions/setup-dotnet@v4`, `dotnet-version: '10.0.x'` | Устанавливает .NET SDK 10 (нужен для DocValidator) |
| 3 | Install jq | `sudo apt-get update && sudo apt-get install -y jq` | Ставит утилиту `jq` для проверки JSON |
| 4 | Validate JSON files | bash-цикл `jq empty` по `docs/kimi/json/*.json` | Проверяет синтаксическую валидность всех JSON-справочников; первая же ошибка → `exit 1` |
| 5 | Run DocValidator | `dotnet run --project tools/DocValidator --configuration Release -- --project-root .` | Сверяет исходный код с JSON-справочниками (см. §3.2); ошибки → exit code 1 |
| 6 | Check Markdown links | `lycheeverse/lychee-action@v1`, `args: --offline --no-progress docs/kimi/`, `fail: true` | Проверяет, что все относительные ссылки в `docs/kimi/` ведут на существующие файлы (см. §3.3) |

### 3.1. JSON-валидация (jq)

Цикл перебирает `docs/kimi/json/*.json` и выполняет `jq empty "$file"` для каждого файла. Проверяется **только синтаксис** JSON (запятые, скобки, кодировка) — соответствие схеме jq не проверяет. Семантическую сверку с кодом выполняет следующий шаг (DocValidator).

### 3.2. DocValidator

[`DocValidator`](../../tools/DocValidator/README.md) — консольное приложение в `tools/DocValidator`, сверяющее исходный код с JSON-справочниками. Валидаторы:

| Валидатор | Что сверяет |
|-----------|-------------|
| `EnumValidator` | enum из `AutoKassa/Models/Enums/*.cs` vs [`json/enums.json`](json/enums.json) |
| `ViewValidator` | XAML-файлы vs [`json/relationships.json`](json/relationships.json) |
| `DiLifetimeValidator` | DI-регистрации (lifetime) в `App.xaml.cs` vs [`json/services.json`](json/services.json); учитывает неявную Scoped-регистрацию `AppDbContext` через `AddDbContextFactory`/`AddPooledDbContextFactory` |
| `DbContextInjectionValidator` | Запрет прямой инъекции `AppDbContext` в конструкторы (доступ к БД — только через `IDbContextFactory<AppDbContext>`) |
| `ModelValidator` | Сущности vs [`json/models.json`](json/models.json) |
| `ViewModelValidator` | ViewModels vs [`json/viewmodels.json`](json/viewmodels.json) |
| `TrapSyncValidator` | [`TRAPS.md`](TRAPS.md) vs [`json/traps.json`](json/traps.json) |
| `ConstantsSyncValidator` | [`json/constants.json`](json/constants.json) vs исходный код: обязательные поля, существование `sourceFiles`, текстовый поиск имени/значения константы |

Коды выхода ([`Program.cs`](../../tools/DocValidator/Program.cs)):

- `0` — все валидаторы прошли. **Предупреждения (warnings) не роняют job**: они выводятся жёлтым, прогон остаётся зелёным. Так, `ConstantsSyncValidator` выдаёт warning, если имя константы условное (документационное) и ни имя, ни значение не найдены в `sourceFiles` текстово.
- `1` — есть ошибки (`Validation failed with N error(s).`) — job `docs` падает.

Это тот механизм, который принуждает держать `docs/kimi/json/` синхронизированными с кодом: забыл внести новый сервис в `services.json` или новый enum в `enums.json` — упадёт CI.

### 3.3. Проверка Markdown-ссылок (lychee)

`lychee-action@v1` запускается в **offline-режиме** (`--offline`): внешние URL не запрашиваются, проверяется только существование локальных файлов, на которые указывают относительные ссылки внутри `docs/kimi/`. Флаг `fail: true` превращает любую битую ссылку в падение job'а. Поэтому при переименовании или удалении файла документации нужно обновить все ссылки на него — иначе CI упадёт.

---

## 4. Локальное воспроизведение проверок CI

Все команды выполняются из корня репозитория (Git Bash):

```bash
# ── Job build-and-test ──
dotnet restore
dotnet build --no-restore --configuration Release
dotnet test --configuration Release --verbosity normal

# ── Job docs: JSON-валидация ──
for file in docs/kimi/json/*.json; do jq empty "$file" || exit 1; done
# Если jq не установлен (Windows) — эквивалент на Python:
python -c "import json, glob; [json.load(open(f, encoding='utf-8')) for f in glob.glob('docs/kimi/json/*.json')]; print('OK')"

# ── Job docs: DocValidator ──
dotnet run --project tools/DocValidator --configuration Release -- --project-root .
# Подробный вывод (каждый валидатор по отдельности):
dotnet run --project tools/DocValidator -- --project-root . --verbose

# ── Job docs: Markdown-ссылки (если lychee установлен локально) ──
lychee --offline --no-progress docs/kimi/
```

Отличия локального прогона от CI:

- Локально обычно собирают Debug (`dotnet build` без `--configuration`), а CI собирает **Release** — перед push имеет смысл прогнать сборку именно в Release.
- `lychee` локально нужно установить отдельно (например, `cargo install lychee`); без него ссылки проверяются любым скриптом, резолвящим относительные пути Markdown-ссылок.
- Базовые локальные команды (`dotnet build`, `dotnet test`, `dotnet ef migrations add`) также перечислены в корневом [`AGENTS.md`](../../AGENTS.md) §5 и [`00-quickstart.md`](00-quickstart.md); про тесты — [`12-tests.md`](12-tests.md).

---

## 5. Типичные падения CI

| Падающий шаг | Типичная причина | Что делать |
|--------------|------------------|-----------|
| Build | Ошибка компиляции в Release: локально проверяли только Debug | Прогнать `dotnet build --configuration Release` локально, исправить по тексту ошибки. См. [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §3.1 |
| Test | Падают тесты: in-memory SQLite, «старые» данные между тестами, `EnsureCreated` vs миграции | Прогнать `dotnet test` локально; см. [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §10, [`TRAPS.md`](TRAPS.md) §7, [`12-tests.md`](12-tests.md) |
| Validate JSON files | Синтаксическая ошибка в `docs/kimi/json/*.json` (лишняя запятая, незакрытая скобка) | Прогнать jq/python-проверку локально — вывод укажет файл и позицию; исправить JSON |
| Run DocValidator | Код изменён, а JSON-справочники не обновлены: новый enum/View/ViewModel/сервис/константа не внесён в соответствующий JSON, либо `TRAPS.md` рассинхронизирован с `traps.json` | Запустить DocValidator локально, обновить перечисленные в ошибках JSON/MD. Жёлтые warnings job не роняют |
| Check Markdown links | Относительная ссылка в `docs/kimi/` ведёт на переименованный или удалённый файл | Найти ссылку из вывода lychee, исправить путь; локально проверить lychee или скриптом |
| Install jq / Setup .NET | Временный сбой инфраструктуры GitHub Actions (apt-репозитории, загрузка SDK) | Перезапустить прогон (Re-run failed jobs) — код менять не нужно |

Общая диагностика проблем сборки и тестов — в [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §3 и §10.

---

## 6. Релизный процесс

- **CD-пайплайна нет:** единственный workflow не имеет релизных триггеров, не собирает инсталлятор и не публикует артефакты. Релиз — полностью ручной процесс.
- Процесс описан в [`checklists/release-checklist.md`](checklists/release-checklist.md):
  1. **Pre-release:** всё смержено и протестировано, `dotnet build` в Release и `dotnet test` зелёные, `docs/kimi/` синхронизирован с кодом, при необходимости поднята версия в файлах проекта / [`json/metadata.json`](json/metadata.json) / [`kimi.md`](kimi.md).
  2. **Packaging** — portable-сборка:
     ```bash
     dotnet publish AutoKassa -c Release -r win-x64 --self-contained true -o ./publish_portable
     ```
     Затем проверить запуск `publish_portable/AutoKassa.exe`, создание БД и применение миграций на первом запуске; при необходимости собрать `AutoKassa_Portable.zip`. Каталог `publish_portable/` и архив `AutoKassa_Portable.zip` в корне репозитория — артефакты именно этого ручного шага, а не CI.
  3. **Документация:** обновить `CHANGELOG.md`, при изменении пользовательского поведения — `README.md` и скриншоты.
  4. **Финальная проверка:** smoke-тесты на опубликованном exe (добавление операции, отчёт, экспорт PDF/Excel), запуск на чистой Windows-машине, отсутствие секретов в выходных файлах.
- Поскольку CD нет, «зелёный» CI — необходимое, но не достаточное условие релиза: чек-лист выше выполняется вручную целиком.

---

## 7. Связь с другими файлами

- [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml) — единственный workflow, источник истины для этого документа.
- [`tools/DocValidator/README.md`](../../tools/DocValidator/README.md) — запуск DocValidator, аргументы, валидаторы.
- [`checklists/release-checklist.md`](checklists/release-checklist.md) — ручной релизный процесс.
- [`12-tests.md`](12-tests.md) — тестовая инфраструктура, запуск тестов локально.
- [`13-conventions.md`](13-conventions.md) — соглашения по коду, нарушение которых роняет сборку/тесты.
- [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) — диагностика проблем сборки (§3) и тестов (§10).
- [`00-quickstart.md`](00-quickstart.md) — стек, структура решения, базовые команды.
- Корневой [`AGENTS.md`](../../AGENTS.md) — полезные команды (§5) и карта документации (§4).
