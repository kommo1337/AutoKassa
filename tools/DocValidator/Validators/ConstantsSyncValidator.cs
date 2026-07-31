using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;

namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Сверяет справочник <c>json/constants.json</c> с исходным кодом.
/// </summary>
/// <remarks>
/// Проверки (ошибки): у каждой константы заполнены обязательные поля
/// (<c>name</c>, <c>value</c>, <c>type</c>, <c>sourceFiles</c>, <c>description</c>)
/// и каждый файл из <c>sourceFiles</c> существует (относительно <c>sourceRoot</c>
/// из JSON, а тот — относительно корня репозитория).
///
/// Условные имена. Многие константы имеют документационные имена, которые дословно
/// не встречаются в коде: <c>WindowWidth</c> — это <c>Width="1366"</c> в XAML,
/// <c>AutoLockMinutesRange</c> — проверка вида <c>&lt; 1 || &gt; 60</c>. Поэтому
/// отсутствие имени в файле — не ошибка: валидатор дополнительно ищет значение
/// константы (для ISO-дат — форму <c>yyyy, m, d</c> из <c>new DateTime(...)</c>,
/// для путей — последний сегмент, для диапазонов <c>a–b</c> — любую границу;
/// поиск значения намеренно слабый: его цель — не порождать ложные предупреждения,
/// а не доказать корректность значения). Если не найдено ни имя, ни значение,
/// запись помечается предупреждением (warning), а не ошибкой.
///
/// Записи с <c>docOnly: true</c> — чисто документационные понятия (например,
/// «единицы измерения таймера»): поиск в коде для них не выполняется.
/// </remarks>
public sealed class ConstantsSyncValidator : IValidator
{
    private static readonly Regex RangePattern = new(
        @"^\s*(\d+(?:[.,]\d+)?)\s*[–—-]\s*(\d+(?:[.,]\d+)?)\s*$",
        RegexOptions.Compiled);

    public string Name => "ConstantsSyncValidator";

    public ValidationResult Validate(ValidationContext context)
    {
        var result = new ValidationResult();
        ConstantsCatalog catalog = context.Constants;
        var fileContents = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (ConstantCategory category in catalog.Categories)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                result.AddError("constants.json contains a category without name.");
            }

            string categoryName = category.Name ?? "?";

            foreach (ConstantEntry entry in category.Constants)
            {
                ValidateRequiredFields(result, categoryName, entry);

                // docOnly-константы — чисто документационные понятия: искать их в коде не нужно.
                if (entry.DocOnly)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.Name) || entry.SourceFiles.Count == 0)
                {
                    continue;
                }

                bool nameFound = false;
                bool valueFound = false;

                foreach (string sourceFile in entry.SourceFiles)
                {
                    if (string.IsNullOrWhiteSpace(sourceFile))
                    {
                        result.AddError($"Constant '{entry.Name}' (category '{categoryName}'): empty entry in sourceFiles.");
                        continue;
                    }

                    string resolvedPath = ResolvePath(context, catalog.SourceRoot, sourceFile);
                    if (!File.Exists(resolvedPath))
                    {
                        result.AddError($"Constant '{entry.Name}' (category '{categoryName}'): file '{sourceFile}' does not exist (resolved to '{resolvedPath}').");
                        continue;
                    }

                    string content = GetFileContent(fileContents, resolvedPath);
                    if (content.Contains(entry.Name, StringComparison.Ordinal))
                    {
                        nameFound = true;
                    }

                    if (!valueFound && MatchesValue(content, entry.Value))
                    {
                        valueFound = true;
                    }
                }

                if (!nameFound && !valueFound)
                {
                    result.AddWarning($"Constant '{entry.Name}' (category '{categoryName}') was not found in its sourceFiles; it likely uses a documentation-only name.");
                }
            }
        }

        return result;
    }

    private static void ValidateRequiredFields(ValidationResult result, string categoryName, ConstantEntry entry)
    {
        string displayName = string.IsNullOrWhiteSpace(entry.Name) ? "?" : entry.Name;
        string prefix = $"Constant '{displayName}' (category '{categoryName}')";

        if (string.IsNullOrWhiteSpace(entry.Name))
        {
            result.AddError($"{prefix}: missing or empty required field 'name'.");
        }

        if (entry.Value is null)
        {
            result.AddError($"{prefix}: missing required field 'value'.");
        }

        if (string.IsNullOrWhiteSpace(entry.Type))
        {
            result.AddError($"{prefix}: missing or empty required field 'type'.");
        }

        if (entry.SourceFiles.Count == 0)
        {
            result.AddError($"{prefix}: missing or empty required field 'sourceFiles'.");
        }

        if (string.IsNullOrWhiteSpace(entry.Description))
        {
            result.AddError($"{prefix}: missing or empty required field 'description'.");
        }
    }

    private static string ResolvePath(ValidationContext context, string? sourceRoot, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        string baseDir = string.IsNullOrWhiteSpace(sourceRoot)
            ? context.ProjectRoot
            : Path.Combine(context.ProjectRoot, sourceRoot);

        return Path.GetFullPath(Path.Combine(baseDir, path));
    }

    private static string GetFileContent(Dictionary<string, string> cache, string path)
    {
        if (!cache.TryGetValue(path, out string? content))
        {
            content = File.ReadAllText(path);
            cache[path] = content;
        }

        return content;
    }

    private static bool MatchesValue(string content, JsonElement? value)
    {
        if (value is null)
        {
            return false;
        }

        foreach (string variant in GetValueVariants(value.Value))
        {
            if (content.Contains(variant, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetValueVariants(JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.True:
                yield return "true";
                yield return "True";
                break;

            case JsonValueKind.False:
                yield return "false";
                yield return "False";
                break;

            case JsonValueKind.Number:
                yield return value.GetRawText();
                // Число "14.0" в JSON в коде записано как "14".
                if (value.TryGetInt64(out long integer))
                {
                    yield return integer.ToString(CultureInfo.InvariantCulture);
                }

                break;

            case JsonValueKind.String:
                string? text = value.GetString();
                // Пустая строка встречается в любом файле — проверка бессмысленна.
                if (string.IsNullOrEmpty(text))
                {
                    break;
                }

                yield return text;

                // ISO-дата "2024-01-01" в коде выглядит как new DateTime(2024, 1, 1).
                if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date))
                {
                    yield return string.Create(CultureInfo.InvariantCulture, $"{date.Year}, {date.Month}, {date.Day}");
                }
                // Диапазон "1–60" в коде размазан по сравнениям и сообщениям — ищем границы.
                else
                {
                    Match range = RangePattern.Match(text);
                    if (range.Success)
                    {
                        yield return range.Groups[1].Value;
                        yield return range.Groups[2].Value;
                    }
                }

                // Путь "logs/autokassa-.log" в коде собирается через Path.Combine — ищем последний сегмент.
                int separator = text.LastIndexOfAny(new[] { '/', '\\' });
                if (separator >= 0 && separator < text.Length - 1)
                {
                    yield return text[(separator + 1)..];
                }

                break;

            case JsonValueKind.Array:
                foreach (JsonElement item in value.EnumerateArray())
                {
                    foreach (string variant in GetValueVariants(item))
                    {
                        yield return variant;
                    }
                }

                break;
        }
    }
}
