using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using AutoKassa.Helpers;
using AutoKassa.Services;
using AutoKassa.ViewModels;

namespace AutoKassa.ViewModels.Reports
{
    /// <summary>
    /// Базовая ViewModel для всех отчетов
    /// </summary>
    public abstract class BaseReportViewModel : ViewModelBase
    {
        protected readonly IDialogService _dialogService;
        protected readonly IToastNotificationService _toastService;
        private CancellationTokenSource? _cts;
        private bool _isLoading;
        private bool _hasData = true;
        private bool _initialized;
        private bool _suppressRefresh;
        private string _activePeriodPreset;
        private bool _isModalOpen;
        private TransactionEditViewModel _editViewModel;

        private DispatcherTimer _refreshTimer;
        private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(50);

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
        /// Инициализировать VM и сформировать первый отчёт.
        /// Вызывать при первом отображении экрана, а не в конструкторе.
        /// </summary>
        public virtual Task InitializeAsync()
        {
            _initialized = true;
            return GenerateReportAsync();
        }

        /// <summary>
        /// Автоматически перегенерировать отчёт при изменении фильтра.
        /// Не срабатывает до вызова InitializeAsync() или внутри BatchUpdate.
        /// С задержкой 400 мс (debounce) для защиты от лавинообразных перестроений.
        /// </summary>
        protected void AutoRefresh()
        {
            if (_initialized && !_suppressRefresh)
            {
                if (_refreshTimer == null)
                {
                    _refreshTimer = new DispatcherTimer(DebounceInterval, DispatcherPriority.Background,
                        (s, e) =>
                        {
                            _refreshTimer?.Stop();
                            var newCts = new CancellationTokenSource();
                            var oldCts = Interlocked.Exchange(ref _cts, newCts);
                            oldCts?.Cancel();
                            oldCts?.Dispose();
                            RunAsync(GenerateReportAsync);
                        }, Dispatcher.CurrentDispatcher);
                }
                _refreshTimer.Stop();
                _refreshTimer.Start();
            }
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
        /// Открыто ли модальное окно редактирования операции
        /// </summary>
        public bool IsModalOpen
        {
            get => _isModalOpen;
            set => SetProperty(ref _isModalOpen, value);
        }

        /// <summary>
        /// ViewModel модального окна редактирования операции
        /// </summary>
        public TransactionEditViewModel EditViewModel
        {
            get => _editViewModel;
            set => SetProperty(ref _editViewModel, value);
        }

        /// <summary>
        /// Название отчета
        /// </summary>
        public abstract string ReportName { get; }

        /// <summary>
        /// Код активного пресета периода ("Today", "Week", "Month", "Quarter", "Year") либо null
        /// для произвольного диапазона. Используется для подсветки кнопок-чипов.
        /// </summary>
        public string ActivePeriodPreset
        {
            get => _activePeriodPreset;
            set
            {
                if (SetProperty(ref _activePeriodPreset, value))
                {
                    OnPropertyChanged(nameof(IsPresetToday));
                    OnPropertyChanged(nameof(IsPresetWeek));
                    OnPropertyChanged(nameof(IsPresetMonth));
                    OnPropertyChanged(nameof(IsPresetQuarter));
                    OnPropertyChanged(nameof(IsPresetYear));
                }
            }
        }

        public bool IsPresetToday   => _activePeriodPreset == "Today";
        public bool IsPresetWeek    => _activePeriodPreset == "Week";
        public bool IsPresetMonth   => _activePeriodPreset == "Month";
        public bool IsPresetQuarter => _activePeriodPreset == "Quarter";
        public bool IsPresetYear    => _activePeriodPreset == "Year";

        /// <summary>
        /// Вызывать из сеттеров DateFrom/DateTo: сбрасывает пресет, если пользователь
        /// меняет дату вручную (а не через BatchUpdate внутри SetPeriod).
        /// </summary>
        protected void OnDateChangedByUser()
        {
            if (!_suppressRefresh)
                ActivePeriodPreset = null;
        }

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
        protected override void OnDispose()
        {
            _refreshTimer?.Stop();
            var cts = Interlocked.Exchange(ref _cts, null);
            cts?.Cancel();
            cts?.Dispose();
            base.OnDispose();
        }

        protected async Task GenerateReportAsync()
        {
            var cts = Interlocked.CompareExchange(ref _cts, null, null) ?? new CancellationTokenSource();
            var ct = cts.Token;

            try
            {
                IsLoading = true;

                await LoadDataAsync(ct);

                HasData = CheckHasData();
            }
            catch (OperationCanceledException)
            {
                // Фильтр изменился — игнорируем устаревший запрос
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
        /// Проверить, есть ли данные для отображения после загрузки.
        /// Переопределяется в наследниках для корректного показа плейсхолдера.
        /// </summary>
        protected virtual bool CheckHasData() => true;

        /// <summary>
        /// Загрузить данные отчета (переопределяется в наследниках)
        /// </summary>
        protected abstract Task LoadDataAsync(CancellationToken ct = default);

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