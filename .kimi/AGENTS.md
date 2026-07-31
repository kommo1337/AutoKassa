# AutoKassa — директивы для Kimi Code CLI

> Проект: `c:\Users\Лиза Блинова\source\repos\kommo1337\AutoKassa`
> Назначение: десктопное WPF-приложение для учёта финансовых операций автосервиса, SQLite, .NET 10.

---

## Обязательный порядок чтения

Перед выполнением **любой** задачи прочитать в указанном порядке:

1. [`docs/kimi/kimi.md`](docs/kimi/kimi.md) — точка входа, технический стек, базовые правила.
2. [`docs/kimi/PRINCIPLES.md`](docs/kimi/PRINCIPLES.md) — принципы написания кода.
3. [`docs/kimi/TRAPS.md`](docs/kimi/TRAPS.md) — запреты, ловушки, типичные ошибки.
4. [`docs/kimi/INDEX.md`](docs/kimi/INDEX.md) — полная карта документации.

---

## Директивы перед изменением кода

- Перед изменением любого **сервиса** прочитать [`docs/kimi/json/relationships.json`](docs/kimi/json/relationships.json) и уточнить, кто от него зависит.
- Перед изменением **сущности** прочитать [`docs/kimi/04-models.md`](docs/kimi/04-models.md) и [`docs/kimi/json/models.json`](docs/kimi/json/models.json).
- Перед изменением **ViewModel** прочитать [`docs/kimi/07-viewmodels.md`](docs/kimi/07-viewmodels.md) и [`docs/kimi/json/viewmodels.json`](docs/kimi/json/viewmodels.json).
- Перед изменением **View** прочитать [`docs/kimi/08-views.md`](docs/kimi/08-views.md).
- Перед изменением **бизнес-логики операций/долгов/кредиток** прочитать [`docs/kimi/06-business-logic.md`](docs/kimi/06-business-logic.md) и [`docs/kimi/json/flows.json`](docs/kimi/json/flows.json).
- Перед изменением **запуска и инициализации** (`App.xaml.cs`, порядок миграций/WAL/кеша) прочитать [`docs/kimi/14-startup.md`](docs/kimi/14-startup.md).
- Перед добавлением **новой константы, лимита или дефолта** проверить [`docs/kimi/CONSTANTS.md`](docs/kimi/CONSTANTS.md) и [`docs/kimi/json/constants.json`](docs/kimi/json/constants.json) — возможно, значение уже существует.
- При **диагностике любой проблемы** сначала смотреть [`docs/kimi/TROUBLESHOOTING.md`](docs/kimi/TROUBLESHOOTING.md).
- Перед изменением **CI workflow** (`.github/workflows/`) или **DocValidator** (`tools/DocValidator/`) прочитать [`docs/kimi/16-cicd.md`](docs/kimi/16-cicd.md).

---

## Директивы после изменений

1. Обновить соответствующие JSON-справочники и Markdown-файлы в [`docs/kimi/`](docs/kimi/).
2. Запустить сборку и тесты:
   ```bash
   dotnet build
   dotnet test
   ```
3. Убедиться, что изменения не противоречат [`docs/kimi/TRAPS.md`](docs/kimi/TRAPS.md) и [`docs/kimi/PRINCIPLES.md`](docs/kimi/PRINCIPLES.md).

---

## Автоматический выбор skill

При типовых задачах активируй соответствующий project-level skill из `.kimi/skills/`:

