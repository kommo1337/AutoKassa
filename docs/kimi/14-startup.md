# 14-startup — запуск и инициализация приложения

> Этот файл описывает точную последовательность запуска AutoKassa: настройку логирования, построение DI-контейнера, инициализацию БД (миграции, WAL), разогрев кеша настроек и выбор стартового окна. Все шаги сверены с кодом [`App.xaml.cs`](../../AutoKassa/App.xaml.cs).

---

## 1. Точка входа

- Проект [`AutoKassa.csproj`](../../AutoKassa/AutoKassa.csproj): `<OutputType>WinExe</OutputType>`, `<UseWPF>true</UseWPF>`, `net10.0-windows`.
- [`App.xaml`](../../AutoKassa/App.xaml) **не задаёт `StartupUri`** — весь запуск выполняется кодом в `App.OnStartup()`. В `App.xaml` находятся только ресурсы: цветовые кисти, конвертеры, стили.
- Сгенерированный `Main()` (в `obj/.../App.g.cs`) создаёт `App` и вызывает `Run()`, который приводит к [`OnStartup(StartupEventArgs)`](../../AutoKassa/App.xaml.cs).

---

## 2. Пошаговая инициализация

Все шаги выполняются в `OnStartup` ([`App.xaml.cs`](../../AutoKassa/App.xaml.cs)) строго в этом порядке:

| # | Шаг | Код | Что делает | Что сломается при нарушении порядка |
|---|-----|-----|-----------|-------------------------------------|
| 1 | Настройка Serilog | `Log.Logger = new LoggerConfiguration()...` | Файловый лог `logs/autokassa-.log` **рядом с exe** (`Assembly.GetExecutingAssembly().Location`), ротация по дням (`RollingInterval.Day`), хранение 30 файлов, шаблон `{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}` | Ошибки старта (шаги 4–7) не попадут в лог — диагностика станет невозможной |
| 2 | Обработчики необработанных исключений | `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException` | `Log.Fatal`/`Log.Error` + `MessageBox` (для UI-исключений, `Handled = true`) | Исключения старта упадут молча или с стандартным диалогом WPF без записи в лог |
| 3 | Лог версии | `Log.Information("=== AutoKassa запуск, версия {Version} ===", ...)` | Маркер начала сессии в логе | — |
| 4 | `base.OnStartup(e)` | в `try/catch` | Стандартная инициализация WPF | Ошибка → `Log.Fatal` + `ShowFatalError` + `Shutdown(1)` |
| 5 | Регистрация сервисов | `ConfigureServices(services)` | Заполнение `ServiceCollection` (см. §3) | — |
| 6 | Построение провайдера | `services.BuildServiceProvider()` в `try/catch` | Создание DI-контейнера | Ошибка конфигурации DI → `Log.Fatal` + `MessageBox` + `Shutdown(1)` |
| 7 | Асинхронная инициализация БД | `Task.Run(() => InitializeAsync(_serviceProvider)).GetAwaiter().GetResult()` в `try/catch` | Миграции + WAL + разогрев кеша настроек (см. §2.1) | Ошибка миграций/БД → `Log.Fatal` + `MessageBox` + `Shutdown(1)` |
| 8 | Проверка пароля | `settingsService.IsPasswordSet()` | Перед диалогами `ShutdownMode` переводится в `OnExplicitShutdown`; если пароль **не установлен** → `InitialSetupView.ShowDialog()`; если установлен → сразу главное окно | См. §4 |
| 9 | Главное окно | `_serviceProvider.GetRequiredService<MainWindow>(); mainWindow.Show()` | Разрешение Singleton `MainWindow` (в конструкторе `MainWindowViewModel` стартуют таймер автоблокировки и навигация на дашборд); после `Show()` восстанавливается `ShutdownMode.OnLastWindowClose` | — |
| 10 | Координатор автобэкапа | `_serviceProvider.GetRequiredService<IAutomaticBackupService>().Start()` | Немедленная проверка локального и облачного бэкапа, затем `PeriodicTimer` раз в час. Исключения рабочего цикла логируются внутри сервиса | — |

