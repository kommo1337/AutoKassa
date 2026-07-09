using AutoKassa.DocValidator.CodeScanner;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.CodeScanner;

public class TrapMarkdownParserTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (string file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public void Scan_ExtractsCategoriesAndTitles()
    {
        string path = CreateMarkdownFile(
            "## 1. Архитектурные запреты\n" +
            "### 1.1. Не пиши бизнес-логику в code-behind View\n" +
            "### 1.2. Не используй статический Service Locator\n" +
            "\n" +
            "## 2. Бизнес-правила-ловушки\n" +
            "### 2.1. Только soft delete\n");

        IReadOnlyList<ScannedTrap> result = TrapMarkdownParser.Scan(path);

        result.Should().HaveCount(3);
        result[0].Category.Should().Be("Архитектурные запреты");
        result[0].Title.Should().Be("Не пиши бизнес-логику в code-behind View");
        result[1].Category.Should().Be("Архитектурные запреты");
        result[1].Title.Should().Be("Не используй статический Service Locator");
        result[2].Category.Should().Be("Бизнес-правила-ловушки");
        result[2].Title.Should().Be("Только soft delete");
    }

    [Fact]
    public void Scan_KeepsMarkdownFormattingInTitle()
    {
        string path = CreateMarkdownFile(
            "## 1. Category\n" +
            "### 1.1. `PaymentType.Debt` не влияет на баланс\n");

        IReadOnlyList<ScannedTrap> result = TrapMarkdownParser.Scan(path);

        result.Should().ContainSingle();
        result[0].Title.Should().Be("`PaymentType.Debt` не влияет на баланс");
    }

    [Fact]
    public void Scan_WithoutNumericPrefix_ReturnsTitleAsIs()
    {
        string path = CreateMarkdownFile(
            "## Category\n" +
            "### Plain title without prefix\n");

        IReadOnlyList<ScannedTrap> result = TrapMarkdownParser.Scan(path);

        result.Should().ContainSingle();
        result[0].Category.Should().Be("Category");
        result[0].Title.Should().Be("Plain title without prefix");
    }

    [Fact]
    public void Scan_WithMissingFile_ThrowsFileNotFoundException()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".md");

        Action act = () => TrapMarkdownParser.Scan(path);

        act.Should().Throw<FileNotFoundException>();
    }

    private string CreateMarkdownFile(string content)
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".md");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }
}
