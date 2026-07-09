using System.Text.Json;
using AutoKassa.DocValidator.JsonCatalog;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;
using AutoKassa.DocValidator.Validators;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.Validators;

public class TrapSyncValidatorTests : IDisposable
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
    public void Validate_WithMatchingTraps_Passes()
    {
        ValidationContext context = CreateContext(
            markdownTraps: new[]
            {
                ("## 1. Category One", "### 1.1. First Trap"),
                ("## 1. Category One", "### 1.2. `Typed` Trap"),
                ("## 2. Category Two", "### 2.1. Third Trap"),
            },
            jsonTraps: new[]
            {
                new TrapEntry { Category = "Category One", Title = "First Trap", Files = new() { "../../AutoKassa/App.xaml.cs" } },
                new TrapEntry { Category = "Category One", Title = "Typed Trap", Files = new() { "../../AutoKassa/App.xaml.cs" } },
                new TrapEntry { Category = "Category Two", Title = "Third Trap", Files = new() { "../../AutoKassa/App.xaml.cs" } },
            },
            existingFiles: new[] { "AutoKassa/App.xaml.cs" });

        var validator = new TrapSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMarkdownTrapMissingInJson_Fails()
    {
        ValidationContext context = CreateContext(
            markdownTraps: new[]
            {
                ("## 1. Category", "### 1.1. Only In Markdown"),
            },
            jsonTraps: Array.Empty<TrapEntry>(),
            existingFiles: Array.Empty<string>());

        var validator = new TrapSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Only In Markdown") && e.Contains("missing in traps.json"));
    }

    [Fact]
    public void Validate_WithJsonTrapMissingInMarkdown_Fails()
    {
        ValidationContext context = CreateContext(
            markdownTraps: Array.Empty<(string, string)>(),
            jsonTraps: new[]
            {
                new TrapEntry { Category = "Category", Title = "Only In Json", Files = new() { "../../AutoKassa/App.xaml.cs" } },
            },
            existingFiles: new[] { "AutoKassa/App.xaml.cs" });

        var validator = new TrapSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Only In Json") && e.Contains("missing in TRAPS.md"));
    }

    [Fact]
    public void Validate_WithMissingFile_Fails()
    {
        ValidationContext context = CreateContext(
            markdownTraps: new[]
            {
                ("## 1. Category", "### 1.1. Trap"),
            },
            jsonTraps: new[]
            {
                new TrapEntry { Category = "Category", Title = "Trap", Files = new() { "../../AutoKassa/Missing.cs" } },
            },
            existingFiles: Array.Empty<string>());

        var validator = new TrapSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Missing.cs") && e.Contains("does not exist"));
    }

    [Fact]
    public void Validate_WithMetaDocumentationFiles_Passes()
    {
        ValidationContext context = CreateContext(
            markdownTraps: new[]
            {
                ("## 1. Category", "### 1.1. Documentation Trap"),
            },
            jsonTraps: new[]
            {
                new TrapEntry { Category = "Category", Title = "Documentation Trap", Files = new() { "TRAPS.md", "json/traps.json" } },
            },
            existingFiles: Array.Empty<string>());

        var validator = new TrapSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithDirectoryReference_Passes()
    {
        ValidationContext context = CreateContext(
            markdownTraps: new[]
            {
                ("## 1. Category", "### 1.1. Migration Trap"),
            },
            jsonTraps: new[]
            {
                new TrapEntry { Category = "Category", Title = "Migration Trap", Files = new() { "../../AutoKassa/Migrations/" } },
            },
            existingFiles: new[] { "AutoKassa/Migrations/.gitkeep" });

        var validator = new TrapSyncValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    private ValidationContext CreateContext(
        (string CategoryLine, string TitleLine)[] markdownTraps,
        TrapEntry[] jsonTraps,
        string[] existingFiles)
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        string docsPath = Path.Combine(projectRoot, "docs", "kimi", "json");
        Directory.CreateDirectory(docsPath);

        WriteTrapsMarkdown(projectRoot, markdownTraps);
        WriteCatalogs(docsPath, jsonTraps);
        CreateFiles(projectRoot, existingFiles);

        return CatalogLoader.Load(projectRoot, docsPath, appPath);
    }

    private static void WriteTrapsMarkdown(string projectRoot, (string CategoryLine, string TitleLine)[] traps)
    {
        string trapsPath = Path.Combine(projectRoot, "docs", "kimi", "TRAPS.md");
        Directory.CreateDirectory(Path.GetDirectoryName(trapsPath)!);

        using var writer = new StreamWriter(trapsPath);
        foreach ((string category, string title) in traps)
        {
            writer.WriteLine(category);
            writer.WriteLine(title);
        }
    }

    private static void WriteCatalogs(string docsPath, TrapEntry[] traps)
    {
        var trapsCatalog = new TrapsCatalog { Traps = traps.ToList() };
        File.WriteAllText(Path.Combine(docsPath, "traps.json"), JsonSerializer.Serialize(trapsCatalog));

        File.WriteAllText(Path.Combine(docsPath, "services.json"), "{ \"services\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "viewmodels.json"), "{ \"viewModels\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "models.json"), "{ \"entities\": [], \"migrations\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "enums.json"), "{ \"enums\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "relationships.json"), "{ \"viewsToViewModels\": [], \"viewModelsToServices\": [], \"servicesToServices\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "flows.json"), "{ \"flows\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "metadata.json"), "{ \"version\": \"1.0.0\" }");
        File.WriteAllText(Path.Combine(docsPath, "index.json"), "{ \"structure\": [], \"skills\": [], \"keywords\": [], \"iNeedTo\": [] }");
    }

    private static void CreateFiles(string projectRoot, string[] relativePaths)
    {
        foreach (string relativePath in relativePaths)
        {
            string path = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, string.Empty);
        }
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }
}
