# AutoKassa — точка входа для KIMI CODE

**Версия:** 1.2.0  
**Репозиторий:** `c:\Users\Лиза Блинова\source\repos\kommo1337\AutoKassa`  
**Назначение:** десктопное WPF-приложение для учёта финансовых операций автосервиса (доходы/расходы). Работает локально, хранит данные в SQLite.

---

## Технологический стек

| Компонент | Технология | Версия |
|-----------|-----------|--------|
| Платформа | .NET | 10.0-windows |
| UI-фреймворк | WPF | — |
| ORM | Entity Framework Core | 10.0.8 |
| База данных | SQLite | — |
| DI-контейнер | Microsoft.Extensions.DependencyInjection | 10.0.8 |
| Графики | OxyPlot.Wpf | 2.2.0 |
| PDF-экспорт | QuestPDF | 2026.5.0 |
| Excel-экспорт | ClosedXML | 0.105.0 |
| Хеширование паролей | BCrypt.Net-Next | 4.2.0 |
| Логирование | Serilog + File sink | 4.3.1 / 7.0.0 |
| Тестирование | xUnit + FluentAssertions | 2.9.3 / 8.10.0 |

---

## Базовые правила (обязательные к исполнению)

1. Перед любой задачей прочитать [`PRINCIPLES.md`](PRINCIPLES.md).
2. Перед любой задачей прочитать [`TRAPS.md`](TRAPS.md).
3. Любое изменение сущности/сервиса/VM/View требует обновления соответствующих JSON и Markdown файлов в `docs/kimi/`.
4. Перед изменением сервиса проверить [`json/relationships.json`](json/relationships.json), чтобы понять, кто от него зависит.
5. После изменений обязательно запускать `dotnet build` и `dotnet test`.
6. Не добавлять бизнес-логику в code-behind View.
7. Не использовать статический Service Locator (`App.GetService<T>()` только в исключительных случаях).
8. Не делать синхронные запросы к БД.

---

## Карта «Я хочу…»

| Я хочу… | Читать |
|---------|--------|
| Быстро понять проект | [`00-quickstart.md`](00-quickstart.md), [`01-overview.md`](01-overview.md) |
| Добавить новый экран | [`PRINCIPLES.md`](PRINCIPLES.md), [`00-quickstart.md`](00-quickstart.md), [`json/relationships.json`](json/relationships.json) |
| Добавить новую сущность | [`PRINCIPLES.md`](PRINCIPLES.md), [`04-models.md`](04-models.md), [`json/models.json`](json/models.json), [`03-database.md`](03-database.md) |
| Добавить новый сервис | [`PRINCIPLES.md`](PRINCIPLES.md), [`05-services.md`](05-services.md), [`json/services.json`](json/services.json), [`json/relationships.json`](json/relationships.json) |
| Исправить баг в операциях | [`TRAPS.md`](TRAPS.md), [`06-business-logic.md`](06-business-logic.md), [`json/services.json`](json/services.json) |
| Понять зависимости сервиса | [`json/relationships.json`](json/relationships.json), [`05-services.md`](05-services.md) |
| Разобраться с отчётами | [`10-reports.md`](10-reports.md), [`json/viewmodels.json`](json/viewmodels.json) |
| Разобраться с кредитками / долгами | [`TRAPS.md`](TRAPS.md), [`06-business-logic.md`](06-business-logic.md), [`json/flows.json`](json/flows.json) |
| Настроить backup / безопасность | [`11-settings-security.md`](11-settings-security.md) |
| Писать или исправить тесты | [`12-tests.md`](12-tests.md), [`PRINCIPLES.md`](PRINCIPLES.md) |
| Настроить UI / стили / конвертеры | [`08-views.md`](08-views.md), [`13-conventions.md`](13-conventions.md) |

---

## Ключевые файлы документации

- [`PRINCIPLES.md`](PRINCIPLES.md) — как правильно писать код в проекте.
- [`TRAPS.md`](TRAPS.md) — запреты, ловушки, типичные ошибки.
- [`INDEX.md`](INDEX.md) — полная карта документации.
- [`json/index.json`](json/index.json) — машиночитаемая навигация.
- [`json/metadata.json`](json/metadata.json) — метаданные проекта.
- [`json/models.json`](json/models.json) — сущности.
- [`json/services.json`](json/services.json) — сервисы.
- [`json/enums.json`](json/enums.json) — перечисления.
- [`json/viewmodels.json`](json/viewmodels.json) — ViewModels.
- [`json/relationships.json`](json/relationships.json) — граф связей.
- [`json/flows.json`](json/flows.json) — пользовательские сценарии.
- [`json/traps.json`](json/traps.json) — запреты в машиночитаемом виде.

---

> Последнее обновление: Этап 4 — дополнение TRAPS.md, синхронизация json/traps.json, исправление ссылок, обновление AGENTS.md.
