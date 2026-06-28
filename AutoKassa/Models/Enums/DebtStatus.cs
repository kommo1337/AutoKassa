namespace AutoKassa.Models.Enums
{
    /// <summary>
    /// Статус долговой операции
    /// </summary>
    public enum DebtStatus
    {
        /// <summary>
        /// Обычная операция (не долг)
        /// </summary>
        NotDebt = 0,

        /// <summary>
        /// Долг не погашен или погашен частично
        /// </summary>
        Active = 1,

        /// <summary>
        /// Долг погашен полностью
        /// </summary>
        Repaid = 2,

        /// <summary>
        /// Долг списан
        /// </summary>
        WrittenOff = 3
    }
}
