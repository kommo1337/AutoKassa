using System.Text.RegularExpressions;

namespace AutoKassa.DocValidator.CodeScanner;

/// <summary>
/// Результат сканирования одной ViewModel из <c>AutoKassa/ViewModels/**/*.cs</c>.
/// </summary>
public sealed class ScannedViewModel
{
    public string Name { get; set; } = string.Empty;

    public string File { get; set; } = string.Empty;

    public string? BaseType { get; set; }

    public List<string> ConstructorParameters { get; set; } = new();
}

/// <summary>
/// Сканирует <c>AutoKassa/ViewModels/**/*.cs</c> и извлекает объявления top-level классов и их конструкторы.
/// Вложенные классы игнорируются (они описываются через <c>nestedClasses</c> в JSON).
/// </summary>
public static class ViewModelScanner
{
    private static readonly Regex ClassRegex = new(
        @"public\s+(?:abstract\s+|partial\s+)?class\s+(?<name>\w+)\s*(?::\s*(?<base>[\w<>,\s]+))?",
        RegexOptions.Compiled);

    private static readonly Regex ConstructorRegex = new(
        @"(?:public|protected|internal)\s+(?<name>\w+)\s*\((?<params>.*?)\)",
        RegexOptions.Singleline | RegexOptions.Compiled);

    public static IReadOnlyList<ScannedViewModel> Scan(ValidationContext context)
    {
        return Scan(context.ProjectRoot, context.AppPath);
    }

    public static IReadOnlyList<ScannedViewModel> Scan(string projectRoot, string appPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectRoot);
        ArgumentException.ThrowIfNullOrEmpty(appPath);

        var result = new List<ScannedViewModel>();
        string viewModelsDir = Path.Combine(appPath, "ViewModels");

        if (!Directory.Exists(viewModelsDir))
        {
            return result;
        }

        foreach (string file in Directory.EnumerateFiles(viewModelsDir, "*.cs", SearchOption.AllDirectories))
        {
            string content = File.ReadAllText(file);
            string relativeFile = Path.GetRelativePath(projectRoot, file)
                .Replace(Path.DirectorySeparatorChar, '/');

            foreach (Match match in ClassRegex.Matches(content).Cast<Match>())
            {
                if (IsNestedClass(content, match.Index))
                {
                    continue;
                }

                string name = match.Groups["name"].Value;
                string? baseType = match.Groups["base"].Success
                    ? match.Groups["base"].Value.Trim()
                    : null;

                List<string> parameters = ParseConstructorParameters(content, name);

                result.Add(new ScannedViewModel
                {
                    Name = name,
                    File = relativeFile,
                    BaseType = baseType,
                    ConstructorParameters = parameters,
                });
            }
        }

