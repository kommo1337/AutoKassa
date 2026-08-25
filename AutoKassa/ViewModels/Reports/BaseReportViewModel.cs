using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using AutoKassa.Helpers;
using AutoKassa.Models.Enums;
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
        private DateTime _dateFrom;
        private DateTime _dateTo;
        private PaymentType? _selectedPaymentType;

        /// <summary>
        /// Был ли отчёт инициализирован (InitializeAsync вызывался)
        /// </summary>
        public bool IsInitialized => _initialized;

        /// <summary>
        /// Сбросить флаг инициализации, чтобы при следующем показе отчёт перегенерировался
        /// </summary>
        public void Invalidate()
        {
            _initialized = false;
        }
        private bool _suppressRefresh;
        private string _activePeriodPreset;

        private DispatcherTimer _refreshTimer;
        private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(50);

        protected BaseReportViewModel(IDialogService dialogService, IToastNotificationService toastService)
        {
            _dialogService = dialogService;
            _toastService = toastService;
            Modal = new ModalViewModel();

            // По умолчанию — текущий месяц (через поля, чтобы не триггерить AutoRefresh)
            _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo = DateTime.Now.Date;
            _activePeriodPreset = "Month";

            // Команды
            GenerateCommand = new AsyncRelayCommand(GenerateReportAsync);
            ExportToPdfCommand = new AsyncRelayCommand(async () =>
            {
                try { await ExportToPdfAsync(); }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Ошибка экспорта отчёта в PDF");
                    _toastService.ShowError("Не удалось экспортировать отчёт в PDF");
                }
            }, () => HasData);
            ExportToExcelCommand = new AsyncRelayCommand(async () =>
            {
                try { await ExportToExcelAsync(); }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Ошибка экспорта отчёта в Excel");
                    _toastService.ShowError("Не удалось экспортировать отчёт в Excel");
                }
            }, () => HasData);

            SetPeriodCommand = new RelayCommand<string>(period => SetPeriod(period));

            // Команды фильтра типа оплаты
            SetPaymentAllCommand        = new RelayCommand(_ => SelectedPaymentType = null);
            SetPaymentCashCommand       = new RelayCommand(_ => SelectedPaymentType = PaymentType.Cash);
            SetPaymentNonCashCommand    = new RelayCommand(_ => SelectedPaymentType = PaymentType.NonCash);
            SetPaymentCreditCardCommand = new RelayCommand(_ => SelectedPaymentType = PaymentType.CreditCard);
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
                        OnRefreshTimerTick, Dispatcher.CurrentDispatcher);
                }
                _refreshTimer.Stop();
                _refreshTimer.Start();
            }
        }

        /// <summary>
        /// Обработчик тика таймера автообновления отчёта.
        /// </summary>
        private void OnRefreshTimerTick(object? sender, EventArgs e)
        {
            _refreshTimer?.Stop();
            var newCts = new CancellationTokenSource();
            var oldCts = Interlocked.Exchange(ref _cts, newCts);
            oldCts?.Cancel();
            oldCts?.Dispose();
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
        /// Универсальная модель модального оверлея отчёта.
        /// </summary>
        public ModalViewModel Modal { get; }

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

        /// <summary>
        /// Дата начала периода
        /// </summary>
        public DateTime DateFrom
        {
            get => _dateFrom;
            set
            {
                if (SetProperty(ref _dateFrom, value))
                {
                    OnDateChangedByUser();
                    ValidateDateRange();
                    AutoRefresh();
                }
            }
        }

        /// <summary>
        /// Дата окончания периода
        /// </summary>
        public DateTime DateTo
        {
            get => _dateTo;
            set
            {
                if (SetProperty(ref _dateTo, value))
                {
                    OnDateChangedByUser();
                    ValidateDateRange();
                    AutoRefresh();
                }
            }
        }

        /// <summary>
        /// Фильтр типа оплаты (null — все типы)
        /// </summary>
        public PaymentType? SelectedPaymentType
        {
            get => _selectedPaymentType;
            set
            {
                if (SetProperty(ref _selectedPaymentType, value))
                {
                    OnPropertyChanged(nameof(IsPaymentAll));
                    OnPropertyChanged(nameof(IsPaymentCash));
                    OnPropertyChanged(nameof(IsPaymentNonCash));
                    OnPropertyChanged(nameof(IsPaymentCreditCard));
                    AutoRefresh();
                }
            }
        }

        public bool IsPaymentAll        => !SelectedPaymentType.HasValue;
        public bool IsPaymentCash       => SelectedPaymentType == PaymentType.Cash;
        public bool IsPaymentNonCash    => SelectedPaymentType == PaymentType.NonCash;
        public bool IsPaymentCreditCard => SelectedPaymentType == PaymentType.CreditCard;

        #endregion

        #region Команды

        public ICommand GenerateCommand { get; }
        public ICommand ExportToPdfCommand { get; }
        public ICommand ExportToExcelCommand { get; }
        public ICommand SetPeriodCommand { get; }
        public ICommand SetPaymentAllCommand        { get; }
        public ICommand SetPaymentCashCommand       { get; }
        public ICommand SetPaymentNonCashCommand    { get; }
        public ICommand SetPaymentCreditCardCommand { get; }

        #endregion

        #region Методы

        /// <summary>
        /// Сформировать отчет
        /// </summary>
        protected override void OnDispose()
        {
            if (_refreshTimer != null)
            {
                _refreshTimer.Tick -= OnRefreshTimerTick;
                _refreshTimer.Stop();
            }
            var cts = Interlocked.Exchange(ref _cts, null);
            cts?.Cancel();
            cts?.Dispose();
            Modal?.Dispose();
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
                Serilog.Log.Error(ex, "Ошибка формирования отчёта");
                _toastService.ShowError("Не удалось сформировать отчёт");
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
        /// Валидация диапазона дат: «с» не может быть позже «по»
        /// </summary>
        protected void ValidateDateRange()
        {
            if (DateFrom > DateTo)
                DateTo = DateFrom;
        }

        /// <summary>
        /// Быстрые фильтры периодов ("Today", "Week", "Month", "Quarter", "Year")
        /// </summary>
        public void SetPeriod(string period)
        {
            BatchUpdate(() =>
            {
                var (from, to) = PeriodHelper.GetDateRange(period);
                DateFrom = from;
                DateTo = to;
            });
            ActivePeriodPreset = period;
        }

        /// <summary>
        /// Показать toast с кнопкой «Открыть» после успешного экспорта отчёта
        /// </summary>
        protected void ShowExportedToast(string filePath, string formatName)
        {
            _toastService.ShowWithAction($"Отчёт {formatName} сохранён", "Открыть", () =>
            {
                if (!FileLauncher.OpenExportedFile(filePath))
                    _toastService.ShowError("Не удалось открыть файл. Проверьте, что файл существует и имеет допустимый формат.");
            });
        }

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