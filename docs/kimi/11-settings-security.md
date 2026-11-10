# 11. Настройки, резервное копирование и безопасность

> Этот файл описывает подсистему настроек, backup и безопасности AutoKassa.

---

## 1. `AppSettings` — модель настроек

**Файл:** [`AutoKassa/Models/AppSettings.cs`](../../AutoKassa/Models/AppSettings.cs)

Single-row таблица, `Id = 1`. Основные группы полей:

| Группа | Поля |
|--------|------|
| Security | `PasswordHash`, `SecurityQuestionId`, `SecurityAnswerHash`, `CustomSecurityQuestion`, `RequirePasswordOnStartup`, `PasswordExpireDays` |
| Auto-lock | `AutoLockEnabled`, `AutoLockTimeout` |
| UI | `Theme`, `Language`, `WindowWidth`, `WindowHeight`, `ShowNotifications`, `ShowOperationsInSidebar` |
| Операции | `DefaultOperationType`, `DefaultIncomeCategoryId`, `DefaultExpenseCategoryId`, `DefaultPaymentType`, `DefaultPageSize`, `ConfirmDelete` |
| Backup | `BackupEnabled`, `BackupPath`, `AutoBackupDays`, `BackupFrequency`, `BackupKeepCount` |
| Финансы | `InitialBalance` |
| Кредитная карта | `CreditCardLimit`, `CreditCardCurrentDebt`, `CreditCardInterestRate`, `CreditCardPaymentDay`, `CreditCardLastPaymentDate`, `CreditCardMinimumPaymentPercent` |

> Поля `ShowNotifications`, `ShowOperationsInSidebar` и `ConfirmDelete` сохранены в модели и БД, но их чекбоксы удалены из `SettingsView.xaml` (настройки не работали) — редактируются только в коде/БД.

---

## 2. `SettingsService`

**Файл:** [`AutoKassa/Services/SettingsService.cs`](../../AutoKassa/Services/SettingsService.cs)

**Интерфейс:** [`ISettingsService.cs`](../../AutoKassa/Services/ISettingsService.cs)

**Lifetime:** Singleton

**Зависимость:** `IDbContextFactory<AppDbContext>`

### 2.1. Кеширование

- `_cachedSettings` — ленивая загрузка при первом вызове `GetSettings()`/`GetSettingsAsync()`.
- `_settingsLock` (`SemaphoreSlim`) — потокобезопасность.
- В конструкторе **не** загружает настройки, чтобы не блокировать инициализацию DI.

### 2.2. Ключевые методы

| Метод | Описание |
|---|---|
| `MigrateAsync` | Применяет миграции + `PRAGMA journal_mode=WAL`. |
| `GetSettings()` / `GetSettingsAsync()` | Синхронное/асинхронное чтение с кеша. |
| `SaveSettingsAsync(AppSettings)` | Сохраняет и обновляет кеш. |
| `SetPasswordAsync` / `UpdatePasswordAsync` | Управление паролем. |
| `ResetToDefaultsAsync` | Сброс с сохранением пароля. |
| `ExportSettingsAsync` / `ImportSettingsAsync` | JSON экспорт/импорт. |
| `CreateBackupAsync` / `RestoreBackupAsync` | Резервное копирование БД (проброс `CancellationToken`). |
| `RunAutoBackupIfDueAsync` | Авто-бэкап по расписанию (проброс `CancellationToken`). |
| `SetYandexDiskLastBackupUtcAsync(DateTime?, CancellationToken)` | Точечная фиксация даты последней облачной выгрузки (UTC) для фоновых сервисов; сброс — `null`. |

### 2.3. Миграции

- Применяются автоматически при старте в [`App.OnStartup()`](../../AutoKassa/App.xaml.cs) через `SettingsService.MigrateAsync()`.
- WAL-режим включается для лучшей конкурентности.
- После восстановления из бэкапа вызывается `MigrateAsync`.

---

## 3. Import/Export настроек в JSON

**Файл:** [`AutoKassa/Services/Settings/SettingsMapper.cs`](../../AutoKassa/Services/Settings/SettingsMapper.cs)

### 3.1. `SettingsExportData`

- Содержит копию всех настроек, **кроме паролей и секретных вопросов**.
- Облачный флаг `YandexDiskBackupEnabled` экспортируется/импортируется; `YandexDiskLastBackupUtc` — **нет** (дата привязана к локальному аккаунту Яндекса, между установками не переносится, TRAPS §3.18). OAuth-файл токена никогда не экспортируется.
- `ExportDate` — дата экспорта.

