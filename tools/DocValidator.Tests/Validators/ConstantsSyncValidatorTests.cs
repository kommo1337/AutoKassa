using AutoKassa.DocValidator.JsonCatalog;
using AutoKassa.DocValidator.Validators;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.Validators;

public class ConstantsSyncValidatorTests : IDisposable
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
    public void Validate_WithNameFoundInSourceFile_Passes()
    {
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""sourceRoot"": ""AutoKassa/"",
  ""categories"": [
    {
      ""name"": ""Database"",
      ""constants"": [
        { ""name"": ""DatabaseFileName"", ""value"": ""AutoKassa.db"", ""type"": ""string"", ""sourceFiles"": [""Services/AppDbContext.cs""], ""description"": ""Имя файла БД"" }
      ]
    }
  ]
}",
            files: new[] { ("AutoKassa/Services/AppDbContext.cs", "const string DatabaseFileName = \"AutoKassa.db\";") });

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithMissingRequiredFields_Fails()
    {
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""categories"": [
    {
      ""name"": ""Broken"",
      ""constants"": [
        { ""name"": ""NoValue"", ""type"": ""int"", ""sourceFiles"": [""a.cs""], ""description"": ""x"" },
        { ""name"": ""NoType"", ""value"": 1, ""sourceFiles"": [""a.cs""], ""description"": ""x"" },
        { ""name"": ""NoDescription"", ""value"": 1, ""type"": ""int"", ""sourceFiles"": [""a.cs""] },
        { ""value"": 1, ""type"": ""int"", ""sourceFiles"": [""a.cs""], ""description"": ""x"" }
      ]
    }
  ]
}",
            files: new[] { ("a.cs", "NoValue NoType NoDescription") });

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NoValue") && e.Contains("'value'"));
        result.Errors.Should().Contain(e => e.Contains("NoType") && e.Contains("'type'"));
        result.Errors.Should().Contain(e => e.Contains("NoDescription") && e.Contains("'description'"));
        result.Errors.Should().Contain(e => e.Contains("'name'"));
    }

    [Fact]
    public void Validate_WithExplicitNullValue_Passes()
    {
        // Поле value присутствует, но равно null — легитимное значение по умолчанию.
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""sourceRoot"": ""AutoKassa/"",
  ""categories"": [
    {
      ""name"": ""AppSettingsDefaults"",
      ""constants"": [
        { ""name"": ""BackupPath"", ""value"": null, ""type"": ""string"", ""sourceFiles"": [""Services/SettingsService.cs""], ""description"": ""Путь к бэкапам (null = по умолчанию)"" }
      ]
    }
  ]
}",
            files: new[] { ("AutoKassa/Services/SettingsService.cs", "public string? BackupPath { get; set; }") });

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithEmptySourceFiles_Fails()
    {
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""categories"": [
    {
      ""name"": ""Broken"",
      ""constants"": [
        { ""name"": ""NoFiles"", ""value"": 1, ""type"": ""int"", ""sourceFiles"": [], ""description"": ""x"" }
      ]
    }
  ]
}",
            files: Array.Empty<(string, string)>());

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NoFiles") && e.Contains("'sourceFiles'"));
    }

    [Fact]
    public void Validate_WithMissingSourceFile_Fails()
    {
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""sourceRoot"": ""AutoKassa/"",
  ""categories"": [
    {
      ""name"": ""Database"",
      ""constants"": [
        { ""name"": ""DatabaseFileName"", ""value"": ""AutoKassa.db"", ""type"": ""string"", ""sourceFiles"": [""Services/Missing.cs""], ""description"": ""x"" }
      ]
    }
  ]
}",
            files: Array.Empty<(string, string)>());

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("DatabaseFileName") && e.Contains("Missing.cs") && e.Contains("does not exist"));
    }

    [Fact]
    public void Validate_WithDocumentationOnlyName_WarnsButDoesNotFail()
    {
        // Имя условное и значение в файле не встречается — warning, а не error.
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""sourceRoot"": ""AutoKassa/"",
  ""categories"": [
    {
      ""name"": ""UiConstants"",
      ""constants"": [
        { ""name"": ""SidebarWidth"", ""value"": 76, ""type"": ""double"", ""sourceFiles"": [""MainWindow.xaml""], ""description"": ""Ширина сайдбара"" }
      ]
    }
  ]
}",
            files: new[] { ("AutoKassa/MainWindow.xaml", "<Window Width=\"1366\" />") });

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Contains("SidebarWidth"));
    }

    [Fact]
    public void Validate_WithDocOnlyFlag_SkipsSearchAndWarning()
    {
        // docOnly-константы — чисто документационные понятия: ни имени, ни значения
        // в коде может не быть, предупреждение при этом не выдаётся.
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""sourceRoot"": ""AutoKassa/"",
  ""categories"": [
    {
      ""name"": ""SecurityAndLocking"",
      ""constants"": [
        { ""name"": ""AutoLockTimerUnit"", ""value"": ""minutes"", ""type"": ""string"", ""sourceFiles"": [""Services/LockService.cs""], ""description"": ""Единицы таймера автоблокировки"", ""docOnly"": true }
      ]
    }
  ]
}",
            files: new[] { ("AutoKassa/Services/LockService.cs", "timer.Interval = TimeSpan.FromMinutes(timeout);") });

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithValueMatchInsteadOfName_PassesWithoutWarnings()
    {
        // Условное имя WindowWidth в XAML не встречается, но значение 1366 — да.
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""sourceRoot"": ""AutoKassa/"",
  ""categories"": [
    {
      ""name"": ""UiConstants"",
      ""constants"": [
        { ""name"": ""WindowWidth"", ""value"": 1366, ""type"": ""double"", ""sourceFiles"": [""MainWindow.xaml""], ""description"": ""Ширина окна"" }
      ]
    }
  ]
}",
            files: new[] { ("AutoKassa/MainWindow.xaml", "<Window Width=\"1366\" />") });

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithIsoDateValue_MatchesDateTimeConstructor()
    {
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""sourceRoot"": ""AutoKassa/"",
  ""categories"": [
    {
      ""name"": ""SeedData"",
      ""constants"": [
        { ""name"": ""SeedDate"", ""value"": ""2024-01-01"", ""type"": ""DateTime"", ""sourceFiles"": [""Services/AppDbContext.cs""], ""description"": ""Фиксированная дата seed"" }
      ]
    }
  ]
}",
            files: new[] { ("AutoKassa/Services/AppDbContext.cs", "var now = new DateTime(2024, 1, 1);") });

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithoutSourceRoot_ResolvesAgainstProjectRoot()
    {
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""categories"": [
    {
      ""name"": ""Logging"",
      ""constants"": [
        { ""name"": ""LogPath"", ""value"": ""logs/autokassa-.log"", ""type"": ""string"", ""sourceFiles"": [""App.xaml.cs""], ""description"": ""Путь к логам"" }
      ]
    }
  ]
}",
            files: new[] { ("App.xaml.cs", "Path.Combine(\"logs\", \"autokassa-.log\")") });

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Validate_WithCategoryWithoutName_Fails()
    {
        ValidationContext context = CreateContext(
            constantsJson: @"{
  ""categories"": [
    { ""constants"": [] }
  ]
}",
            files: Array.Empty<(string, string)>());

        var validator = new ConstantsSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("category without name"));
    }

    private ValidationContext CreateContext(string constantsJson, (string RelativePath, string Content)[] files)
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        string docsPath = Path.Combine(projectRoot, "docs", "kimi", "json");
        Directory.CreateDirectory(docsPath);

        WriteCatalogs(docsPath, constantsJson);

        foreach ((string relativePath, string content) in files)
        {
            string path = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
        }

        return CatalogLoader.Load(projectRoot, docsPath, appPath);
    }

    private static void WriteCatalogs(string docsPath, string constantsJson)
    {
        File.WriteAllText(Path.Combine(docsPath, "constants.json"), constantsJson);

        File.WriteAllText(Path.Combine(docsPath, "services.json"), "{ \"services\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "viewmodels.json"), "{ \"viewModels\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "models.json"), "{ \"entities\": [], \"migrations\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "enums.json"), "{ \"enums\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "relationships.json"), "{ \"viewsToViewModels\": [], \"viewModelsToServices\": [], \"servicesToServices\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "flows.json"), "{ \"flows\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "traps.json"), "{ \"traps\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "metadata.json"), "{ \"version\": \"1.0.0\" }");
        File.WriteAllText(Path.Combine(docsPath, "index.json"), "{ \"structure\": [], \"skills\": [], \"keywords\": [], \"iNeedTo\": [] }");
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }
}
