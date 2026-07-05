using AutoKassa.Services;

namespace AutoKassa.Tests.Infrastructure
{
    /// <summary>
    /// Фейк IDataChangeService для тестов ViewModel, не требующих реальной шины событий.
    /// </summary>
    public class FakeDataChangeService : IDataChangeService
    {
        public event Action? DataChanged;

        public void NotifyDataChanged()
        {
            DataChanged?.Invoke();
        }
    }
}
