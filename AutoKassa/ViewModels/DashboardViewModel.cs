using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Services.Dashboard;
using AutoKassa.Services.Transactions;
using OxyPlot;
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
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly IDataChangeService _dataChangeService;
        private readonly INavigationService _navigationService;
        private readonly IDashboardService _dashboardService;
        private readonly ITransactionEditViewModelFactory _transactionEditViewModelFactory;
        private readonly IDashboardPeriodCoordinator _periodCoordinator;
        private readonly IDashboardTransactionCoordinator _transactionCoordinator;
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
        private DashboardPeriod _currentPeriod;

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

        // Кредитные карты
        private bool _hasCreditCards;
        private decimal _totalCreditLimit;
        private decimal _totalCreditUsed;
        private decimal _totalCreditAvailable;
        private decimal _totalCreditDebt;
        private DateTime? _nextCreditPaymentDate;
        private decimal _nextCreditPaymentAmount;

        // Быстрое добавление (субVM)
        private QuickAddViewModel _quickAdd;

        // Модальное окно
        private bool _isModalOpen;
        private TransactionEditViewModel _editViewModel;

        #endregion

        public DashboardViewModel(
            ITransactionService transactionService,
            ICategoryService categoryService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            IDataChangeService dataChangeService,
            INavigationService navigationService,
            IDashboardService dashboardService,
            ITransactionEditViewModelFactory transactionEditViewModelFactory,
            IDashboardPeriodCoordinator periodCoordinator,
            IDashboardTransactionCoordinator transactionCoordinator,
            QuickAddViewModel quickAdd)
        {
            _transactionService = transactionService;
            _dialogService = dialogService;
            _toastService = toastService;
            _dataChangeService = dataChangeService;
            _navigationService = navigationService;
            _dashboardService = dashboardService;
            _transactionEditViewModelFactory = transactionEditViewModelFactory;
            _periodCoordinator = periodCoordinator;
            _transactionCoordinator = transactionCoordinator;
            _quickAdd = quickAdd;

            RecentTransactions = new ObservableCollection<Transaction>();
            GroupedTransactions = new ObservableCollection<DateGroup>();
            TopExpenses = new ObservableCollection<CategorySummaryItem>();
            TopIncomes = new ObservableCollection<CategorySummaryItem>();

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
            _currentPeriod = _periodCoordinator.SetPeriod(PeriodType.Month);
            ApplyPeriod(_currentPeriod);

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
                    try { await Task.Delay(1, cts.Token); }
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

        #region Кредитные карты

        /// <summary>
        /// Есть ли активные кредитные карты
        /// </summary>
        public bool HasCreditCards
        {
            get => _hasCreditCards;
            set => SetProperty(ref _hasCreditCards, value);
        }

        /// <summary>
        /// Общий кредитный лимит
        /// </summary>
        public decimal TotalCreditLimit
        {
            get => _totalCreditLimit;
            set
            {
                if (SetProperty(ref _totalCreditLimit, value))
                {
                    OnPropertyChanged(nameof(TotalCreditLimitFormatted));
                    OnPropertyChanged(nameof(CreditUsagePercent));
                }
            }
        }

        /// <summary>
        /// Использовано кредитных средств
        /// </summary>
        public decimal TotalCreditUsed
        {
            get => _totalCreditUsed;
            set
            {
                if (SetProperty(ref _totalCreditUsed, value))
                {
                    OnPropertyChanged(nameof(TotalCreditUsedFormatted));
                    OnPropertyChanged(nameof(CreditUsagePercent));
                }
            }
        }

        /// <summary>
        /// Доступный кредитный остаток
        /// </summary>
        public decimal TotalCreditAvailable
        {
            get => _totalCreditAvailable;
            set
            {
                if (SetProperty(ref _totalCreditAvailable, value))
                    OnPropertyChanged(nameof(TotalCreditAvailableFormatted));
            }
        }

        /// <summary>
        /// Текущий общий долг по кредитным картам
        /// </summary>
        public decimal TotalCreditDebt
        {
            get => _totalCreditDebt;
            set
            {
                if (SetProperty(ref _totalCreditDebt, value))
                    OnPropertyChanged(nameof(TotalCreditDebtFormatted));
            }
        }

        /// <summary>
        /// Дата ближайшего платежа
        /// </summary>
        public DateTime? NextCreditPaymentDate
        {
            get => _nextCreditPaymentDate;
            set
            {
                if (SetProperty(ref _nextCreditPaymentDate, value))
                    OnPropertyChanged(nameof(NextCreditPaymentDateFormatted));
            }
        }

        /// <summary>
        /// Сумма ближайшего минимального платежа
        /// </summary>
        public decimal NextCreditPaymentAmount
        {
            get => _nextCreditPaymentAmount;
            set
            {
                if (SetProperty(ref _nextCreditPaymentAmount, value))
                    OnPropertyChanged(nameof(NextCreditPaymentAmountFormatted));
            }
        }

        public string TotalCreditLimitFormatted => $"{TotalCreditLimit:N0} ₽";
        public string TotalCreditUsedFormatted => $"{TotalCreditUsed:N0} ₽";
        public string TotalCreditAvailableFormatted => $"{TotalCreditAvailable:N0} ₽";
        public string TotalCreditDebtFormatted => $"{TotalCreditDebt:N0} ₽";
        public string NextCreditPaymentDateFormatted => NextCreditPaymentDate?.ToString("dd.MM.yyyy") ?? "—";
        public string NextCreditPaymentAmountFormatted => $"{NextCreditPaymentAmount:N0} ₽";

        /// <summary>
        /// Процент использования кредитного лимита (для ProgressBar)
        /// </summary>
        public double CreditUsagePercent => TotalCreditLimit > 0
            ? (double)(TotalCreditUsed / TotalCreditLimit * 100m)
            : 0;

        #endregion

        /// <summary>
        /// Заголовок выбранного периода.
        /// </summary>
        public string PeriodLabel => _currentPeriod?.Label ?? "ЗА МЕСЯЦ";

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
        public string PeriodButtonLabel => _currentPeriod?.ButtonLabel ?? "Месяц";

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
            QuickAdd.OnTransactionAdded = async () =>
            {
                if (QuickAdd.SavedTransaction != null)
                    ApplyOptimisticAdd(QuickAdd.SavedTransaction);
                await LoadDataAsync();
                _dataChangeService?.NotifyDataChanged();
            };

            await QuickAdd.InitializeAsync();
            await LoadDataAsync();
        }

        /// <summary>
        /// Загрузка всех данных Dashboard через фасадный сервис.
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
                var data = await _dashboardService.LoadDashboardDataAsync(DateFrom, DateTo, ct);
                ApplyDashboardData(data);
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
        /// Раскладывает данные, полученные от сервиса, по свойствам ViewModel.
        /// </summary>
        private void ApplyDashboardData(DashboardData data)
        {
            var s = data.Summary;
            TotalIncome = s.Income;
            TotalExpense = s.Expense;
            Profit = s.Profit;
            Profitability = s.Profitability;

            var pb = data.PaymentBreakdown;
            CashIncome = pb.CashIncome;
            CashExpense = pb.CashExpense;
            CashProfit = pb.CashProfit;
            NonCashIncome = pb.NonCashIncome;
            NonCashExpense = pb.NonCashExpense;
            NonCashProfit = pb.NonCashProfit;

            var ch = data.Changes;
            IncomeChangePercent = ch.IncomeChangePercent;
            ExpenseChangePercent = ch.ExpenseChangePercent;
            ProfitChangePercent = ch.ProfitChangePercent;

            var r = data.Recent;
            RecentTransactions = new ObservableCollection<Transaction>(r.Transactions);
            HasTransactions = r.Transactions.Count > 0;
            GroupedTransactions = new ObservableCollection<DateGroup>(r.GroupedTransactions);
            StatsCount = r.StatsCount;
            StatsDays = r.StatsDays;
            StatsAvgExpense = r.AvgExpense;
            StatsAvgIncome = r.AvgIncome;
            UpdateCollection(TopExpenses, r.TopExpenses);
            UpdateCollection(TopIncomes, r.TopIncomes);

            var t = data.Today;
            TodayBalance = t.Balance;
            HasTodayTransactions = t.HasTransactions;

            var c = data.Cash;
            CashBalance = c.CashBalance;
            NonCashBalance = c.NonCashBalance;
            CurrentCashBalance = c.CurrentCashBalance;

            var cr = data.Credit;
            HasCreditCards = cr.HasCreditCards;
            TotalCreditLimit = cr.TotalLimit;
            TotalCreditUsed = cr.TotalUsed;
            TotalCreditAvailable = cr.TotalAvailable;
            TotalCreditDebt = cr.TotalDebt;
            NextCreditPaymentDate = cr.NextPaymentDate;
            NextCreditPaymentAmount = cr.NextPaymentAmount;

            ChartModel = data.ChartModel;
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
        /// Выбор периода
        /// </summary>
        private void SelectPeriod(PeriodType period)
        {
            SelectedPeriod = period;
            _currentPeriod = _periodCoordinator.SetPeriod(period);
            ApplyPeriod(_currentPeriod);
            IsPeriodPickerOpen = false;

            if (period != PeriodType.Custom)
            {
                RunAsync(LoadDataAsync);
            }
        }

        private async Task ApplyCustomPeriodAsync()
        {
            SelectedPeriod = PeriodType.Custom;
            _currentPeriod = _periodCoordinator.SetCustomPeriod(DateFrom, DateTo);
            ApplyPeriod(_currentPeriod);
            IsPeriodPickerOpen = false;
            await LoadDataAsync();
        }

        private void ApplyPeriod(DashboardPeriod period)
        {
            DateFrom = period.From;
            DateTo = period.To;
            OnPropertyChanged(nameof(PeriodLabel));
            OnPropertyChanged(nameof(PeriodButtonLabel));
        }

        /// <summary>
        /// Открыть диалог добавления новой операции
        /// </summary>
        private void OpenAddTransaction()
        {
            var callbacks = new DashboardTransactionCallbacks(
                OnSaved: async () =>
                {
                    IsModalOpen = false;
                    if (EditViewModel?.SavedTransaction != null)
                        ApplyOptimisticAdd(EditViewModel.SavedTransaction);
                    await LoadDataAsync();
                    _dataChangeService?.NotifyDataChanged();
                },
                OnSavedKeepOpen: async () => { await LoadDataAsync(); _dataChangeService?.NotifyDataChanged(); },
                OnCancelled: () => { IsModalOpen = false; });

            var vm = _transactionCoordinator.OpenAdd(callbacks);
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

            var callbacks = new DashboardTransactionCallbacks(
                OnSaved: async () =>
                {
                    IsModalOpen = false;
                    await LoadDataAsync();
                    _dataChangeService?.NotifyDataChanged();
                },
                OnSavedKeepOpen: async () => { await LoadDataAsync(); _dataChangeService?.NotifyDataChanged(); },
                OnCancelled: () => { IsModalOpen = false; });

            var vm = _transactionCoordinator.OpenEdit(t, callbacks);
            EditViewModel = vm;
            IsModalOpen = true;
        }

        /// <summary>
        /// Оптимистично добавляет операцию в UI до завершения полной перезагрузки.
        /// Это устраняет визуальную задержку на слабом железе.
        /// </summary>
        private void ApplyOptimisticAdd(Transaction transaction)
        {
            var state = new DashboardOptimisticState(
                RecentTransactions,
                GroupedTransactions,
                TotalIncome,
                TotalExpense,
                DateFrom,
                DateTo);

            var result = _transactionCoordinator.ApplyOptimisticAdd(state, transaction);

            RecentTransactions = new ObservableCollection<Transaction>(result.RecentTransactions);
            GroupedTransactions = new ObservableCollection<DateGroup>(result.GroupedTransactions);
            TotalIncome = result.TotalIncome;
            TotalExpense = result.TotalExpense;
            HasTransactions = result.HasTransactions;
        }

        /// <summary>
        /// Удалить операцию
        /// </summary>
        private async Task DeleteTransactionAsync(Transaction transaction = null)
        {
            var t = transaction ?? SelectedTransaction;
            if (t == null) return;

            await _transactionCoordinator.DeleteAsync(t, LoadDataAsync);
            RecentTransactions.Remove(t);
            HasTransactions = RecentTransactions.Any();
            await LoadDataAsync();
        }

        /// <summary>
        /// Переход к списку всех операций
        /// </summary>
        private void NavigateToAllTransactions()
        {
            _navigationService.NavigateTo<TransactionsViewModel>();
        }

        /// <summary>
        /// Открыть полный отчет "Баланс за период"
        /// </summary>
        private void OpenFullReport()
        {
            _navigationService.NavigateTo<ReportsViewModel>();
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

            (_editViewModel as IDisposable)?.Dispose();
        }

        #endregion
    }
}
