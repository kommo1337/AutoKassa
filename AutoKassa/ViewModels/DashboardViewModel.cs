using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для главного экрана (Dashboard)
    /// </summary>
    public class DashboardViewModel : ViewModelBase, INavigationAware
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly ISettingsService _settingsService;
        private readonly IDataChangeService _dataChangeService;
        private bool _isInitialized;
        private bool _needsRefresh;

        #region Поля

        private CancellationTokenSource _loadCts;

        // Сводка
        private decimal _totalIncome;
        private decimal _totalExpense;
        private decimal _profit;
        private decimal _incomeChangePercent;
        private decimal _expenseChangePercent;
        private decimal _profitChangePercent;
        private decimal _profitability;

        // Период
        private PeriodType _selectedPeriod = PeriodType.Month;
        private DateTime _dateFrom;
        private DateTime _dateTo;
        private bool _isPeriodPickerOpen;

        // Последние операции
        private ObservableCollection<Transaction> _recentTransactions;
        private Transaction _selectedTransaction;
        private bool _hasTransactions;

        // График
        private PlotModel _chartModel;
        private bool _isLoading;

        // Сгруппированные операции и панели
        private ObservableCollection<DateGroup> _groupedTransactions;
        private ObservableCollection<CategorySummaryItem> _topExpenses;
        private ObservableCollection<CategorySummaryItem> _topIncomes;

        // Debounce навигации — не запускаем загрузку, если пользователь быстро ушёл
        private CancellationTokenSource _navigateCts;
        private int _statsCount;
        private int _statsDays;
        private decimal _statsAvgExpense;
        private decimal _statsAvgIncome;
        private decimal _todayBalance;
        private bool _hasTodayTransactions;
        private decimal _currentCashBalance;

        // Разбивка по типу оплаты
        private decimal _cashBalance;
        private decimal _nonCashBalance;
        private decimal _cashIncome;
        private decimal _nonCashIncome;
        private decimal _cashExpense;
        private decimal _nonCashExpense;
        private decimal _cashProfit;
        private decimal _nonCashProfit;
        private string _periodLabel = "ЗА МЕСЯЦ";

        // Быстрое добавление (субVM)
        private QuickAddViewModel _quickAdd;

        // Модальное окно
        private bool _isModalOpen;
        private TransactionEditViewModel _editViewModel;

        // Кэш графика — не пересоздаём модель, если данные не изменились
        private PlotModel? _cachedChartModel;
        private (DateTime From, DateTime To, decimal InitialBalance) _cachedChartKey;

        #endregion

        public DashboardViewModel(
            ITransactionService transactionService,
            ICategoryService categoryService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            ISettingsService settingsService,
            IDataChangeService dataChangeService)
        {
            _transactionService = transactionService;
            _categoryService = categoryService;
            _dialogService = dialogService;
            _toastService = toastService;
            _settingsService = settingsService;
            _dataChangeService = dataChangeService;

            RecentTransactions = new ObservableCollection<Transaction>();
            GroupedTransactions = new ObservableCollection<DateGroup>();
            TopExpenses = new ObservableCollection<CategorySummaryItem>();
            TopIncomes = new ObservableCollection<CategorySummaryItem>();

            QuickAdd = new QuickAddViewModel(transactionService, categoryService, dialogService, toastService);
            QuickAdd.OnTransactionAdded = async () =>
            {
                await LoadDataAsync();
                _dataChangeService?.NotifyDataChanged();
            };

            // Команды периода
            SelectPeriodCommand = new RelayCommand(period => SelectPeriod((PeriodType)period));
            ApplyCustomPeriodCommand = new RelayCommand(async _ => await ApplyCustomPeriodAsync());
            TogglePeriodPickerCommand = new RelayCommand(_ => IsPeriodPickerOpen = !IsPeriodPickerOpen);
            OpenAddTransactionCommand = new RelayCommand(_ => OpenAddTransaction());
            RefreshCommand = new RelayCommand(_ => RunAsync(LoadDataAsync));
            CloseModalCommand = new RelayCommand(_ => { if (IsModalOpen) IsModalOpen = false; });

            // Команды операций (параметр = конкретная транзакция или null → используем SelectedTransaction)
            OpenTransactionCommand = new RelayCommand(
                t => OpenTransaction(t as Transaction ?? SelectedTransaction),
                t => (t as Transaction ?? SelectedTransaction) != null);
            EditTransactionCommand = new RelayCommand(
                t => OpenTransaction(t as Transaction ?? SelectedTransaction),
                t => (t as Transaction ?? SelectedTransaction) != null);
            DeleteTransactionCommand = new RelayCommand(async t => await DeleteTransactionAsync(t as Transaction ?? SelectedTransaction));
            NavigateToAllTransactionsCommand = new RelayCommand(_ => NavigateToAllTransactions());
            OpenFullReportCommand = new RelayCommand(_ => OpenFullReport());

            // Установка начального периода
            SetPeriodDates(PeriodType.Month);

            _dataChangeService.DataChanged += OnDataChanged;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized || _needsRefresh)
            {
                _needsRefresh = false;
                _isInitialized = true;

                var cts = new CancellationTokenSource();
                var old = Interlocked.Exchange(ref _navigateCts, cts);
                old?.Cancel();
                old?.Dispose();

                RunAsync(async () =>
                {
                    try { await Task.Delay(50, cts.Token); }
                    catch (OperationCanceledException) { return; }
                    await InitializeAsync();
                });
            }
        }

        public void OnNavigatedFrom() { }

        private void OnDataChanged()
        {
            _needsRefresh = true;
        }

        #region Свойства сводки

        /// <summary>
        /// Общий доход за период
        /// </summary>
        public decimal TotalIncome
        {
            get => _totalIncome;
            set => SetProperty(ref _totalIncome, value);
        }

        /// <summary>
        /// Общие расходы за период
        /// </summary>
        public decimal TotalExpense
        {
            get => _totalExpense;
            set => SetProperty(ref _totalExpense, value);
        }

        /// <summary>
        /// Прибыль (доходы - расходы)
        /// </summary>
        public decimal Profit
        {
            get => _profit;
            set => SetProperty(ref _profit, value);
        }

        /// <summary>
        /// Изменение доходов относительно предыдущего периода (%)
        /// </summary>
        public decimal IncomeChangePercent
        {
            get => _incomeChangePercent;
            set => SetProperty(ref _incomeChangePercent, value);
        }

        /// <summary>
        /// Изменение расходов относительно предыдущего периода (%)
        /// </summary>
        public decimal ExpenseChangePercent
        {
            get => _expenseChangePercent;
            set => SetProperty(ref _expenseChangePercent, value);
        }

        /// <summary>
        /// Изменение прибыли относительно предыдущего периода (%)
        /// </summary>
        public decimal ProfitChangePercent
        {
            get => _profitChangePercent;
            set => SetProperty(ref _profitChangePercent, value);
        }

        /// <summary>
        /// Рентабельность (%)
        /// </summary>
        public decimal Profitability
        {
            get => _profitability;
            set => SetProperty(ref _profitability, value);
        }

        // Разбивка по типу оплаты
        public decimal CashBalance
        {
            get => _cashBalance;
            set => SetProperty(ref _cashBalance, value);
        }

        public decimal NonCashBalance
        {
            get => _nonCashBalance;
            set => SetProperty(ref _nonCashBalance, value);
        }

        public decimal CashIncome
        {
            get => _cashIncome;
            set => SetProperty(ref _cashIncome, value);
        }

        public decimal NonCashIncome
        {
            get => _nonCashIncome;
            set => SetProperty(ref _nonCashIncome, value);
        }

        public decimal CashExpense
        {
            get => _cashExpense;
            set => SetProperty(ref _cashExpense, value);
        }

        public decimal NonCashExpense
        {
            get => _nonCashExpense;
            set => SetProperty(ref _nonCashExpense, value);
        }

        public decimal CashProfit
        {
            get => _cashProfit;
            set => SetProperty(ref _cashProfit, value);
        }

        public decimal NonCashProfit
        {
            get => _nonCashProfit;
            set => SetProperty(ref _nonCashProfit, value);
        }

        public string PeriodLabel
        {
            get => _periodLabel;
            set => SetProperty(ref _periodLabel, value);
        }

        #endregion

        #region Свойства периода

        /// <summary>
        /// Выбранный период
        /// </summary>
        public PeriodType SelectedPeriod
        {
            get => _selectedPeriod;
            set
            {
                if (SetProperty(ref _selectedPeriod, value))
                {
                    OnPropertyChanged(nameof(IsTodaySelected));
                    OnPropertyChanged(nameof(IsWeekSelected));
                    OnPropertyChanged(nameof(IsMonthSelected));
                    OnPropertyChanged(nameof(IsYearSelected));
                    OnPropertyChanged(nameof(IsAllTimeSelected));
                    OnPropertyChanged(nameof(IsCustomSelected));
                }
            }
        }

        public bool IsTodaySelected => SelectedPeriod == PeriodType.Today;
        public bool IsWeekSelected => SelectedPeriod == PeriodType.Week;
        public bool IsMonthSelected => SelectedPeriod == PeriodType.Month;
        public bool IsYearSelected => SelectedPeriod == PeriodType.Year;
        public bool IsAllTimeSelected => SelectedPeriod == PeriodType.AllTime;
        public bool IsCustomSelected => SelectedPeriod == PeriodType.Custom;

        /// <summary>
        /// Дата начала периода
        /// </summary>
        public DateTime DateFrom
        {
            get => _dateFrom;
            set => SetProperty(ref _dateFrom, value);
        }

        /// <summary>
        /// Дата окончания периода
        /// </summary>
        public DateTime DateTo
        {
            get => _dateTo;
            set => SetProperty(ref _dateTo, value);
        }

        /// <summary>
        /// Открыт ли Popup выбора периода
        /// </summary>
        public bool IsPeriodPickerOpen
        {
            get => _isPeriodPickerOpen;
            set => SetProperty(ref _isPeriodPickerOpen, value);
        }

        /// <summary>
        /// Текст кнопки выбранного периода (для кнопки как в транзакциях)
        /// </summary>
        public string PeriodButtonLabel => SelectedPeriod switch
        {
            PeriodType.Today => "Сегодня",
            PeriodType.Week => "Неделя",
            PeriodType.Month => "Месяц",
            PeriodType.Year => "Год",
            PeriodType.AllTime => "Всё время",
            PeriodType.Custom => "Период",
            _ => "Период"
        };

        #endregion

        #region Свойства быстрого добавления

        /// <summary>
        /// СубVM для быстрого добавления операции
        /// </summary>
        public QuickAddViewModel QuickAdd
        {
            get => _quickAdd;
            set => SetProperty(ref _quickAdd, value);
        }

        #endregion

        #region Свойства последних операций

        /// <summary>
        /// Последние операции
        /// </summary>
        public ObservableCollection<Transaction> RecentTransactions
        {
            get => _recentTransactions;
            set => SetProperty(ref _recentTransactions, value);
        }

        /// <summary>
        /// Выбранная операция
        /// </summary>
        public Transaction SelectedTransaction
        {
            get => _selectedTransaction;
            set => SetProperty(ref _selectedTransaction, value);
        }

        /// <summary>
        /// Есть ли операции
        /// </summary>
        public bool HasTransactions
        {
            get => _hasTransactions;
            set => SetProperty(ref _hasTransactions, value);
        }

        #endregion

        #region Свойства графика

        /// <summary>
        /// Модель графика OxyPlot
        /// </summary>
        public PlotModel ChartModel
        {
            get => _chartModel;
            set => SetProperty(ref _chartModel, value);
        }

        /// <summary>
        /// Идет загрузка
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        #endregion

        #region Свойства группировки и статистики

        public ObservableCollection<DateGroup> GroupedTransactions
        {
            get => _groupedTransactions;
            set => SetProperty(ref _groupedTransactions, value);
        }

        public ObservableCollection<CategorySummaryItem> TopExpenses
        {
            get => _topExpenses;
            set => SetProperty(ref _topExpenses, value);
        }

        public ObservableCollection<CategorySummaryItem> TopIncomes
        {
            get => _topIncomes;
            set => SetProperty(ref _topIncomes, value);
        }

        public int StatsCount
        {
            get => _statsCount;
            set => SetProperty(ref _statsCount, value);
        }

        public int StatsDays
        {
            get => _statsDays;
            set => SetProperty(ref _statsDays, value);
        }

        public decimal StatsAvgExpense
        {
            get => _statsAvgExpense;
            set => SetProperty(ref _statsAvgExpense, value);
        }

        public decimal StatsAvgIncome
        {
            get => _statsAvgIncome;
            set => SetProperty(ref _statsAvgIncome, value);
        }

        public decimal TodayBalance
        {
            get => _todayBalance;
            set
            {
                if (SetProperty(ref _todayBalance, value))
                    OnPropertyChanged(nameof(TodayBalanceFormatted));
            }
        }

        public bool HasTodayTransactions
        {
            get => _hasTodayTransactions;
            set => SetProperty(ref _hasTodayTransactions, value);
        }

        /// <summary>
        /// Текущий остаток в кассе: начальный баланс + все доходы за всё время - все расходы за всё время
        /// </summary>
        public decimal CurrentCashBalance
        {
            get => _currentCashBalance;
            set => SetProperty(ref _currentCashBalance, value);
        }

        public bool IsModalOpen
        {
            get => _isModalOpen;
            set => SetProperty(ref _isModalOpen, value);
        }

        public TransactionEditViewModel EditViewModel
        {
            get => _editViewModel;
            set => SetProperty(ref _editViewModel, value);
        }

        public string TodayBalanceFormatted =>
            TodayBalance > 0 ? $"+{TodayBalance:N0} ₽" :
            TodayBalance < 0 ? $"{TodayBalance:N0} ₽" : "0 ₽";

        #endregion

        #region Команды

        public ICommand SelectPeriodCommand { get; }
        public ICommand ApplyCustomPeriodCommand { get; }
        public ICommand TogglePeriodPickerCommand { get; }
        public ICommand OpenAddTransactionCommand { get; }
        public ICommand RefreshCommand { get; }
        public ICommand CloseModalCommand { get; }
        public ICommand OpenTransactionCommand { get; }
        public ICommand EditTransactionCommand { get; }
        public ICommand DeleteTransactionCommand { get; }
        public ICommand NavigateToAllTransactionsCommand { get; }
        public ICommand OpenFullReportCommand { get; }

        #endregion

        #region Методы

        /// <summary>
        /// Инициализация данных
        /// </summary>
        private async Task InitializeAsync()
        {
            await QuickAdd.InitializeAsync();
            await LoadDataAsync();
        }

        /// <summary>
        /// Загрузка всех данных Dashboard
        /// </summary>
        private async Task LoadDataAsync()
        {
            // Отменяем предыдущую загрузку атомарно — защита от гонки при быстром
            // переключении периода (двойной Dispose / ObjectDisposedException).
            var newCts = new CancellationTokenSource();
            var oldCts = System.Threading.Interlocked.Exchange(ref _loadCts, newCts);
            oldCts?.Cancel();
            oldCts?.Dispose();
            var ct = newCts.Token;

            try
            {
                IsLoading = true;
                await Task.WhenAll(
                    LoadSummaryAsync(ct),
                    LoadRecentTransactionsAsync(ct),
                    LoadChartDataAsync(ct),
                    LoadTodayBalanceAsync(ct),
                    LoadCashBalanceAsync(ct));
            }
            catch (OperationCanceledException) { /* пользователь сменил период — игнорируем */ }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки данных: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Загрузка сводки за период — SQL-агрегация вместо загрузки всех записей
        /// </summary>
        private async Task LoadSummaryAsync(CancellationToken ct = default)
        {
            var allTask  = _transactionService.GetPeriodTotalsAsync(DateFrom, DateTo, ct: ct);
            var cashTask = _transactionService.GetPeriodTotalsAsync(DateFrom, DateTo, PaymentType.Cash, ct);
            var cardTask = _transactionService.GetPeriodTotalsAsync(DateFrom, DateTo, PaymentType.NonCash, ct);
            await Task.WhenAll(allTask, cashTask, cardTask);

            var (income, expense, _, _) = await allTask;
            TotalIncome  = income;
            TotalExpense = expense;
            Profit       = income - expense;
            Profitability = income > 0 ? ((income - expense) / income) * 100 : 0;

            var (ci, ce, _, _) = await cashTask;
            CashIncome = ci; CashExpense = ce;
            CashProfit = ci - ce;
            var (ni, ne, _, _) = await cardTask;
            NonCashIncome = ni; NonCashExpense = ne;
            NonCashProfit = ni - ne;

            await CalculateChangesAsync(ct);
        }

        /// <summary>
        /// Расчет изменений относительно предыдущего периода
        /// </summary>
        private async Task CalculateChangesAsync(CancellationToken ct = default)
        {
            var periodLength = (DateTo - DateFrom).Days + 1;
            var prevDateTo   = DateFrom.AddDays(-1);
            var prevDateFrom = prevDateTo.AddDays(-periodLength + 1);

            var (prevIncome, prevExpense, _, _) =
                await _transactionService.GetPeriodTotalsAsync(prevDateFrom, prevDateTo, ct: ct);
            var prevProfit = prevIncome - prevExpense;

            IncomeChangePercent  = prevIncome  > 0 ? ((TotalIncome  - prevIncome)  / prevIncome)             * 100 : (TotalIncome  > 0 ? 100 : 0);
            ExpenseChangePercent = prevExpense > 0 ? ((TotalExpense - prevExpense) / prevExpense)             * 100 : (TotalExpense > 0 ? 100 : 0);
            ProfitChangePercent  = prevProfit != 0 ? ((Profit       - prevProfit)  / Math.Abs(prevProfit))   * 100 : (Profit       != 0 ? 100 : 0);
        }

        /// <summary>
        /// Загрузка последних операций — список (Take=100) + агрегация для статистики и топ-категорий
        /// </summary>
        private async Task LoadRecentTransactionsAsync(CancellationToken ct = default)
        {
            // Последние 100 записей для ленты и группировки
            var filters = new TransactionFilterParameters
            {
                DateFrom = DateFrom,
                DateTo   = DateTo,
                Take     = 100
            };
            var list = await _transactionService.GetTransactionsAsync(filters, ct);

            // Обновляем коллекции in-place вместо пересоздания — это убирает полную перестройку UI
            RecentTransactions.Clear();
            foreach (var t in list)
                RecentTransactions.Add(t);
            HasTransactions = RecentTransactions.Count > 0;

            // Группировка по дням — выполняем в пуле потоков, чтобы не блокировать UI
            var grouped = await Task.Run(() =>
            {
                return list
                    .GroupBy(t => t.Date.Date)
                    .OrderByDescending(g => g.Key)
                    .Select(g => new DateGroup
                    {
                        Date     = g.Key,
                        DayTotal = g.Sum(t => t.Type == OperationType.Income ? t.Amount : -t.Amount),
                        Items    = new ObservableCollection<Transaction>(g.OrderByDescending(t => t.CreatedAt))
                    })
                    .ToList();
            }, ct);

            GroupedTransactions.Clear();
            foreach (var g in grouped)
                GroupedTransactions.Add(g);

            // Статистика — SQL-агрегация вместо in-memory по всем записям
            var totalsTask   = _transactionService.GetPeriodTotalsAsync(DateFrom, DateTo, ct: ct);
            var dailyTask    = _transactionService.GetDailyTotalsAsync(DateFrom, DateTo, ct: ct);
            var topExpTask   = _transactionService.GetTopCategoriesAsync(DateFrom, DateTo, OperationType.Expense, 5, ct: ct);
            var topIncTask   = _transactionService.GetTopCategoriesAsync(DateFrom, DateTo, OperationType.Income,  5, ct: ct);

            await Task.WhenAll(totalsTask, dailyTask, topExpTask, topIncTask);

            var (_, _, incCount, expCount) = await totalsTask;
            var dailyGroups = await dailyTask;

            StatsCount      = await _transactionService.GetTotalCountAsync(new TransactionFilterParameters { DateFrom = DateFrom, DateTo = DateTo, Take = 1 }, ct);
            StatsDays       = dailyGroups.Count;
            StatsAvgExpense = expCount > 0 ? TotalExpense / expCount : 0;
            StatsAvgIncome  = incCount > 0 ? TotalIncome  / incCount : 0;

            UpdateCollection(TopExpenses,
                (await topExpTask).Select(x => new CategorySummaryItem { Name = x.Name, Total = x.Total }));
            UpdateCollection(TopIncomes,
                (await topIncTask).Select(x => new CategorySummaryItem { Name = x.Name, Total = x.Total }));
        }

        /// <summary>
        /// Обновляет ObservableCollection in-place: Clear + Add.
        /// Это избегает полной перестройки визуального дерева списка.
        /// </summary>
        private static void UpdateCollection<T>(ObservableCollection<T> collection, IEnumerable<T> items)
        {
            collection.Clear();
            foreach (var item in items)
                collection.Add(item);
        }

        /// <summary>
        /// Загрузка баланса за сегодня — SQL-агрегация
        /// </summary>
        private async Task LoadTodayBalanceAsync(CancellationToken ct = default)
        {
            var (income, expense, incCount, expCount) =
                await _transactionService.GetPeriodTotalsAsync(DateTime.Today, DateTime.Today, ct: ct);
            HasTodayTransactions = (incCount + expCount) > 0;
            TodayBalance = income - expense;
        }

        /// <summary>
        /// Загрузка актуального остатка в кассе — начальный баланс + все доходы - все расходы за всё время
        /// </summary>
        private async Task LoadCashBalanceAsync(CancellationToken ct = default)
        {
            var settings = await _settingsService.GetSettingsAsync();
            var epoch = new DateTime(2000, 1, 1);

            var allTask  = _transactionService.GetPeriodTotalsAsync(epoch, DateTime.Today, ct: ct);
            var cashTask = _transactionService.GetPeriodTotalsAsync(epoch, DateTime.Today, PaymentType.Cash, ct);
            var cardTask = _transactionService.GetPeriodTotalsAsync(epoch, DateTime.Today, PaymentType.NonCash, ct);
            await Task.WhenAll(allTask, cashTask, cardTask);

            var (allIncome, allExpense, _, _) = await allTask;
            CurrentCashBalance = settings.InitialBalance + allIncome - allExpense;

            var (cashInc, cashExp, _, _) = await cashTask;
            CashBalance = settings.InitialBalance + cashInc - cashExp;

            var (cardInc, cardExp, _, _) = await cardTask;
            NonCashBalance = cardInc - cardExp;
        }

        /// <summary>
        /// Загрузка данных для графика — SQL-агрегация по дням.
        /// Модель кэшируется по ключу (период + начальный баланс), чтобы не перерисовывать
        /// OxyPlot при каждом переключении на Dashboard без изменения данных.
        /// </summary>
        private async Task LoadChartDataAsync(CancellationToken ct = default)
        {
            var groupedData = await _transactionService.GetDailyTotalsAsync(DateFrom, DateTo, ct: ct);
            var chartSettings = await _settingsService.GetSettingsAsync();

            var key = (DateFrom, DateTo, chartSettings.InitialBalance);
            if (_cachedChartModel != null && _cachedChartKey.Equals(key))
                return;

            // Построение модели графика в пуле потоков, чтобы не блокировать UI
            var model = await Task.Run(() => BuildChartModel(groupedData, chartSettings), ct);

            _cachedChartModel = model;
            _cachedChartKey = key;
            ChartModel = model;
        }

        private static PlotModel BuildChartModel(List<DailyTotalsItem> groupedData, AppSettings chartSettings)
        {
            var model = new PlotModel
            {
                PlotAreaBorderThickness = new OxyThickness(0),
                Padding = new OxyThickness(0),
                PlotMargins = new OxyThickness(0)
            };

            // Настройка осей (скрытых)
            var dateAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                IsAxisVisible = false,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None
            };

            var valueAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                IsAxisVisible = false,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None
            };

            model.Axes.Add(dateAxis);
            model.Axes.Add(valueAxis);

            // Линия доходов
            var incomeSeries = new LineSeries
            {
                Title = "Доходы",
                Color = OxyColor.Parse("#4CAF50"),
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };

            // Линия расходов
            var expenseSeries = new LineSeries
            {
                Title = "Расходы",
                Color = OxyColor.Parse("#F44336"),
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };

            // Линия прибыли
            var profitSeries = new LineSeries
            {
                Title = "Прибыль",
                Color = OxyColor.Parse("#2196F3"),
                StrokeThickness = 2,
                MarkerType = MarkerType.None
            };

            // Накопительный баланс
            decimal runningIncome = 0;
            decimal runningExpense = 0;
            decimal initialBalance = chartSettings.InitialBalance;

            foreach (var data in groupedData)
            {
                runningIncome += data.Income;
                runningExpense += data.Expense;
                var runningProfit = initialBalance + runningIncome - runningExpense;

                var dateValue = DateTimeAxis.ToDouble(data.Date);
                incomeSeries.Points.Add(new DataPoint(dateValue, (double)runningIncome));
                expenseSeries.Points.Add(new DataPoint(dateValue, (double)runningExpense));
                profitSeries.Points.Add(new DataPoint(dateValue, (double)runningProfit));
            }

            model.Series.Add(incomeSeries);
            model.Series.Add(expenseSeries);
            model.Series.Add(profitSeries);

            return model;
        }

        /// <summary>
        /// Выбор периода
        /// </summary>
        private void SelectPeriod(PeriodType period)
        {
            SelectedPeriod = period;
            SetPeriodDates(period);
            IsPeriodPickerOpen = false;

            if (period != PeriodType.Custom)
            {
                RunAsync(LoadDataAsync);
            }
        }

        private async Task ApplyCustomPeriodAsync()
        {
            SelectedPeriod = PeriodType.Custom;
            IsPeriodPickerOpen = false;
            await LoadDataAsync();
        }

        /// <summary>
        /// Установка дат периода
        /// </summary>
        private void SetPeriodDates(PeriodType period)
        {
            switch (period)
            {
                case PeriodType.Today:
                    (DateFrom, DateTo) = PeriodHelper.GetDateRange("Today");
                    PeriodLabel = "ЗА СЕГОДНЯ";
                    break;

                case PeriodType.Week:
                    (DateFrom, DateTo) = PeriodHelper.GetDateRange("Week");
                    PeriodLabel = "ЗА НЕДЕЛЮ";
                    break;

                case PeriodType.Month:
                    (DateFrom, DateTo) = PeriodHelper.GetDateRange("Month");
                    PeriodLabel = "ЗА МЕСЯЦ";
                    break;

                case PeriodType.Year:
                    (DateFrom, DateTo) = PeriodHelper.GetDateRange("Year");
                    PeriodLabel = "ЗА ГОД";
                    break;

                case PeriodType.AllTime:
                    DateFrom = new DateTime(2000, 1, 1);
                    DateTo = DateTime.Today;
                    PeriodLabel = "ЗА ВСЁ ВРЕМЯ";
                    break;

                case PeriodType.Custom:
                    PeriodLabel = "ЗА ПЕРИОД";
                    break;
            }
            OnPropertyChanged(nameof(PeriodButtonLabel));
        }

        /// <summary>
        /// Открыть диалог добавления новой операции
        /// </summary>
        private void OpenAddTransaction()
        {
            var vm = new TransactionEditViewModel(_transactionService, _categoryService, _dialogService, _settingsService, _toastService);
            vm.InitializeForAdd();
            vm.OnSaved = () => { IsModalOpen = false; RunAsync(LoadDataAsync); _dataChangeService?.NotifyDataChanged(); };
            vm.OnSavedKeepOpen = () => { RunAsync(LoadDataAsync); _dataChangeService?.NotifyDataChanged(); };
            vm.OnCancelled = () => { IsModalOpen = false; };
            EditViewModel = vm;
            IsModalOpen = true;
        }

        /// <summary>
        /// Открыть операцию для редактирования
        /// </summary>
        private void OpenTransaction(Transaction transaction = null)
        {
            var t = transaction ?? SelectedTransaction;
            if (t == null) return;

            var vm = new TransactionEditViewModel(_transactionService, _categoryService, _dialogService, _settingsService, _toastService);
            vm.InitializeForEdit(t);
            vm.OnSaved = () => { IsModalOpen = false; RunAsync(LoadDataAsync); _dataChangeService?.NotifyDataChanged(); };
            vm.OnSavedKeepOpen = () => { RunAsync(LoadDataAsync); _dataChangeService?.NotifyDataChanged(); };
            vm.OnCancelled = () => { IsModalOpen = false; };
            EditViewModel = vm;
            IsModalOpen = true;
        }

        /// <summary>
        /// Удалить операцию
        /// </summary>
        private async Task DeleteTransactionAsync(Transaction transaction = null)
        {
            var t = transaction ?? SelectedTransaction;
            if (t == null) return;

            var deletedId = t.Id;

            try
            {
                await _transactionService.DeleteAsync(deletedId);
                RecentTransactions.Remove(t);
                HasTransactions = RecentTransactions.Any();

                _toastService.ShowDeleteWithUndo(
                    "Операция удалена",
                    async () =>
                    {
                        await _transactionService.RestoreAsync(deletedId);
                        await LoadDataAsync();
                    });

                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ошибка удаления: {ex.Message}");
            }
        }

        /// <summary>
        /// Переход к списку всех операций
        /// </summary>
        private void NavigateToAllTransactions()
        {
            var navigationService = App.GetService<INavigationService>();
            navigationService?.NavigateTo<TransactionsViewModel>();
        }

        /// <summary>
        /// Открыть полный отчет "Баланс за период"
        /// </summary>
        private void OpenFullReport()
        {
            var navigationService = App.GetService<INavigationService>();
            navigationService?.NavigateTo<ReportsViewModel>();
        }

        protected override void OnDispose()
        {
            _dataChangeService.DataChanged -= OnDataChanged;

            var cts = System.Threading.Interlocked.Exchange(ref _loadCts, null);
            cts?.Cancel();
            cts?.Dispose();

            var navCts = Interlocked.Exchange(ref _navigateCts, null);
            navCts?.Cancel();
            navCts?.Dispose();

            if (QuickAdd != null)
            {
                QuickAdd.OnTransactionAdded = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// Типы периодов для сводки
    /// </summary>
    public enum PeriodType
    {
        Today,
        Week,
        Month,
        Year,
        AllTime,
        Custom
    }

    /// <summary>
    /// Группа операций за один день
    /// </summary>
    public class DateGroup
    {
        public DateTime Date { get; set; }
        public decimal DayTotal { get; set; }
        public ObservableCollection<Transaction> Items { get; set; } = new();

        public string DateLabel => Date == DateTime.Today
            ? "Сегодня"
            : Date == DateTime.Today.AddDays(-1)
                ? "Вчера"
                : Date.ToString("d MMMM", new System.Globalization.CultureInfo("ru-RU"));

        public string DayTotalFormatted => DayTotal >= 0
            ? $"+{DayTotal:N0} ₽"
            : $"{DayTotal:N0} ₽";

        public string DayTotalColor => DayTotal >= 0 ? "#22c55e" : "#ef4444";
    }

    /// <summary>
    /// Позиция в топе категорий
    /// </summary>
    public class CategorySummaryItem
    {
        public string Name { get; set; }
        public decimal Total { get; set; }
        public string TotalFormatted => $"{Total:N0} ₽";
    }
}
