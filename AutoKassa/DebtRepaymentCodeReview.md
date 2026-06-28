# Code Review: функционал погашения долга

> Дата ревью: 2026-06-28  
> Область: `Views/DebtRepaymentView.xaml.cs`, `ViewModels/DebtRepaymentViewModel.cs`, `Services/DebtService.cs`, связанные View/VM/Models/Migrations.

## Общая оценка

Сервисный слой (`DebtService`) и юнит-тесты к нему реализованы аккуратно и покрывают базовые сценарии. Однако **функционал погашения долга фактически недоступен пользователю**: форма `DebtRepaymentView`/`DebtRepaymentViewModel` создана, но не интегрирована в навигацию и модальные окна приложения. Кроме того, есть серьёзные проблемы с целостностью данных при удалении/редактировании долговых операций и их погашений, лишние обращения к БД и риски вызова UI-кода из не-UI потока.

Сборка (`dotnet build ../AutoKassa.sln`) успешна, тесты `DebtServiceTests` проходят (10/10), но это не отменяет перечисленных ниже архитектурных и функциональных недостатков.

---

## 1. Критические проблемы (блокируют функционал)

### 1.1. `DebtRepaymentViewModel` нигде не создаётся и не отображается
- `DebtRepaymentViewModel` зарегистрирован в DI (`App.xaml.cs:196`), но **ни в одном ViewModel не происходит его инстанцирование**.
- В `DebtReportViewModel` отсутствует команда «Погасить».
- В `CounterpartiesViewModel` / `Views/CounterpartiesView.xaml` отсутствует кнопка «Погасить долг», хотя это было указано в `DebtSystemImplementationPlan.md` (п. 5.11).
- В `MainWindow.xaml` отсутствует `DataTemplate` для `DebtRepaymentViewModel`.

**Последствие:** пользователь не может открыть форму погашения.

### 1.2. Конструктор `DebtRepaymentViewModel` несовместим с DI
```csharp
// ViewModels/DebtRepaymentViewModel.cs:29
public DebtRepaymentViewModel(
    IDebtService debtService,
    IDialogService dialogService,
    IToastNotificationService toastService,
    DebtItem debt)
```
`DebtItem` — runtime-параметр, но VM регистрируется как `Transient` без фабрики. DI-контейнер не сможет создать экземпляр. В проекте аналогичные VM (`TransactionEditViewModel`) создаются вручную внутри родительских VM.

### 1.3. `ReportsView.xaml` жёстко заточен под `TransactionEditView`
```xml
<!-- Views/ReportsView.xaml:166 -->
<views:TransactionEditView DataContext="{Binding CurrentReport.EditViewModel}"/>
```
Даже если добавить в `DebtReportViewModel` логику открытия формы погашения, `ReportsView.xaml` не умеет отображать `DebtRepaymentView`. Необходим универсальный модальный контейнер или отдельный оверлей.

---

## 2. Архитектурные проблемы

### 2.1. Смешение модальных диалогов разных типов в `BaseReportViewModel`
`BaseReportViewModel` содержит:
```csharp
public bool IsModalOpen { get; set; }
public TransactionEditViewModel EditViewModel { get; set; }
```
Это заточка под редактирование операции. Для погашения долга придётся добавлять ещё одно свойство, увеличивая связность. Рекомендуется вынести модальный контейнер в отдельный `ModalViewModel`/`OverlayService` или сделать универсальное свойство `ModalContent : ViewModelBase`.

### 2.2. `DebtRepaymentViewModel` не соответствует общему паттерну редактирования
У него отсутствуют:
- подписка на `IDataChangeService`;
- `_settingsService`;
- методы `InitializeForEdit` / `InitializeForAdd`;
- оптимистичное обновление UI.

### 2.3. `IDebtService.GetDebtsAsync` не поддерживает пагинацию
```csharp
Task<IReadOnlyList<DebtItem>> GetDebtsAsync(...)
```
При большом количестве долгов список будет неуправляемым.

