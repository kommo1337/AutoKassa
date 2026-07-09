using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class IndexCatalog
{
    [JsonPropertyName("project")]
    public string? Project { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("documentationRoot")]
    public string? DocumentationRoot { get; set; }

    [JsonPropertyName("entryPoints")]
    public IndexEntryPoints? EntryPoints { get; set; }

    [JsonPropertyName("structure")]
    public List<IndexSection> Structure { get; set; } = new();

    [JsonPropertyName("skills")]
    public List<IndexSkill> Skills { get; set; } = new();

    [JsonPropertyName("keywords")]
    public List<IndexKeyword> Keywords { get; set; } = new();

    [JsonPropertyName("iNeedTo")]
    public List<IndexNeed> INeedTo { get; set; } = new();
}

public sealed class IndexEntryPoints
{
    [JsonPropertyName("kimi")]
    public string? Kimi { get; set; }

    [JsonPropertyName("principles")]
    public string? Principles { get; set; }

    [JsonPropertyName("traps")]
    public string? Traps { get; set; }

    [JsonPropertyName("index")]
    public string? Index { get; set; }
}

public sealed class IndexSection
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("files")]
    public List<IndexSectionFile> Files { get; set; } = new();
}

public sealed class IndexSectionFile
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }
}

public sealed class IndexSkill
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }
}

public sealed class IndexKeyword
{
    [JsonPropertyName("term")]
    public string? Term { get; set; }

    [JsonPropertyName("files")]
    public List<string> Files { get; set; } = new();
}

public sealed class IndexNeed
{
    [JsonPropertyName("task")]
    public string? Task { get; set; }

    [JsonPropertyName("read")]
    public List<string> Read { get; set; } = new();
}
