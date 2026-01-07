using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AutoKassa.Models.Enums;

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
    }
}