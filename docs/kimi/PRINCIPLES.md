# PRINCIPLES — принципы правильного кода в AutoKassa

> Этот файл описывает, **как** нужно писать код в проекте. Прочитай перед началом любой задачи.

---

## 1. MVVM

- **ViewModel не знает о View.** Вся бизнес-логика живёт в сервисах.
- **Code-behind View** содержит только минимальную UI-логику (например, обработку кнопок тайтлбара).
- Для команд используй [`RelayCommand`](../../AutoKassa/Helpers/RelayCommand.cs) / [`AsyncRelayCommand`](../../AutoKassa/Helpers/AsyncRelayCommand.cs).
- Для свойств используй [`SetProperty<T>`](../../AutoKassa/Helpers/ViewModelBase.cs) из [`ViewModelBase`](../../AutoKassa/Helpers/ViewModelBase.cs).

```csharp
// ../AutoKassa/ViewModels/TransactionEditViewModel.cs
public class TransactionEditViewModel : ViewModelBase
{
    private readonly ITransactionService _transactionService;

    public TransactionEditViewModel(ITransactionService transactionService, ...)
    {
        _transactionService = transactionService;
        // Для async-операций — только AsyncRelayCommand.
        // НЕ пиши new RelayCommand(async _ => await SaveAsync(), ...):
        // это async void без перехвата ошибок и блокировки повторного запуска.
        SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
    }

    private bool CanSave() => /* проверка заполненности полей */ true;

    private async Task SaveAsync()
    {
        var transaction = BuildTransaction();
        SavedTransaction = await _transactionService.AddAsync(transaction);
    }
}
```

Подробный паттерн `AsyncRelayCommand` (перехват исключений, показ ошибок в UI) — см. раздел 10.

---

## 2. Dependency Injection

- Все зависимости передаются **через конструктор**.
- Регистрация сервисов и ViewModels выполняется в [`App.ConfigureServices`](../../AutoKassa/App.xaml.cs).
- Уважай lifetime: `Singleton`, `Scoped`, `Transient`.
- Для создания `DbContext` в Singleton-сервисах используй [`IDbContextFactory<AppDbContext>`](../../AutoKassa/Services/AppDbContext.cs).

```csharp
// ../AutoKassa/App.xaml.cs
private void ConfigureServices(IServiceCollection services)
{
    services.AddDbContextFactory<AppDbContext>();
    services.AddSingleton<ISettingsService, SettingsService>();
    services.AddTransient<ITransactionService, TransactionService>();
    services.AddTransient<DashboardViewModel>();
}
```

---

## 3. Работа с базой данных

- Все операции с БД — **строго асинхронные**.
- Создавай `DbContext` через фабрику и не храни его долго.
- Для чтения используй `AsNoTracking()`, если не планируешь изменять сущности.
- Для атомарных операций используй `BeginTransactionAsync`.

```csharp
// ../AutoKassa/Services/TransactionService.cs
public async Task<List<Transaction>> GetTransactionsAsync(TransactionFilterParameters filters, CancellationToken ct = default)
{
    await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
    return await context.Transactions
        .AsNoTracking()
        .Include(t => t.Category)
        .Where(t => !t.IsDeleted)
        .Skip(filters.Skip).Take(filters.Take)
        .ToListAsync(ct)
        .ConfigureAwait(false);
}
```

---

## 4. Async/Await

- Публичные асинхронные методы именуются с суффиксом `Async`.
- В сервисах используй `ConfigureAwait(false)`.
- Передавай `CancellationToken` в публичных методах, где это имеет смысл.

```csharp
// ../AutoKassa/Services/TransactionService.cs
public async Task<int> GetTotalCountAsync(TransactionFilterParameters filters, CancellationToken ct = default)
{
    await using var context = await _contextFactory.CreateDbContextAsync().ConfigureAwait(false);
    return await context.Transactions
        .Where(t => !t.IsDeleted)
        .CountAsync(ct)
        .ConfigureAwait(false);
}
```

---

## 5. Валидация

- В ViewModel используй [`INotifyDataErrorInfo`](../../AutoKassa/Helpers/ViewModelBase.cs) через `SetErrors` / `ClearErrors`.
- Дополнительные проверки выполняй в сервисах перед сохранением.

