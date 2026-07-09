using System.Text.Json;
using AutoKassa.DocValidator.JsonCatalog;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.JsonCatalog;

public class CatalogLoaderTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        foreach (var dir in _tempDirectories)
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public void Load_WithRepositoryRoot_LoadsAllCatalogs()
    {
        string repoRoot = FindRepositoryRoot();

        var context = CatalogLoader.Load(repoRoot);

        context.Should().NotBeNull();
        context.Services.Should().NotBeNull();
        context.ViewModels.Should().NotBeNull();
        context.Models.Should().NotBeNull();
        context.Enums.Should().NotBeNull();
        context.Relationships.Should().NotBeNull();
        context.Flows.Should().NotBeNull();
        context.Traps.Should().NotBeNull();
        context.Metadata.Should().NotBeNull();
        context.Index.Should().NotBeNull();

        context.Metadata.Version.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Load_WithSampleCatalogs_LoadsExpectedCounts()
    {
        string docs = CreateSampleDocsDirectory();

        var context = CatalogLoader.Load(docs, docsPath: docs);

        context.Services.Services.Should().HaveCount(2);
        context.ViewModels.ViewModels.Should().HaveCount(1);
        context.Models.Entities.Should().HaveCount(1);
        context.Enums.Enums.Should().HaveCount(1);
        context.Relationships.ViewsToViewModels.Should().HaveCount(1);
        context.Flows.Flows.Should().HaveCount(1);
        context.Traps.Traps.Should().HaveCount(1);
        context.Index.Structure.Should().HaveCount(1);

        var service = context.Services.Services.First();
        service.Name.Should().Be("TransactionService");
        service.Lifetime.Should().Be("Transient");
    }

    [Fact]
    public void Load_WithMissingFile_ThrowsFileNotFoundException()
    {
        string emptyDir = CreateTempDirectory();

        Action act = () => CatalogLoader.Load(emptyDir, docsPath: emptyDir);

        act.Should().Throw<FileNotFoundException>();
    }

    [Theory]
    [InlineData("2026-07-08T22:00:00+03:00")]
    [InlineData("2026-07-08")]
    [InlineData("2026-07-08T19:00:00Z")]
    public void Load_AcceptsDifferentLastUpdatedFormats(string lastUpdated)
    {
        string docs = CreateTempDirectory();
        WriteCatalog(docs, "services.json", $"{{ \"lastUpdated\": \"{lastUpdated}\", \"services\": [] }}");
        WriteCatalog(docs, "viewmodels.json", "{ \"viewModels\": [] }");
        WriteCatalog(docs, "models.json", "{ \"entities\": [], \"migrations\": [] }");
        WriteCatalog(docs, "enums.json", "{ \"enums\": [] }");
        WriteCatalog(docs, "relationships.json", "{ \"viewsToViewModels\": [], \"viewModelsToServices\": [], \"servicesToServices\": [] }");
        WriteCatalog(docs, "flows.json", "{ \"flows\": [] }");
        WriteCatalog(docs, "traps.json", "{ \"traps\": [] }");
        WriteCatalog(docs, "metadata.json", "{ \"version\": \"1.0.0\" }");
        WriteCatalog(docs, "index.json", "{ \"structure\": [], \"skills\": [], \"keywords\": [], \"iNeedTo\": [] }");

        var context = CatalogLoader.Load(docs, docsPath: docs);

        context.Services.LastUpdated.Should().NotBeNull();
    }

    [Fact]
    public void Load_WithCommentsAndTrailingCommas_ParsesSuccessfully()
    {
        string docs = CreateTempDirectory();
        const string json = @"{
            // catalog header
            ""version"": ""1.0.0"", // project version
        }";
        WriteCatalog(docs, "metadata.json", json);
        WriteCatalog(docs, "services.json", "{ \"services\": [] }");
        WriteCatalog(docs, "viewmodels.json", "{ \"viewModels\": [] }");
        WriteCatalog(docs, "models.json", "{ \"entities\": [], \"migrations\": [] }");
        WriteCatalog(docs, "enums.json", "{ \"enums\": [] }");
        WriteCatalog(docs, "relationships.json", "{ \"viewsToViewModels\": [], \"viewModelsToServices\": [], \"servicesToServices\": [] }");
        WriteCatalog(docs, "flows.json", "{ \"flows\": [] }");
        WriteCatalog(docs, "traps.json", "{ \"traps\": [] }");
        WriteCatalog(docs, "index.json", "{ \"structure\": [], \"skills\": [], \"keywords\": [], \"iNeedTo\": [] }");

        var context = CatalogLoader.Load(docs, docsPath: docs);

        context.Metadata.Version.Should().Be("1.0.0");
    }

    private static string FindRepositoryRoot()
    {
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current))
        {
            if (File.Exists(Path.Combine(current, "AutoKassa.sln")))
            {
                return current;
            }

            string parent = Directory.GetParent(current)?.FullName!;
            if (parent == current)
            {
                break;
            }

            current = parent;
        }

        throw new InvalidOperationException("Could not locate repository root (AutoKassa.sln).");
    }

    private string CreateSampleDocsDirectory()
    {
        string docs = CreateTempDirectory();

        WriteCatalog(docs, "services.json", @"{
  ""lastUpdated"": ""2026-07-08T22:00:00+03:00"",
  ""services"": [
    { ""name"": ""TransactionService"", ""interface"": ""ITransactionService"", ""implementation"": ""TransactionService"", ""lifetime"": ""Transient"" },
    { ""name"": ""SettingsService"", ""lifetime"": ""Singleton"" }
  ]
}");

        WriteCatalog(docs, "viewmodels.json", @"{
  ""viewModels"": [
    { ""name"": ""DashboardViewModel"", ""registeredInDi"": true, ""dependencies"": [""TransactionService""] }
  ]
}");

        WriteCatalog(docs, "models.json", @"{
  ""entities"": [
    { ""name"": ""Transaction"", ""table"": ""Transactions"" }
  ],
  ""migrations"": [
    { ""name"": ""InitialCreate"", ""file"": ""AutoKassa/Migrations/InitialCreate.cs"" }
  ]
}");

        WriteCatalog(docs, "enums.json", @"{
  ""enums"": [
    { ""name"": ""OperationType"", ""underlyingType"": ""int"", ""values"": [{ ""name"": ""Income"", ""value"": 1 }] }
  ]
}");

        WriteCatalog(docs, "relationships.json", @"{
  ""viewsToViewModels"": [{ ""view"": ""DashboardView.xaml"", ""viewModel"": ""DashboardViewModel"" }],
  ""viewModelsToServices"": [],
  ""servicesToServices"": []
}");

        WriteCatalog(docs, "flows.json", @"{
  ""flows"": [
    { ""id"": ""add-income"", ""name"": ""Add income"" }
  ]
}");

        WriteCatalog(docs, "traps.json", @"{
  ""traps"": [
    { ""category"": ""DI"", ""title"": ""Service Locator"" }
  ]
}");

        WriteCatalog(docs, "metadata.json", @"{
  ""version"": ""1.0.0"",
  ""project"": ""AutoKassa""
}");

        WriteCatalog(docs, "index.json", @"{
  ""structure"": [
    {
      ""id"": ""entry"",
      ""title"": ""Architecture"",
      ""files"": [
        { ""id"": ""architecture"", ""file"": ""02-architecture.md"", ""purpose"": ""Architecture"" }
      ]
    }
  ],
  ""skills"": [],
  ""keywords"": [],
  ""iNeedTo"": []
}");

        return docs;
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }

    private static void WriteCatalog(string directory, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(directory, fileName), content);
    }
}