На каждом критическом шаге (4, 6, 7) исключение обрабатывается одинаково: `Log.Fatal` → `ShowFatalError(ex)` (MessageBox с текстом ошибки и ссылкой на `logs/autokassa-*.log`) → `Shutdown(1)`.

### 2.1. `InitializeAsync` — что именно инициализируется

Метод `InitializeAsync(IServiceProvider)` ([`App.xaml.cs`](../../AutoKassa/App.xaml.cs)) выполняет два действия на потоке пула:

1. **Миграции и WAL** — статический метод [`SettingsService.MigrateAsync(IDbContextFactory<AppDbContext>)`](../../AutoKassa/Services/SettingsService.cs):
   - `context.Database.MigrateAsync()` — применяет все pending-миграции. На первом запуске создаёт `AutoKassa.db` в каталоге приложения (путь задаёт [`AppDbContext.GetDatabasePath()`](../../AutoKassa/Services/AppDbContext.cs): `AppDomain.CurrentDomain.BaseDirectory + "AutoKassa.db"`; используется при регистрации пула контекстов в `ConfigureServices`) и заливает seed-данные: системные категории `Id = 1–11` и строку `AppSettings` с `Id = 1`.
   - `ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;")` — включает WAL-режим SQLite для конкурентности читателей и писателей.
2. **Разогрев кеша настроек** — `await settingsService.GetSettingsAsync()`:
   - Загружает `AppSettings` в поле `_cachedSettings` синглтона `SettingsService`.
   - После этого все **синхронные** вызовы `GetSettings()` (которые берут `SemaphoreSlim.Wait()` и при промахе кеша делают блокирующий I/O) работают из кеша без обращения к диску.

> ⚠️ Если убрать разогрев, первый синхронный `GetSettings()` из UI-потока выполнит блокирующее чтение БД — подвисание интерфейса (см. [`TRAPS.md`](TRAPS.md)).

### 2.2. Почему `Task.Run(...).GetAwaiter().GetResult()`

`OnStartup` — переопределяемый `void`-метод, его сигнатуру изменить нельзя. Варианты и их проблемы:

- **`async void OnStartup`** — исключения из `await` уходят мимо `try/catch` в `SynchronizationContext`, приложение падает без `Log.Fatal` и `MessageBox`; кроме того, нельзя гарантировать, что инициализация завершится **до** показа окон (шаг 8–9).
- **`InitializeAsync(...).GetAwaiter().GetResult()` без `Task.Run`** — классический deadlock: UI-поток блокируется ожиданием задачи, а продолжение после `await` внутри задачи ждёт освобождения UI-потока (Dispatcher/`SynchronizationContext`).
- **`Task.Run(...).GetAwaiter().GetResult()`** (текущий код) — вся работа уходит на поток пула, где UI-поток не нужен; внутри `InitializeAsync` дополнительно стоит `ConfigureAwait(false)`. UI-поток блокируется, но задаче он не требуется → deadlock невозможен. Исключение разворачивается и ловится `catch` с `Log.Fatal`.

Комментарий в коде подтверждает намерение: «Асинхронная инициализация выполняется синхронно на фоновом потоке, чтобы избежать deadlock на UI-потоке и позволить обработать ошибки».

---

## 3. DI-контейнер: карта регистраций

Вся регистрация — в `ConfigureServices(IServiceCollection)` ([`App.xaml.cs`](../../AutoKassa/App.xaml.cs)). Подробный разбор lifetime — в [`02-architecture.md`](02-architecture.md) §3.

### Singleton (живут всё время работы приложения)

