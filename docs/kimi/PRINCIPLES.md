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
        SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
    }

    private async Task SaveAsync()
    {
        var transaction = BuildTransaction();
        SavedTransaction = await _transactionService.AddAsync(transaction);
    }
}
```

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

> Последнее обновление: подэтапы 3.1–3.2 — Markdown-описания.
