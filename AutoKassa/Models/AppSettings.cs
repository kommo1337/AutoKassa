using AutoKassa.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoKassa.Models
{
    /// <summary>
    /// Настройки приложения (single-row table, всегда Id = 1)
    /// </summary>
    [Table("AppSettings")]
    public class AppSettings
    {
        /// <summary>
        /// Идентификатор (всегда 1, т.к. одна запись)
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Хеш пароля для блокировки приложения (BCrypt)
        /// </summary>
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; }

        /// <summary>
        /// ID секретного вопроса для восстановления пароля
        /// </summary>
        public SecurityQuestion? SecurityQuestionId { get; set; }

        /// <summary>
        /// Хеш ответа на секретный вопрос (BCrypt)
        /// </summary>
        [MaxLength(255)]
        public string? SecurityAnswerHash { get; set; }

        /// <summary>
        /// Свой секретный вопрос (если SecurityQuestionId = Custom)
        /// </summary>
        [MaxLength(200)]
        public string? CustomSecurityQuestion { get; set; }

        /// <summary>
        /// Таймаут автоблокировки в минутах (0 = отключено)
        /// </summary>
        [Required]
        public int AutoLockTimeout { get; set; }

        /// <summary>
        /// Тема оформления (Light/Dark)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Theme { get; set; }

        /// <summary>
        /// Фильтр периода по умолчанию на главной странице
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string DefaultPeriodFilter { get; set; }

        /// <summary>
        /// Включено ли автоматическое резервное копирование
        /// </summary>
        [Required]
        public bool BackupEnabled { get; set; }

        /// <summary>
        /// Частота резервного копирования (Daily/Weekly/Monthly)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string BackupFrequency { get; set; }

        /// <summary>
        /// Путь для сохранения резервных копий
        /// </summary>
        [MaxLength(500)]
        public string? BackupPath { get; set; }

        /// <summary>
        /// Количество хранимых резервных копий
        /// </summary>
        [Required]
        public int BackupKeepCount { get; set; }

        /// <summary>
        /// Тип операции по умолчанию для новых операций
        /// </summary>
        [Required]
        public int DefaultOperationType { get; set; } = 2; // 2 = Expense (Расход)

        /// <summary>
        /// ID категории по умолчанию для доходов
        /// </summary>
        public int? DefaultIncomeCategoryId { get; set; }

        /// <summary>
        /// ID категории по умолчанию для расходов
        /// </summary>
        public int? DefaultExpenseCategoryId { get; set; }

        #region Общие настройки

        /// <summary>
        /// Включена ли автоблокировка
        /// </summary>
        [Required]
        public bool AutoLockEnabled { get; set; } = true;

        /// <summary>
        /// Показывать уведомления
        /// </summary>
        [Required]
        public bool ShowNotifications { get; set; } = true;

        #endregion

        #region Настройки операций

        /// <summary>
        /// Показывать кнопку "Операции" в боковом меню
        /// </summary>
        [Required]
        public bool ShowOperationsInSidebar { get; set; } = false;

        /// <summary>
        /// Количество записей на странице по умолчанию
        /// </summary>
        [Required]
        public int DefaultPageSize { get; set; } = 20;

        /// <summary>
        /// Подтверждать удаление операций
        /// </summary>
        [Required]
        public bool ConfirmDelete { get; set; } = true;

        #endregion

        #region Настройки отчетов

        /// <summary>
        /// Автоматически формировать отчеты
        /// </summary>
        [Required]
        public bool AutoGenerateReports { get; set; } = false;

        #endregion

        #region Настройки резервного копирования

        /// <summary>
        /// Интервал автоматического резервного копирования (дней)
        /// </summary>
        [Required]
        public int AutoBackupDays { get; set; } = 7;

        #endregion

        #region Настройки безопасности

        /// <summary>
        /// Требовать пароль при запуске
        /// </summary>
        [Required]
        public bool RequirePasswordOnStartup { get; set; } = true;

        /// <summary>
        /// Срок действия пароля (дней, 0 = никогда)
        /// </summary>
        [Required]
        public int PasswordExpireDays { get; set; } = 0;

        #endregion

        #region Финансовые настройки

        /// <summary>
        /// Начальный баланс (сумма на кассе до первой транзакции)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal InitialBalance { get; set; } = 0;

        /// <summary>
        /// Тип оплаты по умолчанию (1 = Наличные, 2 = Безналичные)
        /// </summary>
        [Required]
        public int DefaultPaymentType { get; set; } = 1;

        #endregion

        #region Настройки интерфейса

        /// <summary>
        /// Язык интерфейса
        /// </summary>
        [Required]
        [MaxLength(10)]
        public string Language { get; set; } = "ru-RU";

        /// <summary>
        /// Ширина окна по умолчанию
        /// </summary>
        [Required]
        public double WindowWidth { get; set; } = 1200;

        /// <summary>
        /// Высота окна по умолчанию
        /// </summary>
        [Required]
        public double WindowHeight { get; set; } = 700;

        #endregion
    }
}