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

- `DispatcherTimer` с интервалом из `SettingsService.GetAutoLockTimeout()`.
- `Lock()` создаёт `LockScreenViewModel` через DI и показывает `LockScreenView` через `DialogService.ShowLockScreen`.
- При успешной разблокировке сбрасывается таймер.

### 5.4. Восстановление пароля

> **Текущее состояние:** [`PasswordRecoveryViewModel`](../../AutoKassa/ViewModels/PasswordRecoveryViewModel.cs) существует, но **не используется**. В [`LockScreenView.xaml`](../../AutoKassa/Views/LockScreenView.xaml) восстановление пароля реализовано в виде заглушки с сообщением «Функция восстановления будет реализована позже».

- В будущем `PasswordRecoveryViewModel` будет загружать вопрос из настроек.
- Проверка ответа → ввод нового пароля → `SettingsService.UpdatePasswordAsync`.

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

---

## 8. Рекомендации

1. Всегда используйте `SettingsService` для чтения/записи `AppSettings`, не обращайтесь к `AppDbContext` напрямую.
2. После изменения настроек вызывайте `DataChangeService.NotifyDataChanged()`.
3. Backup-путь всегда санитизируйте через `SanitizeBackupPath()`.
4. Пароли и ответы на секретные вопросы никогда не храните в открытом виде — только BCrypt-хеши.
