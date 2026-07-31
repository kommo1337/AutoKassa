namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Результат работы одного валидатора.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    /// Некритичные замечания: не влияют на <see cref="IsValid"/>, но выводятся в отчёт.
    /// </summary>
    public IReadOnlyList<string> Warnings => _warnings;

    public bool IsValid => _errors.Count == 0;

    public int ErrorCount => _errors.Count;

    public int WarningCount => _warnings.Count;

    public void AddError(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        _errors.Add(message);
    }

    public void AddErrors(IEnumerable<string> messages)
    {
        foreach (string message in messages)
        {
            AddError(message);
        }
    }

    public void AddWarning(string message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
        _warnings.Add(message);
    }

    public static ValidationResult Success() => new();

    public static ValidationResult WithError(string message)
    {
        var result = new ValidationResult();
        result.AddError(message);
        return result;
    }
}
