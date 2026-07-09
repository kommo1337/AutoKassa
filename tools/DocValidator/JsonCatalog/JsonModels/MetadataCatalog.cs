using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class MetadataCatalog
{
    [JsonPropertyName("project")]
    public string? Project { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    [JsonPropertyName("targetFramework")]
    public string? TargetFramework { get; set; }

    [JsonPropertyName("language")]
    public string? Language { get; set; }

    [JsonPropertyName("outputType")]
    public string? OutputType { get; set; }

    [JsonPropertyName("uiFramework")]
    public string? UiFramework { get; set; }

    [JsonPropertyName("orm")]
    public string? Orm { get; set; }

    [JsonPropertyName("database")]
    public string? Database { get; set; }

    [JsonPropertyName("testFrameworks")]
    public List<string> TestFrameworks { get; set; } = new();

    [JsonPropertyName("keyLibraries")]
    public List<string> KeyLibraries { get; set; } = new();

    [JsonPropertyName("technologyStack")]
    public List<TechnologyStackEntry> TechnologyStack { get; set; } = new();

    [JsonPropertyName("keyFiles")]
    public KeyFilesEntry? KeyFiles { get; set; }

    [JsonPropertyName("projects")]
    public List<ProjectEntry> Projects { get; set; } = new();
}

public sealed class TechnologyStackEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("purpose")]
    public string? Purpose { get; set; }
}

public sealed class KeyFilesEntry
{
    [JsonPropertyName("app")]
    public string? App { get; set; }

    [JsonPropertyName("mainWindow")]
    public string? MainWindow { get; set; }

    [JsonPropertyName("mainWindowCodeBehind")]
    public string? MainWindowCodeBehind { get; set; }

    [JsonPropertyName("dbContext")]
    public string? DbContext { get; set; }

    [JsonPropertyName("projectFile")]
    public string? ProjectFile { get; set; }

    [JsonPropertyName("testProjectFile")]
    public string? TestProjectFile { get; set; }

    [JsonPropertyName("solutionFile")]
    public string? SolutionFile { get; set; }

    [JsonPropertyName("directoryBuildProps")]
    public string? DirectoryBuildProps { get; set; }

    [JsonPropertyName("agentsGuide")]
    public string? AgentsGuide { get; set; }

    [JsonPropertyName("entryDoc")]
    public string? EntryDoc { get; set; }
}

public sealed class ProjectEntry
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("outputType")]
    public string? OutputType { get; set; }
}
