# Task Templates for AutoKassa

> Шаблоны типовых задач в проекте AutoKassa. Каждый шаблон ссылается на соответствующий project-level skill в `.kimi/skills/` и чек-лист в `checklists/`.
> Перед любой задачей читай [`PRINCIPLES.md`](PRINCIPLES.md) и [`TRAPS.md`](TRAPS.md).

---

## 1. Add a new screen

**Skill:** `.kimi/skills/autokassa-new-screen/SKILL.md`  
**Checklist:** [`checklists/new-screen-checklist.md`](checklists/new-screen-checklist.md)  
**Типичные запросы:** «добавь экран…», «новая страница», «новый пункт меню», «новое окно».

### Goal
Добавить полноценный экран (ViewModel + View + навигация) в WPF-приложение.

### Inputs
- Название экрана и назначение.
- Источники данных (сервисы, сущности).
- Нужна ли кнопка в сайдбаре или только внутренняя навигация.

### Steps
1. Прочитать [`PRINCIPLES.md`](PRINCIPLES.md), [`TRAPS.md`](TRAPS.md), [`00-quickstart.md`](00-quickstart.md), [`02-architecture.md`](02-architecture.md), [`07-viewmodels.md`](07-viewmodels.md), [`08-views.md`](08-views.md), [`json/relationships.json`](json/relationships.json), [`json/viewmodels.json`](json/viewmodels.json).
2. Создать `NewScreenViewModel : ViewModelBase` в `AutoKassa/ViewModels/`.
3. Создать `NewScreenView.xaml` (UserControl) в `AutoKassa/Views/`.
4. Зарегистрировать VM в DI в `App.xaml.cs` (`services.AddTransient<NewScreenViewModel>()`).
5. Добавить `DataTemplate` в `MainWindow.xaml`.
6. Добавить команду навигации в `MainWindowViewModel` и кнопку в сайдбар, если требуется.
7. Реализовать `IAsyncInitializable` / `RunAsync` для асинхронной загрузки данных.
8. Подписаться на `_dataChangeService.DataChanged` и отписаться в `OnDispose()`.
9. Обновить `json/viewmodels.json` и `json/relationships.json`.
10. Добавить описание экрана в `07-viewmodels.md` и `08-views.md`.
11. Запустить `dotnet build` и `dotnet test`.

### Outputs
- Новый `ViewModel` + `View`.
- Регистрация в DI, `DataTemplate`, навигация.
- Обновлённые JSON/Markdown.

### Verification
- [ ] `dotnet build` проходит.
- [ ] `dotnet test` проходит.
- [ ] Экран открывается из сайдбара / навигации.
- [ ] При изменении данных на другом экране новый экран обновляется.
- [ ] Документация обновлена.

---

## 2. Add a new entity + migration

**Skill:** `.kimi/skills/autokassa-new-entity/SKILL.md`  
**Checklist:** [`checklists/new-entity-checklist.md`](checklists/new-entity-checklist.md)  
**Типичные запросы:** «добавь сущность…», «новая таблица», «миграция», «новое поле в модели».

### Goal
Добавить или изменить EF Core-сущность и синхронизировать схему БД через миграцию.

### Inputs
- Название сущности, поля, типы данных.
- Связи с существующими сущностями.
- Нужен ли UI/сервис для работы с сущностью.

### Steps
1. Прочитать [`PRINCIPLES.md`](PRINCIPLES.md), [`TRAPS.md`](TRAPS.md), [`04-models.md`](04-models.md), [`03-database.md`](03-database.md), [`json/models.json`](json/models.json), [`json/relationships.json`](json/relationships.json).
2. Проверить зависимости через `json/relationships.json`.
3. Создать/изменить класс сущности в `AutoKassa/Models/`.
4. Добавить/обновить `DbSet<T>` в `AutoKassa/Services/AppDbContext.cs`.
5. Настроить связи, индексы, ограничения в `OnModelCreating`.
6. Создать миграцию: `cd AutoKassa && dotnet ef migrations add <Name>`.
7. При необходимости обновить DTO/VM/View.
8. Обновить `json/models.json`, `json/relationships.json`, `04-models.md`, `03-database.md`.
9. Запустить `dotnet build` и `dotnet test`.

