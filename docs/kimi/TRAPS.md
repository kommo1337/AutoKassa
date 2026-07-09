# TRAPS — запреты и ловушки в AutoKassa

> Этот файл собирает правила, которые легко нарушить, и объясняет, почему так делать нельзя. Прочитай перед изменением кода.

---

## 1. Архитектурные запреты

### 1.1. Не пиши бизнес-логику в code-behind View
- **Запрет:** обработчики событий в `.xaml.cs` не должны принимать решения, обращаться к БД или вызывать сервисы.
- **Почему:** нарушает MVVM, усложняет тестирование и приводит к дублированию логики.
- **Правильно:** используй команды и ViewModel.
- **Файлы:** [`../../AutoKassa/Views/TransactionsView.xaml.cs`](../../AutoKassa/Views/TransactionsView.xaml.cs), [`../../AutoKassa/Helpers/RelayCommand.cs`](../../AutoKassa/Helpers/RelayCommand.cs).

### 1.2. Не используй статический Service Locator
- **Запрет:** не обращайся к статическому `IServiceProvider` или `App.GetService<T>()` из ViewModel или сервисов. В проекте такого метода нет — не добавляй.
- **Почему:** скрывает зависимости, усложняет тестирование и нарушает DI.
- **Правильно:** передавай зависимости через конструктор; фабрики (`IReportViewModelFactory`, `ITransactionEditViewModelFactory`) получают `IServiceProvider` явно.
- **Файлы:** [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs) (конфигурация DI).

### 1.3. Не делай синхронные запросы к БД
- **Запрет:** никаких `.Result`, `.Wait()`, синхронных методов EF Core (`SaveChanges`, `ToList`).
- **Почему:** блокирует UI-поток, вызывает deadlock и деградацию отзывчивости.
- **Правильно:** `await _contextFactory.CreateDbContextAsync()` + `await ...ToListAsync()`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).

### 1.4. ViewModel не должна напрямую работать с DbContext
- **Запрет:** не создавай `DbContext` и не выполняй запросы из ViewModel.
- **Почему:** ViewModel отвечает за состояние экрана, а не за доступ к данным.
- **Правильно:** инжектируй сервис (`ITransactionService` и т.п.) и вызывай его методы.
- **Файлы:** [`../../AutoKassa/ViewModels/TransactionsViewModel.cs`](../../AutoKassa/ViewModels/TransactionsViewModel.cs).

### 1.5. Не вызывай долгие операции в конструкторе сервисов
- **Запрет:** не обращайся к БД и не блокируй поток в конструкторе Singleton-сервисов.
- **Почему:** `SettingsService` регистрируется как Singleton; загрузка настроек в конструкторе заблокирует инициализацию DI и старт приложения.
- **Правильно:** используй ленивую загрузку (`GetSettings()`) или асинхронный метод `GetSettingsAsync()`.
- **Файлы:** [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (конструктор, `LoadSettings`).

---

## 2. Бизнес-правила-ловушки

### 2.1. Только soft delete
- **Запрет:** нельзя физически удалять операции, категории и другие бизнес-сущности.
- **Почему:** необходима история и возможность восстановления данных.
- **Правильно:** устанавливай `IsDeleted = true` и используй `RestoreAsync` для отката.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`DeleteAsync`, `RestoreAsync`).

### 2.2. `PaymentType.Debt` не влияет на баланс
- **Запрет:** не учитывай долговые операции при расчёте фактического баланса и прибыли.
- **Почему:** `Debt` фиксирует обязательство, а не движение денег.
- **Правильно:** баланс считай только по `Cash`, `NonCash` и `CreditCard`; погашения — обычными операциями.
- **Файлы:** [`../../AutoKassa/Services/ReportService.cs`](../../AutoKassa/Services/ReportService.cs), [`../../AutoKassa/Models/Enums/PaymentType.cs`](../../AutoKassa/Models/Enums/PaymentType.cs).

### 2.3. Системные категории нельзя удалить
- **Запрет:** не устанавливай `IsDeleted = true` для категорий с `IsSystem = true`.
- **Почему:** системные категории используются для специальных операций (например, "Погашение кредита").
- **Правильно:** можно деактивировать (`IsActive = false`), но не удалять.
- **Файлы:** [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs).

### 2.4. Уникальность названия категории в рамках типа
- **Запрет:** нельзя создать две категории с одинаковым названием внутри одного `OperationType`.
- **Почему:** в БД есть уникальный индекс `UQ_Category_Name_Type`.
- **Правильно:** проверяй существование перед добавлением.
- **Файлы:** [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs), [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs).

