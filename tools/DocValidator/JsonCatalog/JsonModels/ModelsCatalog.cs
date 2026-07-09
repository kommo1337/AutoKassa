using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class ModelsCatalog
{
    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("entities")]
    public List<EntityEntry> Entities { get; set; } = new();

    [JsonPropertyName("migrations")]
    public List<MigrationEntry> Migrations { get; set; } = new();
}

public sealed class EntityEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("table")]
    public string? Table { get; set; }

    [JsonPropertyName("isDto")]
    public bool? IsDto { get; set; }

    [JsonPropertyName("fields")]
    public List<EntityField> Fields { get; set; } = new();

    [JsonPropertyName("indexes")]
    public List<EntityIndex> Indexes { get; set; } = new();

    [JsonPropertyName("foreignKeys")]
    public List<EntityForeignKey> ForeignKeys { get; set; } = new();

    [JsonPropertyName("seedData")]
    public object? SeedData { get; set; }

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();
}

public sealed class EntityField
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("nullable")]
    public bool? Nullable { get; set; }

    [JsonPropertyName("isKey")]
    public bool? IsKey { get; set; }

    [JsonPropertyName("isRequired")]
    public bool? IsRequired { get; set; }

    [JsonPropertyName("notes")]
    public List<string> Notes { get; set; } = new();
}

public sealed class EntityIndex
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("fields")]
    public List<string> Fields { get; set; } = new();

    [JsonPropertyName("unique")]
    public bool? Unique { get; set; }
}

public sealed class EntityForeignKey
{
    [JsonPropertyName("property")]
    public string? Property { get; set; }

    [JsonPropertyName("principalTable")]
    public string? PrincipalTable { get; set; }

    [JsonPropertyName("principalColumn")]
    public string? PrincipalColumn { get; set; }

    [JsonPropertyName("onDelete")]
    public string? OnDelete { get; set; }
}

public sealed class MigrationEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }
}
