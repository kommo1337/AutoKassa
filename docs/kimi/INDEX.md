# INDEX — карта документации AutoKassa для KIMI

Этот файл содержит оглавление всей документации в `docs/kimi/` и карту сценариев «Я хочу сделать X».

---

## Точка входа и правила

| Файл | Назначение |
|------|------------|
| [`kimi.md`](kimi.md) | Главная точка входа. Читать в начале каждой задачи. |
| [`PRINCIPLES.md`](PRINCIPLES.md) | Правильные техники и принципы кода. |
| [`TRAPS.md`](TRAPS.md) | Запреты, ловушки, типичные ошибки. |

---

## JSON-справочники

| Файл | Назначение | Что внутри |
|------|------------|------------|
| [`json/index.json`](json/index.json) | Навигация | Разделы документации и сценарии «я хочу сделать X». |
| [`json/metadata.json`](json/metadata.json) | Метаданные | Версия, стек, ключевые файлы, проекты решения. |
| [`json/models.json`](json/models.json) | Сущности | 9 EF-сущностей: поля, типы, связи, индексы, seed-данные. |
| [`json/services.json`](json/services.json) | Сервисы | 29 сервисов: lifetime, зависимости, методы, потребители. |
| [`json/enums.json`](json/enums.json) | Перечисления | 9 enum со значениями, UI-маппингами и конвертерами. |
| [`json/viewmodels.json`](json/viewmodels.json) | ViewModels | 36 VM: экраны, диалоги, зависимости, команды. |
| [`json/relationships.json`](json/relationships.json) | Граф связей | View → ViewModel → Commands → Services → DbContext. |
| [`json/flows.json`](json/flows.json) | Сценарии | 9 пользовательских потоков с пошаговой трассировкой. |
| [`json/traps.json`](json/traps.json) | Запреты | Структурированные ловушки из [`TRAPS.md`](TRAPS.md). |

---

## Markdown-описания

| Файл | Назначение | Ключевые темы |
|------|------------|---------------|
| [`00-quickstart.md`](00-quickstart.md) | 5-минутный обзор | Стек, структура, как читать документацию, чек-листы добавления функционала. |
| [`01-overview.md`](01-overview.md) | Общее описание | Назначение, функционал, стек, структура решения, сравнение с `AGENTS.md` и `README.md`. |
| [`02-architecture.md`](02-architecture.md) | Архитектура | MVVM, DI, навигация, жизненный цикл VM, Mermaid-графы зависимостей. |
| [`03-database.md`](03-database.md) | База данных | ER-диаграмма, таблицы, связи, индексы, seed-данные, миграции. |
| [`04-models.md`](04-models.md) | Модели | Детальное описание сущностей, DTO, отчётных моделей, правила валидации. |
| [`05-services.md`](05-services.md) | Сервисы | Каталог сервисов по категориям, lifetime, зависимости, ключевые методы. |
| [`06-business-logic.md`](06-business-logic.md) | Бизнес-логика | Soft delete, операции, кредитные карты, долги, категории, контрагенты. |
| [`07-viewmodels.md`](07-viewmodels.md) | ViewModels | Экраны, команды, валидация, async-инициализация, сложные VM. |
| [`08-views.md`](08-views.md) | Views | XAML, стили, конвертеры, кастомные контролы, `MainWindow.xaml`. |
| [`09-flows.md`](09-flows.md) | Сценарии | Запуск, добавление операции, погашение кредита, долги, отчёты, настройки. |
| [`10-reports.md`](10-reports.md) | Отчёты | Виды отчётов, `BaseReportViewModel`, экспорт PDF/Excel. |
| [`11-settings-security.md`](11-settings-security.md) | Настройки и безопасность | `AppSettings`, backup, пароли, автоблокировка, восстановление. |
| [`12-tests.md`](12-tests.md) | Тесты | `TestDatabase`, покрытие, хелперы, запуск тестов. |
| [`13-conventions.md`](13-conventions.md) | Соглашения | Именование, async/await, работа с БД, MVVM, логирование. |

---

## Карта «Я хочу сделать X»

| Я хочу… | Читать сначала | JSON-справочники |
|---------|----------------|------------------|
| Быстро понять проект | [`00-quickstart.md`](00-quickstart.md), [`01-overview.md`](01-overview.md) | [`metadata.json`](json/metadata.json) |
| Добавить новый экран | [`PRINCIPLES.md`](PRINCIPLES.md), [`00-quickstart.md`](00-quickstart.md) | [`relationships.json`](json/relationships.json), [`viewmodels.json`](json/viewmodels.json) |
| Добавить новую сущность | [`PRINCIPLES.md`](PRINCIPLES.md), [`04-models.md`](04-models.md) | [`models.json`](json/models.json), [`03-database.md`](03-database.md) |
| Добавить новый сервис | [`PRINCIPLES.md`](PRINCIPLES.md), [`05-services.md`](05-services.md) | [`services.json`](json/services.json), [`relationships.json`](json/relationships.json) |
| Исправить баг в операциях | [`TRAPS.md`](TRAPS.md), [`06-business-logic.md`](06-business-logic.md) | [`services.json`](json/services.json), [`flows.json`](json/flows.json) |
| Понять зависимости сервиса | [`02-architecture.md`](02-architecture.md) | [`relationships.json`](json/relationships.json), [`services.json`](json/services.json) |
| Разобраться с отчётами | [`10-reports.md`](10-reports.md), [`07-viewmodels.md`](07-viewmodels.md) | [`viewmodels.json`](json/viewmodels.json), [`flows.json`](json/flows.json) |
| Разобраться с кредитками / долгами | [`TRAPS.md`](TRAPS.md), [`06-business-logic.md`](06-business-logic.md) | [`flows.json`](json/flows.json), [`models.json`](json/models.json) |
| Настроить backup / безопасность | [`11-settings-security.md`](11-settings-security.md) | [`services.json`](json/services.json) |
| Писать или исправить тесты | [`12-tests.md`](12-tests.md), [`PRINCIPLES.md`](PRINCIPLES.md) | — |
| Настроить UI / стили / конвертеры | [`08-views.md`](08-views.md), [`13-conventions.md`](13-conventions.md) | [`enums.json`](json/enums.json) |

---

## Правила использования документации

1. Всегда начинай с [`kimi.md`](kimi.md) — там базовые правила и точка входа.
2. Перед изменением кода прочитай [`PRINCIPLES.md`](PRINCIPLES.md) и [`TRAPS.md`](TRAPS.md).
3. Для поиска конкретной сущности, сервиса или экрана используй JSON-справочники.
4. Для понимания зависимостей смотри [`json/relationships.json`](json/relationships.json) и [`02-architecture.md`](02-architecture.md).
5. Для бизнес-логики смотри [`06-business-logic.md`](06-business-logic.md) и [`json/flows.json`](json/flows.json).
6. После любого изменения обновляй соответствующие JSON и Markdown файлы.

---

> Последнее обновление: подэтапы 3.3–3.5 — Markdown-описания business-logic, viewmodels, views, flows, reports, settings-security, tests, conventions.
