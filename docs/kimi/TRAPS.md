# TRAPS — запреты и ловушки в AutoKassa

> Этот файл собирает правила, которые легко нарушить, и объясняет, почему так делать нельзя. Прочитай перед изменением кода.

---

## 0. Легенда тегов

Каждая ловушка помечена 1–3 тегами для быстрой фильтрации по теме:

- `#architecture` — архитектура, DI, время жизни сервисов.
- `#ef-core` — Entity Framework Core: контексты, миграции, tracking.
- `#sqlite` — особенности SQLite.
- `#wpf` — WPF: UI-поток, XAML, конвертеры, CommandManager.
- `#async` — асинхронность, блокировки, threading.
- `#mvvm` — MVVM: ViewModel, команды, привязки.
- `#security` — пароли, блокировка, пути, бэкапы.
- `#testing` — тесты и тестовая инфраструктура.
- `#business` — бизнес-правила предметной области.

---

## 1. Архитектурные запреты

### 1.1. Не пиши бизнес-логику в code-behind View
- **Запрет:** обработчики событий в `.xaml.cs` не должны принимать решения, обращаться к БД или вызывать сервисы.
- **Почему:** нарушает MVVM, усложняет тестирование и приводит к дублированию логики.
- **Правильно:** используй команды и ViewModel.
- **Файлы:** [`../../AutoKassa/Views/TransactionsView.xaml.cs`](../../AutoKassa/Views/TransactionsView.xaml.cs), [`../../AutoKassa/Helpers/RelayCommand.cs`](../../AutoKassa/Helpers/RelayCommand.cs).
- **Теги:** #mvvm #architecture

### 1.2. Не используй статический Service Locator
- **Запрет:** не обращайся к статическому `IServiceProvider` или `App.GetService<T>()` из ViewModel или сервисов. В проекте такого метода нет — не добавляй.
- **Почему:** скрывает зависимости, усложняет тестирование и нарушает DI.
- **Правильно:** передавай зависимости через конструктор; фабрики (`IReportViewModelFactory`, `ITransactionEditViewModelFactory`) получают `IServiceProvider` явно.
- **Файлы:** [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs) (конфигурация DI).
- **Теги:** #architecture

### 1.3. Не делай синхронные запросы к БД
- **Запрет:** никаких `.Result`, `.Wait()`, синхронных методов EF Core (`SaveChanges`, `ToList`).
- **Почему:** блокирует UI-поток, вызывает deadlock и деградацию отзывчивости.
- **Правильно:** `await _contextFactory.CreateDbContextAsync()` + `await ...ToListAsync()`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).
- **Теги:** #async #ef-core #wpf

### 1.4. ViewModel не должна напрямую работать с DbContext
- **Запрет:** не создавай `DbContext` и не выполняй запросы из ViewModel.
- **Почему:** ViewModel отвечает за состояние экрана, а не за доступ к данным.
- **Правильно:** инжектируй сервис (`ITransactionService` и т.п.) и вызывай его методы.
- **Файлы:** [`../../AutoKassa/ViewModels/TransactionsViewModel.cs`](../../AutoKassa/ViewModels/TransactionsViewModel.cs).
- **Теги:** #mvvm #ef-core

### 1.5. Не вызывай долгие операции в конструкторе сервисов
- **Запрет:** не обращайся к БД и не блокируй поток в конструкторе Singleton-сервисов.
- **Почему:** `SettingsService` регистрируется как Singleton; загрузка настроек в конструкторе заблокирует инициализацию DI и старт приложения.
- **Правильно:** используй ленивую загрузку (`GetSettings()`) или асинхронный метод `GetSettingsAsync()`.
- **Файлы:** [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (конструктор, `LoadSettings`).
- **Теги:** #architecture #async

### 1.6. `NavigationService` передаёт параметр через reflection
- **Запрет:** не полагайся на компилятор при передаче параметра навигации: `NavigateTo<TViewModel>(object parameter)` ищет метод `Initialize` по имени через reflection (`GetMethod("Initialize")`).
- **Почему:** переименование метода `Initialize` или изменение его сигнатуры не ловится компилятором: если метод не найден, параметр молча не передаётся; если тип параметра не совпал — исключение в рантайме.
- **Правильно:** объявляй в целевой ViewModel публичный метод `Initialize` с одним параметром нужного типа; при рефакторинге ищи все вызовы `NavigateTo<T>(parameter)`.
- **Файлы:** [`../../AutoKassa/Services/NavigationService.cs`](../../AutoKassa/Services/NavigationService.cs) (`NavigateTo<TViewModel>(object)`).
- **Теги:** #architecture #mvvm

### 1.7. `DialogService` находит View по соглашению имён
- **Запрет:** не нарушай соглашение именования диалогов: `ViewModels.X.YViewModel` → `Views.X.YView`.
- **Почему:** `ResolveViewType` строит имя View заменой `ViewModels` → `Views` и `ViewModel` → `View` в полном имени типа и ищет его через `Type.GetType`; при нарушении соглашения диалог падает с `InvalidOperationException` в рантайме, а не при сборке.
- **Правильно:** держи диалоговые View и ViewModel в зеркальных пространствах имён и согласованных именах; после переименования проверяй открытие диалога вручную.
- **Файлы:** [`../../AutoKassa/Services/DialogService.cs`](../../AutoKassa/Services/DialogService.cs) (`ResolveViewType`).
- **Теги:** #architecture #wpf #mvvm

---

## 2. Бизнес-правила-ловушки

### 2.1. Только soft delete
- **Запрет:** нельзя физически удалять операции, категории и другие бизнес-сущности.
- **Почему:** необходима история и возможность восстановления данных.
- **Правильно:** устанавливай `IsDeleted = true` и используй `RestoreAsync` для отката.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`DeleteAsync`, `RestoreAsync`).
- **Теги:** #business #ef-core

