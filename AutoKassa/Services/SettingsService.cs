using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Text.Json;

namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для работы с настройками приложения
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly AppDbContext _context;
        private AppSettings _cachedSettings;

        public SettingsService(AppDbContext context)
        {
            _context = context;

            // Создаем таблицы если их нет
            _context.Database.EnsureCreated();

            LoadSettings();
        }

        /// <summary>
        /// Загрузить настройки из БД (с кешированием)
        /// </summary>
        private void LoadSettings()
        {
            _cachedSettings = _context.AppSettings.FirstOrDefault();

            // Если настроек нет (не должно быть, т.к. есть seed data), создаем
            if (_cachedSettings == null)
            {
                _cachedSettings = new AppSettings
                {
                    Id = 1,
                    PasswordHash = string.Empty,
                    AutoLockTimeout = 10,
                    Theme = "Light",
                    DefaultPeriodFilter = "CurrentMonth",
                    BackupEnabled = true,
                    BackupFrequency = "Weekly",
                    BackupKeepCount = 10
                };
                _context.AppSettings.Add(_cachedSettings);
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Получить настройки приложения
        /// </summary>
        public AppSettings GetSettings()
        {
            return _cachedSettings;
        }

        /// <summary>
        /// Сохранить настройки приложения
        /// </summary>
        public void SaveSettings(AppSettings settings)
        {
            _context.Entry(settings).State = EntityState.Modified;
            _context.SaveChanges();
            _cachedSettings = settings;
        }

        /// <summary>
        /// Проверить, установлен ли пароль
        /// </summary>
        public bool IsPasswordSet()
        {
            return !string.IsNullOrEmpty(_cachedSettings.PasswordHash);
        }

        /// <summary>
        /// Установить пароль
        /// </summary>
        public void SetPassword(string passwordHash, SecurityQuestion? questionId, string answerHash, string customQuestion = null)
        {
            _cachedSettings.PasswordHash = passwordHash;
            _cachedSettings.SecurityQuestionId = questionId;
            _cachedSettings.SecurityAnswerHash = answerHash;
            _cachedSettings.CustomSecurityQuestion = customQuestion;
            SaveSettings(_cachedSettings);
        }

        /// <summary>
        /// Обновить пароль
        /// </summary>
        public void UpdatePassword(string newPasswordHash)
        {
            _cachedSettings.PasswordHash = newPasswordHash;
            SaveSettings(_cachedSettings);
        }

        /// <summary>
        /// Получить таймаут автоблокировки (в минутах)
        /// </summary>
        public int GetAutoLockTimeout()
        {
            return _cachedSettings.AutoLockTimeout;
        }

        /// <summary>
        /// Установить таймаут автоблокировки
        /// </summary>
        public void SetAutoLockTimeout(int minutes)
        {
            _cachedSettings.AutoLockTimeout = minutes;
            SaveSettings(_cachedSettings);
        }

        /// <summary>
        /// Получить текущую тему
        /// </summary>
        public string GetTheme()
        {
            return _cachedSettings.Theme;
        }

        /// <summary>
        /// Установить тему
        /// </summary>
        public void SetTheme(string theme)
        {
            _cachedSettings.Theme = theme;
            SaveSettings(_cachedSettings);
        }

        /// <summary>
        /// Получить тип операции по умолчанию
        /// </summary>
        public OperationType GetDefaultOperationType()
        {
            return (OperationType)_cachedSettings.DefaultOperationType;
        }

        /// <summary>
        /// Установить тип операции по умолчанию
        /// </summary>
        public void SetDefaultOperationType(OperationType type)
        {
            _cachedSettings.DefaultOperationType = (int)type;
            SaveSettings(_cachedSettings);
        }

        /// <summary>
        /// Получить ID категории по умолчанию для типа операции
        /// </summary>
        public int? GetDefaultCategoryId(OperationType type)
        {
            return type == OperationType.Income
                ? _cachedSettings.DefaultIncomeCategoryId
                : _cachedSettings.DefaultExpenseCategoryId;
        }

        /// <summary>
        /// Установить категорию по умолчанию для типа операции
        /// </summary>
        public void SetDefaultCategoryId(OperationType type, int? categoryId)
        {
            if (type == OperationType.Income)
            {
                _cachedSettings.DefaultIncomeCategoryId = categoryId;
            }
            else
            {
                _cachedSettings.DefaultExpenseCategoryId = categoryId;
            }
            SaveSettings(_cachedSettings);
        }

        #region Новые асинхронные методы

        /// <summary>
        /// Получить настройки асинхронно
        /// </summary>
        public async Task<AppSettings> GetSettingsAsync()
        {
            if (_cachedSettings == null)
            {
                _cachedSettings = await _context.AppSettings.FirstOrDefaultAsync() ?? CreateDefaultSettings();
            }
            return _cachedSettings;
        }

        /// <summary>
        /// Сохранить настройки асинхронно
        /// </summary>
        public async Task SaveSettingsAsync(AppSettings settings)
        {
            _context.Entry(settings).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            _cachedSettings = settings;
        }

        /// <summary>
        /// Сбросить настройки к значениям по умолчанию
        /// </summary>
        public async Task ResetToDefaultsAsync()
        {
            var currentPassword = _cachedSettings.PasswordHash;
            var currentSecurityQuestion = _cachedSettings.SecurityQuestionId;
            var currentSecurityAnswer = _cachedSettings.SecurityAnswerHash;
            var currentCustomQuestion = _cachedSettings.CustomSecurityQuestion;

            // Создаем настройки по умолчанию, сохраняя пароль
            _cachedSettings.AutoLockTimeout = 10;
            _cachedSettings.AutoLockEnabled = true;
            _cachedSettings.Theme = "Light";
            _cachedSettings.DefaultPeriodFilter = "Month";
            _cachedSettings.ShowNotifications = true;
            _cachedSettings.ShowOperationsInSidebar = false;
            _cachedSettings.DefaultPageSize = 20;
            _cachedSettings.ConfirmDelete = true;
            _cachedSettings.AutoGenerateReports = false;
            _cachedSettings.BackupEnabled = false;
            _cachedSettings.AutoBackupDays = 7;
            _cachedSettings.BackupFrequency = "Weekly";
            _cachedSettings.BackupKeepCount = 10;
            _cachedSettings.BackupPath = null;
            _cachedSettings.RequirePasswordOnStartup = true;
            _cachedSettings.PasswordExpireDays = 0;
            _cachedSettings.Language = "ru-RU";
            _cachedSettings.WindowWidth = 1200;
            _cachedSettings.WindowHeight = 700;
            _cachedSettings.DefaultOperationType = (int)OperationType.Expense;
            _cachedSettings.DefaultIncomeCategoryId = null;
            _cachedSettings.DefaultExpenseCategoryId = null;

            // Восстанавливаем пароль
            _cachedSettings.PasswordHash = currentPassword;
            _cachedSettings.SecurityQuestionId = currentSecurityQuestion;
            _cachedSettings.SecurityAnswerHash = currentSecurityAnswer;
            _cachedSettings.CustomSecurityQuestion = currentCustomQuestion;

            await SaveSettingsAsync(_cachedSettings);
        }

        /// <summary>
        /// Экспортировать настройки в JSON файл
        /// </summary>
        public async Task<bool> ExportSettingsAsync(string filePath)
        {
            try
            {
                var exportData = new SettingsExportData
                {
                    AutoLockTimeout = _cachedSettings.AutoLockTimeout,
                    AutoLockEnabled = _cachedSettings.AutoLockEnabled,
                    Theme = _cachedSettings.Theme,
                    DefaultPeriodFilter = _cachedSettings.DefaultPeriodFilter,
                    ShowNotifications = _cachedSettings.ShowNotifications,
                    ShowOperationsInSidebar = _cachedSettings.ShowOperationsInSidebar,
                    DefaultPageSize = _cachedSettings.DefaultPageSize,
                    ConfirmDelete = _cachedSettings.ConfirmDelete,
                    AutoGenerateReports = _cachedSettings.AutoGenerateReports,
                    BackupEnabled = _cachedSettings.BackupEnabled,
                    AutoBackupDays = _cachedSettings.AutoBackupDays,
                    BackupFrequency = _cachedSettings.BackupFrequency,
                    BackupKeepCount = _cachedSettings.BackupKeepCount,
                    BackupPath = _cachedSettings.BackupPath,
                    RequirePasswordOnStartup = _cachedSettings.RequirePasswordOnStartup,
                    PasswordExpireDays = _cachedSettings.PasswordExpireDays,
                    Language = _cachedSettings.Language,
                    WindowWidth = _cachedSettings.WindowWidth,
                    WindowHeight = _cachedSettings.WindowHeight,
                    DefaultOperationType = _cachedSettings.DefaultOperationType,
                    ExportDate = DateTime.Now
                };

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(exportData, options);
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch
            {
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

                _cachedSettings.AutoLockTimeout = importData.AutoLockTimeout;
                _cachedSettings.AutoLockEnabled = importData.AutoLockEnabled;
                _cachedSettings.Theme = importData.Theme ?? "Light";
                _cachedSettings.DefaultPeriodFilter = importData.DefaultPeriodFilter ?? "Month";
                _cachedSettings.ShowNotifications = importData.ShowNotifications;
                _cachedSettings.ShowOperationsInSidebar = importData.ShowOperationsInSidebar;
                _cachedSettings.DefaultPageSize = importData.DefaultPageSize;
                _cachedSettings.ConfirmDelete = importData.ConfirmDelete;
                _cachedSettings.AutoGenerateReports = importData.AutoGenerateReports;
                _cachedSettings.BackupEnabled = importData.BackupEnabled;
                _cachedSettings.AutoBackupDays = importData.AutoBackupDays;
                _cachedSettings.BackupFrequency = importData.BackupFrequency ?? "Weekly";
                _cachedSettings.BackupKeepCount = importData.BackupKeepCount;
                _cachedSettings.BackupPath = importData.BackupPath;
                _cachedSettings.RequirePasswordOnStartup = importData.RequirePasswordOnStartup;
                _cachedSettings.PasswordExpireDays = importData.PasswordExpireDays;
                _cachedSettings.Language = importData.Language ?? "ru-RU";
                _cachedSettings.WindowWidth = importData.WindowWidth;
                _cachedSettings.WindowHeight = importData.WindowHeight;
                _cachedSettings.DefaultOperationType = importData.DefaultOperationType;

                await SaveSettingsAsync(_cachedSettings);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Создать резервную копию базы данных
        /// </summary>
        public async Task<string?> CreateBackupAsync(string backupPath)
        {
            try
            {
                // Получаем путь к текущей базе данных
                var dbPath = _context.Database.GetDbConnection().DataSource;

                if (string.IsNullOrEmpty(dbPath) || !File.Exists(dbPath))
                    return null;

                // Создаем директорию если не существует
                if (!Directory.Exists(backupPath))
                    Directory.CreateDirectory(backupPath);

                // Формируем имя файла резервной копии
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupFileName = $"AutoKassa_Backup_{timestamp}.db";
                var backupFilePath = Path.Combine(backupPath, backupFileName);

                // Копируем файл базы данных
                await Task.Run(() => File.Copy(dbPath, backupFilePath, overwrite: true));

                return backupFilePath;
            }
            catch
            {
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
                    return false;

                // Получаем путь к текущей базе данных
                var dbPath = _context.Database.GetDbConnection().DataSource;

                if (string.IsNullOrEmpty(dbPath))
                    return false;

                // Закрываем соединение перед копированием
                await _context.Database.CloseConnectionAsync();

                // Копируем резервную копию на место текущей БД
                await Task.Run(() => File.Copy(backupFilePath, dbPath, overwrite: true));

                // Переоткрываем соединение
                await _context.Database.OpenConnectionAsync();

                // Перезагружаем настройки
                LoadSettings();

                return true;
            }
            catch
            {
                return false;
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
                DefaultOperationType = (int)OperationType.Expense
            };
            _context.AppSettings.Add(settings);
            _context.SaveChanges();
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
        public DateTime ExportDate { get; set; }
    }
}