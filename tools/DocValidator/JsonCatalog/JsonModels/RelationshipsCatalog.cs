using System.Text.Json.Serialization;

namespace AutoKassa.DocValidator.JsonCatalog.JsonModels;

public sealed class RelationshipsCatalog
{
    [JsonPropertyName("lastUpdated")]
    [JsonConverter(typeof(FlexibleDateTimeConverter))]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("viewsToViewModels")]
    public List<ViewToViewModel> ViewsToViewModels { get; set; } = new();

    [JsonPropertyName("viewModelsToServices")]
    public List<ViewModelToServices> ViewModelsToServices { get; set; } = new();

    [JsonPropertyName("servicesToServices")]
    public List<ServiceToService> ServicesToServices { get; set; } = new();
}

public sealed class ViewToViewModel
{
    [JsonPropertyName("view")]
    public string? View { get; set; }

    [JsonPropertyName("viewModel")]
    public string? ViewModel { get; set; }

    [JsonPropertyName("dataTemplateIn")]
    public string? DataTemplateIn { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class ViewModelToServices
{
    [JsonPropertyName("viewModel")]
    public string? ViewModel { get; set; }

    [JsonPropertyName("services")]
    public List<ViewModelServiceEntry> Services { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class ViewModelServiceEntry
{
    [JsonPropertyName("service")]
    public string? Service { get; set; }

    [JsonPropertyName("via")]
    public string? Via { get; set; }

    [JsonPropertyName("commands")]
    public List<string> Commands { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

public sealed class ServiceToService
{
    [JsonPropertyName("service")]
    public string? Service { get; set; }

    [JsonPropertyName("dependencies")]
    public List<string> Dependencies { get; set; } = new();

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