### 2.5. Нельзя удалить категорию по умолчанию
- **Запрет:** если категория выбрана в `AppSettings.DefaultIncomeCategoryId` / `DefaultExpenseCategoryId`, её нельзя удалить.
- **Почему:** настройки ссылаются на категорию, и удаление сломает поведение по умолчанию.
- **Правильно:** сначала измени категорию по умолчанию в настройках.
- **Файлы:** [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs).

### 2.6. Нельзя редактировать операцию-погашение
- **Запрет:** операции, участвующие в `DebtPayment` или `CreditCardPayment` как `RepaymentTransaction`, нельзя редактировать.
- **Почему:** изменение суммы или типа погашения нарушит связи и расчёты остатков.
- **Правильно:** удалить старое погашение (soft delete) и создать новое.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`UpdateAsync`).

### 2.7. Нельзя удалить контрагента с операциями
- **Запрет:** удаление контрагента запрещено, если к нему привязаны операции (включая удалённые).
- **Почему:** сохраняем целостность истории.
- **Правильно:** деактивируй контрагента (`IsActive = false`).
- **Файлы:** [`../../AutoKassa/Services/CounterpartyService.cs`](../../AutoKassa/Services/CounterpartyService.cs).

### 2.8. Нельзя списать долг с погашениями
- **Запрет:** `DebtService.WriteOffAsync` выбросит исключение, если по долгу есть погашения.
- **Почему:** списание долга с уже внесёнными платежами приведёт к некорректной истории.
- **Правильно:** сначала удалить (soft delete) все погашения долга.
- **Файлы:** [`../../AutoKassa/Services/DebtService.cs`](../../AutoKassa/Services/DebtService.cs).

### 2.9. Нельзя погасить неактивную кредитную карту
- **Запрет:** погашение возможно только для активной карты (`IsActive = true`).
- **Почему:** неактивная карта исключена из оборота.
- **Правильно:** активируй карту или выбери другую.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs), [`../../AutoKassa/Services/CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs).

### 2.10. Погашение кредитной карты — только наличные или безналичные
- **Запрет:** для операции погашения `PaymentType` не может быть `Debt` или `CreditCard`.
- **Почему:** погашение — это реальный перевод денег, а не новый долг или кредит.
- **Правильно:** используй `Cash` или `NonCash`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`CreateCreditCardRepaymentInContextAsync`).

### 2.11. Нельзя удалить кредитную карту с непогашенным долгом
- **Запрет:** `CreditCardService.DeleteAsync` не позволит деактивировать карту, пока `GetCurrentDebtAsync` > 0.
- **Почему:** удаление карты с долгом приведёт к потере обязательства и нарушит баланс.
- **Правильно:** сначала погаси долг по карте.
- **Файлы:** [`../../AutoKassa/Services/CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs) (`DeleteAsync`).

### 2.12. Нельзя добавить покупку по неактивной карте или превысить лимит
- **Запрет:** `CreditCardOperationHelper.AddPurchaseAsync` проверяет `IsActive` и `currentDebt + amount <= Limit`.
- **Почему:** неактивная карта не должна использоваться; превышение лимита нарушает кредитное соглашение.
- **Правильно:** проверяй доступный лимит через `CreditCardService.GetAvailableLimitAsync` до создания операции.
- **Файлы:** [`../../AutoKassa/Services/CreditCardOperationHelper.cs`](../../AutoKassa/Services/CreditCardOperationHelper.cs) (`AddPurchaseAsync`).

