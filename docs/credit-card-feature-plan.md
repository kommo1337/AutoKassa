# План реализации: учёт кредитных карт в AutoKassa

> Статус: реализовано  
> Решения приняты в диалоге с пользователем  
> Обратная совместимость: обязательна

---

## 1. Цель

Добавить в AutoKassa возможность учитывать закупки автосервиса, оплаченные кредитной картой, с отслеживанием:

- кредитного лимита и доступного остатка;
- текущего общего долга по карте;
- ближайшей даты и суммы минимального платежа;
- истории кредитных покупок и погашений.

Существующие операции по наличным (`Cash`) и безналичным (`NonCash`) средствам не должны измениться.

---

## 2. Архитектурные решения

| Параметр | Решение |
|----------|---------|
| Способ выделения кредита | Новое значение в enum `PaymentType.CreditCard = 3` |
| Учёт в отчётах | Гибридный: покупки учитываются в расходах/категориях сразу; баланс разделяет фактические деньги и кредитные обязательства |
| Погашение долга | Расходная операция с системной категорией «Погашение кредита» и выбором карты |
| Уровень детализации долга | Общий долг по карте (не по каждой покупке) |
| Начальные данные | В настройках задаётся лимит и уже израсходованная сумма / текущий долг |
| Название старого типа | «Безналичные» оставляем без изменений |
| Минимальный платёж | Рассчитывается как настраиваемый процент от текущего долга карты |
| UI | Новый экран «Кредитные карты» в сайдбаре + виджеты на дашборде + настройки |

---

## 3. Модели данных

### 3.1. `PaymentType` (изменение)

```csharp
public enum PaymentType
{
    /// <summary> Наличные </summary>
    Cash = 1,

    /// <summary> Безналичные </summary>
    NonCash = 2,

    /// <summary> Кредитная карта </summary>
    CreditCard = 3
}
```

### 3.2. `CreditCard` (новая сущность)

| Поле | Тип | Описание |
|------|-----|----------|
| `Id` | `int` | PK |
| `Name` | `string` | Пользовательское название карты |
| `BankName` | `string?` | Банк-эмитент |
| `Limit` | `decimal` | Кредитный лимит |
| `InterestRate` | `decimal?` | Годовая процентная ставка, % |
| `StatementDay` | `int?` | День выписки (1–31) |
| `PaymentDay` | `int?` | День платежа (1–31) |
| `LastPaymentDate` | `DateTime?` | Дата последнего платежа (для расчёта следующего) |
| `MinimumPaymentPercent` | `decimal` | Процент от долга для мин. платежа |
| `InitialDebt` | `decimal` | Начальный долг, введённый в настройках |
| `IsActive` | `bool` | Активна ли карта |
| `CreatedAt` | `DateTime` | Дата создания |

### 3.3. `CreditCardPurchase` (новая сущность)

| Поле | Тип | Описание |
|------|-----|----------|
| `Id` | `int` | PK |
| `CreditCardId` | `int` | FK → `CreditCard` |
| `TransactionId` | `int` | FK → `Transaction` |
| `Amount` | `decimal` | Сумма покупки |
| `RemainingDebt` | `decimal` | Оставшийся долг по этой покупке |
| `PurchaseDate` | `DateTime` | Дата покупки |
| `Notes` | `string?` | Примечание |

### 3.4. `Transaction` (изменение)

Добавить nullable FK:

```csharp
public int? CreditCardId { get; set; }
public CreditCard? CreditCard { get; set; }
```

### 3.5. `Category` (seed-данные)

Добавить системную категорию расходов:

- `Name = "Погашение кредита"`
- `Type = OperationType.Expense`
- `IsSystem = true`
- `IsActive = true`

---

## 4. База данных и миграции

### 4.1. Миграция EF Core

Создать новую миграцию, например `AddCreditCardSupport`:

