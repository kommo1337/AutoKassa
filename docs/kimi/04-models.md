# 04-models — сущности и DTO AutoKassa

> Этот файл описывает доменные сущности, отчётные модели и DTO проекта. Полные схемы полей см. в [`json/models.json`](json/models.json).

---

## 1. Доменные сущности

### 1.1. Transaction

Финансовая операция — центральная сущность приложения.

**Файл:** [`AutoKassa/Models/Transaction.cs`](../../AutoKassa/Models/Transaction.cs)

**Таблица:** `Transactions`

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | PK |
| Date | DateTime | Дата операции |
| Amount | decimal(18,2) | Сумма |
| Type | OperationType | Доход / Расход |
| PaymentType | PaymentType | Наличные / Безналичные / Кредитная карта / Долг |
| CategoryId | int | FK → Category (обязательный) |
| CreditCardId | int? | FK → CreditCard (для `PaymentType.CreditCard`) |
| CounterpartyId | int? | FK → Counterparty (для `PaymentType.Debt`) |
| DebtStatus | DebtStatus | NotDebt / Active / Repaid / WrittenOff |
| Description | string?(500) | Описание |
| CreatedAt | DateTime | Дата создания |
| UpdatedAt | DateTime? | Дата изменения |
| IsDeleted | bool | Soft delete flag |

**Навигационные свойства:** `Category`, `CreditCard`, `Counterparty`.

**Особенности:**
- `PaymentType` по умолчанию — `Cash`.
- `DebtStatus` по умолчанию — `NotDebt`.
- При `PaymentType.CreditCard` создаётся связанная `CreditCardPurchase`.
- При `PaymentType.Debt` устанавливается `DebtStatus.Active`.
- Операции никогда не удаляются физически — только `IsDeleted = true`.

---

### 1.2. Category

Справочник категорий доходов и расходов.

**Файл:** [`AutoKassa/Models/Category.cs`](../../AutoKassa/Models/Category.cs)

**Таблица:** `Categories`

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | PK |
| Name | string(100) | Название |
| Type | OperationType | Доход / Расход |
| IsActive | bool | Активна ли категория |
| IsSystem | bool | Системная категория (нельзя удалить) |
| Color | string(7) | HEX-цвет |
| SortOrder | int | Порядок сортировки |
| CreatedAt | DateTime | Дата создания |

**Навигационные свойства:** `Transactions`.

**Ограничения:**
- Уникальность названия в рамках `Type` (индекс `UQ_Category_Name_Type`).
- Системные категории нельзя удалить.
- Категорию нельзя удалить, если на неё ссылаются операции.

**Seed-данные:** 11 системных категорий (см. [`03-database.md`](03-database.md)).

---

### 1.3. Counterparty

Контрагент: клиент, филиал, поставщик, прочее.

**Файл:** [`AutoKassa/Models/Counterparty.cs`](../../AutoKassa/Models/Counterparty.cs)

**Таблица:** `Counterparties`

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | PK |
| Name | string(150) | Название, unique NOCASE |
| Type | CounterpartyType | Client / Branch / Supplier / Other |
| Phone | string?(20) | Телефон |
| Notes | string?(500) | Примечания |
| IsActive | bool | Активность |
| CreatedAt | DateTime | Дата создания |

**Навигационные свойства:** `Transactions`.

**Ограничения:**
- Имя уникально и регистронезависимо.
- Контрагента нельзя удалить, если к нему привязаны операции (даже удалённые).

---

### 1.4. CreditCard

Кредитная карта.

**Файл:** [`AutoKassa/Models/CreditCard.cs`](../../AutoKassa/Models/CreditCard.cs)

**Таблица:** `CreditCards`

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | PK |
| Name | string(100) | Название карты |
| BankName | string?(100) | Банк |
| Limit | decimal(18,2) | Кредитный лимит |
| InterestRate | decimal?(5,2) | Годовая ставка, % |
| StatementDay | int? | День выписки (1–31) |
| PaymentDay | int? | День платежа (1–31) |
| LastPaymentDate | DateTime? | Дата последнего платежа |
| MinimumPaymentPercent | decimal(5,2) | Процент для мин. платежа |
| InitialDebt | decimal(18,2) | Начальный долг |
| IsActive | bool | Активна ли карта |
| CreatedAt | DateTime | Дата создания |

