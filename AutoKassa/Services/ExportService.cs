using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoKassa.Models.Enums;
using AutoKassa.Models.Reports;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AutoKassa.Services
{
    /// <summary>
    /// Реализация сервиса экспорта отчетов
    /// </summary>
    public class ExportService : IExportService
    {
        private readonly string _exportFolder;

        public ExportService()
        {
            // Папка для экспорта в Documents
            _exportFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "AutoKassa",
                "Exports"
            );

            // Создаем папку если не существует
            if (!Directory.Exists(_exportFolder))
            {
                Directory.CreateDirectory(_exportFolder);
            }

            // Настройка лицензии QuestPDF (Community)
            QuestPDF.Settings.License = LicenseType.Community;
        }

        #region Balance Report

        /// <summary>
        /// Экспорт отчета "Баланс за период" в PDF
        /// </summary>
        public Task<string> ExportBalanceReportToPdfAsync(BalanceReport report)
        {
            return Task.Run(() =>
            {
                var fileName = $"Баланс_{report.DateFrom:dd.MM.yyyy}-{report.DateTo:dd.MM.yyyy}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var filePath = Path.Combine(_exportFolder, fileName);

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        // Заголовок
                        page.Header().Element(c => ComposeBalanceHeader(c, report));

                        // Содержимое
                        page.Content().Element(c => ComposeBalanceContent(c, report));

                        // Подвал
                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("AutoKassa | Сформировано: ");
                            text.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                        });
                    });
                }).GeneratePdf(filePath);

                return filePath;
            });
        }

        private void ComposeBalanceHeader(IContainer container, BalanceReport report)
        {
            container.Column(column =>
            {
                column.Item().AlignCenter().Text("Отчет: Баланс за период")
                    .FontSize(18).Bold();

                column.Item().AlignCenter().Text($"{report.DateFrom:dd.MM.yyyy} - {report.DateTo:dd.MM.yyyy}")
                    .FontSize(12);

                column.Item().PaddingVertical(10).LineHorizontal(1);
            });
        }

        private void ComposeBalanceContent(IContainer container, BalanceReport report)
        {
            container.Column(column =>
            {
                // Сводка
                column.Item().PaddingBottom(20).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Начальный баланс:").FontSize(11);
                        c.Item().Text($"{report.StartBalance:N2} руб.").FontSize(14).Bold();
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Доходы:").FontSize(11);
                        c.Item().Text($"+{report.TotalIncome:N2} руб.").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Расходы:").FontSize(11);
                        c.Item().Text($"-{report.TotalExpense:N2} руб.").FontSize(14).Bold().FontColor(Colors.Red.Medium);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Конечный баланс:").FontSize(11);
                        c.Item().Text($"{report.EndBalance:N2} руб.").FontSize(14).Bold();
                    });
                });

                // Таблица по дням
                if (report.DailyBalances != null && report.DailyBalances.Any())
                {
                    column.Item().Text("Детализация по дням").FontSize(12).Bold();
                    column.Item().PaddingTop(10).Table(table =>
                    {
                        // Определение колонок
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Дата
                            columns.RelativeColumn(2); // Доходы
                            columns.RelativeColumn(2); // Расходы
                            columns.RelativeColumn(2); // Баланс
                        });

                        // Заголовок таблицы
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Дата").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Доходы").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Расходы").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Баланс").Bold();
                        });

                        // Данные
                        foreach (var daily in report.DailyBalances)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(daily.Date.ToString("dd.MM.yyyy"));

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text(daily.Income > 0 ? $"+{daily.Income:N2}" : "-");

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text(daily.Expense > 0 ? $"-{daily.Expense:N2}" : "-");

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text($"{daily.Balance:N2}");
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Экспорт отчета "Баланс за период" в Excel
        /// </summary>
        public Task<string> ExportBalanceReportToExcelAsync(BalanceReport report)
        {
            return Task.Run(() =>
            {
                var fileName = $"Баланс_{report.DateFrom:dd.MM.yyyy}-{report.DateTo:dd.MM.yyyy}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = Path.Combine(_exportFolder, fileName);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Баланс за период");

                // Заголовок
                worksheet.Cell("A1").Value = "Отчет: Баланс за период";
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 16;
                worksheet.Range("A1:D1").Merge();

                worksheet.Cell("A2").Value = $"Период: {report.DateFrom:dd.MM.yyyy} - {report.DateTo:dd.MM.yyyy}";
                worksheet.Range("A2:D2").Merge();

                // Сводка
                worksheet.Cell("A4").Value = "Начальный баланс:";
                worksheet.Cell("B4").Value = report.StartBalance;
                worksheet.Cell("B4").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";

                worksheet.Cell("A5").Value = "Доходы:";
                worksheet.Cell("B5").Value = report.TotalIncome;
                worksheet.Cell("B5").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";
                worksheet.Cell("B5").Style.Font.FontColor = XLColor.Green;

                worksheet.Cell("A6").Value = "Расходы:";
                worksheet.Cell("B6").Value = report.TotalExpense;
                worksheet.Cell("B6").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";
                worksheet.Cell("B6").Style.Font.FontColor = XLColor.Red;

                worksheet.Cell("A7").Value = "Конечный баланс:";
                worksheet.Cell("B7").Value = report.EndBalance;
                worksheet.Cell("B7").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";
                worksheet.Cell("B7").Style.Font.Bold = true;

                // Таблица детализации
                if (report.DailyBalances != null && report.DailyBalances.Any())
                {
                    worksheet.Cell("A9").Value = "Детализация по дням";
                    worksheet.Cell("A9").Style.Font.Bold = true;
                    worksheet.Range("A9:D9").Merge();

                    // Заголовки таблицы
                    var headerRow = 10;
                    worksheet.Cell(headerRow, 1).Value = "Дата";
                    worksheet.Cell(headerRow, 2).Value = "Доходы";
                    worksheet.Cell(headerRow, 3).Value = "Расходы";
                    worksheet.Cell(headerRow, 4).Value = "Баланс";

                    var headerRange = worksheet.Range(headerRow, 1, headerRow, 4);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    // Данные
                    var row = headerRow + 1;
                    foreach (var daily in report.DailyBalances)
                    {
                        worksheet.Cell(row, 1).Value = daily.Date;
                        worksheet.Cell(row, 1).Style.DateFormat.Format = "dd.MM.yyyy";

                        worksheet.Cell(row, 2).Value = daily.Income;
                        worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";

                        worksheet.Cell(row, 3).Value = daily.Expense;
                        worksheet.Cell(row, 3).Style.NumberFormat.Format = "#,##0.00";

                        worksheet.Cell(row, 4).Value = daily.Balance;
                        worksheet.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";

                        row++;
                    }

                    // Границы таблицы
                    var dataRange = worksheet.Range(headerRow, 1, row - 1, 4);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                }

                // Автоширина колонок
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
                return filePath;
            });
        }

        #endregion

        #region Category Report

        /// <summary>
        /// Экспорт отчета "Структура по категориям" в PDF
        /// </summary>
        public Task<string> ExportCategoryReportToPdfAsync(CategoryReport report)
        {
            return Task.Run(() =>
            {
                var typeText = report.OperationType == OperationType.Expense ? "Расходы" : "Доходы";
                var fileName = $"Категории_{typeText}_{report.DateFrom:dd.MM.yyyy}-{report.DateTo:dd.MM.yyyy}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                var filePath = Path.Combine(_exportFolder, fileName);

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        // Заголовок
                        page.Header().Element(c => ComposeCategoryHeader(c, report));

                        // Содержимое
                        page.Content().Element(c => ComposeCategoryContent(c, report));

                        // Подвал
                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("AutoKassa | Сформировано: ");
                            text.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                        });
                    });
                }).GeneratePdf(filePath);

                return filePath;
            });
        }

        private void ComposeCategoryHeader(IContainer container, CategoryReport report)
        {
            var typeText = report.OperationType == OperationType.Expense ? "Расходы" : "Доходы";

            container.Column(column =>
            {
                column.Item().AlignCenter().Text($"Отчет: Структура по категориям ({typeText})")
                    .FontSize(18).Bold();

                column.Item().AlignCenter().Text($"{report.DateFrom:dd.MM.yyyy} - {report.DateTo:dd.MM.yyyy}")
                    .FontSize(12);

                column.Item().PaddingVertical(10).LineHorizontal(1);
            });
        }

        private void ComposeCategoryContent(IContainer container, CategoryReport report)
        {
            container.Column(column =>
            {
                // Сводка
                column.Item().PaddingBottom(20).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Общая сумма:").FontSize(11);
                        c.Item().Text($"{report.TotalAmount:N2} руб.").FontSize(14).Bold();
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Количество операций:").FontSize(11);
                        c.Item().Text($"{report.TransactionCount}").FontSize(14).Bold();
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Категорий:").FontSize(11);
                        c.Item().Text($"{report.CategoryItems?.Count ?? 0}").FontSize(14).Bold();
                    });
                });

                // Таблица по категориям
                if (report.CategoryItems != null && report.CategoryItems.Any())
                {
                    column.Item().Text("Детализация по категориям").FontSize(12).Bold();
                    column.Item().PaddingTop(10).Table(table =>
                    {
                        // Определение колонок
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3); // Категория
                            columns.RelativeColumn(2); // Сумма
                            columns.RelativeColumn(1); // Доля
                            columns.RelativeColumn(2); // Кол-во операций
                        });

                        // Заголовок таблицы
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Категория").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Сумма").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Доля").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Операций").Bold();
                        });

                        // Данные
                        foreach (var item in report.CategoryItems)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(item.CategoryName);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text($"{item.Amount:N2} руб.");

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text($"{item.Percentage:N1}%");

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text($"{item.TransactionCount}");
                        }

                        // Итого
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("ИТОГО").Bold();
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight()
                            .Text($"{report.TotalAmount:N2} руб.").Bold();
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight()
                            .Text("100%").Bold();
                        table.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight()
                            .Text($"{report.TransactionCount}").Bold();
                    });
                }
            });
        }

        /// <summary>
        /// Экспорт отчета "Структура по категориям" в Excel
        /// </summary>
        public Task<string> ExportCategoryReportToExcelAsync(CategoryReport report)
        {
            return Task.Run(() =>
            {
                var typeText = report.OperationType == OperationType.Expense ? "Расходы" : "Доходы";
                var fileName = $"Категории_{typeText}_{report.DateFrom:dd.MM.yyyy}-{report.DateTo:dd.MM.yyyy}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = Path.Combine(_exportFolder, fileName);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Структура по категориям");

                // Заголовок
                worksheet.Cell("A1").Value = $"Отчет: Структура по категориям ({typeText})";
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 16;
                worksheet.Range("A1:D1").Merge();

                worksheet.Cell("A2").Value = $"Период: {report.DateFrom:dd.MM.yyyy} - {report.DateTo:dd.MM.yyyy}";
                worksheet.Range("A2:D2").Merge();

                // Сводка
                worksheet.Cell("A4").Value = "Общая сумма:";
                worksheet.Cell("B4").Value = report.TotalAmount;
                worksheet.Cell("B4").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";
                worksheet.Cell("B4").Style.Font.Bold = true;

                worksheet.Cell("A5").Value = "Количество операций:";
                worksheet.Cell("B5").Value = report.TransactionCount;

                worksheet.Cell("A6").Value = "Категорий:";
                worksheet.Cell("B6").Value = report.CategoryItems?.Count ?? 0;

                // Таблица детализации
                if (report.CategoryItems != null && report.CategoryItems.Any())
                {
                    worksheet.Cell("A8").Value = "Детализация по категориям";
                    worksheet.Cell("A8").Style.Font.Bold = true;
                    worksheet.Range("A8:D8").Merge();

                    // Заголовки таблицы
                    var headerRow = 9;
                    worksheet.Cell(headerRow, 1).Value = "Категория";
                    worksheet.Cell(headerRow, 2).Value = "Сумма";
                    worksheet.Cell(headerRow, 3).Value = "Доля (%)";
                    worksheet.Cell(headerRow, 4).Value = "Операций";

                    var headerRange = worksheet.Range(headerRow, 1, headerRow, 4);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                    // Данные
                    var row = headerRow + 1;
                    foreach (var item in report.CategoryItems)
                    {
                        worksheet.Cell(row, 1).Value = item.CategoryName;

                        worksheet.Cell(row, 2).Value = item.Amount;
                        worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";

                        worksheet.Cell(row, 3).Value = item.Percentage / 100;
                        worksheet.Cell(row, 3).Style.NumberFormat.Format = "0.0%";

                        worksheet.Cell(row, 4).Value = item.TransactionCount;

                        row++;
                    }

                    // Итого
                    worksheet.Cell(row, 1).Value = "ИТОГО";
                    worksheet.Cell(row, 1).Style.Font.Bold = true;

                    worksheet.Cell(row, 2).Value = report.TotalAmount;
                    worksheet.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(row, 2).Style.Font.Bold = true;

                    worksheet.Cell(row, 3).Value = 1;
                    worksheet.Cell(row, 3).Style.NumberFormat.Format = "0.0%";
                    worksheet.Cell(row, 3).Style.Font.Bold = true;

                    worksheet.Cell(row, 4).Value = report.TransactionCount;
                    worksheet.Cell(row, 4).Style.Font.Bold = true;

                    var totalRange = worksheet.Range(row, 1, row, 4);
                    totalRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                    // Границы таблицы
                    var dataRange = worksheet.Range(headerRow, 1, row, 4);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                }

                // Автоширина колонок
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
                return filePath;
            });
        }

        #endregion
    }
}
