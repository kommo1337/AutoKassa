# TROUBLESHOOTING — устранение неполадок AutoKassa

> Справочник диагностики: **симптом → причина → решение → файлы для проверки**. Все факты сверены с кодом.
> Этот файл — про **починку**. Про запреты и ловушки при написании кода — [`TRAPS.md`](TRAPS.md), про принципы — [`PRINCIPLES.md`](PRINCIPLES.md), про запуск — [`14-startup.md`](14-startup.md), про лимиты и дефолты — [`CONSTANTS.md`](CONSTANTS.md).

---

## 1. Как пользоваться этим файлом

1. Найди симптом через Ctrl+F (текст ошибки, название экрана, операция) или по таблице ниже.
2. Примени **Решение** из найденной записи.
3. Если не помогло — смотри логи (см. §2) и проверяй файлы из записи.

| Симптом | Раздел |
|---|---|
| Не собирается, падает на старте, виснет UI | §3 |
| Миграции, `database is locked`, soft delete, поиск, даты | §4 |
| Пустой экран, навигация, диалоги, кнопки | §5 |
| Баланс, сохранение/редактирование/удаление операций | §6 |
| Лимит карты, погашения, статусы долгов | §7 |
| Пустые отчёты, неверные итоги, PDF/Excel | §8 |
| Пароль, автоблокировка, бэкап, настройки | §9 |
| Падают тесты | §10 |
| Тормоза, память | §11 |

---

## 2. Где искать логи и как их читать

- **Путь:** `<папка с AutoKassa.exe>/logs/autokassa-YYYYMMDD.log` — ротация ежедневная, хранятся последние 30 файлов. Dev-сборка: `AutoKassa/bin/Debug/net10.0-windows/logs/`; portable: `publish_portable/logs/`. Конфигурация — в начале `App.OnStartup` ([`App.xaml.cs`](../../AutoKassa/App.xaml.cs)), Serilog инициализируется **до** DI, чтобы ловить ошибки старта.
- **Не путай:** `logs/` в корне репозитория — это логи тест-раннера (`vstest`), а не приложения.
- **Формат строки** (шаблон `{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message} {Exception}`):
  ```
  2026-07-09 21:50:22.615 [INF] === AutoKassa запуск, версия 1.2.0.0 ===
  2026-07-09 21:50:24.718 [INF] Главное окно открыто
  2026-07-09 21:50:27.817 [INF] === AutoKassa завершение ===
  ```
- **Уровни:** `[INF]` информация, `[WRN]` предупреждение, `[ERR]` ошибка, `[FTL]` фатальная. Минимальный уровень — `Information`, debug-вывода нет.
- **Что искать при падении:** `[FTL]` + `Необработанное исключение UI (Dispatcher)`, `Необработанное исключение домена приложения`, `Ошибка инициализации приложения (миграция БД / настройки)`, `Ошибка построения DI контейнера`.
- **Что искать при «тихих» сбоях** (кнопка нажалась — ничего не произошло): `[ERR]` + `Ошибка выполнения асинхронной команды` (исключение проглочено `AsyncRelayCommand`), `Необработанное исключение в Task`.
- **Что искать по доменам:** ошибки БД — `Microsoft.Data.Sqlite.SqliteException`; экспорт — `Ошибка экспорта отчёта ... в PDF/Excel`; бэкап — `Ошибка создания резервной копии` / `Ошибка восстановления из бэкапа`; долги — `Статус долга ID=... изменён с ... на ...`.

```bash
# Все ошибки и фаталы за сегодня (Git Bash, из папки с exe)
grep -E "\[(ERR|FTL)\]" logs/autokassa-$(date +%Y%m%d).log
# Фатал со стектрейсом
grep -A 20 "\[FTL\]" logs/autokassa-*.log
```

---

## 3. Сборка и запуск

