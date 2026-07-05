namespace AutoKassa.ViewModels
{
    /// <summary>
    /// Позиция в топе категорий на дашборде.
    /// </summary>
    public class CategorySummaryItem
    {
        public string Name { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public string TotalFormatted => $"{Total:N0} ₽";
    }
}