### Outputs
- Класс сущности, `DbSet`, `OnModelCreating`.
- Новая миграция в `AutoKassa/Migrations/`.
- Обновлённые JSON/Markdown.

### Verification
- [ ] `dotnet ef migrations add` выполнена без ошибок.
- [ ] `dotnet build` проходит.
- [ ] `dotnet test` проходит.
- [ ] Приложение стартует на чистой БД.
- [ ] Документация обновлена.

---

## 3. Add a new service

**Skill:** `.kimi/skills/autokassa-new-service/SKILL.md`  
**Checklist:** [`checklists/new-service-checklist.md`](checklists/new-service-checklist.md)  
**Типичные запросы:** «добавь сервис…», «новая бизнес-логика», «рефакторинг сервисного слоя».

### Goal
Создать сервис с интерфейсом, DI-регистрацией, асинхронным доступом к БД и тестами.

### Inputs
- Название и ответственность сервиса.
- Зависимости (фабрика контекста, другие сервисы).
- Нужен ли `Singleton` или `Transient` lifetime.

### Steps
1. Прочитать [`PRINCIPLES.md`](PRINCIPLES.md), [`TRAPS.md`](TRAPS.md), [`05-services.md`](05-services.md), [`json/services.json`](json/services.json), [`json/relationships.json`](json/relationships.json).
2. Создать `INewService` и `NewService` в `AutoKassa/Services/`.
3. Зарегистрировать в DI в `App.xaml.cs` с правильным lifetime.
4. Внедрить зависимости через конструктор; не использовать `App.GetService<T>()`.
5. Использовать `IDbContextFactory<AppDbContext>`; все публичные методы — `async` с суффиксом `Async`.
6. Использовать `ConfigureAwait(false)` в сервисе, передавать `CancellationToken` где уместно.
7. Добавить логирование через `private static readonly ILogger _log = Log.ForContext<NewService>();`.
8. После мутаций вызывать `_dataChangeService.NotifyDataChanged()`.
9. Обновить `json/services.json`, `json/relationships.json`, `05-services.md`.
10. Добавить юнит-тесты в `AutoKassa.Tests/`.
11. Запустить `dotnet build` и `dotnet test`.

### Outputs
- Интерфейс + реализация сервиса.
- Регистрация в DI.
- Юнит-тесты.
- Обновлённые JSON/Markdown.

### Verification
- [ ] `dotnet build` проходит.
- [ ] `dotnet test` проходит.
- [ ] Сервис корректно разрешается через DI.
- [ ] Документация обновлена.

---

## 4. Fix a bug

**Skill:** `.kimi/skills/autokassa-bugfix/SKILL.md`  
**Checklist:** [`checklists/bugfix-checklist.md`](checklists/bugfix-checklist.md)  
**Типичные запросы:** «исправь баг…», «падает…», «не работает…», «ошибка…», «исключение…».

### Goal
Локализовать и исправить ошибку, добавить тест, предотвращающий регрессию.

### Inputs
- Описание ошибки или исключения.
- Шаги воспроизведения.
- Логи / stack trace (если есть).

### Steps
1. Прочитать [`TRAPS.md`](TRAPS.md), [`06-business-logic.md`](06-business-logic.md), [`json/flows.json`](json/flows.json), [`json/services.json`](json/services.json).
2. Воспроизвести проблему: запустить `dotnet build`, `dotnet test`, посмотреть логи в `logs/`.
3. Найти затронутый сервис/VM/View через `Grep` / `Search`.
4. Проверить, не нарушает ли исправление [`TRAPS.md`](TRAPS.md) (soft delete, MVVM, async, DI и т.д.).
5. Написать или обновить тест, воспроизводящий баг.
6. Применить минимальное исправление.
7. Обновить JSON/Markdown, если изменилась логика или сигнатура.
8. Запустить `dotnet build` и `dotnet test`.