### 3.2. Экспорт

- `SaveFileDialog` с фильтром `JSON файлы (*.json)|*.json`.
- Имя файла: `AutoKassa_Settings_{yyyyMMdd}.json`.
- `JsonSerializerOptions.WriteIndented = true`.

### 3.3. Импорт

- `OpenFileDialog` с фильтром JSON.
- Десериализация, копирование полей в `AppSettings`.
- `BackupPath` санитизируется через `SanitizeBackupPath()`.

---

## 4. Резервное копирование

### 4.1. `BackupCoordinator`

**Файл:** [`AutoKassa/Services/Backup/BackupCoordinator.cs`](../../AutoKassa/Services/Backup/BackupCoordinator.cs)

**Интерфейс:** [`IBackupCoordinator.cs`](../../AutoKassa/Services/Backup/IBackupCoordinator.cs)

UI-координатор:
- `SelectBackupFolder()` — диалог выбора папки.
- `CreateBackupAsync(targetPath)` — если путь пуст, открывает диалог.
- `RestoreBackupAsync()` — диалог выбора `.db`, при успехе перезапускает приложение.

### 4.2. `SettingsService.CreateBackupAsync` / `CreateSqliteSnapshotAsync`

- Используют `SqliteConnection.BackupDatabase` (SQLite Online Backup API).
- Не закрывают соединения, не используют SQL-инъекций.
- Имя файла: `AutoKassa_Backup_{yyyyMMdd_HHmmss}.db`.
- `CreateSqliteSnapshotAsync` — общая фабрика снимка без побочных эффектов: ошибки пробрасываются (`InvalidOperationException` — файл БД не найден, отмена — `OperationCanceledException`); её использует и облачная очередь (`.staging`).
- `CreateBackupAsync` — локальный сценарий: снимок + ротация старых бэкапов сверх `BackupKeepCount`; ошибки и отмена → null.

### 4.3. `SettingsService.RestoreBackupAsync`

- `SqliteConnection.ClearAllPools()` — закрывает хендлы.
- `File.Copy(backupFilePath, dbPath, overwrite: true)`.
- Применяет миграции.
- Сбрасывает и перезагружает `_cachedSettings`.

### 4.4. Автобэкап

- Координатор расписания — `IAutomaticBackupService`/`AutomaticBackupService` (Singleton): немедленная проверка после открытия главного окна, затем `PeriodicTimer` раз в час; локальная часть делегируется `RunAutoBackupIfDueAsync` (правила `BackupEnabled`/`AutoBackupDays` внутри неё), облачная выполняется только при `YandexDiskBackupEnabled`.
- `RunAutoBackupIfDueAsync` проверяет `BackupEnabled` и `AutoBackupDays`.
- Дата последнего бэкапа определяется по имени файла.
- Стартовые, таймерные и внеочередные запуски сериализованы `SemaphoreSlim`; исключения цикла логируются внутри и не останавливают расписание.
- При выходе `App.OnExit` вызывает `StopAsync()` (отмена активной операции + ожидание до 10 с) до `Log.CloseAndFlush()` и Dispose DI (TRAPS §3.19).

### 4.5. Облачный бэкап на Яндекс Диск (этапы 1–5 из `YandexBackUp.md`)

Отдельная подсистема в [`AutoKassa/Services/Backup/YandexDisk/`](../../AutoKassa/Services/Backup/YandexDisk/); локальный бэкап продолжает работать независимо. Регистрация в DI: все сервисы + общий `HttpClient` — Singleton.

