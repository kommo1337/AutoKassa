using System.Text.Json;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;

namespace AutoKassa.DocValidator.JsonCatalog;

/// <summary>
/// Загружает все JSON-справочники из <c>docs/kimi/json/</c>.
/// </summary>
public static class CatalogLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ValidationContext Load(string projectRoot, string? docsPath = null, string? appPath = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectRoot);

        if (!Directory.Exists(projectRoot))
        {
            throw new DirectoryNotFoundException($"Project root not found: {projectRoot}");
        }

        string docs = docsPath ?? Path.Combine(projectRoot, "docs", "kimi", "json");
        string app = appPath ?? Path.Combine(projectRoot, "AutoKassa");

        var context = new ValidationContext(projectRoot, docs, app)
        {
            Services = LoadCatalog<ServicesCatalog>(docs, "services.json"),
            ViewModels = LoadCatalog<ViewModelsCatalog>(docs, "viewmodels.json"),
            Models = LoadCatalog<ModelsCatalog>(docs, "models.json"),
            Enums = LoadCatalog<EnumsCatalog>(docs, "enums.json"),
            Relationships = LoadCatalog<RelationshipsCatalog>(docs, "relationships.json"),
            Flows = LoadCatalog<FlowsCatalog>(docs, "flows.json"),
            Traps = LoadCatalog<TrapsCatalog>(docs, "traps.json"),
            Metadata = LoadCatalog<MetadataCatalog>(docs, "metadata.json"),
            Index = LoadCatalog<IndexCatalog>(docs, "index.json"),
        };

        return context;
    }

    private static T LoadCatalog<T>(string directory, string fileName) where T : new()
    {
        string path = Path.Combine(directory, fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"JSON catalog not found: {path}", path);
        }

        string json = File.ReadAllText(path);
        T? result = JsonSerializer.Deserialize<T>(json, Options);

        if (result is null)
        {
            throw new JsonException($"Catalog {path} deserialized to null.");
        }

        return result;
    }
}
