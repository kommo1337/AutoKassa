using AutoKassa.DocValidator;
using AutoKassa.DocValidator.Validators;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.Validators;

public class DbContextInjectionValidatorTests : IDisposable
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
    public void Validate_WithFactoryInjection_Passes()
    {
        ValidationContext context = CreateContext(
            ("Services/ReportService.cs", @"
public class ReportService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public ReportService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }
}"));

        var validator = new DbContextInjectionValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithDirectDbContextInjection_Fails()
    {
        ValidationContext context = CreateContext(
            ("Services/LegacyService.cs", @"
public class LegacyService
{
    public LegacyService(AppDbContext context)
    {
    }
}"));

        var validator = new DbContextInjectionValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("LegacyService") && e.Contains("AppDbContext"));
    }

    [Fact]
    public void Validate_WithMultilineConstructor_Fails()
    {
        ValidationContext context = CreateContext(
            ("Services/MultilineService.cs", @"
public class MultilineService
{
    public MultilineService(
        ISettingsService settingsService,
        AppDbContext context,
        IToastNotificationService toastService)
    {
    }
}"));

        var validator = new DbContextInjectionValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("MultilineService"));
    }

    [Fact]
    public void Validate_WithDbContextMethodParameter_Passes()
    {
        // Приватные методы-помощники, принимающие контекст параметром, разрешены:
        // контекст им передаётся из метода, создавшего его через фабрику.
        ValidationContext context = CreateContext(
            ("Services/HelperService.cs", @"
public class HelperService
{
    private readonly IDbContextFactory<AppDbContext> _contextFactory;

    public HelperService(IDbContextFactory<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private async Task<decimal> GetDebtAsync(AppDbContext context, int id)
    {
        return 0m;
    }
}"));

        var validator = new DbContextInjectionValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithDbContextOptionsConstructor_Passes()
    {
        // Сам AppDbContext получает DbContextOptions<AppDbContext> — это не нарушение.
        ValidationContext context = CreateContext(
            ("Services/AppDbContext.cs", @"
public class AppDbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
    {
    }
}"));

        var validator = new DbContextInjectionValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_FilesUnderObj_AreIgnored()
    {
        ValidationContext context = CreateContext(
            ("obj/Debug/net10.0-windows/GeneratedService.cs", @"
public class GeneratedService
{
    public GeneratedService(AppDbContext context)
    {
    }
}"));

        var validator = new DbContextInjectionValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    private ValidationContext CreateContext(params (string RelativePath, string Content)[] files)
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        string docsPath = Path.Combine(projectRoot, "docs", "kimi", "json");
        Directory.CreateDirectory(appPath);
        Directory.CreateDirectory(docsPath);

        foreach ((string relativePath, string content) in files)
        {
            string fullPath = Path.Combine(appPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, content);
        }

        return new ValidationContext(projectRoot, docsPath, appPath);
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }
}