---

## 3. Проблемы UI / XAML (`DebtRepaymentView.xaml`)

### 3.1. Захардкоженные цвета и фиксированная ширина
```xml
<Grid Width="400" Margin="28">
<TextBlock Foreground="#1A1D23" .../>
<Border Background="#F8F9FB" .../>
```
В проекте есть глобальные ресурсы (`App.xaml`: `BackgroundBrush`, `SurfaceBrush`, `TextPrimaryBrush`). Literal-цвета ломают единообразие и темную тему.

### 3.2. Несоответствие форматов чисел
- В VM: `AmountText = debt.RemainingAmount.ToString(CultureInfo.InvariantCulture);` → `1000.00`
- В XAML: `<Run Text="{Binding Debt.RemainingAmount, StringFormat=N2}"/>` → `1000,00` для `ru-RU`

Пользователь видит одно, а в поле ввода по умолчанию — другое.

### 3.3. `BoolToVisibilityConverter` для строковой ошибки
```xml
<TextBlock Text="{Binding AmountError}"
           Visibility="{Binding AmountError, Converter={StaticResource BoolToVisibilityConverter}}"/>
```
Конвертер умеет обрабатывать `string`, но это side-effect. Лучше завести `bool HasAmountError` или использовать `StringToVisibilityConverter`.

### 3.4. Отсутствие валидации даты
Можно выбрать будущую дату погашения без предупреждения.

### 3.5. Поле суммы не ограничивает ввод
Можно ввести произвольные символы. Парсинг в VM «молча» ставит `_amount = 0`, что вызывает ошибку «Сумма должна быть больше 0», но UX низкий.

---

## 4. Проблемы `DebtRepaymentViewModel`

### 4.1. `ConfigureAwait(false)` + UI-вызовы
```csharp
await _debtService.RepayAsync(...).ConfigureAwait(false);
_toastService.ShowSuccess("Погашение создано");
_dialogService.ShowError($"Ошибка погашения: {ex.Message}");
```
После `ConfigureAwait(false)` продолжение выполняется в thread-pool. `_dialogService` вызывает `MessageBox.Show`, `_toastService` — событие, которое слушает WPF. Возможны `InvalidOperationException` или race condition. Другие VM проекта не используют `ConfigureAwait(false)` при вызовах сервисов.

### 4.2. Нет уведомления об изменении данных
После успешного погашения не вызывается `_dataChangeService.NotifyDataChanged()`. Отчёт по долгам, дашборд и список операций не обновятся автоматически.

### 4.3. `Title` не обновляется при смене `Debt`
```csharp
public string Title => $"Погашение долга: {Debt.CounterpartyName}";
```
При изменении `Debt` не вызывается `OnPropertyChanged(nameof(Title))`.

### 4.4. Парсинг суммы ломает тысячные разделители
```csharp
var normalized = (value ?? "").Replace(',', '.');
```
- `"1,234.56"` → `"1.234.56"` (невалидно)
- `"1 234"` не поддерживается

### 4.5. `AmountError` валидируется только при изменении `AmountText`
Если `Debt.RemainingAmount` изменится извне, ошибка «превышает остаток» не пересчитается.

---

## 5. Проблемы сервисного слоя (`DebtService`)

### 5.1. Лишние round-trip'ы в БД в `RepayAsync`
```csharp
var remaining = await GetRemainingAmountAsync(debtTransactionId, ct).ConfigureAwait(false); // новый контекст
...
await RecalculateStatusAsync(debtTransactionId, ct).ConfigureAwait(false);                  // новый контекст
```
`debt` уже загружен в текущем контексте; остаток и статус можно пересчитать в том же `DbContext`.

