using System.Linq;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;

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


    }
}