### 3.1. Проект не собирается
- **Симптом:** `dotnet build` падает с ошибками SDK/платформы или пакетов.
- **Причина:** нет .NET 10 SDK; сборка не на Windows (`net10.0-windows` + `<UseWPF>true</UseWPF>` требуют Windows); побился NuGet-кеш.
- **Решение:** проверь `dotnet --list-sdks` (нужен 10.x); собирай на Windows; выполни `dotnet restore`, при подозрении на кеш — `dotnet nuget locals all --clear` и повтори restore.
- **Файлы:** [`AutoKassa.csproj`](../../AutoKassa/AutoKassa.csproj), [`Directory.Build.props`](../../Directory.Build.props). Примечание: лицензия QuestPDF Community задаётся в коде (конструктор `ExportService`), предупреждение о лицензии сборку не блокирует.

### 3.2. Приложение падает или мгновенно закрывается на старте
- **Симптом:** MessageBox «Критическая ошибка... Подробности записаны в журнал logs/autokassa-*.log» и выход, либо окно вообще не появляется.
- **Причина:** сбой миграций/доступа к БД в `InitializeAsync`, ошибка построения DI-контейнера, занятый файл `AutoKassa.db` (см. §4.2).
- **Решение:** открой свежий `logs/autokassa-*.log` рядом с exe, найди `[FTL]`: `Ошибка инициализации приложения (миграция БД / настройки)` → см. §4.1/§4.2; `Ошибка построения DI контейнера` → проверь регистрации в `ConfigureServices`. Проверь права на папку приложения.
- **Файлы:** [`App.xaml.cs`](../../AutoKassa/App.xaml.cs) (`OnStartup`, `InitializeAsync`, `ShowFatalError`), [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`MigrateAsync`), [`14-startup.md`](14-startup.md).

### 3.3. Приложение закрывается сразу после окна первоначальной настройки
- **Симптом:** окно установки пароля закрыли — приложение завершилось.
- **Причина:** это штатное поведение: если `InitialSetupView` закрыт без `DialogResult = true`, вызывается `Shutdown()`; окно показывается, когда `IsPasswordSet()` видит пустой `PasswordHash` в настройках.
- **Решение:** пройди настройку пароля до конца. Если окно появляется при уже заданном пароле — проверь `PasswordHash` в таблице `AppSettings` (запись `Id = 1`) и кеш `SettingsService`.
- **Файлы:** [`App.xaml.cs`](../../AutoKassa/App.xaml.cs) (блок `IsPasswordSet`), [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs).

### 3.4. UI зависает (на старте или при работе)
- **Симптом:** окно не отвечает на секунды/навсегда.
- **Причина:** синхронная блокировка UI-потока: `.Result`/`.Wait()` (запрещено, [`TRAPS.md`](TRAPS.md) §1.3); синхронный `SettingsService.GetSettings()` до разогрева кеша; тяжёлая выборка без лимита.
- **Решение:** убери синхронные ожидания — только `await`; разогрев кеша настроек выполняется в `InitializeAsync` при старте — не удаляй его; для поиска/отчётов уважай лимиты (см. §11).
- **Файлы:** [`App.xaml.cs`](../../AutoKassa/App.xaml.cs) (`InitializeAsync`), [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`GetSettings`, `GetSettingsAsync`).

---

## 4. EF Core и SQLite

