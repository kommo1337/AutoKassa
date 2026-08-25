# 06. Бизнес-логика AutoKassa

> Этот файл описывает ключевые бизнес-правила приложения: операции, soft delete, кредитные карты, долги, категории, контрагенты и отчёты. Машиночитаемые справочники: [`json/models.json`](json/models.json), [`json/services.json`](json/services.json), [`json/flows.json`](json/flows.json).

---

## 1. Общие принципы

### 1.1. Unit of work и DbContext

- Каждый публичный метод сервиса — отдельная единица работы.
- Сервисы получают [`IDbContextFactory<AppDbContext>`](../../AutoKassa/Services/AppDbContext.cs) и создают свой контекст на вызов:
  ```csharp
  await using var context = await _contextFactory.CreateDbContextAsync();
  ```
- Атомарные операции оборачиваются в транзакцию:
  ```csharp
  await using var dbTransaction = await context.Database.BeginTransactionAsync();
  ```

### 1.2. Soft delete

- Удаление операций выполняется только через флаг [`Transaction.IsDeleted`](../../AutoKassa/Models/Transaction.cs).
- Физическое удаление `Transaction` запрещено.
- Большинство запросов фильтрует `Where(t => !t.IsDeleted)`.
- Связанные сущности обрабатываются по-разному:
  - [`CreditCardPurchase`](../../AutoKassa/Models/CreditCardPurchase.cs) — жёстко удаляется при soft delete операции покупки.
  - [`CreditCardPayment`](../../AutoKassa/Models/CreditCardPayment.cs) — удаляются через helper при удалении погашения.
  - [`DebtPayment`](../../AutoKassa/Models/DebtPayment.cs) — записи остаются; помечаются soft delete связанные операции-погашения.

---

## 2. Операции (`Transaction`)

### 2.1. Ключевые файлы

| Файл | Роль |
|---|---|
| [`Transaction.cs`](../../AutoKassa/Models/Transaction.cs) | Сущность операции |
| [`ITransactionService.cs`](../../AutoKassa/Services/ITransactionService.cs) | Интерфейс |
| [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) | CRUD, фильтрация, агрегации, восстановление |
| [`FilterParameters.cs`](../../AutoKassa/Models/FilterParameters.cs) | Параметры фильтрации |

### 2.2. Бизнес-правила при создании

- `Amount` должен быть **> 0**.
- Категория должна существовать и быть активной.
- Если `PaymentType == PaymentType.Debt`, обязателен `CounterpartyId`:
  ```csharp
  if (!transaction.CounterpartyId.HasValue)
      throw new InvalidOperationException("Для долговой операции необходимо выбрать контрагента");
  transaction.DebtStatus = DebtStatus.Active;
  ```
- Если операция не долг — `DebtStatus = DebtStatus.NotDebt`.
- Для `PaymentType.CreditCard` создаётся [`CreditCardPurchase`](../../AutoKassa/Models/CreditCardPurchase.cs) в той же транзакции.
- Операция дохода с привязанной картой (`Type = Income` + `CreditCardId`) — пополнение карты: покупка не создаётся, сумма распределяется по покупкам FIFO и уменьшает долг (см. §3.8).
- Навигационные свойства сбрасываются в `null` перед сохранением, чтобы избежать конфликтов отслеживания.

### 2.3. Бизнес-правила при обновлении

- Нельзя редактировать операцию-погашение (долга или кредитной карты).
- При смене типа оплаты с `Debt` на обычный удаляются связи `DebtPayment`, а `CounterpartyId` сбрасывается.
- При смене категории на/с «Погашение кредита» пересчитываются остатки покупок.
- Изменения по кредитной карте обрабатываются в `HandleCreditCardChangesAsync`.

### 2.4. Soft delete и Restore

```csharp
transaction.IsDeleted = true;
transaction.UpdatedAt = DateTime.Now;
```

Особенности удаления:
- При удалении покупки по кредитке жёстко удаляется `CreditCardPurchase`.
- При удалении погашения кредитки восстанавливаются остатки покупок.
- При удалении долговой операции связанные погашения помечаются `IsDeleted = true`.
- При удалении операции-погашения долга пересчитывается статус связанного долга.

`RestoreAsync` атомарно восстанавливает операцию и все связанные сущности (покупки, погашения, статусы долгов).

### 2.5. Фильтрация

Метод `ApplyFilters` в [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) поддерживает:

| Параметр | Описание |
|---|---|
| `DateFrom` / `DateTo` | Диапазон дат, `DateTo` включает весь день |
| `Type` | `OperationType` |
| `PaymentType` | `PaymentType` |
| `CategoryId` | По категории |
| `CounterpartyId` | По контрагенту |
| `CreditCardId` | По кредитной карте (покупки, пополнения и погашения конкретной карты) |
| `IsDebtFilter` | Только долговые операции |
| `DebtStatus` | Фильтр по статусу долга |
| `AmountFrom` / `AmountTo` | Диапазон суммы |
| `SearchText` | Поиск по описанию |