| Сервис | Реализация / примечание |
|--------|------------------------|
| `INavigationService` | `NavigationService` — хранит `CurrentView`, получает `IServiceProvider` |
| `IPasswordService` | `PasswordService` (BCrypt) |
| `ISettingsService` | `SettingsService` — кеш `AppSettings` + `SemaphoreSlim` |
| `IDialogService` | `DialogService` |
| `IToastNotificationService` | `ToastNotificationService` |
| `ILockService` | `LockService` — получает `IServiceProvider`, таймер автоблокировки |
| `IExportService` | `ExportService` (PDF/Excel) |
| `IDataChangeService` | `DataChangeService` — шина уведомлений об изменении данных |
| `ISettingsMapper`, `ISettingsValidator`, `IAppInfoService` | сервисы настроек |
| `IBackupCoordinator` | `BackupCoordinator` |
| `IYandexDiskAuthService`, `IYandexDiskBackupService`, `IYandexDiskApiClient`, `IYandexDiskTokenStore`, `YandexDiskOptions` + общий `HttpClient` | облачный бэкап на Яндекс Диск (состояние OAuth/очереди) |
| `IAutomaticBackupService` | `AutomaticBackupService` — координатор расписания автобэкапа (таймер + сериализация запусков) |
| `IReportViewModelFactory` | `ReportViewModelFactory` |
| `ITransactionEditViewModelFactory` | `TransactionEditViewModelFactory` |
| `MainWindow` | через фабрику: разрешает `MainWindowViewModel` + `IToastNotificationService` и вызывает `new MainWindow(viewModel, toastService)` |

### Transient (новый экземпляр при каждом разрешении)

- Доменные сервисы: `ITransactionService`, `ITransactionListService`, `ITransactionGroupingService`, `IInlineTransactionService`, `ITransactionSelectionService`, `ICategoryService`, `ICounterpartyService`, `IDebtService`, `IReportService`, `ICreditCardService`, `ICreditCardSettingsService`.
- Дашборд: `IDashboardService`, `IDashboardPeriodCoordinator`, `IDashboardTransactionCoordinator`.
- **Все ViewModels**: `MainWindowViewModel`, `InitialSetupViewModel`, `DashboardViewModel`, `TransactionsViewModel`, `TransactionEditViewModel`, `CategoriesViewModel`, `CategoryManagerViewModel`, `ReportsViewModel`, `BalanceReportViewModel`, `CategoryReportViewModel`, `TransactionDetailReportViewModel`, `DebtReportViewModel`, `CounterpartiesViewModel`, `CounterpartyEditViewModel`, `CreditCardsViewModel`, `CreditCardEditViewModel`, `CreditCardRepaymentViewModel`, `ReconciliationViewModel`, `SettingsViewModel`, `ChangePasswordViewModel`, `LockScreenViewModel`, `PasswordRecoveryViewModel`.
- **Views** (только две): `ChangePasswordView`, `LockScreenView`. Остальные представления создаются через `DataTemplate` в `MainWindow.xaml`, а `InitialSetupView` — вручную в `OnStartup`.

### DbContext

```csharp
services.AddPooledDbContextFactory<AppDbContext>(options =>
    options.AddInterceptors(new SqlitePragmaInterceptor()));
```

`IDbContextFactory<AppDbContext>` (синглтон) — единственный способ доступа к БД для сервисов: каждый вызов создаёт короткоживущий контекст, который переиспользуется из пула EF Core. Явной регистрации `AppDbContext` нет, но фабрика неявно делает его разрешимым как Scoped — прямая инъекция в конструкторы при этом запрещена и контролируется `DbContextInjectionValidator` (см. [`02-architecture.md`](02-architecture.md) §3). `SqlitePragmaInterceptor` применяет per-connection PRAGMA на каждом открытии соединения. Подробнее — [`03-database.md`](03-database.md).

---

## 4. Выбор стартового окна

В `OnStartup` реализованы **две** ветки:

