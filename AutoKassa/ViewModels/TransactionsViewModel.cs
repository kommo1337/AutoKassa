using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Services.Transactions;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    public enum PeriodPreset
    {
        Today,
        Week,
        Month,
        LastMonth,
        Year,
        AllTime,
        Custom
    }

    public class TransactionsViewModel : ViewModelBase, INavigationAware, IPageViewModel
    {
        private readonly ITransactionService _transactionService;
        private readonly ITransactionListService _transactionListService;
        private readonly ITransactionGroupingService _transactionGroupingService;
        private readonly IInlineTransactionService _inlineTransactionService;
        private readonly ITransactionSelectionService _transactionSelectionService;
        private readonly ICategoryService _categoryService;
        private readonly ICreditCardService _creditCardService;
        private readonly ICounterpartyService _counterpartyService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly IDataChangeService _dataChangeService;
        private readonly ITransactionEditViewModelFactory _transactionEditViewModelFactory;
        private bool _isInitialized;
        private bool _needsRefresh;

        private ObservableCollection<Transaction> _transactions;
        private ObservableCollection<Category> _categories;
        private ObservableCollection<CreditCard> _creditCards = new();
        private ObservableCollection<CreditCard> _creditCardFilterItems = new();
        private ObservableCollection<Counterparty> _counterparties = new();
        private ObservableCollection<SelectableDateGroup> _groupedTransactions = new();
        private Transaction _selectedTransaction;
        private bool _isLoading;
        private int _totalCount;
        private int _currentPage;
        private int _pageSize = 100;
        private CancellationTokenSource _loadCts;
        private CancellationTokenSource _searchDebounceCts;
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
        private bool _isDebtFilter;
        private DebtStatus? _selectedDebtStatusFilter;
        private Counterparty _selectedCounterpartyFilter;
        private CreditCard _selectedCreditCardFilter;
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

        // Counterparty picker
        private bool _isCounterpartyPickerOpen;

        // Credit card picker
        private bool _isCreditCardPickerOpen;

        // Modal
        private bool _isModalOpen;
        private TransactionEditViewModel _editViewModel;

        // Inline add
        private bool _isInlineOpen;
        private string _inlineAmountText;
        private OperationType _inlineType = OperationType.Expense;
        private Category _inlineCategory;
        private PaymentType _inlinePaymentType = PaymentType.Cash;
        private CreditCard _inlineCreditCard;
        private string _inlineDescription;

        public TransactionsViewModel(
            ITransactionService transactionService,
            ITransactionListService transactionListService,
            ITransactionGroupingService transactionGroupingService,
            IInlineTransactionService inlineTransactionService,
            ITransactionSelectionService transactionSelectionService,
            ICategoryService categoryService,
            ICreditCardService creditCardService,
            ICounterpartyService counterpartyService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            IDataChangeService dataChangeService,
            ITransactionEditViewModelFactory transactionEditViewModelFactory)
        {
            _transactionService = transactionService;
            _transactionListService = transactionListService;
            _transactionGroupingService = transactionGroupingService;
            _inlineTransactionService = inlineTransactionService;
            _transactionSelectionService = transactionSelectionService;
            _categoryService = categoryService;
            _creditCardService = creditCardService;
            _counterpartyService = counterpartyService;
            _dialogService = dialogService;
            _toastService = toastService;
            _dataChangeService = dataChangeService;
            _transactionEditViewModelFactory = transactionEditViewModelFactory;

            Transactions = new ObservableCollection<Transaction>();
            Categories = new ObservableCollection<Category>();

            // Standard commands
            LoadCommand = new AsyncRelayCommand(LoadDataAsync);
            AddCommand = new RelayCommand(_ => AddTransaction());
            // Параметр = конкретная операция (строка списка) или null → используем SelectedTransaction
            EditCommand = new RelayCommand(p => EditTransaction(p as Transaction ?? SelectedTransaction), p => (p as Transaction ?? SelectedTransaction) != null);
            DeleteCommand = new AsyncRelayCommand<object>(p => DeleteTransactionAsync(p as Transaction ?? SelectedTransaction), p => (p as Transaction ?? SelectedTransaction) != null);
            SelectTransactionCommand = new RelayCommand(p => SelectedTransaction = p as Transaction);
            ApplyFiltersCommand = new AsyncRelayCommand(ApplyFiltersAsync);
            ResetFiltersCommand = new AsyncRelayCommand(ResetFiltersAsync);
            LoadMoreCommand = new AsyncRelayCommand(LoadMoreAsync);
            SelectAllPaymentCommand = new AsyncRelayCommand(async () => { _selectedPaymentTypeFilter = null; RefreshPaymentFilterUI(); await LoadDataAsync(); });
            SelectCashFilterCommand = new AsyncRelayCommand(async () => { _selectedPaymentTypeFilter = PaymentType.Cash; _isDebtFilter = false; RefreshPaymentFilterUI(); RefreshDebtFilterUI(); await LoadDataAsync(); });
            SelectNonCashFilterCommand = new AsyncRelayCommand(async () => { _selectedPaymentTypeFilter = PaymentType.NonCash; _isDebtFilter = false; RefreshPaymentFilterUI(); RefreshDebtFilterUI(); await LoadDataAsync(); });
            SelectCreditCardFilterCommand = new AsyncRelayCommand(async () => { _selectedPaymentTypeFilter = PaymentType.CreditCard; _isDebtFilter = false; RefreshPaymentFilterUI(); RefreshDebtFilterUI(); await LoadDataAsync(); });
            SelectDebtFilterCommand = new AsyncRelayCommand(async () => { _isDebtFilter = true; _selectedPaymentTypeFilter = null; RefreshPaymentFilterUI(); RefreshDebtFilterUI(); await LoadDataAsync(); });
            FilterByIncomeCommand = new AsyncRelayCommand(async () => { _selectedType = OperationType.Income; RefreshTypeFilterUI(); await LoadDataAsync(); });
            FilterByExpenseCommand = new AsyncRelayCommand(async () => { _selectedType = OperationType.Expense; RefreshTypeFilterUI(); await LoadDataAsync(); });
            ResetTypeFilterCommand = new AsyncRelayCommand(async () => { _selectedType = null; RefreshTypeFilterUI(); await LoadDataAsync(); });
            SelectDebtStatusFilterCommand = new AsyncRelayCommand<DebtStatus?>(async status => { _selectedDebtStatusFilter = status; RefreshDebtStatusFilterUI(); await LoadDataAsync(); });

            // Period picker commands
            TogglePeriodPickerCommand = new RelayCommand(_ => IsPeriodPickerOpen = !IsPeriodPickerOpen);
            SelectPeriodCommand = new AsyncRelayCommand<object>(async p => { if (p is string s) await SelectPeriodAsync(s); });

            // Category picker commands
            ToggleCategoryPickerCommand = new RelayCommand(_ => IsCategoryPickerOpen = !IsCategoryPickerOpen);
            SelectCategoryCommand = new AsyncRelayCommand<object>(async p =>
            {
                if (p is Category cat)
                {
                    SelectedCategory = cat;
                    IsCategoryPickerOpen = false;
                    CategorySearchText = string.Empty;
                    await LoadDataAsync();
                }
            });

            // Counterparty picker commands
            ToggleCounterpartyPickerCommand = new RelayCommand(_ => IsCounterpartyPickerOpen = !IsCounterpartyPickerOpen);
            SelectCounterpartyFilterCommand = new RelayCommand<Counterparty>(cp =>
            {
                SelectedCounterpartyFilter = cp;
                IsCounterpartyPickerOpen = false;
            });

            // Credit card picker commands
            ToggleCreditCardPickerCommand = new RelayCommand(_ => IsCreditCardPickerOpen = !IsCreditCardPickerOpen);
            SelectCreditCardFilterItemCommand = new RelayCommand<CreditCard>(card =>
            {
                SelectedCreditCardFilter = card;
                IsCreditCardPickerOpen = false;
            });

            // Bulk selection commands
            DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync);
            ClearSelectionCommand = new RelayCommand(_ => _transactionSelectionService.ClearSelection(GroupedTransactions, RefreshSelectionState));

            // Inline add commands
            OpenInlineCommand = new RelayCommand(_ => OpenInline());
            InlineSaveCommand = new AsyncRelayCommand(InlineSaveAsync, CanInlineSave);
            InlineCancelCommand = new RelayCommand(_ => CloseInline());
            InlineToggleTypeCommand    = new RelayCommand(_ => InlineType = InlineType == OperationType.Expense ? OperationType.Income : OperationType.Expense);
            InlineSelectExpenseCommand = new RelayCommand(_ => InlineType = OperationType.Expense);
            InlineSelectIncomeCommand  = new RelayCommand(_ => InlineType = OperationType.Income);
            InlineSelectCashCommand = new RelayCommand(_ => InlinePaymentType = PaymentType.Cash);
            InlineSelectNonCashCommand = new RelayCommand(_ => InlinePaymentType = PaymentType.NonCash);
            InlineSelectCreditCardCommand = new RelayCommand(_ => InlinePaymentType = PaymentType.CreditCard);
            InlineTogglePaymentTypeCommand = new RelayCommand(_ => InlinePaymentType = InlinePaymentType switch
            {
                PaymentType.Cash => PaymentType.NonCash,
                PaymentType.NonCash => PaymentType.CreditCard,
                _ => PaymentType.Cash
            });

            // Период по умолчанию — текущий месяц целиком, включая будущие даты:
            // иначе операции с будущей датой скрыты до её наступления
            // и «появляются из ниоткуда» в свой день
            _dateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            _dateTo = new DateTime(DateTime.Now.Year, DateTime.Now.Month,
                DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));

            _dataChangeService.DataChanged += OnDataChanged;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized || _needsRefresh)
            {
                _needsRefresh = false;
                _isInitialized = true;
                RunAsync(InitializeAsync);
            }
        }

        public void OnNavigatedFrom() { }

        private void OnDataChanged()
        {
            _needsRefresh = true;
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

        public ObservableCollection<CreditCard> CreditCards
        {
            get => _creditCards;
            set
            {
                if (SetProperty(ref _creditCards, value))
                {
                    if (InlineIsCreditCard && _inlineCreditCard == null && value.Count > 0)
                        InlineCreditCard = value[0];
                }
            }
        }

        /// <summary>
        /// Список карт для фильтра операций: пункт «Все карты» (Id = 0) и все карты,
        /// включая неактивные — по ним могут остаться исторические операции.
        /// </summary>
        public ObservableCollection<CreditCard> CreditCardFilterItems
        {
            get => _creditCardFilterItems;
            set => SetProperty(ref _creditCardFilterItems, value);
        }

        public ObservableCollection<Counterparty> Counterparties
        {
            get => _counterparties;
            set => SetProperty(ref _counterparties, value);
        }

        public Transaction SelectedTransaction
        {
            get => _selectedTransaction;
            set
            {
                if (SetProperty(ref _selectedTransaction, value))
                {
                    // CanExecute команд Edit/Delete зависит от SelectedTransaction —
                    // обновляем только эти команды, а не все команды приложения
                    EditCommand.RaiseCanExecuteChanged();
                    DeleteCommand.RaiseCanExecuteChanged();
                }
            }
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
            set
            {
                if (SetProperty(ref _searchText, value))
                    DebounceSearch();
            }
        }

        private void DebounceSearch()
        {
            var newCts = new CancellationTokenSource();
            var oldCts = System.Threading.Interlocked.Exchange(ref _searchDebounceCts, newCts);
            oldCts?.Cancel();
            oldCts?.Dispose();
            var ct = newCts.Token;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(350, ct);
                    if (ct.IsCancellationRequested) return;
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                    {
                        if (!ct.IsCancellationRequested) await LoadDataAsync();
                    });
                }
                catch (TaskCanceledException) { }
            });
        }

        public bool IsAllPaymentFilter => _selectedPaymentTypeFilter == null && !_isDebtFilter;
        public bool IsCashFilter => _selectedPaymentTypeFilter == PaymentType.Cash;
        public bool IsNonCashFilter => _selectedPaymentTypeFilter == PaymentType.NonCash;
        public bool IsCreditCardFilter => _selectedPaymentTypeFilter == PaymentType.CreditCard;
        public bool IsDebtFilter => _isDebtFilter;

        public bool IsDebtStatusAll => _selectedDebtStatusFilter == null;
        public bool IsDebtStatusActive => _selectedDebtStatusFilter == DebtStatus.Active;
        public bool IsDebtStatusRepaid => _selectedDebtStatusFilter == DebtStatus.Repaid;
        public bool IsDebtStatusWrittenOff => _selectedDebtStatusFilter == DebtStatus.WrittenOff;

        public Counterparty SelectedCounterpartyFilter
        {
            get => _selectedCounterpartyFilter;
            set
            {
                if (SetProperty(ref _selectedCounterpartyFilter, value))
                    _ = LoadDataAsync();
            }
        }

        public CreditCard SelectedCreditCardFilter
        {
            get => _selectedCreditCardFilter;
            set
            {
                if (SetProperty(ref _selectedCreditCardFilter, value))
                    _ = LoadDataAsync();
            }
        }

        public string AmountFromText
        {
            get => _amountFromText;
            set
            {
                if (SetProperty(ref _amountFromText, value))
                    _amountFrom = decimal.TryParse(value, NumberStyles.Any, RuCulture, out var v) ? v : (decimal?)null;
            }
        }

        public string AmountToText
        {
            get => _amountToText;
            set
            {
                if (SetProperty(ref _amountToText, value))
                    _amountTo = decimal.TryParse(value, NumberStyles.Any, RuCulture, out var v) ? v : (decimal?)null;
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
            PeriodPreset.Year => "Год",
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

        public bool IsCounterpartyPickerOpen
        {
            get => _isCounterpartyPickerOpen;
            set => SetProperty(ref _isCounterpartyPickerOpen, value);
        }

        public bool IsCreditCardPickerOpen
        {
            get => _isCreditCardPickerOpen;
            set => SetProperty(ref _isCreditCardPickerOpen, value);
        }

        #endregion

        #region Properties — bulk selection

        public int SelectedCount => _transactionSelectionService.GetSelected(GroupedTransactions).Count;
        public bool HasSelection => SelectedCount > 0;
        public string SelectedCountLabel => _transactionSelectionService.GetSelectionLabel(SelectedCount);

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
                    OnPropertyChanged(nameof(InlineIsCreditCard));
                    if (value == PaymentType.CreditCard && _inlineCreditCard == null && _creditCards.Count > 0)
                        InlineCreditCard = _creditCards[0];
                }
            }
        }

        public CreditCard InlineCreditCard
        {
            get => _inlineCreditCard;
            set => SetProperty(ref _inlineCreditCard, value);
        }

        public bool InlineIsCash => _inlinePaymentType == PaymentType.Cash;
        public bool InlineIsNonCash => _inlinePaymentType == PaymentType.NonCash;
        public bool InlineIsCreditCard => _inlinePaymentType == PaymentType.CreditCard;

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
        public RelayCommand EditCommand { get; }
        public AsyncRelayCommand<object> DeleteCommand { get; }
        public ICommand SelectTransactionCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand LoadMoreCommand { get; }
        public ICommand SelectAllPaymentCommand { get; }
        public ICommand SelectCashFilterCommand { get; }
        public ICommand SelectNonCashFilterCommand { get; }
        public ICommand SelectCreditCardFilterCommand { get; }
        public ICommand SelectDebtFilterCommand { get; }
        public ICommand SelectDebtStatusFilterCommand { get; }
        public ICommand FilterByIncomeCommand { get; }
        public ICommand FilterByExpenseCommand { get; }
        public ICommand ResetTypeFilterCommand { get; }
        public ICommand TogglePeriodPickerCommand { get; }
        public ICommand SelectPeriodCommand { get; }
        public ICommand ToggleCategoryPickerCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand ToggleCounterpartyPickerCommand { get; }
        public ICommand SelectCounterpartyFilterCommand { get; }
        public ICommand ToggleCreditCardPickerCommand { get; }
        public ICommand SelectCreditCardFilterItemCommand { get; }
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
        public ICommand InlineSelectCreditCardCommand { get; }
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

                var newCategories = new ObservableCollection<Category>();
                newCategories.Add(new Category { Id = 0, Name = "Все категории" });

                foreach (var category in categories)
                    newCategories.Add(category);

                Categories = newCategories;

                var creditCards = await _creditCardService.GetAllAsync();
                var newCreditCards = new ObservableCollection<CreditCard>();
                foreach (var card in creditCards.Where(c => c.IsActive))
                    newCreditCards.Add(card);
                CreditCards = newCreditCards;

                // Для фильтра — все карты, включая неактивные (по ним могут быть операции)
                var cardFilterItems = new ObservableCollection<CreditCard> { new CreditCard { Id = 0, Name = "Все карты" } };
                foreach (var card in creditCards)
                    cardFilterItems.Add(card);
                CreditCardFilterItems = cardFilterItems;
                // Начальное значение — «Все карты»; через поле, чтобы не запускать лишнюю загрузку
                _selectedCreditCardFilter ??= CreditCardFilterItems.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedCreditCardFilter));

                var counterparties = await _counterpartyService.GetActiveAsync();
                var newCounterparties = new ObservableCollection<Counterparty>();
                newCounterparties.Add(new Counterparty { Id = 0, Name = "Все контрагенты" });
                foreach (var c in counterparties)
                    newCounterparties.Add(c);
                Counterparties = newCounterparties;
                // Начальное значение — «Все контрагенты»; через поле, чтобы не запускать лишнюю загрузку
                _selectedCounterpartyFilter ??= Counterparties.FirstOrDefault();
                OnPropertyChanged(nameof(SelectedCounterpartyFilter));

                OnPropertyChanged(nameof(InlineCategories));
                OnPropertyChanged(nameof(FilteredCategories));
                SelectedCategory = Categories.FirstOrDefault();
                InlineCategory = InlineCategories.FirstOrDefault();
                if (InlineIsCreditCard && _inlineCreditCard == null && CreditCards.Count > 0)
                    InlineCreditCard = CreditCards[0];
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка загрузки категорий");
                _dialogService.ShowError("Не удалось загрузить категории. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
        }

        private async Task LoadDataAsync()
        {
            var newCts = new CancellationTokenSource();
            var oldCts = System.Threading.Interlocked.Exchange(ref _loadCts, newCts);
            oldCts?.Cancel();
            oldCts?.Dispose();
            var ct = newCts.Token;

            try
            {
                IsLoading = true;
                _currentPage = 0;

                var filters = CreateFilterParameters();
                var request = new TransactionListRequest(filters, _pageSize, _currentPage);
                var result = await _transactionListService.LoadFirstPageAsync(request, ct);

                // Пересоздаём коллекцию одним действием вместо N уведомлений UI
                Transactions = new ObservableCollection<Transaction>(result.Transactions);

                TotalCount = result.TotalCount;
                OnPropertyChanged(nameof(CanLoadMore));
                await RefreshGroupsAsync();
            }
            catch (OperationCanceledException) { /* фильтр изменился — игнорируем */ }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка загрузки операций");
                _dialogService.ShowError("Не удалось загрузить операции. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
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

                var filters = CreateFilterParameters();
                var request = new TransactionListRequest(filters, _pageSize, _currentPage);
                var result = await _transactionListService.LoadNextPageAsync(request);

                _currentPage = result.CurrentPage;
                // Одно уведомление коллекции вместо N — меньше перестроек списка
                Transactions = new ObservableCollection<Transaction>(Transactions.Concat(result.Transactions));

                OnPropertyChanged(nameof(DisplayInfo));
                OnPropertyChanged(nameof(CanLoadMore));
                await RefreshGroupsAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка загрузки операций");
                _dialogService.ShowError("Не удалось загрузить операции. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private TransactionFilterParameters CreateFilterParameters()
        {
            return new TransactionFilterParameters
            {
                DateFrom = DateFrom,
                DateTo = DateTo,
                Type = _selectedType,
                PaymentType = _selectedPaymentTypeFilter,
                CategoryId = SelectedCategory?.Id > 0 ? SelectedCategory.Id : (int?)null,
                CounterpartyId = SelectedCounterpartyFilter?.Id > 0 ? SelectedCounterpartyFilter.Id : (int?)null,
                CreditCardId = SelectedCreditCardFilter?.Id > 0 ? SelectedCreditCardFilter.Id : (int?)null,
                IsDebtFilter = _isDebtFilter,
                DebtStatus = _selectedDebtStatusFilter,
                SearchText = SearchText,
                AmountFrom = _amountFrom,
                AmountTo = _amountTo,
                SortBy = "Date",
                SortDescending = true
            };
        }

        private void RefreshPaymentFilterUI()
        {
            OnPropertyChanged(nameof(IsAllPaymentFilter));
            OnPropertyChanged(nameof(IsCashFilter));
            OnPropertyChanged(nameof(IsNonCashFilter));
            OnPropertyChanged(nameof(IsCreditCardFilter));
            OnPropertyChanged(nameof(IsDebtFilter));
        }

        private void RefreshDebtFilterUI()
        {
            OnPropertyChanged(nameof(IsDebtFilter));
            OnPropertyChanged(nameof(IsAllPaymentFilter));
        }

        private void RefreshDebtStatusFilterUI()
        {
            OnPropertyChanged(nameof(IsDebtStatusAll));
            OnPropertyChanged(nameof(IsDebtStatusActive));
            OnPropertyChanged(nameof(IsDebtStatusRepaid));
            OnPropertyChanged(nameof(IsDebtStatusWrittenOff));
        }

        private void RefreshTypeFilterUI()
        {
            OnPropertyChanged(nameof(IsAllTypeFilter));
            OnPropertyChanged(nameof(IsIncomeFilter));
            OnPropertyChanged(nameof(IsExpenseFilter));
        }

        private async Task RefreshGroupsAsync()
        {
            // Снимаем подписки на старых элементах, чтобы они не держали ViewModel через делегат.
            foreach (var oldGroup in GroupedTransactions)
                foreach (var item in oldGroup.Items)
                    item.SelectionChanged = null;

            var result = await _transactionGroupingService.BuildGroupsAsync(
                Transactions,
                _categories,
                CreditCards,
                GroupInlineSaveAsync,
                RefreshSelectionState);

            TotalIncome = result.TotalIncome;
            TotalExpense = result.TotalExpense;
            GroupedTransactions = new ObservableCollection<SelectableDateGroup>(result.Groups);
            OnPropertyChanged(nameof(HasTransactions));
            RefreshSelectionState();
        }

        private void RefreshSelectionState()
        {
            OnPropertyChanged(nameof(SelectedCount));
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(SelectedCountLabel));
        }

        private async Task DeleteSelectedAsync()
        {
            var selected = _transactionSelectionService.GetSelected(GroupedTransactions);

            if (!selected.Any()) return;

            try
            {
                var deletedIds = selected.Select(t => t.Id).ToList();
                // Массовое удаление одним контекстом и одной транзакцией БД
                await _transactionService.DeleteRangeAsync(deletedIds);

                await LoadDataAsync();
                _dataChangeService?.NotifyDataChanged();

                var count = deletedIds.Count;
                _toastService.ShowDeleteWithUndo(
                    $"Удалено {count} {_transactionSelectionService.GetCountWord(count)}",
                    async () =>
                    {
                        // Массовое восстановление одним контекстом и одной транзакцией БД
                        await _transactionService.RestoreRangeAsync(deletedIds);
                        await LoadDataAsync();
                    });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка массового удаления операций");
                _dialogService.ShowError("Не удалось удалить операции. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
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
            // Конец месяца, а не «сейчас»: будущие даты текущего месяца должны быть видны
            DateTo = new DateTime(DateTime.Now.Year, DateTime.Now.Month,
                DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month));
            _selectedType = null;
            RefreshTypeFilterUI();
            _selectedPaymentTypeFilter = null;
            _isDebtFilter = false;
            _selectedDebtStatusFilter = null;
            RefreshPaymentFilterUI();
            RefreshDebtFilterUI();
            RefreshDebtStatusFilterUI();
            SelectedCategory = Categories.FirstOrDefault();
            SelectedCounterpartyFilter = Counterparties.FirstOrDefault();
            SelectedCreditCardFilter = CreditCardFilterItems.FirstOrDefault();
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
                    // Весь месяц, включая будущие даты (см. комментарий в конструкторе)
                    DateTo = new DateTime(today.Year, today.Month, DateTime.DaysInMonth(today.Year, today.Month));
                    _selectedPeriodPreset = PeriodPreset.Month;
                    break;
                case "LastMonth":
                    var last = today.AddMonths(-1);
                    DateFrom = new DateTime(last.Year, last.Month, 1);
                    DateTo = new DateTime(last.Year, last.Month, DateTime.DaysInMonth(last.Year, last.Month));
                    _selectedPeriodPreset = PeriodPreset.LastMonth;
                    break;
                case "Year":
                    DateFrom = today.AddYears(-1);
                    DateTo = today;
                    _selectedPeriodPreset = PeriodPreset.Year;
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
            var vm = _transactionEditViewModelFactory.CreateForAdd();
            vm.OnSaved = async () =>
            {
                CloseEditModal();
                await LoadDataAsync();
                _dataChangeService?.NotifyDataChanged();
            };
            vm.OnSavedKeepOpen = async () => { await LoadDataAsync(); _dataChangeService?.NotifyDataChanged(); };
            vm.OnCancelled = () => CloseEditModal();
            EditViewModel = vm;
            IsModalOpen = true;
        }

        private void EditTransaction(Transaction transaction)
        {
            if (transaction == null) return;

            var vm = _transactionEditViewModelFactory.CreateForEdit(transaction);
            vm.OnSaved = async () => { CloseEditModal(); await LoadDataAsync(); _dataChangeService?.NotifyDataChanged(); };
            vm.OnSavedKeepOpen = async () => { await LoadDataAsync(); _dataChangeService?.NotifyDataChanged(); };
            vm.OnCancelled = () => CloseEditModal();
            EditViewModel = vm;
            IsModalOpen = true;
        }

        /// <summary>
        /// Закрыть модальное окно операции: ViewModel освобождается и обнуляется,
        /// чтобы её представление пересоздавалось при следующем открытии.
        /// </summary>
        private void CloseEditModal()
        {
            IsModalOpen = false;
            (_editViewModel as IDisposable)?.Dispose();
            EditViewModel = null;
        }

        private async Task DeleteTransactionAsync(Transaction t)
        {
            if (t == null) return;

            var deletedId = t.Id;

            try
            {
                await _transactionService.DeleteAsync(deletedId);
                await LoadDataAsync();
                _dataChangeService?.NotifyDataChanged();

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
                Serilog.Log.Error(ex, "Ошибка удаления операции {TransactionId}", deletedId);
                _dialogService.ShowError("Не удалось удалить операцию. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
        }

        private void OpenInline()
        {
            InlineAmountText = string.Empty;
            InlineType = OperationType.Expense;
            InlinePaymentType = PaymentType.Cash;
            InlineDescription = string.Empty;
            InlineCategory = InlineCategories.FirstOrDefault();
            InlineCreditCard = CreditCards.Count > 0 ? CreditCards[0] : null;
            IsInlineOpen = true;
        }

        private void CloseInline()
        {
            if (!HasInlineInput())
            {
                IsInlineOpen = false;
                return;
            }

            _toastService.ShowWithAction(
                "Отменить добавление? Данные будут потеряны.",
                "Отменить",
                () =>
                {
                    IsInlineOpen = false;
                    InlineAmountText = string.Empty;
                    InlineDescription = string.Empty;
                },
                ToastType.Info);
        }

        private bool HasInlineInput()
        {
            return !string.IsNullOrWhiteSpace(InlineAmountText)
                || !string.IsNullOrWhiteSpace(InlineDescription);
        }

        private bool _isInlineSaving;

        private bool CanInlineSave() => !_isInlineSaving;

        private async Task InlineSaveAsync()
        {
            if (_isInlineSaving) return;

            _isInlineSaving = true;
            (InlineSaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                var input = new InlineTransactionInput(
                    InlineAmountText,
                    InlineType,
                    InlineCategory,
                    InlinePaymentType,
                    InlineCreditCard,
                    InlineDescription,
                    DateTime.Now);

                var transaction = await _inlineTransactionService.TrySaveAsync(
                    input,
                    msg => _dialogService.ShowError(msg));

                if (transaction == null) return;

                IsInlineOpen = false;
                InlineAmountText = string.Empty;
                InlineDescription = string.Empty;
                await LoadDataAsync();
                _dataChangeService?.NotifyDataChanged();
                _toastService.ShowSuccess("Операция добавлена");
            }
            catch (InvalidOperationException ex)
            {
                // Бизнес-правило из сервиса — текст уже понятен пользователю
                _dialogService.ShowError(ex.Message);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка добавления inline-операции");
                _dialogService.ShowError("Не удалось добавить операцию. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                _isInlineSaving = false;
                (InlineSaveCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private async Task GroupInlineSaveAsync(SelectableDateGroup group)
        {
            var input = new InlineTransactionInput(
                group.InlineAmountText,
                group.InlineType,
                group.GroupInlineCategory,
                group.InlinePaymentType,
                group.InlineCreditCard,
                group.InlineDescription,
                group.Date);

            var transaction = await _inlineTransactionService.TrySaveAsync(
                input,
                msg => _dialogService.ShowError(msg));

            if (transaction == null) return;

            await LoadDataAsync();
            _dataChangeService?.NotifyDataChanged();
            _toastService.ShowSuccess("Операция добавлена");
        }

        protected override void OnDispose()
        {
            _dataChangeService.DataChanged -= OnDataChanged;

            var cts = Interlocked.Exchange(ref _loadCts, null);
            cts?.Cancel();
            cts?.Dispose();
            var sCts = Interlocked.Exchange(ref _searchDebounceCts, null);
            sCts?.Cancel();
            sCts?.Dispose();

            (_editViewModel as IDisposable)?.Dispose();

            base.OnDispose();
        }

        #endregion
    }
}
