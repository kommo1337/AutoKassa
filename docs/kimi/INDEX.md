# INDEX — карта документации AutoKassa для KIMI

Этот файл содержит оглавление всей документации в `docs/kimi/`, карту сценариев «Я хочу сделать X», таблицы быстрого поиска и алфавитный указатель терминов.

---

## Точка входа и правила

| Файл | Назначение |
|------|------------|
| [`kimi.md`](kimi.md) | Главная точка входа. Читать в начале каждой задачи. |
| [`PRINCIPLES.md`](PRINCIPLES.md) | Правильные техники и принципы кода. |
| [`TRAPS.md`](TRAPS.md) | Запреты, ловушки, типичные ошибки. |
| [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) | Симптом → причина → решение. Первый файл при любой проблеме. |

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
| [`json/constants.json`](json/constants.json) | Константы | Лимиты, дефолты, seed-ID и магические строки из [`CONSTANTS.md`](CONSTANTS.md). |

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
| [`14-startup.md`](14-startup.md) | Запуск приложения | Пошаговая инициализация, DI-контейнер, выбор стартового окна, sequence-диаграмма. |
| [`15-converters.md`](15-converters.md) | XAML-конвертеры | Каталог всех конвертеров, регистрация, правила добавления, типичные ошибки. |
| [`16-cicd.md`](16-cicd.md) | CI/CD | Workflow `.NET CI`: сборка и тесты, валидация JSON/DocValidator/ссылок, локальное воспроизведение, релизный процесс. |
| [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) | Устранение неполадок | Как читать логи; проблемы сборки, EF Core, UI, операций, кредиток, отчётов, настроек, тестов. |
| [`CONSTANTS.md`](CONSTANTS.md) | Константы и лимиты | Дефолты `AppSettings`, лимиты валидации, пагинация, seed-ID, UI-константы, логирование. |
| [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md) | Шаблоны задач | 8 пошаговых шаблонов: экран, сущность, сервис, баг, рефакторинг, тесты, документация, отчёт. |
| [`checklists/`](checklists/) | Чек-листы | Контрольные списки для типовых задач (багфикс, новый экран/сущность/сервис, тесты, релиз). |

---

## Карта «Я хочу сделать X»

| Я хочу… | Читать сначала | JSON-справочники |
|---------|----------------|------------------|
| Быстро понять проект | [`00-quickstart.md`](00-quickstart.md), [`01-overview.md`](01-overview.md) | [`metadata.json`](json/metadata.json) |
| Добавить новый экран | [`PRINCIPLES.md`](PRINCIPLES.md), [`00-quickstart.md`](00-quickstart.md) | [`relationships.json`](json/relationships.json), [`viewmodels.json`](json/viewmodels.json) |
| Добавить новую сущность | [`PRINCIPLES.md`](PRINCIPLES.md), [`04-models.md`](04-models.md) | [`models.json`](json/models.json), [`03-database.md`](03-database.md) |
| Добавить новый сервис | [`PRINCIPLES.md`](PRINCIPLES.md), [`05-services.md`](05-services.md) | [`services.json`](json/services.json), [`relationships.json`](json/relationships.json) |
| Исправить баг в операциях | [`TRAPS.md`](TRAPS.md), [`06-business-logic.md`](06-business-logic.md) | [`services.json`](json/services.json), [`flows.json`](json/flows.json) |
| Диагностировать любую проблему | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md), [`TRAPS.md`](TRAPS.md) | [`traps.json`](json/traps.json) |
| Найти константу, лимит или дефолт | [`CONSTANTS.md`](CONSTANTS.md) | [`constants.json`](json/constants.json) |
| Понять запуск приложения и DI | [`14-startup.md`](14-startup.md), [`02-architecture.md`](02-architecture.md) | [`services.json`](json/services.json) |
| Разобраться с XAML-конвертером | [`15-converters.md`](15-converters.md), [`08-views.md`](08-views.md) | [`enums.json`](json/enums.json) |
| Понять зависимости сервиса | [`02-architecture.md`](02-architecture.md) | [`relationships.json`](json/relationships.json), [`services.json`](json/services.json) |
| Разобраться с отчётами | [`10-reports.md`](10-reports.md), [`07-viewmodels.md`](07-viewmodels.md) | [`viewmodels.json`](json/viewmodels.json), [`flows.json`](json/flows.json) |
| Разобраться с кредитками / долгами | [`TRAPS.md`](TRAPS.md), [`06-business-logic.md`](06-business-logic.md) | [`flows.json`](json/flows.json), [`models.json`](json/models.json) |
| Настроить backup / безопасность | [`11-settings-security.md`](11-settings-security.md) | [`services.json`](json/services.json) |
| Писать или исправить тесты | [`12-tests.md`](12-tests.md), [`PRINCIPLES.md`](PRINCIPLES.md) | — |
| Настроить UI / стили / конвертеры | [`08-views.md`](08-views.md), [`15-converters.md`](15-converters.md), [`13-conventions.md`](13-conventions.md) | [`enums.json`](json/enums.json) |
| Понять CI / починить упавший пайплайн | [`16-cicd.md`](16-cicd.md), [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) | — |

