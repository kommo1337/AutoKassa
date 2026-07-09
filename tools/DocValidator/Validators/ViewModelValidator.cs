using AutoKassa.DocValidator.CodeScanner;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;

namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Сверяет ViewModel из <c>AutoKassa/ViewModels/**/*.cs</c> и их DI-регистрации
/// из <c>App.xaml.cs</c> со справочником <c>viewmodels.json</c>.
/// </summary>
public sealed class ViewModelValidator : IValidator
{
    public string Name => "ViewModelValidator";

    public ValidationResult Validate(ValidationContext context)
    {
        var result = new ValidationResult();
        IReadOnlyList<ScannedViewModel> scannedViewModels = ViewModelScanner.Scan(context);
        IReadOnlyList<ScannedDiRegistration> diRegistrations = DiRegistrationScanner.Scan(context);
        List<ViewModelEntry> catalogViewModels = context.ViewModels.ViewModels;

        var scannedByName = scannedViewModels.ToDictionary(vm => vm.Name, StringComparer.Ordinal);
        var catalogByName = catalogViewModels
            .Where(vm => !string.IsNullOrEmpty(vm.Name))
            .ToDictionary(vm => vm.Name!, StringComparer.Ordinal);

        HashSet<string> nestedClassNames = catalogViewModels
            .SelectMany(vm => vm.NestedClasses)
            .Where(n => !string.IsNullOrEmpty(n.Name))
            .Select(n => n.Name!)
            .ToHashSet(StringComparer.Ordinal);

        HashSet<string> registeredVmNames = GetRegisteredViewModelNames(diRegistrations, catalogByName);

        // 1. Каждый класс из ViewModels/**/*.cs должен присутствовать в viewmodels.json.
        foreach (ScannedViewModel scanned in scannedViewModels)
        {
            // Игнорируем базовые и вспомогательные файлы, которые не являются VM.
            if (IsIgnoredFile(scanned.File))
            {
                continue;
            }

            // Вложенные классы описываются через nestedClasses родительской VM.
            if (nestedClassNames.Contains(scanned.Name))
            {
                continue;
            }

            if (!catalogByName.TryGetValue(scanned.Name, out ViewModelEntry? catalogEntry))
            {
                result.AddError($"ViewModel '{scanned.Name}' from {scanned.File} is missing in viewmodels.json.");
                continue;
            }

            ValidateEntry(result, context, scanned, catalogEntry, registeredVmNames);
        }

        // 2. Каждая запись viewmodels.json должна иметь соответствующий файл в коде.
        foreach (ViewModelEntry catalogEntry in catalogViewModels)
        {
            if (string.IsNullOrEmpty(catalogEntry.Name))
            {
                result.AddError("viewmodels.json contains a ViewModel entry without a name.");
                continue;
            }

            if (IsInterfaceEntry(catalogEntry))
            {
                // Для интерфейсов проверяем только существование файла.
                ValidateFileExists(result, context, catalogEntry.Name, catalogEntry.File);
                continue;
            }

            if (scannedByName.ContainsKey(catalogEntry.Name))
            {
                continue;
            }

            // Вложенные классы не являются отдельными файлами.
            if (IsNestedClassName(catalogEntry.Name, catalogViewModels))
            {
                continue;
            }

            result.AddError($"ViewModel '{catalogEntry.Name}' from viewmodels.json was not found in AutoKassa/ViewModels/**/*.cs.");
        }

        return result;
    }