**Навигационные свойства:** `Purchases`, `Transactions`.

**Особенности:**
- Карта с `Id = 1` синхронизируется с настройками (`AppSettings.CreditCard*`).
- `InitialDebt` учитывается при расчёте текущего долга.

---

### 1.5. CreditCardPurchase

Покупка, оплаченная кредитной картой.

**Файл:** [`AutoKassa/Models/CreditCardPurchase.cs`](../../AutoKassa/Models/CreditCardPurchase.cs)

**Таблица:** `CreditCardPurchases`

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | PK |
| CreditCardId | int | FK → CreditCard |
| TransactionId | int | FK → Transaction |
| Amount | decimal(18,2) | Сумма покупки |
| RemainingDebt | decimal(18,2) | Оставшийся долг |
| PurchaseDate | DateTime | Дата покупки |
| Notes | string?(500) | Примечание |

**Навигационные свойства:** `CreditCard`, `Transaction`, `CreditCardPayments`.

**Особенности:**
- Создаётся автоматически при операции с `PaymentType.CreditCard`.
- Погашение распределяется FIFO по `RemainingDebt`.

---

### 1.6. CreditCardPayment

Связь операции-погашения кредитной карты и покупки.

**Файл:** [`AutoKassa/Models/CreditCardPayment.cs`](../../AutoKassa/Models/CreditCardPayment.cs)

**Таблица:** `CreditCardPayments`

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | PK |
| RepaymentTransactionId | int | FK → Transaction (погашение) |
| CreditCardPurchaseId | int | FK → CreditCardPurchase |
| Amount | decimal(18,2) | Сумма погашения на покупку |

**Навигационные свойства:** `RepaymentTransaction`, `CreditCardPurchase`.

**Особенности:**
- Составной индекс уникален: одна покупка + одно погашение.
- При удалении покупки погашения удаляются каскадно (`OnDelete(Cascade)`).

---

### 1.7. DebtPayment

Связь долговой операции и операции-погашения.

**Файл:** [`AutoKassa/Models/DebtPayment.cs`](../../AutoKassa/Models/DebtPayment.cs)

**Таблица:** `DebtPayments`

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | PK |
| DebtTransactionId | int | FK → Transaction (долг) |
| RepaymentTransactionId | int | FK → Transaction (погашение) |
| Amount | decimal(18,2) | Сумма погашения |

**Навигационные свойства:** `DebtTransaction`, `RepaymentTransaction`.

**Особенности:**
- Поддерживаются частичные погашения.
- Статус долга пересчитывается на основе суммы погашений.

---

### 1.8. AppSettings

Глобальные настройки приложения.

**Файл:** [`AutoKassa/Models/AppSettings.cs`](../../AutoKassa/Models/AppSettings.cs)

**Таблица:** `AppSettings`

**Ключевые поля:**

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | PK, всегда 1 |
| PasswordHash | string(255) | BCrypt-хеш пароля |
| SecurityQuestionId | SecurityQuestion? | Секретный вопрос |
| SecurityAnswerHash | string?(255) | BCrypt-хеш ответа |
| AutoLockTimeout | int | Таймаут автоблокировки, мин |
| Theme | string(20) | Light / Dark |
| DefaultOperationType | int | Тип операции по умолчанию |
| DefaultIncomeCategoryId | int? | Категория дохода по умолчанию |
| DefaultExpenseCategoryId | int? | Категория расхода по умолчанию |
| InitialBalance | decimal(18,2) | Начальный баланс |
| DefaultPaymentType | int | Тип оплаты по умолчанию |
| CreditCardLimit | decimal(18,2) | Лимит основной карты |
| CreditCardCurrentDebt | decimal(18,2) | Текущий долг основной карты |
| ... | ... | Полный список см. в [`json/models.json`](json/models.json) |

**Особенности:**
- Single-row таблица: всегда одна запись с `Id = 1`.
- `SettingsService` кеширует настройки в памяти (`_cachedSettings`).

---

### 1.9. FavoriteReport

Избранный отчёт с сохранёнными параметрами.

**Файл:** [`AutoKassa/Models/FavoriteReport.cs`](../../AutoKassa/Models/FavoriteReport.cs)

**Таблица:** `FavoriteReports`

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | PK |
| Name | string(100) | Название |
| ReportType | string(50) | Тип отчёта |
| Parameters | string(2000) | JSON с параметрами |
| CreatedAt | DateTime | Дата создания |