```csharp
// ../AutoKassa/ViewModels/CounterpartyEditViewModel.cs
public string Name
{
    get => _name;
    set
    {
        if (SetProperty(ref _name, value))
            ValidateName();
    }
}

private void ValidateName()
{
    if (string.IsNullOrWhiteSpace(_name))
        SetErrors(nameof(Name), new[] { "Название контрагента обязательно" });
    else if (_name.Length > MaxNameLength)
        SetErrors(nameof(Name), new[] { $"Название не должно превышать {MaxNameLength} символов" });
    else
        ClearErrors(nameof(Name));
}
```

---

## 6. Навигация и жизненный цикл

- Реализуй [`INavigationAware`](../../AutoKassa/Services/INavigationAware.cs), если ViewModel должна реагировать на смену вкладки.
- Для асинхронной загрузки используй [`IAsyncInitializable`](../../AutoKassa/ViewModels/IAsyncInitializable.cs) или `RunAsync` из `ViewModelBase`.
- Отписывайся от событий и освобождай ресурсы в `Dispose()` / `OnDispose()`.
- После изменений данных вызывай [`IDataChangeService.NotifyDataChanged()`](../../AutoKassa/Services/IDataChangeService.cs), чтобы обновить зависимые экраны.

```csharp
// ../AutoKassa/Services/NavigationService.cs
public void NavigateTo<TViewModel>() where TViewModel : ViewModelBase
{
    if (oldView is INavigationAware oldNav)
        oldNav.OnNavigatedFrom();

    _currentView = viewModel;

    if (viewModel is INavigationAware newNav)
        newNav.OnNavigatedTo();

    DisposeViewModel(oldView);
}
```

---

## 7. Логирование

- Используй Serilog через `Log.ForContext<T>()`.
- Логируй значимые события: создание, обновление, удаление операций/категорий.
- Ошибки БД и файловой системы логируй с полным исключением.

```csharp
// ../AutoKassa/Services/TransactionService.cs
private static readonly ILogger _log = Log.ForContext<TransactionService>();

public async Task<Transaction> AddAsync(Transaction transaction)
{
    // ...
    _log.Information("Добавлена операция ID={Id}, тип={Type}, сумма={Amount}", transaction.Id, transaction.Type, transaction.Amount);
    return transaction;
}
```

---

## 8. Тестирование

- Используй [`TestDatabase.Create()`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs) для создания изолированной in-memory SQLite.
- Соединение SQLite должно оставаться открытым на время теста — вызывай `connection.Dispose()` в `Dispose()`.
- Покрывай новую бизнес-логику юнит-тестами.

```csharp
// ../AutoKassa.Tests/Infrastructure/TestDatabase.cs
public static (AppDbContext context, SqliteConnection connection) Create()
{
    var connection = new SqliteConnection("DataSource=:memory:");
    connection.Open();

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseSqlite(connection)
        .Options;

    var context = new AppDbContext(options);
    context.Database.EnsureCreated();

    return (context, connection);
}
```

---

## 9. Чек-лист добавления нового функционала

| Шаг | Что сделать | Куда зафиксировать |
|-----|-------------|--------------------|
| 1 | Добавить/изменить сущность | [`models.json`](json/models.json), [`04-models.md`](04-models.md), миграция EF Core |
| 2 | Добавить/изменить сервис | [`services.json`](json/services.json), [`relationships.json`](json/relationships.json), регистрация в `App.ConfigureServices` |
| 3 | Добавить/изменить ViewModel | [`viewmodels.json`](json/viewmodels.json), [`07-viewmodels.md`](07-viewmodels.md) |
| 4 | Добавить/изменить View | [`08-views.md`](08-views.md), DataTemplate в `MainWindow.xaml` |
| 5 | Добавить/изменить enum | [`enums.json`](json/enums.json) |
| 6 | Добавить/обновить тесты | [`12-tests.md`](12-tests.md) |
| 7 | Обновить сценарии | [`flows.json`](json/flows.json), [`09-flows.md`](09-flows.md) |
| 8 | Проверить сборку и тесты | `dotnet build`, `dotnet test` |

---

## 10. AsyncRelayCommand: правильный паттерн