### 2.2. `PaymentType.Debt` не влияет на баланс
- **Запрет:** не учитывай долговые операции при расчёте фактического баланса и прибыли.
- **Почему:** `Debt` фиксирует обязательство, а не движение денег.
- **Правильно:** баланс считай только по `Cash`, `NonCash` и `CreditCard`; погашения — обычными операциями.
- **Файлы:** [`../../AutoKassa/Services/ReportService.cs`](../../AutoKassa/Services/ReportService.cs), [`../../AutoKassa/Models/Enums/PaymentType.cs`](../../AutoKassa/Models/Enums/PaymentType.cs).
- **Теги:** #business

### 2.3. Системные категории нельзя удалить
- **Запрет:** не устанавливай `IsDeleted = true` для категорий с `IsSystem = true`.
- **Почему:** системные категории используются для специальных операций (например, "Погашение кредита").
- **Правильно:** можно деактивировать (`IsActive = false`), но не удалять.
- **Файлы:** [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs).
- **Теги:** #business

### 2.4. Уникальность названия категории в рамках типа
- **Запрет:** нельзя создать две категории с одинаковым названием внутри одного `OperationType`.
- **Почему:** в БД есть уникальный индекс `UQ_Category_Name_Type`.
- **Правильно:** проверяй существование перед добавлением.
- **Файлы:** [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs), [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs).
- **Теги:** #business #sqlite

### 2.5. Нельзя удалить категорию по умолчанию
- **Запрет:** если категория выбрана в `AppSettings.DefaultIncomeCategoryId` / `DefaultExpenseCategoryId`, её нельзя удалить.
- **Почему:** настройки ссылаются на категорию, и удаление сломает поведение по умолчанию.
- **Правильно:** сначала измени категорию по умолчанию в настройках.
- **Файлы:** [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs).
- **Теги:** #business

### 2.6. Нельзя редактировать операцию-погашение
- **Запрет:** операции, участвующие в `DebtPayment` или `CreditCardPayment` как `RepaymentTransaction`, нельзя редактировать.
- **Почему:** изменение суммы или типа погашения нарушит связи и расчёты остатков.
- **Правильно:** удалить старое погашение (soft delete) и создать новое.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`UpdateAsync`).
- **Теги:** #business

### 2.7. Нельзя удалить контрагента с операциями
- **Запрет:** удаление контрагента запрещено, если к нему привязаны операции (включая удалённые).
- **Почему:** сохраняем целостность истории.
- **Правильно:** деактивируй контрагента (`IsActive = false`).
- **Файлы:** [`../../AutoKassa/Services/CounterpartyService.cs`](../../AutoKassa/Services/CounterpartyService.cs).
- **Теги:** #business

### 2.8. Нельзя списать долг с погашениями
- **Запрет:** `DebtService.WriteOffAsync` выбросит исключение, если по долгу есть погашения.
- **Почему:** списание долга с уже внесёнными платежами приведёт к некорректной истории.
- **Правильно:** сначала удалить (soft delete) все погашения долга.
- **Файлы:** [`../../AutoKassa/Services/DebtService.cs`](../../AutoKassa/Services/DebtService.cs).
- **Теги:** #business

