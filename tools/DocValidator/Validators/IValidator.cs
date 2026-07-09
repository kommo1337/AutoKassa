namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Проверяет соответствие части исходного кода и JSON-справочника.
/// </summary>
public interface IValidator
{
    string Name { get; }

    ValidationResult Validate(ValidationContext context);
}
