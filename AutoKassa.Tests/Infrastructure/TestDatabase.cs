using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AutoKassa.Tests.Infrastructure
{
    /// <summary>
    /// Фабрика контекстов для тестов SettingsService (требует IDbContextFactory).
    /// </summary>
    public class TestDbContextFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;

        public TestDbContextFactory(DbContextOptions<AppDbContext> options)
        {
            _options = options;
        }

        public AppDbContext CreateDbContext() => new AppDbContext(_options);
    }

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
        /// Создаёт БД с IDbContextFactory для тестов SettingsService.
        /// </summary>
        public static (TestDbContextFactory factory, SqliteConnection connection) CreateWithFactory()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            connection.Open();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(connection)
                .Options;

            // Создаём схему + seed
            using (var ctx = new AppDbContext(options))
            {
                ctx.Database.EnsureCreated();
            }

            var factory = new TestDbContextFactory(options);
            return (factory, connection);
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
            string description = "",
            int? creditCardId = null)
        {
            var t = new Transaction
            {
                CategoryId  = categoryId,
                Amount      = amount,
                Type        = type,
                PaymentType = paymentType,
                Date        = date ?? DateTime.Today,
                Description = description,
                CreditCardId = creditCardId,
                CreatedAt   = DateTime.Now,
                IsDeleted   = false
            };
            ctx.Transactions.Add(t);
            ctx.SaveChanges();
            return t;
        }

        /// <summary>
        /// Добавить кредитную карту с заданными параметрами.
        /// </summary>
        public static CreditCard SeedCreditCard(
            AppDbContext ctx,
            string name = "Тест-карта",
            decimal limit = 100000m,
            decimal initialDebt = 0m,
            decimal minimumPaymentPercent = 5m)
        {
            var card = new CreditCard
            {
                Name = name,
                Limit = limit,
                InitialDebt = initialDebt,
                MinimumPaymentPercent = minimumPaymentPercent,
                PaymentDay = 10,
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            ctx.CreditCards.Add(card);
            ctx.SaveChanges();
            return card;
        }

        /// <summary>
        /// Добавить покупку по кредитной карте.
        /// </summary>
        public static CreditCardPurchase SeedCreditCardPurchase(
            AppDbContext ctx,
            int creditCardId,
            int transactionId,
            decimal amount)
        {
            var purchase = new CreditCardPurchase
            {
                CreditCardId = creditCardId,
                TransactionId = transactionId,
                Amount = amount,
                RemainingDebt = amount,
                PurchaseDate = DateTime.Today
            };
            ctx.CreditCardPurchases.Add(purchase);
            ctx.SaveChanges();
            return purchase;
        }
    }
}