- Для асинхронных действий используй [`AsyncRelayCommand`](../../AutoKassa/Helpers/AsyncRelayCommand.cs) (без параметра) или `AsyncRelayCommand<T>` (с параметром). Оба класса реализуют `ICommand` напрямую — отдельного интерфейса `IAsyncRelayCommand` в проекте **нет**, свойство объявляй как `ICommand`.
- Конструктор: `new AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)`. Передавай методы группой: `new AsyncRelayCommand(SaveAsync, CanSave)`.
- Повторный запуск блокируется флагом `_isExecuting`: пока команда выполняется, `CanExecute` возвращает `false`. `CanExecuteChanged` привязан к `CommandManager.RequerySuggested`, поэтому вручную вызывать `RaiseCanExecuteChanged` не нужно.
- **Исключения глотаются.** `Execute` — это `async void`, весь вызов обёрнут в `try/catch`, и любое исключение уходит только в лог (`_log.Error(ex, "Ошибка выполнения асинхронной команды")`). UI о сбое **не узнаёт автоматически**: ни toast, ни диалог не появятся. Ошибки, о которых должен знать пользователь, обрабатывай внутри самого метода: `try/catch` + `_dialogService.ShowError(...)` / `_toastService.ShowError(...)`.
- `OperationCanceledException` тоже попадёт в лог — если отмена штатная, лови её внутри метода до общего `catch` (см. §11).

```csharp
// ../AutoKassa/ViewModels/ChangePasswordViewModel.cs
public ICommand SaveCommand { get; }

public ChangePasswordViewModel(...)
{
    SaveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
}

private async Task SaveAsync()
{
    try
    {
        var settings = await _settingsService.GetSettingsAsync();
        // ... проверка и сохранение ...
        _toastService.ShowSuccess("Пароль успешно изменен");
    }
    catch (Exception ex)
    {
        // Без этого catch пользователь не увидит ошибку: AsyncRelayCommand только залогирует её
        _dialogService.ShowError($"Ошибка смены пароля: {ex.Message}");
    }
}
```

> См. также [`TRAPS.md`](TRAPS.md) — ловушки про `async void` и команды.

---

## 11. CancellationTokenSource в ViewModel

- Если операцию можно перезапустить (смена периода, повторная загрузка данных), отменяй предыдущий запрос через `CancellationTokenSource`.
- Заменяй CTS **атомарно** через `Interlocked.Exchange` — иначе при быстром переключении возможны гонки (`ObjectDisposedException`, двойной `Dispose`).
- Отмена — штатная ситуация: лови `OperationCanceledException` и игнорируй.
- Освобождение ресурсов — в `OnDispose()`: [`ViewModelBase`](../../AutoKassa/Helpers/ViewModelBase.cs) реализует невиртуальный `Dispose()`, который вызывает `protected virtual void OnDispose()`. Переопределяй именно `OnDispose()`, а не `Dispose()`. Там же отписывайся от событий и вызывай `base.OnDispose()`.

```csharp
// Паттерн из ../AutoKassa/ViewModels/DashboardViewModel.cs
private CancellationTokenSource _loadCts;

private async Task LoadDataAsync()
{
    // Атомарная замена — защита от гонки при быстром переключении периода
    var newCts = new CancellationTokenSource();
    var oldCts = Interlocked.Exchange(ref _loadCts, newCts);
    oldCts?.Cancel();
    oldCts?.Dispose();
    var ct = newCts.Token;

    try
    {
        IsLoading = true;
        var data = await _dashboardService.LoadDashboardDataAsync(DateFrom, DateTo, ct);
        ApplyDashboardData(data);
    }
    catch (OperationCanceledException) { /* пользователь сменил период — игнорируем */ }
    finally
    {
        IsLoading = false;
    }
}

protected override void OnDispose()
{
    _dataChangeService.DataChanged -= OnDataChanged;

    var cts = Interlocked.Exchange(ref _loadCts, null);
    cts?.Cancel();
    cts?.Dispose();

    base.OnDispose();
}
```

> См. также [`TRAPS.md`](TRAPS.md) — ловушка «Забыть отписаться от `IDataChangeService.DataChanged`» и другие async-ловушки.

---

## 12. Обновление json/relationships.json при изменении зависимостей

[`relationships.json`](json/relationships.json) — машиночитаемая карта связей: `viewsToViewModels`, `viewModelsToServices`, `commands`, `servicesToServices`, `servicesToDbContext`, `dataTemplates`. Её проверяет [`tools/DocValidator`](../../tools/DocValidator/README.md) — при рассинхронизации падает CI job `docs`.

