using System.Text.Json;
using AutoKassa.DocValidator.JsonCatalog;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;
using AutoKassa.DocValidator.Validators;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.Validators;

public class ViewValidatorTests : IDisposable
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
    public void Validate_WithMatchingViews_Passes()
    {
        ValidationContext context = CreateContext(
            views: new[] { "AutoKassa/Views/DashboardView.xaml" },
            viewModels: new[] { "DashboardViewModel" },
            catalogEntries: new[] { new ViewToViewModel { View = "AutoKassa/Views/DashboardView.xaml", ViewModel = "DashboardViewModel" } });

        var validator = new ViewValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNullViewModel_Passes()
    {
        ValidationContext context = CreateContext(
            views: new[] { "AutoKassa/Views/ToastOverlayView.xaml" },
            viewModels: Array.Empty<string>(),
            catalogEntries: new[] { new ViewToViewModel { View = "AutoKassa/Views/ToastOverlayView.xaml", ViewModel = null } });

        var validator = new ViewValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMissingViewInCatalog_Fails()
    {
        ValidationContext context = CreateContext(
            views: new[] { "AutoKassa/Views/DashboardView.xaml" },
            viewModels: Array.Empty<string>(),
            catalogEntries: Array.Empty<ViewToViewModel>());

        var validator = new ViewValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("DashboardView.xaml") && e.Contains("missing in relationships.json"));
    }

    [Fact]
    public void Validate_WithExtraViewInCatalog_Fails()
    {
        ValidationContext context = CreateContext(
            views: Array.Empty<string>(),
            viewModels: Array.Empty<string>(),
            catalogEntries: new[] { new ViewToViewModel { View = "AutoKassa/Views/MissingView.xaml", ViewModel = "MissingViewModel" } });

        var validator = new ViewValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MissingView.xaml") && e.Contains("not found"));
    }

    [Fact]
    public void Validate_WithMissingViewModelFile_Fails()
    {
        ValidationContext context = CreateContext(
            views: new[] { "AutoKassa/Views/DashboardView.xaml" },
            viewModels: Array.Empty<string>(),
            catalogEntries: new[] { new ViewToViewModel { View = "AutoKassa/Views/DashboardView.xaml", ViewModel = "NonExistentViewModel" } });

        var validator = new ViewValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NonExistentViewModel") && e.Contains("does not exist"));
    }

    [Fact]
    public void Validate_WithNestedViewModel_FindsFile()
    {
        ValidationContext context = CreateContext(
            views: new[] { "AutoKassa/Views/Reports/BalanceReportView.xaml" },
            viewModels: new[] { "Reports/BalanceReportViewModel" },
            catalogEntries: new[] { new ViewToViewModel { View = "AutoKassa/Views/Reports/BalanceReportView.xaml", ViewModel = "BalanceReportViewModel" } });

        var validator = new ViewValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    private ValidationContext CreateContext(string[] views, string[] viewModels, ViewToViewModel[] catalogEntries)
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        string docsPath = Path.Combine(projectRoot, "docs", "kimi", "json");
        Directory.CreateDirectory(docsPath);
        Directory.CreateDirectory(Path.Combine(appPath, "ViewModels"));

        foreach (string view in views)
        {
            string path = Path.Combine(projectRoot, view);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "<UserControl/>");
        }

        foreach (string viewModel in viewModels)
        {
            string path = Path.Combine(appPath, "ViewModels", viewModel + ".cs");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "public class " + Path.GetFileNameWithoutExtension(path) + " { }");
        }

        var relationshipsCatalog = new RelationshipsCatalog
        {
            ViewsToViewModels = catalogEntries.ToList(),
        };
        File.WriteAllText(Path.Combine(docsPath, "relationships.json"), JsonSerializer.Serialize(relationshipsCatalog));

        WriteMinimalCatalogs(docsPath);

        return CatalogLoader.Load(projectRoot, docsPath, appPath);
    }

    private static void WriteMinimalCatalogs(string docsPath)
    {
        File.WriteAllText(Path.Combine(docsPath, "services.json"), "{ \"services\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "viewmodels.json"), "{ \"viewModels\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "models.json"), "{ \"entities\": [], \"migrations\": [] }");
        File.WriteAllText(Path.Combine(docsPath, "enums.json"), "{ \"enums\": [] }");
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