---

## Если сломалось X → смотри Y

| Симптом | Где решение |
|---------|-------------|
| Проект не собирается | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §3.1 |
| Приложение падает или закрывается на старте | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §3.2–3.3, [`14-startup.md`](14-startup.md) §7 |
| UI зависает на старте или при работе | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §3.4, [`TRAPS.md`](TRAPS.md) §1.3, §3.12 |
| Ошибка применения миграций | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §4.1, [`03-database.md`](03-database.md) §7 |
| `SQLite Error 5: database is locked` | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §4.2 |
| Данные «пропали» после удаления | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §4.3, [`TRAPS.md`](TRAPS.md) §2.1 |
| Поиск по описанию не находит операции | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §4.4, [`TRAPS.md`](TRAPS.md) §3.1, §3.4 |
| Экран не отображается (пустая область контента) | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §5.1, [`TRAPS.md`](TRAPS.md) §6.3 |
| Навигация падает / параметр не доходит до VM | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §5.2–5.3, [`TRAPS.md`](TRAPS.md) §1.6 |
| Диалог не открывается | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §5.4, [`TRAPS.md`](TRAPS.md) §1.7 |
| Кнопка не срабатывает или неактивна | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §5.5, [`TRAPS.md`](TRAPS.md) §4.2 |
| Баланс не сходится | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §6.1, [`06-business-logic.md`](06-business-logic.md) §7.2 |
| Операцию-погашение нельзя редактировать | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §6.3, [`TRAPS.md`](TRAPS.md) §2.6 |
| Погашение не распределяется по покупкам (FIFO) | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §7.1, [`06-business-logic.md`](06-business-logic.md) §3.6 |
| Пустой отчёт / неверные итоги | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §8.1–8.2, [`TRAPS.md`](TRAPS.md) §3.10 |
| Не работает экспорт PDF / Excel | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §8.3–8.4, [`10-reports.md`](10-reports.md) §4 |
| Пароль не принимается / автоблокировка не работает | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §9.1–9.2, [`11-settings-security.md`](11-settings-security.md) §5 |
| Бэкап / восстановление не работает | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §9.3–9.4, [`11-settings-security.md`](11-settings-security.md) §4 |
| Падают тесты (SQLite in-memory, «старые» данные) | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §10, [`TRAPS.md`](TRAPS.md) §7, [`12-tests.md`](12-tests.md) |
| Подвисания / высокое потребление RAM | [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §11, [`CONSTANTS.md`](CONSTANTS.md) §3 |

---

## Если нужно сделать X → смотри Y

| Задача | Шаблон / чек-лист / skill |
|--------|---------------------------|
| Добавить новый экран | [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md) §1, [`new-screen-checklist.md`](checklists/new-screen-checklist.md), [`.kimi/skills/autokassa-new-screen/SKILL.md`](../../.kimi/skills/autokassa-new-screen/SKILL.md) |
| Добавить новую сущность + миграцию | [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md) §2, [`new-entity-checklist.md`](checklists/new-entity-checklist.md), [`.kimi/skills/autokassa-new-entity/SKILL.md`](../../.kimi/skills/autokassa-new-entity/SKILL.md) |
| Добавить новый сервис | [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md) §3, [`new-service-checklist.md`](checklists/new-service-checklist.md), [`.kimi/skills/autokassa-new-service/SKILL.md`](../../.kimi/skills/autokassa-new-service/SKILL.md) |
| Исправить баг | [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md) §4, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md), [`bugfix-checklist.md`](checklists/bugfix-checklist.md), [`.kimi/skills/autokassa-bugfix/SKILL.md`](../../.kimi/skills/autokassa-bugfix/SKILL.md) |
| Отрефакторить сервис / ViewModel | [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md) §5, [`refactor-checklist.md`](checklists/refactor-checklist.md), [`.kimi/skills/autokassa-refactor/SKILL.md`](../../.kimi/skills/autokassa-refactor/SKILL.md) |
| Добавить или исправить тесты | [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md) §6, [`test-checklist.md`](checklists/test-checklist.md), [`.kimi/skills/autokassa-test/SKILL.md`](../../.kimi/skills/autokassa-test/SKILL.md) |
| Обновить документацию / JSON-справочники | [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md) §7, [`docs-update-checklist.md`](checklists/docs-update-checklist.md), [`.kimi/skills/autokassa-docs/SKILL.md`](../../.kimi/skills/autokassa-docs/SKILL.md) |
| Добавить новый отчёт | [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md) §8, [`10-reports.md`](10-reports.md) |
| Добавить XAML-конвертер | [`15-converters.md`](15-converters.md) §3, [`08-views.md`](08-views.md) §4 |
| Найти константу, лимит или дефолт | [`CONSTANTS.md`](CONSTANTS.md), [`json/constants.json`](json/constants.json) |
| Понять порядок запуска и регистрации DI | [`14-startup.md`](14-startup.md), [`02-architecture.md`](02-architecture.md) §3 |
| Провести code review | [`.kimi/skills/autokassa-review/SKILL.md`](../../.kimi/skills/autokassa-review/SKILL.md), [`bugfix-checklist.md`](checklists/bugfix-checklist.md) + [`refactor-checklist.md`](checklists/refactor-checklist.md) |
| Подготовить релиз / публикацию | [`release-checklist.md`](checklists/release-checklist.md), [`.kimi/skills/autokassa/SKILL.md`](../../.kimi/skills/autokassa/SKILL.md) |
| Понять CI / починить упавший пайплайн | [`16-cicd.md`](16-cicd.md), [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §3, §10 |

---

## Алфавитный указатель

| Термин | Где описано |
|--------|-------------|
| `AppDbContext` | [`03-database.md`](03-database.md), [`14-startup.md`](14-startup.md) §3 |
| `AppSettings` | [`04-models.md`](04-models.md) §1.8, [`11-settings-security.md`](11-settings-security.md) §1, [`CONSTANTS.md`](CONSTANTS.md) §1, [`TRAPS.md`](TRAPS.md) §3.3 |
| `App.xaml` | [`08-views.md`](08-views.md) §2, [`15-converters.md`](15-converters.md) §1 |
| `AsyncRelayCommand` | [`07-viewmodels.md`](07-viewmodels.md) §3.2, [`PRINCIPLES.md`](PRINCIPLES.md) §10, [`TRAPS.md`](TRAPS.md) §4.1 |
| `BackupCoordinator` | [`05-services.md`](05-services.md) §4.6, [`11-settings-security.md`](11-settings-security.md) §4.1 |
| `BaseReportViewModel` | [`07-viewmodels.md`](07-viewmodels.md) §9.1, [`10-reports.md`](10-reports.md) §3.1 |
| `BoolToVisibilityConverter` | [`15-converters.md`](15-converters.md) §2.2, [`TRAPS.md`](TRAPS.md) §5.1 |
| CI/CD (GitHub Actions) | [`16-cicd.md`](16-cicd.md) |
| `CancellationTokenSource` | [`PRINCIPLES.md`](PRINCIPLES.md) §11, [`13-conventions.md`](13-conventions.md) §3 |
| `Category` | [`04-models.md`](04-models.md) §1.2, [`03-database.md`](03-database.md) §3.1 |
| `CategoryColorConverter` | [`15-converters.md`](15-converters.md) §2.13, [`TRAPS.md`](TRAPS.md) §5.4 |
| `CategoryService` | [`05-services.md`](05-services.md) §2.6, [`06-business-logic.md`](06-business-logic.md) §5 |
| `ConfigureAwait` | [`13-conventions.md`](13-conventions.md) §3, [`PRINCIPLES.md`](PRINCIPLES.md) §4 |
| `Counterparty` | [`04-models.md`](04-models.md) §1.3, [`06-business-logic.md`](06-business-logic.md) §6 |
| `CounterpartyService` | [`05-services.md`](05-services.md) §2.7 |
| `CreditCard` | [`04-models.md`](04-models.md) §1.4, [`06-business-logic.md`](06-business-logic.md) §3 |
| `CreditCardPayment` | [`04-models.md`](04-models.md) §1.6, [`TRAPS.md`](TRAPS.md) §3.6 |
| `CreditCardPurchase` | [`04-models.md`](04-models.md) §1.5, [`TRAPS.md`](TRAPS.md) §3.5 |
| `CreditCardService` | [`05-services.md`](05-services.md) §2.9, [`06-business-logic.md`](06-business-logic.md) §3 |
| `CreditCardSettingsService` | [`05-services.md`](05-services.md) §4.7 |
| `DashboardPeriodCoordinator` | [`05-services.md`](05-services.md) §2.12 |
| `DashboardService` | [`05-services.md`](05-services.md) §2.11 |
| `DashboardTransactionCoordinator` | [`05-services.md`](05-services.md) §2.13 |
| `DataChangeService` (`IDataChangeService`) | [`05-services.md`](05-services.md) §3.6, [`02-architecture.md`](02-architecture.md) §6, [`TRAPS.md`](TRAPS.md) §6.1 |
| `DebtPayment` | [`04-models.md`](04-models.md) §1.7, [`06-business-logic.md`](06-business-logic.md) §4 |
| `DebtReport` | [`04-models.md`](04-models.md) §3.5 |
| `DebtService` | [`05-services.md`](05-services.md) §2.8, [`06-business-logic.md`](06-business-logic.md) §4 |
| `DecimalToStringConverter` | [`15-converters.md`](15-converters.md) §2.4, [`TRAPS.md`](TRAPS.md) §5.2 |
| `DialogService` | [`05-services.md`](05-services.md) §3.2, [`TRAPS.md`](TRAPS.md) §1.7 |
| `DocValidator` | [`16-cicd.md`](16-cicd.md) §3.2 |
| EF Core | [`03-database.md`](03-database.md), [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §4 |
| `EnsureCreated` | [`TRAPS.md`](TRAPS.md) §7.2, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §10.2 |
| `EqualityConverter` | [`15-converters.md`](15-converters.md) §2.7 |
| `ExportService` | [`05-services.md`](05-services.md) §3.5, [`10-reports.md`](10-reports.md) §4 |
| `FavoriteReport` | [`04-models.md`](04-models.md) §1.9 |
| FIFO-погашение | [`06-business-logic.md`](06-business-logic.md) §3.6, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §7.1 |
| `IAsyncInitializable` | [`02-architecture.md`](02-architecture.md) §5, [`07-viewmodels.md`](07-viewmodels.md) §4.1 |
| `IDbContextFactory` | [`02-architecture.md`](02-architecture.md) §3, [`14-startup.md`](14-startup.md) §3, [`PRINCIPLES.md`](PRINCIPLES.md) §3 |
| `INavigationAware` | [`02-architecture.md`](02-architecture.md) §5, [`07-viewmodels.md`](07-viewmodels.md) §4.1 |
| `InitialBalance` | [`TRAPS.md`](TRAPS.md) §2.20, [`06-business-logic.md`](06-business-logic.md) §7.3 |
| `InitialSetupView` | [`08-views.md`](08-views.md) §8.8, [`14-startup.md`](14-startup.md) §4 |
| `InlineTransactionService` | [`05-services.md`](05-services.md) §2.4, [`06-business-logic.md`](06-business-logic.md) §8.3 |
| `IntEqualityConverter` | [`15-converters.md`](15-converters.md) §2.6 |
| `IntToStringConverter` | [`15-converters.md`](15-converters.md) §2.5 |
| `InverseBoolConverter` | [`15-converters.md`](15-converters.md) §2.3 |
| `IsNegativeConverter` | [`15-converters.md`](15-converters.md) §2.9 |
| `LockScreenView` | [`08-views.md`](08-views.md) §8.8, [`14-startup.md`](14-startup.md) §4 |
| `LockService` | [`05-services.md`](05-services.md) §3.4, [`11-settings-security.md`](11-settings-security.md) §5.3 |
| `MainWindow.xaml` | [`08-views.md`](08-views.md) §3, [`TRAPS.md`](TRAPS.md) §6.3 |
| `MaxDetailTransactions` / `MaxCategoryTransactions` | [`CONSTANTS.md`](CONSTANTS.md) §3, [`TRAPS.md`](TRAPS.md) §3.11, [`10-reports.md`](10-reports.md) §2.1 |
| `ModalViewModel` | [`07-viewmodels.md`](07-viewmodels.md) §7.1, [`10-reports.md`](10-reports.md) §3.2 |
| MVVM | [`02-architecture.md`](02-architecture.md) §2, [`PRINCIPLES.md`](PRINCIPLES.md) §1 |
| `NavigationService` | [`05-services.md`](05-services.md) §3.1, [`02-architecture.md`](02-architecture.md) §4, [`TRAPS.md`](TRAPS.md) §1.6 |
| `NotifyDataChanged` | [`02-architecture.md`](02-architecture.md) §6, [`TRAPS.md`](TRAPS.md) §6.1 |
| `OperationTypeToColorConverter` | [`15-converters.md`](15-converters.md) §2.10 |
| `PasswordService` | [`05-services.md`](05-services.md) §4.2, [`11-settings-security.md`](11-settings-security.md) §5.1 |
| `PaymentType.Debt` | [`TRAPS.md`](TRAPS.md) §2.2, [`06-business-logic.md`](06-business-logic.md) §7.2 |
| `RelayCommand` | [`07-viewmodels.md`](07-viewmodels.md) §3.1, [`TRAPS.md`](TRAPS.md) §4.2 |
| `ReconciliationData` | [`04-models.md`](04-models.md) §3.6, [`06-business-logic.md`](06-business-logic.md) §7.7 |
| `ReportService` | [`05-services.md`](05-services.md) §2.10, [`10-reports.md`](10-reports.md) §2 |
| `ReportViewModelFactory` | [`05-services.md`](05-services.md) §5.1, [`10-reports.md`](10-reports.md) §3.3 |
| Seed-данные | [`03-database.md`](03-database.md) §6, [`CONSTANTS.md`](CONSTANTS.md) §4, [`TRAPS.md`](TRAPS.md) §2.21 |
| Serilog | [`14-startup.md`](14-startup.md) §2, [`CONSTANTS.md`](CONSTANTS.md) §8, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §2 |
| Service Locator | [`TRAPS.md`](TRAPS.md) §1.2, [`PRINCIPLES.md`](PRINCIPLES.md) §2 |
| `SettingsMapper` | [`05-services.md`](05-services.md) §4.3 |
| `SettingsService` | [`05-services.md`](05-services.md) §4.1, [`11-settings-security.md`](11-settings-security.md) §2, [`PRINCIPLES.md`](PRINCIPLES.md) §13 |
| `SettingsValidator` | [`05-services.md`](05-services.md) §4.4, [`CONSTANTS.md`](CONSTANTS.md) §2 |
| Soft delete | [`06-business-logic.md`](06-business-logic.md) §1.2, [`TRAPS.md`](TRAPS.md) §2.1, [`03-database.md`](03-database.md) |
| SQLite | [`03-database.md`](03-database.md), [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §4, [`TRAPS.md`](TRAPS.md) §3.1 |
| `StringToVisibilityConverter` | [`15-converters.md`](15-converters.md) §2.8 |
| `TASK_TEMPLATES` | [`TASK_TEMPLATES.md`](TASK_TEMPLATES.md), [`checklists/`](checklists/) |
| `TestDatabase` | [`12-tests.md`](12-tests.md) §1.1, [`TRAPS.md`](TRAPS.md) §7.1 |
| `TestDbContextFactory` | [`12-tests.md`](12-tests.md) §1.2 |
| `ToastNotificationService` | [`05-services.md`](05-services.md) §3.3, [`08-views.md`](08-views.md) §8.7 |
| `Transaction` | [`04-models.md`](04-models.md) §1.1, [`06-business-logic.md`](06-business-logic.md) §2 |
| `TransactionAmountConverter` | [`15-converters.md`](15-converters.md) §2.14, [`TRAPS.md`](TRAPS.md) §5.3 |
| `TransactionDotColorConverter` | [`15-converters.md`](15-converters.md) §2.15 |
| `TransactionEditViewModelFactory` | [`05-services.md`](05-services.md) §5.2 |
| `TransactionFilterParameters` | [`04-models.md`](04-models.md) §2.1 |
| `TransactionGroupingService` | [`05-services.md`](05-services.md) §2.3, [`06-business-logic.md`](06-business-logic.md) §8.2 |
| `TransactionListService` | [`05-services.md`](05-services.md) §2.2, [`06-business-logic.md`](06-business-logic.md) §8.1 |
| `TransactionSelectionService` | [`05-services.md`](05-services.md) §2.5, [`06-business-logic.md`](06-business-logic.md) §8.4 |
| `TransactionService` | [`05-services.md`](05-services.md) §2.1, [`06-business-logic.md`](06-business-logic.md) §2 |
| `ViewModelBase` | [`07-viewmodels.md`](07-viewmodels.md) §2, [`02-architecture.md`](02-architecture.md) §2 |
| WAL (`journal_mode`) | [`14-startup.md`](14-startup.md) §2, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §4.2 |
| Автоблокировка | [`11-settings-security.md`](11-settings-security.md) §5.3, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §9.2, [`CONSTANTS.md`](CONSTANTS.md) §7 |
| Баланс | [`06-business-logic.md`](06-business-logic.md) §7.2, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §6.1 |
| Бэкап (резервное копирование) | [`11-settings-security.md`](11-settings-security.md) §4, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §9.3–9.4 |
| Валидация | [`PRINCIPLES.md`](PRINCIPLES.md) §5, [`07-viewmodels.md`](07-viewmodels.md) §5, [`04-models.md`](04-models.md) §4 |
| Горячие клавиши | [`02-architecture.md`](02-architecture.md) §10 |
| Дашборд | [`02-architecture.md`](02-architecture.md) §9, [`CONSTANTS.md`](CONSTANTS.md) §6 |
| Долги | [`06-business-logic.md`](06-business-logic.md) §4, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §7.4 |
| Жизненный цикл ViewModel | [`02-architecture.md`](02-architecture.md) §5, [`07-viewmodels.md`](07-viewmodels.md) §4 |
| Категории | [`06-business-logic.md`](06-business-logic.md) §5, [`03-database.md`](03-database.md) §3.1 |
| Code-behind | [`PRINCIPLES.md`](PRINCIPLES.md) §14, [`08-views.md`](08-views.md) §9, [`TRAPS.md`](TRAPS.md) §1.1 |
| Конвертеры | [`15-converters.md`](15-converters.md), [`08-views.md`](08-views.md) §4 |
| Контрагенты | [`06-business-logic.md`](06-business-logic.md) §6 |
| Кредитные карты | [`06-business-logic.md`](06-business-logic.md) §3, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §7 |
| Логирование | [`13-conventions.md`](13-conventions.md) §6, [`CONSTANTS.md`](CONSTANTS.md) §8, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §2 |
| Миграции | [`03-database.md`](03-database.md) §7, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §4.1 |
| Навигация | [`02-architecture.md`](02-architecture.md) §4, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §5 |
| Отчёты | [`10-reports.md`](10-reports.md), [`06-business-logic.md`](06-business-logic.md) §7 |
| Пагинация | [`CONSTANTS.md`](CONSTANTS.md) §3 |
| Пароль | [`11-settings-security.md`](11-settings-security.md) §5, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §9.1 |
| Период-пикер | [`08-views.md`](08-views.md) §5.2 |
| Погашение кредита (системная категория) | [`06-business-logic.md`](06-business-logic.md) §3.2, [`TRAPS.md`](TRAPS.md) §2.22, [`CONSTANTS.md`](CONSTANTS.md) §4 |
| Сверка кассы | [`06-business-logic.md`](06-business-logic.md) §7.7 |
| Системные категории | [`TRAPS.md`](TRAPS.md) §2.3, [`CONSTANTS.md`](CONSTANTS.md) §4 |
| Стартовое окно | [`14-startup.md`](14-startup.md) §4 |
| Тесты | [`12-tests.md`](12-tests.md), [`TRAPS.md`](TRAPS.md) §7, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §10 |
| Фильтрация операций | [`06-business-logic.md`](06-business-logic.md) §2.5, [`CONSTANTS.md`](CONSTANTS.md) §3 |
| Экспорт PDF / Excel | [`10-reports.md`](10-reports.md) §4, [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §8.3–8.4 |

---

## Правила использования документации

1. Всегда начинай с [`kimi.md`](kimi.md) — там базовые правила и точка входа.
2. Перед изменением кода прочитай [`PRINCIPLES.md`](PRINCIPLES.md) и [`TRAPS.md`](TRAPS.md).
3. При любой проблеме сначала смотри [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md).
4. Для поиска конкретной сущности, сервиса или экрана используй JSON-справочники и алфавитный указатель выше.
5. Для понимания зависимостей смотри [`json/relationships.json`](json/relationships.json) и [`02-architecture.md`](02-architecture.md).
6. Для бизнес-логики смотри [`06-business-logic.md`](06-business-logic.md) и [`json/flows.json`](json/flows.json).
7. Новую константу или лимит сначала ищи в [`CONSTANTS.md`](CONSTANTS.md) и [`json/constants.json`](json/constants.json).
8. После любого изменения обновляй соответствующие JSON и Markdown файлы.

---

> Последнее обновление: 2026-08-20 — пакет «безопасность + UX» (восстановление пароля на экране блокировки, автоблокировка по простою, защита от обхода блокировки, конвенция дружелюбных ошибок, адаптивные модалки, infinite scroll) покрыт тестами; исправлен учёт пополнения кредитной карты (доход на карту уменьшает долг, TRAPS 2.23), в окне операций добавлен фильтр по кредитной карте. Ранее: Этап 7 — добавлены TROUBLESHOOTING.md, CONSTANTS.md, 14-startup.md, 15-converters.md, алфавитный указатель и таблицы быстрого поиска. Добавлен 16-cicd.md (CI/CD).
