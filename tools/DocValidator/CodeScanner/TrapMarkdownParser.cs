using System.Text.RegularExpressions;

namespace AutoKassa.DocValidator.CodeScanner;

/// <summary>
/// Результат парсинга одной ловушки из <c>TRAPS.md</c>.
/// </summary>
public sealed class ScannedTrap
{
    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
}

/// <summary>
/// Парсит <c>docs/kimi/TRAPS.md</c>, извлекая иерархию категорий и названий ловушек.
/// </summary>
public static class TrapMarkdownParser
{
    // Убирает числовой префикс вида "1.1", "2.14", "1.1." и т.п.
    private static readonly Regex TitlePrefixRegex = new(
        @"^\d+(\.\d+)*\s*[.\)\-]?\s*",
        RegexOptions.Compiled);

    public static IReadOnlyList<ScannedTrap> Scan(ValidationContext context)
    {
        string path = Path.Combine(context.ProjectRoot, "docs", "kimi", "TRAPS.md");
        return Scan(path);
    }

    public static IReadOnlyList<ScannedTrap> Scan(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("TRAPS.md not found", filePath);
        }

        var result = new List<ScannedTrap>();
        string[] lines = File.ReadAllLines(filePath);
        string currentCategory = string.Empty;

        foreach (string rawLine in lines)
        {
            string line = rawLine.TrimStart();

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                currentCategory = RemoveNumericPrefix(line.Substring(3)).Trim();
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                string title = line.Substring(4).Trim();
                title = RemoveNumericPrefix(title);

                result.Add(new ScannedTrap
                {
                    Category = currentCategory,
                    Title = title,
                });
            }
        }

        return result;
    }

    private static string RemoveNumericPrefix(string title)
    {
        return TitlePrefixRegex.Replace(title, string.Empty).Trim();
    }
}