---

## 2. DTO и вспомогательные модели

### 2.1. TransactionFilterParameters

Параметры фильтрации списка операций.

**Файл:** [`AutoKassa/Models/FilterParameters.cs`](../../AutoKassa/Models/FilterParameters.cs)

| Поле | Тип | Описание |
|------|-----|----------|
| DateFrom / DateTo | DateTime? | Период |
| Type | OperationType? | Тип операции |
| PaymentType | PaymentType? | Тип оплаты |
| CategoryId | int? | Категория |
| CounterpartyId | int? | Контрагент |
| IsDebtFilter | bool | Только долги |
| DebtStatus | DebtStatus? | Статус долга |
| SearchText | string | Поиск по описанию |
| AmountFrom / AmountTo | decimal? | Диапазон сумм |
| Skip / Take | int | Пагинация |
| SortBy | string | Поле сортировки |
| SortDescending | bool | Направление сортировки |

### 2.2. DailyTotalsItem

Дневные доходы/расходы для группировки.

**Файл:** [`AutoKassa/Models/DailyTotalsItem.cs`](../../AutoKassa/Models/DailyTotalsItem.cs)

| Поле | Тип |
|------|-----|
| Date | DateTime |
| Income | decimal |
| Expense | decimal |

### 2.3. CreditCardMetrics

Агрегированные метрики по кредитной карте. Возвращается пакетным методом `ICreditCardService.GetCardsMetricsAsync` для загрузки метрик всех карт без N+1.

**Файл:** [`AutoKassa/Models/CreditCardMetrics.cs`](../../AutoKassa/Models/CreditCardMetrics.cs)

| Поле | Тип | Описание |
|------|-----|----------|
| Card | CreditCard | Кредитная карта |
| CurrentDebt | decimal | Текущий долг |
| AvailableLimit | decimal | Доступный остаток лимита |
| MinimumPayment | decimal | Минимальный платёж |
| NextPaymentDate | DateTime? | Дата ближайшего платежа |

---

## 3. Отчётные модели

> Полные схемы отчётных моделей и DTO см. в [`json/models.json`](json/models.json).

### 3.1. ReportBase

Базовый класс для отчётов.

**Файл:** [`AutoKassa/Models/ReportBase.cs`](../../AutoKassa/Models/ReportBase.cs)

| Поле | Тип | Описание |
|------|-----|----------|
| Name | string | Название отчёта |
| GeneratedAt | DateTime | Дата формирования |

### 3.2. BalanceReport

Отчёт «Баланс за период».

**Файл:** [`AutoKassa/Models/Reports/BalanceReport.cs`](../../AutoKassa/Models/Reports/BalanceReport.cs)

| Поле | Тип | Описание |
|------|-----|----------|
| DateFrom / DateTo | DateTime | Период |
| StartBalance | decimal | Начальный баланс |
| EndBalance | decimal | Конечный баланс |
| TotalIncome | decimal | Доходы |
| TotalExpense | decimal | Расходы |
| TotalCreditPurchases | decimal | Кредитные покупки |
| TotalCreditDebt | decimal | Текущий кредитный долг |
| FactBalance | decimal | Наличные + безналичные |
| NetBalance | decimal | FactBalance − TotalCreditDebt |
| NextCreditPaymentDate | DateTime? | Ближайший платёж |
| NextCreditPaymentAmount | decimal | Сумма ближайшего платежа |
| TotalDebtReceivable | decimal | Активная дебиторка |
| TotalDebtPayable | decimal | Активная кредиторка |
| DailyBalances | List<DailyBalance> | Данные по дням |

### 3.3. CategoryReport

Отчёт «Структура по категориям».

**Файл:** [`AutoKassa/Models/Reports/CategoryReport.cs`](../../AutoKassa/Models/Reports/CategoryReport.cs)

| Поле | Тип | Описание |
|------|-----|----------|
| DateFrom / DateTo | DateTime | Период |
| OperationType | OperationType | Доход / Расход |
| TotalAmount | decimal | Общая сумма |
| TransactionCount | int | Количество операций |
| CategoryItems | List<CategoryReportItem> | Данные по категориям |

**CategoryReportItem:**