> ℹ️ Поиск по `SearchText` выполняется через SQLite FTS5 (таблица `TransactionsFts`, токенизатор unicode61): регистронезависимо, в т.ч. для кириллицы, по префиксам слов (см. [TRAPS.md](TRAPS.md) §3.1, §3.4).

### 2.6. SQL-агрегации

- `GetPeriodTotalsAsync` — `GROUP BY Type`, сумма и количество.
- `GetDailyTotalsAsync` — `GROUP BY Date.Date`.
- `GetTopCategoriesAsync` — топ категорий по сумме.

---

## 3. Кредитные карты

### 3.1. Ключевые файлы

| Файл | Роль |
|---|---|
| [`CreditCard.cs`](../../AutoKassa/Models/CreditCard.cs) | Сущность карты |
| [`CreditCardPurchase.cs`](../../AutoKassa/Models/CreditCardPurchase.cs) | Покупка по карте |
| [`CreditCardPayment.cs`](../../AutoKassa/Models/CreditCardPayment.cs) | Связь погашения и покупки |
| [`ICreditCardService.cs`](../../AutoKassa/Services/ICreditCardService.cs) | Интерфейс |
| [`CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs) | CRUD карт, погашения, расчёты |
| [`CreditCardOperationHelper.cs`](../../AutoKassa/Services/CreditCardOperationHelper.cs) | Низкоуровневые операции |

### 3.2. Системная категория погашения

Категория «Погашение кредита» создаётся в `AppDbContext.SeedData` как системная категория расходов (Id = 11).

### 3.3. Валидация карты

```csharp
if (string.IsNullOrWhiteSpace(card.Name))
    throw new ArgumentException("Название карты не может быть пустым", nameof(card));
if (card.Limit < 0)
    throw new ArgumentException("Кредитный лимит не может быть отрицательным", nameof(card));
if (card.InitialDebt < 0)
    throw new ArgumentException("Начальный долг не может быть отрицательным", nameof(card));
if (card.MinimumPaymentPercent < 0 || card.MinimumPaymentPercent > 100)
    throw new ArgumentException("Процент минимального платежа должен быть в диапазоне 0–100", nameof(card));
if (card.StatementDay.HasValue && (card.StatementDay.Value < 1 || card.StatementDay.Value > 31))
    throw new ArgumentException("День выписки должен быть в диапазоне 1–31", nameof(card));
if (card.PaymentDay.HasValue && (card.PaymentDay.Value < 1 || card.PaymentDay.Value > 31))
    throw new ArgumentException("День платежа должен быть в диапазоне 1–31", nameof(card));
```

### 3.4. Удаление карты

Карта не удаляется физически, а деактивируется (`IsActive = false`). Запрещено, если есть непогашенный долг.

### 3.5. Покупка по кредитной карте

- Проверяется лимит: `currentDebt + amount <= card.Limit`.
- `RemainingDebt` изначально равен сумме покупки.
- Покупки нельзя добавлять по неактивной карте.

### 3.6. FIFO-погашение

```csharp
var purchases = await context.CreditCardPurchases
    .Where(p => p.CreditCardId == creditCardId && p.RemainingDebt > 0)
    .OrderBy(p => p.PurchaseDate)
    .ThenBy(p => p.Id)
    .ToListAsync(ct)
    .ConfigureAwait(false);
```

- Покупки сортируются по дате, затем по `Id` — чистый FIFO.
- Погашение возможно только наличными или безналичными.
- Сумма погашения не может превышать текущий долг по карте.
- При погашении обновляется `card.LastPaymentDate`.

### 3.7. Текущий долг и минимальный платёж

```csharp
var debt = card.InitialDebt + purchasesSum - repaymentsSum - replenishmentsSum;
var minimumPayment = debt * (card.MinimumPaymentPercent / 100m);
```

Здесь `replenishmentsSum` — сумма пополнений (операций дохода с привязанной картой), см. §3.8.

### 3.8. Пополнение карты (доход на карту)

- Операция с `Type = Income` и привязанной картой — это пополнение карты (возврат средств, приход на карту), а не покупка.
- Запись `CreditCardPurchase` не создаётся; сумма распределяется по покупкам с остатком долга FIFO через `CreditCardOperationHelper.DistributeRepaymentAsync` (создаются записи `CreditCardPayment`).
- Карта должна быть активной; сумма пополнения не может превышать текущий долг по карте (как и погашение, TRAPS 2.13).
- Пополнение уменьшает долг наравне с погашениями во всех расчётах (`GetCurrentDebtAsync`, `GetCardsMetricsAsync`, `GetTotalDebtAsync`, `GetTotalDebtAsOfAsync`).
- Как и погашение, операция-пополнение не редактируется (TRAPS 2.6): удалить и создать заново. При удалении остатки покупок восстанавливаются, при восстановлении операции распределение пересоздаётся.
- Исторические ошибочные покупки от операций дохода удаляются миграцией `FixIncomeCreditCardPurchases`.

---

## 4. Долги

### 4.1. Ключевые файлы

| Файл | Роль |
|---|---|
| [`DebtStatus.cs`](../../AutoKassa/Models/Enums/DebtStatus.cs) | Статусы долга |
| [`DebtPayment.cs`](../../AutoKassa/Models/DebtPayment.cs) | Связь долг ↔ погашение |
| [`IDebtService.cs`](../../AutoKassa/Services/IDebtService.cs) | Интерфейс |
| [`DebtService.cs`](../../AutoKassa/Services/DebtService.cs) | Реализация |

### 4.2. Направление долга

| Операция | Значение |
|---|---|
| `Income + Debt` | Нам должны (дебиторская задолженность) |
| `Expense + Debt` | Мы должны (кредиторская задолженность) |
| Погашение | Обычная операция того же `Type` с обычным `PaymentType` |

### 4.3. Статусы долга

```csharp
public enum DebtStatus
{
    NotDebt = 0,
    Active = 1,
    Repaid = 2,
    WrittenOff = 3
}
```

### 4.4. Создание долга

Долг создаётся как обычная операция через `TransactionService.AddAsync` с `PaymentType = Debt`. Обязательно указывается контрагент.

### 4.5. Погашение долга

`DebtService.RepayAsync`:
- Сумма > 0.
- `PaymentType` погашения не может быть `Debt`.
- Долг должен быть в статусе `Active`.
- Сумма погашения не превышает остаток.
- Создаётся обычная операция того же `Type`.
- Добавляется запись `DebtPayment`.
- Пересчитывается статус долга: `Repaid` если погашено полностью, иначе `Active`.

### 4.6. Списание долга

`DebtService.WriteOffAsync`:
- Нельзя списать долг, по которому есть погашения.
- Списание не создаёт операции движения денег.
- Статус меняется на `WrittenOff`.

---

## 5. Категории

### 5.1. Ключевые файлы

| Файл | Роль |
|---|---|
| [`Category.cs`](../../AutoKassa/Models/Category.cs) | Сущность категории |
| [`ICategoryService.cs`](../../AutoKassa/Services/ICategoryService.cs) | Интерфейс |
| [`CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs) | Реализация |

