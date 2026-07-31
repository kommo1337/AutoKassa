using System.Text.Json;
using AutoKassa.DocValidator.JsonCatalog;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;
using AutoKassa.DocValidator.Validators;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.Validators;

public class ModelValidatorTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        foreach (string dir in _tempDirectories)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public void Validate_WithMatchingEntityAndDbSet_Passes()
    {
        ValidationContext context = CreateContext(
            codeModel: @"
namespace AutoKassa.Models
{
    public class Transaction
    {
        public int Id { get; set; }
    }
}
",
            appDbContext: @"
namespace AutoKassa.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<Transaction> Transactions { get; set; }
    }
}
",
            catalogEntry: new EntityEntry
            {
                Name = "Transaction",
                File = "AutoKassa/Models/Model.cs",
                Table = "Transactions",
            });

        var validator = new ModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMissingEntityInCatalog_Fails()
    {
        ValidationContext context = CreateContext(
            codeModel: @"
namespace AutoKassa.Models
{
    public class Transaction
    {
        public int Id { get; set; }
    }
}
",
            appDbContext: @"
namespace AutoKassa.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<Transaction> Transactions { get; set; }
    }
}
",
            catalogEntry: null);

        var validator = new ModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Transaction") && e.Contains("missing in models.json"));
    }

    [Fact]
    public void Validate_WithTableMismatch_Fails()
    {
        ValidationContext context = CreateContext(
            codeModel: @"
namespace AutoKassa.Models
{
    public class Transaction
    {
        public int Id { get; set; }
    }
}
",
            appDbContext: @"
namespace AutoKassa.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<Transaction> Transactions { get; set; }
    }
}
",
            catalogEntry: new EntityEntry
            {
                Name = "Transaction",
                File = "AutoKassa/Models/Model.cs",
                Table = "Operations",
            });

        var validator = new ModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("table mismatch"));
    }

    [Fact]
    public void Validate_WithDto_DoesNotRequireDbSet()
    {
        ValidationContext context = CreateContext(
            codeModel: @"
namespace AutoKassa.Models
{
    public class DailyTotalsItem
    {
        public DateTime Date { get; set; }
    }
}
",
            appDbContext: @"
namespace AutoKassa.Services
{
    public class AppDbContext : DbContext
    {
    }
}
",
            catalogEntry: new EntityEntry
            {
                Name = "DailyTotalsItem",
                File = "AutoKassa/Models/Model.cs",
                Table = null,
                IsDto = true,
            });

        var validator = new ModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMissingDbSet_Fails()
    {
        ValidationContext context = CreateContext(
            codeModel: @"
namespace AutoKassa.Models
{
    public class Transaction
    {
        public int Id { get; set; }
    }
}
",
            appDbContext: @"
namespace AutoKassa.Services
{
    public class AppDbContext : DbContext
    {
    }
}
",
            catalogEntry: new EntityEntry
            {
                Name = "Transaction",
                File = "AutoKassa/Models/Model.cs",
                Table = "Transactions",
            });

        var validator = new ModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("DbSet<Transaction>") && e.Contains("not found") || e.Contains("DbSet<Transaction>") && e.Contains("found"));
    }

    [Fact]
    public void Validate_WithMigrationMismatch_Fails()
    {
        ValidationContext context = CreateContext(
            codeModel: @"
namespace AutoKassa.Models
{
    public class Transaction
    {
        public int Id { get; set; }
    }
}
",
            appDbContext: @"
namespace AutoKassa.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<Transaction> Transactions { get; set; }
    }
}
",
            catalogEntry: new EntityEntry
            {
                Name = "Transaction",
                File = "AutoKassa/Models/Transaction.cs",
                Table = "Transactions",
            },
            migrations: new[] { "InitialCreate" });

        var validator = new ModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("InitialCreate") && e.Contains("not found"));
    }

    private ValidationContext CreateContext(
        string codeModel,
        string appDbContext,
        EntityEntry? catalogEntry,
        string[]? migrations = null)
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        string modelsDir = Path.Combine(appPath, "Models");
        string servicesDir = Path.Combine(appPath, "Services");
        string migrationsDir = Path.Combine(appPath, "Migrations");
        string docsPath = Path.Combine(projectRoot, "docs", "kimi", "json");
        Directory.CreateDirectory(modelsDir);
        Directory.CreateDirectory(servicesDir);
        Directory.CreateDirectory(migrationsDir);
        Directory.CreateDirectory(docsPath);

        File.WriteAllText(Path.Combine(modelsDir, "Model.cs"), codeModel);
        File.WriteAllText(Path.Combine(servicesDir, "AppDbContext.cs"), appDbContext);

        var modelsCatalog = new ModelsCatalog();
        if (catalogEntry is not null)
        {
            modelsCatalog.Entities.Add(catalogEntry);
        }

        foreach (string migration in migrations ?? Array.Empty<string>())
        {
            modelsCatalog.Migrations.Add(new MigrationEntry { Name = migration, File = $"AutoKassa/Migrations/{migration}.cs" });
        }

        File.WriteAllText(Path.Combine(docsPath, "models.json"), JsonSerializer.Serialize(modelsCatalog));
        WriteMinimalCatalogs(docsPath);

        return CatalogLoader.Load(projectRoot, docsPath, appPath);
    }

    private static void WriteMinimalCatalogs(string docsPath)
    {
        File.WriteAllText(Path.Combine(docsPath, "services.json"), "{ \"services\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "viewmodels.json"), "{ \"viewModels\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "enums.json"), "{ \"enums\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "relationships.json"), "{ \"viewsToViewModels\": [], \"viewModelsToServices\": [], \"servicesToServices\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "flows.json"), "{ \"flows\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "traps.json"), "{ \"traps\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "metadata.json"), "{ \"version\": \"1.0.0\" }");
        File.WriteAllText(Path.Combine(docsPath, "index.json"), "{ \"structure\": [], \"skills\": [], \"keywords\": [], \"iNeedTo\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "constants.json"), "{ \"categories\": [] }");
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }
}