1. Создать таблицу `CreditCards`.
2. Создать таблицу `CreditCardPurchases`.
3. Добавить в `Transactions` столбец `CreditCardId` (nullable, FK).
4. Добавить индекс `IX_Transactions_CreditCardId`.
5. Добавить системную категорию «Погашение кредита» через `migrationBuilder.InsertData`.

> ✅ Выполнено: также создана миграция `FixCreditCardPendingModel`, добавляющая в `AppSettings` столбцы кредитной карты (`CreditCardLimit`, `CreditCardCurrentDebt`, `CreditCardInterestRate`, `CreditCardPaymentDay`, `CreditCardLastPaymentDate`, `CreditCardMinimumPaymentPercent`).

### 4.2. Обратная совместимость

- Старые транзакции имеют `PaymentType = 1` или `2`, `CreditCardId = NULL` — это валидно.
- Существующие миграции и snapshot не изменяются ретроспективно.
- Значения enum не удаляются и не переупорядочиваются.
- `DefaultPaymentType` в `AppSettings` остаётся `int`; значение `3` добавляется только в UI-коллекции.

---

## 5. Сервисы

### 5.1. `ICreditCardService` / `CreditCardService`

```csharp
public interface ICreditCardService
{
    Task<IReadOnlyList<CreditCard>> GetAllAsync(CancellationToken ct = default);
    Task<CreditCard?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<CreditCard> CreateAsync(CreditCard card, CancellationToken ct = default);
    Task UpdateAsync(CreditCard card, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<decimal> GetCurrentDebtAsync(int creditCardId, CancellationToken ct = default);
    Task<decimal> GetAvailableLimitAsync(int creditCardId, CancellationToken ct = default);
    Task<decimal> GetMinimumPaymentAsync(int creditCardId, CancellationToken ct = default);
    Task<DateTime?> GetNextPaymentDateAsync(int creditCardId, CancellationToken ct = default);

    Task AddPurchaseAsync(int creditCardId, int transactionId, decimal amount, CancellationToken ct = default);
    Task RepayDebtAsync(int creditCardId, decimal amount, CancellationToken ct = default);
}
```

### 5.2. `TransactionService`

- При `CreateAsync`/`UpdateAsync`:
  - если `PaymentType == CreditCard` и `CreditCardId != null` — создать `CreditCardPurchase`;
  - если категория операции — «Погашение кредита» и `CreditCardId != null` — вызвать `CreditCardService.RepayDebtAsync`;
  - при смене типа с `CreditCard` на другой — удалить связанную `CreditCardPurchase` (восстановить долг).

### 5.3. `ReportService`

- **Балансовый отчёт**: показывать три блока:
  - фактические деньги (наличные + безналичные);
  - кредитные обязательства (текущий долг);
  - условный баланс = фактические деньги − кредитный долг.
- **Отчёт по категориям**: кредитные покупки учитываются в своих категориях расходов.
- **Детализация операций**: фильтр «Кредит» работает как отдельный тип оплаты.

### 5.4. `SettingsService`

- Добавить в настройки начальные параметры кредитной карты (лимит, текущий долг, ставка, день платежа, процент мин. платежа).
- Обновить экспорт/импорт JSON.

---

## 6. ViewModels

### 6.1. Новые

- `CreditCardsViewModel` — список карт, быстрые показатели.
- `CreditCardEditViewModel` — создание/редактирование карты.
- `CreditCardPurchaseViewModel` — элемент истории покупки.

### 6.2. Изменения

| ViewModel | Изменения |
|-----------|-----------|
| `TransactionEditViewModel` | ✅ Добавить `AvailableCreditCards`, `SelectedCreditCard`, видимость выбора при `PaymentType.CreditCard` |
| `TransactionsViewModel` | ✅ Добавить фильтр «Кредит», обновить чипы фильтров |
| `SelectableTransaction` | ✅ Добавить `InlineCreditCard`, команды выбора кредита |
| `DashboardViewModel` | ✅ Добавить виджеты: лимит/использовано/доступно, долг, ближайший платёж |
| `SettingsViewModel` | ✅ Добавить секцию «Кредитная карта» |
| `BalanceReportViewModel` | ✅ Добавить фильтр «Кредит», показать кредитные обязательства |
| `CategoryReportViewModel` | ✅ Добавить фильтр «Кредит» |
| `TransactionDetailReportViewModel` | ✅ Добавить фильтр «Кредит» |
| `MainWindowViewModel` | ✅ Добавить команду навигации к экрану карт |

