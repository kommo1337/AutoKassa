using AutoKassa.DocValidator.CodeScanner;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;

namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Сверяет View/XAML-файлы со справочником <c>relationships.json</c>.
/// </summary>
public sealed class ViewValidator : IValidator
{
    public string Name => "ViewValidator";

    public ValidationResult Validate(ValidationContext context)
    {
        var result = new ValidationResult();
        IReadOnlyList<ScannedView> scannedViews = ViewScanner.Scan(context);
        List<ViewToViewModel> catalogViews = context.Relationships.ViewsToViewModels;

        var catalogByView = catalogViews
            .Where(v => !string.IsNullOrEmpty(v.View))
            .ToDictionary(v => v.View!, StringComparer.OrdinalIgnoreCase);

        // 1. Каждый View из кода должен присутствовать в JSON.
        foreach (ScannedView scanned in scannedViews)
        {
            if (!catalogByView.ContainsKey(scanned.File))
            {
                result.AddError($"View '{scanned.File}' is missing in relationships.json.");
            }
        }

        // 2. Каждая запись JSON должна указывать на существующий View-файл и VM-файл (если указан).
        foreach (ViewToViewModel catalogEntry in catalogViews)
        {
            if (string.IsNullOrEmpty(catalogEntry.View))
            {
                result.AddError("relationships.json contains a viewsToViewModels entry without a view path.");
                continue;
            }

            if (!scannedViews.Any(v => PathsEqual(v.File, catalogEntry.View)))
            {
                result.AddError($"View '{catalogEntry.View}' from relationships.json was not found in AutoKassa/Views/ or MainWindow.xaml.");
            }

            ValidateViewModelFile(result, context, catalogEntry.View, catalogEntry.ViewModel);
        }

        return result;
    }

    private static void ValidateViewModelFile(ValidationResult result, ValidationContext context, string viewPath, string? viewModelPathOrName)
    {
        if (string.IsNullOrEmpty(viewModelPathOrName))
        {
            return;
        }

        // viewModel может быть либо путём (AutoKassa/ViewModels/Reports/X.cs), либо именем класса (X).
        string relativePath = viewModelPathOrName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        string absolutePath = Path.IsPathRooted(relativePath)
            ? relativePath
            : Path.Combine(context.ProjectRoot, relativePath);

        if (File.Exists(absolutePath))
        {
            return;
        }

        // Пробуем интерпретировать как имя класса и найти во вложенных папках ViewModels.
        string viewModelsDir = Path.Combine(context.AppPath, "ViewModels");
        if (Directory.Exists(viewModelsDir))
        {
            string fileName = $"{viewModelPathOrName}.cs";
            bool found = Directory.EnumerateFiles(viewModelsDir, fileName, SearchOption.AllDirectories).Any();
            if (found)
            {
                return;
            }
        }

        result.AddError($"View '{viewPath}': ViewModel '{viewModelPathOrName}' does not exist.");
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(first.Replace('\\', '/'), second.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