### 2.9. Нельзя погасить неактивную кредитную карту
- **Запрет:** погашение возможно только для активной карты (`IsActive = true`).
- **Почему:** неактивная карта исключена из оборота.
- **Правильно:** активируй карту или выбери другую.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs), [`../../AutoKassa/Services/CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs).
- **Теги:** #business

### 2.10. Погашение кредитной карты — только наличные или безналичные
- **Запрет:** для операции погашения `PaymentType` не может быть `Debt` или `CreditCard`.
- **Почему:** погашение — это реальный перевод денег, а не новый долг или кредит.
- **Правильно:** используй `Cash` или `NonCash`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`CreateCreditCardRepaymentInContextAsync`).
- **Теги:** #business

### 2.11. Нельзя удалить кредитную карту с непогашенным долгом
- **Запрет:** `CreditCardService.DeleteAsync` не позволит деактивировать карту, пока `GetCurrentDebtAsync` > 0.
- **Почему:** удаление карты с долгом приведёт к потере обязательства и нарушит баланс.
- **Правильно:** сначала погаси долг по карте.
- **Файлы:** [`../../AutoKassa/Services/CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs) (`DeleteAsync`).
- **Теги:** #business

### 2.12. Нельзя добавить покупку по неактивной карте или превысить лимит
- **Запрет:** `CreditCardOperationHelper.AddPurchaseAsync` проверяет `IsActive` и `currentDebt + amount <= Limit`.
- **Почему:** неактивная карта не должна использоваться; превышение лимита нарушает кредитное соглашение.
- **Правильно:** проверяй доступный лимит через `CreditCardService.GetAvailableLimitAsync` до создания операции.
- **Файлы:** [`../../AutoKassa/Services/CreditCardOperationHelper.cs`](../../AutoKassa/Services/CreditCardOperationHelper.cs) (`AddPurchaseAsync`).
- **Теги:** #business

### 2.13. Погашение не может превышать текущий долг по карте
- **Запрет:** сумма погашения должна быть `<= currentDebt`.
- **Почему:** избыточное погашение создаст отрицательный долг и сломает FIFO-учёт.
- **Правильно:** рассчитывай текущий долг через `CreditCardOperationHelper.GetCurrentDebtAsync` и ограничивай сумму.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`CreateCreditCardRepaymentInContextAsync`, `RestoreAsync`), [`../../AutoKassa/Services/CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs) (`RepayDebtAsync`).
- **Теги:** #business

### 2.14. Операции создания долга скрыты из списка операций по умолчанию
- **Запрет:** не отображай `PaymentType == Debt` в общем списке операций без явного фильтра.
- **Почему:** долговые операции — это обязательства; их место в отчёте по долгам.
- **Правильно:** в `TransactionFilterParameters` установи `IsDebtFilter = true`, чтобы получить долги.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`ApplyFilters`).
- **Теги:** #business

### 2.15. Нельзя создать или обновить операцию с неактивной категорией
- **Запрет:** `TransactionService.AddAsync` / `UpdateAsync` проверяют `category.IsActive`.
- **Почему:** неактивные категории исключены из выбора в UI и не должны использоваться в новых операциях.
- **Правильно:** активируй категорию или выбери активную.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`AddAsync`, `UpdateAsync`).
- **Теги:** #business

### 2.16. При смене типа оплаты с долга на обычную удаляются связи с погашениями
- **Запрет:** `UpdateAsync` удаляет записи `DebtPayment`, если операция перестаёт быть `PaymentType.Debt`.
- **Почему:** погашения останутся висячими и будут учтены как реальные доходы/расходы без связанного долга.
- **Правильно:** предупреди пользователя или запрети смену типа, если есть погашения.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`UpdateAsync`).
- **Теги:** #business #ef-core

### 2.17. Погашение долга не может превышать остаток
- **Запрет:** `DebtService.RepayAsync` проверяет `amount <= remaining`.
- **Почему:** переплата по долгу нарушит статус `Repaid` и историю платежей.
- **Правильно:** рассчитывай остаток через `GetRemainingAmountAsync` до погашения.
- **Файлы:** [`../../AutoKassa/Services/DebtService.cs`](../../AutoKassa/Services/DebtService.cs) (`RepayAsync`).
- **Теги:** #business

### 2.18. Название контрагента уникально регистронезависимо
- **Запрет:** нельзя создать двух контрагентов с одинаковым именем.
- **Почему:** в БД `Counterparty.Name` имеет `TEXT COLLATE NOCASE` и уникальный индекс `IX_Counterparty_Name`.
- **Правильно:** проверяй `ExistsAsync` перед добавлением.
- **Файлы:** [`../../AutoKassa/Services/CounterpartyService.cs`](../../AutoKassa/Services/CounterpartyService.cs), [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs).
- **Теги:** #business #sqlite

### 2.19. Не меняй флаг `IsSystem` у существующих категорий
- **Запрет:** `CategoryService.UpdateAsync` позволяет записать `existing.IsSystem = category.IsSystem`, но делать этого нельзя.
- **Почему:** системные категории ищутся по имени и `Id` в бизнес-логике (например, "Погашение кредита").
- **Правильно:** не передавай `IsSystem` из UI; новые системные категории добавляй только через seed-данные.
- **Файлы:** [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs) (`UpdateAsync`), [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs) (`SeedData`).
- **Теги:** #business

### 2.20. `InitialBalance` относится только к наличным
- **Запрет:** не добавляй начальный баланс к безналичным или кредитным расчётам.
- **Почему:** в `ReportService.GetInitialBalanceAsync` начальный баланс применяется только при `paymentType == Cash` или отсутствии фильтра.
- **Правильно:** при фильтрации по `NonCash` / `CreditCard` не учитывай `InitialBalance`.
- **Файлы:** [`../../AutoKassa/Services/ReportService.cs`](../../AutoKassa/Services/ReportService.cs) (`GetInitialBalanceAsync`).
- **Теги:** #business

### 2.21. Seed-ID категорий 1–11 зафиксированы миграциями
- **Запрет:** не меняй `Id` существующих seed-категорий (1–4 — доходы, 5–11 — расходы) и не вставляй новые категории "в середину" этого диапазона.
- **Почему:** Id 1–11 записаны в миграциях и снапшоте модели; существующие БД уже содержат эти строки, и их изменение приведёт к конфликту данных при миграции.
- **Правильно:** новые системные категории добавляй отдельной миграцией со свободными Id (12+); список seed-значений — в [`CONSTANTS.md`](CONSTANTS.md).
- **Файлы:** [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs) (`SeedData`).
- **Теги:** #business #ef-core

### 2.22. Категория «Погашение кредита» ищется по имени, а не по Id
- **Запрет:** не переименовывай системную категорию «Погашение кредита» (seed `Id = 11`) и не зашивай число 11 в новый код.
- **Почему:** бизнес-логика находит её по имени через константу `CreditCardOperationHelper.RepaymentCategoryName` (её используют `CreditCardService`, `TransactionService`, `TransactionEditViewModel`); переименование категории в БД без смены константы сломает учёт погашений. Число 11 встречается только в seed-данных и миграциях.
- **Правильно:** во всех проверках используй константу `RepaymentCategoryName`; если переименование всё же нужно — миграция данных + правка константы + обновление [`CONSTANTS.md`](CONSTANTS.md).
- **Файлы:** [`../../AutoKassa/Services/CreditCardOperationHelper.cs`](../../AutoKassa/Services/CreditCardOperationHelper.cs), [`../../AutoKassa/Services/CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs), [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).
- **Теги:** #business

