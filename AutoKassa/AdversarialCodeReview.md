# Adversarial Code Review — AutoKassa

> Дата: 2026-07-04  
> Объём: ~50 тыс. строк C#/XAML  
> Цель: выявить критические уязвимости, ошибки бизнес-логики, проблемы архитектуры и качества кода, которые могут привести к крашам, некорректным финансовым показателям или компрометации данных.

---

## 1. Критические проблемы (Critical)

### 1.3 Crash расчёта следующего платежа при `PaymentDay = 31`
- **Файл:** `Services/CreditCardService.cs` (строки 198–214)
- **Метод:** `GetNextPaymentDateAsync`
- **Описание:**
  ```csharp
  var candidate = new DateTime(referenceDate.Year, referenceDate.Month, card.PaymentDay.Value);
  ```
  падает с `ArgumentOutOfRangeException`, если у карты `PaymentDay = 31`, а в текущем/расчётном месяце меньше 31 дня (апрель, июнь, сентябрь, ноябрь, февраль). Это ломает Dashboard (`LoadCreditMetricsAsync`) и отчёт «Баланс за период».
- **Риск:** невозможность открыть Dashboard / сформировать балансовый отчёт для любой активной карты с таким днём платежа.
- **Рекомендация:** использовать `DateTime.DaysInMonth(year, month)` и ограничивать день платежа при валидации карты.

### 1.4 Crash отчёта по долгам без явной даты окончания
- **Файл:** `Services/ReportService.cs` (строки 340–342)
- **Метод:** `GenerateDebtReportAsync`
- **Описание:**
  ```csharp
  var to = dateTo ?? DateTime.MaxValue;
  var dateToEnd = to.Date.AddDays(1);
  ```
  `DateTime.MaxValue.Date.AddDays(1)` выбрасывает `ArgumentOutOfRangeException`. Хотя UI сейчас передаёт даты, сигнатура метода допускает `null`, и любой вызов без `dateTo` приведёт к падению.
- **Риск:** непредсказуемый краш при расширении функционала или тестировании.
- **Рекомендация:** добавить проверку `if (to == DateTime.MaxValue) dateToEnd = to;` или использовать `DateTime.MaxValue.AddTicks(-1)`.

### 1.5 Восстановление операции (soft delete) не восстанавливает связи
- **Файл:** `Services/TransactionService.cs` (строки 353–364)
- **Метод:** `RestoreAsync`
- **Описание:** Метод только устанавливает `IsDeleted = false` для самой операции, но не восстанавливает:
  - `CreditCardPurchase`, удалённую при soft delete покупки по карте;
  - `DebtPayment`-связи, hard-удалённые при удалении долга/погашения;
  - soft-deleted операции-погашения, которые `DeleteAsync` пометил удалёнными вместе с долгом.
- **Риск:** двойной учёт денег, некорректный остаток долга, расхождение фактического баланса и кредитного долга.
- **Рекомендация:** либо запретить восстановление связанных операций, либо атомарно восстанавливать всю цепочку зависимостей в рамках одной транзакции.

### 1.6 `async void` в точке входа приложения
- **Файл:** `App.xaml.cs` (строка 21)
- **Метод:** `OnStartup`
- **Описание:** `protected override async void OnStartup(StartupEventArgs e)` не позволяет вызывающему коду поймать исключения из асинхронного тела. Ошибки при миграции/инициализации могут привести к немедленному крашу процесса вместо контролируемого `Shutdown`.
- **Риск:** необработанный краш на старте.
- **Рекомендация:** переписать стартап на синхронный `OnStartup` с явным `.GetAwaiter().GetResult()` для контролируемого запуска, либо обернуть всё тело в `try/catch` с логированием.

---

## 2. Высокие риски (High)




### 2.18 Утечки подписок на события / неполное освобождение
- **Файлы:** `Views/ConfirmationDialogView.xaml.cs`, `Views/InitialSetupView.xaml.cs`, `Views/ChangePasswordView.xaml.cs`, `Views/LockScreenView.xaml.cs`, `ViewModels/CategoriesViewModel.cs`, `ViewModels/ReportsViewModel.cs`, `Services/NavigationService.cs`
- **Описание:** Подписки на события `RequestClose`, `OnSetupCompleted`, `OnClose`, `OnUnlocked`, `DataChanged` не отписываются при закрытии окна/навигации. `NavigationService` и `ReportsViewModel` кэшируют VM без `Dispose`.
- **Риск:** утечка памяти, накопление обработчиков, stale-данные.
- **Рекомендация:** явно отписываться в `Unloaded`/`OnDispose` и вызывать `Dispose()` при замене кэшированных VM.

