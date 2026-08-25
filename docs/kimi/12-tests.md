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

### 1.4. Фейки UI-сервисов

| Файл | Назначение |
|---|---|
| [`FakeDialogService.cs`](../../AutoKassa.Tests/Infrastructure/FakeDialogService.cs) | Записывает сообщения (`LastError`/`LastWarning`/`LastInfo`), результаты диалогов настраиваются свойствами. |
| [`FakeLockService.cs`](../../AutoKassa.Tests/Infrastructure/FakeLockService.cs) | Считает вызовы `Lock`/`Start`/`Stop`/`ResetAutoLockTimer`, без реальных таймеров. |
| [`FakeNavigationService.cs`](../../AutoKassa.Tests/Infrastructure/FakeNavigationService.cs) | Заглушка навигации: хранит `CurrentView`, навигация — no-op. |
| [`FakeToastNotificationService.cs`](../../AutoKassa.Tests/Infrastructure/FakeToastNotificationService.cs) | Записывает toast-сообщения (`LastSuccess`/`LastError`/`LastInfo`). |

### 1.5. `TestWaitHelper`

**Файл:** [`AutoKassa.Tests/Infrastructure/TestWaitHelper.cs`](../../AutoKassa.Tests/Infrastructure/TestWaitHelper.cs)

`WaitUntilAsync(Func<bool>, timeoutMs)` — поллинг условия для `async void` команд (`AsyncRelayCommand.Execute` не возвращает `Task`, выполнение отслеживается по побочным эффектам).

### 1.6. Хелперы seed

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
| `TransactionServiceTests` | CRUD, soft delete (включая массовый `DeleteRangeAsync`), restore (включая массовый `RestoreRangeAsync`), фильтры, пагинация, агрегации, CancellationToken, кредитки (покупки, погашения, пополнения), долги | ~70+ |
| `CategoryServiceTests` | CRUD, ExistsAsync, Delete/Deactivate, ReorderAsync | ~20 |
| `SettingsServiceTests` | Кеш, сохранение, сброс к дефолтам, пароль, категории по умолчанию | ~10 |
| `PasswordServiceTests` | Хеширование, верификация, edge cases | ~8 |
| `ReportServiceTests` | Баланс, категории, детализация, долги, сверка кассы, decimal precision | ~40 |
| `CreditCardServiceTests` | Создание карты, долг, лимит, погашение, пополнение, FIFO, дата платежа | ~22 |
| `DebtServiceTests` | Создание долга, погашение, частичные, списание, статусы, пагинация | ~20 |
| `CounterpartyServiceTests` | CRUD, дубликаты, удаление с операциями | ~12 |
| `AppInfoServiceTests` | Версия, счётчики, дата бэкапа | ~3 |
| `DashboardPeriodCoordinatorTests` | Периоды дашборда | ~3 |
| `SettingsMapperTests` | Маппинг в обе стороны, Clone | ~4 |
| `SettingsValidatorTests` | Валидация границ настроек | ~7 |
| `TransactionListServiceTests` | Пагинация, immutable filters | ~3 |
| `PooledDbContextFactoryTests` | Регрессия: пулинг контекстов + `MigrateAsync` (опции задаются при регистрации, не в `OnConfiguring`) | 1 |
| `LockServiceTests` | Таймер автоблокировки: настройки вкл/выкл, таймаут, старт/стоп/сброс | ~7 |
| `PasswordRecoveryViewModelTests` | Восстановление пароля: загрузка вопроса, проверка ответа, смена пароля, `Reset` | ~10 |
| `LockScreenViewModelTests` | Разблокировка, режим восстановления пароля, отписка при `Dispose` | ~8 |
| `MainWindowViewModelTests` | Автоблокировка: старт таймера из конструктора, троттлинг `NotifyUserActivity` | ~4 |
| `MaxModalHeightConverterTests` | Конвертер максимальной высоты модалок: отступы, минимум, невалидный вход | ~8 |
| `AsyncRelayCommandTests` | Блокировка повторного запуска, проглатывание исключений, `CanExecute` | ~3 |
| `ChangePasswordViewModelTests` | Политика паролей: минимум 6, отличие от текущего, совпадение подтверждения | ~4 |

---

## 4. Ключевые паттерны тестов

- Тестовые классы реализуют `IDisposable` с `_conn.Dispose()` в `Dispose()`.
- Использование `FluentAssertions`.
- `TestDatabase.CreateWithFactory()` для сервисов с `IDbContextFactory`.
- Проверка `CancellationToken` propagation через уже отменённый `CancellationTokenSource`.
- Проверка decimal precision: суммы вроде `0.1m + 0.2m = 0.3m`.
- Soft-delete: проверка, что запись остаётся в БД с `IsDeleted = true`.
- `async void` команды (`AsyncRelayCommand`): `Execute(null)` + `TestWaitHelper.WaitUntilAsync(...)` по наблюдаемому эффекту.
- Приватное состояние без публичного наблюдателя (таймер `_autoLockTimer`, троттлинг `_lastActivityReset`) — через reflection, точечно.

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
