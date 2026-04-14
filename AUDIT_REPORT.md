# AutoKassa — Комплексный аудит и план улучшений

**Дата:** 2026-04-13  
**Стек:** C# .NET 8 WPF, Entity Framework Core 8, SQLite, MVVM, Serilog, BCrypt

---

## Что УЖЕ хорошо (не требует изменений)

- BCrypt хеширование паролей с уникальными солями (PasswordService.cs)
- Все запросы через EF Core — параметризованные, нет SQL-инъекций
- Serilog структурированное логирование с 30-дневной ротацией
- Глобальные обработчики исключений (DispatcherUnhandled, AppDomain, TaskScheduler)
- Правильное время жизни DbContext: Scoped для сервисов, IDbContextFactory для синглтонов
- Экспорт настроек исключает чувствительные данные (пароли, ответы на секретные вопросы)
- Актуальные зависимости (EF 8.0.22, BCrypt 4.0.3, QuestPDF 2025.12.1, Serilog 4.3.0)
- Soft-delete паттерн для транзакций и категорий
- 93 юнит-теста с хорошим покрытием сервисного слоя

---

## HIGH: Критичные проблемы

### #1. Утечка памяти: ReportService загружает ВСЕ транзакции в память

**Файл:** `AutoKassa\Services\ReportService.cs` (строки 73-91)  
**Почему критично:** При 100K+ записей (реальный объём за пару лет работы автосервиса) метод `GetInitialBalanceAsync()` материализует все транзакции до указанной даты через `ToListAsync()`. Это десятки МБ оперативки на каждый вызов.

**До:**
```csharp
var transactionsBeforeDate = await query.ToListAsync(); // ВСЕ записи в память!
var incomeBeforeDate = transactionsBeforeDate
    .Where(t => t.Type == OperationType.Income).Sum(t => t.Amount);
var expenseBeforeDate = transactionsBeforeDate
    .Where(t => t.Type == OperationType.Expense).Sum(t => t.Amount);
return incomeBeforeDate - expenseBeforeDate;
```

**После:** (аналог паттерна из TransactionService.cs:166-167)
```csharp
var rows = await query
    .GroupBy(t => t.Type)
    .Select(g => new { Type = g.Key, Total = (decimal)g.Sum(t => (double)t.Amount) })
    .ToListAsync(); // Только 2 строки (Income/Expense), а не тысячи
var income  = rows.FirstOrDefault(r => r.Type == OperationType.Income)?.Total  ?? 0m;
var expense = rows.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m;
return income - expense;
```

---

### #2. Утечка памяти: подписки на события не очищаются

**Файл:** `AutoKassa\ViewModels\TransactionsViewModel.cs` (строки ~587-600)  
**Почему критично:** При каждом обновлении списка `RebuildGroupsAndTotals()` вызывает `GroupedTransactions.Clear()`, но не обнуляет `SelectionChanged` на старых `SelectableTransaction`. Старые объекты держат ссылку на ViewModel через делегат `RefreshSelectionState`, что мешает сборщику мусора их собрать.

**До:**
```csharp
private void RebuildGroupsAndTotals()
{
    // ...
    GroupedTransactions.Clear(); // Старые подписки остаются!
    // ... создаются новые SelectableTransaction с новыми подписками
```

**После:**
```csharp
private void RebuildGroupsAndTotals()
{
    // ...
    // Очистка подписок перед удалением элементов
    foreach (var group in GroupedTransactions)
        foreach (var item in group.Items)
            item.SelectionChanged = null;

    GroupedTransactions.Clear();
    // ...
```

---

### #3. Race condition на CancellationTokenSource

**Файлы:** `DashboardViewModel.cs` (строки 489-492), `TransactionsViewModel.cs` (строки ~485-488)  
**Почему критично:** При быстром переключении периодов два вызова `LoadDataAsync()` могут выполняться параллельно. Второй вызов может `Dispose()` CTS, которым ещё пользуется первый, вызывая `ObjectDisposedException`.

**До:**
```csharp
_loadCts?.Cancel();
_loadCts?.Dispose();
_loadCts = new CancellationTokenSource(); // Не потокобезопасно!
var ct = _loadCts.Token;
```