| Запрос | Skill | Чек-лист |
|--------|-------|----------|
| Новый экран / страница / окно / пункт меню | `autokassa-new-screen` | [`docs/kimi/checklists/new-screen-checklist.md`](docs/kimi/checklists/new-screen-checklist.md) |
| Новая сущность / миграция / изменение модели | `autokassa-new-entity` | [`docs/kimi/checklists/new-entity-checklist.md`](docs/kimi/checklists/new-entity-checklist.md) |
| Новый сервис / бизнес-логика | `autokassa-new-service` | [`docs/kimi/checklists/new-service-checklist.md`](docs/kimi/checklists/new-service-checklist.md) |
| Исправление ошибки / исключения / некорректного поведения | `autokassa-bugfix` | [`docs/kimi/checklists/bugfix-checklist.md`](docs/kimi/checklists/bugfix-checklist.md) |
| Тесты / покрытие / исправление тестов | `autokassa-test` | [`docs/kimi/checklists/test-checklist.md`](docs/kimi/checklists/test-checklist.md) |
| Рефакторинг / переименование / упрощение | `autokassa-refactor` | [`docs/kimi/checklists/refactor-checklist.md`](docs/kimi/checklists/refactor-checklist.md) |
| Обновление документации / JSON-справочников | `autokassa-docs` | [`docs/kimi/checklists/docs-update-checklist.md`](docs/kimi/checklists/docs-update-checklist.md) |
| Code review / проверка конвенций | `autokassa-review` | [`docs/kimi/checklists/bugfix-checklist.md`](docs/kimi/checklists/bugfix-checklist.md) + [`refactor-checklist.md`](docs/kimi/checklists/refactor-checklist.md) |
| Релиз / публикация | `autokassa` | [`docs/kimi/checklists/release-checklist.md`](docs/kimi/checklists/release-checklist.md) |

Если ни один skill не подходит, используй `autokassa` meta-skill для базового контекста.

Сводка шаблонов задач: [`docs/kimi/TASK_TEMPLATES.md`](docs/kimi/TASK_TEMPLATES.md).

---

## Критические правила (кратко)

- Не добавлять бизнес-логику в code-behind View.
- Не использовать статический Service Locator (`App.GetService<T>()`), кроме исключительных случаев.
- Все операции с БД — асинхронные (`async/await` + `ConfigureAwait(false)`).
- Сервисы получают `IDbContextFactory<AppDbContext>`, а не `AppDbContext` напрямую.
- Soft delete для операций (`IsDeleted = true`); физическое удаление запрещено.
- После изменений данных вызывать `IDataChangeService.NotifyDataChanged()`.

---

## Быстрые ссылки

| Тема | Файл |
|------|------|
| Полная документация | [`docs/kimi/INDEX.md`](docs/kimi/INDEX.md) |
| Шаблоны задач и чек-листы | [`docs/kimi/TASK_TEMPLATES.md`](docs/kimi/TASK_TEMPLATES.md), [`docs/kimi/checklists/`](docs/kimi/checklists/) |
| Project-level skills | [`.kimi/skills/`](.kimi/skills/) |
| JSON-справочники | [`docs/kimi/json/`](docs/kimi/json/) |
| Архитектура / DI / MVVM | [`docs/kimi/02-architecture.md`](docs/kimi/02-architecture.md) |
| База данных | [`docs/kimi/03-database.md`](docs/kimi/03-database.md) |
| Сервисы | [`docs/kimi/05-services.md`](docs/kimi/05-services.md) |
| Бизнес-логика | [`docs/kimi/06-business-logic.md`](docs/kimi/06-business-logic.md) |
| Тесты | [`docs/kimi/12-tests.md`](docs/kimi/12-tests.md) |
| Соглашения | [`docs/kimi/13-conventions.md`](docs/kimi/13-conventions.md) |
| Устранение неполадок | [`docs/kimi/TROUBLESHOOTING.md`](docs/kimi/TROUBLESHOOTING.md) |
| Константы и лимиты | [`docs/kimi/CONSTANTS.md`](docs/kimi/CONSTANTS.md), [`docs/kimi/json/constants.json`](docs/kimi/json/constants.json) |
| Запуск приложения / DI | [`docs/kimi/14-startup.md`](docs/kimi/14-startup.md) |
| XAML-конвертеры | [`docs/kimi/15-converters.md`](docs/kimi/15-converters.md) |
| CI/CD / валидация документации | [`docs/kimi/16-cicd.md`](docs/kimi/16-cicd.md) |