---

## 7. Views (XAML)

### 7.1. Новые

- ✅ `Views/CreditCardsView.xaml` — список карт, карточки с метриками, история покупок.
- ✅ `Views/CreditCardEditView.xaml` — форма создания/редактирования карты.

### 7.2. Изменения

| View | Изменения |
|------|-----------|
| `TransactionEditView.xaml` | ✅ Добавить `ComboBox` выбора карты, видимый только при `CreditCard`; добавить кнопку «Кредит» рядом с наличными/безналичными |
| `TransactionsView.xaml` | ✅ Добавить фильтр «Кредит», иконку кредитной карты в списке |
| `DashboardView.xaml` | ✅ Добавить виджет кредитной карты (лимит/использовано/доступно, долг, ближайший платёж) |
| `SettingsView.xaml` | ✅ Добавить поля лимита, текущего долга, ставки, дня платежа, процента мин. платежа |
| `Reports/BalanceReportView.xaml` | ✅ Добавить фильтр «Кредит», блок кредитных обязательств |
| `Reports/CategoryReportView.xaml` | ✅ Добавить фильтр «Кредит» |
| `Reports/TransactionDetailReportView.xaml` | ✅ Добавить фильтр «Кредит» |
| `MainWindow.xaml` | ✅ Добавить `DataTemplate` для `CreditCardsViewModel` и кнопку в сайдбар |

### 7.3. Конвертеры

- ✅ `PaymentTypeToTextConverter` — добавить `CreditCard → "Кредит"`.
- ✅ `PaymentTypeToIconConverter` — добавить иконку кредитной карты.

---

## 8. DI и навигация

В `App.xaml.cs` → `ConfigureServices`:

```csharp
services.AddScoped<ICreditCardService, CreditCardService>();
services.AddTransient<CreditCardsViewModel>();
services.AddTransient<CreditCardEditViewModel>();
```

В `MainWindow.xaml` добавить `DataTemplate` для `CreditCardsViewModel`.

В `MainWindowViewModel` добавить команду `NavigateToCreditCardsCommand`.

---

## 9. Логика погашения долга

1. Пользователь создаёт расходную операцию.
2. Выбирает категорию «Погашение кредита».
3. Выбирает способ оплаты (`Cash` или `NonCash`) — это реальные деньги, уходящие банку.
4. Выбирает кредитную карту из списка.
5. `TransactionService` при сохранении:
   - создаёт операцию-расход;
   - вызывает `CreditCardService.RepayDebtAsync(creditCardId, amount)`;
   - сервис уменьшает текущий долг карты.

### Расчёт текущего долга

```
CurrentDebt = InitialDebt
            + SUM(CreditCardPurchase.Amount)
            − SUM(Repayments)
```

### Расчёт минимального платежа

```
MinimumPayment = CurrentDebt * (CreditCard.MinimumPaymentPercent / 100)
```

### Расчёт ближайшей даты платежа

1. Определяем опорную дату: если `LastPaymentDate` задана — следующий месяц после неё, иначе текущий месяц.
2. Формируем дату платежа как `PaymentDay` в опорном месяце.
3. Если эта дата уже прошла — переносим на следующий месяц.
4. Обновляем `LastPaymentDate` при каждом успешном погашении долга.

---

## 10. Отчётность

### 10.1. Дашборд

- **Виджет лимита**: лимит / использовано / доступно.
- **Виджет долга**: текущий общий долг по всем активным картам.
- **Виджет платежа**: ближайшая дата и сумма минимального платежа.