### 2.19 Отсутствие валидации длины пользовательских строк перед записью в БД
- **Файлы:** `ViewModels/TransactionEditViewModel.cs` (строки 201–205), `ViewModels/CounterpartyEditViewModel.cs` (строки 72–82), `ViewModels/CreditCardEditViewModel.cs` (строки 70–74), `ViewModels/InitialSetupViewModel.cs` (строки 264–285), `Helpers/CategoryItemViewModel.cs` (строки 42–46)
- **Описание:** В моделях `MaxLength` ограничен, но ViewModel не валидируют верхнюю границу.
- **Риск:** исключение EF Core или неявное обрезание, DoS, порча данных.
- **Рекомендация:** добавить `MaxLength`-валидацию во все VM перед сохранением.

---

## 3. Средние риски (Medium)

### 3.1 `CancellationToken.None` вместо проброса токена
- **Файл:** `Services/TransactionService.cs` (строки 133, 168, 647, 664)
- **Описание:** Вызовы `_creditCardService.RepayDebtAsync`, `_creditCardService.AddPurchaseAsync`, `_creditCardService.DistributeRepaymentForTransactionAsync`, `_creditCardService.RecalculateOnRepaymentDeletedAsync` передают `CancellationToken.None`, игнорируя токен вызывающего.
- **Риск:** долгие операции нельзя отменить.
- **Рекомендация:** пробрасывать `CancellationToken` через всю цепочку.

### 3.2 N+1 / множественные запросы к БД
- **Файлы:** `ViewModels/CreditCardsViewModel.cs` (строки 262–268), `Services/ReportService.cs` (строки 47–91, 470–496), `ViewModels/DashboardViewModel.cs` (строки 879–888)
- **Описание:** Для каждой кредитной карты параллельно выполняются 4 асинхронных вызова сервиса, каждый — с новым DbContext.
- **Риск:** лавинный рост запросов при большом числе карт, нагрузка на SQLite/WAL.
- **Рекомендация:** добавить batch-методы в `ICreditCardService`.

### 3.3 `AddDbContext<AppDbContext>(ServiceLifetime.Transient)` — лишняя регистрация
- **Файл:** `App.xaml.cs` (строка 162)
- **Описание:** Сервисы используют `IDbContextFactory`, но `AppDbContext` как Transient остаётся доступным для прямого инжектирования.
- **Риск:** непреднамеренное совместное использование контекста.
- **Рекомендация:** убрать регистрацию или явно задокументировать.

### 3.4 `ICreditCardService` зарегистрирован как Scoped, остальные сервисы — Transient
- **Файл:** `App.xaml.cs` (строка 177)
- **Описание:** В WPF нет HTTP-запроса/scope по умолчанию; Scoped-регистрация фактически ведёт себя как Singleton.
- **Риск:** неожиданное поведение lifetime.
- **Рекомендация:** привести к `AddTransient` для единообразия.

### 3.5 Отчёт «Структура по категориям» включает долговые операции
- **Файл:** `Services/ReportService.cs` (строки 176–179)
- **Описание:** Запрос фильтрует только по `Type`, не исключая `PaymentType.Debt`.
- **Риск:** расхождение между отчётом по категориям и балансом/прибылью.
- **Рекомендация:** добавить `.Where(t => t.PaymentType != PaymentType.Debt)`.

### 3.6 «Детализация операций» показывает долги, но итоги их исключают
- **Файл:** `Services/ReportService.cs` (строки 265–323)
- **Описание:** Список транзакций формируется без фильтра по `PaymentType`, но итоги считаются с исключением долгов.
- **Риск:** пользователь видит операцию в списке, но она не входит в итоги — неинтуитивно.
- **Рекомендация:** либо включать долги в итоги с отдельной колонкой, либо фильтровать их в списке.

### 3.7 `DebtReport.TotalReceivable` / `TotalPayable` учитывают уже погашенные долги
- **Файл:** `Models/Reports/DebtReport.cs`, `Services/ReportService.cs` (строки 396–397)
- **Описание:** Суммируют `Amount` для всех долгов со статусом не `WrittenOff`, включая `Repaid`.
- **Риск:** завышенная сумма задолженности.
- **Рекомендация:** учитывать только `Active` долги.

### 3.8 Неполнота текстового поиска на больших объёмах
- **Файл:** `Services/TransactionService.cs` (строки 53–60, 79–86)
- **Описание:** При наличии `SearchText` выборка ограничивается `MaxInMemoryFilterLimit = 5000`. `GetTotalCountAsync` возвращает `5000` как приближённое значение.
- **Риск:** потеря результатов поиска и неверная пагинация.
- **Рекомендация:** реализовать полнотекстовый поиск SQLite (FTS5).