### 5.2. Разрешено погашение уже погашенного долга
```csharp
if (debt.DebtStatus != DebtStatus.Active && debt.DebtStatus != DebtStatus.Repaid)
    throw new InvalidOperationException("...");
```
`Repaid` не должен быть разрешён. Дополнительные погашения по закрытому долгу недопустимы.

### 5.3. `WriteOffAsync` не обрабатывает существующие погашения
Можно списать долг с частичными погашениями. Статус станет `WrittenOff`, остаток = 0, но операции-погашения останутся в `Transactions` как обычные доходы/расходы, искажая отчёты.

### 5.4. `GetDebtsAsync` материализует все долговые операции в память
```csharp
// Загружаем все долговые операции в память (их обычно немного)
var debts = await query...ToListAsync(ct);
```
Для отчёта это компромисс, но для списка контрагентов (`CounterpartiesViewModel.LoadAsync`) не масштабируется.

### 5.5. Агрегация `decimal` через `double`
```csharp
.Select(g => new { DebtId = g.Key, Total = (decimal)g.Sum(dp => (double)dp.Amount) })
```
Повторяющийся паттерн в проекте, теряющий точность decimal.

---

## 6. Проблемы целостности данных (`TransactionService`)

### 6.1. Удаление долга не удаляет связанные операции-погашения
```csharp
// Services/TransactionService.cs:264-278
if (transaction.PaymentType == PaymentType.Debt)
{
    var debtPayments = await context.DebtPayments
        .Where(dp => dp.DebtTransactionId == id)
        .ToListAsync();
    if (debtPayments.Count > 0)
        context.DebtPayments.RemoveRange(debtPayments);
}
```
Удаляются только записи `DebtPayment`, сами `Transaction`-погашения остаются — получаются «висячие» операции.

### 6.2. Удаление погашения не удаляет запись `DebtPayment`
```csharp
// Services/TransactionService.cs:280-287
else
{
    var debtPayment = await context.DebtPayments
        .FirstOrDefaultAsync(dp => dp.RepaymentTransactionId == id);
    relatedDebtId = debtPayment?.DebtTransactionId;
}
```
Soft delete операции-погашения скрывает её из расчётов, но запись `DebtPayment` остаётся мусором в БД.

### 6.3. Редактирование погашения/долга не контролирует связи
В `TransactionEditViewModel` можно изменить сумму или тип операции-погашения. `DebtPayment.Amount` при этом не синхронизируется.

### 6.4. Смена типа операции с `Debt` на обычную не чистит `DebtPayment`
```csharp
// Services/TransactionService.cs:217-221
else
{
    existing.DebtStatus = DebtStatus.NotDebt;
    existing.CounterpartyId = null;
}
```
Если у операции были погашения, записи `DebtPayment` остаются.

---

## 7. Проблемы отчётности и DTO

### 7.1. `DebtReport.TotalReceivable` / `TotalPayable` учитывают списанные долги
```csharp
// Services/ReportService.cs:396-397
TotalReceivable = items.Where(i => i.Direction == OperationType.Income).Sum(i => i.Amount),
TotalPayable = items.Where(i => i.Direction == OperationType.Expense).Sum(i => i.Amount),
```
Нет фильтра по `Status`. Возможно, списанные долги не должны входить в эти итоги.

### 7.2. `DebtItem.CounterpartyId = 0` по умолчанию
```csharp
// Models/Reports/DebtItem.cs:49 (заполняется в DebtService.cs:84)
CounterpartyId = t.CounterpartyId ?? 0,
```
`0` — валидное значение int, но невалидный ID. Лучше `int?`.

### 7.3. `DebtReportViewModel.NavigateToCounterpartyCommand` не передаёт контрагента
```csharp
private void NavigateToCounterparty(DebtItem? item)
{
    if (item == null) return;
    _navigationService.NavigateTo<CounterpartiesViewModel>();
}
```
Переход просто открывает справочник, а не фильтрует/выделяет нужного контрагента.

