# DocValidator

Консольное приложение для сверки исходного кода **AutoKassa** с JSON-справочниками в `docs/kimi/json/`.

## Запуск

Из корня репозитория:

```bash
dotnet run --project tools/DocValidator -- --project-root .
```

## Аргументы

| Аргумент | Описание | По умолчанию |
|----------|----------|--------------|
| `--project-root <path>` | Корень репозитория | Текущая директория |
| `--docs-path <path>` | Путь к `docs/kimi/json` | `<project-root>/docs/kimi/json` |
| `--app-path <path>` | Путь к проекту AutoKassa | `<project-root>/AutoKassa` |

## Валидаторы

- `EnumValidator` — enum из `AutoKassa/Models/Enums/*.cs` vs `enums.json`.
- `ViewValidator` — XAML-файлы vs `relationships.json`.
- `DiLifetimeValidator` — DI-регистрации vs `services.json`. Сканер учитывает, что `AddDbContextFactory<AppDbContext>()` помимо фабрики (Singleton) неявно регистрирует сам `AppDbContext` как Scoped (поведение EF Core).
- `DbContextInjectionValidator` — запрет прямой инъекции `AppDbContext` в конструкторы классов приложения: сервисы обязаны получать `IDbContextFactory<AppDbContext>`. Методы-помощники, принимающие контекст параметром, не проверяются.
- `ModelValidator` — сущности vs `models.json`.
- `ViewModelValidator` — ViewModels vs `viewmodels.json`.
- `TrapSyncValidator` — `TRAPS.md` vs `traps.json`.
- `ConstantsSyncValidator` — `constants.json` vs исходный код: обязательные поля у каждой константы (`name`, `value`, `type`, `sourceFiles`, `description`), существование файлов из `sourceFiles` (относительно `sourceRoot` из JSON). Имя константы ищется текстово хотя бы в одном `sourceFiles`; если имя условное (документационное, например `WindowWidth` для XAML), валидатор дополнительно ищет значение (для ISO-дат — форму `yyyy, m, d`, для путей — последний сегмент, для диапазонов `a–b` — границы). Если не найдено ни имя, ни значение — записывается предупреждение (warning), а не ошибка: прогон остаётся зелёным, предупреждения выводятся жёлтым.

## CI

Job `docs` в `.github/workflows/ci.yml` запускает `DocValidator` автоматически на каждый `push` / `pull_request` в `master`.