Правило: **изменил зависимости — обнови `relationships.json` в том же коммите.**

1. Найди затронутый раздел: новая View → `viewsToViewModels` и `dataTemplates`; новый сервис в конструкторе ViewModel → `viewModelsToServices`; новая связь сервисов → `servicesToServices`.
2. Добавь/удали записи и обнови поле `lastUpdated`.
3. Проверь соответствие коду:

```bash
dotnet run --project tools/DocValidator -- --project-root .
```

---

## 13. Работа с SettingsService

[`SettingsService`](../../AutoKassa/Services/SettingsService.cs) кеширует `AppSettings` в поле `_cachedSettings` и возвращает **один и тот же объект по ссылке** из `GetSettings()` и `GetSettingsAsync()`.

- **Асинхронный код → `GetSettingsAsync()`.** Синхронный `GetSettings()` блокирует поток семафором (`SemaphoreSlim.Wait()`), а при первом вызове ещё и делает синхронное чтение из БД. Кеш разогревается при старте в `App.InitializeAsync`, но в новых async-сценариях используй `GetSettingsAsync()`.
- **Не мутируй закешированный объект.** Изменение свойств объекта, полученного из `GetSettings()`/`GetSettingsAsync()`, молча меняет состояние всего приложения, но не попадает в БД — а при следующем сохранении из другого места эти правки будут перезаписаны. Считай возвращённый объект read-only.
- **Паттерн редактирования** (как в `SettingsViewModel`): скопируй значения в свойства ViewModel (`SettingsMapper.MapToViewModel` + `Clone` для снимка «до»), дай пользователю редактировать копию, при сохранении собери объект (`MapToModel`) и вызови `SaveSettingsAsync(settings)` — он сохраняет в БД и обновляет кеш.
- Для точечных изменений есть готовые методы: `UpdatePasswordAsync`, `SetThemeAsync`, `SetAutoLockTimeoutAsync`, `SetDefaultOperationTypeAsync`, `SetDefaultCategoryIdAsync` — используй их вместо ручной сборки `AppSettings`.
- После сохранения настроек, влияющих на другие экраны, вызови `IDataChangeService.NotifyDataChanged()`.

> См. также [`TRAPS.md`](TRAPS.md), раздел «Технические ловушки»: настройки кешируются, `AppSettings` — single-row таблица.

---

## 14. Code-behind View: что разрешено и что запрещено

Code-behind (`.xaml.cs`) содержит **только UI-логику**, которую невозможно или непрактично выразить в XAML и ViewModel.

**Разрешено** (реальные примеры в проекте):

- Обработчики чисто визуальных событий, которые делегируют работу ViewModel: двойной клик по строке, контекстное меню — с проверкой `CanExecute` перед `Execute` ([`TransactionsView.xaml.cs`](../../AutoKassa/Views/TransactionsView.xaml.cs)).
- Drag-and-drop: `PreviewMouseMove` → `DragDrop.DoDragDrop`, `Drop` → вызов публичного метода ViewModel ([`CategoryManagerView.xaml.cs`](../../AutoKassa/Views/CategoryManagerView.xaml.cs)).
- Window chrome: кнопки тайтлбара, `WndProc` для `WM_GETMINMAXINFO`, подписка toast-оверлея, dispose `DataContext` при закрытии окна ([`MainWindow.xaml.cs`](../../AutoKassa/MainWindow.xaml.cs)).
- Фильтрация ввода в текстовые поля (`PreviewTextInput` + regex).
- `async void` в обработчиках допустим, но оборачивай тело в `try/catch` с логированием — иначе исключение уйдёт в необработанные исключения приложения.

**Запрещено:**

- Бизнес-логика, расчёты, бизнес-правила — это сервисы и ViewModel.
- Прямые вызовы сервисов и `DbContext` из View.
- `.Result` / `.Wait()` / `GetAwaiter().GetResult()` — deadlock на UI-потоке.
- Создание ViewModel вручную (`new XxxViewModel(...)`); единственное исключение — корень композиции: `MainWindow` получает VM через конструктор из DI.

> См. также [`TRAPS.md`](TRAPS.md), раздел «Архитектурные запреты».

---

> Последнее обновление: этап 7.4.5 — добавлены разделы 10–14 (`AsyncRelayCommand`, `CancellationTokenSource`, обновление `relationships.json`, `SettingsService`, code-behind).
