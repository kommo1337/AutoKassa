using AutoKassa.DocValidator.CodeScanner;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.CodeScanner;

public class EnumScannerTests : IDisposable
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
    public void Scan_WithExplicitValues_ParsesMembersCorrectly()
    {
        (string projectRoot, string appPath) = CreateProjectWithEnum("OperationType.cs", @"
namespace AutoKassa.Models.Enums
{
    public enum OperationType
    {
        Income = 1,
        Expense = 2
    }
}
");

        IReadOnlyList<ScannedEnum> result = EnumScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        ScannedEnum operationType = result.Single();
        operationType.Name.Should().Be("OperationType");
        operationType.UnderlyingType.Should().Be("int");
        operationType.Members.Should().HaveCount(2);
        operationType.Members[0].Name.Should().Be("Income");
        operationType.Members[0].Value.Should().Be(1);
        operationType.Members[1].Name.Should().Be("Expense");
        operationType.Members[1].Value.Should().Be(2);
    }

    [Fact]
    public void Scan_WithImplicitValues_AssignsSequentialValuesFromZero()
    {
        (string projectRoot, string appPath) = CreateProjectWithEnum("PeriodType.cs", @"
namespace AutoKassa.Models.Enums;

public enum PeriodType
{
    Today,
    Week,
    Month
}
");

        IReadOnlyList<ScannedEnum> result = EnumScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        ScannedEnum periodType = result.Single();
        periodType.Members.Should().HaveCount(3);
        periodType.Members[0].Should().BeEquivalentTo(new { Name = "Today", Value = 0 });
        periodType.Members[1].Should().BeEquivalentTo(new { Name = "Week", Value = 1 });
        periodType.Members[2].Should().BeEquivalentTo(new { Name = "Month", Value = 2 });
    }

    [Fact]
    public void Scan_WithMixedImplicitAndExplicitValues_ComputesCorrectValues()
    {
        (string projectRoot, string appPath) = CreateProjectWithEnum("MixedEnum.cs", @"
namespace AutoKassa.Models.Enums
{
    public enum MixedEnum
    {
        A = 5,
        B,
        C = 10,
        D
    }
}
");

        IReadOnlyList<ScannedEnum> result = EnumScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        ScannedEnum mixed = result.Single();
        mixed.Members.Should().HaveCount(4);
        mixed.Members[0].Should().BeEquivalentTo(new { Name = "A", Value = 5 });
        mixed.Members[1].Should().BeEquivalentTo(new { Name = "B", Value = 6 });
        mixed.Members[2].Should().BeEquivalentTo(new { Name = "C", Value = 10 });
        mixed.Members[3].Should().BeEquivalentTo(new { Name = "D", Value = 11 });
    }

    [Fact]
    public void Scan_WithCustomUnderlyingType_ParsesUnderlyingType()
    {
        (string projectRoot, string appPath) = CreateProjectWithEnum("ByteEnum.cs", @"
namespace AutoKassa.Models.Enums
{
    public enum ByteEnum : byte
    {
        One = 1,
        Two = 2
    }
}
");

        IReadOnlyList<ScannedEnum> result = EnumScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        result.Single().UnderlyingType.Should().Be("byte");
    }

    [Fact]
    public void Scan_WithNoEnumsDirectory_ReturnsEmptyList()
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        Directory.CreateDirectory(appPath);

        IReadOnlyList<ScannedEnum> result = EnumScanner.Scan(projectRoot, appPath);

        result.Should().BeEmpty();
    }

    private (string ProjectRoot, string AppPath) CreateProjectWithEnum(string fileName, string content)
    {
        string projectRoot = CreateTempDirectory();
        string enumsDir = Path.Combine(projectRoot, "AutoKassa", "Models", "Enums");
        Directory.CreateDirectory(enumsDir);
        File.WriteAllText(Path.Combine(enumsDir, fileName), content);
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
