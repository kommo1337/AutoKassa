# План реализации системы долгов (дебиторская / кредиторская задолженность)

> Статус: **в реализации**. Пункты 1–5 выполнены (модели, enum'ы, миграция, сервисный слой, UI, навигация, конвертеры, тесты). Осталось проверить миграцию на существующей БД (пункт 18).  
> Метод учёта: **кассовый** (долговая операция не влияет на прибыль, погашение — реальный доход/расход).  
> Автоматическое создание операции-погашения с привязкой к долгу. Поддержка частичных погашений.

---

## 1. Цель и принципы

### 1.1. Что должно получиться
- Возможность оформить операцию «в долг» с выбором контрагента.
- Отдельный справочник контрагентов (клиенты, филиалы, поставщики).
- Автоматическое создание операции-погашения из экрана долгов / контрагентов.
- Частичные погашения: один долг — несколько платежей.
- Отчёт по долгам во вкладке «Отчёты».
- Корректное отображение в дашборде и балансе.

### 1.2. Ключевые принципы
1. **Долговая операция** (`PaymentType == Debt`) фиксирует обязательство, но **не влияет** на фактический баланс кассы и **не попадает** в прибыль.
2. **Операция погашения** — обычная операция с `PaymentType` `Cash` / `NonCash` / `CreditCard`. Она влияет на фактический баланс и прибыль.
3. **Категория погашения** = категория погашаемого долга.
4. **Soft delete** работает для всех операций. Удаление погашения возвращает долг в статус `Active`.
5. **Кредитные карты** не затрагиваются — система долгов реализуется отдельно.
6. **Обратная совместимость** с существующими БД сохраняется через EF Core миграцию с `DEFAULT`-значениями.

---

## 2. Модели данных

### 2.1. `Models/Enums/PaymentType.cs`
Добавить значение:

```csharp
public enum PaymentType
{
    Cash = 1,
    NonCash = 2,
    CreditCard = 3,
    Debt = 4        // Долг
}
```

### 2.2. `Models/Enums/DebtStatus.cs` (новый файл)
```csharp
public enum DebtStatus
{
    NotDebt = 0,    // Обычная операция
    Active = 1,     // Долг не погашен (или погашен частично)
    Repaid = 2,     // Долг погашен полностью
    WrittenOff = 3  // Долг списан
}
```

### 2.3. `Models/Enums/CounterpartyType.cs` (новый файл)
```csharp
public enum CounterpartyType
{
    Client = 1,     // Клиент
    Branch = 2,     // Филиал
    Supplier = 3,   // Поставщик
    Other = 4       // Прочее
}
```

### 2.4. `Models/Counterparty.cs` (новый файл)
```csharp
[Table("Counterparties")]
public class Counterparty
{
    [Key] public int Id { get; set; }
    [Required][MaxLength(150)] public string Name { get; set; }
    [Required] public CounterpartyType Type { get; set; }
    [MaxLength(20)] public string? Phone { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    [Required] public bool IsActive { get; set; } = true;
    [Required] public DateTime CreatedAt { get; set; }
    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
```

### 2.5. `Models/DebtPayment.cs` (новый файл)
Связь между долговой операцией и операциями-погашениями (один долг — много погашений).

```csharp
[Table("DebtPayments")]
public class DebtPayment
{
    [Key] public int Id { get; set; }
    [Required] public int DebtTransactionId { get; set; }
    [ForeignKey(nameof(DebtTransactionId))] public virtual Transaction DebtTransaction { get; set; }
    [Required] public int RepaymentTransactionId { get; set; }
    [ForeignKey(nameof(RepaymentTransactionId))] public virtual Transaction RepaymentTransaction { get; set; }
    [Required][Column(TypeName = "decimal(18,2)")] public decimal Amount { get; set; }
}
```

### 2.6. `Models/Transaction.cs`
Добавить поля:

```csharp
public int? CounterpartyId { get; set; }
[ForeignKey(nameof(CounterpartyId))] public virtual Counterparty? Counterparty { get; set; }

[Required] public DebtStatus DebtStatus { get; set; } = DebtStatus.NotDebt;
```

> Поле `DebtStatus` добавляется как `NOT NULL DEFAULT 0`, поэтому старые операции автоматически получат статус `NotDebt`.

---

## 3. DbContext и миграции

### 3.1. `Services/AppDbContext.cs`
- Добавить `DbSet<Counterparty> Counterparties { get; set; }`
- Добавить `DbSet<DebtPayment> DebtPayments { get; set; }`
- В `OnModelCreating` настроить:
  - `Counterparty.Name` — уникальный индекс (case-insensitive через ` COLLATE NOCASE` или валидацию в сервисе).
  - `DebtPayment` — составной уникальный индекс на `(DebtTransactionId, RepaymentTransactionId)`.
  - FK `Transaction → Counterparty` с `DeleteBehavior.Restrict`.
  - FK `DebtPayment → Transaction(DebtTransactionId)` и `DebtPayment → Transaction(RepaymentTransactionId)` с `DeleteBehavior.Restrict`.

### 3.2. Миграция
Создать новую миграцию:

```bash
cd AutoKassa
dotnet ef migrations add AddDebtSupport
```

Миграция должна включать:
- `CREATE TABLE Counterparties`
- `CREATE TABLE DebtPayments`
- `ALTER TABLE Transactions ADD COLUMN CounterpartyId INTEGER NULL`
- `ALTER TABLE Transactions ADD COLUMN DebtStatus INTEGER NOT NULL DEFAULT 0`
- Индексы для новых полей.

---

## 4. Сервисный слой

### 4.1. `Services/ICounterpartyService.cs` (новый)
```csharp
public interface ICounterpartyService
{
    Task<IReadOnlyList<Counterparty>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Counterparty>> GetActiveAsync(CounterpartyType? type = null, CancellationToken ct = default);
    Task<Counterparty?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Counterparty> AddAsync(Counterparty counterparty, CancellationToken ct = default);
    Task UpdateAsync(Counterparty counterparty, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default); // запретить, если есть операции
    Task<bool> ExistsAsync(string name, CancellationToken ct = default);
}
```

### 4.2. `Services/CounterpartyService.cs` (новый)
Реализация с `IDbContextFactory<AppDbContext>`.  
Валидация: нельзя удалить контрагента, у которого есть связанные операции (включая удалённые — проверять по `Transactions.Any()`).

### 4.3. `Services/IDebtService.cs` (новый)
```csharp
public interface IDebtService
{
    /// <summary>
    /// Получает список долгов с остатком и статусом.
    /// </summary>
    Task<IReadOnlyList<DebtItem>> GetDebtsAsync(
        OperationType? direction = null,
        int? counterpartyId = null,
        DebtStatus? status = null,
        CancellationToken ct = default);

    /// <summary>
    /// Создаёт операцию-погашение для указанного долга.
    /// </summary>
    Task<Transaction> RepayAsync(int debtTransactionId, decimal amount,
        PaymentType paymentType, DateTime date, string? description = null,
        CancellationToken ct = default);

    /// <summary>
    /// Списывает долг (без создания операции движения денег).
    /// </summary>
    Task WriteOffAsync(int debtTransactionId, CancellationToken ct = default);

    /// <summary>
    /// Получает остаток по долгу.
    /// </summary>
    Task<decimal> GetRemainingAmountAsync(int debtTransactionId, CancellationToken ct = default);

    /// <summary>
    /// Обновляет статусы долгов после изменения/удаления операции погашения.
    /// </summary>
    Task RecalculateStatusAsync(int debtTransactionId, CancellationToken ct = default);
}
```

### 4.4. `Services/DebtService.cs` (новый)
Реализация:
- `GetDebtsAsync` — выбирает операции с `PaymentType == Debt` и `DebtStatus != NotDebt`, считает сумму погашений через `DebtPayment`, возвращает остаток.
- `RepayAsync`:
  1. Проверить, что долг существует и активен.
  2. Проверить, что сумма погашения `> 0` и `<= остаток`.
  3. Создать операцию `Transaction` с `Type` противоположным долгу (`Income` для долга-расхода? — см. раздел 4.6), `PaymentType = paymentType`, `CategoryId = категория долга`, `CounterpartyId = контрагент долга`, `DebtStatus = NotDebt`.
  4. Создать запись `DebtPayment`.
  5. Пересчитать статус долга.
- `WriteOffAsync` — меняет `DebtStatus` на `WrittenOff`.
- `RecalculateStatusAsync` — суммирует `DebtPayment.Amount` для долга и устанавливает `Active`/`Repaid`.

### 4.5. `Models/Reports/DebtItem.cs` (новый DTO)
```csharp
public class DebtItem
{
    public int TransactionId { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal RepaidAmount { get; set; }
    public decimal RemainingAmount => Amount - RepaidAmount;
    public DebtStatus Status { get; set; }
    public OperationType Direction { get; set; }
    public int CounterpartyId { get; set; }
    public string CounterpartyName { get; set; }
    public CounterpartyType CounterpartyType { get; set; }
    public string CategoryName { get; set; }
    public string? Description { get; set; }
}
```

### 4.6. Направление долга
- `Transaction.Type == Income` + `PaymentType == Debt` → нам должны (дебиторская задолженность). Погашение — `Income` (поступили деньги).
- `Transaction.Type == Expense` + `PaymentType == Debt` → мы должны (кредиторская задолженность). Погашение — `Expense` (отдали деньги).

То есть погашение всегда имеет тот же `OperationType`, что и долг.

### 4.7. `Services/TransactionService.cs`
- В `AddAsync`/`UpdateAsync` добавить обработку `PaymentType.Debt`:
  - Если `PaymentType == Debt`, то `CounterpartyId` обязателен.
  - Установить `DebtStatus = Active`.
- В `DeleteAsync` (soft delete) — если удаляется долговая операция:
  - Удалить связанные `DebtPayment` (жёстко или каскадно, если настроено).
  - Если удаляется операция погашения — вызвать `_debtService.RecalculateStatusAsync` для связанного долга.

### 4.8. `Services/IReportService.cs` / `Services/ReportService.cs`
- В агрегации доходов/расходов (`TotalIncome`, `TotalExpense`) **исключать** операции с `PaymentType == Debt`.
- В фактический баланс (`GetFactBalanceAsync`) долги уже не попадают, так как там фильтр по `Cash`/`NonCash`.
- Добавить метод:
  ```csharp
  Task<DebtReport> GenerateDebtReportAsync(
      DateTime? dateFrom, DateTime? dateTo,
      DebtDirection? direction, DebtStatus? status,
      CancellationToken ct = default);
  ```
- Создать `Models/Reports/DebtReport.cs`:
  ```csharp
  public class DebtReport : ReportBase
  {
      public decimal TotalReceivable { get; set; }      // Нам должны
      public decimal TotalPayable { get; set; }         // Мы должны
      public decimal ActiveReceivable { get; set; }
      public decimal ActivePayable { get; set; }
      public List<DebtItem> Items { get; set; }
  }
  ```

### 4.9. `Services/ExportService.cs`
Добавить методы экспорта отчёта по долгам:
- `ExportDebtReportToPdfAsync`
- `ExportDebtReportToExcelAsync`

---

## 5. UI / ViewModels

### 5.1. `ViewModels/CounterpartyViewModel.cs` (новый)
VM для одной строки в списке контрагентов:
- `Counterparty` модель
- `ActiveDebtAmount` — текущий активный долг контрагента
- Команды: `EditCommand`, `DeleteCommand`

### 5.2. `ViewModels/CounterpartiesViewModel.cs` (новый)
- Список `CounterpartyViewModel`
- Команды: `AddCommand`, `RefreshCommand`, `FilterByTypeCommand`
- Свойства: `SearchText`, `SelectedTypeFilter`
- Переход к редактированию/созданию через `CounterpartyEditViewModel`.

### 5.3. `ViewModels/CounterpartyEditViewModel.cs` (новый)
Форма создания/редактирования контрагента с валидацией:
- `Name` — обязательное, уникальное
- `Type` — enum
- `Phone`, `Notes`

### 5.4. `Views/CounterpartiesView.xaml` / `Views/CounterpartyEditView.xaml` (новые)
- Список контрагентов с типом и суммой долга.
- Кнопки добавить/изменить/удалить.

### 5.5. `ViewModels/TransactionEditViewModel.cs`
- Добавить `ObservableCollection<Counterparty> AvailableCounterparties`
- Добавить `Counterparty? SelectedCounterparty`
- При `SelectedPaymentType == PaymentType.Debt`:
  - показывать выбор контрагента,
  - делать его обязательным,
  - скрывать выбор кредитной карты.
- При сохранении устанавливать `DebtStatus = Active`.

### 5.6. `Views/TransactionEditView.xaml`
- Добавить четвёртую кнопку типа оплаты: **«В долг»**.
- При выборе показывать `ComboBox` с контрагентами.

### 5.7. `ViewModels/TransactionsViewModel.cs`
- Добавить фильтр по долгам:
  - `IsDebtFilter`
  - фильтр по `DebtStatus` (Active / Repaid / WrittenOff)
  - фильтр по контрагенту.

### 5.8. `ViewModels/DebtReportViewModel.cs` (новый)
- Фильтры: период, направление (нам должны / мы должны / все), статус.
- Свойства: `Items`, `TotalReceivable`, `TotalPayable`, `ActiveReceivable`, `ActivePayable`.
- Команды: `RefreshCommand`, `ExportToPdfCommand`, `ExportToExcelCommand`.
- Двойной клик по долгу — переход к контрагенту или списку операций.

### 5.9. `Views/DebtReportView.xaml` (новый)
Таблица долгов с колонками:
- Дата
- Контрагент (с типом)
- Категория
- Сумма долга
- Погашено
- Остаток
- Статус
- Описание

### 5.10. `ViewModels/DebtRepaymentViewModel.cs` (новый, опционально)
Если погашение делается из экрана долгов через отдельную форму:
- `DebtItem`
- `Amount` (по умолчанию остаток, но можно менять)
- `PaymentType` (`Cash` / `NonCash` / `CreditCard`)
- `Date`
- `Description`
- Команда `RepayCommand` → вызывает `DebtService.RepayAsync`.

### 5.11. `Views/CounterpartiesView.xaml` — кнопка «Погасить»
В контекстном меню или деталях контрагента добавить кнопку **«Погасить долг»**, открывающую `DebtRepaymentView`.

---

## 6. Навигация и DI

### 6.1. `App.xaml.cs`
Регистрация сервисов и VM:
```csharp
services.AddScoped<ICounterpartyService, CounterpartyService>();
services.AddScoped<IDebtService, DebtService>();

services.AddTransient<CounterpartiesViewModel>();
services.AddTransient<CounterpartyEditViewModel>();
services.AddTransient<DebtReportViewModel>();
services.AddTransient<DebtRepaymentViewModel>();
```

### 6.2. `ViewModels/MainWindowViewModel.cs`
Добавить команду навигации:
```csharp
private void NavigateToCounterparties() => _navigationService.NavigateTo<CounterpartiesViewModel>();
```

### 6.3. `Views/MainWindow.xaml`
- Добавить кнопку «Контрагенты» в сайдбар (иконка + tooltip).
- Добавить `DataTemplate` для новых VM:
  ```xml
  <DataTemplate DataType="{x:Type vm:CounterpartiesViewModel}">
      <views:CounterpartiesView/>
  </DataTemplate>
  <DataTemplate DataType="{x:Type vm:DebtReportViewModel}">
      <views:DebtReportView/>
  </DataTemplate>
  ```

---

## 7. Отчёты и дашборд

### 7.1. `Services/ReportService.cs`
- В методах расчёта `TotalIncome` / `TotalExpense` добавить фильтр:
  ```csharp
  .Where(t => t.PaymentType != PaymentType.Debt)
  ```
- В `BalanceReport` добавить поля:
  - `TotalDebtReceivable` — нам должны (активные)
  - `TotalDebtPayable` — мы должны (активные)
- В `GenerateDebtReportAsync` реализовать логику фильтрации и агрегации.

### 7.2. `ViewModels/Reports/BalanceReportViewModel.cs`
Добавить отображение `TotalDebtReceivable` / `TotalDebtPayable`.

### 7.3. `ViewModels/DashboardViewModel.cs`
- В прибыль за период долги не включаются (автоматически через `ReportService`).
- Добавить небольшой виджет/карточку:
  - «Нам должны: X ₽»
  - «Мы должны: Y ₽»
  - По клику — переход в отчёт по долгам.

### 7.4. `Services/ExportService.cs`
- PDF/Excel для `DebtReport` с таблицей долгов и итогами.

---

## 8. Конвертеры и ресурсы

### 8.1. `Helpers/Converters/`
- Обновить `PaymentTypeToTextConverter` — добавить «В долг».
- Обновить `PaymentTypeToIconConverter` — добавить иконку для долга.
- Добавить `DebtStatusToTextConverter` / `DebtStatusToColorConverter`.
- Добавить `CounterpartyTypeToTextConverter`.

### 8.2. `App.xaml`
- Зарегистрировать новые конвертеры в ресурсах.

---

## 9. Тестирование

### 9.1. `AutoKassa.Tests/Services/CounterpartyServiceTests.cs` (новый)
- CRUD контрагентов
- Запрет удаления при наличии операций
- Проверка уникальности имени

### 9.2. `AutoKassa.Tests/Services/DebtServiceTests.cs` (новый)
- Создание долговой операции
- Полное погашение долга
- Частичное погашение и пересчёт остатка
- Запрет погашения суммой больше остатка
- Списание долга
- Soft delete погашения → долг снова Active

### 9.3. `AutoKassa.Tests/Services/ReportServiceTests.cs`
- Проверить, что долговые операции **не попадают** в `TotalIncome` / `TotalExpense`.
- Проверить, что погашения **попадают** в доходы/расходы.

---

## 10. Порядок реализации (пошагово)

| # | Задача | Файлы |
|---|--------|-------|
| 1 | [x] Добавить enum'ы (`PaymentType`, `DebtStatus`, `CounterpartyType`) | `Models/Enums/*.cs` |
| 2 | [x] Создать модели `Counterparty`, `DebtPayment`, расширить `Transaction` | `Models/*.cs` |
| 3 | [x] Обновить `AppDbContext` и создать миграцию | `Services/AppDbContext.cs`, `Migrations/` |
| 4 | [x] Реализовать `CounterpartyService` + интерфейс | `Services/ICounterpartyService.cs`, `Services/CounterpartyService.cs` |
| 5 | [x] Реализовать `DebtService` + интерфейс + DTO | `Services/IDebtService.cs`, `Services/DebtService.cs`, `Models/Reports/DebtItem.cs` |
| 6 | [x] Обновить `TransactionService` для работы с долгами | `Services/TransactionService.cs` |
| 7 | [x] Обновить `ReportService` — исключить долги из прибыли, добавить `DebtReport` | `Services/IReportService.cs`, `Services/ReportService.cs`, `Models/Reports/DebtReport.cs` |
| 8 | [x] Обновить `ExportService` для экспорта отчёта по долгам | `Services/ExportService.cs`, `IExportService.cs` |
| 9 | [x] Создать VM и View для контрагентов | `ViewModels/Counterparty*.cs`, `Views/Counterparty*.xaml` |
| 10 | [x] Создать VM и View для отчёта по долгам | `ViewModels/DebtReportViewModel.cs`, `Views/DebtReportView.xaml` |
| 11 | [x] Создать VM/View для погашения долга | `ViewModels/DebtRepaymentViewModel.cs`, `Views/DebtRepaymentView.xaml` |
| 12 | [x] Обновить форму операции (`TransactionEditView`) | `ViewModels/TransactionEditViewModel.cs`, `Views/TransactionEditView.xaml` |
| 13 | [x] Обновить фильтры в списке операций | `ViewModels/TransactionsViewModel.cs`, `Views/TransactionsView.xaml` |
| 14 | [x] Обновить дашборд — виджет долгов | `ViewModels/DashboardViewModel.cs`, `Views/DashboardView.xaml` |
| 15 | [x] Добавить навигацию и DI | `App.xaml.cs`, `ViewModels/MainWindowViewModel.cs`, `Views/MainWindow.xaml` |
| 16 | [x] Добавить конвертеры | `Helpers/Converters/*.cs`, `App.xaml` |
| 17 | [x] Написать тесты | `AutoKassa.Tests/Services/*Tests.cs` |
| 18 | [ ] Проверить миграцию на существующей БД | `AutoKassa.db` |

---

## 11. Важные проверки перед релизом

- [ ] Существующая БД мигрируется без ошибок.
- [x] Старые операции открываются и сохраняются корректно (юнит-тесты + обратная совместимость `DebtStatus.NotDebt`).
- [x] Долговая операция не влияет на баланс кассы (проверено тестами `ReportServiceTests`).
- [x] Погашение влияет на баланс кассы (погашение — обычная операция с `Cash`/`NonCash`/`CreditCard`).
- [x] Долговая операция не попадает в прибыль (проверено тестами).
- [x] Погашение попадает в прибыль.
- [x] Частичное погашение корректно пересчитывает остаток (проверено в `DebtServiceTests`).
- [x] Soft delete погашения возвращает долг в статус `Active` (проверено в `DebtServiceTests`).
- [x] Удаление контрагента с операциями запрещено (проверено в `CounterpartyServiceTests`).
- [x] Отчёт по долгам экспортируется в PDF/Excel (методы добавлены в `ExportService`).

---

## 12. Примечания

- Все публичные методы новых сервисов должны иметь XML-комментарии на русском языке (согласно `AGENTS.md`).
- Все операции с БД — асинхронные (`async`/`await`).
- Сервисы используют `IDbContextFactory<AppDbContext>`.
- UI-стили должны соответствовать существующей дизайн-системе (`App.xaml`).
- После реализации обновить этот файл: отметить выполненные пункты и зафиксировать принятые решения.
- **Принятые решения:**
  - В `IDebtService.GetDebtsAsync` и `IReportService.GenerateDebtReportAsync` для направления используется `OperationType?` вместо отдельного `DebtDirection`, так как направление долга однозначно определяется типом операции (`Income` / `Expense`).
  - `CounterpartyService` и `DebtService` зарегистрированы как `Transient` (вместо `Scoped` в черновике), как и остальные сервисы проекта.
  - Сборка и тесты: `dotnet build` — 0 ошибок, `dotnet test` — 143/143 пройдено.
