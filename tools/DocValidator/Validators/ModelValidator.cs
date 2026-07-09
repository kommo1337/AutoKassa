using System.Text.RegularExpressions;
using AutoKassa.DocValidator.CodeScanner;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;

namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Сверяет сущности/DTO из <c>AutoKassa/Models/*.cs</c> и <c>DbSet&lt;T&gt;</c> в <c>AppDbContext.cs</c>
/// со справочником <c>models.json</c>.
/// </summary>
public sealed class ModelValidator : IValidator
{
    public string Name => "ModelValidator";

    public ValidationResult Validate(ValidationContext context)
    {
        var result = new ValidationResult();
        IReadOnlyList<ScannedModel> scannedModels = ModelScanner.Scan(context);
        List<EntityEntry> catalogEntities = context.Models.Entities;

        var scannedByName = scannedModels.ToDictionary(m => m.Name, StringComparer.Ordinal);
        var catalogByName = catalogEntities
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .ToDictionary(e => e.Name!, StringComparer.Ordinal);

        IReadOnlyDictionary<string, string> dbSets = ScanDbSets(context);

        // 1. Каждый класс из Models/*.cs должен присутствовать в models.json.
        foreach (ScannedModel scanned in scannedModels)
        {
            if (!catalogByName.TryGetValue(scanned.Name, out EntityEntry? catalogEntry))
            {
                result.AddError($"Model '{scanned.Name}' from {scanned.File} is missing in models.json.");
                continue;
            }

            ValidateEntry(result, context, scanned, catalogEntry, dbSets);
        }

        // 2. Каждая запись models.json должна иметь соответствующий файл в коде.
        foreach (EntityEntry catalogEntry in catalogEntities)
        {
            if (string.IsNullOrEmpty(catalogEntry.Name))
            {
                result.AddError("models.json contains an entity entry without a name.");
                continue;
            }

            if (!scannedByName.ContainsKey(catalogEntry.Name))
            {
                result.AddError($"Entity '{catalogEntry.Name}' from models.json was not found in AutoKassa/Models/*.cs.");
            }
        }

        // 3. Сверка миграций.
        ValidateMigrations(result, context);

        return result;
    }

    private static void ValidateEntry(ValidationResult result, ValidationContext context, ScannedModel scanned, EntityEntry catalog, IReadOnlyDictionary<string, string> dbSets)
    {
        // Сверка пути к файлу.
        if (!string.IsNullOrEmpty(catalog.File) &&
            !PathsEqual(scanned.File, catalog.File))
        {
            result.AddError($"Model '{scanned.Name}': file mismatch. Code: '{scanned.File}', JSON: '{catalog.File}'.");
        }

        bool catalogIsDto = catalog.IsDto ?? false;

        // Сверка DTO/Entity.
        if (scanned.IsDto != catalogIsDto)
        {
            result.AddError($"Model '{scanned.Name}': DTO flag mismatch. Code: {scanned.IsDto}, JSON: {catalogIsDto}.");
        }

        if (catalogIsDto)
        {
            // Для DTO table должен быть null.
            if (!string.IsNullOrEmpty(catalog.Table))
            {
                result.AddError($"Model '{scanned.Name}' is marked as DTO in code, but models.json specifies table '{catalog.Table}'.");
            }

            return;
        }

        // Для entity должна быть таблица и DbSet.
        if (string.IsNullOrEmpty(catalog.Table))
        {
            result.AddError($"Model '{scanned.Name}' is an entity, but models.json does not specify a table.");
            return;
        }

        if (!dbSets.TryGetValue(scanned.Name, out string? dbSetTable))
        {
            result.AddError($"Model '{scanned.Name}' is an entity, but no DbSet<{scanned.Name}> found in AppDbContext.cs.");
            return;
        }

        if (!string.Equals(dbSetTable, catalog.Table, StringComparison.OrdinalIgnoreCase))
        {
            result.AddError($"Model '{scanned.Name}': table mismatch. AppDbContext: '{dbSetTable}', models.json: '{catalog.Table}'.");
        }
    }

    private static IReadOnlyDictionary<string, string> ScanDbSets(ValidationContext context)
    {
        var dbSets = new Dictionary<string, string>(StringComparer.Ordinal);
        string appDbContextPath = Path.Combine(context.AppPath, "Services", "AppDbContext.cs");

        if (!File.Exists(appDbContextPath))
        {
            return dbSets;
        }

        string content = File.ReadAllText(appDbContextPath);
        content = StripComments(content);

        // public DbSet<Name> TableName { get; set; }
        var regex = new Regex(
            @"public\s+DbSet\s*<\s*(?<entity>\w+)\s*>\s+(?<table>\w+)\s*\{",
            RegexOptions.Compiled);

        foreach (Match match in regex.Matches(content).Cast<Match>())
        {
            string entity = match.Groups["entity"].Value;
            string table = match.Groups["table"].Value;
            dbSets[entity] = table;
        }

        return dbSets;
    }

    private static void ValidateMigrations(ValidationResult result, ValidationContext context)
    {
        List<MigrationEntry> catalogMigrations = context.Models.Migrations;
        string migrationsDir = Path.Combine(context.AppPath, "Migrations");

        if (!Directory.Exists(migrationsDir))
        {
            if (catalogMigrations.Count > 0)
            {
                result.AddError("Migrations directory does not exist, but models.json contains migrations.");
            }

            return;
        }

        var files = Directory.EnumerateFiles(migrationsDir, "*.cs")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(f => f is not null
                && !f.EndsWith("ModelSnapshot", StringComparison.OrdinalIgnoreCase)
                && !f.EndsWith(".Designer", StringComparison.OrdinalIgnoreCase))
            .Select(f => ExtractMigrationName(f!))
            .ToHashSet(StringComparer.Ordinal);

        var catalogNames = catalogMigrations
            .Where(m => !string.IsNullOrEmpty(m.Name))
            .Select(m => m.Name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (MigrationEntry migration in catalogMigrations)
        {
            if (string.IsNullOrEmpty(migration.Name))
            {
                result.AddError("models.json contains a migration entry without a name.");
                continue;
            }

            if (!files.Contains(migration.Name))
            {
                result.AddError($"Migration '{migration.Name}' from models.json was not found in AutoKassa/Migrations/.");
            }
        }

        foreach (string file in files)
        {
            if (!catalogNames.Contains(file))
            {
                result.AddError($"Migration '{file}' from AutoKassa/Migrations/ is missing in models.json.");
            }
        }
    }

    private static string ExtractMigrationName(string fileName)
    {
        // Имя файла миграции обычно имеет вид "20260107002354_AddDefaultOperationSettings".
        int underscore = fileName.IndexOf('_');
        return underscore >= 0 ? fileName.Substring(underscore + 1) : fileName;
    }

    private static string StripComments(string content)
    {
        content = Regex.Replace(content, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);

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

    private static bool PathsEqual(string first, string second)
    {
        return string.Equals(first.Replace('\\', '/'), second.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }
}
