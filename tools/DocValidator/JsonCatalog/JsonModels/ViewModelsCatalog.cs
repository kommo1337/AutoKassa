using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class ViewModelsCatalog
{
    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("viewModels")]
    public List<ViewModelEntry> ViewModels { get; set; } = new();
}

public sealed class ViewModelEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("view")]
    public string? View { get; set; }

    [JsonPropertyName("screen")]
    public string? Screen { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("registeredInDi")]
    public bool? RegisteredInDi { get; set; }

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("interfaces")]
    public List<string> Interfaces { get; set; } = new();

    [JsonPropertyName("keyCommands")]
    public List<string> KeyCommands { get; set; } = new();

    [JsonPropertyName("usedBy")]
    public List<string> UsedBy { get; set; } = new();

    [JsonPropertyName("nestedClasses")]
    public List<NestedClassEntry> NestedClasses { get; set; } = new();

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class NestedClassEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }
}
