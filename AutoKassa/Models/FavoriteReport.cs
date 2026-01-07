using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AutoKassa.Models
{
    /// <summary>
    /// Избранный отчет с сохраненными параметрами
    /// </summary>
    [Table("FavoriteReports")]
    public class FavoriteReport
    {
        /// <summary>
        /// Уникальный идентификатор избранного отчета
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// Название избранного отчета (задается пользователем)
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        /// <summary>
        /// Тип отчета (Balance, CategoryStructure, Comparison, Forecast, ABC)
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string ReportType { get; set; }

        /// <summary>
        /// JSON с параметрами отчета
        /// </summary>
        [Required]
        [MaxLength(2000)]
        public string Parameters { get; set; }

        /// <summary>
        /// Дата создания избранного отчета
        /// </summary>
        [Required]
        public DateTime CreatedAt { get; set; }
    }
}