---

## 3. Технические ловушки

### 3.1. SQLite `LOWER()` не поддерживает кириллицу
- **Запрет:** не используй SQL-фильтрацию по `Description` с `LOWER`/`Contains` для кириллического текста.
- **Почему:** SQLite не умеет применять `LOWER` к Unicode; поиск станет регистрозависимым или неработающим.
- **Правильно:** используй FTS5-поиск через виртуальную таблицу `TransactionsFts` (`MATCH` с префиксными фразами из `BuildFtsMatchQuery`); unicode61 делает регистронезависимый case folding, в т.ч. для кириллицы. Фильтрацию в памяти не применяй.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`GetTransactionsAsync`).
- **Теги:** #sqlite #ef-core

### 3.2. Отключай навигационные свойства из других DbContext
- **Запрет:** не добавляй в `DbContext` сущность с уже заполненными навигационными свойствами, полученными из другого контекста.
- **Почему:** EF Core будет пытаться отслеживать связанные объекты, что приводит к конфликтам tracking.
- **Правильно:** устанавливай `Category = null; CreditCard = null; Counterparty = null;` перед `Add`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`AddAsync`).
- **Теги:** #ef-core

### 3.3. `AppSettings` — single-row таблица
- **Запрет:** не создавай несколько строк `AppSettings`; `Id` всегда должен быть равен `1`.
- **Почему:** настройки приложения глобальны; код ожидает одну запись.
- **Правильно:** всегда читай/обновляй запись с `Id = 1`.
- **Файлы:** [`../../AutoKassa/Models/AppSettings.cs`](../../AutoKassa/Models/AppSettings.cs), [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs).
- **Теги:** #ef-core #business

### 3.4. Поиск по описанию — FTS5 по префиксам слов
- **Запрет:** не фильтруй описания операций в памяти и не рассчитывай на совпадение по подстроке в середине слова.
- **Почему:** поиск переведён на SQLite FTS5 (виртуальная таблица `TransactionsFts`, токенизатор unicode61, миграция `AddTransactionsFts`); `MATCH` работает по токенам с префиксом: «рем» находит «ремонт», но «ожо» не находит «похоже».
- **Правильно:** строй `MATCH`-запрос через `BuildFtsMatchQuery` (только букво-цифровые токены, в кавычках, с суффиксом `*`, пробел = AND); синхронизацию `TransactionsFts` с `Transactions` обеспечивают триггеры миграции, при физическом удалении/обновлении описания руками не дублируй их работу.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).
- **Теги:** #sqlite #ef-core

