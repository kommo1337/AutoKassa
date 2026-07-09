using AutoKassa.DocValidator.CodeScanner;
using FluentAssertions;
using Xunit;

namespace AutoKassa.DocValidator.Tests.CodeScanner;

public class ViewScannerTests : IDisposable
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
    public void Scan_FindsViewsInViewsDirectoryAndMainWindow()
    {
        (string projectRoot, string appPath) = CreateProjectWithViews();

        IReadOnlyList<ScannedView> result = ViewScanner.Scan(projectRoot, appPath);

        result.Select(v => v.File).Should().BeEquivalentTo(new[]
        {
            "AutoKassa/Views/DashboardView.xaml",
            "AutoKassa/Views/Reports/BalanceReportView.xaml",
            "AutoKassa/MainWindow.xaml"
        });
    }

    [Fact]
    public void Scan_WithMissingViewsDirectory_ReturnsMainWindowOnly()
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        Directory.CreateDirectory(appPath);
        File.WriteAllText(Path.Combine(appPath, "MainWindow.xaml"), "<Window/>");

        IReadOnlyList<ScannedView> result = ViewScanner.Scan(projectRoot, appPath);

        result.Should().ContainSingle();
        result.Single().File.Should().Be("AutoKassa/MainWindow.xaml");
    }

    private (string ProjectRoot, string AppPath) CreateProjectWithViews()
    {
        string projectRoot = CreateTempDirectory();
        string appPath = Path.Combine(projectRoot, "AutoKassa");
        string viewsDir = Path.Combine(appPath, "Views");
        string reportsDir = Path.Combine(viewsDir, "Reports");
        Directory.CreateDirectory(reportsDir);

        File.WriteAllText(Path.Combine(viewsDir, "DashboardView.xaml"), "<UserControl/>");
        File.WriteAllText(Path.Combine(reportsDir, "BalanceReportView.xaml"), "<UserControl/>");
        File.WriteAllText(Path.Combine(appPath, "MainWindow.xaml"), "<Window/>");

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
