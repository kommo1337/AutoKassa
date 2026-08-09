using System.Windows.Input;

namespace AutoKassa.Helpers
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;
        private EventHandler _canExecuteChanged;

        /// <summary>
        /// Конструктор команды
        /// </summary>
        /// <param name="execute">Действие, выполняемое командой</param>
        /// <param name="canExecute">Условие, при котором команда может выполняться</param>
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
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
            return _canExecute == null || _canExecute(parameter);
        }

        /// <summary>
        /// Выполнение команды
        /// </summary>
        public void Execute(object parameter)
        {
            _execute(parameter);
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
    /// Типизированная версия RelayCommand
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;
        private EventHandler _canExecuteChanged;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; _canExecuteChanged += value; }
            remove { CommandManager.RequerySuggested -= value; _canExecuteChanged -= value; }
        }

        public bool CanExecute(object parameter)
        {
            if (parameter == null && default(T) == null)
                return _canExecute == null || _canExecute(default);
            if (parameter is T typed)
                return _canExecute == null || _canExecute(typed);
            return _canExecute == null;
        }

        public void Execute(object parameter)
        {
            if (parameter is T typed)
                _execute(typed);
            else if (parameter == null && default(T) == null)
                _execute(default);
        }

        /// <summary>
        /// Принудительное обновление состояния CanExecute только этой команды
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            _canExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