### 3.9 `GetReconciliationDataAsync` показывает текущий кредитный долг вместо долга на дату сверки
- **Файл:** `Services/ReportService.cs` (строки 444–465)
- **Описание:** `CreditDebt` запрашивает `_creditCardService.GetTotalDebtAsync()`, который считает долг за всё время.
- **Риск:** сверка кассы на прошлую дату показывает актуальный кредитный долг.
- **Рекомендация:** передавать дату сверки в метод расчёта долга.

### 3.10 Race condition при проверке лимита кредитной карты
- **Файл:** `Services/CreditCardService.cs` (строки 237–239)
- **Описание:** `currentDebt` вычисляется в одном DbContext, запись `CreditCardPurchase` создаётся в том же, но `TransactionService` открывает новый контекст.
- **Риск:** возможное превышение кредитного лимита при одновременных операциях.
- **Рекомендация:** единый контекст + явная транзакция.


### 3.12 Импорт настроек не валидирует критичные значения
- **Файл:** `Services/SettingsService.cs` (строки 328–355)
- **Описание:** Числовые/строковые поля принимаются как есть. Возможны `BackupKeepCount = int.MaxValue`, `AutoLockTimeout = 0`, `CreditCardPaymentDay = 0`.
- **Риск:** некорректная работа UI и расчётов, краши.
- **Рекомендация:** валидировать все поля при импорте JSON.

### 3.13 `JsonSerializer` без явного `MaxDepth`
- **Файл:** `Services/SettingsService.cs` (строки 302, 322)
- **Описание:** Используются настройки по умолчанию.
- **Риск:** усложнение аудита и защиты от специально подготовленных JSON.
- **Рекомендация:** явно задать `JsonSerializerOptions` с `MaxDepth` и `PropertyNameCaseInsensitive = false`.

### 3.14 Fire-and-forget авто-бэкап без обработки ошибок
- **Файл:** `App.xaml.cs` (строка 144)
- **Описание:** `_ = settingsService.RunAutoBackupIfDueAsync();` — исключения не ловятся.
- **Риск:** скрытые сбои бэкапа.
- **Рекомендация:** добавить `.ContinueWith` с логированием ошибок.

### 3.15 Бизнес-логика / вызовы команд VM из code-behind Views
- **Файлы:** `Views/DashboardView.xaml.cs`, `Views/TransactionsView.xaml.cs`, `Views/Reports/CategoryReportView.xaml.cs`, `Views/Reports/BalanceReportView.xaml.cs`, `Views/Reports/TransactionDetailReportView.xaml.cs`
- **Описание:** Code-behind напрямую вызывает команды VM и меняет `SelectedTransaction`.
- **Риск:** нарушение MVVM, дублирование логики.
- **Рекомендация:** перенести обработчики в VM через `CommandParameter` и behaviors.

### 3.16 Дублирование кода в отчётах
- **Файлы:** `ViewModels/Reports/BalanceReportViewModel.cs`, `CategoryReportViewModel.cs`, `TransactionDetailReportViewModel.cs`, `DebtReportViewModel.cs`
- **Описание:** `ValidateDateRange()`, `SetPeriod(string period)`, `ExportToPdfAsync()` / `ExportToExcelAsync()` почти идентичны.
- **Риск:** расхождения при изменениях, сложность поддержки.
- **Рекомендация:** вынести общую логику в базовый класс `BaseReportViewModel`.

### 3.17 Проблемы с командами (CanExecute без уведомлений)
- **Файлы:** `ViewModels/TransactionsViewModel.cs` (строки 113–115), `ViewModels/DashboardViewModel.cs` (строки 148–154)
- **Описание:** `EditCommand`/`DeleteCommand` зависят от `SelectedTransaction`, но setter не вызывает `RaiseCanExecuteChanged()`.
- **Риск:** кнопки остаются активными/неактивными некорректно.
- **Рекомендация:** добавить вызов `CommandManager.InvalidateRequerySuggested()` или событие `CanExecuteChanged`.

### 3.18 Проблемы INotifyPropertyChanged / валидация
- **Файлы:** `ViewModels/TransactionsViewModel.cs` (строки 372–392), `ViewModels/InitialSetupViewModel.cs` (строки 176–199), `ViewModels/ChangePasswordViewModel.cs` (строки 75–91)
- **Описание:** Некоторые свойства не используют `SetProperty`, ошибки валидации хранятся в обычных свойствах вместо `INotifyDataErrorInfo`.
- **Риск:** UI не обновляется, неединообразие.
- **Рекомендация:** привести все VM к единому механизму валидации.

---

## 4. Низкие риски / наблюдения (Low)



### 4.2 Использование `DateTime.Now` вместо `DateTime.UtcNow`
- **Файлы:** множество сервисов и VM
- **Описание:** Локальное время приводит к непредсказуемому поведению при смене часового пояса/DST.
- **Риск:** низкий.
- **Рекомендация:** хранить `DateTime.UtcNow`, отображать в локальном часовом поясе.

