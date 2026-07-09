using System.Text.RegularExpressions;

namespace AutoKassa.DocValidator.CodeScanner;

/// <summary>
/// Результат сканирования одной DI-регистрации из <c>App.xaml.cs</c>.
/// </summary>
public sealed class ScannedDiRegistration
{
    public string ServiceType { get; set; } = string.Empty;

    public string ImplementationType { get; set; } = string.Empty;

    public string Lifetime { get; set; } = string.Empty;

    public string NormalizedServiceType => NormalizeTypeName(ServiceType);

    public string NormalizedImplementation => NormalizeTypeName(ImplementationType);

    private static string NormalizeTypeName(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return string.Empty;
        }

        string trimmed = type.Trim();
        int lastDot = trimmed.LastIndexOf('.');
        string name = lastDot >= 0 ? trimmed.Substring(lastDot + 1) : trimmed;

        // Убираем возможные пробелы внутри generic-аргументов, например "IDbContextFactory< AppDbContext >".
        if (name.Contains('<'))
        {
            name = Regex.Replace(name, @"\s+", string.Empty);
        }

        return name;
    }
}

/// <summary>
/// Сканирует <c>AutoKassa/App.xaml.cs</c> и извлекает DI-регистрации.
/// </summary>
public static class DiRegistrationScanner
{
    // services.AddLifetime<IInterface, Implementation>()
    private static readonly Regex InterfaceAndImplementationRegex = new(
        @"services\.Add(?<lifetime>Singleton|Transient|Scoped)\s*<\s*(?<interface>[\w<>,\.\s]+?)\s*,\s*(?<impl>[\w<>,\.\s]+?)\s*>\s*\(",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // services.AddLifetime<Type>()  (включая factory-регистрации вида AddSingleton<MainWindow>(provider => ...))
    private static readonly Regex SingleTypeRegex = new(
        @"services\.Add(?<lifetime>Singleton|Transient|Scoped)\s*<\s*(?<type>[\w<>,\.\s]+?)\s*>\s*\(",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // services.AddDbContext<AppDbContext>(ServiceLifetime.Transient)
    private static readonly Regex AddDbContextRegex = new(
        @"services\.AddDbContext\s*<\s*(?<type>[\w<>,\.\s]+?)\s*>\s*\(\s*(?:ServiceLifetime\s*\.\s*(?<lifetime>Singleton|Transient|Scoped))?\s*\)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // services.AddDbContextFactory<AppDbContext>() — по умолчанию Singleton
    private static readonly Regex AddDbContextFactoryRegex = new(
        @"services\.AddDbContextFactory\s*<\s*(?<type>[\w<>,\.\s]+?)\s*>\s*\(",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public static IReadOnlyList<ScannedDiRegistration> Scan(ValidationContext context)
    {
        return Scan(context.AppPath);
    }

    public static IReadOnlyList<ScannedDiRegistration> Scan(string appPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(appPath);

        var result = new List<ScannedDiRegistration>();
        string appXamlCs = Path.Combine(appPath, "App.xaml.cs");

        if (!File.Exists(appXamlCs))
        {
            return result;
        }

        string content = File.ReadAllText(appXamlCs);
        content = StripComments(content);

        // Регистрации с интерфейсом и реализацией.
        foreach (Match match in InterfaceAndImplementationRegex.Matches(content).Cast<Match>())
        {
            string interfaceType = match.Groups["interface"].Value.Trim();
            string implementationType = match.Groups["impl"].Value.Trim();
            string lifetime = match.Groups["lifetime"].Value;

            if (HasTopLevelComma(interfaceType) || HasTopLevelComma(implementationType))
            {
                continue;
            }

            result.Add(new ScannedDiRegistration
            {
                ServiceType = interfaceType,
                ImplementationType = implementationType,
                Lifetime = lifetime,
            });
        }

        // Саморегистрации (AddSingleton<T>(), AddTransient<T>() и т.д.).
        foreach (Match match in SingleTypeRegex.Matches(content).Cast<Match>())
        {
            string type = match.Groups["type"].Value.Trim();
            string lifetime = match.Groups["lifetime"].Value;

            if (HasTopLevelComma(type))
            {
                continue;
            }

            result.Add(new ScannedDiRegistration
            {
                ServiceType = type,
                ImplementationType = type,
                Lifetime = lifetime,
            });
        }

        // AddDbContext<AppDbContext>(...)
        foreach (Match match in AddDbContextRegex.Matches(content).Cast<Match>())
        {
            string type = match.Groups["type"].Value.Trim();
            string lifetime = match.Groups["lifetime"].Success
                ? match.Groups["lifetime"].Value
                : "Scoped";

            result.Add(new ScannedDiRegistration
            {
                ServiceType = type,
                ImplementationType = type,
                Lifetime = lifetime,
            });
        }

        // AddDbContextFactory<AppDbContext>() — регистрирует IDbContextFactory<T> как Singleton.
        foreach (Match match in AddDbContextFactoryRegex.Matches(content).Cast<Match>())
        {
            string type = match.Groups["type"].Value.Trim();
            string factoryServiceType = $"IDbContextFactory<{type}>";

            result.Add(new ScannedDiRegistration
            {
                ServiceType = factoryServiceType,
                ImplementationType = factoryServiceType,
                Lifetime = "Singleton",
            });
        }

        return result;
    }

    private static string StripComments(string content)
    {
        // Удаляем блочные комментарии /* ... */.
        content = Regex.Replace(content, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        // Удаляем однострочные комментарии //.
        string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("//", StringComparison.Ordinal))
            {
                lines[i] = string.Empty;
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Проверяет, есть ли запятая на нулевом уровне вложенности generic-аргументов.
    /// Нужно, чтобы не перепутать AddSingleton&lt;IInterface, Implementation&gt;() с саморегистрацией.
    /// </summary>
    private static bool HasTopLevelComma(string text)
    {
        int depth = 0;

        foreach (char c in text)
        {
            switch (c)
            {
                case '<':
                    depth++;
                    break;
                case '>':
                    depth--;
                    break;
                case ',' when depth == 0:
                    return true;
            }
        }

        return false;
    }
}