- **OAuth/PKCE.** `YandexDiskAuthService` открывает системный браузер на `oauth.yandex.ru/authorize` с `scope=cloud_api:disk.app_folder` (только папка приложения `app:/`), генерирует PKCE S256-пару; одноразовый код обменивается на access-token **без `client_secret`** (общий секрет в WPF не встраивается). Refresh-token не сохраняется — Яндекс требует `client_secret` для refresh grant, поэтому при истечении/отзыве токена (`AuthRequired`) пользователь выполняет повторный вход. Конечная модель состояний: `NotConnected`/`Connected`/`AuthRequired` + транзитное `AwaitingCode` — неуспешный обмен или отмена (`CancelAuthorizationAsync`, кнопка «Отмена» в настройках) возвращают устойчивое состояние, повторное подключение доступно без перезапуска (TRAPS §3.21). Незавершённый `code_verifier` живёт 10 минут.
- **Хранение токена.** `DpapiYandexDiskTokenStore` шифрует JSON токена через DPAPI `CurrentUser` (привязка к Windows-пользователю и машине) и атомарно пишет в `%LOCALAPPDATA%\AutoKassa\Secrets\yandex-disk-token.bin`: временный файл с уникальным именем + `File.Replace` (общий помощник `Helpers/AtomicFile`), сбой замены не лишает сохранённого токена (YD-08). Токен, коды и заголовки авторизации не попадают в логи, `AppSettings` и экспорт (TRAPS §3.16); регрессионно проверяется тестом `YandexDiskLogRedactionTests` (YD-12).
- **Выгрузка.** `YandexDiskBackupService`: локальная очередь `%LOCALAPPDATA%\AutoKassa\CloudBackupQueue` (один `AutoKassa_Backup_*.db`, переиспользуется после сбоев и перезапуска; повреждённый файл очереди уходит в карантин `*.corrupt`, TRAPS §3.20); свежий снимок создаётся существующим `ISettingsService.CreateSqliteSnapshotAsync` во временной папке `.staging` и атомарно попадает в очередь только после `PRAGMA integrity_check` (read-only, `Pooling=false`, TRAPS §3.15); успех подтверждается сверкой имени/размера/MD5 через метаданные Диска с ожиданием появления ресурса после 201 и 202 (404 — штатное «ещё не появился», до 10 с). HTTP 409 с совпавшими метаданными = идемпотентный успех; с другим содержимым — ошибка без перезаписи. Дата успеха фиксируется до удаления файла очереди: при сбое её записи pending остаётся для идемпотентного подтверждения без повторной передачи.
- **Повторы и ошибки.** Сеть/429/5xx и HTTP-таймауты (собственные лимиты `ApiRequestTimeout` 100 с и `UploadTimeout` 10 мин — общий `HttpClient.Timeout` отключён, чтобы таймаут не маскировался под штатную отмену) — до 3 попыток с задержками 1/2 с и уважением `Retry-After`, исчерпание → `PendingRetry` с сохранением очереди; 401 на любой стадии (включая чтение метаданных после загрузки) → инвалидация токена (`AuthRequired`), очередь не удаляется; 403/507 — сообщение о необходимости действия пользователя без частых повторов.
- **Ротация.** После подтверждённой выгрузки из `app:/` удаляются только ресурсы `type == "file"` с точным именем `AutoKassa_Backup_yyyyMMdd_HHmmss.db` (папки и файлы с похожими именами не трогаются) сверх `BackupKeepCount` (общая настройка с локальным бэкапом); ошибка ротации не отменяет успех загрузки (TRAPS §3.17).
- **Состояние.** `CloudBackupStatus` (`NotConnected`/`UpToDate`/`Due`/`Uploading`/`PendingRetry`/`AuthRequired`/`Error`) публикуется через `CloudBackupState` и событие `StateChanged` (на потоке операции — UI подписчик обязан маршалить). Дата последнего успеха персистентна: сервис перечитывает `AppSettings.YandexDiskLastBackupUtc` в начале каждого запуска и фиксирует её через `ISettingsService.SetYandexDiskLastBackupUtcAsync` после подтверждённой выгрузки; без даты копия считается просроченной. Сброс даты (новое подключение/отключение аккаунта) выполняет `SettingsViewModel` тем же методом (TRAPS §3.18).
- **UI.** Во вкладке «Резервное копирование» раздел «Яндекс Диск»: включение облачного бэкапа (`YandexDiskBackupEnabled`, сохраняется общим «Сохранить»), статус, дата последнего успеха, признак ожидающей копии, «Подключить»/«Отключить», поле одноразового кода и «Загрузить сейчас» (`UploadNowAsync(force: true)` через общий `SemaphoreSlim` сервиса). Если ClientId не задан, «Подключить» открывает диалог ввода (`YandexClientIdDialogViewModel`): значение сохраняется через `IYandexDiskClientIdStore` в профиль пользователя и применяется сразу — без ручной правки файлов.
- **Конфигурация.** Публичный `YandexDisk:ClientId` и URL OAuth/API — в `appsettings.json` (копируется в output); секретов в файле нет. ClientId, введённый пользователем в диалоге, хранится в `%LOCALAPPDATA%\AutoKassa\yandex-disk-clientid.txt` и имеет приоритет над appsettings.json — портативная установка может лежать в каталоге без прав записи. ClientId не секрет: он передаётся в URL авторизации открыто.
- **Ограничения (явно).** Копии создаются только при запущенном приложении: проверка при старте и раз в час координатором, службы Windows и планировщик задач не используются. Облачный `.db` **не шифруется дополнительно**: в папке приложения Диска лежит обычный файл SQLite-базы (передача по HTTPS), включая финансовые данные и хеши настроек. При истечении или отзыве токена облачные запуски приостанавливаются до повторного входа пользователя (`AuthRequired`), очередь не удаляется. DPAPI-привязка токена к Windows-пользователю означает: токен недоступен другим пользователям машины, но при смене профиля Windows подключение нужно выполнить заново. Восстановление из облака — только ручное: скачать `.db` и восстановить существующей командой из раздела «Локальное резервное копирование».

