using System;
using System.Threading.Tasks;

namespace AutoKassa.Services.Dashboard
{
    /// <summary>
    /// Колбэки, которые ViewModel передаёт координатору операций дашборда.
    /// </summary>
    public record DashboardTransactionCallbacks(
        Func<Task> OnSaved,
        Func<Task> OnSavedKeepOpen,
        Action OnCancelled);
}
