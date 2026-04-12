using System;
using System.Threading.Tasks;
using System.Windows.Input;
using AutoKassa.Helpers;
using AutoKassa.Services;

namespace AutoKassa.ViewModels.Reports
{
    /// <summary>
    /// Базовая ViewModel для всех отчетов
    /// </summary>
    public abstract class BaseReportViewModel : ViewModelBase
    {
        protected readonly IDialogService _dialogService;
        protected readonly IToastNotificationService _toastService;
        private bool _isLoading;
        private bool _hasData;
        private bool _initialized;
        private bool _suppressRefresh;

        protected BaseReportViewModel(IDialogService dialogService, IToastNotificationService toastService)
        {
            _dialogService = dialogService;
            _toastService = toastService;

            // Команды
            GenerateCommand = new RelayCommand(async _ => await GenerateReportAsync());
            ExportToPdfCommand = new RelayCommand(async _ =>
            {
                try { await ExportToPdfAsync(); }
                catch (Exception ex) { _toastService.ShowError($"Ошибка экспорта в PDF: {ex.Message}"); }
            }, _ => HasData);
            ExportToExcelCommand = new RelayCommand(async _ =>
            {
                try { await ExportToExcelAsync(); }
                catch (Exception ex) { _toastService.ShowError($"Ошибка экспорта в Excel: {ex.Message}"); }
            }, _ => HasData);
        }

        /// <summary>
        /// Помечает VM как инициализированную и запускает первый отчёт.
        /// Вызывать в конце конструктора наследника.
        /// </summary>
        protected void MarkInitialized()
        {
            _initialized = true;
            RunAsync(GenerateReportAsync);
        }

        /// <summary>
        /// Автоматически перегенерировать отчёт при изменении фильтра.
        /// Не срабатывает до вызова MarkInitialized() или внутри BatchUpdate.
        /// </summary>
        protected void AutoRefresh()
        {
            if (_initialized && !_suppressRefresh)
                RunAsync(GenerateReportAsync);
        }

        /// <summary>
        /// Выполнить несколько изменений фильтров с одной перегенерацией в конце.
        /// </summary>
        protected void BatchUpdate(Action changes)
        {
            _suppressRefresh = true;
            try { changes(); }
            finally { _suppressRefresh = false; }
            AutoRefresh();
        }

        #region Свойства

        /// <summary>
        /// Идет загрузка данных
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Есть ли данные для отображения
        /// </summary>
        public bool HasData
        {
            get => _hasData;
            set => SetProperty(ref _hasData, value);
        }

        /// <summary>
        /// Название отчета
        /// </summary>
        public abstract string ReportName { get; }

        #endregion

        #region Команды

        public ICommand GenerateCommand { get; }
        public ICommand ExportToPdfCommand { get; }
        public ICommand ExportToExcelCommand { get; }

        #endregion

        #region Методы

        /// <summary>
        /// Сформировать отчет
        /// </summary>
        protected async Task GenerateReportAsync()
        {
            try
            {
                IsLoading = true;
                HasData = false;

                await LoadDataAsync();

                HasData = true;
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ошибка формирования отчета: {ex.Message}");
                HasData = false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Загрузить данные отчета (переопределяется в наследниках)
        /// </summary>
        protected abstract Task LoadDataAsync();

        /// <summary>
        /// Экспорт в PDF (переопределяется в наследниках)
        /// </summary>
        protected virtual Task ExportToPdfAsync()
        {
            _toastService.ShowInfo("Экспорт в PDF будет реализован в следующем этапе");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Экспорт в Excel (переопределяется в наследниках)
        /// </summary>
        protected virtual Task ExportToExcelAsync()
        {
            _toastService.ShowInfo("Экспорт в Excel будет реализован в следующем этапе");
            return Task.CompletedTask;
        }

        #endregion
    }
}