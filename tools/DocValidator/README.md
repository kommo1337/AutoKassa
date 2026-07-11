# DocValidator

Консольное приложение для сверки исходного кода **AutoKassa** с JSON-справочниками в `docs/kimi/json/`.

## Запуск

Из корня репозитория:

```bash
dotnet run --project tools/DocValidator -- --project-root .
```

Для подробного вывода:

```bash
dotnet run --project tools/DocValidator -- --project-root . --verbose
```

## Аргументы

| Аргумент | Описание | По умолчанию |
|----------|----------|--------------|
| `--project-root <path>` | Корень репозитория | Текущая директория |
| `--docs-path <path>` | Путь к `docs/kimi/json` | `<project-root>/docs/kimi/json` |
| `--app-path <path>` | Путь к проекту AutoKassa | `<project-root>/AutoKassa` |
| `--verbose` | Подробный вывод | `false` |

## Валидаторы

- `EnumValidator` — enum из `AutoKassa/Models/Enums/*.cs` vs `enums.json`.
- `ViewValidator` — XAML-файлы vs `relationships.json`.
- `DiLifetimeValidator` — DI-регистрации vs `services.json`.
- `ModelValidator` — сущности vs `models.json`.
- `ViewModelValidator` — ViewModels vs `viewmodels.json`.
- `TrapSyncValidator` — `TRAPS.md` vs `traps.json`.

## CI

Job `docs` в `.github/workflows/ci.yml` запускает `DocValidator` автоматически на каждый `push` / `pull_request` в `master`.
