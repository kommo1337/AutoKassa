namespace AutoKassa.Services
{
    /// <summary>
    /// Интерфейс сервиса блокировки приложения
    /// </summary>
    public interface ILockService
    {
        /// <summary>
        /// Заблокировать приложение
        /// </summary>
        void Lock();

        /// <summary>
        /// Проверить, заблокировано ли приложение
        /// </summary>
        bool IsLocked { get; }

        /// <summary>
        /// Запустить таймер автоблокировки
        /// </summary>
        void StartAutoLockTimer();

        /// <summary>
        /// Остановить таймер автоблокировки
        /// </summary>
        void StopAutoLockTimer();

        /// <summary>
        /// Сбросить таймер автоблокировки (при активности пользователя)
        /// </summary>
        void ResetAutoLockTimer();
    }
}