### 5.2. Бизнес-правила

- Название не может быть пустым.
- Название уникально в рамках типа (`OperationType`).
- Проверка уникности: exact match в БД + регистронезависимая проверка в памяти для Unicode.
- Нельзя удалить категорию, установленную по умолчанию в настройках.
- Нельзя удалить категорию с привязанными операциями (включая удалённые).
- Системные категории создаются через seed data.

### 5.3. Seed-категории

В [`AppDbContext.SeedData`](../../AutoKassa/Services/AppDbContext.cs) создаётся 11 системных категорий:

- **Доходы:** Ремонт авто, Ремонт мото, Диагностика, Прочие доходы.
- **Расходы:** Запчасти, Зарплата, Аренда, Коммунальные услуги, Маркетинг, Прочие расходы, Погашение кредита.

### 5.4. Сортировка

По умолчанию: `.OrderBy(c => c.Type).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)`.
`ReorderAsync` обновляет `SortOrder` для переданных категорий.

---

## 6. Контрагенты

### 6.1. Ключевые файлы

| Файл | Роль |
|---|---|
| [`Counterparty.cs`](../../AutoKassa/Models/Counterparty.cs) | Сущность контрагента |
| [`CounterpartyType.cs`](../../AutoKassa/Models/Enums/CounterpartyType.cs) | Типы контрагентов |
| [`ICounterpartyService.cs`](../../AutoKassa/Services/ICounterpartyService.cs) | Интерфейс |
| [`CounterpartyService.cs`](../../AutoKassa/Services/CounterpartyService.cs) | Реализация |

### 6.2. Типы контрагентов

```csharp
public enum CounterpartyType
{
    Client = 1,
    Branch = 2,
    Supplier = 3,
    Other = 4
}
```

### 6.3. Бизнес-правила

- Название не может быть пустым.
- Название уникально регистронезависимо (`TEXT COLLATE NOCASE`).
- Проверка уникности: exact match + fallback в памяти.
- Нельзя удалить контрагента, у которого есть связанные операции.

---

## 7. Отчёты, баланс и сверка кассы

### 7.1. Ключевые файлы