| Поле | Тип | Описание |
|------|-----|----------|
| CategoryId | int | ID категории |
| CategoryName | string | Название |
| Amount | decimal | Сумма |
| Percentage | double | Процент |
| TransactionCount | int | Количество операций |
| Color | string | HEX-цвет |
| Transactions | List<Transaction> | Операции категории |

### 3.4. TransactionDetailReport

Отчёт «Детализация операций».

**Файл:** [`AutoKassa/Models/Reports/TransactionDetailReport.cs`](../../AutoKassa/Models/Reports/TransactionDetailReport.cs)

| Поле | Тип | Описание |
|------|-----|----------|
| DateFrom / DateTo | DateTime | Период |
| FilterType | OperationType? | Фильтр по типу |
| FilterCategoryId | int? | Фильтр по категории |
| FilterCategoryName | string | Название категории |
| Transactions | List<TransactionDetailItem> | Операции |
| TotalIncome / TotalExpense | decimal | Итоги |

**TransactionDetailItem:**

| Поле | Тип | Описание |
|------|-----|----------|
| Id | int | ID операции |
| Date | DateTime | Дата |
| Type | OperationType | Тип |
| Amount | decimal | Сумма |
| CategoryName | string | Категория |
| Description | string | Описание |
| CreatedAt | DateTime | Дата создания |

### 3.5. DebtReport

Отчёт по долгам.

**Файл:** [`AutoKassa/Models/Reports/DebtReport.cs`](../../AutoKassa/Models/Reports/DebtReport.cs)

| Поле | Тип | Описание |
|------|-----|----------|
| DateFrom / DateTo | DateTime | Период |
| TotalReceivable | decimal | Нам должны (всего) |
| TotalPayable | decimal | Мы должны (всего) |
| ActiveReceivable | decimal | Нам должны (активно) |
| ActivePayable | decimal | Мы должны (активно) |
| Items | List<DebtItem> | Долги |

**DebtItem:**

**Файл:** [`AutoKassa/Models/Reports/DebtItem.cs`](../../AutoKassa/Models/Reports/DebtItem.cs)

| Поле | Тип | Описание |
|------|-----|----------|
| TransactionId | int | ID долга |
| Date | DateTime | Дата |
| Amount | decimal | Первоначальная сумма |
| RepaidAmount | decimal | Погашено |
| RemainingAmount | decimal | Остаток |
| Status | DebtStatus | Статус |
| Direction | OperationType | Income = нам должны, Expense = мы должны |
| CounterpartyId | int? | ID контрагента |
| CounterpartyName | string | Имя контрагента |
| CounterpartyType | CounterpartyType | Тип контрагента |
| CategoryName | string | Категория |
| Description | string? | Описание |

### 3.6. ReconciliationData

Данные для окна «Сверка кассы».

**Файл:** [`AutoKassa/Models/Reports/ReconciliationData.cs`](../../AutoKassa/Models/Reports/ReconciliationData.cs)

| Поле | Тип | Описание |
|------|-----|----------|
| Date | DateTime | Дата сверки |
| CashAmount | decimal | Остаток наличных |
| NonCashAmount | decimal | Остаток безналичных |
| CreditDebt | decimal | Текущий кредитный долг |
| NextPaymentAmount | decimal | Минимальный платёж |
| NextPaymentDate | DateTime? | Дата платежа |

---

## 4. Правила валидации

### Transaction

- `Amount` > 0.
- `CategoryId` обязателен.
- `Description` не длиннее 500 символов.
- `CreditCardId` обязателен при `PaymentType.CreditCard`.
- `CounterpartyId` обязателен при `PaymentType.Debt`.
- Нельзя редактировать операцию-погашение (`DebtPayment` / `CreditCardPayment`).

### Category

- `Name` обязательно, не длиннее 100 символов.
- `Type` обязателен.
- Название уникально в рамках `Type`.
- Системные категории нельзя удалить.

### Counterparty

- `Name` обязательно, уникально, не длиннее 150 символов.
- Удаление запрещено при наличии связанных операций.

### CreditCard

- `Name` обязательно.
- `Limit` ≥ 0.
- `StatementDay` / `PaymentDay` в диапазоне 1–31.

### AppSettings

- `Id` всегда 1.
- `AutoLockTimeout` ≥ 0.
- `DefaultPageSize` > 0.

---

> Последнее обновление: подэтапы 3.1–3.2 — Markdown-описания.
