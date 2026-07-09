using AutoKassa.DocValidator.CodeScanner;
using AutoKassa.DocValidator.JsonCatalog.JsonModels;

namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Сверяет lifetime DI-регистраций из <c>App.xaml.cs</c> со справочником <c>services.json</c>.
/// </summary>
public sealed class DiLifetimeValidator : IValidator
{
    public string Name => "DiLifetimeValidator";

    public ValidationResult Validate(ValidationContext context)
    {
        var result = new ValidationResult();
        IReadOnlyList<ScannedDiRegistration> registrations = DiRegistrationScanner.Scan(context);
        List<ServiceEntry> catalogEntries = context.Services.Services;

        HashSet<string> viewModelNames = context.ViewModels.ViewModels
            .Where(vm => !string.IsNullOrEmpty(vm.Name))
            .Select(vm => vm.Name!)
            .ToHashSet(StringComparer.Ordinal);

        var registrationsByImplementation = registrations
            .GroupBy(r => r.NormalizedImplementation, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var registrationsByServiceType = registrations
            .GroupBy(r => r.NormalizedServiceType, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        // Прямая проверка: каждая запись services.json должна соответствовать реальной DI-регистрации.
        foreach (ServiceEntry entry in catalogEntries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                result.AddError("services.json contains a service entry without a name.");
                continue;
            }

            string entryLifetime = entry.Lifetime ?? string.Empty;
            if (string.Equals(entryLifetime, "static", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string? implementationName = NormalizeTypeName(entry.Implementation) ?? NormalizeTypeName(entry.Name);
            if (string.IsNullOrEmpty(implementationName))
            {
                result.AddError($"Service '{entry.Name}' has no implementation or name to match against DI.");
                continue;
            }

            if (!registrationsByImplementation.TryGetValue(implementationName, out List<ScannedDiRegistration>? implRegistrations))
            {
                result.AddError($"Service '{entry.Name}' (implementation '{implementationName}') is not registered in App.xaml.cs.");
                continue;
            }

            ScannedDiRegistration? matchingRegistration = implRegistrations
                .FirstOrDefault(r => LifetimeMatches(entryLifetime, r.Lifetime));

            if (matchingRegistration is null)
            {
                string actualLifetimes = string.Join(", ", implRegistrations.Select(r => r.Lifetime).Distinct());
                result.AddError($"Service '{entry.Name}' lifetime mismatch. JSON: '{entryLifetime}', DI: '{actualLifetimes}'.");
                continue;
            }

            // Если указан интерфейс — проверяем, что он тоже зарегистрирован для этой реализации.
            if (!string.IsNullOrEmpty(entry.Interface))
            {
                foreach (string interfaceToken in SplitInterfaceTokens(entry.Interface))
                {
                    string? interfaceName = NormalizeTypeName(interfaceToken);
                    if (string.IsNullOrEmpty(interfaceName))
                    {
                        continue;
                    }

                    // DbContext — базовый класс AppDbContext, не отдельная DI-регистрация.
                    if (interfaceName.Equals("DbContext", StringComparison.OrdinalIgnoreCase)
                        && implementationName.Equals("AppDbContext", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // IDbContextFactory<AppDbContext> — регистрируется отдельно через AddDbContextFactory.
                    if (interfaceName.StartsWith("IDbContextFactory<", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!registrationsByServiceType.ContainsKey(interfaceName))
                        {
                            result.AddError($"Service '{entry.Name}': factory interface '{interfaceToken}' is not registered in DI.");
                        }

                        continue;
                    }

                    if (!registrationsByServiceType.TryGetValue(interfaceName, out List<ScannedDiRegistration>? interfaceRegistrations)
                        || !interfaceRegistrations.Any(r => string.Equals(r.NormalizedImplementation, implementationName, StringComparison.OrdinalIgnoreCase)))
                    {
                        result.AddError($"Service '{entry.Name}': interface '{interfaceToken}' is not registered for implementation '{implementationName}'.");
                    }
                }
            }
        }

        // Обратная проверка: каждая DI-регистрация (кроме VM, View и DbContextFactory) должна быть в services.json.
        HashSet<string> catalogNames = catalogEntries
            .SelectMany(e => new[] { NormalizeTypeName(e.Name), NormalizeTypeName(e.Implementation) }.Where(n => !string.IsNullOrEmpty(n)))
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (ScannedDiRegistration registration in registrations)
        {
            if (IsViewModelRegistration(registration, viewModelNames))
            {
                continue;
            }

            if (IsViewRegistration(registration))
            {
                continue;
            }

            if (IsDbContextFactory(registration))
            {
                continue;
            }

            string implName = registration.NormalizedImplementation;
            string serviceName = registration.NormalizedServiceType;

            if (!catalogNames.Contains(implName) && !catalogNames.Contains(serviceName))
            {
                result.AddError($"DI registration '{registration.ServiceType}' (lifetime {registration.Lifetime}) has no entry in services.json.");
            }
        }

        return result;
    }

    private static bool IsViewModelRegistration(ScannedDiRegistration registration, HashSet<string> viewModelNames)
    {
        // Саморегистрация VM: ServiceType и Implementation совпадают и имя есть в каталоге VM.
        return viewModelNames.Contains(registration.NormalizedImplementation);
    }

    private static bool IsViewRegistration(ScannedDiRegistration registration)
    {
        string name = registration.NormalizedServiceType;
        return name.EndsWith("View", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("Window", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDbContextFactory(ScannedDiRegistration registration)
    {
        return registration.NormalizedServiceType.StartsWith("IDbContextFactory<", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LifetimeMatches(string catalogLifetime, string registrationLifetime)
    {
        if (string.Equals(catalogLifetime, registrationLifetime, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Поддержка составных lifetime-строк, например "Transient (DbContext) / Singleton (Factory)".
        return catalogLifetime.Contains(registrationLifetime, StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeTypeName(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return null;
        }

        string trimmed = typeName!.Trim();
        int lastDot = trimmed.LastIndexOf('.');
        string name = lastDot >= 0 ? trimmed.Substring(lastDot + 1) : trimmed;

        // Убираем пробелы внутри generic-аргументов.
        if (name.Contains('<'))
        {
            name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", string.Empty);
        }

        return name;
    }

    private static IEnumerable<string> SplitInterfaceTokens(string? interfaceValue)
    {
        if (string.IsNullOrWhiteSpace(interfaceValue))
        {
            yield break;
        }

        foreach (string part in interfaceValue!.Split(new[] { '/', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string token = part.Trim();
            if (!string.IsNullOrEmpty(token))
            {
                yield return token;
            }
        }
    }
}
