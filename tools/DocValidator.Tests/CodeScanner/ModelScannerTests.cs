using AutoKassa.DocValidator.CodeScanner;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.CodeScanner;

public class ModelScannerTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        foreach (string dir in _tempDirectories)
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
    public void Scan_WithEntity_ParsesClass()
    {
        (string projectRoot, string appPath) = CreateProjectWithModel("Transaction.cs", @"
namespace AutoKassa.Models
{
    public class Transaction
    {
        public int Id { get; set; }
    }
}
");

        IReadOnlyList<ScannedModel> result = ModelScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        result.Single().Name.Should().Be("Transaction");
        result.Single().IsDto.Should().BeFalse();
    }

    [Fact]
    public void Scan_WithReportDto_MarksAsDto()
    {
        (string projectRoot, string appPath) = CreateProjectWithModel(
            Path.Combine("Reports", "BalanceReport.cs"),
            @"
namespace AutoKassa.Models.Reports
{
    public class BalanceReport : ReportBase
    {
        public decimal Total { get; set; }
    }
}
");

        IReadOnlyList<ScannedModel> result = ModelScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        result.Single().Name.Should().Be("BalanceReport");
        result.Single().IsDto.Should().BeTrue();
    }

    [Fact]
    public void Scan_WithReportBase_MarksAsDto()
    {
        (string projectRoot, string appPath) = CreateProjectWithModel("ReportBase.cs", @"
namespace AutoKassa.Models
{
    public abstract class ReportBase
    {
        public string Name { get; set; }
    }
}
");

        IReadOnlyList<ScannedModel> result = ModelScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        result.Single().Name.Should().Be("ReportBase");
        result.Single().IsDto.Should().BeTrue();
    }

    [Fact]
    public void Scan_WithFilterParameters_MarksAsDto()
    {
        (string projectRoot, string appPath) = CreateProjectWithModel("FilterParameters.cs", @"
namespace AutoKassa.Models
{
    public class TransactionFilterParameters
    {
        public DateTime? DateFrom { get; set; }
    }
}
");

        IReadOnlyList<ScannedModel> result = ModelScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        result.Single().Name.Should().Be("TransactionFilterParameters");
        result.Single().IsDto.Should().BeTrue();
    }

    [Fact]
    public void Scan_WithNestedReportItem_MarksAsDto()
    {
        (string projectRoot, string appPath) = CreateProjectWithModel(
            Path.Combine("Reports", "BalanceReport.cs"),
            @"
namespace AutoKassa.Models.Reports
{
    public class BalanceReport : ReportBase
    {
        public List<DailyBalance> Items { get; set; }
    }

    public class DailyBalance
    {
        public DateTime Date { get; set; }
    }
}
");

        IReadOnlyList<ScannedModel> result = ModelScanner.Scan(projectRoot, appPath);

        result.Should().HaveCount(2);
        result.Should().Contain(m => m.Name == "BalanceReport" && m.IsDto);
        result.Should().Contain(m => m.Name == "DailyBalance" && m.IsDto);
    }

    [Fact]
    public void Scan_WithNoModelsDirectory_ReturnsEmptyList()
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        Directory.CreateDirectory(appPath);

        IReadOnlyList<ScannedModel> result = ModelScanner.Scan(projectRoot, appPath);

        result.Should().BeEmpty();
    }

    private (string ProjectRoot, string AppPath) CreateProjectWithModel(string relativePath, string content)
    {
        string projectRoot = CreateTempDirectory();
        string modelsDir = Path.Combine(projectRoot, "AutoKassa", "Models", Path.GetDirectoryName(relativePath)!);
        Directory.CreateDirectory(modelsDir);
        File.WriteAllText(Path.Combine(modelsDir, Path.GetFileName(relativePath)), content);
        return (projectRoot, Path.Combine(projectRoot, "AutoKassa"));
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }
}
