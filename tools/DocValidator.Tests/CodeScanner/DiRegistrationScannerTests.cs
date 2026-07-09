using AutoKassa.DocValidator.CodeScanner;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.CodeScanner;

public class DiRegistrationScannerTests : IDisposable
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
    public void Scan_WithInterfaceAndImplementation_ParsesRegistration()
    {
        (string projectRoot, string appPath) = CreateProjectWithAppXamlCs(@"
            services.AddSingleton<INavigationService, AutoKassa.Services.NavigationService>();
            services.AddTransient<ITransactionService, TransactionService>();
        ");

        IReadOnlyList<ScannedDiRegistration> result = DiRegistrationScanner.Scan(appPath);

        result.Should().Contain(r =>
            r.NormalizedServiceType == "INavigationService" &&
            r.NormalizedImplementation == "NavigationService" &&
            r.Lifetime == "Singleton");

        result.Should().Contain(r =>
            r.NormalizedServiceType == "ITransactionService" &&
            r.NormalizedImplementation == "TransactionService" &&
            r.Lifetime == "Transient");
    }

    [Fact]
    public void Scan_WithSelfRegistration_ParsesRegistration()
    {
        (string projectRoot, string appPath) = CreateProjectWithAppXamlCs(@"
            services.AddTransient<QuickAddViewModel>();
            services.AddSingleton<MainWindow>(provider => new MainWindow());
        ");

        IReadOnlyList<ScannedDiRegistration> result = DiRegistrationScanner.Scan(appPath);

        result.Should().Contain(r =>
            r.NormalizedServiceType == "QuickAddViewModel" &&
            r.NormalizedImplementation == "QuickAddViewModel" &&
            r.Lifetime == "Transient");

        result.Should().Contain(r =>
            r.NormalizedServiceType == "MainWindow" &&
            r.NormalizedImplementation == "MainWindow" &&
            r.Lifetime == "Singleton");
    }

    [Fact]
    public void Scan_WithDbContext_ParsesLifetime()
    {
        (string projectRoot, string appPath) = CreateProjectWithAppXamlCs(@"
            services.AddDbContext<AppDbContext>(ServiceLifetime.Transient);
            services.AddDbContextFactory<AppDbContext>();
        ");

        IReadOnlyList<ScannedDiRegistration> result = DiRegistrationScanner.Scan(appPath);

        result.Should().Contain(r =>
            r.NormalizedServiceType == "AppDbContext" &&
            r.Lifetime == "Transient");

        result.Should().Contain(r =>
            r.NormalizedServiceType == "IDbContextFactory<AppDbContext>" &&
            r.Lifetime == "Singleton");
    }

    [Fact]
    public void Scan_WithComments_IgnoresCommentedRegistrations()
    {
        (string projectRoot, string appPath) = CreateProjectWithAppXamlCs(@"
            // services.AddSingleton<IGhostService, GhostService>();
            services.AddSingleton<ILockService, LockService>();
            /*
            services.AddTransient<HiddenService>();
            */
        ");

        IReadOnlyList<ScannedDiRegistration> result = DiRegistrationScanner.Scan(appPath);

        result.Should().ContainSingle();
        result.Single().NormalizedServiceType.Should().Be("ILockService");
    }

    [Fact]
    public void Scan_WithNoAppXamlCs_ReturnsEmptyList()
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        Directory.CreateDirectory(appPath);

        IReadOnlyList<ScannedDiRegistration> result = DiRegistrationScanner.Scan(appPath);

        result.Should().BeEmpty();
    }

    private (string ProjectRoot, string AppPath) CreateProjectWithAppXamlCs(string configureServicesBody)
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        Directory.CreateDirectory(appPath);

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
        return (projectRoot, appPath);
    }

    private string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        _tempDirectories.Add(path);
        return path;
    }
}
