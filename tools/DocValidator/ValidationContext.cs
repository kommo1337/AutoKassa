using AutoKassa.DocValidator.JsonCatalog.JsonModels;

namespace AutoKassa.DocValidator;

/// <summary>
/// Контекст валидации: пути к проекту и загруженные JSON-справочники.
/// </summary>
public sealed class ValidationContext
{
    public ValidationContext(string projectRoot, string docsPath, string appPath)
    {
        ProjectRoot = projectRoot;
        DocsPath = docsPath;
        AppPath = appPath;
    }

    public string ProjectRoot { get; }

    public string DocsPath { get; }

    public string AppPath { get; }

    public ServicesCatalog Services { get; set; } = new();

    public ViewModelsCatalog ViewModels { get; set; } = new();

    public ModelsCatalog Models { get; set; } = new();

    public EnumsCatalog Enums { get; set; } = new();

    public RelationshipsCatalog Relationships { get; set; } = new();

    public FlowsCatalog Flows { get; set; } = new();

    public TrapsCatalog Traps { get; set; } = new();

    public MetadataCatalog Metadata { get; set; } = new();

    public IndexCatalog Index { get; set; } = new();
}
