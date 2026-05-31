using System.Threading.Tasks;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// Интерфейс для ViewModel, требующих асинхронной инициализации при первом показе.
    /// </summary>
    public interface IAsyncInitializable
    {
        /// <summary>
        /// Была ли выполнена инициализация.
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// Асинхронно инициализировать ViewModel (загрузить данные и т.д.).
        /// Безопасно вызывать многократно — повторные вызовы игнорируются.
        /// </summary>
        Task InitializeAsync();
    }
}