### 3.5. `CreditCardPurchase.RemainingDebt` не может быть меньше суммы погашений
- **Запрет:** не устанавливай `RemainingDebt` меньше уже произведённых погашений.
- **Почему:** это нарушит FIFO-учёт и баланс долга по карте.
- **Правильно:** при изменении суммы покупки пересчитывай `RemainingDebt = max(0, Amount - paid)`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`HandleCreditCardChangesAsync`).
- **Теги:** #business #ef-core

### 3.6. `CreditCardPayment` удаляются каскадно при удалении `CreditCardPurchase`
- **Запрет:** не полагайся на soft delete для `CreditCardPayment`; они физически удаляются при удалении покупки.
- **Почему:** в `AppDbContext` настроен `OnDelete(DeleteBehavior.Cascade)` для `CreditCardPayment -> CreditCardPurchase`.
- **Правильно:** при восстановлении покупки заново распределяй погашения через `DistributeRepaymentAsync`.
- **Файлы:** [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs), [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`RestoreAsync`).
- **Теги:** #ef-core #business

### 3.7. Настройки кешируются — изменяй только через `SaveSettingsAsync`
- **Запрет:** не мутируй объект, полученный из `GetSettings()`, напрямую вне сервиса.
- **Почему:** `_cachedSettings` разделяется между вызовами; прямая мутация может привести к рассинхронизации.
- **Правильно:** изменяй поля и вызывай `SaveSettingsAsync(settings)`, который обновит БД и кеш.
- **Файлы:** [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`GetSettings`, `SaveSettingsAsync`).
- **Теги:** #architecture

### 3.8. Путь бэкапа санитизируется
- **Запрет:** не сохраняй резервные копии за пределами профиля пользователя или каталога приложения.
- **Почему:** `SanitizeBackupPath` отклоняет пути вне разрешённых зон для защиты от path traversal.
- **Правильно:** выбирай путь внутри `%USERPROFILE%` или папки приложения.
- **Файлы:** [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`SanitizeBackupPath`, `ImportSettingsAsync`).
- **Теги:** #security

### 3.9. Восстановление из бэкапа сбрасывает пулы соединений и применяет миграции
- **Запрет:** не копируй файл бэкапа поверх открытой БД без `SqliteConnection.ClearAllPools()`.
- **Почему:** открытые хендлы заблокируют файл и восстановление упадёт; старая копия может иметь устаревшую схему.
- **Правильно:** используй `RestoreBackupAsync`, который очищает пулы, копирует файл и вызывает `MigrateAsync`.
- **Файлы:** [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`RestoreBackupAsync`).
- **Теги:** #ef-core #sqlite #security

### 3.10. Группировка по категориям в отчётах — по `CategoryId`, не по ссылке
- **Запрет:** не используй `GroupBy(t => t.Category)` при `AsNoTracking()`.
- **Почему:** EF Core создаёт отдельные экземпляры `Category` для каждой строки; группировка по ссылке даст неверный результат.
- **Правильно:** группируй по `CategoryId` и бери имя из `g.First().Category`.
- **Файлы:** [`../../AutoKassa/Services/ReportService.cs`](../../AutoKassa/Services/ReportService.cs) (`GenerateCategoryReportAsync`).
- **Теги:** #ef-core #sqlite

### 3.11. Лимиты `MaxDetailTransactions` и `MaxCategoryTransactions`
- **Запрет:** не загружай неограниченное количество операций для отчётов.
- **Почему:** большие выборки могут привести к зависанию UI и RAM-всплеску.
- **Правильно:** уважай константы `MaxDetailTransactions` / `MaxCategoryTransactions` при кастомных отчётах.
- **Файлы:** [`../../AutoKassa/Services/ReportService.cs`](../../AutoKassa/Services/ReportService.cs).
- **Теги:** #business

### 3.12. `GetSettings()` — синхронный метод с блокировкой; не вызывай до разогрева кеша
- **Запрет:** не вызывай синхронный `GetSettings()` из UI-потока до разогрева кеша и не вызывай его из конструкторов сервисов.
- **Почему:** метод берёт `_settingsLock.Wait()` и при холодном кеше выполняет синхронный запрос к БД (`LoadSettings` с синхронным `FirstOrDefault()`) — это блокирует вызывающий поток. Разогрев делается один раз при старте в `App.InitializeAsync` через `GetSettingsAsync()`.
- **Правильно:** в асинхронном коде используй `GetSettingsAsync()`; синхронный `GetSettings()` допустим только после разогрева (свойства UI, синхронные геттеры).
- **Файлы:** [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`GetSettings`, `LoadSettings`), [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs) (`InitializeAsync`).
- **См. также:** [`14-startup.md`](14-startup.md).
- **Теги:** #async #wpf