### 4.1. Ошибка применения миграций
- **Симптом:** на старте `[FTL] Ошибка инициализации приложения` с исключением EF Core (`no such column`, `table ... already exists` и т.п.).
- **Причина:** модель изменена без миграции ([`TRAPS.md`](TRAPS.md) §4.4); БД от старой версии; повреждённая миграция.
- **Решение:** создай миграцию `cd AutoKassa && dotnet ef migrations add <Имя>` и проверь на чистой БД; существующую БД обнови `dotnet ef database update`; посмотри, какая миграция падает, в [`Migrations/`](../../AutoKassa/Migrations/).
- **Файлы:** [`AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs), [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`MigrateAsync`), [`03-database.md`](03-database.md).

### 4.2. `SQLite Error 5: database is locked`
- **Симптом:** `SqliteException: SQLite Error 5: 'database is locked'` при записи или на старте.
- **Причина:** файл БД открыт другим процессом (второй экземпляр приложения, DB Browser, sqlite3 CLI) или висят хендлы после копирования файла поверх открытой БД.
- **Решение:** закрой второй экземпляр и просмотрщики БД; восстановление из бэкапа выполняй только через `RestoreBackupAsync` — он вызывает `SqliteConnection.ClearAllPools()`; убедись, что при старте применился `PRAGMA journal_mode=WAL` (делает `SettingsService.MigrateAsync`).
- **Файлы:** [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`MigrateAsync`, `RestoreBackupAsync`), [`AppDbContext.cs`](../../AutoKassa/Services/AppDbContext.cs) (`GetDatabasePath`, путь `AutoKassa.db` в папке приложения).

### 4.3. Данные «пропали» после удаления
- **Симптом:** операция/категория исчезла из списков и отчётов.
- **Причина:** удаление — soft delete (`IsDeleted = true`), запросы фильтруют `Where(t => !t.IsDeleted)`; физически запись осталась в БД.
- **Решение:** проверь запись: `sqlite3 AutoKassa.db "SELECT Id, IsDeleted FROM Transactions WHERE Id=<id>;"`; восстанови через `TransactionService.RestoreAsync(id)` (атомарно восстанавливает покупки/погашения/статусы долгов).
- **Файлы:** [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`DeleteAsync`, `RestoreAsync`), [`06-business-logic.md`](06-business-logic.md) §1.2.

### 4.4. Поиск по описанию не находит операции
- **Симптом:** поиск возвращает неполный результат или «не видит» существующую операцию.
- **Причина:** поиск использует SQLite FTS5 (`TransactionsFts`, unicode61) и работает по префиксам слов: «рем» находит «ремонт», но «ожо» не находит «похоже» — подстрока в середине слова не совпадает. Также возможно рассинхрон индекса при ручной правке БД мимо триггеров.
- **Решение:** ищи по началу слова; при подозрении на рассинхрон пересоздай индекс: `DELETE FROM TransactionsFts; INSERT INTO TransactionsFts(rowid, Description) SELECT Id, Description FROM Transactions;`. Не возвращай фильтрацию в память — это регрессия ([`TRAPS.md`](TRAPS.md) §3.1, §3.4).
- **Файлы:** [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`GetTransactionsAsync`, `GetTotalCountAsync`, `BuildFtsMatchQuery`).

### 4.5. Операции за крайний день периода не попадают в выборку
- **Симптом:** новый запрос/отчёт теряет операции за последний день периода.
- **Причина:** в проекте верхняя граница — **исключающая**: `t.Date < DateTo.Date.AddDays(1)`; собственный запрос с `t.Date <= DateTo` обрезает весь день, кроме полуночи.
- **Решение:** копируй паттерн из `ApplyFilters` / `ReportService`: нормализуй `DateTo` через `.Date.AddDays(1)` и используй строгое `<`.
- **Файлы:** [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`ApplyFilters`), [`ReportService.cs`](../../AutoKassa/Services/ReportService.cs).

---

## 5. Навигация и UI

### 5.1. Экран не отображается (пустая область контента)
- **Симптом:** навигация прошла, но в `MainWindow` пусто.
- **Причина:** для ViewModel нет `DataTemplate` в ресурсах `MainWindow.xaml` — WPF не знает, какое View рисовать ([`TRAPS.md`](TRAPS.md) §4.3).
- **Решение:** добавь `<DataTemplate DataType="{x:Type vm:NewViewModel}">` с соответствующим View; проверь, что VM зарегистрирована в DI (иначе была бы ошибка, см. §5.2).
- **Файлы:** [`MainWindow.xaml`](../../AutoKassa/MainWindow.xaml), [`App.xaml.cs`](../../AutoKassa/App.xaml.cs) (`ConfigureServices`).

### 5.2. Навигация падает с `InvalidOperationException` («No service for type...»)
- **Симптом:** при `NavigateTo<TViewModel>()` исключение из `GetRequiredService`.
- **Причина:** ViewModel не зарегистрирована в DI-контейнере ([`TRAPS.md`](TRAPS.md) §4.2).
- **Решение:** зарегистрируй VM в `ConfigureServices` (page ViewModel вкладки — `AddSingleton` + `IPageViewModel`, модальную — `AddTransient`); собери заново.
- **Файлы:** [`App.xaml.cs`](../../AutoKassa/App.xaml.cs), [`NavigationService.cs`](../../AutoKassa/Services/NavigationService.cs).

### 5.3. Параметр навигации не доходит до ViewModel
- **Симптом:** `NavigateTo<TViewModel>(parameter)` отрабатывает, но VM параметр не получает.
- **Причина:** параметр передаётся через reflection: вызывается публичный метод `Initialize` **по имени**; если метода нет — вызов молча пропускается (`?.Invoke`), если сигнатура не совпадает — рантайм-исключение.
- **Решение:** объяви `public void Initialize(<ТипПараметра> value)` точно с этим именем; при переименовании/смене сигнатуры обнови все вызовы `NavigateTo` с параметром.
- **Файлы:** [`NavigationService.cs`](../../AutoKassa/Services/NavigationService.cs) (`NavigateTo<TViewModel>(object)`).

### 5.4. Диалог не открывается: «Не найден View '...' для ViewModel '...'»
- **Симптом:** `InvalidOperationException` из `DialogService.ShowDialog`.
- **Причина:** View ищется по соглашению имён: `ViewModels.X.YViewModel` → `Views.X.YView` (строковая замена в полном имени типа + `Type.GetType`). Нарушение соглашения (другое имя/namespace) ломает резолв.
- **Решение:** назови View строго по соглашению и положи в зеркальный namespace `Views.*`; учитывай, что замена `ViewModel`→`View` применяется ко **всему** имени.
- **Файлы:** [`DialogService.cs`](../../AutoKassa/Services/DialogService.cs) (`ResolveViewType`).

### 5.5. Кнопка нажимается, но ничего не происходит
- **Симптом:** команда выполняется молча: ни результата, ни ошибки.
- **Причина:** `AsyncRelayCommand.Execute` — `async void`, исключения перехватываются и пишутся в лог (`[ERR] Ошибка выполнения асинхронной команды`); UI о сбое не узнаёт. Повторный запуск блокируется флагом `_isExecuting`.
- **Решение:** смотри `[ERR]` в логе (§2); в самом командном методе лови ошибку и показывай toast/диалог вручную; проверь `CanExecute` — во время выполнения он возвращает `false`.
- **Файлы:** [`AsyncRelayCommand.cs`](../../AutoKassa/Helpers/AsyncRelayCommand.cs), [`PRINCIPLES.md`](PRINCIPLES.md).

---

## 6. Операции и баланс

### 6.1. Баланс не сходится
- **Симптом:** начальный/конечный баланс в отчёте не совпадает с ожиданием.
- **Причина:** `InitialBalance` из настроек прибавляется **только к наличным** (при `paymentType == Cash` или без фильтра); операции `PaymentType.Debt` исключены из итогов и прибыли; кредитные покупки не уменьшают фактический баланс (`FactBalance` = Cash + NonCash, `NetBalance = FactBalance − TotalCreditDebt`).
- **Решение:** сверяй цифры при том же фильтре типа оплаты; проверь `InitialBalance` в настройках; помни, что погашение кредитки — реальный расход, а сама покупка по кредитке — нет.
- **Файлы:** [`ReportService.cs`](../../AutoKassa/Services/ReportService.cs) (`GetInitialBalanceAsync`, `GetFactBalanceAsync`), [`06-business-logic.md`](06-business-logic.md) §7.

### 6.2. Операция не сохраняется (ошибка валидации)
- **Симптом:** `ArgumentException`/`InvalidOperationException` при добавлении или обновлении.
- **Причина:** одно из правил `AddAsync`/`UpdateAsync`: сумма должна быть > 0; категория существует и активна; для `PaymentType.Debt` обязателен контрагент.
- **Решение:** проверь текст исключения: «Сумма операции должна быть больше нуля», «Категория ID=... не найдена или неактивна», «Для долговой операции необходимо выбрать контрагента» — и исправь входные данные.
- **Файлы:** [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`AddAsync`, `UpdateAsync`).

### 6.3. «Редактирование операции-погашения запрещено»
- **Симптом:** `InvalidOperationException` при сохранении отредактированной операции.
- **Причина:** операция связана с `DebtPayment` или `CreditCardPayment` как погашение — её редактирование запрещено, чтобы не сломать расчёты остатков ([`TRAPS.md`](TRAPS.md) §2.6).
- **Решение:** удали старое погашение (soft delete) и создай новое; в UI проверяй заранее через `IsRepaymentTransactionAsync(id)`.
- **Файлы:** [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`UpdateAsync`, `IsRepaymentTransactionAsync`).

### 6.4. Операции «пропали» из списка после фильтра/поиска
- **Симптом:** список операций не показывает часть записей.
- **Причина:** по умолчанию `ApplyFilters` скрывает операции создания долга (`PaymentType.Debt`) — они видны только при `IsDebtFilter = true`; при поиске действует лимит 5000 кандидатов (см. §4.4); возможна пагинация (`Skip`/`Take`).
- **Решение:** включи фильтр долгов, если ищешь долговые операции; сузь период; проверь текущую страницу.
- **Файлы:** [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`ApplyFilters`), [`TransactionListService.cs`](../../AutoKassa/Services/Transactions/TransactionListService.cs).

---

## 7. Кредитные карты и долги

### 7.1. Погашение не распределяется по покупкам (FIFO)
- **Симптом:** погашение создано, но `RemainingDebt` покупок не уменьшился.
- **Причина:** распределение срабатывает только для операции с категорией ровно «Погашение кредита» (тип `Expense`) **и** заполненным `CreditCardId`; имя категории — зашитая константа, переименование системной категории (seed Id = 11) ломает механизм.
- **Решение:** создавай погашение через `CreditCardService.RepayDebtAsync` или с системной категорией и выбранной картой; не переименовывай категорию «Погашение кредита».
- **Файлы:** [`CreditCardOperationHelper.cs`](../../AutoKassa/Services/CreditCardOperationHelper.cs) (`RepaymentCategoryName`, `DistributeRepaymentAsync`), [`CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs).