**После:**
```csharp
var newCts = new CancellationTokenSource();
var oldCts = Interlocked.Exchange(ref _loadCts, newCts);
oldCts?.Cancel();
oldCts?.Dispose();
var ct = newCts.Token;
```

Аналогично в `OnDispose()`:
```csharp
protected override void OnDispose()
{
    var cts = Interlocked.Exchange(ref _loadCts, null);
    cts?.Cancel();
    cts?.Dispose();
}
```

---

### #4. RunAsync проглатывает исключения без feedback пользователю

**Файл:** `AutoKassa\Helpers\ViewModelBase.cs` (строки 17-27)  
**Почему критично:** Ошибки в async операциях (загрузка данных, сохранение) только логируются в файл. Пользователь не видит ничего — приложение выглядит "зависшим".

**До:**
```csharp
protected void RunAsync(Func<Task> action)
{
    action().ContinueWith(t =>
    {
        if (t.IsFaulted)
            Log.ForContext(GetType()).Error(t.Exception?.GetBaseException(), "...");
    }, TaskScheduler.Default);
}
```

**После:** (обратно совместимо — старые вызовы продолжают работать)
```csharp
protected void RunAsync(Func<Task> action, Action<Exception>? onError = null)
{
    action().ContinueWith(t =>
    {
        if (!t.IsFaulted) return;
        var ex = t.Exception?.GetBaseException();
        Log.ForContext(GetType()).Error(ex, "Необработанная ошибка в [{ViewModel}]", GetType().Name);

        if (onError != null && ex != null)
            Application.Current?.Dispatcher?.BeginInvoke(() => onError(ex));
    }, TaskScheduler.Default);
}
```

**Использование:** постепенно обновить критичные вызовы:
```csharp
RunAsync(InitializeAsync, ex => _dialogService.ShowError($"Ошибка загрузки: {ex.Message}"));
```

---

## MEDIUM: Значимые проблемы

### #5. Баг + дублирование: "Неделя" считается по-разному

**Файлы:**
- `DashboardViewModel.cs` — `SetPeriodDates()` — неделя с **понедельника** (ISO 8601)
- `BalanceReportViewModel.cs` — `SetPeriod()` — неделя с **воскресенья**
- `CategoryReportViewModel.cs` — `SetPeriod()` — неделя с **воскресенья**
- `TransactionDetailReportViewModel.cs` — `SetPeriod()` — неделя с **воскресенья**

**Почему важно:** Пользователь видит разные данные "за неделю" на Dashboard и в отчётах. Код продублирован 4 раза — при правке в одном месте баг остаётся в трёх других.

**Исправление:** Создать `AutoKassa\Helpers\PeriodHelper.cs`:
```csharp
public static class PeriodHelper
{
    public static (DateTime From, DateTime To) GetDateRange(string period)
    {
        var today = DateTime.Today;
        return period switch
        {
            "Today"   => (today, today),
            "Week"    => (today.AddDays(-((int)today.DayOfWeek + 6) % 7), today), // Понедельник
            "Month"   => (new DateTime(today.Year, today.Month, 1), today),
            "Quarter" => (new DateTime(today.Year, ((today.Month - 1) / 3) * 3 + 1, 1), today),
            "Year"    => (new DateTime(today.Year, 1, 1), today),
            _         => (today, today)
        };
    }
}
```

Рефакторить все 4 VM на использование `PeriodHelper.GetDateRange()`.

---

### #6. OnTransactionAdded?.Invoke() вне try-catch

**Файл:** `AutoKassa\ViewModels\QuickAddViewModel.cs` (строка ~178)  
**Почему важно:** Обработчик — `async void` лямбда из DashboardViewModel. Если она бросит исключение, оно обходит catch-блок и может крашнуть приложение.

**Исправление:**
```csharp
try
{
    OnTransactionAdded?.Invoke();
}
catch (Exception handlerEx)
{
    Log.ForContext<QuickAddViewModel>().Error(handlerEx, "Ошибка в обработчике OnTransactionAdded");
}
```

---

### #7. TransactionEditViewModel — 568 строк, смешанные ответственности

**Файл:** `AutoKassa\ViewModels\TransactionEditViewModel.cs`  
**Почему важно:** Калькулятор (100+ строк) имеет собственное состояние (`_calcLeft`, `_calcOp`, `_calcWaiting`, `_calcCurrentInput`) и не зависит от остального ViewModel. Смешение усложняет тестирование и навигацию по коду.

