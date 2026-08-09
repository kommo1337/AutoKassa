# 13. Соглашения по коду

> Этот файл фиксирует стиль кодирования, архитектурные правила и соглашения AutoKassa.

---

## 1. Именование

| Конструкция | Стиль | Пример |
|-------------|-------|--------|
| Классы, методы, свойства, константы | PascalCase | `ReportService`, `GenerateBalanceReportAsync` |
| Параметры, локальные переменные | camelCase | `dateFrom`, `paymentType` |
| Private поля | _camelCase | `_contextFactory`, `_cachedSettings` |
| Асинхронные методы | Async-суффикс | `GetSettingsAsync`, `SaveAsync` |
| Интерфейсы | I + PascalCase | `IReportService`, `ISettingsService` |

---

## 2. Комментарии и XML-документация

- XML-комментарии (`/// <summary>`) **обязательны** для всего public API.
- Inline-комментарии — для сложной/нетривиальной логики.
- Все комментарии на русском языке.

---

## 3. Async/Await и CancellationToken

- Все операции с БД — строго асинхронные (`async Task` + `await`).
- `ConfigureAwait(false)` используется в сервисах.
- `CancellationToken` передаётся в публичные методы сервисов, где это имеет смысл.
- Пример:
  ```csharp
  Task<BalanceReport> GenerateBalanceReportAsync(
      DateTime dateFrom,
      DateTime dateTo,
      PaymentType? paymentType = null,
      CancellationToken ct = default);
  ```

---

## 4. Работа с базой данных

- Сервисы получают `AppDbContext` через конструктор (DI).
- Сервисы, требующие ручного управления контекстом (например, `SettingsService`), используют `IDbContextFactory<AppDbContext>`.
- Seed-данные добавляются только в [`AppDbContext.OnModelCreating`](../../AutoKassa/Services/AppDbContext.cs).
- Soft delete: фильтрация по `!t.IsDeleted` на уровне сервиса.
- SQLite WAL-режим включён через `PRAGMA journal_mode=WAL` (персистентно, в `SettingsService.MigrateAsync`); per-connection прагмы (`synchronous`, `temp_store`, `cache_size`) применяет [`SqlitePragmaInterceptor`](../../AutoKassa/Services/SqlitePragmaInterceptor.cs) на каждом открытии соединения.

---

## 5. MVVM-правила

- **Никакой бизнес-логики в code-behind View.**
- ViewModel не должен знать о View.
- Команды — [`RelayCommand`](../../AutoKassa/Helpers/RelayCommand.cs) / [`AsyncRelayCommand`](../../AutoKassa/Helpers/AsyncRelayCommand.cs) из `Helpers`.
- Свойства — `SetProperty<T>` из [`ViewModelBase`](../../AutoKassa/Helpers/ViewModelBase.cs).
- Валидация — `INotifyDataErrorInfo` (`SetErrors` / `ClearErrors`).
- Все VM наследуют `ViewModelBase`.

---

## 6. Логирование

- Каждый сервис имеет статический `ILogger`:
  ```csharp
  private static readonly ILogger _log = Log.ForContext<ExportService>();
  ```
- Логируются значимые события: создание, обновление, удаление операций/категорий.
- Ошибки БД и файловой системы логируются с полным исключением.
- Логи не содержат паролей или хешей.
- Serilog: `logs/autokassa-.log`, rolling по дням, хранение 30 дней.

---

## 7. Dependency Injection

Регистрация в [`App.xaml.cs`](../../AutoKassa/App.xaml.cs) → `ConfigureServices`:

| Lifetime | Сервисы |
|----------|---------|
| Singleton | `NavigationService`, `PasswordService`, `SettingsService`, `DialogService`, `ToastNotificationService`, `LockService`, `ExportService`, `DataChangeService`, `SettingsMapper`, `SettingsValidator`, `AppInfoService`, `BackupCoordinator`, `ReferenceDataCache`, `ReportViewModelFactory`, `TransactionEditViewModelFactory` |
| Singleton | Page ViewModel вкладок (маркер `IPageViewModel`): `DashboardViewModel`, `TransactionsViewModel`, `CategoriesViewModel`, `CounterpartiesViewModel`, `CreditCardsViewModel`, `ReportsViewModel`, `SettingsViewModel` |
| Transient | `TransactionService`, `TransactionListService`, `TransactionGroupingService`, `InlineTransactionService`, `TransactionSelectionService`, `CategoryService`, `CounterpartyService`, `DebtService`, `ReportService`, `CreditCardService`, `CreditCardSettingsService`, `DashboardService`, `DashboardPeriodCoordinator`, `DashboardTransactionCoordinator`, модальные/диалоговые ViewModels |

- `AppDbContext` не инжектируется в конструкторы — доступ к БД только через фабрику (контролирует `DbContextInjectionValidator` из DocValidator).
- `IDbContextFactory<AppDbContext>` — Singleton (регистрация `AddPooledDbContextFactory`, контексты переиспользуются из пула); сам контекст неявно разрешим как Scoped.

---

## 8. Добавление нового экрана

1. Создать ViewModel, наследующий `ViewModelBase`.
2. Создать View (UserControl/XAML).
3. Зарегистрировать VM в DI в `App.xaml.cs`: page ViewModel вкладки — `services.AddSingleton<NewViewModel>()` + маркер `IPageViewModel`; модальную/диалоговую — `services.AddTransient<NewViewModel>()`.
4. Добавить `DataTemplate` в `MainWindow.xaml`.
5. Добавить команду навигации в `MainWindowViewModel` и кнопку в сайдбар.
6. Обновить [`json/viewmodels.json`](json/viewmodels.json) и [`json/relationships.json`](json/relationships.json).
7. Описать экран в [`07-viewmodels.md`](07-viewmodels.md) и [`08-views.md`](08-views.md).
8. Прогнать `dotnet build` и `dotnet test`.

---

## 9. Прочие важные конвенции

- **Софт-делит:** удаление операций только через `IsDeleted = true`. Восстановление через `RestoreAsync`.
- **Категории:** нельзя удалить системную, категорию по умолчанию или с привязанными операциями (даже удалёнными).
- **Поиск по описанию:** через SQLite FTS5 (`TransactionsFts`, unicode61) — регистронезависимо, по префиксам слов; SQL `LOWER()`/`Contains` для кириллицы не используем.
- **Язык интерфейса и документации:** русский.
- **Single-row настройки:** `AppSettings.Id = 1`.
- **Долги не влияют на фактический баланс.**
- **Кредитные покупки не уменьшают фактический баланс.**
