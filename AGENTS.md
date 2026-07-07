# AutoKassa — шлюзовое руководство для AI-агентов

> ⚠️ **Обязательный порядок чтения:** перед любой задачей прочитай [`docs/kimi/kimi.md`](docs/kimi/kimi.md), [`docs/kimi/PRINCIPLES.md`](docs/kimi/PRINCIPLES.md) и [`docs/kimi/TRAPS.md`](docs/kimi/TRAPS.md). Полная карта документации — в [`docs/kimi/INDEX.md`](docs/kimi/INDEX.md).

---

## 1. Обзор проекта

**AutoKassa** — десктопное WPF-приложение для учёта финансовых операций автосервиса (доходы/расходы). Работает локально на Windows, хранит данные в SQLite, использует .NET 10 и EF Core. Язык интерфейса и документации — русский.

Основной функционал: операции с категориями, дашборд, отчёты с экспортом PDF/Excel, управление категориями, контрагентами, кредитными картами и долгами, настройки, backup, автоблокировка и toast-уведомления.

Подробнее: [`01-overview.md`](docs/kimi/01-overview.md), [`02-architecture.md`](docs/kimi/02-architecture.md).

---

## 2. Технологический стек

| Компонент | Технология | Версия |
|-----------|-----------|--------|
| Платформа | .NET | 10.0-windows |
| UI | WPF | — |
| ORM | EF Core | 10.0.8 |
| База данных | SQLite | — |
| DI | Microsoft.Extensions.DependencyInjection | 10.0.8 |
| Графики | OxyPlot.Wpf | 2.2.0 |
| PDF / Excel | QuestPDF / ClosedXML | 2026.5.0 / 0.105.0 |
| Логирование | Serilog + File sink | 4.3.1 / 7.0.0 |
| Тесты | xUnit + FluentAssertions | 2.9.3 / 8.10.0 |

Полный стек и зависимости: [`kimi.md`](docs/kimi/kimi.md), [`metadata.json`](docs/kimi/json/metadata.json).

---

## 3. Критические правила (шпаргалка)

- **MVVM:** бизнес-логика только в сервисах, ViewModel не знает о View, code-behind содержит только минимальную UI-логику. Подробнее: [`PRINCIPLES.md`](docs/kimi/PRINCIPLES.md), [`02-architecture.md`](docs/kimi/02-architecture.md).
- **DI:** все зависимости через конструктор; сервисы получают [`IDbContextFactory<AppDbContext>`](AutoKassa/Services/AppDbContext.cs), а не сам `AppDbContext`. Lifetime: большинство сервисов — `Transient`, Singleton — `NavigationService`, `PasswordService`, `SettingsService`, `DialogService`, `ToastNotificationService`, `LockService`, `ExportService`, `DataChangeService`, `SettingsMapper`, `SettingsValidator`, `AppInfoService`, `BackupCoordinator`, фабрики VM. Подробнее: [`02-architecture.md`](docs/kimi/02-architecture.md), [`services.json`](docs/kimi/json/services.json).
- **База данных:** SQLite, файл `AutoKassa.db` в папке приложения; soft delete у `Transaction` (`IsDeleted = true`); миграции применяются автоматически. Подробнее: [`03-database.md`](docs/kimi/03-database.md).
- **Async/await:** все операции с БД — строго асинхронные, публичные методы именуются с суффиксом `Async`, в сервисах используй `ConfigureAwait(false)`. Подробнее: [`13-conventions.md`](docs/kimi/13-conventions.md).
- **Софт-делит:** удаление операций только через `IsDeleted = true`; физическое удаление запрещено. Подробнее: [`06-business-logic.md`](docs/kimi/06-business-logic.md).
- **Service Locator:** не используй `App.GetService<T>()`, кроме исключительных случаев. Подробнее: [`TRAPS.md`](docs/kimi/TRAPS.md).
- **Изменения данных:** после мутаций вызывай [`IDataChangeService.NotifyDataChanged()`](AutoKassa/Services/IDataChangeService.cs). Подробнее: [`PRINCIPLES.md`](docs/kimi/PRINCIPLES.md).

---

## 4. Карта документации

| Тема | Markdown | JSON |
|------|----------|------|
| Точка входа и правила | [`kimi.md`](docs/kimi/kimi.md) | [`index.json`](docs/kimi/json/index.json) |
| Принципы кода | [`PRINCIPLES.md`](docs/kimi/PRINCIPLES.md) | — |
| Ловушки и запреты | [`TRAPS.md`](docs/kimi/TRAPS.md) | [`traps.json`](docs/kimi/json/traps.json) |
| Архитектура / DI / MVVM | [`02-architecture.md`](docs/kimi/02-architecture.md) | [`relationships.json`](docs/kimi/json/relationships.json) |
| База данных | [`03-database.md`](docs/kimi/03-database.md) | [`models.json`](docs/kimi/json/models.json) |
| Сущности и DTO | [`04-models.md`](docs/kimi/04-models.md) | [`models.json`](docs/kimi/json/models.json) |
| Сервисы | [`05-services.md`](docs/kimi/05-services.md) | [`services.json`](docs/kimi/json/services.json), [`relationships.json`](docs/kimi/json/relationships.json) |
| Бизнес-логика | [`06-business-logic.md`](docs/kimi/06-business-logic.md) | [`flows.json`](docs/kimi/json/flows.json) |
| ViewModels | [`07-viewmodels.md`](docs/kimi/07-viewmodels.md) | [`viewmodels.json`](docs/kimi/json/viewmodels.json) |
| Views / UI | [`08-views.md`](docs/kimi/08-views.md) | [`relationships.json`](docs/kimi/json/relationships.json) |
| Пользовательские сценарии | [`09-flows.md`](docs/kimi/09-flows.md) | [`flows.json`](docs/kimi/json/flows.json) |
| Отчёты | [`10-reports.md`](docs/kimi/10-reports.md) | [`viewmodels.json`](docs/kimi/json/viewmodels.json) |
| Настройки и безопасность | [`11-settings-security.md`](docs/kimi/11-settings-security.md) | [`services.json`](docs/kimi/json/services.json) |
| Тесты | [`12-tests.md`](docs/kimi/12-tests.md) | — |
| Соглашения по коду | [`13-conventions.md`](docs/kimi/13-conventions.md) | — |

---

## 5. Полезные команды

```bash
# Сборка всего решения
dotnet build

# Запуск тестов
dotnet test

# Запуск приложения
dotnet run --project AutoKassa

# Создание миграции (из папки AutoKassa/)
cd AutoKassa && dotnet ef migrations add <ИмяМиграции>

# Проверка структуры БД (если установлен sqlite3)
sqlite3 AutoKassa/bin/Debug/net10.0-windows/AutoKassa.db ".schema"
```

---

## 6. Известные особенности

- Windows-only (`<UseWPF>true</UseWPF>`, `<OutputType>WinExe</OutputType>`).
- При первом запуске требуется установка пароля (`InitialSetupView`).
- `AppSettings` — single-row таблица, всегда `Id = 1`.
- Поиск по описанию выполняется в памяти, потому что SQLite `LOWER()` не поддерживает кириллицу.
- Подробный каталог особенностей и ловушек: [`TRAPS.md`](docs/kimi/TRAPS.md), [`13-conventions.md`](docs/kimi/13-conventions.md).
