using System.Text.RegularExpressions;

namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Запрещает прямую инъекцию <c>AppDbContext</c> в конструкторы классов приложения.
/// </summary>
/// <remarks>
/// Сервисы обязаны получать <c>IDbContextFactory&lt;AppDbContext&gt;</c> и создавать
/// короткоживущий контекст на каждую операцию (см. docs/kimi/02-architecture.md).
/// Явной регистрации <c>AddDbContext&lt;AppDbContext&gt;</c> в DI нет, но
/// <c>AddDbContextFactory</c> неявно делает контекст разрешимым (Scoped) — поэтому
/// запрет закреплён этой проверкой, а не только соглашением.
///
/// Методы-помощники, принимающие <c>AppDbContext</c> параметром (например,
/// <c>CreditCardOperationHelper</c>), не нарушают правило: контекст туда передаётся
/// из метода, уже создавшего его через фабрику. Проверяются только конструкторы.
/// Первичные конструкторы (C# 12) не распознаются — в проекте не используются.
/// </remarks>
public sealed class DbContextInjectionValidator : IValidator
{
    private static readonly Regex ClassNameRegex = new(
        @"\bclass\s+(?<name>\w+)",
        RegexOptions.Compiled);

    // Параметр вида "AppDbContext context", но не IDbContextFactory<AppDbContext>
    // и не DbContextOptions<AppDbContext> — там типу предшествует '<'.
    private static readonly Regex DbContextParameterRegex = new(
        @"(?<![\w<])AppDbContext(?![\w>])\s+\w+",
        RegexOptions.Compiled);

    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "bin", "obj",
    };

    public string Name => "DbContextInjectionValidator";

    public ValidationResult Validate(ValidationContext context)
    {
        var result = new ValidationResult();

        foreach (string file in EnumerateSourceFiles(context.AppPath))
        {
            string content = File.ReadAllText(file);

            foreach (Match classMatch in ClassNameRegex.Matches(content).Cast<Match>())
            {
                string className = classMatch.Groups["name"].Value;

                // Конструктор: модификатор доступа + имя класса + параметры
                // (в отличие от метода — без возвращаемого типа).
                var ctorRegex = new Regex(
                    $@"(?:public|internal|protected|private)\s+(?:static\s+)?{Regex.Escape(className)}\s*\((?<params>[^)]*)\)",
                    RegexOptions.Compiled);

                foreach (Match ctorMatch in ctorRegex.Matches(content).Cast<Match>())
                {
                    string parameters = ctorMatch.Groups["params"].Value;

                    if (DbContextParameterRegex.IsMatch(parameters))
                    {
                        string relativePath = Path.GetRelativePath(context.ProjectRoot, file);
                        result.AddError(
                            $"{relativePath}: constructor of '{className}' injects AppDbContext directly; " +
                            "use IDbContextFactory<AppDbContext> instead (see docs/kimi/02-architecture.md).");
                    }
                }
            }
        }

        return result;
    }

    private static IEnumerable<string> EnumerateSourceFiles(string appPath)
    {
        return Directory
            .EnumerateFiles(appPath, "*.cs", SearchOption.AllDirectories)
            .Where(file => !file
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => ExcludedDirectories.Contains(segment)));
    }
}
