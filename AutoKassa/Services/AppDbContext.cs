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

            // ========== TRANSACTION ==========

            // Связь Transaction -> Category (один ко многим)
            modelBuilder.Entity<Transaction>()
                .HasOne(t => t.Category)
                .WithMany(c => c.Transactions)
                .HasForeignKey(t => t.CategoryId)
                .OnDelete(DeleteBehavior.Restrict); // Запрет каскадного удаления

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

            // Составной индекс для частых запросов
            modelBuilder.Entity<Transaction>()
                .HasIndex(t => new { t.Date, t.IsDeleted })
                .HasDatabaseName("IX_Transaction_Date_IsDeleted");

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
                    DefaultOperationType = 2
                }
            );
        }
    }
}