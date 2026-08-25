# Adversarial Code Review — AutoKassa

> Дата: 2026-07-04  
> Объём: ~50 тыс. строк C#/XAML  
> Цель: выявить критические уязвимости, ошибки бизнес-логики, проблемы архитектуры и качества кода, которые могут привести к крашам, некорректным финансовым показателям или компрометации данных.

> **Статус на 2026-08-21:** все Critical (1.3–1.6) и все P1 закрыты — см. столбец «Статус» в §5. Открыты два P0: шифрование БД (осознанно отложено — локальное приложение) и санитизация строк в Excel (низкая эксплуатируемость: ClosedXML записывает строки как текст, формулы не вычисляются).

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
- **Статус:** ✅ исправлено — `CalculateNextPaymentDate` ограничивает день через `Math.Min(PaymentDay, DaysInMonth)`; валидация диапазона 1–31 на месте.

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
- **Статус:** ✅ исправлено — для «без ограничения» используется само `MaxValue` как верхняя граница (`GenerateDebtReportAsync`).

### 1.5 Восстановление операции (soft delete) не восстанавливает связи
- **Файл:** `Services/TransactionService.cs` (строки 353–364)
- **Метод:** `RestoreAsync`
- **Описание:** Метод только устанавливает `IsDeleted = false` для самой операции, но не восстанавливает:
  - `CreditCardPurchase`, удалённую при soft delete покупки по карте;
  - `DebtPayment`-связи, hard-удалённые при удалении долга/погашения;
  - soft-deleted операции-погашения, которые `DeleteAsync` пометил удалёнными вместе с долгом.
- **Риск:** двойной учёт денег, некорректный остаток долга, расхождение фактического баланса и кредитного долга.
- **Рекомендация:** либо запретить восстановление связанных операций, либо атомарно восстанавливать всю цепочку зависимостей в рамках одной транзакции.
- **Статус:** ✅ исправлено — `RestoreAsync`/`RestoreRangeAsync` атомарно восстанавливают покупки, погашения кредиток и долговые связи в одной транзакции; покрыто тестами.

### 1.6 `async void` в точке входа приложения
- **Файл:** `App.xaml.cs` (строка 21)
- **Метод:** `OnStartup`
- **Описание:** `protected override async void OnStartup(StartupEventArgs e)` не позволяет вызывающему коду поймать исключения из асинхронного тела. Ошибки при миграции/инициализации могут привести к немедленному крашу процесса вместо контролируемого `Shutdown`.
- **Риск:** необработанный краш на старте.
- **Рекомендация:** переписать стартап на синхронный `OnStartup` с явным `.GetAwaiter().GetResult()` для контролируемого запуска, либо обернуть всё тело в `try/catch` с логированием.
- **Статус:** ✅ исправлено — `OnStartup` синхронный, инициализация через `Task.Run(() => InitializeAsync(...)).GetAwaiter().GetResult()`.

---

## 2. Высокие риски (High)





---

## 3. Средние риски (Medium)



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

| Приоритет | Задача | Файлы | Статус (на 2026-08-21) |
|-----------|--------|-------|------------------------|
| **P0 — Critical** | Шифрование SQLite или защита файла БД | `Services/AppDbContext.cs` | ⬜ Открыто — осознанно отложено (локальное приложение) |
| **P0 — Critical** | Санитизация строк перед записью в Excel | `Services/ExportService.cs` | ⬜ Открыто — низкая эксплуатируемость: ClosedXML пишет строки как текст, формулы не вычисляются |
| **P0 — Critical** | Исправить `GetNextPaymentDateAsync` для 31-го дня | `Services/CreditCardService.cs` | ✅ Исправлено |
| **P0 — Critical** | Защита `DateTime.MaxValue.AddDays(1)` в отчёте по долгам | `Services/ReportService.cs` | ✅ Исправлено |
| **P0 — Critical** | Корректное восстановление связей при `RestoreAsync` | `Services/TransactionService.cs` | ✅ Исправлено, покрыто тестами |
| **P1 — High** | Убрать `async void` в `App.OnStartup` и VM-командах | `App.xaml.cs`, `ViewModels/*` | ✅ `OnStartup` синхронный; все async-команды переведены на `AsyncRelayCommand` (2026-08-21) |
| **P1 — High** | Атомарность операций с кредитками/долгами в одной транзакции | `Services/TransactionService.cs`, `Services/CreditCardService.cs`, `Services/DebtService.cs` | ✅ `TransactionService` — транзакции везде; добавлены в `RepayDebtAsync` и `DebtService.RepayAsync` (2026-08-21) |
| **P1 — High** | Отказ от вечного кэша VM в `NavigationService` | `Services/NavigationService.cs` | ✅ Кэш удалён: страницы — singleton по маркеру `IPageViewModel`, модальные диспозятся при навигации |
| **P1 — High** | Усилить политику паролей | `ViewModels/ChangePasswordViewModel.cs`, `InitialSetupViewModel.cs` | ✅ Единый минимум 6 символов во всех сценариях (2026-08-21) |
| **P1 — High** | Потокобезопасность `_cachedSettings` | `Services/SettingsService.cs` | ✅ Все доступы под `SemaphoreSlim _settingsLock` |
| **P1 — High** | Внедрить `IDialogService` и убрать зависимости VM от View | `ViewModels/SettingsViewModel.cs`, `Services/LockService.cs` | ✅ Внедрён повсеместно |
| **P1 — High** | Отказ от service locator `App.GetService<T>` | `App.xaml.cs`, `ViewModels/DashboardViewModel.cs`, `ReportsViewModel.cs` | ✅ Метод удалён; `GetRequiredService` только в composition root и фабриках (TRAPS 1.2) |
| **P1 — High** | Суммировать `decimal` без приведения к `double` | `Services/*Service.cs` | ✅ Суммы в `decimal`; `(double)` остался только для расчёта процентов |
| **P3 — Low** | Локализация, именованные константы, `DateTime.UtcNow`, async dispose | По всему проекту | ⬜ Частично (reflection `Initialize` задокументирован в TRAPS 1.6; остальное сознательно отложено) |

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
