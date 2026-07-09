using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class ServicesCatalog
{
    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("totalCount")]
    public int? TotalCount { get; set; }

    [JsonPropertyName("services")]
    public List<ServiceEntry> Services { get; set; } = new();
}

public sealed class ServiceEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("interface")]
    public string? Interface { get; set; }

    [JsonPropertyName("implementation")]
    public string? Implementation { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("interfaceFile")]
    public string? InterfaceFile { get; set; }

    [JsonPropertyName("lifetime")]
    public string? Lifetime { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("keyMethods")]
    public List<KeyMethodEntry> KeyMethods { get; set; } = new();

    [JsonPropertyName("usedBy")]
    public List<string> UsedBy { get; set; } = new();

    [JsonPropertyName("docFile")]
    public string? DocFile { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class KeyMethodEntry
{
    [JsonPropertyName("signature")]
    public string? Signature { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