### 2.13. Погашение не может превышать текущий долг по карте
- **Запрет:** сумма погашения должна быть `<= currentDebt`.
- **Почему:** избыточное погашение создаст отрицательный долг и сломает FIFO-учёт.
- **Правильно:** рассчитывай текущий долг через `CreditCardOperationHelper.GetCurrentDebtAsync` и ограничивай сумму.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`CreateCreditCardRepaymentInContextAsync`, `RestoreAsync`), [`../../AutoKassa/Services/CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs) (`RepayDebtAsync`).

### 2.14. Операции создания долга скрыты из списка операций по умолчанию
- **Запрет:** не отображай `PaymentType == Debt` в общем списке операций без явного фильтра.
- **Почему:** долговые операции — это обязательства; их место в отчёте по долгам.
- **Правильно:** в `TransactionFilterParameters` установи `IsDebtFilter = true`, чтобы получить долги.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`ApplyFilters`).

### 2.15. Нельзя создать или обновить операцию с неактивной категорией
- **Запрет:** `TransactionService.AddAsync` / `UpdateAsync` проверяют `category.IsActive`.
- **Почему:** неактивные категории исключены из выбора в UI и не должны использоваться в новых операциях.
- **Правильно:** активируй категорию или выбери активную.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`AddAsync`, `UpdateAsync`).

### 2.16. При смене типа оплаты с долга на обычную удаляются связи с погашениями
- **Запрет:** `UpdateAsync` удаляет записи `DebtPayment`, если операция перестаёт быть `PaymentType.Debt`.
- **Почему:** погашения останутся висячими и будут учтены как реальные доходы/расходы без связанного долга.
- **Правильно:** предупреди пользователя или запрети смену типа, если есть погашения.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`UpdateAsync`).

### 2.17. Погашение долга не может превышать остаток
- **Запрет:** `DebtService.RepayAsync` проверяет `amount <= remaining`.
- **Почему:** переплата по долгу нарушит статус `Repaid` и историю платежей.
- **Правильно:** рассчитывай остаток через `GetRemainingAmountAsync` до погашения.
- **Файлы:** [`../../AutoKassa/Services/DebtService.cs`](../../AutoKassa/Services/DebtService.cs) (`RepayAsync`).

### 2.18. Название контрагента уникально регистронезависимо
- **Запрет:** нельзя создать двух контрагентов с одинаковым именем.
- **Почему:** в БД `Counterparty.Name` имеет `TEXT COLLATE NOCASE` и уникальный индекс `IX_Counterparty_Name`.
- **Правильно:** проверяй `ExistsAsync` перед добавлением.
- **Файлы:** [`../../AutoKassa/Services/CounterpartyService.cs`](../../AutoKassa/Services/CounterpartyService.cs), [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs).

### 2.19. Не меняй флаг `IsSystem` у существующих категорий
- **Запрет:** `CategoryService.UpdateAsync` позволяет записать `existing.IsSystem = category.IsSystem`, но делать этого нельзя.
- **Почему:** системные категории ищутся по имени и `Id` в бизнес-логике (например, "Погашение кредита").
- **Правильно:** не передавай `IsSystem` из UI; новые системные категории добавляй только через seed-данные.
- **Файлы:** [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs) (`UpdateAsync`), [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs) (`SeedData`).

### 2.20. `InitialBalance` относится только к наличным
- **Запрет:** не добавляй начальный баланс к безналичным или кредитным расчётам.
- **Почему:** в `ReportService.GetInitialBalanceAsync` начальный баланс применяется только при `paymentType == Cash` или отсутствии фильтра.
- **Правильно:** при фильтрации по `NonCash` / `CreditCard` не учитывай `InitialBalance`.
- **Файлы:** [`../../AutoKassa/Services/ReportService.cs`](../../AutoKassa/Services/ReportService.cs) (`GetInitialBalanceAsync`).

---

## 3. Технические ловушки

### 3.1. SQLite `LOWER()` не поддерживает кириллицу
- **Запрет:** не используй SQL-фильтрацию по `Description` с `LOWER`/`Contains` для кириллического текста.
- **Почему:** SQLite не умеет применять `LOWER` к Unicode; поиск станет регистрозависимым или неработающим.
- **Правильно:** фильтруй в памяти после SQL-выборки с ограничением `MaxInMemoryFilterLimit`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`GetTransactionsAsync`).

### 3.2. Отключай навигационные свойства из других DbContext
- **Запрет:** не добавляй в `DbContext` сущность с уже заполненными навигационными свойствами, полученными из другого контекста.
- **Почему:** EF Core будет пытаться отслеживать связанные объекты, что приводит к конфликтам tracking.
- **Правильно:** устанавливай `Category = null; CreditCard = null; Counterparty = null;` перед `Add`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`AddAsync`).

### 3.3. `AppSettings` — single-row таблица
- **Запрет:** не создавай несколько строк `AppSettings`; `Id` всегда должен быть равен `1`.
- **Почему:** настройки приложения глобальны; код ожидает одну запись.
- **Правильно:** всегда читай/обновляй запись с `Id = 1`.
- **Файлы:** [`../../AutoKassa/Models/AppSettings.cs`](../../AutoKassa/Models/AppSettings.cs), [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs).

### 3.4. Ограничение поиска в памяти `MaxInMemoryFilterLimit`
- **Запрет:** не загружай неограниченное количество записей для текстового поиска.
- **Почему:** большая таблица операций может вызвать RAM-всплеск.
- **Правильно:** используй `Take(MaxInMemoryFilterLimit)` перед фильтрацией в памяти.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).

### 3.5. `CreditCardPurchase.RemainingDebt` не может быть меньше суммы погашений
- **Запрет:** не устанавливай `RemainingDebt` меньше уже произведённых погашений.
- **Почему:** это нарушит FIFO-учёт и баланс долга по карте.
- **Правильно:** при изменении суммы покупки пересчитывай `RemainingDebt = max(0, Amount - paid)`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`HandleCreditCardChangesAsync`).

### 3.6. `CreditCardPayment` удаляются каскадно при удалении `CreditCardPurchase`
- **Запрет:** не полагайся на soft delete для `CreditCardPayment`; они физически удаляются при удалении покупки.
- **Почему:** в `AppDbContext` настроен `OnDelete(DeleteBehavior.Cascade)` для `CreditCardPayment -> CreditCardPurchase`.
- **Правильно:** при восстановлении покупки заново распределяй погашения через `DistributeRepaymentAsync`.
- **Файлы:** [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs), [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`RestoreAsync`).

### 3.7. Настройки кешируются — изменяй только через `SaveSettingsAsync`
- **Запрет:** не мутируй объект, полученный из `GetSettings()`, напрямую вне сервиса.
- **Почему:** `_cachedSettings` разделяется между вызовами; прямая мутация может привести к рассинхронизации.
- **Правильно:** изменяй поля и вызывай `SaveSettingsAsync(settings)`, который обновит БД и кеш.
- **Файлы:** [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`GetSettings`, `SaveSettingsAsync`).

### 3.8. Путь бэкапа санитизируется
- **Запрет:** не сохраняй резервные копии за пределами профиля пользователя или каталога приложения.
- **Почему:** `SanitizeBackupPath` отклоняет пути вне разрешённых зон для защиты от path traversal.
- **Правильно:** выбирай путь внутри `%USERPROFILE%` или папки приложения.
- **Файлы:** [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`SanitizeBackupPath`, `ImportSettingsAsync`).

### 3.9. Восстановление из бэкапа сбрасывает пулы соединений и применяет миграции
- **Запрет:** не копируй файл бэкапа поверх открытой БД без `SqliteConnection.ClearAllPools()`.
- **Почему:** открытые хендлы заблокируют файл и восстановление упадёт; старая копия может иметь устаревшую схему.
- **Правильно:** используй `RestoreBackupAsync`, который очищает пулы, копирует файл и вызывает `MigrateAsync`.
- **Файлы:** [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`RestoreBackupAsync`).

### 3.10. Группировка по категориям в отчётах — по `CategoryId`, не по ссылке
- **Запрет:** не используй `GroupBy(t => t.Category)` при `AsNoTracking()`.
- **Почему:** EF Core создаёт отдельные экземпляры `Category` для каждой строки; группировка по ссылке даст неверный результат.
- **Правильно:** группируй по `CategoryId` и бери имя из `g.First().Category`.
- **Файлы:** [`../../AutoKassa/Services/ReportService.cs`](../../AutoKassa/Services/ReportService.cs) (`GenerateCategoryReportAsync`).

### 3.11. Лимиты `MaxDetailTransactions` и `MaxCategoryTransactions`
- **Запрет:** не загружай неограниченное количество операций для отчётов.
- **Почему:** большие выборки могут привести к зависанию UI и RAM-всплеску.
- **Правильно:** уважай константы `MaxDetailTransactions` / `MaxCategoryTransactions` при кастомных отчётах.
- **Файлы:** [`../../AutoKassa/Services/ReportService.cs`](../../AutoKassa/Services/ReportService.cs).

---

## 4. Частые ошибки при изменениях

### 4.1. Забыть вызвать `NotifyDataChanged()`
- **Запрет:** после изменения данных не оставляй подписчиков `IDataChangeService` без уведомления.
- **Почему:** другие экраны (Dashboard, Reports) не обновятся и покажут устаревшие данные.
- **Правильно:** вызывай `_dataChangeService.NotifyDataChanged()` после успешного сохранения.
- **Файлы:** [`../../AutoKassa/Services/IDataChangeService.cs`](../../AutoKassa/Services/IDataChangeService.cs), [`../../AutoKassa/ViewModels/TransactionsViewModel.cs`](../../AutoKassa/ViewModels/TransactionsViewModel.cs).

### 4.2. Забыть зарегистрировать сервис или ViewModel в DI
- **Запрет:** не оставляй новый класс без регистрации в `App.ConfigureServices`.
- **Почему:** `GetRequiredService<T>()` бросит исключение при навигации или использовании.
- **Правильно:** добавь `services.AddTransient<T>()` / `AddSingleton<T>()` с правильным lifetime.
- **Файлы:** [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs).

### 4.3. Забыть добавить DataTemplate в `MainWindow.xaml`
- **Запрет:** новый ViewModel без соответствующего `DataTemplate` не отобразится.
- **Почему:** WPF не знает, какое View использовать для типа VM.
- **Правильно:** добавь `<DataTemplate DataType="{x:Type vm:NewViewModel}">` в ресурсы `MainWindow.xaml`.
- **Файлы:** [`../../AutoKassa/MainWindow.xaml`](../../AutoKassa/MainWindow.xaml).

### 4.4. Забыть обновить миграцию после изменения модели
- **Запрет:** не меняй свойства сущностей без соответствующей миграции EF Core.
- **Почему:** приложение упадёт на старте из-за несоответствия схемы БД.
- **Правильно:** создай миграцию `dotnet ef migrations add <Name>` и протестируй на чистой БД.
- **Файлы:** [`../../AutoKassa/Migrations/`](../../AutoKassa/Migrations/).

### 4.5. Забыть отписаться от `IDataChangeService.DataChanged`
- **Запрет:** не оставляй подписки на события Singleton-сервиса после закрытия экрана.
- **Почему:** ViewModel будет удерживаться в памяти и реагировать на чужие изменения.
- **Правильно:** в `OnDispose()` отписывайся от `_dataChangeService.DataChanged`.
- **Файлы:** [`../../AutoKassa/Helpers/ViewModelBase.cs`](../../AutoKassa/Helpers/ViewModelBase.cs), [`../../AutoKassa/Services/IDataChangeService.cs`](../../AutoKassa/Services/IDataChangeService.cs).

### 4.6. Забыть обновить документацию при изменении правил
- **Запрет:** новые ограничения, исключения и бизнес-правила должны попадать в `TRAPS.md` и `json/traps.json`.
- **Почему:** следующий агент не узнает о новой ловушке и повторит ошибку.
- **Правильно:** добавь ловушку в `TRAPS.md` и синхронизируй `json/traps.json`.
- **Файлы:** [`TRAPS.md`](TRAPS.md), [`json/traps.json`](json/traps.json).

---

## 5. Что нельзя делать без согласования

### 5.1. Изменять lifetime сервисов
- **Запрет:** не меняй Singleton / Scoped / Transient у существующих сервисов без анализа.
- **Почему:** может сломать DI-граф и состояние.
- **Правильно:** согласуй изменение и проверь всех потребителей в relationships.json.
- **Файлы:** [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs).

### 5.2. Добавлять каскадное удаление
- **Запрет:** не добавляй `OnDelete(Cascade)` без согласования.
- **Почему:** проект работает по принципу soft delete и явных связей.
- **Правильно:** используй `Restrict` + явную логику soft delete.
- **Файлы:** [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs).

### 5.3. Физически удалять данные
- **Запрет:** не используй `Remove` / `DELETE` без согласования.
- **Почему:** нарушает требование сохранения истории.
- **Правильно:** используй soft delete (`IsDeleted = true`).
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).

### 5.4. Изменять системные категории
- **Запрет:** не меняй `Name`, `Id` или `IsSystem` у системных категорий.
- **Почему:** категории ищутся по имени и используются в бизнес-логике.
- **Правильно:** если нужна новая системная категория — согласуй и обновь seed-данные.
- **Файлы:** [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs), [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs).

### 5.5. Менять схему AppSettings
- **Запрет:** не добавляй/удаляй обязательные поля `AppSettings` без анализа.
- **Почему:** single-row таблица с фиксированным `Id = 1` и широким каскадом зависимостей.
- **Правильно:** согласуй изменение, добавь миграцию и проверь `SettingsMapper` / `Validator`.
- **Файлы:** [`../../AutoKassa/Models/AppSettings.cs`](../../AutoKassa/Models/AppSettings.cs), [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs).

### 5.6. Менять порядок инициализации в App.OnStartup
- **Запрет:** не меняй последовательность миграций, разогрева кеша и создания сервисов.
- **Почему:** миграции и настройки должны быть готовы до создания остальных сервисов.
- **Правильно:** согласуй изменение и проверь старт приложения на чистой БД.
- **Файлы:** [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs).

### 5.7. Менять правила soft delete для операций-погашений
- **Запрет:** не изменяй логику `DeleteAsync` / `RestoreAsync` для кредитных карт и долгов.
- **Почему:** восстановление кредитных карт и долгов зависит от атомарности этих методов.
- **Правильно:** согласуй изменение, покрой тестами и обновь `flows.json` / `TRAPS.md`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).

---

> Последнее обновление: Этап 5 — приведение раздела 5 к формату `### N.N. Заголовок` для синхронизации с `json/traps.json` (DocValidator `TrapSyncValidator`).
