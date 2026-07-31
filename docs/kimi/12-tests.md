# 12. Тестирование

> Этот файл описывает тестовую инфраструктуру и покрытие проекта AutoKassa.

---

## 1. Инфраструктура

### 1.1. `TestDatabase`

**Файл:** [`AutoKassa.Tests/Infrastructure/TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs)

- SQLite in-memory: `DataSource=:memory:`.
- Соединение держится открытым на время теста.
- `EnsureCreated()` создаёт схему и seed-категории (миграции не применяются).
- После `EnsureCreated()` выполняется SQL-константа `FtsSetupSql`, создающая FTS5-таблицу `TransactionsFts` и триггеры — зеркало миграции `AddTransactionsFts`; при изменении миграции синхронизируй константу.
- `Create()` → `(AppDbContext context, SqliteConnection connection)`.
- `CreateWithFactory()` → `(TestDbContextFactory factory, SqliteConnection connection)`.

### 1.2. `TestDbContextFactory`

**Файл:** [`AutoKassa.Tests/Infrastructure/TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs)

Реализация `IDbContextFactory<AppDbContext>` для тестов сервисов, использующих фабрику.

### 1.3. `FakeDataChangeService`

**Файл:** [`AutoKassa.Tests/Infrastructure/FakeDataChangeService.cs`](../../AutoKassa.Tests/Infrastructure/FakeDataChangeService.cs)

Фейк `IDataChangeService` для тестов VM.

### 1.4. Хелперы seed

| Метод | Назначение |
|---|---|
| `SeedExpenseCategory(ctx, name)` | Создание категории расходов. |
| `SeedIncomeCategory(ctx, name)` | Создание категории доходов. |
| `SeedTransaction(ctx, categoryId, amount, type, paymentType, date, description, ...)` | Создание операции. |
| `SeedCounterparty(ctx, name, type, phone)` | Создание контрагента. |
| `SeedCreditCard(ctx, name, limit, initialDebt, minimumPaymentPercent)` | Создание кредитной карты. |
| `SeedCreditCardPurchase(ctx, creditCardId, transactionId, amount)` | Создание покупки по карте. |

---

## 2. Запуск тестов

```bash
# Все тесты
dotnet test

# Только тесты сервисов
dotnet test --filter "FullyQualifiedName~Services"
```

---

## 3. Покрытие по классам тестов

| Тестовый класс | Что покрывается | Примерное количество тестов |
|----------------|-----------------|------------------------------|
| `TransactionServiceTests` | CRUD, soft delete, restore, фильтры, пагинация, агрегации, CancellationToken, кредитки, долги | ~50+ |
| `CategoryServiceTests` | CRUD, ExistsAsync, Delete/Deactivate, ReorderAsync | ~20 |
| `SettingsServiceTests` | Кеш, сохранение, сброс к дефолтам, пароль, категории по умолчанию | ~10 |
| `PasswordServiceTests` | Хеширование, верификация, edge cases | ~8 |
| `ReportServiceTests` | Баланс, категории, детализация, долги, сверка кассы, decimal precision | ~40 |
| `CreditCardServiceTests` | Создание карты, долг, лимит, погашение, FIFO, дата платежа | ~18 |
| `DebtServiceTests` | Создание долга, погашение, частичные, списание, статусы, пагинация | ~20 |
| `CounterpartyServiceTests` | CRUD, дубликаты, удаление с операциями | ~12 |
| `AppInfoServiceTests` | Версия, счётчики, дата бэкапа | ~3 |
| `DashboardPeriodCoordinatorTests` | Периоды дашборда | ~3 |
| `SettingsMapperTests` | Маппинг в обе стороны, Clone | ~4 |
| `SettingsValidatorTests` | Валидация границ настроек | ~7 |
| `TransactionListServiceTests` | Пагинация, immutable filters | ~3 |

---

## 4. Ключевые паттерны тестов

- Тестовые классы реализуют `IDisposable` с `_conn.Dispose()` в `Dispose()`.
- Использование `FluentAssertions`.
- `TestDatabase.CreateWithFactory()` для сервисов с `IDbContextFactory`.
- Проверка `CancellationToken` propagation через уже отменённый `CancellationTokenSource`.
- Проверка decimal precision: суммы вроде `0.1m + 0.2m = 0.3m`.
- Soft-delete: проверка, что запись остаётся в БД с `IsDeleted = true`.

---

## 5. Пример теста

```csharp
[Fact]
public async Task AddAsync_WithValidTransaction_CreatesTransaction()
{
    var (ctx, conn) = TestDatabase.Create();
    await using var _ = conn;
    
    var category = TestDatabase.SeedExpenseCategory(ctx, "Запчасти");
    var service = new TransactionService(new TestDbContextFactory(ctx));
    
    var transaction = new Transaction
    {
        Date = DateTime.Today,
        Amount = 1000m,
        Type = OperationType.Expense,
        PaymentType = PaymentType.Cash,
        CategoryId = category.Id
    };
    
    var result = await service.AddAsync(transaction);
    
    result.Id.Should().BeGreaterThan(0);
    result.IsDeleted.Should().BeFalse();
}
```

---

## 6. Рекомендации по тестированию

1. Всегда освобождайте `SqliteConnection` в `Dispose()`.
2. Используйте `TestDatabase.CreateWithFactory()` для сервисов с фабрикой контекста.
3. Покрывайте новую бизнес-логику юнит-тестами.
4. Проверяйте граничные случаи: отрицательные суммы, null, пустые строки.
5. Проверяйте `CancellationToken` propagation для асинхронных методов.