### 7.2. «Покупка превышает кредитный лимит карты»
- **Симптом:** `InvalidOperationException` при создании операции по кредитке.
- **Причина:** проверка `currentDebt + amount > card.Limit`; в `currentDebt` входит и `InitialDebt` карты (`InitialDebt + покупки − погашения`), о нём часто забывают.
- **Решение:** проверь доступный лимит через `GetAvailableLimitAsync` перед операцией; уменьши сумму, погаси долг или подними `Limit` в карточке.
- **Файлы:** [`CreditCardOperationHelper.cs`](../../AutoKassa/Services/CreditCardOperationHelper.cs) (`AddPurchaseAsync`, `GetCurrentDebtAsync`).

### 7.3. «Сумма погашения превышает текущий долг по карте»
- **Симптом:** `InvalidOperationException` при погашении кредитки.
- **Причина:** погашение больше `GetCurrentDebtAsync`; также погашение возможно только `Cash`/`NonCash` и только по активной карте.
- **Решение:** запроси текущий долг (`CreditCardService.GetCurrentDebtAsync`) и ограничь сумму; проверь `IsActive` карты и тип оплаты.
- **Файлы:** [`CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs) (`RepayDebtAsync`), [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`CreateCreditCardRepaymentInContextAsync`).

### 7.4. Статус долга не обновляется
- **Симптом:** долг погашён, но статус остался `Active` (или наоборот).
- **Причина:** статус пересчитывается только в `RepayAsync`, `DeleteAsync`, `RestoreAsync`; списанный долг (`WrittenOff`) пересчёту не подлежит (guard в `RecalculateStatusAsync`). Погашения с `IsDeleted = true` в сумму не входят.
- **Решение:** проверь записи `DebtPayments` и флаг `IsDeleted` у операций-погашений; для принудительного пересчёта вызови `DebtService.RecalculateStatusAsync(debtId)`; в логе ищи `Статус долга ID=... изменён`.
- **Файлы:** [`DebtService.cs`](../../AutoKassa/Services/DebtService.cs), [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`RecalculateDebtStatusInContextAsync`).

### 7.5. Нельзя списать долг / удалить карту
- **Симптом:** «Нельзя списать долг, по которому есть погашения» или «Нельзя удалить карту с непогашенным долгом».
- **Причина:** защитные инварианты: списание долга с платежами оставит «висячие» реальные операции; удаление (деактивация) карты с долгом потеряет обязательство.
- **Решение:** для долга — сначала удали (soft delete) все погашения, затем `WriteOffAsync`; для карты — погаси долг до нуля (`GetCurrentDebtAsync`), затем `DeleteAsync`.
- **Файлы:** [`DebtService.cs`](../../AutoKassa/Services/DebtService.cs) (`WriteOffAsync`), [`CreditCardService.cs`](../../AutoKassa/Services/CreditCardService.cs) (`DeleteAsync`).

---

## 8. Отчёты и экспорт

### 8.1. Пустой отчёт
- **Симптом:** отчёт сформировался без строк/с нулевыми суммами.
- **Причина:** в периоде нет неудалённых операций, подходящих под фильтры (тип, категория, тип оплаты); напоминание: долговые операции в итоги баланса/детализации не входят; отчёт по долгам берёт только `PaymentType.Debt` со статусом ≠ `NotDebt`.
- **Решение:** расширь период, сбрось фильтры; проверь, не удалены ли операции (§4.3); для детализации/категорий учитывай лимит 1000 записей (§11.2).
- **Файлы:** [`ReportService.cs`](../../AutoKassa/Services/ReportService.cs), [`10-reports.md`](10-reports.md).

### 8.2. Неверные итоги в отчёте
- **Симптом:** суммы/проценты по категориям или баланс выглядят неправдоподобно.
- **Причина:** типичные источники расхождений: `InitialBalance` учтён только для наличных; долги исключены из итогов; при `AsNoTracking()` группировка по ссылке `Category` даёт неверные группы — группировать нужно по `CategoryId` ([`TRAPS.md`](TRAPS.md) §3.10); детализация обрезана лимитом.
- **Решение:** сверь логику с `GenerateCategoryReportAsync` / `GenerateBalanceReportAsync`; в новых отчётах группируй по `CategoryId`, имя бери из `g.First().Category`.
- **Файлы:** [`ReportService.cs`](../../AutoKassa/Services/ReportService.cs).

### 8.3. Не работает экспорт PDF
- **Симптом:** ошибка при экспорте, файл не создаётся; в логе `[ERR] Ошибка экспорта отчёта ... в PDF`.
- **Причина:** QuestPDF требует установки лицензии (задаётся `LicenseType.Community` в конструкторе `ExportService`); файл с тем же именем открыт в просмотрщике; нет доступа к папке экспорта.
- **Решение:** читай стектрейс в логе; закрой открытый PDF; проверь, что экспорт идёт через DI-синглтон `IExportService` (лицензия установлена); проверь доступность `Documents/AutoKassa/Exports`.
- **Файлы:** [`ExportService.cs`](../../AutoKassa/Services/ExportService.cs).

### 8.4. Не работает экспорт Excel
- **Симптом:** исключение при сохранении `.xlsx`; в логе `[ERR] Ошибка экспорта отчёта ... в Excel`.
- **Причина:** чаще всего целевой файл открыт в Excel (блокировка) либо нет прав на папку экспорта; ClosedXML пишет весь workbook одним `SaveAs`.
- **Решение:** закрой файл в Excel и повтори; проверь путь из лога (`Экспорт Excel (...): {FilePath}`); при повторяющихся ошибках прав — смени папку профиля пользователя.
- **Файлы:** [`ExportService.cs`](../../AutoKassa/Services/ExportService.cs).

### 8.5. Файл экспорта «не найден» / не открывается
- **Симптом:** экспорт прошёл без ошибок, но пользователь не может найти файл.
- **Причина:** файлы складываются в `%USERPROFILE%/Documents/AutoKassa/Exports` с именами вида `Баланс_01.07.2026-31.07.2026_20260730_134500.pdf` — не в папку приложения.
- **Решение:** точный путь — в логе по строке `Экспорт PDF/Excel (...): {FilePath}`; открой папку `Documents/AutoKassa/Exports`.
- **Файлы:** [`ExportService.cs`](../../AutoKassa/Services/ExportService.cs) (`_exportFolder`).

---

## 9. Настройки и безопасность

### 9.1. Пароль не принимается
- **Симптом:** правильный (по мнению пользователя) пароль отклоняется на входе/смене.
- **Причина:** проверка — `BCrypt.Verify` против `PasswordHash` в `AppSettings`; требования длины различаются: первичная установка ≥ 6, восстановление ≥ 6, смена пароля ≥ 4 (несогласованность задокументирована в [`CONSTANTS.md`](CONSTANTS.md)).
- **Решение:** проверь раскладку/регистр; при забытом пароле — восстановление по контрольному вопросу; для отладки убедись, что `PasswordHash` не пуст (пустой → экран первичной настройки).
- **Файлы:** [`PasswordService.cs`](../../AutoKassa/Services/PasswordService.cs), [`InitialSetupViewModel.cs`](../../AutoKassa/ViewModels/InitialSetupViewModel.cs), [`ChangePasswordViewModel.cs`](../../AutoKassa/ViewModels/ChangePasswordViewModel.cs), [`11-settings-security.md`](11-settings-security.md).

### 9.2. Автоблокировка не работает (или работает при «выключенной»)
- **Симптом:** приложение не блокируется по таймауту — либо блокируется, хотя автоблокировка «отключена».
- **Причина:** `LockService.StartAutoLockTimer` не стартует таймер в двух случаях: `AutoLockEnabled == false` **или** `AutoLockTimeout <= 0`. Таймер запускается в конструкторе `MainWindowViewModel` и сбрасывается после успешной разблокировки. Если настройки менялись в процессе работы, таймер перечитает их только при следующем запуске/сбросе.
- **Решение:** проверь обе настройки: флаг «Автоблокировка» (`AutoLockEnabled`) и таймаут 1–60 минут (диапазон валидатора); проверь, что `LockScreenView` доступен и пароль задан, иначе блокировка бессмысленна. Экран блокировки при старте — отдельная настройка `RequirePasswordOnStartup` (см. [`14-startup.md`](14-startup.md) §4).
- **Файлы:** [`LockService.cs`](../../AutoKassa/Services/LockService.cs), [`MainWindowViewModel.cs`](../../AutoKassa/ViewModels/MainWindowViewModel.cs), [`SettingsValidator.cs`](../../AutoKassa/Services/Settings/SettingsValidator.cs).

### 9.3. Бэкап не создаётся
- **Симптом:** в папке бэкапов нет новых файлов; ошибок пользователь не видит.
- **Причина:** авто-бэкап молча пропускается, если `BackupEnabled = false` или `BackupPath` пуст; при импорте настроек `SanitizeBackupPath` отклоняет пути вне профиля пользователя и папки приложения (возвращает `null`); реальные ошибки пишутся в лог (`Ошибка создания резервной копии в {BackupPath}`, `файл БД не найден`).
- **Решение:** включи бэкап и задай путь внутри `%USERPROFILE%`; проверь лог; вручную — через `CreateBackupAsync` (использует SQLite Online Backup API, WAL не мешает).
- **Файлы:** [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`RunAutoBackupIfDueAsync`, `CreateBackupAsync`, `SanitizeBackupPath`).

### 9.4. Восстановление из бэкапа не срабатывает
- **Симптом:** после «восстановления» данные прежние; в логе `Ошибка восстановления из бэкапа`.
- **Причина:** файл бэкапа отсутствует; БД занята сторонним процессом; восстановление делалось ручным копированием поверх открытой БД (без `ClearAllPools`) — хендлы блокируют замену.
- **Решение:** восстанавливай только через `RestoreBackupAsync` — он очищает пулы соединений, копирует файл, применяет миграции и перечитывает кеш настроек; закрой просмотрщики БД; проверь, что файл — `AutoKassa_Backup_yyyyMMdd_HHmmss.db`.
- **Файлы:** [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs) (`RestoreBackupAsync`).

### 9.5. Настройки не сохраняются / сбрасываются
- **Симптом:** после сохранения значения возвращаются к старым или отклоняются.
- **Причина:** `SettingsValidator` отклоняет значения вне диапазонов (автоблокировка 1–60 мин, размер страницы 10–100, авто-бэкап 1–365 дн., хранение 1–100 копий, день платежа 1–31, процент 0–100, путь ≤ 500 символов); прямая мутация закешированного `AppSettings` без `SaveSettingsAsync` не попадает в БД ([`TRAPS.md`](TRAPS.md) §3.7).
- **Решение:** держи значения в диапазонах валидатора; изменения сохраняй только через `SaveSettingsAsync` (обновляет и БД, и кеш); помни, что `AppSettings` — single-row таблица, `Id = 1`.
- **Файлы:** [`SettingsValidator.cs`](../../AutoKassa/Services/Settings/SettingsValidator.cs), [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs), [`CONSTANTS.md`](CONSTANTS.md).

---

## 10. Тесты

### 10.1. Тест падает с `SQLite Error 1: 'no such table: ...'`
- **Симптом:** тест сервиса падает на первом запросе к БД.
- **Причина:** SQLite in-memory база живёт, пока открыто соединение; `TestDatabase.Create()` открывает `SqliteConnection("DataSource=:memory:")` и вызывает `EnsureCreated()` — если закрыть соединение или создать контекст на другом соединении, база исчезает.
- **Решение:** используй `(context, connection) = TestDatabase.Create()` и `connection.Dispose()` только в конце теста; для сервисов с фабрикой — `CreateWithFactory()`.
- **Файлы:** [`TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs), [`12-tests.md`](12-tests.md).

