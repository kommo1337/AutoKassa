namespace AutoKassa.Services
{
    /// <summary>
    /// Сервис для уведомления о изменении данных в приложении.
    /// Используется для инвалидации кэшированных ViewModel.
    /// </summary>
    public interface IDataChangeService
    {
        /// <summary>
        /// Событие вызывается при изменении данных (транзакции, категории, настройки)
        /// </summary>
        event Action DataChanged;

        /// <summary>
        /// Уведомить всех подписчиков об изменении данных
        /// </summary>
        void NotifyDataChanged();
    }

    public class DataChangeService : IDataChangeService
    {
        public event Action DataChanged;

        public void NotifyDataChanged()
        {
            DataChanged?.Invoke();
        }
    }
}
