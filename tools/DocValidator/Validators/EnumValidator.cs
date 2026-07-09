using AutoKassa.DocValidator.CodeScanner;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;

namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Сверяет enum из <c>AutoKassa/Models/Enums/*.cs</c> со справочником <c>enums.json</c>.
/// </summary>
public sealed class EnumValidator : IValidator
{
    public string Name => "EnumValidator";

    public ValidationResult Validate(ValidationContext context)
    {
        var result = new ValidationResult();
        IReadOnlyList<ScannedEnum> scannedEnums = EnumScanner.Scan(context);
        List<EnumEntry> catalogEnums = context.Enums.Enums;

        var scannedByName = scannedEnums.ToDictionary(e => e.Name, StringComparer.Ordinal);
        var catalogByName = catalogEnums
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .ToDictionary(e => e.Name!, StringComparer.Ordinal);

        // 1. Каждый enum из кода должен присутствовать в JSON.
        foreach (ScannedEnum scanned in scannedEnums)
        {
            if (!catalogByName.TryGetValue(scanned.Name, out EnumEntry? catalogEntry))
            {
                result.AddError($"Enum '{scanned.Name}' from {scanned.File} is missing in enums.json.");
                continue;
            }

            ValidateEntry(result, context, scanned, catalogEntry);
        }

        // 2. Каждый enum из JSON должен иметь соответствующий файл в коде.
        foreach (EnumEntry catalogEntry in catalogEnums)
        {
            if (string.IsNullOrEmpty(catalogEntry.Name))
            {
                result.AddError("enums.json contains an enum entry without a name.");
                continue;
            }

            if (!scannedByName.ContainsKey(catalogEntry.Name))
            {
                result.AddError($"Enum '{catalogEntry.Name}' from enums.json was not found in AutoKassa/Models/Enums/*.cs.");
            }
        }

        return result;
    }

    private static void ValidateEntry(ValidationResult result, ValidationContext context, ScannedEnum scanned, EnumEntry catalog)
    {
        // Сверка пути к файлу.
        if (!string.IsNullOrEmpty(catalog.File) &&
            !PathsEqual(scanned.File, catalog.File))
        {
            result.AddError($"Enum '{scanned.Name}': file mismatch. Code: '{scanned.File}', JSON: '{catalog.File}'.");
        }

        // Сверка underlying type.
        string catalogUnderlying = string.IsNullOrEmpty(catalog.UnderlyingType) ? "int" : catalog.UnderlyingType;
        if (!string.Equals(scanned.UnderlyingType, catalogUnderlying, StringComparison.OrdinalIgnoreCase))
        {
            result.AddError($"Enum '{scanned.Name}': underlying type mismatch. Code: '{scanned.UnderlyingType}', JSON: '{catalogUnderlying}'.");
        }

        // Сверка членов.
        var scannedMembers = scanned.Members.ToDictionary(m => m.Name, StringComparer.Ordinal);
        var catalogMembers = catalog.Values
            .Where(v => !string.IsNullOrEmpty(v.Name))
            .ToDictionary(v => v.Name!, StringComparer.Ordinal);

        foreach (ScannedEnumMember scannedMember in scanned.Members)
        {
            if (!catalogMembers.TryGetValue(scannedMember.Name, out EnumValue? catalogMember))
            {
                result.AddError($"Enum '{scanned.Name}': member '{scannedMember.Name}' is missing in enums.json.");
                continue;
            }

            if (catalogMember.Value.HasValue && catalogMember.Value.Value != scannedMember.Value)
            {
                result.AddError($"Enum '{scanned.Name}.{scannedMember.Name}': value mismatch. Code: {scannedMember.Value}, JSON: {catalogMember.Value.Value}.");
            }
        }

        foreach (EnumValue catalogMember in catalog.Values)
        {
            if (string.IsNullOrEmpty(catalogMember.Name))
            {
                result.AddError($"Enum '{scanned.Name}': enums.json contains a member without a name.");
                continue;
            }

            if (!scannedMembers.ContainsKey(catalogMember.Name))
            {
                result.AddError($"Enum '{scanned.Name}': member '{catalogMember.Name}' from enums.json is missing in code.");
            }
        }

        // Проверка usedIn.
        foreach (string usedIn in catalog.UsedIn)
        {
            if (string.IsNullOrEmpty(usedIn))
            {
                continue;
            }

            string resolvedPath = ResolvePath(context, usedIn);
            if (!File.Exists(resolvedPath))
            {
                result.AddError($"Enum '{scanned.Name}': usedIn file '{usedIn}' does not exist.");
            }
        }

        // Проверка uiMappings.
        if (catalog.UiMappings is not null)
        {
            CheckUiMappingFile(result, context, scanned.Name, "textConverter", catalog.UiMappings.TextConverter);
            CheckUiMappingFile(result, context, scanned.Name, "colorConverter", catalog.UiMappings.ColorConverter);
            CheckUiMappingFile(result, context, scanned.Name, "iconConverter", catalog.UiMappings.IconConverter);
        }
    }

    private static void CheckUiMappingFile(ValidationResult result, ValidationContext context, string enumName, string mappingName, string? mappingPath)
    {
        if (string.IsNullOrEmpty(mappingPath))
        {
            return;
        }

        string resolvedPath = ResolvePath(context, mappingPath);
        if (!File.Exists(resolvedPath))
        {
            result.AddError($"Enum '{enumName}': uiMappings.{mappingName} file '{mappingPath}' does not exist.");
        }
    }

    private static string ResolvePath(ValidationContext context, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        // Поддержка путей относительно docs/kimi/json/ (например, ../../AutoKassa/...).
        string fromDocs = Path.GetFullPath(Path.Combine(context.DocsPath, path));
        if (File.Exists(fromDocs))
        {
            return fromDocs;
        }

        // Поддержка путей относительно корня репозитория.
        return Path.GetFullPath(Path.Combine(context.ProjectRoot, path));
    }

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(first.Replace('\\', '/'), second.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
