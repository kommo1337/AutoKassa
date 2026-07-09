using AutoKassa.DocValidator.CodeScanner;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;

namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Сверяет ловушки в <c>docs/kimi/TRAPS.md</c> со справочником <c>json/traps.json</c>.
/// </summary>
public sealed class TrapSyncValidator : IValidator
{
    public string Name => "TrapSyncValidator";

    public ValidationResult Validate(ValidationContext context)
    {
        var result = new ValidationResult();
        IReadOnlyList<ScannedTrap> markdownTraps = TrapMarkdownParser.Scan(context);
        List<TrapEntry> jsonTraps = context.Traps.Traps;

        var markdownSet = markdownTraps
            .Select(t => (Normalize(t.Category), Normalize(t.Title)))
            .ToHashSet();
        var jsonSet = jsonTraps
            .Select(t => (Normalize(t.Category), Normalize(t.Title)))
            .ToHashSet();

        // Дополнительная сверка счётчиков — полезна при быстром анализе отчёта.
        if (markdownTraps.Count != jsonTraps.Count)
        {
            result.AddError($"Trap count mismatch: TRAPS.md has {markdownTraps.Count} traps, traps.json has {jsonTraps.Count} traps.");
        }

        // Каждая ловушка из Markdown должна быть в JSON.
        foreach (ScannedTrap trap in markdownTraps)
        {
            if (!jsonSet.Contains((Normalize(trap.Category), Normalize(trap.Title))))
            {
                result.AddError($"Trap '{trap.Title}' (category '{trap.Category}') from TRAPS.md is missing in traps.json.");
            }
        }

        // Каждая ловушка из JSON должна быть в Markdown, а её файлы — существовать.
        foreach (TrapEntry trap in jsonTraps)
        {
            string category = trap.Category ?? string.Empty;
            string title = trap.Title ?? string.Empty;

            if (string.IsNullOrEmpty(category) || string.IsNullOrEmpty(title))
            {
                result.AddError("traps.json contains a trap entry without category or title.");
                continue;
            }

            if (!markdownSet.Contains((Normalize(category), Normalize(title))))
            {
                result.AddError($"Trap '{title}' (category '{category}') from traps.json is missing in TRAPS.md.");
            }

            foreach (string file in trap.Files)
            {
                if (string.IsNullOrEmpty(file))
                {
                    continue;
                }

                string resolvedPath = ResolvePath(context, file);
                if (!PathExists(resolvedPath))
                {
                    result.AddError($"Trap '{title}': file '{file}' does not exist (resolved to '{resolvedPath}').");
                }
            }
        }

        return result;
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string trimmed = value.Trim();
        string collapsed = string.Join(
            " ",
            trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries));

        // Markdown-заголовки могут использовать обратные апострофы для форматирования
        // (например, "`PaymentType.Debt` не влияет..."), тогда как JSON хранит чистый текст.
        return collapsed.Replace("`", string.Empty).ToLowerInvariant();
    }

    private static string ResolvePath(ValidationContext context, string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        // Пути вида ../../AutoKassa/... заданы относительно docs/kimi/json/.
        string fromDocs = Path.GetFullPath(Path.Combine(context.DocsPath, path));
        if (PathExists(fromDocs))
        {
            return fromDocs;
        }

        // Документ-файлы (TRAPS.md, json/traps.json) заданы относительно docs/kimi/.
        string docsParent = Path.GetDirectoryName(context.DocsPath) ?? context.ProjectRoot;
        string fromDocsParent = Path.GetFullPath(Path.Combine(docsParent, path));
        if (PathExists(fromDocsParent))
        {
            return fromDocsParent;
        }

        // Фолбэк на корень репозитория.
        return Path.GetFullPath(Path.Combine(context.ProjectRoot, path));
    }

    private static bool PathExists(string path)
    {
        return Directory.Exists(path) || File.Exists(path);
    }
}