### Outputs
- Исправленный код.
- Тест, покрывающий сценарий.
- Обновлённые JSON/Markdown (при необходимости).

### Verification
- [ ] Тест, воспроизводящий баг, проходит после исправления.
- [ ] `dotnet build` проходит.
- [ ] `dotnet test` проходит.
- [ ] Исправление не нарушает [`TRAPS.md`](TRAPS.md).

---

## 5. Refactor a service / VM

**Skill:** `.kimi/skills/autokassa-refactor/SKILL.md`  
**Checklist:** [`checklists/refactor-checklist.md`](checklists/refactor-checklist.md)  
**Типичные запросы:** «рефакторинг…», «переименуй…», «упрости…», «почисти…».

### Goal
Улучшить структуру кода без изменения внешнего поведения.

### Inputs
- Что именно нужно отрефакторить.
- Причина (дублирование, читаемость, сложность).

### Steps
1. Прочитать [`PRINCIPLES.md`](PRINCIPLES.md), [`TRAPS.md`](TRAPS.md), [`json/relationships.json`](json/relationships.json).
2. Убедиться, что есть тесты до рефакторинга.
3. Внести изменения с минимальной семантической нагрузкой: не менять поведение, только структуру.
4. При переименовании обновить все ссылки (использовать `Rename` в IDE / `Grep`).
5. Запустить `dotnet build` и `dotnet test`.
6. Обновить JSON/Markdown, если изменились имена или зависимости.

### Outputs
- Рефакторенный код.
- Обновлённые JSON/Markdown (если были переименования).

### Verification
- [ ] Все существующие тесты проходят.
- [ ] `dotnet build` проходит.
- [ ] Поведение не изменилось (где применимо — добавлены тесты-защитники).

---

## 6. Add or fix tests

**Skill:** `.kimi/skills/autokassa-test/SKILL.md`  
**Checklist:** [`checklists/test-checklist.md`](checklists/test-checklist.md)  
**Типичные запросы:** «добавь тесты…», «покрой тестами…», «исправь тесты…», «покрытие…».

### Goal
Увеличить покрытие бизнес-логики или починить существующие тесты.

### Inputs
- Какой сервис/VM покрыть.
- Какие сценарии важны (CRUD, фильтрация, edge cases, отмена).