| Файл | Роль |
|---|---|
| [`IReportService.cs`](../../AutoKassa/Services/IReportService.cs) | Интерфейс |
| [`ReportService.cs`](../../AutoKassa/Services/ReportService.cs) | Реализация |
| [`BalanceReport.cs`](../../AutoKassa/Models/Reports/BalanceReport.cs) | Отчёт «Баланс» |
| [`CategoryReport.cs`](../../AutoKassa/Models/Reports/CategoryReport.cs) | Отчёт «Структура по категориям» |
| [`TransactionDetailReport.cs`](../../AutoKassa/Models/Reports/TransactionDetailReport.cs) | Детализация операций |
| [`DebtReport.cs`](../../AutoKassa/Models/Reports/DebtReport.cs) | Отчёт по долгам |
| [`ReconciliationData.cs`](../../AutoKassa/Models/Reports/ReconciliationData.cs) | Данные сверки кассы |

### 7.2. Баланс за период

```csharp
report.StartBalance = await GetInitialBalanceAsync(dateFrom, paymentType, ct);
report.TotalIncome  = totals.FirstOrDefault(r => r.Type == OperationType.Income)?.Total  ?? 0m;
report.TotalExpense = totals.FirstOrDefault(r => r.Type == OperationType.Expense)?.Total ?? 0m;
report.EndBalance   = report.StartBalance + report.TotalIncome - report.TotalExpense;
```

Дополнительные показатели:
- `TotalDebtReceivable` / `TotalDebtPayable` — активные долги.
- `TotalCreditPurchases` — кредитные покупки за период.
- `TotalCreditDebt` — общий текущий долг по картам.
- `FactBalance` — фактический баланс (наличные + безналичные).
- `NetBalance = FactBalance - TotalCreditDebt`.

### 7.3. Начальный баланс

- `InitialBalance` из [`AppSettings`](../../AutoKassa/Models/AppSettings.cs) относится только к наличным (касса).
- Долговые операции не влияют на баланс.

### 7.4. Структура по категориям

- SQL-агрегация суммы и количества: `GroupBy(CategoryId)` с join к `Categories` на стороне БД; операции в память не материализуются (сортировка агрегатов — в памяти, см. TRAPS 3.13).
- Списки операций категории в DTO не хранятся — загружаются лениво при раскрытии через `GetCategoryTransactionsAsync` (лимит `MaxCategoryTransactions = 1000` на категорию).
- Каждая категория получает цвет из фиксированной палитры.

### 7.5. Детализация операций

- Проекция в DTO на стороне SQL.
- Лимит `MaxDetailTransactions = 1000`; при усечении в отчёт (PDF/Excel/UI) выводится пометка «Показаны первые 1000 из N операций».
- Итоги по доходам/расходам исключают долги.

### 7.6. Отчёт по долгам

- Выбирает операции с `PaymentType == Debt` и `DebtStatus != NotDebt`.
- Считает `RepaidAmount` через `DebtPayment`.
- Итоги: `TotalReceivable`, `TotalPayable`, `ActiveReceivable`, `ActivePayable`, `Balance`.

### 7.7. Сверка кассы

`GetReconciliationDataAsync` показывает остатки на конец выбранной даты:
- Наличные и безналичные остатки.
- Кредитный долг.
- Ближайший платёж по кредитной карте.

---

## 8. Вспомогательные сервисы операций

### 8.1. `TransactionListService`

- Загружает страницы операций через `ITransactionService`.
- `LoadFirstPageAsync` сбрасывает `Skip = 0`.
- `LoadNextPageAsync` увеличивает `CurrentPage`.
- Копирует фильтры, чтобы не мутировать исходный запрос.

### 8.2. `TransactionGroupingService`

- Группирует операции по дням (`Date.Date`).
- Рассчитывает `TotalIncome` / `TotalExpense`.
- Создаёт `SelectableDateGroup` с inline-состоянием.

### 8.3. `InlineTransactionService`

- Валидирует и сохраняет операцию из inline-формы.
- Правила: сумма > 0, категория выбрана, при кредитке выбрана карта.

### 8.4. `TransactionSelectionService`

- `GetSelected` — возвращает выделенные операции.
- `ClearSelection` — снимает выделение.
- Русские склонения слова «запись».

---

## 9. Важные особенности

- **Долги не влияют на фактический баланс и прибыль.** Они фиксируют обязательства; погашения — это реальные доходы/расходы.
- **Кредитные покупки** — это расходы, но они не уменьшают фактический баланс; погашение кредитки — уже реальный расход.
- **Soft delete восстановим:** удалённые операции можно восстановить через `RestoreAsync`.
- **Поиск по описанию** — через SQLite FTS5 (префиксы слов, регистронезависимо, в т.ч. кириллица).
- **Все публичные методы сервисов асинхронны** и принимают `CancellationToken`.