1. **Пароль не установлен** (`IsPasswordSet() == false` — пустой `PasswordHash` в закешированных настройках):
   - `InitialSetupViewModel` разрешается из DI, окно создаётся вручную: `new InitialSetupView(setupViewModel)`.
   - `ShowDialog()` блокирует `OnStartup` до закрытия окна.
   - ViewModel ([`InitialSetupViewModel`](../../AutoKassa/ViewModels/InitialSetupViewModel.cs)): пароль минимум 6 символов, подтверждение, секретный вопрос (предопределённый или свой до 200 символов) и ответ (минимум 2 символа); хеширование через BCrypt (`PasswordService.HashPassword`), сохранение через `SetPasswordAsync`.
   - Успех → `DialogResult = true` → переход к шагу 9. Закрытие окна без настройки → `Log.Information("Пользователь закрыл начальную настройку — завершение")` + `Shutdown()`.
2. **Пароль установлен** → сразу `MainWindow`. Экран блокировки при запуске **не показывается**: настройка `RequirePasswordOnStartup` хранится в БД и отображается в настройках, но в `OnStartup` не проверяется.

Перед показом диалогов до главного окна `ShutdownMode` переводится в `OnExplicitShutdown`, а после `mainWindow.Show()` возвращается в `OnLastWindowClose`. Это обязательно: в режиме по умолчанию (`OnLastWindowClose`) закрытие диалога — единственного на тот момент окна приложения — инициирует `Application.Shutdown()`, и показанный следом `MainWindow` уже не спасает: приложение завершается сразу после старта (в логе подряд «Главное окно открыто» и «=== AutoKassa завершение ===»).

При разрешении `MainWindow` из DI выполняется конструктор [`MainWindowViewModel`](../../AutoKassa/ViewModels/MainWindowViewModel.cs): `LoadSettings()`, подписка на `CurrentViewChanged`, создание команд навигации, **`_lockService.StartAutoLockTimer()`** и `NavigateToDashboard()`. То есть дашборд открывается ещё до `mainWindow.Show()`.

> ℹ️ **Автоблокировка в процессе работы** выполняется через [`LockService.Lock()`](../../AutoKassa/Services/LockService.cs): по таймеру (`DispatcherTimer`, интервал `AutoLockTimeout` минут, по умолчанию 10; таймер не стартует, если `AutoLockEnabled == false` или таймаут `<= 0`) или по команде `LockCommand` в `MainWindowViewModel`. `Lock()` разрешает `LockScreenViewModel` из DI и открывает окно через `DialogService.ShowLockScreen()` с blur-эффектом главного окна (radius 15).

---

## 5. Sequence-диаграмма запуска

```mermaid
sequenceDiagram
    participant App as App (UI-поток)
    participant DI as ServiceProvider
    participant Pool as Поток пула (Task.Run)
    participant DB as SQLite (AutoKassa.db)
    participant Win as Окна

    App->>App: Serilog + обработчики исключений + лог версии
    App->>DI: ConfigureServices(services)
    App->>DI: BuildServiceProvider()
    App->>Pool: Task.Run(InitializeAsync).GetAwaiter().GetResult()
    Pool->>DI: GetRequiredService IDbContextFactory&lt;AppDbContext&gt;
    Pool->>DB: Database.MigrateAsync() — pending-миграции + seed
    Pool->>DB: PRAGMA journal_mode=WAL
    Pool->>DI: GetRequiredService ISettingsService
    Pool->>DB: GetSettingsAsync() — разогрев кеша
    Pool-->>App: Инициализация завершена (или исключение → Fatal + Shutdown(1))
    App->>App: ShutdownMode = OnExplicitShutdown
    App->>DI: ISettingsService.IsPasswordSet() (из кеша)
    alt Пароль не установлен
        App->>Win: InitialSetupView.ShowDialog()
        Win-->>App: DialogResult == true (иначе Shutdown())
    end
    App->>DI: GetRequiredService MainWindow (Singleton)
    Note over DI: Конструктор MainWindowViewModel:<br/>StartAutoLockTimer() + NavigateToDashboard()
    App->>Win: mainWindow.Show()
    App->>App: ShutdownMode = OnLastWindowClose
    App->>Pool: RunAutoBackupIfDueAsync() (fire-and-forget)
```

