using System;
using System.Collections.Generic;
using AutoKassa.Models.Reports;

namespace AutoKassa.Helpers
{
    public class TransactionDetailGroup
    {
        public DateTime Date { get; set; }
        public string DateLabel { get; set; } = "";
        public string DayTotalFormatted { get; set; } = "";
        public string DayTotalColor { get; set; } = "#22c55e";
        public List<TransactionDetailItem> Items { get; set; } = new();
    }
}
