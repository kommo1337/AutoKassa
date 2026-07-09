namespace AutoKassa.DocValidator.Validators;

/// <summary>
/// Результат работы одного валидатора.
/// </summary>
public sealed class ValidationResult
{
    private readonly List<string> _errors = new();

    public IReadOnlyList<string> Errors => _errors;

    public bool IsValid => _errors.Count == 0;

    public int ErrorCount => _errors.Count;

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

    public static ValidationResult Success() => new();

    public static ValidationResult WithError(string message)
    {
        var result = new ValidationResult();
        result.AddError(message);
        return result;
    }
}
