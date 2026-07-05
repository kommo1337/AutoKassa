using AutoKassa.Helpers;

namespace AutoKassa.ViewModels.Reports
{
    /// <summary>
    /// Универсальная ViewModel для модального оверлея отчётов.
    /// </summary>
    public class ModalViewModel : ViewModelBase
    {
        private bool _isOpen;
        private ViewModelBase _content;

        public ModalViewModel()
        {
            _content = null!;
        }

        /// <summary>
        /// Открыто ли модальное окно.
        /// </summary>
        public bool IsOpen
        {
            get => _isOpen;
            set => SetProperty(ref _isOpen, value);
        }

        /// <summary>
        /// Содержимое модального окна.
        /// </summary>
        public ViewModelBase Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        /// <summary>
        /// Показать указанное содержимое в модальном окне.
        /// При замене содержимого освобождается предыдущий ViewModel.
        /// </summary>
        public void Show(ViewModelBase content)
        {
            if (_content != null && _content != content)
            {
                (_content as IDisposable)?.Dispose();
            }

            Content = content;
            IsOpen = true;
        }

        /// <summary>
        /// Закрыть модальное окно и очистить содержимое.
        /// </summary>
        public void Close()
        {
            IsOpen = false;

            if (_content != null)
            {
                (_content as IDisposable)?.Dispose();
                Content = null!;
            }
        }

        /// <summary>
        /// Освободить текущее содержимое модального окна.
        /// </summary>
        protected override void OnDispose()
        {
            (_content as IDisposable)?.Dispose();
            base.OnDispose();
        }
    }
}