---

## 4. MVVM и команды

### 4.1. `AsyncRelayCommand` — async void: исключения уходят только в лог
- **Запрет:** не ожидай, что исключение из асинхронной команды всплывёт в UI или глобальный обработчик.
- **Почему:** `ICommand.Execute` синхронный, поэтому `Execute` объявлен `async void`; все исключения перехватываются внутри и пишутся в Serilog. Пользователь не узнает о сбое, если ты не покажешь его сам.
- **Правильно:** внутри обработчика команды показывай ошибку через `IDialogService` или toast; искать детали сбоя — в `logs/autokassa-.log`.
- **Файлы:** [`../../AutoKassa/Helpers/AsyncRelayCommand.cs`](../../AutoKassa/Helpers/AsyncRelayCommand.cs).
- **См. также:** [`PRINCIPLES.md`](PRINCIPLES.md).
- **Теги:** #mvvm #async

### 4.2. `CanExecute` не пересчитывается автоматически при смене свойств ViewModel
- **Запрет:** не ожидай мгновенного обновления доступности кнопки при программном изменении состояния.
- **Почему:** `RelayCommand` и `AsyncRelayCommand` подписывают `CanExecuteChanged` на `CommandManager.RequerySuggested`, который срабатывает на ввод пользователя, а не на изменение свойств ViewModel; кнопка может остаться в устаревшем состоянии.
- **Правильно:** после изменения свойств, влияющих на `CanExecute`, вызывай `RaiseCanExecuteChanged()` (у `RelayCommand`) или `CommandManager.InvalidateRequerySuggested()`.
- **Файлы:** [`../../AutoKassa/Helpers/RelayCommand.cs`](../../AutoKassa/Helpers/RelayCommand.cs), [`../../AutoKassa/Helpers/AsyncRelayCommand.cs`](../../AutoKassa/Helpers/AsyncRelayCommand.cs).
- **Теги:** #mvvm #wpf

---

## 5. WPF и конвертеры

### 5.1. `BoolToVisibilityConverter` считает любой не-null объект true
- **Запрет:** не биндь на него не-bool значения, ожидая осмысленной проверки «истинности».
- **Почему:** конвертер возвращает `Visible` для любого не-null объекта и для любой непустой строки; параметр `"inverse"` инвертирует результат.
- **Правильно:** для проверок строк и null используй специализированные конвертеры (например, `StringToVisibilityConverter`); полный каталог — в [`15-converters.md`](15-converters.md).
- **Файлы:** [`../../AutoKassa/Helpers/Converters/BoolToVisibilityConverter.cs`](../../AutoKassa/Helpers/Converters/BoolToVisibilityConverter.cs).
- **Теги:** #wpf

### 5.2. `DecimalToStringConverter` зависит от `CurrentCulture`
- **Запрет:** не используй его там, где нужен стабильный формат независимо от культуры ОС.
- **Почему:** форматирование (`"N2"` + " ₽") и парсинг идут через `CultureInfo.CurrentCulture`; на машине с другой культурой изменятся разделители, а fallback жёстко задан как `"0,00 ₽"` в русском формате.
- **Правильно:** для сумм операций со знаком используй `TransactionAmountConverter` (жёстко ru-RU); детали — в [`15-converters.md`](15-converters.md).
- **Файлы:** [`../../AutoKassa/Helpers/Converters/DecimalToStringConverter.cs`](../../AutoKassa/Helpers/Converters/DecimalToStringConverter.cs).
- **Теги:** #wpf

### 5.3. `TransactionAmountConverter` жёстко использует ru-RU и `N0`
- **Запрет:** не ожидай от него копеек, учёта культуры ОС или обратного преобразования.
- **Почему:** это `IMultiValueConverter` с входом `(decimal Amount, OperationType)`: формат `"N0"` в культуре ru-RU отбрасывает дробную часть, а `ConvertBack` бросает `NotImplementedException`.
- **Правильно:** передавай оба значения через `MultiBinding`; для полей ввода суммы он не подходит — см. [`15-converters.md`](15-converters.md).
- **Файлы:** [`../../AutoKassa/Helpers/Converters/TransactionAmountConverter.cs`](../../AutoKassa/Helpers/Converters/TransactionAmountConverter.cs).
- **Теги:** #wpf

