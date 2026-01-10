using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Views;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для главного экрана (Dashboard)
    /// </summary>
    public class DashboardViewModel : ViewModelBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly ISettingsService _settingsService;

        #region Поля

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
        private bool _isCustomPeriodVisible;

        // Быстрое добавление
        private decimal _quickAmount;
        private bool _isIncome;
        private Category _selectedCategory;
        private ObservableCollection<Category> _filteredCategories;
        private DateTime _quickDate = DateTime.Today;
        private string _quickDescription;
        private bool _isDescriptionVisible;
        private bool _isDateVisible;
        private string _quickAmountText;

        // Последние операции
        private ObservableCollection<Transaction> _recentTransactions;
        private Transaction _selectedTransaction;
        private bool _hasTransactions;

        // График
        private PlotModel _chartModel;
        private bool _isLoading;

        #endregion

        public DashboardViewModel(
            ITransactionService transactionService,
            ICategoryService categoryService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            ISettingsService settingsService)
        {
            _transactionService = transactionService;
            _categoryService = categoryService;
            _dialogService = dialogService;
            _toastService = toastService;
            _settingsService = settingsService;

            FilteredCategories = new ObservableCollection<Category>();
            RecentTransactions = new ObservableCollection<Transaction>();

            // Команды периода
            SelectPeriodCommand = new RelayCommand(period => SelectPeriod((PeriodType)period));
            ApplyCustomPeriodCommand = new RelayCommand(async _ => await LoadDataAsync());

            // Команды быстрого добавления
            AddQuickTransactionCommand = new RelayCommand(async _ => await AddQuickTransactionAsync(), _ => CanAddQuickTransaction());
            ToggleTransactionTypeCommand = new RelayCommand(_ => ToggleTransactionType());
            ToggleDescriptionCommand = new RelayCommand(_ => IsDescriptionVisible = !IsDescriptionVisible);
            ToggleDateCommand = new RelayCommand(_ => IsDateVisible = !IsDateVisible);

            // Команды операций
            OpenTransactionCommand = new RelayCommand(_ => OpenTransaction(), _ => SelectedTransaction != null);
            NavigateToAllTransactionsCommand = new RelayCommand(_ => NavigateToAllTransactions());
            OpenFullReportCommand = new RelayCommand(_ => OpenFullReport());

            // Установка начального периода
            SetPeriodDates(PeriodType.Month);

            // Загрузка данных
            _ = InitializeAsync();
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
                    IsCustomPeriodVisible = value == PeriodType.Custom;
                    OnPropertyChanged(nameof(IsTodaySelected));
                    OnPropertyChanged(nameof(IsWeekSelected));
                    OnPropertyChanged(nameof(IsMonthSelected));
                    OnPropertyChanged(nameof(IsYearSelected));
                    OnPropertyChanged(nameof(IsCustomSelected));
                }
            }
        }

        public bool IsTodaySelected => SelectedPeriod == PeriodType.Today;
        public bool IsWeekSelected => SelectedPeriod == PeriodType.Week;
        public bool IsMonthSelected => SelectedPeriod == PeriodType.Month;
        public bool IsYearSelected => SelectedPeriod == PeriodType.Year;
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
        /// Видимость выбора произвольного периода
        /// </summary>
        public bool IsCustomPeriodVisible
        {
            get => _isCustomPeriodVisible;
            set => SetProperty(ref _isCustomPeriodVisible, value);
        }

        #endregion

        #region Свойства быстрого добавления

        /// <summary>
        /// Сумма операции (текст для валидации)
        /// </summary>
        public string QuickAmountText
        {
            get => _quickAmountText;
            set
            {
                if (SetProperty(ref _quickAmountText, value))
                {
                    // Парсим сумму
                    if (decimal.TryParse(value?.Replace(" ", "").Replace("₽", ""), out var amount))
                    {
                        _quickAmount = amount;
                    }
                    else
                    {
                        _quickAmount = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Сумма операции
        /// </summary>
        public decimal QuickAmount
        {
            get => _quickAmount;
            set => SetProperty(ref _quickAmount, value);
        }

        /// <summary>
        /// Тип операции - доход
        /// </summary>
        public bool IsIncome
        {
            get => _isIncome;
            set
            {
                if (SetProperty(ref _isIncome, value))
                {
                    _ = LoadCategoriesForTypeAsync();
                }
            }
        }

        /// <summary>
        /// Выбранная категория
        /// </summary>
        public Category SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        /// <summary>
        /// Отфильтрованные категории по типу
        /// </summary>
        public ObservableCollection<Category> FilteredCategories
        {
            get => _filteredCategories;
            set => SetProperty(ref _filteredCategories, value);
        }

        /// <summary>
        /// Дата операции
        /// </summary>
        public DateTime QuickDate
        {
            get => _quickDate;
            set => SetProperty(ref _quickDate, value);
        }

        /// <summary>
        /// Описание операции
        /// </summary>
        public string QuickDescription
        {
            get => _quickDescription;
            set => SetProperty(ref _quickDescription, value);
        }

        /// <summary>
        /// Видимость поля описания
        /// </summary>
        public bool IsDescriptionVisible
        {
            get => _isDescriptionVisible;
            set => SetProperty(ref _isDescriptionVisible, value);
        }

        /// <summary>
        /// Видимость выбора даты
        /// </summary>
        public bool IsDateVisible
        {
            get => _isDateVisible;
            set => SetProperty(ref _isDateVisible, value);
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

        #region Команды

        public ICommand SelectPeriodCommand { get; }
        public ICommand ApplyCustomPeriodCommand { get; }
        public ICommand AddQuickTransactionCommand { get; }
        public ICommand ToggleTransactionTypeCommand { get; }
        public ICommand ToggleDescriptionCommand { get; }
        public ICommand ToggleDateCommand { get; }
        public ICommand OpenTransactionCommand { get; }
        public ICommand NavigateToAllTransactionsCommand { get; }
        public ICommand OpenFullReportCommand { get; }

        #endregion

        #region Методы

        /// <summary>
        /// Инициализация данных
        /// </summary>
        private async Task InitializeAsync()
        {
            await LoadCategoriesForTypeAsync();
            await LoadDataAsync();
        }

        /// <summary>
        /// Загрузка категорий для текущего типа операции
        /// </summary>
        private async Task LoadCategoriesForTypeAsync()
        {
            try
            {
                var type = IsIncome ? OperationType.Income : OperationType.Expense;
                var categories = await _categoryService.GetByTypeAsync(type);

                FilteredCategories.Clear();
                foreach (var category in categories)
                {
                    FilteredCategories.Add(category);
                }

                // Выбираем первую категорию
                SelectedCategory = FilteredCategories.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка всех данных Dashboard
        /// </summary>
        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;

                // Параллельная загрузка данных
                var summaryTask = LoadSummaryAsync();
                var recentTask = LoadRecentTransactionsAsync();
                var chartTask = LoadChartDataAsync();

                await Task.WhenAll(summaryTask, recentTask, chartTask);
            }
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
        /// Загрузка сводки за период
        /// </summary>
        private async Task LoadSummaryAsync()
        {
            var filters = new TransactionFilterParameters
            {
                DateFrom = DateFrom,
                DateTo = DateTo.Date.AddDays(1).AddTicks(-1) // До конца дня
            };

            var transactions = await _transactionService.GetTransactionsAsync(filters);

            // Расчет текущего периода
            TotalIncome = transactions.Where(t => t.Type == OperationType.Income).Sum(t => t.Amount);
            TotalExpense = transactions.Where(t => t.Type == OperationType.Expense).Sum(t => t.Amount);
            Profit = TotalIncome - TotalExpense;

            // Рентабельность
            Profitability = TotalIncome > 0 ? (Profit / TotalIncome) * 100 : 0;

            // Расчет изменений относительно предыдущего периода
            await CalculateChangesAsync();
        }

        /// <summary>
        /// Расчет изменений относительно предыдущего периода
        /// </summary>
        private async Task CalculateChangesAsync()
        {
            // Определяем предыдущий период такой же длины
            var periodLength = (DateTo - DateFrom).Days + 1;
            var prevDateTo = DateFrom.AddDays(-1);
            var prevDateFrom = prevDateTo.AddDays(-periodLength + 1);

            var prevFilters = new TransactionFilterParameters
            {
                DateFrom = prevDateFrom,
                DateTo = prevDateTo.Date.AddDays(1).AddTicks(-1)
            };

            var prevTransactions = await _transactionService.GetTransactionsAsync(prevFilters);

            var prevIncome = prevTransactions.Where(t => t.Type == OperationType.Income).Sum(t => t.Amount);
            var prevExpense = prevTransactions.Where(t => t.Type == OperationType.Expense).Sum(t => t.Amount);
            var prevProfit = prevIncome - prevExpense;

            // Расчет процентных изменений
            IncomeChangePercent = prevIncome > 0 ? ((TotalIncome - prevIncome) / prevIncome) * 100 : (TotalIncome > 0 ? 100 : 0);
            ExpenseChangePercent = prevExpense > 0 ? ((TotalExpense - prevExpense) / prevExpense) * 100 : (TotalExpense > 0 ? 100 : 0);
            ProfitChangePercent = prevProfit != 0 ? ((Profit - prevProfit) / Math.Abs(prevProfit)) * 100 : (Profit != 0 ? 100 : 0);
        }

        /// <summary>
        /// Загрузка последних операций
        /// </summary>
        private async Task LoadRecentTransactionsAsync()
        {
            var transactions = await _transactionService.GetRecentAsync(10);

            RecentTransactions.Clear();
            foreach (var transaction in transactions)
            {
                RecentTransactions.Add(transaction);
            }

            HasTransactions = RecentTransactions.Any();
        }

        /// <summary>
        /// Загрузка данных для графика
        /// </summary>
        private async Task LoadChartDataAsync()
        {
            var filters = new TransactionFilterParameters
            {
                DateFrom = DateFrom,
                DateTo = DateTo.Date.AddDays(1).AddTicks(-1)
            };

            var transactions = await _transactionService.GetTransactionsAsync(filters);

            // Группировка по дням
            var groupedData = transactions
                .GroupBy(t => t.Date.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    Date = g.Key,
                    Income = g.Where(t => t.Type == OperationType.Income).Sum(t => t.Amount),
                    Expense = g.Where(t => t.Type == OperationType.Expense).Sum(t => t.Amount)
                })
                .ToList();

            // Создание модели графика
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

            foreach (var data in groupedData)
            {
                runningIncome += data.Income;
                runningExpense += data.Expense;
                var runningProfit = runningIncome - runningExpense;

                var dateValue = DateTimeAxis.ToDouble(data.Date);
                incomeSeries.Points.Add(new DataPoint(dateValue, (double)runningIncome));
                expenseSeries.Points.Add(new DataPoint(dateValue, (double)runningExpense));
                profitSeries.Points.Add(new DataPoint(dateValue, (double)runningProfit));
            }

            model.Series.Add(incomeSeries);
            model.Series.Add(expenseSeries);
            model.Series.Add(profitSeries);

            ChartModel = model;
        }

        /// <summary>
        /// Выбор периода
        /// </summary>
        private void SelectPeriod(PeriodType period)
        {
            SelectedPeriod = period;
            SetPeriodDates(period);

            if (period != PeriodType.Custom)
            {
                _ = LoadDataAsync();
            }
        }

        /// <summary>
        /// Установка дат периода
        /// </summary>
        private void SetPeriodDates(PeriodType period)
        {
            var today = DateTime.Today;

            switch (period)
            {
                case PeriodType.Today:
                    DateFrom = today;
                    DateTo = today;
                    break;

                case PeriodType.Week:
                    // Начало недели (понедельник)
                    var daysFromMonday = ((int)today.DayOfWeek + 6) % 7;
                    DateFrom = today.AddDays(-daysFromMonday);
                    DateTo = today;
                    break;

                case PeriodType.Month:
                    DateFrom = new DateTime(today.Year, today.Month, 1);
                    DateTo = today;
                    break;

                case PeriodType.Year:
                    DateFrom = new DateTime(today.Year, 1, 1);
                    DateTo = today;
                    break;

                case PeriodType.Custom:
                    // Даты не меняем
                    break;
            }
        }

        /// <summary>
        /// Переключение типа операции
        /// </summary>
        private void ToggleTransactionType()
        {
            IsIncome = !IsIncome;
        }

        /// <summary>
        /// Можно ли добавить операцию
        /// </summary>
        private bool CanAddQuickTransaction()
        {
            return _quickAmount > 0 && SelectedCategory != null;
        }

        /// <summary>
        /// Быстрое добавление операции
        /// </summary>
        private async Task AddQuickTransactionAsync()
        {
            try
            {
                var transaction = new Transaction
                {
                    Amount = _quickAmount,
                    Type = IsIncome ? OperationType.Income : OperationType.Expense,
                    CategoryId = SelectedCategory.Id,
                    Date = QuickDate,
                    Description = QuickDescription ?? string.Empty,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };

                await _transactionService.AddAsync(transaction);

                // Показываем уведомление
                _toastService.ShowSuccess("Операция добавлена успешно");

                // Очищаем форму
                ClearQuickForm();

                // Обновляем данные
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ошибка: {ex.Message}");
            }
        }

        /// <summary>
        /// Очистка формы быстрого добавления
        /// </summary>
        private void ClearQuickForm()
        {
            QuickAmountText = string.Empty;
            QuickAmount = 0;
            QuickDescription = string.Empty;
            QuickDate = DateTime.Today;
            IsDescriptionVisible = false;
            IsDateVisible = false;
        }

        /// <summary>
        /// Открыть операцию для редактирования
        /// </summary>
        private void OpenTransaction()
        {
            if (SelectedTransaction == null) return;

            var viewModel = new TransactionEditViewModel(_transactionService, _categoryService, _dialogService, _settingsService);
            viewModel.InitializeForEdit(SelectedTransaction);

            var window = new TransactionEditView(viewModel)
            {
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() == true)
            {
                _ = LoadDataAsync();
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
        Custom
    }
}
