# Code Review — Оставшиеся проблемы

> Выполнено 2026-04-12. Критические фиксы (#1-#5) и часть warning (#6-#10, #12, #14) уже применены.

---

## WARNING (требуют правок)

### #9. Синхронная миграция в конструкторе SettingsService
- **Файл:** `Services/SettingsService.cs` — конструктор
- **Проблема:** `context.Database.Migrate()` блокирует UI-поток при первом запуске. На медленном диске = зависание splash screen.
- **Fix:** Вынести миграцию в `App.OnStartup()` до показа UI, выполнять через `await context.Database.MigrateAsync()`. SettingsService конструктор должен только читать кэш.

### #10. Event-подписки без отписки (утечки памяти)
- **Файлы:**
  - `ViewModels/MainWindowViewModel.cs` ~33 — `_navigationService.CurrentViewChanged += ...` без отписки
  - `Views/TransactionEditView.xaml.cs` ~17 — `DataContextChanged += ...` без отписки
  - `Services/LockService.cs` ~64 — lambda в `lockWindow.Closed += ...`
- **Fix:** Реализовать `IDisposable` в ViewModels, отписываться в `Dispose()`. Для Views — подписка на `Unloaded`.

### #11. Удаление категории, установленной как дефолтная
- **Файлы:** `Services/CategoryService.cs` (DeleteAsync), `Models/AppSettings.cs` (DefaultIncomeCategoryId, DefaultExpenseCategoryId)
- **Проблема:** Можно удалить категорию, которая используется как `DefaultIncomeCategoryId` / `DefaultExpenseCategoryId` в настройках — останутся битые FK-ссылки.
- **Fix:** В `CategoryService.DeleteAsync` перед удалением проверять:
  ```csharp
  var settings = await _context.AppSettings.FirstOrDefaultAsync();
  if (settings?.DefaultIncomeCategoryId == id || settings?.DefaultExpenseCategoryId == id)
      throw new InvalidOperationException("Нельзя удалить категорию, установленную по умолчанию");
  ```

### #13. Отсутствие null-check при открытии транзакции
- **Файл:** `ViewModels/DashboardViewModel.cs` ~100
- **Код:** `OpenTransactionCommand = new RelayCommand(t => OpenTransaction(t as Transaction ?? SelectedTransaction));`
- **Проблема:** Если оба null — `OpenTransaction(null)`. Метод проверяет на ~716, но лучше не вызывать вообще.
- **Fix:** Добавить `CanExecute`: `_ => SelectedTransaction != null` или guard в лямбде.

### #15. Несогласованные границы дат (мелкий, но важный)
- **Файл:** `Services/TransactionService.cs`
- **Проблема:** Используется `to.Date.AddDays(1).AddTicks(-1)` + `<=`. Это корректно, но хрупко — при переходе на другую БД или DateTime precision может сломаться.
- **Рекомендация:** Перейти на паттерн exclusive upper bound: `t.Date < to.Date.AddDays(1)` без `AddTicks(-1)`.

### #16. DateTime.Now — нет единого источника времени
- **Файлы:** TransactionService, CategoryService, SettingsService, ExportService, ViewModels
- **Проблема:** `DateTime.Now` разбросан по всему коду. При DST-переходе теоретически возможны дубли/пропуски.
- **Fix (низкий приоритет):** Создать `ITimeProvider` с методом `Now` для тестируемости и единообразия. Или использовать .NET 8 `TimeProvider.System`.

### #17. ViewModels не реализуют IDisposable
- **Файлы:** Все ViewModels, `Helpers/ViewModelBase.cs`
- **Проблема:** ViewModels держат CancellationTokenSource, подписки на события — но нет cleanup при уничтожении.
- **Fix:**
  1. Добавить `virtual void Dispose()` в `ViewModelBase` с `IDisposable`
  2. В `DashboardViewModel` и `TransactionsViewModel` — dispose `_loadCts`
  3. В `MainWindowViewModel` — отписка от `CurrentViewChanged`
  4. Navigation service при смене View должен вызывать `Dispose()` на старой VM

---

## INFO (низкий приоритет, по мере рефакторинга)

### #18. MVVM-нарушения в code-behind
- **Файлы:**
  - `Views/CategoryManagerView.xaml.cs` — drag-drop логика (~23-71, 75-121)
  - `Views/TransactionsView.xaml.cs` — event handlers с прямым доступом к VM (~19-52)
  - `Views/DashboardView.xaml.cs` — вызов команд из code-behind (~18-24)
  - `Views/Reports/BalanceReportView.xaml.cs`, `CategoryReportView.xaml.cs` (~18-24)
- **Рекомендация:** Перенести через attached behaviors / Interaction Triggers. Некритично для desktop-приложения.

### #19. Отсутствие валидации на уровне модели
- **Файлы:**
  - `Models/Transaction.cs` — Amount без `[Range(0.01, 999999999.99)]`
  - `Models/Category.cs` — Color без `[RegularExpression(@"^#[0-9a-fA-F]{6}$")]`
  - `Models/FilterParameters.cs` — Skip/Take без `[Range]`
- **Рекомендация:** Добавить Data Annotations для защиты на уровне БД.

### #20. Отсутствие индексов для частых запросов
- **Файл:** `Services/AppDbContext.cs` ~100-110
- **Рекомендация:** Добавить составные индексы:
  ```csharp
  modelBuilder.Entity<Transaction>()
      .HasIndex(t => new { t.CategoryId, t.IsDeleted });
  modelBuilder.Entity<Transaction>()
      .HasIndex(t => new { t.Type, t.IsDeleted });
  ```

### #21. Nullable reference safety
- **Файлы:**
  - `Services/TransactionService.cs` ~54 — `GetByIdAsync` возвращает `Transaction` вместо `Transaction?`
  - `Models/Transaction.cs` ~53 — `Category` navigation property не nullable
- **Рекомендация:** Обновить сигнатуры, проставить `?` где нужно.

### #22. Пустой PasswordHash в seed-данных
- **Файл:** `Services/AppDbContext.cs` ~246
- **Проблема:** `PasswordHash = ""` — проверяется в App.xaml.cs, но лучше explicit null.

---

## async void в отчётах (отдельный блок)

Базовый класс `BaseReportViewModel` объявляет `ExportToPdf()` / `ExportToExcel()` как `virtual void`.
Наследники переопределяют как `async void`:

- `BalanceReportViewModel.cs` ~257, ~282
- `CategoryReportViewModel.cs` ~260, ~285
- `TransactionDetailReportViewModel.cs` ~211, ~223

**Fix:** Изменить базовый класс:
```csharp
// BaseReportViewModel.cs
protected virtual Task ExportToPdfAsync() => Task.CompletedTask;
protected virtual Task ExportToExcelAsync() => Task.CompletedTask;

// Команды:
ExportToPdfCommand = new RelayCommand(async _ => {
    try { await ExportToPdfAsync(); }
    catch (Exception ex) { _dialogService.ShowError(...); }
});
```
Затем все наследники: `async void ExportToPdf()` → `async Task ExportToPdfAsync()`.