### 7.4. В `DebtReportView.xaml` нет действий по строке
Нет кнопок «Погасить» / «Списать» / «Открыть операции». Отчёт по долгам — логичное место для запуска погашения.

---

# План исправления

## Этап 1. Критический функционал (must have)

| # | Задача | Файлы | Примечания |
|---|--------|-------|------------|
| 1.1 | Добавить в `DebtReportViewModel` команду `RepayDebtCommand` и свойство для модальной формы погашения | `ViewModels/Reports/DebtReportViewModel.cs` | Аналогично `EditViewModel`/`IsModalOpen` из `BaseReportViewModel` |
| 1.2 | Добавить кнопку «Погасить» в `DebtReportView.xaml` | `Views/Reports/DebtReportView.xaml` | В строке DataGrid или в контекстном меню |
| 1.3 | Добавить универсальный модальный оверлей в `ReportsView.xaml` | `Views/ReportsView.xaml` | `ContentControl Content="{Binding CurrentReport.ModalViewModel}"` + `DataTemplate` для `DebtRepaymentViewModel` |
| 1.4 | Исключить `DebtRepaymentViewModel` из DI и создавать вручную | `App.xaml.cs`, `ViewModels/Reports/DebtReportViewModel.cs` | Передать `DebtItem`, сервисы, `IDataChangeService` через конструктор при ручном создании |
| 1.5 | Добавить `DataTemplate` для `DebtRepaymentViewModel` | `Views/ReportsView.xaml` или `MainWindow.xaml` | В зависимости от выбранного способа отображения |

## Этап 2. Исправление `DebtRepaymentViewModel`

| # | Задача | Файлы |
|---|--------|-------|
| 2.1 | Убрать `ConfigureAwait(false)` в `RepayAsync` или маршалить UI-вызовы через `Dispatcher` | `ViewModels/DebtRepaymentViewModel.cs` |
| 2.2 | Добавить `IDataChangeService` и вызывать `NotifyDataChanged()` после успешного погашения | `ViewModels/DebtRepaymentViewModel.cs` |
| 2.3 | Исправить форматирование `AmountText` в соответствии с текущей культурой | `ViewModels/DebtRepaymentViewModel.cs` |
| 2.4 | Улучшить парсинг суммы: не ломать тысячные разделители, явно обрабатывать `CultureInfo.CurrentCulture` | `ViewModels/DebtRepaymentViewModel.cs` |
| 2.5 | Добавить валидацию даты (запрет будущих дат) | `ViewModels/DebtRepaymentViewModel.cs` |
| 2.6 | Добавить `OnPropertyChanged(nameof(Title))` при изменении `Debt` | `ViewModels/DebtRepaymentViewModel.cs` |
| 2.7 | Заменить `Action? OnClosed` на типизированное событие или `TaskCompletionSource` | `ViewModels/DebtRepaymentViewModel.cs` |

## Этап 3. Исправление `DebtService` ✅

| # | Задача | Файлы |
|---|--------|-------|
| 3.1 | Запретить погашение, если статус `Repaid` или `WrittenOff` | `Services/DebtService.cs` |
| 3.2 | Пересчитывать статус долга в том же `DbContext`, не вызывая `RecalculateStatusAsync` с новым контекстом | `Services/DebtService.cs` |
| 3.3 | Не вызывать `GetRemainingAmountAsync` отдельным контекстом в `RepayAsync` | `Services/DebtService.cs` |
| 3.4 | В `WriteOffAsync` запретить списание, если есть неучтённые погашения, или явно обработать их | `Services/DebtService.cs` |
| 3.5 | Добавить пагинацию в `GetDebtsAsync` (или отдельный метод для отчётов) | `Services/IDebtService.cs`, `Services/DebtService.cs` |

## Этап 4. Целостность данных в `TransactionService` ✅