### 10.2. `EnsureCreated()` проходит, но приложение падает
- **Симптом:** тесты зелёные, а на реальной БД — ошибки схемы.
- **Причина:** `EnsureCreated()` строит схему напрямую из модели, **минуя миграции**; расхождения (ручные миграции, данные, индексы) в тестах не видны.
- **Решение:** не полагайся на тесты для проверки миграций: прогони `cd AutoKassa && dotnet ef database update` на чистой файловой БД и запусти приложение на ней.
- **Файлы:** [`TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs), [`Migrations/`](../../AutoKassa/Migrations/).

### 10.3. Тест видит «старые»/чужие данные
- **Симптом:** тест зависит от порядка запуска, находит записи, которые не создавал.
- **Причина:** переиспользование контекста/соединения между тестами; незакоммиченные изменения в общем контексте; кеш настроек `SettingsService` переживает вызовы.
- **Решение:** каждому тесту — новый `TestDatabase.Create()`; не шари `AppDbContext` между тестами; для `SettingsService` учитывай кеш и при необходимости вызывай `SaveSettingsAsync`/`ResetToDefaultsAsync`.
- **Файлы:** [`TestDatabase.cs`](../../AutoKassa.Tests/Infrastructure/TestDatabase.cs), [`SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs).

---

## 11. Производительность

