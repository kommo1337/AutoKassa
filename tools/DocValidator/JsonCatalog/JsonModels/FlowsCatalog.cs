using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class FlowsCatalog
{
    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("flows")]
    public List<FlowEntry> Flows { get; set; } = new();
}

public sealed class FlowEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("trigger")]
    public string? Trigger { get; set; }

    [JsonPropertyName("steps")]
    public List<FlowStep> Steps { get; set; } = new();

    [JsonPropertyName("servicesInvolved")]
    public List<string> ServicesInvolved { get; set; } = new();

    [JsonPropertyName("viewModelsInvolved")]
    public List<string> ViewModelsInvolved { get; set; } = new();

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
}

public sealed class FlowStep
{
    [JsonPropertyName("order")]
    public int? Order { get; set; }

    [JsonPropertyName("actor")]
    public string? Actor { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
