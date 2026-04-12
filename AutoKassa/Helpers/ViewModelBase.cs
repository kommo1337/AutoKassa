using Serilog;
using System.Collections;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoKassa.Helpers
{
    public abstract class ViewModelBase : INotifyPropertyChanged, INotifyDataErrorInfo
    {
        private static readonly ILogger _log = Log.ForContext<ViewModelBase>();

        /// <summary>
        /// Запускает асинхронную операцию без блокировки конструктора/UI.
        /// Перехватывает и логирует исключения вместо silent fail.
        /// </summary>
        protected void RunAsync(Func<Task> action)
        {
            action().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Log.ForContext(GetType()).Error(
                        t.Exception?.GetBaseException(),
                        "Необработанная ошибка в асинхронной операции [{ViewModel}]",
                        GetType().Name);
            }, System.Threading.Tasks.TaskScheduler.Default);
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        #region INotifyDataErrorInfo

        private readonly Dictionary<string, List<string>> _errors = new();

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        public bool HasErrors => _errors.Count > 0;

        public IEnumerable GetErrors(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
                return _errors.Values.SelectMany(e => e);
            return _errors.TryGetValue(propertyName, out var list) ? list : null;
        }

        /// <summary>
        /// Установить ошибки для свойства. Пустой список или null — очищает ошибки.
        /// </summary>
        protected void SetErrors(string propertyName, IEnumerable<string> errors)
        {
            var list = errors?.Where(e => !string.IsNullOrEmpty(e)).ToList();
            if (list == null || list.Count == 0)
                _errors.Remove(propertyName);
            else
                _errors[propertyName] = list;

            OnPropertyChanged(nameof(HasErrors));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Очистить ошибки для свойства (или все ошибки, если propertyName == null)
        /// </summary>
        protected void ClearErrors([CallerMemberName] string propertyName = null)
        {
            if (propertyName == null)
                _errors.Clear();
            else
                _errors.Remove(propertyName);

            OnPropertyChanged(nameof(HasErrors));
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Первая ошибка для свойства, или null если ошибок нет
        /// </summary>
        protected string GetFirstError(string propertyName) =>
            _errors.TryGetValue(propertyName, out var list) ? list.FirstOrDefault() : null;

        #endregion
    }
}
