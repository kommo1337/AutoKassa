using AutoKassa.DocValidator;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;
using AutoKassa.DocValidator.Validators;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.Validators;

public class DiLifetimeValidatorTests : IDisposable
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
    public void Validate_WithMatchingLifetimes_Passes()
    {
        ValidationContext context = CreateContext(
            @"
            services.AddSingleton<IPasswordService, PasswordService>();
            services.AddTransient<ITransactionService, TransactionService>();
            ",
            new[]
            {
                new ServiceEntry
                {
                    Name = "PasswordService",
                    Interface = "IPasswordService",
                    Implementation = "PasswordService",
                    Lifetime = "Singleton",
                },
                new ServiceEntry
                {
                    Name = "TransactionService",
                    Interface = "ITransactionService",
                    Implementation = "TransactionService",
                    Lifetime = "Transient",
                },
            });

        var validator = new DiLifetimeValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithLifetimeMismatch_Fails()
    {
        ValidationContext context = CreateContext(
            @"services.AddSingleton<IPasswordService, PasswordService>();",
            new[]
            {
                new ServiceEntry
                {
                    Name = "PasswordService",
                    Interface = "IPasswordService",
                    Implementation = "PasswordService",
                    Lifetime = "Transient",
                },
            });

        var validator = new DiLifetimeValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("PasswordService") && e.Contains("lifetime mismatch"));
    }

    [Fact]
    public void Validate_WithStaticService_SkipsRegistrationCheck()
    {
        ValidationContext context = CreateContext(
            @"// no DI registration for static helper",
            new[]
            {
                new ServiceEntry
                {
                    Name = "CreditCardOperationHelper",
                    Interface = null,
                    Implementation = "CreditCardOperationHelper",
                    Lifetime = "static",
                },
            });

        var validator = new DiLifetimeValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithMissingDiRegistration_Fails()
    {
        ValidationContext context = CreateContext(
            @"// service not registered",
            new[]
            {
                new ServiceEntry
                {
                    Name = "MissingService",
                    Interface = "IMissingService",
                    Implementation = "MissingService",
                    Lifetime = "Singleton",
                },
            });

        var validator = new DiLifetimeValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("MissingService") && e.Contains("not registered in App.xaml.cs"));
    }

    [Fact]
    public void Validate_WithUnregisteredDiService_Fails()
    {
        ValidationContext context = CreateContext(
            @"services.AddSingleton<IGhostService, GhostService>();",
            new[]
            {
                new ServiceEntry
                {
                    Name = "PasswordService",
                    Interface = "IPasswordService",
                    Implementation = "PasswordService",
                    Lifetime = "Singleton",
                },
            });

        var validator = new DiLifetimeValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("GhostService") && e.Contains("no entry in services.json"));
    }

    [Fact]
    public void Validate_WithViewModelRegistration_SkipsReverseCheck()
    {
        ValidationContext context = CreateContext(
            @"services.AddTransient<DashboardViewModel>();",
            Array.Empty<ServiceEntry>(),
            new[]
            {
                new ViewModelEntry
                {
                    Name = "DashboardViewModel",
                    RegisteredInDi = true,
                },
            });

        var validator = new DiLifetimeValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithDbContext_AcceptsCompositeLifetime()
    {
        ValidationContext context = CreateContext(
            @"
            services.AddDbContext<AppDbContext>(ServiceLifetime.Transient);
            services.AddDbContextFactory<AppDbContext>();
            ",
            new[]
            {
                new ServiceEntry
                {
                    Name = "AppDbContext",
                    Interface = "DbContext / IDbContextFactory<AppDbContext>",
                    Implementation = "AppDbContext",
                    Lifetime = "Transient (DbContext) / Singleton (IDbContextFactory<AppDbContext>)",
                },
            });

        var validator = new DiLifetimeValidator();
        ValidationResult result = validator.Validate(context);

        result.IsValid.Should().BeTrue();
    }

    private ValidationContext CreateContext(
        string configureServicesBody,
        ServiceEntry[] serviceEntries,
        ViewModelEntry[]? viewModels = null)
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        string docsPath = Path.Combine(projectRoot, "docs", "kimi", "json");
        Directory.CreateDirectory(appPath);
        Directory.CreateDirectory(docsPath);

        string appXamlCs = $@"
using Microsoft.Extensions.DependencyInjection;

namespace AutoKassa
{{
    public partial class App
    {{
        private void ConfigureServices(IServiceCollection services)
        {{
            {configureServicesBody}
        }}
    }}
}}
";
        File.WriteAllText(Path.Combine(appPath, "App.xaml.cs"), appXamlCs);

        var context = new ValidationContext(projectRoot, docsPath, appPath)
        {
            Services = new ServicesCatalog { Services = serviceEntries.ToList() },
            ViewModels = new ViewModelsCatalog { ViewModels = (viewModels ?? Array.Empty<ViewModelEntry>()).ToList() },
        };

        return context;
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }
}
