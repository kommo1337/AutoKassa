using Serilog;
using System.Windows.Input;

namespace AutoKassa.Helpers
{
    /// <summary>
    /// Асинхронная команда без параметра.
    /// Оборачивает <see cref="Func{Task}"/>, блокирует повторный запуск и логирует исключения.
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private static readonly ILogger _log = Log.ForContext<AsyncRelayCommand>();

        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _isExecuting;
        private EventHandler _canExecuteChanged;

        /// <summary>
        /// Создаёт асинхронную команду
        /// </summary>
        /// <param name="execute">Асинхронное действие</param>
        /// <param name="canExecute">Условие выполнения</param>
        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Событие изменения возможности выполнения команды
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; _canExecuteChanged += value; }
            remove { CommandManager.RequerySuggested -= value; _canExecuteChanged -= value; }
        }

        /// <summary>
        /// Проверка возможности выполнения команды
        /// </summary>
        public bool CanExecute(object parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }

        /// <summary>
        /// Выполнение команды
        /// </summary>
        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Ошибка выполнения асинхронной команды");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Принудительное обновление состояния CanExecute только этой команды
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            _canExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Асинхронная команда с параметром.
    /// </summary>
    public class AsyncRelayCommand<T> : ICommand
    {
        private static readonly ILogger _log = Log.ForContext<AsyncRelayCommand<T>>();

        private readonly Func<T, Task> _execute;
        private readonly Func<T, bool> _canExecute;
        private bool _isExecuting;
        private EventHandler _canExecuteChanged;

        /// <summary>
        /// Создаёт типизированную асинхронную команду
        /// </summary>
        public AsyncRelayCommand(Func<T, Task> execute, Func<T, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// Событие изменения возможности выполнения команды
        /// </summary>
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; _canExecuteChanged += value; }
            remove { CommandManager.RequerySuggested -= value; _canExecuteChanged -= value; }
        }

        /// <summary>
        /// Проверка возможности выполнения команды
        /// </summary>
        public bool CanExecute(object parameter)
        {
            if (_isExecuting)
                return false;

            var value = GetParameterValue(parameter);
            return _canExecute?.Invoke(value) ?? true;
        }

        /// <summary>
        /// Выполнение команды
        /// </summary>
        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            var value = GetParameterValue(parameter);

            _isExecuting = true;
            RaiseCanExecuteChanged();

            try
            {
                await _execute(value);
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Ошибка выполнения типизированной асинхронной команды");
            }
            finally
            {
                _isExecuting = false;
                RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// Принудительное обновление состояния CanExecute только этой команды
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            _canExecuteChanged?.Invoke(this, EventArgs.Empty);
        }

        private static T GetParameterValue(object parameter)
        {
            if (parameter == null && default(T) == null)
                return default;

            if (parameter is T value)
                return value;

            return default;
        }
    }
}