    private static void ValidateEntry(
        ValidationResult result,
        ValidationContext context,
        ScannedViewModel scanned,
        ViewModelEntry catalog,
        HashSet<string> registeredVmNames)
    {
        // Сверка пути к файлу.
        if (!string.IsNullOrEmpty(catalog.File) &&
            !PathsEqual(scanned.File, catalog.File))
        {
            result.AddError($"ViewModel '{scanned.Name}': file mismatch. Code: '{scanned.File}', JSON: '{catalog.File}'.");
        }

        bool registeredInDi = catalog.RegisteredInDi ?? false;
        bool isRegistered = registeredVmNames.Contains(scanned.Name);

        if (registeredInDi && !isRegistered)
        {
            result.AddError($"ViewModel '{scanned.Name}' is marked as registeredInDi: true, but no DI registration found in App.xaml.cs.");
        }

        if (!registeredInDi && isRegistered)
        {
            // MainWindowViewModel зарегистрирован как зависимость MainWindow — это допустимо.
            if (!scanned.Name.Equals("MainWindowViewModel", StringComparison.Ordinal))
            {
                result.AddError($"ViewModel '{scanned.Name}' is not marked as registeredInDi, but found DI registration in App.xaml.cs.");
            }
        }

        // Сверка зависимостей конструктора.
        var scannedDependencies = scanned.ConstructorParameters
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var catalogDependencies = catalog.Dependencies
            .Select(NormalizeDependencyName)
            .Where(d => !string.IsNullOrEmpty(d))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string dependency in catalogDependencies)
        {
            if (!scannedDependencies.Contains(dependency))
            {
                result.AddError($"ViewModel '{scanned.Name}': dependency '{dependency}' from viewmodels.json is missing in constructor.");
            }
        }

        foreach (string dependency in scannedDependencies)
        {
            if (!catalogDependencies.Contains(dependency))
            {
                result.AddError($"ViewModel '{scanned.Name}': constructor dependency '{dependency}' is missing in viewmodels.json.");
            }
        }

        // Проверка существования файла.
        ValidateFileExists(result, context, scanned.Name, scanned.File);
    }

    private static HashSet<string> GetRegisteredViewModelNames(
        IReadOnlyList<ScannedDiRegistration> registrations,
        Dictionary<string, ViewModelEntry> catalogByName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ScannedDiRegistration registration in registrations)
        {
            string impl = registration.NormalizedImplementation;
            string service = registration.NormalizedServiceType;

            // Саморегистрация VM/Factory.
            if (string.Equals(impl, service, StringComparison.OrdinalIgnoreCase)
                && (catalogByName.ContainsKey(impl)
                    || impl.EndsWith("ViewModel", StringComparison.OrdinalIgnoreCase)
                    || impl.EndsWith("ViewModelFactory", StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(impl);
                continue;
            }

            // Интерфейсная регистрация фабрики VM (IReportViewModelFactory -> ReportViewModelFactory).
            if (impl.EndsWith("ViewModelFactory", StringComparison.OrdinalIgnoreCase)
                && service.EndsWith("ViewModelFactory", StringComparison.OrdinalIgnoreCase))
            {
                result.Add(impl);
            }
        }

        return result;
    }

    private static bool IsInterfaceEntry(ViewModelEntry entry)
    {
        return string.Equals(entry.Category, "interface", StringComparison.OrdinalIgnoreCase)
            || entry.Name?.StartsWith("I", StringComparison.Ordinal) == true;
    }

    private static bool IsNestedClassName(string name, List<ViewModelEntry> catalogViewModels)
    {
        return catalogViewModels
            .SelectMany(vm => vm.NestedClasses)
            .Any(n => n.Name == name);
    }

    private static bool IsIgnoredFile(string file)
    {
        string normalized = file.Replace('\\', '/');
        return normalized.EndsWith("ViewModelBase.cs", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("IAsyncInitializable.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateFileExists(ValidationResult result, ValidationContext context, string name, string? file)
    {
        if (string.IsNullOrEmpty(file))
        {
            return;
        }

        string absolutePath = Path.Combine(context.ProjectRoot, file.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(absolutePath))
        {
            result.AddError($"ViewModel '{name}': file '{file}' does not exist.");
        }
    }

    private static string? NormalizeDependencyName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string trimmed = name!.Trim();

        // Убираем nullable суффикс.
        if (trimmed.EndsWith("?", StringComparison.Ordinal))
        {
            trimmed = trimmed.Substring(0, trimmed.Length - 1);
        }

        // Убираем пробелы внутри generic-аргументов.
        if (trimmed.Contains('<'))
        {
            trimmed = System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", string.Empty);
        }

        return trimmed;
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(first.Replace('\\', '/'), second.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
