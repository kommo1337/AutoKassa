using System.Text.RegularExpressions;

namespace AutoKassa.DocValidator.CodeScanner;

/// <summary>
/// Результат сканирования одной сущности/DTO из <c>AutoKassa/Models/*.cs</c>.
/// </summary>
public sealed class ScannedModel
{
    public string Name { get; set; } = string.Empty;

    public string File { get; set; } = string.Empty;

    public string? BaseType { get; set; }

    public bool IsDto { get; set; }
}

/// <summary>
/// Сканирует <c>AutoKassa/Models/*.cs</c> и извлекает объявления классов.
/// </summary>
public static class ModelScanner
{
    private static readonly Regex ClassRegex = new(
        @"public\s+(?:abstract\s+|partial\s+)?class\s+(?<name>\w+)\s*(?::\s*(?<base>[\w<>,\s]+))?",
        RegexOptions.Compiled);

    public static IReadOnlyList<ScannedModel> Scan(ValidationContext context)
    {
        return Scan(context.ProjectRoot, context.AppPath);
    }

    public static IReadOnlyList<ScannedModel> Scan(string projectRoot, string appPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectRoot);
        ArgumentException.ThrowIfNullOrEmpty(appPath);

        var result = new List<ScannedModel>();
        string modelsDir = Path.Combine(appPath, "Models");

        if (!Directory.Exists(modelsDir))
        {
            return result;
        }

        foreach (string file in Directory.EnumerateFiles(modelsDir, "*.cs", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(file);
            string relativeFile = Path.GetRelativePath(projectRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/');

            foreach (Match match in ClassRegex.Matches(content).Cast<Match>())
            {
                string name = match.Groups["name"].Value;
                string? baseType = match.Groups["base"].Success
                    ? match.Groups["base"].Value.Trim()
                    : null;

                bool isDto = IsDtoByBaseType(baseType)
                    || IsDtoByFolder(file)
                    || IsDtoByName(name);

                result.Add(new ScannedModel
                {
                    Name = name,
                    File = relativeFile,
                    BaseType = baseType,
                    IsDto = isDto,
                });
            }
        }

        return result;
    }

    private static bool IsDtoByBaseType(string? baseType)
    {
        if (string.IsNullOrWhiteSpace(baseType))
        {
            return false;
        }

        // Берём только первый базовый тип (до запятой), игнорируя интерфейсы.
        string firstBase = baseType!.Split(',')[0].Trim();
        string normalized = System.Text.RegularExpressions.Regex.Replace(firstBase, @"\s+", string.Empty);

        return normalized.Equals("ReportBase", StringComparison.Ordinal)
            || normalized.EndsWith("ReportBase", StringComparison.Ordinal);
    }

    private static bool IsDtoByFolder(string file)
    {
        // Все классы в Models/Reports/, кроме ReportBase, являются DTO.
        string normalized = file.Replace('\\', '/');
        return normalized.Contains("/Models/Reports/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDtoByName(string name)
    {
        // DTO по соглашению имени: DailyTotalsItem, TransactionFilterParameters и т.п.
        // ReportBase — абстрактный базовый класс для DTO отчётов.
        return name.EndsWith("Item", StringComparison.Ordinal)
            || name.EndsWith("Parameters", StringComparison.Ordinal)
            || name.Equals("ReportBase", StringComparison.Ordinal);
    }
}
