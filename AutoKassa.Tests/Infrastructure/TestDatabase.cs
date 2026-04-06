using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AutoKassa.Tests.Infrastructure
{
    /// <summary>
    /// Хелпер для создания изолированной SQLite in-memory базы на каждый тест.
    /// </summary>
    public static class TestDatabase
    {
        /// <summary>
        /// Создаёт открытое соединение, применяет схему и seed-данные.
        /// Вызывающий код обязан вызвать connection.Dispose() после теста.
        /// </summary>
        public static (AppDbContext context, SqliteConnection connection) Create()
        {
            // Держим соединение открытым всё время теста — иначе in-memory БД исчезает
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            var context = new AppDbContext(options);
            context.Database.EnsureCreated(); // схема + seed-категории из OnModelCreating

            return (context, connection);
        }

        /// <summary>
        /// Добавить категорию расходов с уникальным именем для теста.
        /// </summary>
        public static Category SeedExpenseCategory(AppDbContext ctx, string name = "Тест-расход")
        {
            var cat = new Category
            {
                Name = name,
                Type = OperationType.Expense,
                IsActive = true,
                IsSystem = false,
                Color = "#aabbcc",
                CreatedAt = DateTime.Now
            };
            ctx.Categories.Add(cat);
            ctx.SaveChanges();
            return cat;
        }

        /// <summary>
        /// Добавить категорию доходов с уникальным именем для теста.
        /// </summary>
        public static Category SeedIncomeCategory(AppDbContext ctx, string name = "Тест-доход")
        {
            var cat = new Category
            {
                Name = name,
                Type = OperationType.Income,
                IsActive = true,
                IsSystem = false,
                Color = "#112233",
                CreatedAt = DateTime.Now
            };
            ctx.Categories.Add(cat);
            ctx.SaveChanges();
            return cat;
        }

        /// <summary>
        /// Добавить транзакцию с заданными параметрами.
        /// </summary>
        public static Transaction SeedTransaction(
            AppDbContext ctx,
            int categoryId,
            decimal amount,
            OperationType type = OperationType.Expense,
            PaymentType paymentType = PaymentType.Cash,
            DateTime? date = null,
            string description = "")
        {
            var t = new Transaction
            {
                CategoryId  = categoryId,
                Amount      = amount,
                Type        = type,
                PaymentType = paymentType,
                Date        = date ?? DateTime.Today,
                Description = description,
                CreatedAt   = DateTime.Now,
                IsDeleted   = false
            };
            ctx.Transactions.Add(t);
            ctx.SaveChanges();
            return t;
        }
    }
}
