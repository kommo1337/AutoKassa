namespace AutoKassa.DocValidator.CodeScanner;

/// <summary>
/// Результат сканирования одного View/XAML-файла.
/// </summary>
public sealed class ScannedView
{
    public string File { get; set; } = string.Empty;
}

/// <summary>
/// Находит все XAML-файлы View в проекте <c>AutoKassa</c>.
/// </summary>
public static class ViewScanner
{
    public static IReadOnlyList<ScannedView> Scan(ValidationContext context)
    {
        return Scan(context.ProjectRoot, context.AppPath);
    }

    public static IReadOnlyList<ScannedView> Scan(string projectRoot, string appPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectRoot);
        ArgumentException.ThrowIfNullOrEmpty(appPath);

        var result = new List<ScannedView>();
        string viewsDir = Path.Combine(appPath, "Views");

        if (Directory.Exists(viewsDir))
        {
            foreach (string file in Directory.EnumerateFiles(viewsDir, "*.xaml", SearchOption.AllDirectories))
            {
                result.Add(CreateScannedView(projectRoot, file));
            }
        }

        string mainWindow = Path.Combine(appPath, "MainWindow.xaml");
        if (File.Exists(mainWindow))
        {
            result.Add(CreateScannedView(projectRoot, mainWindow));
        }

        return result;
    }

    private static ScannedView CreateScannedView(string projectRoot, string file)
    {
        string relativeFile = Path.GetRelativePath(projectRoot, file)
            .Replace(Path.DirectorySeparatorChar, '/');

        return new ScannedView { File = relativeFile };
    }
}