| # | Задача | Файлы |
|---|--------|-------|
| 4.1 | При soft delete долга делать soft delete связанных операций-погашений | `Services/TransactionService.cs` |
| 4.2 | При soft delete погашения удалять запись `DebtPayment` | `Services/TransactionService.cs` |
| 4.3 | При смене типа операции с `Debt` на другой удалять записи `DebtPayment` | `Services/TransactionService.cs` |
| 4.4 | Запретить редактирование операции, являющейся погашением, или синхронизировать `DebtPayment.Amount` | `ViewModels/TransactionEditViewModel.cs`, `Services/TransactionService.cs` |

## Этап 5. UI / XAML ✅

| # | Задача | Файлы |
|---|--------|-------|
| 5.1 | Заменить literal-цвета на ресурсы из `App.xaml` | `Views/DebtRepaymentView.xaml` |
| 5.2 | Убрать фиксированную `Width="400"` или сделать адаптивную ширину | `Views/DebtRepaymentView.xaml` |
| 5.3 | Использовать `StringToVisibilityConverter` или `HasAmountError` для видимости ошибки | `Views/DebtRepaymentView.xaml`, `ViewModels/DebtRepaymentViewModel.cs` |
| 5.4 | Добавить ограничение ввода в поле суммы | `Views/DebtRepaymentView.xaml` |

## Этап 6. Отчётность ✅

| # | Задача | Файлы |
|---|--------|-------|
| 6.1 | Уточнить, должны ли списанные долги входить в `TotalReceivable`/`TotalPayable`; при необходимости добавить фильтр | `Services/ReportService.cs` |
| 6.2 | Сделать `DebtItem.CounterpartyId` nullable | `Models/Reports/DebtItem.cs` |
| 6.3 | Передавать `counterpartyId` в `NavigateToCounterpartyCommand` | `ViewModels/Reports/DebtReportViewModel.cs`, `Services/NavigationService.cs` |

## Этап 7. Архитектура ✅

| # | Задача | Файлы |
|---|--------|-------|
| 7.1 | Вынести модальный контейнер из `BaseReportViewModel` в отдельный `ModalViewModel` или сделать универсальное свойство `ModalContent` | `ViewModels/Reports/BaseReportViewModel.cs`, `Views/ReportsView.xaml` |
| 7.2 | Привести `DebtRepaymentViewModel` к общему паттерну редактирования (Initialize, IDataChangeService, оптимистичное обновление) | `ViewModels/DebtRepaymentViewModel.cs` |

## Этап 8. Тестирование

| # | Задача | Файлы |
|---|--------|-------|
| 8.1 | Удаление долга с погашениями: проверить soft delete связанных операций | `AutoKassa.Tests/Services/DebtServiceTests.cs`, `AutoKassa.Tests/Services/TransactionServiceTests.cs` |
| 8.2 | Редактирование операции-погашения: проверить запрет или синхронизацию суммы | `AutoKassa.Tests/Services/TransactionServiceTests.cs` |
| 8.3 | Списание частично погашенного долга | `AutoKassa.Tests/Services/DebtServiceTests.cs` |
| 8.4 | Попытка повторного погашения уже погашенного долга | `AutoKassa.Tests/Services/DebtServiceTests.cs` |
| 8.5 | Проверить, что погашение влияет на баланс, а долг — нет | `AutoKassa.Tests/Services/ReportServiceTests.cs` |

---

## Рекомендуемый порядок работы

1. **Сначала починить критический путь**: открытие формы погашения из отчёта по долгам.
2. **Параллельно исправить `DebtService`**: запрет погашения Repaid/WrittenOff, устранение лишних контекстов.
3. **Затем целостность данных**: корректное удаление долгов и погашений.
4. **В конце — UI-причёсывание и тесты**.

> **Примечание:** перед началом работы стоит обновить уязвимый NuGet-пакет `SQLitePCLRaw.lib.e_sqlite3` (предупреждение NU1903 при сборке).
