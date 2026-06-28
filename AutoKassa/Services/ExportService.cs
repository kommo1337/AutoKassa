using Serilog;
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
        private static readonly ILogger _log = Log.ForContext<ExportService>();

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
                try
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
                            page.Header().Element(c => ComposeBalanceHeader(c, report));
                            page.Content().Element(c => ComposeBalanceContent(c, report));
                            page.Footer().AlignCenter().Text(text =>
                            {
                                text.Span("AutoKassa | Сформировано: ");
                                text.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                            });
                        });
                    }).GeneratePdf(filePath);

                    _log.Information("Экспорт PDF (баланс): {FilePath}", filePath);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Ошибка экспорта отчёта баланса в PDF");
                    throw;
                }
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
                try
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
                _log.Information("Экспорт Excel (баланс): {FilePath}", filePath);
                return filePath;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Ошибка экспорта отчёта баланса в Excel");
                    throw;
                }
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
                try
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
                            page.Header().Element(c => ComposeCategoryHeader(c, report));
                            page.Content().Element(c => ComposeCategoryContent(c, report));
                            page.Footer().AlignCenter().Text(text =>
                            {
                                text.Span("AutoKassa | Сформировано: ");
                                text.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                            });
                        });
                    }).GeneratePdf(filePath);

                    _log.Information("Экспорт PDF (категории): {FilePath}", filePath);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Ошибка экспорта отчёта по категориям в PDF");
                    throw;
                }
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
                try
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
                _log.Information("Экспорт Excel (категории): {FilePath}", filePath);
                return filePath;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Ошибка экспорта отчёта по категориям в Excel");
                    throw;
                }
            });
        }

        #endregion

        #region Transaction Detail Report

        /// <summary>
        /// Экспорт отчета "Детализация операций" в PDF
        /// </summary>
        public Task<string> ExportTransactionDetailReportToPdfAsync(TransactionDetailReport report)
        {
            return Task.Run(() =>
            {
                try
                {
                    var fileName = $"Детализация_{report.DateFrom:dd.MM.yyyy}-{report.DateTo:dd.MM.yyyy}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    var filePath = Path.Combine(_exportFolder, fileName);

                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(30);
                            page.DefaultTextStyle(x => x.FontSize(10));
                            page.Header().Element(c => ComposeTransactionDetailHeader(c, report));
                            page.Content().Element(c => ComposeTransactionDetailContent(c, report));
                            page.Footer().AlignCenter().Text(text =>
                            {
                                text.Span("AutoKassa | Сформировано: ");
                                text.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                            });
                        });
                    }).GeneratePdf(filePath);

                    _log.Information("Экспорт PDF (детализация): {FilePath}", filePath);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Ошибка экспорта детализации операций в PDF");
                    throw;
                }
            });
        }

        private void ComposeTransactionDetailHeader(IContainer container, TransactionDetailReport report)
        {
            container.Column(column =>
            {
                column.Item().AlignCenter().Text("Отчет: Детализация операций")
                    .FontSize(18).Bold();

                var periodText = $"{report.DateFrom:dd.MM.yyyy} - {report.DateTo:dd.MM.yyyy}";
                if (report.FilterType.HasValue)
                {
                    periodText += $" | {(report.FilterType == OperationType.Income ? "Доходы" : "Расходы")}";
                }
                if (!string.IsNullOrEmpty(report.FilterCategoryName))
                {
                    periodText += $" | {report.FilterCategoryName}";
                }

                column.Item().AlignCenter().Text(periodText).FontSize(12);
                column.Item().PaddingVertical(10).LineHorizontal(1);
            });
        }

        private void ComposeTransactionDetailContent(IContainer container, TransactionDetailReport report)
        {
            container.Column(column =>
            {
                // Сводка
                column.Item().PaddingBottom(20).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Операций:").FontSize(11);
                        c.Item().Text($"{report.TransactionCount}").FontSize(14).Bold();
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
                        c.Item().Text("Разница:").FontSize(11);
                        c.Item().Text($"{report.NetAmount:N2} руб.").FontSize(14).Bold()
                            .FontColor(report.NetAmount >= 0 ? Colors.Blue.Medium : Colors.Red.Medium);
                    });
                });

                // Таблица операций
                if (report.Transactions != null && report.Transactions.Any())
                {
                    column.Item().Text("Список операций").FontSize(12).Bold();
                    column.Item().PaddingTop(10).Table(table =>
                    {
                        // Определение колонок
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70);  // Дата
                            columns.ConstantColumn(60);  // Тип
                            columns.ConstantColumn(80);  // Сумма
                            columns.RelativeColumn(2);   // Категория
                            columns.RelativeColumn(3);   // Описание
                        });

                        // Заголовок таблицы
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Дата").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Тип").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Сумма").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Категория").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Описание").Bold();
                        });

                        // Данные
                        foreach (var transaction in report.Transactions)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(transaction.Date.ToString("dd.MM.yyyy"));

                            var typeColor = transaction.Type == OperationType.Income
                                ? Colors.Green.Medium
                                : Colors.Red.Medium;

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(transaction.TypeName).FontColor(typeColor);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text($"{transaction.Amount:N2}");

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(transaction.CategoryName);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(transaction.Description ?? "");
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Экспорт отчета "Детализация операций" в Excel
        /// </summary>
        public Task<string> ExportTransactionDetailReportToExcelAsync(TransactionDetailReport report)
        {
            return Task.Run(() =>
            {
                try
                {
                var fileName = $"Детализация_{report.DateFrom:dd.MM.yyyy}-{report.DateTo:dd.MM.yyyy}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                var filePath = Path.Combine(_exportFolder, fileName);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("Детализация операций");

                // Заголовок
                worksheet.Cell("A1").Value = "Отчет: Детализация операций";
                worksheet.Cell("A1").Style.Font.Bold = true;
                worksheet.Cell("A1").Style.Font.FontSize = 16;
                worksheet.Range("A1:E1").Merge();

                var periodText = $"Период: {report.DateFrom:dd.MM.yyyy} - {report.DateTo:dd.MM.yyyy}";
                if (report.FilterType.HasValue)
                {
                    periodText += $" | {(report.FilterType == OperationType.Income ? "Доходы" : "Расходы")}";
                }
                if (!string.IsNullOrEmpty(report.FilterCategoryName))
                {
                    periodText += $" | {report.FilterCategoryName}";
                }

                worksheet.Cell("A2").Value = periodText;
                worksheet.Range("A2:E2").Merge();

                // Сводка
                worksheet.Cell("A4").Value = "Операций:";
                worksheet.Cell("B4").Value = report.TransactionCount;

                worksheet.Cell("A5").Value = "Доходы:";
                worksheet.Cell("B5").Value = report.TotalIncome;
                worksheet.Cell("B5").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";
                worksheet.Cell("B5").Style.Font.FontColor = XLColor.Green;

                worksheet.Cell("A6").Value = "Расходы:";
                worksheet.Cell("B6").Value = report.TotalExpense;
                worksheet.Cell("B6").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";
                worksheet.Cell("B6").Style.Font.FontColor = XLColor.Red;

                worksheet.Cell("A7").Value = "Разница:";
                worksheet.Cell("B7").Value = report.NetAmount;
                worksheet.Cell("B7").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";
                worksheet.Cell("B7").Style.Font.FontColor = report.NetAmount >= 0 ? XLColor.Blue : XLColor.Red;
                worksheet.Cell("B7").Style.Font.Bold = true;

                // Таблица операций
                if (report.Transactions != null && report.Transactions.Any())
                {
                    var currentRow = 9;

                    // Заголовки
                    worksheet.Cell(currentRow, 1).Value = "Дата";
                    worksheet.Cell(currentRow, 2).Value = "Тип";
                    worksheet.Cell(currentRow, 3).Value = "Сумма";
                    worksheet.Cell(currentRow, 4).Value = "Категория";
                    worksheet.Cell(currentRow, 5).Value = "Описание";

                    var headerRange = worksheet.Range(currentRow, 1, currentRow, 5);
                    headerRange.Style.Font.Bold = true;
                    headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                    headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

                    currentRow++;

                    // Данные
                    foreach (var transaction in report.Transactions)
                    {
                        worksheet.Cell(currentRow, 1).Value = transaction.Date.ToString("dd.MM.yyyy");

                        worksheet.Cell(currentRow, 2).Value = transaction.TypeName;
                        worksheet.Cell(currentRow, 2).Style.Font.FontColor =
                            transaction.Type == OperationType.Income ? XLColor.Green : XLColor.Red;

                        worksheet.Cell(currentRow, 3).Value = transaction.Amount;
                        worksheet.Cell(currentRow, 3).Style.NumberFormat.Format = "#,##0.00 \" руб.\"";

                        worksheet.Cell(currentRow, 4).Value = transaction.CategoryName;
                        worksheet.Cell(currentRow, 5).Value = transaction.Description ?? "";

                        currentRow++;
                    }
                }

                // Автоширина колонок
                worksheet.Columns().AdjustToContents();

                workbook.SaveAs(filePath);
                _log.Information("Экспорт Excel (детализация): {FilePath}", filePath);
                return filePath;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Ошибка экспорта детализации операций в Excel");
                    throw;
                }
            });
        }

        #endregion

        #region Debt Report

        /// <summary>
        /// Экспорт отчета по долгам в PDF
        /// </summary>
        public Task<string> ExportDebtReportToPdfAsync(DebtReport report)
        {
            return Task.Run(() =>
            {
                try
                {
                    var fileName = $"Долги_{report.DateFrom:dd.MM.yyyy}-{report.DateTo:dd.MM.yyyy}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    var filePath = Path.Combine(_exportFolder, fileName);

                    Document.Create(container =>
                    {
                        container.Page(page =>
                        {
                            page.Size(PageSizes.A4.Landscape());
                            page.Margin(30);
                            page.DefaultTextStyle(x => x.FontSize(10));
                            page.Header().Element(c => ComposeDebtHeader(c, report));
                            page.Content().Element(c => ComposeDebtContent(c, report));
                            page.Footer().AlignCenter().Text(text =>
                            {
                                text.Span("AutoKassa | Сформировано: ");
                                text.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm"));
                            });
                        });
                    }).GeneratePdf(filePath);

                    _log.Information("Экспорт PDF (долги): {FilePath}", filePath);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Ошибка экспорта отчёта по долгам в PDF");
                    throw;
                }
            });
        }

        private void ComposeDebtHeader(IContainer container, DebtReport report)
        {
            container.Column(column =>
            {
                column.Item().AlignCenter().Text("Отчет: Долги")
                    .FontSize(18).Bold();

                column.Item().AlignCenter().Text($"{report.DateFrom:dd.MM.yyyy} - {report.DateTo:dd.MM.yyyy}")
                    .FontSize(12);

                column.Item().PaddingVertical(10).LineHorizontal(1);
            });
        }

        private void ComposeDebtContent(IContainer container, DebtReport report)
        {
            container.Column(column =>
            {
                // Сводка
                column.Item().PaddingBottom(20).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Нам должны:").FontSize(11);
                        c.Item().Text($"{report.TotalReceivable:N2} руб.").FontSize(14).Bold().FontColor(Colors.Green.Medium);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Мы должны:").FontSize(11);
                        c.Item().Text($"{report.TotalPayable:N2} руб.").FontSize(14).Bold().FontColor(Colors.Red.Medium);
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Активно к получению:").FontSize(11);
                        c.Item().Text($"{report.ActiveReceivable:N2} руб.").FontSize(14).Bold();
                    });

                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Активно к оплате:").FontSize(11);
                        c.Item().Text($"{report.ActivePayable:N2} руб.").FontSize(14).Bold();
                    });
                });

                // Таблица долгов
                if (report.Items != null && report.Items.Any())
                {
                    column.Item().Text("Список долгов").FontSize(12).Bold();
                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(70);  // Дата
                            columns.RelativeColumn(2);   // Контрагент
                            columns.RelativeColumn();    // Тип
                            columns.RelativeColumn(2);   // Категория
                            columns.ConstantColumn(80);  // Сумма
                            columns.ConstantColumn(80);  // Погашено
                            columns.ConstantColumn(80);  // Остаток
                            columns.ConstantColumn(90);  // Статус
                            columns.RelativeColumn(2);   // Описание
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Дата").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Контрагент").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Тип").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Категория").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Сумма").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Погашено").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("Остаток").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Статус").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text("Описание").Bold();
                        });

                        foreach (var item in report.Items)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(item.Date.ToString("dd.MM.yyyy"));

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(item.CounterpartyName);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(item.CounterpartyType.ToString());

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(item.CategoryName);

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text($"{item.Amount:N2}");

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text($"{item.RepaidAmount:N2}");

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .AlignRight().Text($"{item.RemainingAmount:N2}");

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(item.Status.ToString());

                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5)
                                .Text(item.Description ?? "");
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Экспорт отчета по долгам в Excel
        /// </summary>
        public Task<string> ExportDebtReportToExcelAsync(DebtReport report)
        {
            return Task.Run(() =>
            {
                try
                {
                    var fileName = $"Долги_{report.DateFrom:dd.MM.yyyy}-{report.DateTo:dd.MM.yyyy}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                    var filePath = Path.Combine(_exportFolder, fileName);

                    using var workbook = new XLWorkbook();
                    var worksheet = workbook.Worksheets.Add("Долги");

                    // Заголовок
                    worksheet.Cell("A1").Value = "Отчет: Долги";
                    worksheet.Cell("A1").Style.Font.Bold = true;
                    worksheet.Cell("A1").Style.Font.FontSize = 16;
                    worksheet.Range("A1:I1").Merge();

                    worksheet.Cell("A2").Value = $"Период: {report.DateFrom:dd.MM.yyyy} - {report.DateTo:dd.MM.yyyy}";
                    worksheet.Range("A2:I2").Merge();

                    // Сводка
                    worksheet.Cell("A4").Value = "Нам должны:";
                    worksheet.Cell("B4").Value = report.TotalReceivable;
                    worksheet.Cell("B4").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";
                    worksheet.Cell("B4").Style.Font.FontColor = XLColor.Green;

                    worksheet.Cell("A5").Value = "Мы должны:";
                    worksheet.Cell("B5").Value = report.TotalPayable;
                    worksheet.Cell("B5").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";
                    worksheet.Cell("B5").Style.Font.FontColor = XLColor.Red;

                    worksheet.Cell("A6").Value = "Активно к получению:";
                    worksheet.Cell("B6").Value = report.ActiveReceivable;
                    worksheet.Cell("B6").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";

                    worksheet.Cell("A7").Value = "Активно к оплате:";
                    worksheet.Cell("B7").Value = report.ActivePayable;
                    worksheet.Cell("B7").Style.NumberFormat.Format = "#,##0.00 \" руб.\"";

                    // Таблица долгов
                    if (report.Items != null && report.Items.Any())
                    {
                        var currentRow = 9;

                        worksheet.Cell(currentRow, 1).Value = "Дата";
                        worksheet.Cell(currentRow, 2).Value = "Контрагент";
                        worksheet.Cell(currentRow, 3).Value = "Тип контрагента";
                        worksheet.Cell(currentRow, 4).Value = "Категория";
                        worksheet.Cell(currentRow, 5).Value = "Сумма";
                        worksheet.Cell(currentRow, 6).Value = "Погашено";
                        worksheet.Cell(currentRow, 7).Value = "Остаток";
                        worksheet.Cell(currentRow, 8).Value = "Статус";
                        worksheet.Cell(currentRow, 9).Value = "Описание";

                        var headerRange = worksheet.Range(currentRow, 1, currentRow, 9);
                        headerRange.Style.Font.Bold = true;
                        headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

                        currentRow++;

                        foreach (var item in report.Items)
                        {
                            worksheet.Cell(currentRow, 1).Value = item.Date.ToString("dd.MM.yyyy");
                            worksheet.Cell(currentRow, 2).Value = item.CounterpartyName;
                            worksheet.Cell(currentRow, 3).Value = item.CounterpartyType.ToString();
                            worksheet.Cell(currentRow, 4).Value = item.CategoryName;
                            worksheet.Cell(currentRow, 5).Value = item.Amount;
                            worksheet.Cell(currentRow, 5).Style.NumberFormat.Format = "#,##0.00";
                            worksheet.Cell(currentRow, 6).Value = item.RepaidAmount;
                            worksheet.Cell(currentRow, 6).Style.NumberFormat.Format = "#,##0.00";
                            worksheet.Cell(currentRow, 7).Value = item.RemainingAmount;
                            worksheet.Cell(currentRow, 7).Style.NumberFormat.Format = "#,##0.00";
                            worksheet.Cell(currentRow, 8).Value = item.Status.ToString();
                            worksheet.Cell(currentRow, 9).Value = item.Description ?? "";

                            currentRow++;
                        }

                        var dataRange = worksheet.Range(9, 1, currentRow - 1, 9);
                        dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                        dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    }

                    worksheet.Columns().AdjustToContents();

                    workbook.SaveAs(filePath);
                    _log.Information("Экспорт Excel (долги): {FilePath}", filePath);
                    return filePath;
                }
                catch (Exception ex)
                {
                    _log.Error(ex, "Ошибка экспорта отчёта по долгам в Excel");
                    throw;
                }
            });
        }

        #endregion
    }
}
