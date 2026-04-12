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
        private bool _isLoading;
        private bool _hasData;

        protected BaseReportViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;

            // Команды
            GenerateCommand = new RelayCommand(async _ => await GenerateReportAsync());
            ExportToPdfCommand = new RelayCommand(async _ =>
            {
                try { await ExportToPdfAsync(); }
                catch (Exception ex) { _dialogService.ShowError($"Ошибка экспорта в PDF: {ex.Message}"); }
            }, _ => HasData);
            ExportToExcelCommand = new RelayCommand(async _ =>
            {
                try { await ExportToExcelAsync(); }
                catch (Exception ex) { _dialogService.ShowError($"Ошибка экспорта в Excel: {ex.Message}"); }
            }, _ => HasData);
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
                _dialogService.ShowError($"Ошибка формирования отчета: {ex.Message}");
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
            _dialogService.ShowInfo("Экспорт в PDF будет реализован в следующем этапе");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Экспорт в Excel (переопределяется в наследниках)
        /// </summary>
        protected virtual Task ExportToExcelAsync()
        {
            _dialogService.ShowInfo("Экспорт в Excel будет реализован в следующем этапе");
            return Task.CompletedTask;
        }

        #endregion
    }
}