### 10.2. Балансовый отчёт

- Доходы / расходы по фактическим деньгам.
- Отдельная строка «Кредитные покупки» (если нужно).
- Итоговый «чистый баланс» с учётом кредитного долга.

### 10.3. Отчёт по категориям

- Кредитные покупки учитываются в своих категориях.
- Фильтр «Кредит» позволяет посмотреть только кредитные траты.

### 10.4. Детализация операций

- Колонка «Тип оплаты» показывает иконку и текст «Кредит».
- Фильтр по типу оплаты включает «Кредит».

---

## 11. Настройки

### 11.1. Параметры кредитной карты в `AppSettings`

Добавить поля:

- `CreditCardLimit`
- `CreditCardCurrentDebt`
- `CreditCardInterestRate`
- `CreditCardPaymentDay`
- `CreditCardLastPaymentDate`
- `CreditCardMinimumPaymentPercent`

### 11.2. Поведение

- При первом включении функции пользователь заполняет эти поля.
- При сохранении настроек создаётся или обновляется запись в `CreditCards` (Id = 1, Name = "Основная кредитная карта").
- В будущем, при переходе на несколько карт, настройки можно вынести в отдельный экран.

---

## 12. Тестирование

### 12.1. Новые тесты

- `CreditCardServiceTests`:
  - `CreateAsync_AddsCard`
  - `GetCurrentDebtAsync_AfterPurchase_Increases`
  - `GetCurrentDebtAsync_AfterRepayment_Decreases`
  - `GetAvailableLimitAsync_RespectsLimitAndDebt`
  - `GetMinimumPaymentAsync_CalculatesCorrectly`
  - `RepayDebtAsync_CannotExceedDebt`

### 12.2. Обновление существующих тестов

- `TransactionServiceTests`:
  - добавить тест создания операции с `PaymentType.CreditCard`;
  - добавить тест погашения долга через категорию «Погашение кредита».
- `ReportServiceTests`:
  - обновить тесты фильтрации по `PaymentType` с учётом `CreditCard`;
  - добавить тест балансового отчёта с кредитными обязательствами.

---

## 13. Порядок реализации

1. ✅ Модели и enum (`PaymentType`, `CreditCard`, `CreditCardPurchase`, `Transaction`).
2. ✅ EF Core: `AppDbContext`, миграция `AddCreditCardSupport`.
3. ✅ Системная категория «Погашение кредита» (seed).
4. ✅ `ICreditCardService` / `CreditCardService` + юнит-тесты.
5. ✅ Интеграция в `TransactionService` (создание покупки, погашение).
6. ✅ `SettingsService` — начальные параметры карты.
7. ✅ `ReportService` — кредитные обязательства в отчётах.
8. ✅ Конвертеры и базовые UI-элементы (`PaymentTypeToTextConverter`, `PaymentTypeToIconConverter`).
9. ✅ `TransactionEditView` + `TransactionEditViewModel` — выбор кредитной карты при типе оплаты «Кредит».
10. ✅ `TransactionsView` + `TransactionsViewModel` — фильтр «Кредит», иконка карты в списке.
11. ✅ `CreditCardsView` + `CreditCardsViewModel` + `CreditCardEditViewModel` — экран списка карт и редактор.
12. ✅ `DashboardView` + виджеты.
13. ✅ `SettingsView` + секция кредитной карты.
14. ✅ Отчётные экраны — фильтр «Кредит».
15. ✅ DI, навигация, сайдбар (`App.xaml.cs`, `MainWindow.xaml`/`MainWindowViewModel`).
16. ✅ Обновление и запуск тестов (114 тестов проходят).
17. ✅ Ручное тестирование и проверка обратной совместимости (исправлена ошибка запуска из-за отсутствующих столбцов в `AppSettings` — добавлена миграция `FixCreditCardPendingModel`); 114 тестов проходят.

---


