using System.Text.Json;
using AutoKassa.DocValidator.JsonCatalog;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;
using AutoKassa.DocValidator.Validators;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.Validators;

public class ViewModelValidatorTests : IDisposable
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
    public void Validate_WithMatchingRegisteredViewModel_Passes()
    {
        ValidationContext context = CreateContext(
            codeViewModel: @"
namespace AutoKassa.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        public DashboardViewModel(ITransactionService transactionService)
        {
        }
    }
}
",
            appXamlCs: @"
private void ConfigureServices(IServiceCollection services)
{
    services.AddTransient<DashboardViewModel>();
}
",
            catalogEntry: new ViewModelEntry
            {
                Name = "DashboardViewModel",
                File = "AutoKassa/ViewModels/ViewModel.cs",
                RegisteredInDi = true,
                Dependencies = { "ITransactionService" },
            });

        var validator = new ViewModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithUnregisteredDiViewModel_Fails()
    {
        ValidationContext context = CreateContext(
            codeViewModel: @"
namespace AutoKassa.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        public DashboardViewModel(ITransactionService transactionService)
        {
        }
    }
}
",
            appXamlCs: @"
private void ConfigureServices(IServiceCollection services)
{
}
",
            catalogEntry: new ViewModelEntry
            {
                Name = "DashboardViewModel",
                File = "AutoKassa/ViewModels/ViewModel.cs",
                RegisteredInDi = true,
                Dependencies = { "ITransactionService" },
            });

        var validator = new ViewModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("DashboardViewModel") && e.Contains("registeredInDi"));
    }

    [Fact]
    public void Validate_WithDependencyMismatch_Fails()
    {
        ValidationContext context = CreateContext(
            codeViewModel: @"
namespace AutoKassa.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        public DashboardViewModel(ITransactionService transactionService, ICategoryService categoryService)
        {
        }
    }
}
",
            appXamlCs: @"
private void ConfigureServices(IServiceCollection services)
{
    services.AddTransient<DashboardViewModel>();
}
",
            catalogEntry: new ViewModelEntry
            {
                Name = "DashboardViewModel",
                File = "AutoKassa/ViewModels/ViewModel.cs",
                RegisteredInDi = true,
                Dependencies = { "ITransactionService" },
            });

        var validator = new ViewModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("ICategoryService") && e.Contains("missing in viewmodels.json"));
    }

    [Fact]
    public void Validate_WithFactoryInterfaceRegistration_Passes()
    {
        ValidationContext context = CreateContext(
            codeViewModel: @"
namespace AutoKassa.ViewModels.Reports
{
    public interface IReportViewModelFactory
    {
    }

    public class ReportViewModelFactory : IReportViewModelFactory
    {
        public ReportViewModelFactory(IServiceProvider serviceProvider)
        {
        }
    }
}
",
            appXamlCs: @"
private void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IReportViewModelFactory, ReportViewModelFactory>();
}
",
            catalogEntry: new ViewModelEntry
            {
                Name = "ReportViewModelFactory",
                File = "AutoKassa/ViewModels/ViewModel.cs",
                RegisteredInDi = true,
                Dependencies = { "IServiceProvider" },
            });

        var validator = new ViewModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithInterfaceEntry_PassesWithoutClass()
    {
        ValidationContext context = CreateContext(
            codeViewModel: @"
namespace AutoKassa.ViewModels
{
    public interface IAsyncInitializable
    {
        Task InitializeAsync();
    }
}
",
            appXamlCs: @"
private void ConfigureServices(IServiceCollection services)
{
}
",
            catalogEntry: new ViewModelEntry
            {
                Name = "IAsyncInitializable",
                File = "AutoKassa/ViewModels/ViewModel.cs",
                Category = "interface",
                RegisteredInDi = false,
            });

        var validator = new ViewModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithNestedClass_IgnoresNestedClass()
    {
        ValidationContext context = CreateContext(
            codeViewModel: @"
namespace AutoKassa.ViewModels
{
    public class CreditCardListItemViewModel : ViewModelBase
    {
        public CreditCardListItemViewModel(CreditCard card)
        {
        }
    }

    public class CreditCardsViewModel : ViewModelBase
    {
        public CreditCardsViewModel(ICreditCardService service)
        {
        }
    }
}
",
            appXamlCs: @"
private void ConfigureServices(IServiceCollection services)
{
    services.AddTransient<CreditCardsViewModel>();
}
",
            catalogEntry: new ViewModelEntry
            {
                Name = "CreditCardsViewModel",
                File = "AutoKassa/ViewModels/ViewModel.cs",
                RegisteredInDi = true,
                Dependencies = { "ICreditCardService" },
                NestedClasses =
                {
                    new NestedClassEntry { Name = "CreditCardListItemViewModel", Category = "item" },
                },
            });

        var validator = new ViewModelValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    private ValidationContext CreateContext(
        string codeViewModel,
        string appXamlCs,
        ViewModelEntry catalogEntry)
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        string viewModelsDir = Path.Combine(appPath, "ViewModels");
        string docsPath = Path.Combine(projectRoot, "docs", "kimi", "json");
        Directory.CreateDirectory(viewModelsDir);
        Directory.CreateDirectory(docsPath);

        File.WriteAllText(Path.Combine(viewModelsDir, "ViewModel.cs"), codeViewModel);
        File.WriteAllText(Path.Combine(appPath, "App.xaml.cs"), $@"
using Microsoft.Extensions.DependencyInjection;
namespace AutoKassa
{{
    public partial class App
    {{
{appXamlCs}
    }}
}}
");

        var viewModelsCatalog = new ViewModelsCatalog
        {
            ViewModels = { catalogEntry },
        };

        File.WriteAllText(Path.Combine(docsPath, "viewmodels.json"), JsonSerializer.Serialize(viewModelsCatalog));
        WriteMinimalCatalogs(docsPath);

        return CatalogLoader.Load(projectRoot, docsPath, appPath);
    }

    private static void WriteMinimalCatalogs(string docsPath)
    {
        File.WriteAllText(Path.Combine(docsPath, "services.json"), "{ \"services\": [] }");
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