---

## 5. Безопасность

### 5.1. `PasswordService`

**Файл:** [`AutoKassa/Services/PasswordService.cs`](../../AutoKassa/Services/PasswordService.cs)

**Интерфейс:** [`IPasswordService.cs`](../../AutoKassa/Services/IPasswordService.cs)

- Хеширование через `BCrypt.Net-Next`.
- `HashPassword` генерирует соль автоматически.
- `VerifyPassword` безопасно обрабатывает null/пустые значения.

### 5.2. Секретный вопрос

- Ответ также хешируется через BCrypt.
- При проверке ответ нормализуется: `Trim().ToLower()`.

### 5.3. `LockService`

**Файл:** [`AutoKassa/Services/LockService.cs`](../../AutoKassa/Services/LockService.cs)

**Интерфейс:** [`ILockService.cs`](../../AutoKassa/Services/ILockService.cs)

**Lifetime:** Singleton

- `DispatcherTimer` с интервалом из `SettingsService.GetAutoLockTimeout()`. Таймер отсчитывает **простой**, а не время с запуска: `MainWindow` отслеживает ввод пользователя (`InputManager.Current.PreProcessInput`, только клавиатура и кнопки мыши — движение мыши игнорируется) и вызывает `MainWindowViewModel.NotifyUserActivity()` → `ResetAutoLockTimer()` с троттлингом 15 секунд (`ActivityResetInterval`).
- `StartAutoLockTimer()` при выключенной настройке (`AutoLockEnabled == false`) или таймауте `<= 0` явно останавливает существующий таймер.
- `ResetAutoLockTimer()` перезапускает таймер безусловно (ранее — только если `IsEnabled`, из-за чего после срабатывания таймер оставался остановленным). Стражи: не сбрасывает на экране блокировки (`_isLocked`) и до первого запуска таймера (`null`).
- `Lock()` создаёт `LockScreenViewModel` через DI и показывает `LockScreenView` через `DialogService.ShowLockScreen`.
- При успешной разблокировке сбрасывается таймер. Закрытие экрана блокировки без ввода пароля (Alt+F4, крестик, Esc) **завершает приложение** (`Application.Current.Shutdown()`) — иначе блокировку можно было бы обойти; разблокировка возможна только паролем.
- Настройки автоблокировки применяются на лету: `SettingsViewModel.SaveAsync` после сохранения вызывает `StartAutoLockTimer()`/`StopAutoLockTimer()` через `ILockService`, перезапуск приложения не требуется.
- При запуске приложения экран блокировки не показывается: настройка `RequirePasswordOnStartup` хранится в БД и UI настроек, но в `OnStartup` не проверяется — сразу открывается `MainWindow` (см. [`14-startup.md`](14-startup.md) §4). Блокировка срабатывает только в процессе работы — по таймеру или вручную.

### 5.4. Восстановление пароля

[`PasswordRecoveryViewModel`](../../AutoKassa/ViewModels/PasswordRecoveryViewModel.cs) (Transient в DI) встроена в экран блокировки как дочерняя панель: `LockScreenViewModel` получает её через конструктор (свойство `Recovery`), а [`LockScreenView.xaml`](../../AutoKassa/Views/LockScreenView.xaml) переключает панели входа и восстановления по `IsPasswordRecoveryMode` (биндинги через `Recovery.*`).

Сценарий: «Забыли пароль?» (`ShowPasswordRecoveryCommand`) → `Recovery.Reset()` (очистка полей, перезагрузка вопроса из настроек) → ввод ответа → `VerifyAnswerAsync` (BCrypt-проверка хеша ответа) → ввод нового пароля и подтверждения → `ResetPasswordAsync` → `SettingsService.UpdatePasswordAsync`. Кнопка «Назад» (`CancelPasswordRecoveryCommand`) и события `Recovery.OnPasswordReset` / `OnCancelled` возвращают к панели входа. В режиме восстановления `CanUnlock()` дополнительно блокирует разблокировку (`&& !IsPasswordRecoveryMode`). `LockScreenViewModel.OnDispose()` отписывает события и диспозит `Recovery`.

