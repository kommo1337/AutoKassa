using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class TrapsCatalog
{
    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("traps")]
    public List<TrapEntry> Traps { get; set; } = new();
}

public sealed class TrapEntry
{
    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("forbidden")]
    public string? Forbidden { get; set; }

    [JsonPropertyName("why")]
    public string? Why { get; set; }

    [JsonPropertyName("correct")]
    public string? Correct { get; set; }

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
