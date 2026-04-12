using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    public enum PeriodPreset
    {
        Today,
        Week,
        Month,
        LastMonth,
        Quarter,
        AllTime,
        Custom
    }

    public class TransactionsViewModel : ViewModelBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly ISettingsService _settingsService;

        private ObservableCollection<Transaction> _transactions;
        private ObservableCollection<Category> _categories;
        private ObservableCollection<SelectableDateGroup> _groupedTransactions = new();
        private Transaction _selectedTransaction;
        private bool _isLoading;
        private int _totalCount;
        private int _currentPage;
        private int _pageSize = 100;
        private CancellationTokenSource _loadCts;
        private decimal _totalIncome;
        private decimal _totalExpense;
        private static readonly CultureInfo RuCulture = new("ru-RU");

        // Filters
        private DateTime? _dateFrom;
        private DateTime? _dateTo;
        private OperationType? _selectedType;
        private Category _selectedCategory;
        private string _searchText;
        private PaymentType? _selectedPaymentTypeFilter;
        private string _amountFromText;
        private string _amountToText;
        private decimal? _amountFrom;
        private decimal? _amountTo;

        // Period picker
        private PeriodPreset _selectedPeriodPreset = PeriodPreset.Month;
        private bool _isPeriodPickerOpen;

        // Category picker
        private bool _isCategoryPickerOpen;
        private string _categorySearchText;

        // Modal
        private bool _isModalOpen;
        private TransactionEditViewModel _editViewModel;

        // Inline add
        private bool _isInlineOpen;
        private string _inlineAmountText;
        private OperationType _inlineType = OperationType.Expense;
        private Category _inlineCategory;
        private PaymentType _inlinePaymentType = PaymentType.Cash;
        private string _inlineDescription;

        public TransactionsViewModel(
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

            Transactions = new ObservableCollection<Transaction>();
            Categories = new ObservableCollection<Category>();

            // Standard commands
            LoadCommand = new RelayCommand(async _ => await LoadDataAsync());
            AddCommand = new RelayCommand(_ => AddTransaction());
            EditCommand = new RelayCommand(_ => EditTransaction(), _ => SelectedTransaction != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteTransactionAsync(), _ => SelectedTransaction != null);
            ApplyFiltersCommand = new RelayCommand(async _ => await ApplyFiltersAsync());
            ResetFiltersCommand = new RelayCommand(async _ => await ResetFiltersAsync());
            LoadMoreCommand = new RelayCommand(async _ => await LoadMoreAsync());
            SelectAllPaymentCommand = new RelayCommand(async _ => { _selectedPaymentTypeFilter = null; RefreshPaymentFilterUI(); await LoadDataAsync(); });
            SelectCashFilterCommand = new RelayCommand(async _ => { _selectedPaymentTypeFilter = PaymentType.Cash; RefreshPaymentFilterUI(); await LoadDataAsync(); });
            SelectNonCashFilterCommand = new RelayCommand(async _ => { _selectedPaymentTypeFilter = PaymentType.NonCash; RefreshPaymentFilterUI(); await LoadDataAsync(); });
            FilterByIncomeCommand = new RelayCommand(async _ => { _selectedType = OperationType.Income; RefreshTypeFilterUI(); await LoadDataAsync(); });
            FilterByExpenseCommand = new RelayCommand(async _ => { _selectedType = OperationType.Expense; RefreshTypeFilterUI(); await LoadDataAsync(); });
            ResetTypeFilterCommand = new RelayCommand(async _ => { _selectedType = null; RefreshTypeFilterUI(); await LoadDataAsync(); });

            // Period picker commands
            TogglePeriodPickerCommand = new RelayCommand(_ => IsPeriodPickerOpen = !IsPeriodPickerOpen);
            SelectPeriodCommand = new RelayCommand(async p => { if (p is string s) await SelectPeriodAsync(s); });

            // Category picker commands
            ToggleCategoryPickerCommand = new RelayCommand(_ => IsCategoryPickerOpen = !IsCategoryPickerOpen);
            SelectCategoryCommand = new RelayCommand(async p =>
            {
                if (p is Category cat)
                {
                    SelectedCategory = cat;
                    IsCategoryPickerOpen = false;
                    CategorySearchText = string.Empty;
                    await LoadDataAsync();
                }
            });

            // Bulk selection commands
            DeleteSelectedCommand = new RelayCommand(async _ => await DeleteSelectedAsync());
            ClearSelectionCommand = new RelayCommand(_ => ClearSelection());

            // Inline add commands
            OpenInlineCommand = new RelayCommand(_ => OpenInline());
            InlineSaveCommand = new RelayCommand(async _ => await InlineSaveAsync());
            InlineCancelCommand = new RelayCommand(_ => CloseInline());
            InlineToggleTypeCommand    = new RelayCommand(_ => InlineType = InlineType == OperationType.Expense ? OperationType.Income : OperationType.Expense);
            InlineSelectExpenseCommand = new RelayCommand(_ => InlineType = OperationType.Expense);
            InlineSelectIncomeCommand  = new RelayCommand(_ => InlineType = OperationType.Income);
            InlineSelectCashCommand = new RelayCommand(_ => InlinePaymentType = PaymentType.Cash);
            InlineSelectNonCashCommand = new RelayCommand(_ => InlinePaymentType = PaymentType.NonCash);
            InlineTogglePaymentTypeCommand = new RelayCommand(_ => InlinePaymentType = InlinePaymentType == PaymentType.Cash ? PaymentType.NonCash : PaymentType.Cash);

            // Default period = current month
            _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo = DateTime.Now;

            RunAsync(InitializeAsync);
        }

        #region Properties — transactions

        public ObservableCollection<SelectableDateGroup> GroupedTransactions
        {
            get => _groupedTransactions;
            set => SetProperty(ref _groupedTransactions, value);
        }

        public decimal TotalIncome
        {
            get => _totalIncome;
            set { if (SetProperty(ref _totalIncome, value)) OnPropertyChanged(nameof(TotalIncomeFormatted)); }
        }

        public decimal TotalExpense
        {
            get => _totalExpense;
            set { if (SetProperty(ref _totalExpense, value)) OnPropertyChanged(nameof(TotalExpenseFormatted)); }
        }

        public string TotalIncomeFormatted => $"+{TotalIncome.ToString("N0", RuCulture)} ₽";
        public string TotalExpenseFormatted => $"−{TotalExpense.ToString("N0", RuCulture)} ₽";

        public bool HasTransactions => GroupedTransactions.Any();

        public bool IsAllTypeFilter => _selectedType == null;
        public bool IsIncomeFilter => _selectedType == OperationType.Income;
        public bool IsExpenseFilter => _selectedType == OperationType.Expense;

        public ObservableCollection<Transaction> Transactions
        {
            get => _transactions;
            set => SetProperty(ref _transactions, value);
        }

        public ObservableCollection<Category> Categories
        {
            get => _categories;
            set
            {
                if (SetProperty(ref _categories, value))
                {
                    OnPropertyChanged(nameof(InlineCategories));
                    OnPropertyChanged(nameof(FilteredCategories));
                }
            }
        }

        public Transaction SelectedTransaction
        {
            get => _selectedTransaction;
            set => SetProperty(ref _selectedTransaction, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public int TotalCount
        {
            get => _totalCount;
            set { if (SetProperty(ref _totalCount, value)) OnPropertyChanged(nameof(DisplayInfo)); }
        }

        public string DisplayInfo => $"Показано {Transactions.Count} из {TotalCount}";
        public bool CanLoadMore => Transactions.Count < TotalCount;

        #endregion

        #region Properties — filters

        public DateTime? DateFrom
        {
            get => _dateFrom;
            set => SetProperty(ref _dateFrom, value);
        }

        public DateTime? DateTo
        {
            get => _dateTo;
            set => SetProperty(ref _dateTo, value);
        }

        public OperationType? SelectedType
        {
            get => _selectedType;
            set { if (SetProperty(ref _selectedType, value)) RefreshTypeFilterUI(); }
        }

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public bool IsAllPaymentFilter => _selectedPaymentTypeFilter == null;
        public bool IsCashFilter => _selectedPaymentTypeFilter == PaymentType.Cash;
        public bool IsNonCashFilter => _selectedPaymentTypeFilter == PaymentType.NonCash;

        public string AmountFromText
        {
            get => _amountFromText;
            set
            {
                _amountFromText = value;
                _amountFrom = decimal.TryParse(value, NumberStyles.Any, RuCulture, out var v) ? v : (decimal?)null;
                OnPropertyChanged();
            }
        }

        public string AmountToText
        {
            get => _amountToText;
            set
            {
                _amountToText = value;
                _amountTo = decimal.TryParse(value, NumberStyles.Any, RuCulture, out var v) ? v : (decimal?)null;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Properties — period picker

        public PeriodPreset SelectedPeriodPreset
        {
            get => _selectedPeriodPreset;
            set { if (SetProperty(ref _selectedPeriodPreset, value)) OnPropertyChanged(nameof(PeriodLabel)); }
        }

        public string PeriodLabel => _selectedPeriodPreset switch
        {
            PeriodPreset.Today => "Сегодня",
            PeriodPreset.Week => "Эта неделя",
            PeriodPreset.Month => "Этот месяц",
            PeriodPreset.LastMonth => "Прошлый месяц",
            PeriodPreset.Quarter => "Квартал",
            PeriodPreset.AllTime => "Всё время",
            PeriodPreset.Custom when _dateFrom.HasValue && _dateTo.HasValue =>
                $"{_dateFrom.Value:dd.MM} – {_dateTo.Value:dd.MM}",
            _ => "Период"
        };

        public bool IsPeriodPickerOpen
        {
            get => _isPeriodPickerOpen;
            set => SetProperty(ref _isPeriodPickerOpen, value);
        }

        #endregion

        #region Properties — category picker

        public bool IsCategoryPickerOpen
        {
            get => _isCategoryPickerOpen;
            set => SetProperty(ref _isCategoryPickerOpen, value);
        }

        public string CategorySearchText
        {
            get => _categorySearchText;
            set
            {
                if (SetProperty(ref _categorySearchText, value))
                    OnPropertyChanged(nameof(FilteredCategories));
            }
        }

        public IEnumerable<Category> FilteredCategories =>
            string.IsNullOrWhiteSpace(_categorySearchText)
                ? (IEnumerable<Category>)Categories
                : Categories.Where(c => c.Name.Contains(_categorySearchText, StringComparison.OrdinalIgnoreCase));

        #endregion

        #region Properties — bulk selection

        public int SelectedCount => GroupedTransactions.SelectMany(g => g.Items).Count(i => i.IsSelected);
        public bool HasSelection => SelectedCount > 0;
        public string SelectedCountLabel => $"Выбрано: {SelectedCount}";

        #endregion

        #region Properties — modal

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

        #endregion

        #region Properties — inline add

        public bool IsInlineOpen
        {
            get => _isInlineOpen;
            set => SetProperty(ref _isInlineOpen, value);
        }

        public string InlineAmountText
        {
            get => _inlineAmountText;
            set => SetProperty(ref _inlineAmountText, value);
        }

        public OperationType InlineType
        {
            get => _inlineType;
            set
            {
                if (SetProperty(ref _inlineType, value))
                {
                    OnPropertyChanged(nameof(IsInlineExpense));
                    OnPropertyChanged(nameof(IsInlineIncome));
                    OnPropertyChanged(nameof(InlineCategories));
                    InlineCategory = InlineCategories.FirstOrDefault();
                }
            }
        }

        public bool IsInlineExpense => _inlineType == OperationType.Expense;
        public bool IsInlineIncome => _inlineType == OperationType.Income;

        public Category InlineCategory
        {
            get => _inlineCategory;
            set => SetProperty(ref _inlineCategory, value);
        }

        public PaymentType InlinePaymentType
        {
            get => _inlinePaymentType;
            set
            {
                if (SetProperty(ref _inlinePaymentType, value))
                {
                    OnPropertyChanged(nameof(InlineIsCash));
                    OnPropertyChanged(nameof(InlineIsNonCash));
                }
            }
        }

        public bool InlineIsCash => _inlinePaymentType == PaymentType.Cash;
        public bool InlineIsNonCash => _inlinePaymentType == PaymentType.NonCash;

        public string InlineDescription
        {
            get => _inlineDescription;
            set => SetProperty(ref _inlineDescription, value);
        }

        public IEnumerable<Category> InlineCategories =>
            Categories.Where(c => c.Id > 0 && c.Type == _inlineType);

        #endregion

        #region Commands

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand SelectAllPaymentCommand { get; }
        public ICommand SelectCashFilterCommand { get; }
        public ICommand SelectNonCashFilterCommand { get; }
        public ICommand FilterByIncomeCommand { get; }
        public ICommand FilterByExpenseCommand { get; }
        public ICommand ResetTypeFilterCommand { get; }
        public ICommand TogglePeriodPickerCommand { get; }
        public ICommand SelectPeriodCommand { get; }
        public ICommand ToggleCategoryPickerCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand ClearSelectionCommand { get; }
        public ICommand OpenInlineCommand { get; }
        public ICommand InlineSaveCommand { get; }
        public ICommand InlineCancelCommand { get; }
        public ICommand InlineToggleTypeCommand { get; }
        public ICommand InlineSelectExpenseCommand { get; }
        public ICommand InlineSelectIncomeCommand { get; }
        public ICommand InlineSelectCashCommand { get; }
        public ICommand InlineSelectNonCashCommand { get; }
        public ICommand InlineTogglePaymentTypeCommand { get; }

        #endregion

        #region Methods

        private async Task InitializeAsync()
        {
            await LoadCategoriesAsync();
            await LoadDataAsync();
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _categoryService.GetActiveAsync();

                Categories.Clear();
                Categories.Add(new Category { Id = 0, Name = "Все категории" });

                foreach (var category in categories)
                    Categories.Add(category);

                OnPropertyChanged(nameof(InlineCategories));
                OnPropertyChanged(nameof(FilteredCategories));
                SelectedCategory = Categories.FirstOrDefault();
                InlineCategory = InlineCategories.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
        }

        private async Task LoadDataAsync()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = new CancellationTokenSource();
            var ct = _loadCts.Token;

            try
            {
                IsLoading = true;
                _currentPage = 0;

                var filters = BuildFilterParameters();
                var transactions = await _transactionService.GetTransactionsAsync(filters, ct);
                var totalCount   = await _transactionService.GetTotalCountAsync(filters, ct);

                Transactions.Clear();
                foreach (var t in transactions)
                    Transactions.Add(t);

                TotalCount = totalCount;
                OnPropertyChanged(nameof(CanLoadMore));
                RebuildGroupsAndTotals();
            }
            catch (OperationCanceledException) { /* фильтр изменился — игнорируем */ }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки данных: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadMoreAsync()
        {
            if (!CanLoadMore || IsLoading) return;

            try
            {
                IsLoading = true;
                _currentPage++;

                var filters  = BuildFilterParameters();
                filters.Skip = _currentPage * _pageSize;

                var transactions = await _transactionService.GetTransactionsAsync(filters);

                foreach (var t in transactions)
                    Transactions.Add(t);

                OnPropertyChanged(nameof(DisplayInfo));
                OnPropertyChanged(nameof(CanLoadMore));
                RebuildGroupsAndTotals();
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

        private TransactionFilterParameters BuildFilterParameters()
        {
            return new TransactionFilterParameters
            {
                DateFrom = DateFrom,
                DateTo = DateTo,
                Type = _selectedType,
                PaymentType = _selectedPaymentTypeFilter,
                CategoryId = SelectedCategory?.Id > 0 ? SelectedCategory.Id : (int?)null,
                SearchText = SearchText,
                AmountFrom = _amountFrom,
                AmountTo = _amountTo,
                Skip = _currentPage * _pageSize,
                Take = _pageSize,
                SortBy = "Date",
                SortDescending = true
            };
        }

        private void RefreshPaymentFilterUI()
        {
            OnPropertyChanged(nameof(IsAllPaymentFilter));
            OnPropertyChanged(nameof(IsCashFilter));
            OnPropertyChanged(nameof(IsNonCashFilter));
        }

        private void RefreshTypeFilterUI()
        {
            OnPropertyChanged(nameof(IsAllTypeFilter));
            OnPropertyChanged(nameof(IsIncomeFilter));
            OnPropertyChanged(nameof(IsExpenseFilter));
        }

        private void RebuildGroupsAndTotals()
        {
            TotalIncome = Transactions.Where(t => t.Type == OperationType.Income).Sum(t => t.Amount);
            TotalExpense = Transactions.Where(t => t.Type == OperationType.Expense).Sum(t => t.Amount);

            GroupedTransactions.Clear();
            var grouped = Transactions.GroupBy(t => t.Date.Date).OrderByDescending(g => g.Key);

            foreach (var g in grouped)
            {
                var dayTotal = g.Sum(t => t.Type == OperationType.Income ? t.Amount : -t.Amount);
                var items = new ObservableCollection<SelectableTransaction>(
                    g.OrderByDescending(t => t.CreatedAt).Select(t =>
                    {
                        var st = new SelectableTransaction(t);
                        st.SelectionChanged = RefreshSelectionState;
                        return st;
                    }));

                var group = new SelectableDateGroup
                {
                    Date = g.Key,
                    DayTotal = dayTotal,
                    Items = items
                };
                group.InitInline(_categories, GroupInlineSaveAsync);
                GroupedTransactions.Add(group);
            }

            OnPropertyChanged(nameof(HasTransactions));
            RefreshSelectionState();
        }

        private void RefreshSelectionState()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedCountLabel));
        }

        private void ClearSelection()
        {
            foreach (var item in GroupedTransactions.SelectMany(g => g.Items).ToList())
            {
                item.SelectionChanged = null;
                item.IsSelected = false;
                item.SelectionChanged = RefreshSelectionState;
            }
            RefreshSelectionState();
        }

        private async Task DeleteSelectedAsync()
        {
            var selected = GroupedTransactions
                .SelectMany(g => g.Items)
                .Where(i => i.IsSelected)
                .Select(i => i.Transaction)
                .ToList();

            if (!selected.Any()) return;

            try
            {
                var deletedIds = selected.Select(t => t.Id).ToList();
                foreach (var id in deletedIds)
                    await _transactionService.DeleteAsync(id);

                await LoadDataAsync();

                var count = deletedIds.Count;
                _toastService.ShowDeleteWithUndo(
                    $"Удалено {count} {GetCountWord(count)}",
                    async () =>
                    {
                        foreach (var id in deletedIds)
                            await _transactionService.RestoreAsync(id);
                        await LoadDataAsync();
                    });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка удаления: {ex.Message}");
            }
        }

        private static string GetCountWord(int count)
        {
            var abs = Math.Abs(count) % 100;
            var mod10 = abs % 10;
            if (abs is >= 11 and <= 19) return "записей";
            return mod10 switch { 1 => "запись", 2 or 3 or 4 => "записи", _ => "записей" };
        }

        private async Task ApplyFiltersAsync()
        {
            IsPeriodPickerOpen = false;
            await LoadDataAsync();
        }

        private async Task ResetFiltersAsync()
        {
            _selectedPeriodPreset = PeriodPreset.Month;
            OnPropertyChanged(nameof(PeriodLabel));

            DateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            DateTo = DateTime.Now;
            _selectedType = null;
            RefreshTypeFilterUI();
            _selectedPaymentTypeFilter = null;
            RefreshPaymentFilterUI();
            SelectedCategory = Categories.FirstOrDefault();
            SearchText = string.Empty;
            AmountFromText = string.Empty;
            AmountToText = string.Empty;

            await LoadDataAsync();
        }

        private async Task SelectPeriodAsync(string preset)
        {
            var today = DateTime.Today;

            switch (preset)
            {
                case "Today":
                    DateFrom = today;
                    DateTo = today;
                    _selectedPeriodPreset = PeriodPreset.Today;
                    break;
                case "Week":
                    var dow = (int)today.DayOfWeek;
                    var mondayOffset = dow == 0 ? -6 : 1 - dow;
                    DateFrom = today.AddDays(mondayOffset);
                    DateTo = today;
                    _selectedPeriodPreset = PeriodPreset.Week;
                    break;
                case "Month":
                    DateFrom = new DateTime(today.Year, today.Month, 1);
                    DateTo = today;
                    _selectedPeriodPreset = PeriodPreset.Month;
                    break;
                case "LastMonth":
                    var last = today.AddMonths(-1);
                    DateFrom = new DateTime(last.Year, last.Month, 1);
                    DateTo = new DateTime(last.Year, last.Month, DateTime.DaysInMonth(last.Year, last.Month));
                    _selectedPeriodPreset = PeriodPreset.LastMonth;
                    break;
                case "Quarter":
                    DateFrom = today.AddMonths(-3);
                    DateTo = today;
                    _selectedPeriodPreset = PeriodPreset.Quarter;
                    break;
                case "AllTime":
                    DateFrom = null;
                    DateTo = null;
                    _selectedPeriodPreset = PeriodPreset.AllTime;
                    break;
            }

            OnPropertyChanged(nameof(PeriodLabel));
            IsPeriodPickerOpen = false;
            await LoadDataAsync();
        }

        private void AddTransaction()
        {
            var vm = new TransactionEditViewModel(_transactionService, _categoryService, _dialogService, _settingsService, _toastService);
            vm.InitializeForAdd();
            vm.OnSaved = () => { IsModalOpen = false; RunAsync(LoadDataAsync); };
            vm.OnCancelled = () => { IsModalOpen = false; };
            EditViewModel = vm;
            IsModalOpen = true;
        }

        private void EditTransaction()
        {
            if (SelectedTransaction == null) return;

            var vm = new TransactionEditViewModel(_transactionService, _categoryService, _dialogService, _settingsService, _toastService);
            vm.InitializeForEdit(SelectedTransaction);
            vm.OnSaved = () => { IsModalOpen = false; RunAsync(LoadDataAsync); };
            vm.OnCancelled = () => { IsModalOpen = false; };
            EditViewModel = vm;
            IsModalOpen = true;
        }

        private async Task DeleteTransactionAsync()
        {
            if (SelectedTransaction == null) return;

            var t = SelectedTransaction;
            var deletedId = t.Id;

            try
            {
                await _transactionService.DeleteAsync(deletedId);
                Transactions.Remove(t);
                TotalCount--;
                RebuildGroupsAndTotals();

                _toastService.ShowDeleteWithUndo(
                    "Операция удалена",
                    async () =>
                    {
                        await _transactionService.RestoreAsync(deletedId);
                        await LoadDataAsync();
                    });
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка удаления: {ex.Message}");
            }
        }

        private void OpenInline()
        {
            InlineAmountText = string.Empty;
            InlineType = OperationType.Expense;
            InlinePaymentType = PaymentType.Cash;
            InlineDescription = string.Empty;
            InlineCategory = InlineCategories.FirstOrDefault();
            IsInlineOpen = true;
        }

        private void CloseInline()
        {
            IsInlineOpen = false;
        }

        private async Task InlineSaveAsync()
        {
            if (!decimal.TryParse(
                    InlineAmountText?.Replace(',', '.'),
                    NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal amount) || amount <= 0)
            {
                _dialogService.ShowError("Введите корректную сумму");
                return;
            }

            if (InlineCategory == null)
            {
                _dialogService.ShowError("Выберите категорию");
                return;
            }

            try
            {
                var transaction = new Transaction
                {
                    Date = DateTime.Now,
                    Amount = amount,
                    Type = InlineType,
                    CategoryId = InlineCategory.Id,
                    Description = InlineDescription ?? string.Empty,
                    PaymentType = InlinePaymentType
                };

                await _transactionService.AddAsync(transaction);
                CloseInline();
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка добавления: {ex.Message}");
            }
        }

        private async Task GroupInlineSaveAsync(SelectableDateGroup group)
        {
            if (!decimal.TryParse(
                    group.InlineAmountText?.Replace(',', '.'),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out decimal amount) || amount <= 0)
            {
                _dialogService.ShowError("Введите корректную сумму");
                return;
            }

            if (group.GroupInlineCategory == null)
            {
                _dialogService.ShowError("Выберите категорию");
                return;
            }

            try
            {
                var transaction = new Transaction
                {
                    Date        = group.Date,
                    Amount      = amount,
                    Type        = group.InlineType,
                    CategoryId  = group.GroupInlineCategory.Id,
                    Description = group.InlineDescription ?? string.Empty,
                    PaymentType = group.InlinePaymentType
                };

                await _transactionService.AddAsync(transaction);
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка добавления: {ex.Message}");
            }
        }

        protected override void OnDispose()
        {
            _loadCts?.Cancel();
            _loadCts?.Dispose();
            _loadCts = null;
        }

        #endregion
    }
}
