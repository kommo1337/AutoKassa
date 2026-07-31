using System.Text.Json;
using AutoKassa.DocValidator.JsonCatalog;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;
using AutoKassa.DocValidator.Validators;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.Validators;

public class EnumValidatorTests : IDisposable
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
    public void Validate_WithMatchingEnum_Passes()
    {
        ValidationContext context = CreateContext(
            codeEnum: @"
namespace AutoKassa.Models.Enums
{
    public enum OperationType
    {
        Income = 1,
        Expense = 2
    }
}
",
            catalogEntry: new EnumEntry
            {
                Name = "OperationType",
                File = "AutoKassa/Models/Enums/OperationType.cs",
                UnderlyingType = "int",
                Values =
                {
                    new EnumValue { Name = "Income", Value = 1 },
                    new EnumValue { Name = "Expense", Value = 2 },
                },
            });

        var validator = new EnumValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMissingEnumInCatalog_Fails()
    {
        ValidationContext context = CreateContext(
            codeEnum: @"
namespace AutoKassa.Models.Enums
{
    public enum OperationType
    {
        Income = 1,
        Expense = 2
    }
}
",
            catalogEntry: null);

        var validator = new EnumValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("OperationType") && e.Contains("missing in enums.json"));
    }

    [Fact]
    public void Validate_WithExtraEnumInCatalog_Fails()
    {
        ValidationContext context = CreateContext(
            codeEnum: @"
namespace AutoKassa.Models.Enums
{
    public enum OperationType
    {
        Income = 1,
        Expense = 2
    }
}
",
            catalogEntry: new EnumEntry
            {
                Name = "OperationType",
                File = "AutoKassa/Models/Enums/OperationType.cs",
                UnderlyingType = "int",
                Values =
                {
                    new EnumValue { Name = "Income", Value = 1 },
                    new EnumValue { Name = "Expense", Value = 2 },
                },
            },
            extraCatalogEntries: new EnumEntry
            {
                Name = "MissingEnum",
                File = "AutoKassa/Models/Enums/MissingEnum.cs",
                UnderlyingType = "int",
                Values = { new EnumValue { Name = "Only", Value = 0 } },
            });

        var validator = new EnumValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MissingEnum") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_WithValueMismatch_Fails()
    {
        ValidationContext context = CreateContext(
            codeEnum: @"
namespace AutoKassa.Models.Enums
{
    public enum OperationType
    {
        Income = 1,
        Expense = 2
    }
}
",
            catalogEntry: new EnumEntry
            {
                Name = "OperationType",
                File = "AutoKassa/Models/Enums/OperationType.cs",
                UnderlyingType = "int",
                Values =
                {
                    new EnumValue { Name = "Income", Value = 1 },
                    new EnumValue { Name = "Expense", Value = 99 },
                },
            });

        var validator = new EnumValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Expense") && e.Contains("value mismatch"));
    }

    [Fact]
    public void Validate_WithMissingUsedInFile_Fails()
    {
        ValidationContext context = CreateContext(
            codeEnum: @"
namespace AutoKassa.Models.Enums
{
    public enum OperationType
    {
        Income = 1,
        Expense = 2
    }
}
",
            catalogEntry: new EnumEntry
            {
                Name = "OperationType",
                File = "AutoKassa/Models/Enums/OperationType.cs",
                UnderlyingType = "int",
                Values =
                {
                    new EnumValue { Name = "Income", Value = 1 },
                    new EnumValue { Name = "Expense", Value = 2 },
                },
                UsedIn = { "AutoKassa/Models/NonExistent.cs" },
            });

        var validator = new EnumValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NonExistent.cs") && e.Contains("does not exist"));
    }

    [Fact]
    public void Validate_WithMissingUiMappingFile_Fails()
    {
        ValidationContext context = CreateContext(
            codeEnum: @"
namespace AutoKassa.Models.Enums
{
    public enum OperationType
    {
        Income = 1,
        Expense = 2
    }
}
",
            catalogEntry: new EnumEntry
            {
                Name = "OperationType",
                File = "AutoKassa/Models/Enums/OperationType.cs",
                UnderlyingType = "int",
                Values =
                {
                    new EnumValue { Name = "Income", Value = 1 },
                    new EnumValue { Name = "Expense", Value = 2 },
                },
                UiMappings = new EnumUiMappings
                {
                    TextConverter = "AutoKassa/Helpers/Converters/NonExistentConverter.cs",
                },
            });

        var validator = new EnumValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NonExistentConverter.cs") && e.Contains("does not exist"));
    }

    private ValidationContext CreateContext(string codeEnum, EnumEntry? catalogEntry, params EnumEntry[] extraCatalogEntries)
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        string enumsDir = Path.Combine(appPath, "Models", "Enums");
        string docsPath = Path.Combine(projectRoot, "docs", "kimi", "json");
        Directory.CreateDirectory(enumsDir);
        Directory.CreateDirectory(docsPath);

        File.WriteAllText(Path.Combine(enumsDir, "OperationType.cs"), codeEnum);

        var enumsCatalog = new EnumsCatalog();
        if (catalogEntry is not null)
        {
            enumsCatalog.Enums.Add(catalogEntry);
        }

        enumsCatalog.Enums.AddRange(extraCatalogEntries);

        File.WriteAllText(Path.Combine(docsPath, "enums.json"), JsonSerializer.Serialize(enumsCatalog));

        // CatalogLoader требует все справочники; создаём минимальные заглушки.
        WriteMinimalCatalogs(docsPath);

        return CatalogLoader.Load(projectRoot, docsPath, appPath);
    }

    private static void WriteMinimalCatalogs(string docsPath)
    {
        File.WriteAllText(Path.Combine(docsPath, "services.json"), "{ \"services\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "viewmodels.json"), "{ \"viewModels\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "models.json"), "{ \"entities\": [], \"migrations\": [] }");
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
