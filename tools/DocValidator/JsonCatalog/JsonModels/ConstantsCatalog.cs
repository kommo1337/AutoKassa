using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class ConstantsCatalog
{
    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("sourceRoot")]
    public string? SourceRoot { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("categories")]
    public List<ConstantCategory> Categories { get; set; } = new();
}

public sealed class ConstantCategory
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("constants")]
    public List<ConstantEntry> Constants { get; set; } = new();
}

public sealed class ConstantEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    // JsonElement? с NullAwareJsonElementConverter, чтобы отличать отсутствие поля
    // (ошибка: обязательное поле) от явного null (допустимое значение, например у BackupPath).
    [JsonPropertyName("value")]
    [JsonConverter(typeof(NullAwareJsonElementConverter))]
    public JsonElement? Value { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("sourceFiles")]
    public List<string> SourceFiles { get; set; } = new();

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("relatedEnum")]
    public string? RelatedEnum { get; set; }

    [JsonPropertyName("isConfigurable")]
    public bool IsConfigurable { get; set; }

    // true — константа является чисто документационной (понятие предметной области,
    // дословно в коде не встречается): поиск имени/значения и warning отключаются.
    [JsonPropertyName("docOnly")]
    public bool DocOnly { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