### 11.1. Подвисание списка операций при поиске / больших данных
- **Симптом:** экран операций тормозит при вводе текста в поиск или на большом объёме записей.
- **Причина:** сам поиск выполняется в SQLite FTS5 и не материализует записи в память; типичные причины тормозов — широкая выборка без фильтров, большие страницы (`_pageSize = 100`) и перестроение групп при каждом дебаунсе поиска.
- **Решение:** сужай выборку фильтрами (период, категория, тип); проверь, что `TransactionsFts` существует и поиск идёт через `MATCH`, а не fallback (см. §4.4).
- **Файлы:** [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs) (`GetTransactionsAsync`), [`CONSTANTS.md`](CONSTANTS.md).

### 11.2. Высокое потребление RAM при отчётах
- **Симптом:** рост памяти и подвисания при формировании отчётов за большой период.
- **Причина:** отчёт «Структура по категориям» лениво подгружает операции категории (лимит `MaxCategoryTransactions = 1000` на категорию), «Детализация» ограничена `MaxDetailTransactions = 1000`; самостоятельные выборки без лимита и `AsNoTracking()` усугубляют проблему.
- **Решение:** используй SQL-агрегации (`GetPeriodTotalsAsync`, `GetDailyTotalsAsync`) вместо материализации; уважай лимиты; для списков — `AsNoTracking()` и пагинация.
- **Файлы:** [`ReportService.cs`](../../AutoKassa/Services/ReportService.cs), [`TransactionService.cs`](../../AutoKassa/Services/TransactionService.cs).

---

> Последнее обновление: Этап 7 — создание файла по результатам аудита кода (этап 7 плана `maria-hill-firestorm-blue-marvel-stage7.md`).