---

## 6. Почему важен порядок

- **Миграции до первого обращения к БД.** Любой запрос сервиса до `MigrateAsync()` упал бы на отсутствующей схеме (таблицы/столбцы из новых миграций).
- **WAL до открытия соединений сервисами.** `journal_mode` — персистентное свойство файла БД; его включение требует отсутствия других активных читателей. Выполняется тем же соединением сразу после миграций, до старта UI.
- **Разогрев кеша до UI.** `MainWindowViewModel` и другие VM читают настройки через синхронный `GetSettings()`. Без разогрева первый вызов сделал бы блокирующий I/O на UI-потоке.
- **Проверка пароля после разогрева.** `IsPasswordSet()` читает `PasswordHash` из кеша — без повторного обращения к БД.
- **Serilog до всего.** Иначе шаги 4–7 не смогут записать `Log.Fatal` при сбое.

---

## 7. Типичные проблемы при старте

| Симптом | Вероятная причина | Что делать |
|---------|-------------------|-----------|
| MessageBox «Критическая ошибка» и выход с кодом 1 | Сбой на шаге 6 (DI) или 7 (миграции/БД): повреждённый `AutoKassa.db`, нет прав на запись в каталог приложения | Открыть `logs/autokassa-*.log` рядом с exe, искать `Fatal`. См. [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §3.2 |
| `SQLite Error 5: database is locked` | WAL не включён или файл БД занят другим процессом (DB Browser, второй экземпляр приложения) | Закрыть сторонние программы; проверить, что `PRAGMA journal_mode=WAL` отработал. См. [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) §4.2 |
| Deadlock после рефакторинга `OnStartup` | `Task.Run` заменён на прямой `.Result`/`.GetAwaiter().GetResult()` без выноса на поток пула | Вернуть паттерн из §2.2 |
| Подвисание UI на первом экране | Удалён разогрев кеша настроек в `InitializeAsync` | Восстановить `GetSettingsAsync()` до показа окон (§2.1) |
| Нет файла лога и БД | Приложение запущено из каталога без прав записи (например, portable-сборка в `Program Files`) | Запускать из каталога с правами записи — лог и БД создаются рядом с exe |
| Приложение закрывается сразу после окна настройки пароля | Пользователь закрыл `InitialSetupView` без завершения — штатное поведение | В логе: «Пользователь закрыл начальную настройку — завершение» |
| Приложение закрывается сразу после модального диалога, показанного до главного окна (в логе подряд «Главное окно открыто» и «=== AutoKassa завершение ===») | `ShutdownMode` по умолчанию (`OnLastWindowClose`): закрытие единственного на тот момент окна инициирует `Application.Shutdown()`, показанный следом `MainWindow` не спасает | Не удалять перевод `ShutdownMode` в `OnExplicitShutdown` перед диалогами в `OnStartup` (см. §4) |

---

## 8. Завершение работы

`OnExit(ExitEventArgs)` ([`App.xaml.cs`](../../AutoKassa/App.xaml.cs)): пишет в лог «=== AutoKassa завершение ===», останавливает координатор автобэкапа — `IAutomaticBackupService.StopAsync()` (отмена активной операции и ожидание её завершения до 10 с; см. [`TRAPS.md`](TRAPS.md) §3.19) — затем вызывает `Log.CloseAndFlush()` (дозапись буферов Serilog) и `_serviceProvider?.Dispose()` (освобождает Singleton-сервисы, включая `LockService` с его таймером).

---

## 9. Связь с другими файлами

- [`02-architecture.md`](02-architecture.md) — архитектура MVVM/DI, lifetime сервисов, навигация.
- [`03-database.md`](03-database.md) — миграции, seed-данные, WAL, `IDbContextFactory`.
- [`11-settings-security.md`](11-settings-security.md) — настройки, пароль, автоблокировка, бэкапы.
- [`TROUBLESHOOTING.md`](TROUBLESHOOTING.md) — устранение неполадок при запуске и в работе.