        return result;
    }

    /// <summary>
    /// Определяет, находится ли объявление класса внутри другого класса.
    /// </summary>
    private static bool IsNestedClass(string content, int matchIndex)
    {
        int depth = 0;

        for (int i = 0; i < matchIndex; i++)
        {
            char c = content[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
            }
        }

        // Глубина > 1 означает, что класс вложен как минимум в namespace + класс.
        return depth > 1;
    }

    private static List<string> ParseConstructorParameters(string content, string className)
    {
        var parameters = new List<string>();

        foreach (Match match in ConstructorRegex.Matches(content).Cast<Match>())
        {
            string name = match.Groups["name"].Value;
            if (!name.Equals(className, StringComparison.Ordinal))
            {
                continue;
            }

            string paramsText = match.Groups["params"].Value;
            parameters.AddRange(ParseParameterTypes(paramsText));
        }

        return parameters;
    }

    private static IEnumerable<string> ParseParameterTypes(string paramsText)
    {
        if (string.IsNullOrWhiteSpace(paramsText))
        {
            yield break;
        }

        // Разбиваем параметры по запятым на нулевом уровне generic-вложенности.
        foreach (string rawParam in SplitTopLevel(paramsText))
        {
            string param = rawParam.Trim();
            if (string.IsNullOrEmpty(param))
            {
                continue;
            }

            // Убираем атрибуты в начале.
            param = StripLeadingAttributes(param);

            // Убираем default-значение ("= ...").
            int equalsIndex = FindTopLevelEquals(param);
            if (equalsIndex >= 0)
            {
                param = param.Substring(0, equalsIndex).TrimEnd();
            }

            // Извлекаем тип: всё до последнего пробела (имя параметра).
            int lastSpace = param.LastIndexOf(' ');
            if (lastSpace <= 0)
            {
                continue;
            }

            string type = param.Substring(0, lastSpace).Trim();
            type = NormalizeTypeName(type);

            if (string.IsNullOrEmpty(type))
            {
                continue;
            }

            if (IsServiceDependency(type))
            {
                yield return type;
            }
        }
    }

    private static bool IsServiceDependency(string type)
    {
        // В JSON хранятся только сервисные интерфейсы и другие ViewModel/фабрики.
        if (type.EndsWith("ViewModel", StringComparison.Ordinal)
            || type.EndsWith("ViewModelFactory", StringComparison.Ordinal))
        {
            return true;
        }

        if (type.StartsWith("I", StringComparison.Ordinal)
            && type.Length > 1
            && char.IsUpper(type[1])
            && !type.Equals("ICommand", StringComparison.Ordinal))
        {
            return true;
        }

        return false;
    }

    private static int FindTopLevelEquals(string text)
    {
        int depth = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                case '<':
                case '[':
                case '(':
                case '{':
                    depth++;
                    break;
                case '>':
                case ']':
                case ')':
                case '}':
                    depth--;
                    break;
                case '=' when depth == 0:
                    return i;
            }
        }

        return -1;
    }

    private static string StripLeadingAttributes(string param)
    {
        int start = 0;
        while (start < param.Length)
        {
            int openBracket = param.IndexOf('[', start);
            if (openBracket < 0)
            {
                break;
            }

            int closeBracket = param.IndexOf(']', openBracket);
            if (closeBracket < 0)
            {
                break;
            }

            // Если сразу после ']' идёт идентификатор или '<' — это скорее всего тип массива/generic, а не атрибут.
            int nextNonSpace = closeBracket + 1;
            while (nextNonSpace < param.Length && char.IsWhiteSpace(param[nextNonSpace]))
            {
                nextNonSpace++;
            }

            if (nextNonSpace < param.Length && (char.IsLetter(param[nextNonSpace]) || param[nextNonSpace] == '<'))
            {
                break;
            }

            start = closeBracket + 1;
        }

        return param.Substring(start).TrimStart();
    }

    private static IEnumerable<string> SplitTopLevel(string text)
    {
        int depth = 0;
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                case '<':
                case '[':
                case '(':
                case '{':
                    depth++;
                    break;
                case '>':
                case ']':
                case ')':
                case '}':
                    depth--;
                    break;
                case ',' when depth == 0:
                    yield return text.Substring(start, i - start);
                    start = i + 1;
                    break;
            }
        }

        if (start < text.Length)
        {
            yield return text.Substring(start);
        }
    }

    private static string NormalizeTypeName(string type)
    {
        type = type.Trim();

        // Убираем ref/out/in модификаторы.
        if (type.StartsWith("ref ", StringComparison.OrdinalIgnoreCase)
            || type.StartsWith("out ", StringComparison.OrdinalIgnoreCase)
            || type.StartsWith("in ", StringComparison.OrdinalIgnoreCase))
        {
            type = type.Substring(4).Trim();
        }

        // Убираем nullable суффикс ?.
        if (type.EndsWith("?", StringComparison.Ordinal))
        {
            type = type.Substring(0, type.Length - 1);
        }

        // Убираем пробелы внутри generic-аргументов.
        if (type.Contains('<'))
        {
            type = Regex.Replace(type, @"\s+", string.Empty);
        }

        return type;
    }
}
