using AutoKassa.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AutoKassa.Tests.Services
{
    /// <summary>
    /// Регрессионный тест: при пулинге контекстов (AddPooledDbContextFactory) опции должны
    /// конфигурироваться при регистрации в DI, а не в AppDbContext.OnConfiguring — иначе
    /// EF Core бросает InvalidOperationException "'OnConfiguring' cannot be used to modify
    /// DbContextOptions when DbContext pooling is enabled" при первом обращении к БД
    /// (баг запуска приложения на MigrateAsync).
    /// </summary>
    public class PooledDbContextFactoryTests
    {
        [Fact]
        public async Task PooledFactory_MigrateAsync_Works()
        {
            var dbPath = Path.Combine(Path.GetTempPath(), $"AutoKassa_Pooled_{Guid.NewGuid():N}.db");
            try
            {
                // Регистрация повторяет App.ConfigureServices
                var services = new ServiceCollection();
                services.AddPooledDbContextFactory<AppDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={dbPath}");
                    options.AddInterceptors(new SqlitePragmaInterceptor());
                });
                using var provider = services.BuildServiceProvider();

                var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();

                // Ровно тот путь, который падал при запуске приложения
                await using var context = await factory.CreateDbContextAsync();
                await context.Database.MigrateAsync();

                // Миграции применились: схема и seed-категории на месте
                (await context.Categories.CountAsync()).Should().BeGreaterThan(0);
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                foreach (var file in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
                {
                    if (File.Exists(file))
                        File.Delete(file);
                }
            }
        }
    }
}