### Steps
1. Прочитать [`12-tests.md`](12-tests.md), [`PRINCIPLES.md`](PRINCIPLES.md), [`AutoKassa.Tests/Infrastructure/TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs).
2. Создать/открыть тестовый класс в `AutoKassa.Tests/`.
3. Использовать `TestDatabase.Create()` / `TestDatabase.CreateWithFactory()` и сохранять `SqliteConnection` в поле класса.
4. Использовать хелперы `SeedExpenseCategory`, `SeedIncomeCategory`, `SeedTransaction` и т.д.
5. Покрывать CRUD, фильтрацию, edge cases, `CancellationToken`, soft delete, бизнес-правила.
6. Реализовать `IDisposable` с `_connection.Dispose()`.
7. Запустить `dotnet test --filter "FullyQualifiedName~Services"`, затем полный `dotnet test`.

### Outputs
- Новые или исправленные тесты.
- Проходящий `dotnet test`.

### Verification
- [ ] `dotnet test --filter "FullyQualifiedName~Services"` проходит.
- [ ] Полный `dotnet test` проходит.
- [ ] Все `SqliteConnection` освобождаются в `Dispose()`.

---

## 7. Update documentation

**Skill:** `.kimi/skills/autokassa-docs/SKILL.md`  
**Checklist:** [`checklists/docs-update-checklist.md`](checklists/docs-update-checklist.md)  
**Типичные запросы:** «обнови документацию…», «docs/kimi…», «добавь в справочник…».

### Goal
Синхронизировать `docs/kimi/` с актуальным состоянием кода.

### Inputs
- Какие сущности/сервисы/VM/потоки изменились.
- Новые правила / ловушки / ограничения.

### Steps
1. Прочитать [`INDEX.md`](INDEX.md), [`json/index.json`](json/index.json).
2. Определить затронутые сущности, сервисы, VM, потоки, ловушки.
3. Обновить соответствующие JSON:
   - `models.json` — сущности.
   - `services.json` — сервисы, lifetime, зависимости.
   - `viewmodels.json` — ViewModels.
   - `relationships.json` — граф связей.
   - `flows.json` — пользовательские сценарии.
   - `traps.json` — запреты и ловушки.
4. Обновить соответствующие Markdown:
   - `04-models.md`, `05-services.md`, `07-viewmodels.md`, `09-flows.md`, `TRAPS.md`.
5. Запустить `tools/DocValidator` и убедиться, что все валидаторы проходят.
6. Запустить `dotnet build` и `dotnet test`.

### Outputs
- Актуализированные JSON и Markdown.
- Проходящие build + test.

### Verification
- [ ] Все изменённые сущности/сервисы/VM отражены в JSON.
- [ ] Markdown-описания не противоречат коду.
- [ ] `tools/DocValidator` проходит без ошибок.
- [ ] `dotnet build` и `dotnet test` проходят.

---

## 8. Add a new report

**Skill:** `.kimi/skills/autokassa-new-screen/SKILL.md` + `.kimi/skills/autokassa-new-service/SKILL.md`  
**Checklist:** [`checklists/new-screen-checklist.md`](checklists/new-screen-checklist.md) + [`checklists/new-service-checklist.md`](checklists/new-service-checklist.md)  
**Типичные запросы:** «добавь отчёт…», «новый вид отчёта…», «экспорт … в PDF/Excel».

### Goal
Добавить отчётную вкладку с формированием данных, UI и экспортом.

### Inputs
- Название отчёта, источники данных.
- Нужен ли экспорт PDF/Excel.
- Фильтры и периоды.

### Steps
1. Прочитать [`10-reports.md`](10-reports.md), [`PRINCIPLES.md`](PRINCIPLES.md), [`TRAPS.md`](TRAPS.md), [`json/viewmodels.json`](json/viewmodels.json), [`json/services.json`](json/services.json), [`json/relationships.json`](json/relationships.json).
2. Создать DTO отчёта в `AutoKassa/Models/Reports/`.
3. Добавить метод в `IReportService` / `ReportService` (`Generate...ReportAsync`).
4. Создать `NewReportViewModel : BaseReportViewModel` в `AutoKassa/ViewModels/Reports/`.
5. Реализовать `LoadDataAsync`, `CheckHasData`, `ExportToPdfAsync`, `ExportToExcelAsync`.
6. Создать `NewReportView.xaml` в `AutoKassa/Views/Reports/`.
7. Добавить фабричный метод в `IReportViewModelFactory` / `ReportViewModelFactory`.
8. Добавить вкладку/навигацию в `ReportsView.xaml` / `ReportsViewModel.cs`.
9. Обновить `json/services.json`, `json/viewmodels.json`, `json/relationships.json`, `10-reports.md`.
10. Добавить тесты на `ReportService`.
11. Запустить `dotnet build` и `dotnet test`.

### Outputs
- DTO, метод в `ReportService`, VM, View, фабричный метод.
- Экспорт (PDF/Excel), если требуется.
- Обновлённые JSON/Markdown.

### Verification
- [ ] Отчёт отображает данные корректно.
- [ ] Фильтры и периоды работают.
- [ ] Экспорт (если есть) генерирует файл.
- [ ] `dotnet build` и `dotnet test` проходят.

---

> Последнее обновление: Этап 5 — DocValidator внедрён и интегрирован в процесс обновления документации.