### 5.4. `CategoryColorConverter` кеширует замороженные кисти
- **Запрет:** не мутируй возвращаемый `Brush` и не ожидай исключения на битом HEX.
- **Почему:** кисти кешируются в статическом `ConcurrentDictionary` и замораживаются (`Freeze()`); попытка изменить кисть выбросит исключение. Невалидный HEX молча возвращает серую кисть по умолчанию.
- **Правильно:** проверяй формат цвета до биндинга; новые конвертеры с кешем делай по тому же образцу — см. [`15-converters.md`](15-converters.md).
- **Файлы:** [`../../AutoKassa/Helpers/Converters/CategoryColorConverter.cs`](../../AutoKassa/Helpers/Converters/CategoryColorConverter.cs).
- **Теги:** #wpf

---

## 6. Частые ошибки при изменениях

### 6.1. Забыть вызвать `NotifyDataChanged()`
- **Запрет:** после изменения данных не оставляй подписчиков `IDataChangeService` без уведомления.
- **Почему:** другие экраны (Dashboard, Reports) не обновятся и покажут устаревшие данные.
- **Правильно:** вызывай `_dataChangeService.NotifyDataChanged()` после успешного сохранения.
- **Файлы:** [`../../AutoKassa/Services/IDataChangeService.cs`](../../AutoKassa/Services/IDataChangeService.cs), [`../../AutoKassa/ViewModels/TransactionsViewModel.cs`](../../AutoKassa/ViewModels/TransactionsViewModel.cs).
- **Теги:** #mvvm #architecture

### 6.2. Забыть зарегистрировать сервис или ViewModel в DI
- **Запрет:** не оставляй новый класс без регистрации в `App.ConfigureServices`.
- **Почему:** `GetRequiredService<T>()` бросит исключение при навигации или использовании.
- **Правильно:** добавь `services.AddTransient<T>()` / `AddSingleton<T>()` с правильным lifetime.
- **Файлы:** [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs).
- **Теги:** #architecture

### 6.3. Забыть добавить DataTemplate в `MainWindow.xaml`
- **Запрет:** новый ViewModel без соответствующего `DataTemplate` не отобразится.
- **Почему:** WPF не знает, какое View использовать для типа VM.
- **Правильно:** добавь `<DataTemplate DataType="{x:Type vm:NewViewModel}">` в ресурсы `MainWindow.xaml`.
- **Файлы:** [`../../AutoKassa/MainWindow.xaml`](../../AutoKassa/MainWindow.xaml).
- **Теги:** #wpf #mvvm

### 6.4. Забыть обновить миграцию после изменения модели
- **Запрет:** не меняй свойства сущностей без соответствующей миграции EF Core.
- **Почему:** приложение упадёт на старте из-за несоответствия схемы БД.
- **Правильно:** создай миграцию `dotnet ef migrations add <Name>` и протестируй на чистой БД.
- **Файлы:** [`../../AutoKassa/Migrations/`](../../AutoKassa/Migrations/).
- **Теги:** #ef-core

### 6.5. Забыть отписаться от `IDataChangeService.DataChanged`
- **Запрет:** не оставляй подписки на события Singleton-сервиса после закрытия экрана.
- **Почему:** ViewModel будет удерживаться в памяти и реагировать на чужие изменения.
- **Правильно:** в `OnDispose()` отписывайся от `_dataChangeService.DataChanged`.
- **Файлы:** [`../../AutoKassa/Helpers/ViewModelBase.cs`](../../AutoKassa/Helpers/ViewModelBase.cs), [`../../AutoKassa/Services/IDataChangeService.cs`](../../AutoKassa/Services/IDataChangeService.cs).
- **Теги:** #mvvm

### 6.6. Забыть обновить документацию при изменении правил
- **Запрет:** новые ограничения, исключения и бизнес-правила должны попадать в `TRAPS.md` и `json/traps.json`.
- **Почему:** следующий агент не узнает о новой ловушке и повторит ошибку.
- **Правильно:** добавь ловушку в `TRAPS.md` и синхронизируй `json/traps.json`.
- **Файлы:** [`TRAPS.md`](TRAPS.md), [`json/traps.json`](json/traps.json).
- **Теги:** #architecture

---

## 7. Тестовые ловушки

### 7.1. In-memory SQLite исчезает при закрытии соединения
- **Запрет:** не закрывай `SqliteConnection` до конца теста.
- **Почему:** база `DataSource=:memory:` живёт, пока открыто соединение; `TestDatabase.Create()` специально держит его открытым, а закрытие обнуляет БД посреди теста.
- **Правильно:** храни соединение в поле тестового класса и освобождай его в `Dispose()` вместе с контекстом.
- **Файлы:** [`../../AutoKassa.Tests/Infrastructure/TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs).
- **Теги:** #testing #sqlite

### 7.2. `EnsureCreated()` в тестах не равен миграциям
- **Запрет:** не считай зелёные тесты доказательством корректности миграций.
- **Почему:** `TestDatabase` создаёт схему через `EnsureCreated()` (напрямую по модели, с seed из `OnModelCreating`), а приложение — через `MigrateAsync()`. Ошибка в миграции пройдёт мимо тестов и упадёт при старте приложения.
- **Правильно:** после добавления миграции проверяй запуск приложения на чистой и на существующей БД вручную.
- **Файлы:** [`../../AutoKassa.Tests/Infrastructure/TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs), [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs) (`InitializeAsync`).
- **Теги:** #testing #ef-core

