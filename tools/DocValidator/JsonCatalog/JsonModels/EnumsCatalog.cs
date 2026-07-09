using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class EnumsCatalog
{
    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("enums")]
    public List<EnumEntry> Enums { get; set; } = new();
}

public sealed class EnumEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    [JsonPropertyName("underlyingType")]
    public string? UnderlyingType { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }

    [JsonPropertyName("values")]
    public List<EnumValue> Values { get; set; } = new();

    [JsonPropertyName("usedIn")]
    public List<string> UsedIn { get; set; } = new();

    [JsonPropertyName("uiMappings")]
    public EnumUiMappings? UiMappings { get; set; }
}

public sealed class EnumValue
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("value")]
    public int? Value { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class EnumUiMappings
{
    [JsonPropertyName("textConverter")]
    public string? TextConverter { get; set; }

    [JsonPropertyName("colorConverter")]
    public string? ColorConverter { get; set; }

    [JsonPropertyName("iconConverter")]
    public string? IconConverter { get; set; }

    [JsonPropertyName("mappings")]
    public List<EnumMapping> Mappings { get; set; } = new();
}

public sealed class EnumMapping
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}
