using AutoKassa.Models;
using AutoKassa.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace AutoKassa.Services
{
    /// <summary>
    /// Контекст базы данных приложения
    /// </summary>
    public class AppDbContext : DbContext
    {
        public AppDbContext() { }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        /// <summary>
        /// Таблица финансовых операций
        /// </summary>
        public DbSet<Transaction> Transactions { get; set; }

        /// <summary>
        /// Таблица категорий
        /// </summary>
        public DbSet<Category> Categories { get; set; }

        /// <summary>
        /// Таблица настроек приложения
        /// </summary>
        public DbSet<AppSettings> AppSettings { get; set; }

        /// <summary>
        /// Таблица избранных отчетов
        /// </summary>
        public DbSet<FavoriteReport> FavoriteReports { get; set; }

        /// <summary>
        /// Таблица кредитных карт
        /// </summary>
        public DbSet<CreditCard> CreditCards { get; set; }

        /// <summary>
        /// Таблица покупок по кредитным картам
        /// </summary>
        public DbSet<CreditCardPurchase> CreditCardPurchases { get; set; }

        /// <summary>
        /// Конфигурация подключения к БД
        /// </summary>
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Путь к файлу БД в папке приложения
                var dbPath = System.IO.Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "AutoKassa.db"
                );

                optionsBuilder.UseSqlite($"Data Source={dbPath}");
            }
        }

        /// <summary>
        /// Конфигурация моделей и связей
        /// </summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ========== CATEGORY ==========

            // Уникальность: название категории уникально в рамках типа
            modelBuilder.Entity<Category>()
                .HasIndex(c => new { c.Name, c.Type })
                .IsUnique()
                .HasDatabaseName("UQ_Category_Name_Type");

            // Индексы для быстрой фильтрации
            modelBuilder.Entity<Category>()
                .HasIndex(c => c.Type)
                .HasDatabaseName("IX_Category_Type");

            modelBuilder.Entity<Category>()
                .HasIndex(c => c.IsActive)
                .HasDatabaseName("IX_Category_IsActive");

            // ========== CREDIT CARD ==========

            modelBuilder.Entity<CreditCard>()
                .HasIndex(c => c.IsActive)
                .HasDatabaseName("IX_CreditCard_IsActive");

            // ========== CREDIT CARD PURCHASE ==========

            // Связь CreditCardPurchase -> CreditCard
            modelBuilder.Entity<CreditCardPurchase>()
                .HasOne(p => p.CreditCard)
                .WithMany(c => c.Purchases)
                .HasForeignKey(p => p.CreditCardId)
                .OnDelete(DeleteBehavior.Restrict);

            // Связь CreditCardPurchase -> Transaction
            modelBuilder.Entity<CreditCardPurchase>()
                .HasOne(p => p.Transaction)
                .WithMany()
                .HasForeignKey(p => p.TransactionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CreditCardPurchase>()
                .HasIndex(p => p.CreditCardId)
                .HasDatabaseName("IX_CreditCardPurchase_CreditCardId");

            modelBuilder.Entity<CreditCardPurchase>()
                .HasIndex(p => p.TransactionId)
                .HasDatabaseName("IX_CreditCardPurchase_TransactionId");

            modelBuilder.Entity<CreditCardPurchase>()
                .HasIndex(p => p.PurchaseDate)
                .HasDatabaseName("IX_CreditCardPurchase_PurchaseDate");

            // ========== TRANSACTION ==========

            // Связь Transaction -> Category (один ко многим)
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Запрет каскадного удаления

            // Связь Transaction -> CreditCard
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.CreditCard)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CreditCardId)
                .OnDelete(DeleteBehavior.Restrict);

            // Индексы для оптимизации запросов
            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.Date)
                .HasDatabaseName("IX_Transaction_Date");

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.Type)
                .HasDatabaseName("IX_Transaction_Type");

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.CategoryId)
                .HasDatabaseName("IX_Transaction_CategoryId");

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.IsDeleted)
                .HasDatabaseName("IX_Transaction_IsDeleted");

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.PaymentType)
                .HasDatabaseName("IX_Transaction_PaymentType");

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.CreditCardId)
                .HasDatabaseName("IX_Transactions_CreditCardId");

            // Составной индекс для частых запросов
            modelBuilder.Entity<Transaction>()
                .HasIndex(t => new { t.Date, t.IsDeleted })
                .HasDatabaseName("IX_Transaction_Date_IsDeleted");

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => new { t.Date, t.Type, t.IsDeleted })
                .HasDatabaseName("IX_Transaction_Date_Type_IsDeleted");

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => new { t.Type, t.IsDeleted, t.Date })
                .HasDatabaseName("IX_Transaction_Type_IsDeleted_Date");

            modelBuilder.Entity<Transaction>()
                .HasIndex(t => t.CreatedAt)
                .HasDatabaseName("IX_Transaction_CreatedAt");

            // ========== SEED DATA ==========
            SeedData(modelBuilder);
        }

        /// <summary>
        /// Предустановленные данные (seed data)
        /// </summary>
        private void SeedData(ModelBuilder modelBuilder)
        {
            var now = new DateTime(2024, 1, 1); // Фиксированная дата для миграций

            // ========== КАТЕГОРИИ ДОХОДОВ ==========
            modelBuilder.Entity<Category>().HasData(
                new Category
                {
                    Id = 1,
                    Name = "Ремонт авто",
                    Type = OperationType.Income,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#6366f1",
                    SortOrder = 1,
                    CreatedAt = now
                },
                new Category
                {
                    Id = 2,
                    Name = "Ремонт мото",
                    Type = OperationType.Income,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#f59e0b",
                    SortOrder = 2,
                    CreatedAt = now
                },
                new Category
                {
                    Id = 3,
                    Name = "Диагностика",
                    Type = OperationType.Income,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#14b8a6",
                    SortOrder = 3,
                    CreatedAt = now
                },
                new Category
                {
                    Id = 4,
                    Name = "Прочие доходы",
                    Type = OperationType.Income,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#94a3b8",
                    SortOrder = 4,
                    CreatedAt = now
                }
            );

            // ========== КАТЕГОРИИ РАСХОДОВ ==========
            modelBuilder.Entity<Category>().HasData(
                new Category
                {
                    Id = 5,
                    Name = "Запчасти",
                    Type = OperationType.Expense,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#ec4899",
                    SortOrder = 1,
                    CreatedAt = now
                },
                new Category
                {
                    Id = 6,
                    Name = "Зарплата",
                    Type = OperationType.Expense,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#f97316",
                    SortOrder = 2,
                    CreatedAt = now
                },
                new Category
                {
                    Id = 7,
                    Name = "Аренда",
                    Type = OperationType.Expense,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#8b5cf6",
                    SortOrder = 3,
                    CreatedAt = now
                },
                new Category
                {
                    Id = 8,
                    Name = "Коммунальные услуги",
                    Type = OperationType.Expense,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#06b6d4",
                    SortOrder = 4,
                    CreatedAt = now
                },
                new Category
                {
                    Id = 9,
                    Name = "Маркетинг",
                    Type = OperationType.Expense,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#84cc16",
                    SortOrder = 5,
                    CreatedAt = now
                },
                new Category
                {
                    Id = 10,
                    Name = "Прочие расходы",
                    Type = OperationType.Expense,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#ef4444",
                    SortOrder = 6,
                    CreatedAt = now
                },
                new Category
                {
                    Id = 11,
                    Name = "Погашение кредита",
                    Type = OperationType.Expense,
                    IsActive = true,
                    IsSystem = true,
                    Color = "#64748b",
                    SortOrder = 7,
                    CreatedAt = now
                }
            );

            // ========== НАСТРОЙКИ ПО УМОЛЧАНИЮ ==========
            modelBuilder.Entity<AppSettings>().HasData(
                new AppSettings
                {
                    Id = 1,
                    PasswordHash = "",
                    SecurityQuestionId = null,
                    SecurityAnswerHash = null,
                    CustomSecurityQuestion = null,
                    AutoLockTimeout = 10,
                    AutoLockEnabled = true,
                    Theme = "Light",
                    DefaultPeriodFilter = "Month",
                    ShowNotifications = true,
                    ShowOperationsInSidebar = false,
                    DefaultPageSize = 20,
                    ConfirmDelete = true,
                    AutoGenerateReports = false,
                    BackupEnabled = false,
                    BackupFrequency = "Weekly",
                    BackupPath = null,
                    BackupKeepCount = 10,
                    AutoBackupDays = 7,
                    RequirePasswordOnStartup = true,
                    PasswordExpireDays = 0,
                    Language = "ru-RU",
                    WindowWidth = 1200,
                    WindowHeight = 700,
                    DefaultOperationType = 2,
                    InitialBalance = 0m,
                    DefaultPaymentType = 1,
                    CreditCardLimit = 0m,
                    CreditCardCurrentDebt = 0m,
                    CreditCardInterestRate = 0m,
                    CreditCardPaymentDay = 10,
                    CreditCardLastPaymentDate = null,
                    CreditCardMinimumPaymentPercent = 5m
                }
            );
        }
    }
}