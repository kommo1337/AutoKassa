using AutoKassa.DocValidator;
using AutoKassa.DocValidator.JsonCatalog;
using AutoKassa.DocValidator.Validators;

string? projectRoot = null;
string? docsPath = null;
string? appPath = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--project-root" when i + 1 < args.Length:
            projectRoot = args[++i];
            break;
        case "--docs-path" when i + 1 < args.Length:
            docsPath = args[++i];
            break;
        case "--app-path" when i + 1 < args.Length:
            appPath = args[++i];
            break;
        default:
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                projectRoot = args[i];
            }

            break;
    }
}

projectRoot ??= Directory.GetCurrentDirectory();

IValidator[] validators =
{
    new EnumValidator(),
    new ViewValidator(),
    new DiLifetimeValidator(),
    new ModelValidator(),
    new ViewModelValidator(),
    new TrapSyncValidator(),
};

try
{
    ValidationContext context = CatalogLoader.Load(projectRoot, docsPath, appPath);

    Console.WriteLine("DocValidator loaded catalogs successfully:");
    Console.WriteLine($"  Project root       : {context.ProjectRoot}");
    Console.WriteLine($"  Docs path          : {context.DocsPath}");
    Console.WriteLine($"  App path           : {context.AppPath}");
    Console.WriteLine($"  services.json      : {context.Services.Services.Count} entries");
    Console.WriteLine($"  viewmodels.json    : {context.ViewModels.ViewModels.Count} entries");
    Console.WriteLine($"  models.json        : {context.Models.Entities.Count} entities, {context.Models.Migrations.Count} migrations");
    Console.WriteLine($"  enums.json         : {context.Enums.Enums.Count} enums");
    Console.WriteLine($"  relationships.json : {context.Relationships.ViewsToViewModels.Count} views, {context.Relationships.ViewModelsToServices.Count} vm->svc, {context.Relationships.ServicesToServices.Count} svc->svc");
    Console.WriteLine($"  flows.json         : {context.Flows.Flows.Count} flows");
    Console.WriteLine($"  traps.json         : {context.Traps.Traps.Count} traps");
    Console.WriteLine($"  metadata.json      : version {context.Metadata.Version ?? "n/a"}");
    Console.WriteLine($"  index.json         : {context.Index.Structure.Count} sections");
    Console.WriteLine();

    bool allValid = true;
    int totalErrors = 0;

    foreach (IValidator validator in validators)
    {
        ValidationResult result = validator.Validate(context);

        if (result.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[PASS] {validator.Name}");
            Console.ResetColor();
        }
        else
        {
            allValid = false;
            totalErrors += result.ErrorCount;

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[FAIL] {validator.Name}: {result.ErrorCount} error(s)");
            Console.ResetColor();

            foreach (string error in result.Errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }
    }

    Console.WriteLine();

    if (allValid)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("All validators passed.");
        Console.ResetColor();
        return 0;
    }

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Validation failed with {totalErrors} error(s).");
    Console.ResetColor();
    return 1;
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Critical error: {ex.Message}");
    Console.ResetColor();
    return 2;
}