**Исправление:** Вынести в `AutoKassa\ViewModels\CalculatorViewModel.cs`:
```csharp
public class CalculatorViewModel : ViewModelBase
{
    public bool IsOpen { get; set; }
    public string Display { get; set; }
    public Action<string>? OnResult { get; set; }
    public ICommand ToggleCommand { get; }
    public ICommand DigitCommand { get; }
    public ICommand OpCommand { get; }
    // ... вся логика калькулятора
}
```

В TransactionEditViewModel:
```csharp
public CalculatorViewModel Calculator { get; }
// Конструктор:
Calculator = new CalculatorViewModel();
Calculator.OnResult = result => AmountText = result;
```

XAML: `{Binding IsCalcOpen}` -> `{Binding Calculator.IsOpen}`

---

## LOW: Защитные улучшения

### #8. BackupPath не валидируется на path traversal

**Файл:** `AutoKassa\Services\SettingsService.cs` (строки 358-401)  
**Почему:** Импорт настроек может подсунуть `BackupPath = "..\..\Windows\System32"`.

**Исправление:** Добавить `SanitizeBackupPath()`:
```csharp
private static string? SanitizeBackupPath(string? path)
{
    if (string.IsNullOrWhiteSpace(path)) return null;
    try
    {
        var fullPath = Path.GetFullPath(path);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        if (!fullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase)
            && !fullPath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase))
            return null;
        return fullPath;
    }
    catch { return null; }
}
```

---

### #9. MainWindow не гарантирует Dispose ViewModel при закрытии

**Файл:** `AutoKassa\MainWindow.xaml.cs`  
**Почему:** ViewModelBase реализует IDisposable, MainWindowViewModel подписан на события NavigationService. Без явного Dispose подписка остаётся до завершения процесса.

**Исправление:**
```csharp
protected override void OnClosed(EventArgs e)
{
    (DataContext as IDisposable)?.Dispose();
    base.OnClosed(e);
}
```

---

## Порядок реализации

| Фаза | Проблемы | Риск изменений | Описание |
|:-----:|:--------:|:--------------:|----------|
| 1 | #1, #3 | Низкий | Утечка памяти + race condition — точечные правки в 3 файлах |
| 2 | #4, #6 | Низкий | Обработка ошибок — обратно совместимые добавления |
| 3 | #5, #2 | Средний | PeriodHelper (новый файл + 4 рефакторинга) + очистка подписок |
| 4 | #7 | Средний | Вынос калькулятора (новый файл + XAML-биндинги) |
| 5 | #8, #9 | Низкий | Защитные правки |

## Верификация

- Запустить 93 существующих теста после каждой фазы (`dotnet test`)
- Для #1: убедиться что `ToListAsync` заменён на `GroupBy` — написать тест с > 1000 транзакций
- Для #5: написать юнит-тест `PeriodHelper` на понедельник/воскресенье
- Для #3: ручной тест — быстро переключать периоды на Dashboard 10+ раз
- Сборка: `dotnet build` (exit code 1 из-за кириллицы в пути — проверять "Ошибок: 0")

## Затрагиваемые файлы

**Изменяемые:**
- `AutoKassa\Services\ReportService.cs` — #1
- `AutoKassa\Helpers\ViewModelBase.cs` — #4
- `AutoKassa\ViewModels\TransactionsViewModel.cs` — #2, #3
- `AutoKassa\ViewModels\DashboardViewModel.cs` — #3, #5
- `AutoKassa\ViewModels\Reports\BalanceReportViewModel.cs` — #5
- `AutoKassa\ViewModels\Reports\CategoryReportViewModel.cs` — #5
- `AutoKassa\ViewModels\Reports\TransactionDetailReportViewModel.cs` — #5
- `AutoKassa\ViewModels\TransactionEditViewModel.cs` — #7
- `AutoKassa\ViewModels\QuickAddViewModel.cs` — #6
- `AutoKassa\Services\SettingsService.cs` — #8
- `AutoKassa\MainWindow.xaml.cs` — #9

**Новые:**
- `AutoKassa\Helpers\PeriodHelper.cs` — #5
- `AutoKassa\ViewModels\CalculatorViewModel.cs` — #7
