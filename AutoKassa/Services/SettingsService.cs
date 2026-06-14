using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.Globalization;
using System.IO;
using System.Text.Json;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с настройками приложения
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private static readonly ILogger _log = Log.ForContext<SettingsService>();

        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private AppSettings _cachedSettings;

        public SettingsService(IDbContextFactory<AppDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
            // НЕ загружаем настройки в конструкторе — это блокирует поток инициализации DI.
            // Ленивая загрузка происходит при первом обращении через GetSettings().
        }

        /// <summary>
        /// Применить все pending миграции. Вызывать из App.OnStartup() ДО создания остальных сервисов.
        /// </summary>
        public static async Task MigrateAsync(IDbContextFactory<AppDbContext> contextFactory)
        {
            await using var context = contextFactory.CreateDbContext();
            await context.Database.MigrateAsync();

            // Включаем WAL-режим SQLite для лучшей конкурентности читателей и писателей.
            // WAL позволяет читать из БД во время записи, что критично для стабильности на слабом железе.
            await context.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }

        /// <summary>
        /// Загрузить настройки из БД (с кешированием)
        /// </summary>
        private void LoadSettings()
        {
            using var context = _contextFactory.CreateDbContext();
            _cachedSettings = context.AppSettings.FirstOrDefault();

            // Если настроек нет (не должно быть, т.к. есть seed data), создаем
            if (_cachedSettings == null)
            {
                _cachedSettings = CreateDefaultSettings();
            }
        }

        /// <summary>
        /// Получить настройки приложения (ленивая загрузка при первом вызове)
        /// </summary>
        public AppSettings GetSettings()
        {
            if (_cachedSettings == null)
            {
                LoadSettings();
            }
            return _cachedSettings;
        }

        /// <summary>
        /// Проверить, установлен ли пароль
        /// </summary>
        public bool IsPasswordSet()
        {
            return !string.IsNullOrEmpty(GetSettings().PasswordHash);
        }

        /// <summary>
        /// Получить таймаут автоблокировки (в минутах)
        /// </summary>
        public int GetAutoLockTimeout()
        {
            return GetSettings().AutoLockTimeout;
        }

        /// <summary>
        /// Получить текущую тему
        /// </summary>
        public string GetTheme()
        {
            return GetSettings().Theme;
        }

        /// <summary>
        /// Получить тип операции по умолчанию
        /// </summary>
        public OperationType GetDefaultOperationType()
        {
            return (OperationType)GetSettings().DefaultOperationType;
        }

        /// <summary>
        /// Получить ID категории по умолчанию для типа операции (async)
        /// </summary>
        public async Task<int?> GetDefaultCategoryIdAsync(OperationType type)
        {
            var settings = await GetSettingsAsync().ConfigureAwait(false);
            return type == OperationType.Income
                ? settings.DefaultIncomeCategoryId
                : settings.DefaultExpenseCategoryId;
        }

        /// <summary>
        /// Установить пароль (async)
        /// </summary>
        public async Task SetPasswordAsync(string passwordHash, SecurityQuestion? questionId, string answerHash, string customQuestion = null)
        {
            var settings = GetSettings();
            settings.PasswordHash = passwordHash;
            settings.SecurityQuestionId = questionId;
            settings.SecurityAnswerHash = answerHash;
            settings.CustomSecurityQuestion = customQuestion;
            await SaveSettingsAsync(settings).ConfigureAwait(false);
        }

        /// <summary>
        /// Обновить пароль (async)
        /// </summary>
        public async Task UpdatePasswordAsync(string newPasswordHash)
        {
            var settings = GetSettings();
            settings.PasswordHash = newPasswordHash;
            await SaveSettingsAsync(settings).ConfigureAwait(false);
        }

        /// <summary>
        /// Установить таймаут автоблокировки (async)
        /// </summary>
        public async Task SetAutoLockTimeoutAsync(int minutes)
        {
            var settings = GetSettings();
            settings.AutoLockTimeout = minutes;
            await SaveSettingsAsync(settings).ConfigureAwait(false);
        }

        /// <summary>
        /// Установить тему (async)
        /// </summary>
        public async Task SetThemeAsync(string theme)
        {
            var settings = GetSettings();
            settings.Theme = theme;
            await SaveSettingsAsync(settings).ConfigureAwait(false);
        }

        /// <summary>
        /// Установить тип операции по умолчанию (async)
        /// </summary>
        public async Task SetDefaultOperationTypeAsync(OperationType type)
        {
            var settings = GetSettings();
            settings.DefaultOperationType = (int)type;
            await SaveSettingsAsync(settings).ConfigureAwait(false);
        }

        /// <summary>
        /// Установить категорию по умолчанию для типа операции (async)
        /// </summary>
        public async Task SetDefaultCategoryIdAsync(OperationType type, int? categoryId)
        {
            var settings = GetSettings();
            if (type == OperationType.Income)
            {
                settings.DefaultIncomeCategoryId = categoryId;
            }
            else
            {
                settings.DefaultExpenseCategoryId = categoryId;
            }
            await SaveSettingsAsync(settings).ConfigureAwait(false);
        }

        #region Асинхронные методы

        /// <summary>
        /// Получить настройки асинхронно
        /// </summary>
        public async Task<AppSettings> GetSettingsAsync()
        {
            if (_cachedSettings == null)
            {
                using var context = _contextFactory.CreateDbContext();
                _cachedSettings = await context.AppSettings.FirstOrDefaultAsync() ?? CreateDefaultSettings();
            }
            return _cachedSettings;
        }

        /// <summary>
        /// Сохранить настройки асинхронно
        /// </summary>
        public async Task SaveSettingsAsync(AppSettings settings)
        {
            using var context = _contextFactory.CreateDbContext();
            context.Update(settings);
            await context.SaveChangesAsync();
            _cachedSettings = settings;
        }

        /// <summary>
        /// Сбросить настройки к значениям по умолчанию
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            var currentPassword = _cachedSettings?.PasswordHash;
            var currentSecurityQuestion = _cachedSettings?.SecurityQuestionId;
            var currentSecurityAnswer = _cachedSettings?.SecurityAnswerHash;
            var currentCustomQuestion = _cachedSettings?.CustomSecurityQuestion;

            var settings = GetSettings();

            // Создаем настройки по умолчанию, сохраняя пароль
            settings.AutoLockTimeout = 10;
            settings.AutoLockEnabled = true;
            settings.Theme = "Light";
            settings.DefaultPeriodFilter = "Month";
            settings.ShowNotifications = true;
            settings.ShowOperationsInSidebar = false;
            settings.DefaultPageSize = 20;
            settings.ConfirmDelete = true;
            settings.AutoGenerateReports = false;
            settings.BackupEnabled = false;
            settings.AutoBackupDays = 7;
            settings.BackupFrequency = "Weekly";
            settings.BackupKeepCount = 10;
            settings.BackupPath = null;
            settings.RequirePasswordOnStartup = true;
            settings.PasswordExpireDays = 0;
            settings.Language = "ru-RU";
            settings.WindowWidth = 1200;
            settings.WindowHeight = 700;
            settings.DefaultOperationType = (int)OperationType.Expense;
            settings.DefaultIncomeCategoryId = null;
            settings.DefaultExpenseCategoryId = null;
            settings.InitialBalance = 0m;
            settings.DefaultPaymentType = 1;
            settings.CreditCardLimit = 0m;
            settings.CreditCardCurrentDebt = 0m;
            settings.CreditCardInterestRate = 0m;
            settings.CreditCardPaymentDay = 10;
            settings.CreditCardLastPaymentDate = null;
            settings.CreditCardMinimumPaymentPercent = 5m;

            // Восстанавливаем пароль
            settings.PasswordHash = currentPassword;
            settings.SecurityQuestionId = currentSecurityQuestion;
            settings.SecurityAnswerHash = currentSecurityAnswer;
            settings.CustomSecurityQuestion = currentCustomQuestion;

            await SaveSettingsAsync(settings);
        }

        /// <summary>
        /// Экспортировать настройки в JSON файл
        /// </summary>
        public async Task<bool> ExportSettingsAsync(string filePath)
        {
            try
            {
                var settings = GetSettings();
                var exportData = new SettingsExportData
                {
                    AutoLockTimeout = settings.AutoLockTimeout,
                    AutoLockEnabled = settings.AutoLockEnabled,
                    Theme = settings.Theme,
                    DefaultPeriodFilter = settings.DefaultPeriodFilter,
                    ShowNotifications = settings.ShowNotifications,
                    ShowOperationsInSidebar = settings.ShowOperationsInSidebar,
                    DefaultPageSize = settings.DefaultPageSize,
                    ConfirmDelete = settings.ConfirmDelete,
                    AutoGenerateReports = settings.AutoGenerateReports,
                    BackupEnabled = settings.BackupEnabled,
                    AutoBackupDays = settings.AutoBackupDays,
                    BackupFrequency = settings.BackupFrequency,
                    BackupKeepCount = settings.BackupKeepCount,
                    BackupPath = settings.BackupPath,
                    RequirePasswordOnStartup = settings.RequirePasswordOnStartup,
                    PasswordExpireDays = settings.PasswordExpireDays,
                    Language = settings.Language,
                    WindowWidth = settings.WindowWidth,
                    WindowHeight = settings.WindowHeight,
                    DefaultOperationType = settings.DefaultOperationType,
                    DefaultPaymentType = settings.DefaultPaymentType,
                    InitialBalance = settings.InitialBalance,
                    CreditCardLimit = settings.CreditCardLimit,
                    CreditCardCurrentDebt = settings.CreditCardCurrentDebt,
                    CreditCardInterestRate = settings.CreditCardInterestRate,
                    CreditCardPaymentDay = settings.CreditCardPaymentDay,
                    CreditCardLastPaymentDate = settings.CreditCardLastPaymentDate,
                    CreditCardMinimumPaymentPercent = settings.CreditCardMinimumPaymentPercent,
                    ExportDate = DateTime.Now
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(exportData, options);
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Ошибка экспорта настроек в {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Импортировать настройки из JSON файла
        /// </summary>
        public async Task<bool> ImportSettingsAsync(string filePath)
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var importData = JsonSerializer.Deserialize<SettingsExportData>(json);

                if (importData == null) return false;

                var settings = GetSettings();

                settings.AutoLockTimeout = importData.AutoLockTimeout;
                settings.AutoLockEnabled = importData.AutoLockEnabled;
                settings.Theme = importData.Theme ?? "Light";
                settings.DefaultPeriodFilter = importData.DefaultPeriodFilter ?? "Month";
                settings.ShowNotifications = importData.ShowNotifications;
                settings.ShowOperationsInSidebar = importData.ShowOperationsInSidebar;
                settings.DefaultPageSize = importData.DefaultPageSize;
                settings.ConfirmDelete = importData.ConfirmDelete;
                settings.AutoGenerateReports = importData.AutoGenerateReports;
                settings.BackupEnabled = importData.BackupEnabled;
                settings.AutoBackupDays = importData.AutoBackupDays;
                settings.BackupFrequency = importData.BackupFrequency ?? "Weekly";
                settings.BackupKeepCount = importData.BackupKeepCount;
                settings.BackupPath = SanitizeBackupPath(importData.BackupPath);
                settings.RequirePasswordOnStartup = importData.RequirePasswordOnStartup;
                settings.PasswordExpireDays = importData.PasswordExpireDays;
                settings.Language = importData.Language ?? "ru-RU";
                settings.WindowWidth = importData.WindowWidth;
                settings.WindowHeight = importData.WindowHeight;
                settings.DefaultOperationType = importData.DefaultOperationType;
                settings.DefaultPaymentType = importData.DefaultPaymentType;
                settings.InitialBalance = importData.InitialBalance;
                settings.CreditCardLimit = importData.CreditCardLimit;
                settings.CreditCardCurrentDebt = importData.CreditCardCurrentDebt;
                settings.CreditCardInterestRate = importData.CreditCardInterestRate;
                settings.CreditCardPaymentDay = importData.CreditCardPaymentDay;
                settings.CreditCardLastPaymentDate = importData.CreditCardLastPaymentDate;
                settings.CreditCardMinimumPaymentPercent = importData.CreditCardMinimumPaymentPercent;

                await SaveSettingsAsync(settings);
                _log.Information("Настройки импортированы из {FilePath}", filePath);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Ошибка импорта настроек из {FilePath}", filePath);
                return false;
            }
        }

        /// <summary>
        /// Создать резервную копию базы данных через SQLite Online Backup API.
        /// Корректно обрабатывает WAL-режим без закрытия соединений и без SQL-инъекций.
        /// </summary>
        public async Task<string?> CreateBackupAsync(string backupPath)
        {
            try
            {
                string dbPath;
                string sourceConnectionString;
                using (var context = _contextFactory.CreateDbContext())
                {
                    dbPath = context.Database.GetDbConnection().DataSource;
                    sourceConnectionString = context.Database.GetDbConnection().ConnectionString;
                }

                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                {
                    _log.Warning("CreateBackupAsync: файл БД не найден по пути {DbPath}", dbPath);
                    return null;
                }

                if (!Directory.Exists(backupPath))
                    Directory.CreateDirectory(backupPath);

                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFilePath = Path.Combine(backupPath, $"AutoKassa_Backup_{timestamp}.db");
                await Task.Run(() =>
                {
                    using var source = new SqliteConnection(sourceConnectionString);
                    using var dest = new SqliteConnection($"Data Source={backupFilePath}");
                    source.Open();
                    dest.Open();
                    source.BackupDatabase(dest);
                });

                _log.Information("Бэкап создан: {BackupFile}", backupFilePath);

                // Удаляем старые бэкапы сверх лимита
                CleanupOldBackups(backupPath, GetSettings().BackupKeepCount);

                return backupFilePath;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Ошибка создания резервной копии в {BackupPath}", backupPath);
                return null;
            }
        }

        /// <summary>
        /// Восстановить базу данных из резервной копии
        /// </summary>
        public async Task<bool> RestoreBackupAsync(string backupFilePath)
        {
            try
            {
                if (!File.Exists(backupFilePath))
                {
                    _log.Warning("RestoreBackupAsync: файл бэкапа не найден {BackupFile}", backupFilePath);
                    return false;
                }

                string dbPath;
                using (var context = _contextFactory.CreateDbContext())
                {
                    dbPath = context.Database.GetDbConnection().DataSource;
                }

                if (string.IsNullOrEmpty(dbPath))
                    return false;

                // Сбрасываем пул соединений Microsoft.Data.Sqlite — закрывает все открытые хендлы на файл
                SqliteConnection.ClearAllPools();

                await Task.Run(() => File.Copy(backupFilePath, dbPath, overwrite: true));

                // Перезагружаем настройки из восстановленной БД
                _cachedSettings = null;
                await GetSettingsAsync().ConfigureAwait(false);

                _log.Information("БД восстановлена из бэкапа: {BackupFile}", backupFilePath);
                return true;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Ошибка восстановления из бэкапа {BackupFile}", backupFilePath);
                return false;
            }
        }

        /// <summary>
        /// Защита от path traversal при импорте настроек: разрешаем только пути
        /// внутри профиля пользователя или каталога приложения.
        /// </summary>
        private static string? SanitizeBackupPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            try
            {
                var fullPath = Path.GetFullPath(path);
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!fullPath.StartsWith(userProfile, StringComparison.OrdinalIgnoreCase)
                    && !fullPath.StartsWith(appDir, StringComparison.OrdinalIgnoreCase))
                    return null;
                return fullPath;
            }
            catch { return null; }
        }

        /// <summary>
        /// Запустить авто-бэкап, если подошёл срок
        /// </summary>
        public async Task RunAutoBackupIfDueAsync()
        {
            var settings = GetSettings();
            if (!settings.BackupEnabled || string.IsNullOrWhiteSpace(settings.BackupPath))
                return;

            var backupDir = settings.BackupPath;
            var daysSinceLast = GetDaysSinceLastBackup(backupDir);

            if (daysSinceLast >= settings.AutoBackupDays)
                await CreateBackupAsync(backupDir);
        }

        /// <summary>
        /// Получить дату последнего резервного копирования (async)
        /// </summary>
        public Task<DateTime?> GetLastBackupDateAsync()
        {
            return Task.Run(() =>
            {
                var backupDir = GetSettings().BackupPath;
                if (string.IsNullOrWhiteSpace(backupDir) || !Directory.Exists(backupDir))
                    return (DateTime?)null;

                var latest = Directory
                    .GetFiles(backupDir, "AutoKassa_Backup_*.db")
                    .OrderByDescending(f => f)
                    .FirstOrDefault();

                if (latest == null) return (DateTime?)null;

                var name = Path.GetFileNameWithoutExtension(latest);
                var parts = name.Split('_');
                if (parts.Length >= 4 &&
                    DateTime.TryParseExact(parts[2] + "_" + parts[3], "yyyyMMdd_HHmmss",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastDate))
                    return (DateTime?)lastDate;

                return (DateTime?)null;
            });
        }

        /// <summary>
        /// Получить путь к файлу базы данных (async)
        /// </summary>
        public async Task<string?> GetDatabasePathAsync()
        {
            using var context = _contextFactory.CreateDbContext();
            return await Task.Run(() => context.Database.GetDbConnection().DataSource).ConfigureAwait(false);
        }

        /// <summary>
        /// Определяет количество дней с последнего бэкапа по имени файла.
        /// Возвращает int.MaxValue если бэкапов нет или папки не существует.
        /// </summary>
        private static int GetDaysSinceLastBackup(string backupDir)
        {
            if (!Directory.Exists(backupDir))
                return int.MaxValue;

            var latest = Directory
                .GetFiles(backupDir, "AutoKassa_Backup_*.db")
                .OrderByDescending(f => f)
                .FirstOrDefault();

            if (latest == null) return int.MaxValue;

            // Имя вида AutoKassa_Backup_yyyyMMdd_HHmmss.db
            var name  = Path.GetFileNameWithoutExtension(latest);
            var parts = name.Split('_');
            if (parts.Length >= 4 &&
                DateTime.TryParseExact(parts[2] + "_" + parts[3], "yyyyMMdd_HHmmss",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var lastDate))
                return (DateTime.Today - lastDate.Date).Days;

            return int.MaxValue;
        }

        /// <summary>
        /// Удаляет старые бэкапы, оставляя не более keepCount последних файлов
        /// </summary>
        private static void CleanupOldBackups(string backupDir, int keepCount)
        {
            if (keepCount <= 0 || !Directory.Exists(backupDir)) return;

            var toDelete = Directory
                .GetFiles(backupDir, "AutoKassa_Backup_*.db")
                .OrderByDescending(f => f)
                .Skip(keepCount);

            foreach (var file in toDelete)
            {
                try { File.Delete(file); }
                catch (Exception ex) { Log.Warning(ex, "Не удалось удалить старый бэкап {File}", file); }
            }
        }

        /// <summary>
        /// Создать настройки по умолчанию
        /// </summary>
        private AppSettings CreateDefaultSettings()
        {
            var settings = new AppSettings
            {
                Id = 1,
                PasswordHash = string.Empty,
                AutoLockTimeout = 10,
                AutoLockEnabled = true,
                Theme = "Light",
                DefaultPeriodFilter = "Month",
                ShowNotifications = true,
                ShowOperationsInSidebar = false,
                DefaultPageSize = 20,
                ConfirmDelete = true,
                AutoGenerateReports = false,
                BackupEnabled = false,
                AutoBackupDays = 7,
                BackupFrequency = "Weekly",
                BackupKeepCount = 10,
                RequirePasswordOnStartup = true,
                PasswordExpireDays = 0,
                Language = "ru-RU",
                WindowWidth = 1200,
                WindowHeight = 700,
                DefaultOperationType = (int)OperationType.Expense,
                DefaultPaymentType = 1,
                CreditCardLimit = 0m,
                CreditCardCurrentDebt = 0m,
                CreditCardInterestRate = 0m,
                CreditCardPaymentDay = 10,
                CreditCardLastPaymentDate = null,
                CreditCardMinimumPaymentPercent = 5m
            };
            using var context = _contextFactory.CreateDbContext();
            context.AppSettings.Add(settings);
            context.SaveChanges();
            return settings;
        }

        #endregion
    }

    /// <summary>
    /// Класс для экспорта/импорта настроек (без паролей)
    /// </summary>
    public class SettingsExportData
    {
        public int AutoLockTimeout { get; set; }
        public bool AutoLockEnabled { get; set; }
        public string? Theme { get; set; }
        public string? DefaultPeriodFilter { get; set; }
        public bool ShowNotifications { get; set; }
        public bool ShowOperationsInSidebar { get; set; }
        public int DefaultPageSize { get; set; }
        public bool ConfirmDelete { get; set; }
        public bool AutoGenerateReports { get; set; }
        public bool BackupEnabled { get; set; }
        public int AutoBackupDays { get; set; }
        public string? BackupFrequency { get; set; }
        public int BackupKeepCount { get; set; }
        public string? BackupPath { get; set; }
        public bool RequirePasswordOnStartup { get; set; }
        public int PasswordExpireDays { get; set; }
        public string? Language { get; set; }
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public int DefaultOperationType { get; set; }
        public int DefaultPaymentType { get; set; }
        public decimal InitialBalance { get; set; }
        public decimal CreditCardLimit { get; set; }
        public decimal CreditCardCurrentDebt { get; set; }
        public decimal CreditCardInterestRate { get; set; }
        public int CreditCardPaymentDay { get; set; }
        public DateTime? CreditCardLastPaymentDate { get; set; }
        public decimal CreditCardMinimumPaymentPercent { get; set; }
        public DateTime ExportDate { get; set; }
    }
}
