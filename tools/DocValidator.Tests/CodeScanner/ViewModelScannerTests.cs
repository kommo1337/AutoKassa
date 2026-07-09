using AutoKassa.DocValidator.CodeScanner;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.CodeScanner;

public class ViewModelScannerTests : IDisposable
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
    public void Scan_WithSimpleViewModel_ParsesConstructorDependencies()
    {
        (string projectRoot, string appPath) = CreateProjectWithViewModel("DashboardViewModel.cs", @"
namespace AutoKassa.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        public DashboardViewModel(ITransactionService transactionService, ICategoryService categoryService)
        {
        }
    }
}
");

        IReadOnlyList<ScannedViewModel> result = ViewModelScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        ScannedViewModel vm = result.Single();
        vm.Name.Should().Be("DashboardViewModel");
        vm.ConstructorParameters.Should().BeEquivalentTo(new[] { "ITransactionService", "ICategoryService" });
    }

    [Fact]
    public void Scan_WithDefaultParameterValues_IgnoresDefaults()
    {
        (string projectRoot, string appPath) = CreateProjectWithViewModel("ConfirmationDialogViewModel.cs", @"
namespace AutoKassa.ViewModels
{
    public class ConfirmationDialogViewModel : ViewModelBase
    {
        public ConfirmationDialogViewModel(
            string title,
            string message,
            string confirmText = ""Да"",
            string cancelText = ""Нет"",
            bool isDestructive = false)
        {
        }
    }
}
");

        IReadOnlyList<ScannedViewModel> result = ViewModelScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        result.Single().ConstructorParameters.Should().BeEmpty();
    }

    [Fact]
    public void Scan_WithModelParameters_FiltersOutModels()
    {
        (string projectRoot, string appPath) = CreateProjectWithViewModel("CategoryItemViewModel.cs", @"
namespace AutoKassa.ViewModels
{
    public class CategoryItemViewModel : ViewModelBase
    {
        public CategoryItemViewModel(Category model, ICategoryService categoryService, Action<CategoryItemViewModel> onDelete)
        {
        }
    }
}
");

        IReadOnlyList<ScannedViewModel> result = ViewModelScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        result.Single().ConstructorParameters.Should().BeEquivalentTo(new[] { "ICategoryService" });
    }

    [Fact]
    public void Scan_WithNestedClass_IgnoresNestedClass()
    {
        (string projectRoot, string appPath) = CreateProjectWithViewModel("CreditCardsViewModel.cs", @"
namespace AutoKassa.ViewModels
{
    public class CreditCardsViewModel : ViewModelBase
    {
        public CreditCardsViewModel(ICreditCardService service)
        {
        }

        public class CreditCardListItemViewModel : ViewModelBase
        {
            public CreditCardListItemViewModel(CreditCard card)
            {
            }
        }
    }
}
");

        IReadOnlyList<ScannedViewModel> result = ViewModelScanner.Scan(projectRoot, appPath);

        result.Should().HaveCount(1);
        result.Single().Name.Should().Be("CreditCardsViewModel");
    }

    [Fact]
    public void Scan_WithProtectedConstructor_ParsesDependencies()
    {
        (string projectRoot, string appPath) = CreateProjectWithViewModel(
            Path.Combine("Reports", "BaseReportViewModel.cs"),
            @"
namespace AutoKassa.ViewModels.Reports
{
    public abstract class BaseReportViewModel : ViewModelBase
    {
        protected BaseReportViewModel(IDialogService dialogService, IToastNotificationService toastService)
        {
        }
    }
}
");

        IReadOnlyList<ScannedViewModel> result = ViewModelScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        result.Single().ConstructorParameters.Should().BeEquivalentTo(new[] { "IDialogService", "IToastNotificationService" });
    }

    [Fact]
    public void Scan_WithNoViewModelsDirectory_ReturnsEmptyList()
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        Directory.CreateDirectory(appPath);

        IReadOnlyList<ScannedViewModel> result = ViewModelScanner.Scan(projectRoot, appPath);

        result.Should().BeEmpty();
    }

    private (string ProjectRoot, string AppPath) CreateProjectWithViewModel(string relativePath, string content)
    {
        string projectRoot = CreateTempDirectory();
        string viewModelsDir = Path.Combine(projectRoot, "AutoKassa", "ViewModels", Path.GetDirectoryName(relativePath)!);
        Directory.CreateDirectory(viewModelsDir);
        File.WriteAllText(Path.Combine(viewModelsDir, Path.GetFileName(relativePath)), content);
        return (projectRoot, Path.Combine(projectRoot, "AutoKassa"));
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }
}