### 7.3. Время жизни `DbContext` в тестах: не переиспользуй и не читай через устаревший контекст
- **Запрет:** не переиспользуй `AppDbContext` или соединение между тестами и не перечитывай через старый контекст данные, изменённые сервисом.
- **Почему:** xUnit создаёт новый экземпляр тестового класса на каждый тест, поэтому `TestDatabase` даёт изолированную БД; сервисы работают через собственные контексты из `TestDbContextFactory`, и сущности, отслеживаемые тестовым контекстом, устаревают в его ChangeTracker.
- **Правильно:** создавай БД в конструкторе теста, освобождай в `Dispose()`; для повторного чтения создавай новый контекст из фабрики или вызывай `ctx.ChangeTracker.Clear()`.
- **Файлы:** [`../../AutoKassa.Tests/Infrastructure/TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs).
- **Теги:** #testing #ef-core

---

## 8. Что нельзя делать без согласования

### 8.1. Изменять lifetime сервисов
- **Запрет:** не меняй Singleton / Scoped / Transient у существующих сервисов без анализа.
- **Почему:** может сломать DI-граф и состояние.
- **Правильно:** согласуй изменение и проверь всех потребителей в relationships.json.
- **Файлы:** [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs).
- **Теги:** #architecture

### 8.2. Добавлять каскадное удаление
- **Запрет:** не добавляй `OnDelete(Cascade)` без согласования.
- **Почему:** проект работает по принципу soft delete и явных связей.
- **Правильно:** используй `Restrict` + явную логику soft delete.
- **Файлы:** [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs).
- **Теги:** #ef-core

### 8.3. Физически удалять данные
- **Запрет:** не используй `Remove` / `DELETE` без согласования.
- **Почему:** нарушает требование сохранения истории.
- **Правильно:** используй soft delete (`IsDeleted = true`).
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).
- **Теги:** #business #ef-core

### 8.4. Изменять системные категории
- **Запрет:** не меняй `Name`, `Id` или `IsSystem` у системных категорий.
- **Почему:** категории ищутся по имени и используются в бизнес-логике.
- **Правильно:** если нужна новая системная категория — согласуй и обновь seed-данные.
- **Файлы:** [`../../AutoKassa/Services/AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs), [`../../AutoKassa/Services/CategoryService.cs`](../../AutoKassa/Services/CategoryService.cs).
- **Теги:** #business

### 8.5. Менять схему AppSettings
- **Запрет:** не добавляй/удаляй обязательные поля `AppSettings` без анализа.
- **Почему:** single-row таблица с фиксированным `Id = 1` и широким каскадом зависимостей.
- **Правильно:** согласуй изменение, добавь миграцию и проверь `SettingsMapper` / `Validator`.
- **Файлы:** [`../../AutoKassa/Models/AppSettings.cs`](../../AutoKassa/Models/AppSettings.cs), [`../../AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs).
- **Теги:** #ef-core #architecture

### 8.6. Менять порядок инициализации в App.OnStartup
- **Запрет:** не меняй последовательность миграций, разогрева кеша и создания сервисов.
- **Почему:** миграции и настройки должны быть готовы до создания остальных сервисов.
- **Правильно:** согласуй изменение и проверь старт приложения на чистой БД.
- **Файлы:** [`../../AutoKassa/App.xaml.cs`](../../AutoKassa/App.xaml.cs).
- **См. также:** [`14-startup.md`](14-startup.md).
- **Теги:** #architecture #async

### 8.7. Менять правила soft delete для операций-погашений
- **Запрет:** не изменяй логику `DeleteAsync` / `RestoreAsync` для кредитных карт и долгов.
- **Почему:** восстановление кредитных карт и долгов зависит от атомарности этих методов.
- **Правильно:** согласуй изменение, покрой тестами и обновь `flows.json` / `TRAPS.md`.
- **Файлы:** [`../../AutoKassa/Services/TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).
- **Теги:** #business

---

> Последнее обновление: Этап 7 — добавлена легенда тегов, теги у всех ловушек, новые ловушки из аудита кода (навигация, диалоги, настройки, команды, конвертеры, seed-данные) и раздел «Тестовые ловушки». Формат `### N.N. Заголовок` сохранён для синхронизации с `json/traps.json` (DocValidator `TrapSyncValidator`).
