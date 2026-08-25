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
| `CreateBackupAsync` / `RestoreBackupAsync` | Резервное копирование БД. |
| `RunAutoBackupIfDueAsync` | Авто-бэкап по расписанию. |

### 2.3. Миграции

- Применяются автоматически при старте в [`App.OnStartup()`](../../AutoKassa/App.xaml.cs) через `SettingsService.MigrateAsync()`.
- WAL-режим включается для лучшей конкурентности.
- После восстановления из бэкапа вызывается `MigrateAsync`.

---

## 3. Import/Export настроек в JSON

**Файл:** [`AutoKassa/Services/Settings/SettingsMapper.cs`](../../AutoKassa/Services/Settings/SettingsMapper.cs)

### 3.1. `SettingsExportData`

- Содержит копию всех настроек, **кроме паролей и секретных вопросов**.
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

### 4.2. `SettingsService.CreateBackupAsync`

- Использует `SqliteConnection.BackupDatabase` (SQLite Online Backup API).
- Не закрывает соединения, не использует SQL-инъекций.
- Имя файла: `AutoKassa_Backup_{yyyyMMdd_HHmmss}.db`.
- Очищает старые бэкапы сверх `BackupKeepCount`.

### 4.3. `SettingsService.RestoreBackupAsync`

- `SqliteConnection.ClearAllPools()` — закрывает хендлы.
- `File.Copy(backupFilePath, dbPath, overwrite: true)`.
- Применяет миграции.
- Сбрасывает и перезагружает `_cachedSettings`.

### 4.4. Автобэкап

- `RunAutoBackupIfDueAsync` проверяет `BackupEnabled` и `AutoBackupDays`.
- Дата последнего бэкапа определяется по имени файла.

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