### 4.3 `ExecuteSqlRawAsync` для PRAGMA
- **Файл:** `Services/SettingsService.cs` (строка 39)
- **Описание:** `await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");` — строка константна, но шаблон рискованный.
- **Риск:** низкий при текущем коде, но может стать уязвимостью при рефакторинге.

### 4.4 Жёстко закодированные значения
- **Файлы:** `DashboardViewModel`, `CreditCardListItemViewModel`, `TransactionDetailReportViewModel` и др.
- **Описание:** Символ валюты `" ₽"`, UI-строки `"ЗА МЕСЯЦ"`, preset-цвета, значения по умолчанию — всё hardcoded.
- **Риск:** низкий, но затрудняет локализацию и поддержку.

### 4.5 `NavigationService.NavigateTo<T>(object parameter)` использует reflection
- **Файл:** `Services/NavigationService.cs` (строки 82–83)
- **Описание:** `initializeMethod?.Invoke(viewModel, new[] { parameter });` — magic string.
- **Риск:** низкий, но хрупко.

### 4.6 `ServiceProvider` освобождается синхронно в `OnExit`
- **Файл:** `App.xaml.cs` (строка 221)
- **Описание:** `_serviceProvider?.Dispose()` — у `ServiceProvider` есть `IAsyncDisposable`.
- **Риск:** неполная очистка ресурсов.

### 4.7 `AppDbContext` имеет публичный параметрless конструктор
- **Файл:** `Services/AppDbContext.cs` (строка 12)
- **Описание:** Доступен для случайного создания вне DI/фабрики.
- **Риск:** использование контекста без настроенного подключения.

---

## 5. Приоритеты исправления

| Приоритет | Задача | Файлы |
|-----------|--------|-------|
| **P0 — Critical** | Шифрование SQLite или защита файла БД | `Services/AppDbContext.cs` |
| **P0 — Critical** | Санитизация строк перед записью в Excel | `Services/ExportService.cs` |
| **P0 — Critical** | Исправить `GetNextPaymentDateAsync` для 31-го дня | `Services/CreditCardService.cs` |
| **P0 — Critical** | Защита `DateTime.MaxValue.AddDays(1)` в отчёте по долгам | `Services/ReportService.cs` |
| **P0 — Critical** | Корректное восстановление связей при `RestoreAsync` | `Services/TransactionService.cs` |
| **P1 — High** | Убрать `async void` в `App.OnStartup` и VM-командах | `App.xaml.cs`, `ViewModels/*` |
| **P1 — High** | Атомарность операций с кредитками/долгами в одной транзакции | `Services/TransactionService.cs`, `Services/CreditCardService.cs`, `Services/DebtService.cs` |
| **P1 — High** | Отказ от вечного кэша VM в `NavigationService` | `Services/NavigationService.cs` |
| **P1 — High** | Усилить политику паролей | `ViewModels/ChangePasswordViewModel.cs`, `InitialSetupViewModel.cs` |
| **P1 — High** | Потокобезопасность `_cachedSettings` | `Services/SettingsService.cs` |
| **P1 — High** | Внедрить `IDialogService` и убрать зависимости VM от View | `ViewModels/SettingsViewModel.cs`, `Services/LockService.cs` |
| **P1 — High** | Отказ от service locator `App.GetService<T>` | `App.xaml.cs`, `ViewModels/DashboardViewModel.cs`, `ReportsViewModel.cs` |
| **P1 — High** | Суммировать `decimal` без приведения к `double` | `Services/*Service.cs` |
| **P2 — Medium** | Проброс `CancellationToken`, batch-методы для карт, валидация импорта настроек | `Services/*Service.cs` |
| **P2 — Medium** | Унификация фильтрации долгов в отчётах | `Services/ReportService.cs` |
| **P3 — Low** | Локализация, именованные константы, `DateTime.UtcNow`, async dispose | По всему проекту |

---

## 6. Что сделано хорошо

- Все запросы EF Core параметризованы; SQL-инъекций не обнаружено.
- Пароли и ответы на секретный вопрос хешируются через BCrypt.
- `RestoreBackupAsync` использует `SqliteConnection.ClearAllPools()` и `File.Copy`, а не SQL-скрипты.
- `ExportService` формирует имена файлов из фиксированных шаблонов и `DateTime`, не из пользовательских строк.
- Реализован soft delete для операций с возможностью восстановления.
- Используется WAL-режим SQLite для улучшения конкурентности.

---

*Отчёт подготовлен в рамках adversarial review. Для каждой P0/P1 проблемы рекомендуется завести отдельную задачу и покрыть исправление тестами.*
