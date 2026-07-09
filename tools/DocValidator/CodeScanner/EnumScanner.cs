using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoKassa.DocValidator.CodeScanner;

/// <summary>
/// Результат сканирования одного enum из исходного C#-файла.
/// </summary>
public sealed class ScannedEnum
{
    public string Name { get; set; } = string.Empty;

    public string File { get; set; } = string.Empty;

    public string UnderlyingType { get; set; } = "int";

    public List<ScannedEnumMember> Members { get; set; } = new();
}

/// <summary>
/// Один член enum.
/// </summary>
public sealed class ScannedEnumMember
{
    public string Name { get; set; } = string.Empty;

    public int Value { get; set; }
}

/// <summary>
/// Сканирует <c>AutoKassa/Models/Enums/*.cs</c> и извлекает объявления enum.
/// </summary>
public static class EnumScanner
{
    private static readonly Regex EnumRegex = new(
        @"public\s+enum\s+(?<name>\w+)\s*(?::\s*(?<underlying>\w+))?\s*\{(?<body>.*?)\}",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex MemberRegex = new(
        @"(?<name>\w+)\s*(?:=\s*(?<value>-?(?:0x)?[0-9a-fA-F]+))?",
        RegexOptions.Compiled);

    public static IReadOnlyList<ScannedEnum> Scan(ValidationContext context)
    {
        return Scan(context.ProjectRoot, context.AppPath);
    }

    public static IReadOnlyList<ScannedEnum> Scan(string projectRoot, string appPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectRoot);
        ArgumentException.ThrowIfNullOrEmpty(appPath);

        var result = new List<ScannedEnum>();
        string enumsDir = Path.Combine(appPath, "Models", "Enums");

        if (!Directory.Exists(enumsDir))
        {
            return result;
        }

        foreach (string file in Directory.EnumerateFiles(enumsDir, "*.cs"))
        {
            string content = File.ReadAllText(file);

            foreach (Match match in EnumRegex.Matches(content).Cast<Match>())
            {
                string name = match.Groups["name"].Value;
                string underlyingType = match.Groups["underlying"].Success
                    ? match.Groups["underlying"].Value
                    : "int";
                string body = match.Groups["body"].Value;

                List<ScannedEnumMember> members = ParseMembers(body);
                string relativeFile = Path.GetRelativePath(projectRoot, file)
                    .Replace(Path.DirectorySeparatorChar, '/');

                result.Add(new ScannedEnum
                {
                    Name = name,
                    File = relativeFile,
                    UnderlyingType = underlyingType,
                    Members = members,
                });
            }
        }

        return result;
    }

    private static List<ScannedEnumMember> ParseMembers(string body)
    {
        var members = new List<ScannedEnumMember>();
        int currentValue = 0;

        foreach (string rawLine in body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = rawLine.Trim();

            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            if (line.StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            // Убираем завершающую запятую, если она есть.
            if (line.EndsWith(",", StringComparison.Ordinal))
            {
                line = line.Substring(0, line.Length - 1).TrimEnd();
            }

            Match match = MemberRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            string memberName = match.Groups["name"].Value;

            if (match.Groups["value"].Success)
            {
                string valueText = match.Groups["value"].Value;
                currentValue = ParseInteger(valueText);
            }

            members.Add(new ScannedEnumMember
            {
                Name = memberName,
                Value = currentValue,
            });

            currentValue++;
        }

        return members;
    }

    private static int ParseInteger(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToInt32(text, 16);
        }

        return int.Parse(text, CultureInfo.InvariantCulture);
    }
}