### 5.5. Смена пароля

- [`ChangePasswordViewModel`](../../AutoKassa/ViewModels/ChangePasswordViewModel.cs) требует текущий пароль.
- Валидация: минимум 4 символа, новый ≠ текущий, совпадение подтверждения.

### 5.6. Path traversal

- `SettingsService.SanitizeBackupPath()` разрешает только пути внутри `UserProfile` или каталога приложения.

---

## 6. Декомпозиция сервисов настроек

| Сервис | Файл | Назначение |
|--------|------|------------|
| `ISettingsMapper` / `SettingsMapper` | [`Settings/SettingsMapper.cs`](../../AutoKassa/Services/Settings/SettingsMapper.cs) | Маппинг `AppSettings` ↔ `SettingsViewModel` |
| `ISettingsValidator` / `SettingsValidator` | [`Settings/SettingsValidator.cs`](../../AutoKassa/Services/Settings/SettingsValidator.cs) | Валидация значений настроек |
| `IAppInfoService` / `AppInfoService` | [`Settings/AppInfoService.cs`](../../AutoKassa/Services/Settings/AppInfoService.cs) | Информация «О программе» |
| `IBackupCoordinator` / `BackupCoordinator` | [`Backup/BackupCoordinator.cs`](../../AutoKassa/Services/Backup/BackupCoordinator.cs) | UI-диалоги бэкапа/восстановления |
| `ICreditCardSettingsService` / `CreditCardSettingsService` | [`CreditCards/CreditCardSettingsService.cs`](../../AutoKassa/Services/CreditCards/CreditCardSettingsService.cs) | Синхронизация настроек кредитной карты в карту по умолчанию |

---

## 7. `SettingsViewModel`

**Файл:** [`AutoKassa/ViewModels/SettingsViewModel.cs`](../../AutoKassa/ViewModels/SettingsViewModel.cs)

### 7.1. Вкладки

1. Общие
2. Операции
3. Отчёты
4. Резервное копирование
5. Безопасность
6. Кредитная карта
7. О программе

### 7.2. Команды

- `SaveCommand`, `CancelCommand`, `ResetCommand`
- `ExportCommand`, `ImportCommand`
- `ChangePasswordCommand`
- `SelectBackupPathCommand`, `CreateBackupCommand`, `RestoreBackupCommand`

### 7.3. Поведение

- `OnNavigatedTo()` — загружает настройки при первом показе.
- `MarkAsChanged()` — отслеживает несохранённые изменения.
- При сохранении: валидация → маппинг → `SaveSettingsAsync` → синхронизация кредитки → `NotifyDataChanged`.
- Настройки автоблокировки применяются сразу после сохранения: `SaveAsync` вызывает `ILockService.StartAutoLockTimer()`/`StopAutoLockTimer()` (зависимость `ILockService` через конструктор) — перезапуск приложения не нужен.

---

## 8. Рекомендации

1. Всегда используйте `SettingsService` для чтения/записи `AppSettings`, не обращайтесь к `AppDbContext` напрямую.
2. После изменения настроек вызывайте `DataChangeService.NotifyDataChanged()`.
3. Backup-путь всегда санитизируйте через `SanitizeBackupPath()`.
4. Пароли и ответы на секретные вопросы никогда не храните в открытом виде — только BCrypt-хеши.
5. Облачные токены и коды OAuth не логируйте и не сохраняйте в `AppSettings` — только DPAPI-хранилище (TRAPS §3.16); дату облачного успеха меняйте только через `SetYandexDiskLastBackupUtcAsync` (TRAPS §3.18).

---

> Последнее обновление: 2026-11-08 — этап 5 из `YandexBackUp.md`: §4.5 дополнен явными ограничениями облачного бэкапа (копии только при запущенном приложении; облачный `.db` не шифруется дополнительно; повторный вход при истечении/отзыве токена; DPAPI привязан к Windows-пользователю; восстановление — только ручное). Ранее: 2026-11-08 — этапы 3–4: `AutomaticBackupService`, `YandexDiskLastBackupUtc` + миграция, `SetYandexDiskLastBackupUtcAsync`, UI «Яндекс Диск». Ранее: 2026-11-08 — этапы 1–2: подсистема `Services/Backup/YandexDisk/` (OAuth/PKCE, DPAPI-хранилище, REST-клиент `app:/`, очередь + ротация), `CloudBackupStatus`.
