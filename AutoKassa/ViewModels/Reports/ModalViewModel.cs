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
        /// </summary>
        public void Show(ViewModelBase content)
        {
            Content = content;
            IsOpen = true;
        }

        /// <summary>
        /// Закрыть модальное окно и очистить содержимое.
        /// </summary>
        public void Close()
        {
            IsOpen = false;
            Content = null!;
        }
    }
}
