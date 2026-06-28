using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System.Globalization;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    public class TransactionEditViewModel : ViewModelBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly ICreditCardService _creditCardService;
        private readonly ICounterpartyService _counterpartyService;
        private readonly IDialogService _dialogService;

        private Transaction _transaction;
        private bool _isEditMode;
        private DateTime _date;
        private decimal _amount;
        private string _amountText = "";
        private OperationType _type;
        private Category _selectedCategory;
        private string _description = string.Empty;
        private List<Category> _categories = new List<Category>();
        private bool _isInitialized;
        private bool _isRepayment;
        private PaymentType _selectedPaymentType = PaymentType.Cash;
        private bool _showAllCategories;
        private List<CreditCard> _availableCreditCards = new();
        private CreditCard? _selectedCreditCard;
        private List<Counterparty> _availableCounterparties = new();
        private Counterparty? _selectedCounterparty;

        public CalculatorViewModel Calculator { get; }

        /// <summary>
        /// Последняя сохранённая транзакция (для оптимистичного обновления UI).
        /// </summary>
        public Transaction? SavedTransaction { get; private set; }

        private readonly ISettingsService _settingsService;
        private readonly IToastNotificationService _toastService;

        // Category manager
        private bool _isCategoryManagerOpen;
        private CategoryManagerViewModel _categoryManagerViewModel;

        // Snapshot to detect unsaved changes
        private string _initialAmountText = "";
        private OperationType _initialType;
        private int? _initialCategoryId;
        private string _initialDescription = "";
        private PaymentType _initialPaymentType = PaymentType.Cash;
        private int? _initialCreditCardId;
        private int? _initialCounterpartyId;
        private DateTime _initialDate;

        public TransactionEditViewModel(
            ITransactionService transactionService,
            ICategoryService categoryService,
            ICreditCardService creditCardService,
            ICounterpartyService counterpartyService,
            IDialogService dialogService,
            ISettingsService settingsService,
            IToastNotificationService toastService)
        {
            _transactionService = transactionService;
            _categoryService = categoryService;
            _creditCardService = creditCardService;
            _counterpartyService = counterpartyService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _toastService = toastService;

            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            SaveAndAddNextCommand = new RelayCommand(async _ => await SaveAndAddNextAsync(), _ => CanSave() && !IsEditMode);
            CancelCommand = new RelayCommand(_ => Cancel());
            SelectCashCommand = new RelayCommand(_ => SelectedPaymentType = PaymentType.Cash);
            SelectNonCashCommand = new RelayCommand(_ => SelectedPaymentType = PaymentType.NonCash);
            SelectCreditCardPaymentCommand = new RelayCommand(_ => SelectedPaymentType = PaymentType.CreditCard);
            SelectDebtCommand = new RelayCommand(_ => SelectedPaymentType = PaymentType.Debt);
            SelectCreditCardCommand = new RelayCommand(p => { if (p is CreditCard c) SelectedCreditCard = c; });
            SelectCounterpartyCommand = new RelayCommand(p => { if (p is Counterparty c) SelectedCounterparty = c; });
            SelectExpenseTypeCommand = new RelayCommand(_ => Type = OperationType.Expense);
            SelectIncomeTypeCommand = new RelayCommand(_ => Type = OperationType.Income);
            ToggleShowAllCategoriesCommand = new RelayCommand(_ => ShowAllCategories = !ShowAllCategories);
            SelectCategoryCommand = new RelayCommand(p => { if (p is Category c) SelectedCategory = c; });

            Calculator = new CalculatorViewModel { OnResult = result => AmountText = result };
            OpenCategoryManagerCommand = new RelayCommand(_ => OpenCategoryManager());

            Date = DateTime.Now;
            _type = _settingsService.GetDefaultOperationType();

            RunAsync(InitializeAsync);
        }

        private async System.Threading.Tasks.Task InitializeAsync()
        {
            await LoadCategoriesAsync();
            await LoadCreditCardsAsync();
            await LoadCounterpartiesAsync();
            _isInitialized = true;
        }

        #region Properties

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(Title));
                    OnPropertyChanged(nameof(SaveButtonText));
                }
            }
        }

        public string Title => IsEditMode ? "Редактировать операцию" : "Новая операция";

        public string SaveButtonText
        {
            get
            {
                if (IsEditMode) return "Сохранить изменения";
                return IsIncome ? "Добавить доход" : "Добавить расход";
            }
        }

        public DateTime Date
        {
            get => _date;
            set
            {
                if (SetProperty(ref _date, value))
                    OnPropertyChanged(nameof(TodayLabel));
            }
        }

        public string TodayLabel => Date.Date == DateTime.Today ? "(сегодня)" : "";

        public string AmountText
        {
            get => _amountText;
            set
            {
                if (SetProperty(ref _amountText, value))
                {
                    var normalized = (value ?? "").Replace(',', '.');
                    _amount = decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal d) ? d : 0;

                    if (!string.IsNullOrEmpty(value))
                        ValidateAmount();
                    else
                    {
                        ClearErrors(nameof(Amount));
                        OnPropertyChanged(nameof(AmountError));
                    }
                }
            }
        }

        public decimal Amount => _amount;

        public OperationType Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    if (_isInitialized)
                        RunAsync(LoadCategoriesAsync);
                    OnPropertyChanged(nameof(IsIncome));
                    OnPropertyChanged(nameof(IsExpense));
                    OnPropertyChanged(nameof(SaveButtonText));
                }
            }
        }

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                    ValidateCategory();
            }
        }

        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        public List<Category> Categories
        {
            get => _categories;
            set
            {
                if (SetProperty(ref _categories, value))
                {
                    OnPropertyChanged(nameof(VisibleCategories));
                    OnPropertyChanged(nameof(HasMoreCategories));
                    OnPropertyChanged(nameof(ShowMoreLabel));
                }
            }
        }

        public bool ShowAllCategories
        {
            get => _showAllCategories;
            set
            {
                if (SetProperty(ref _showAllCategories, value))
                {
                    OnPropertyChanged(nameof(VisibleCategories));
                    OnPropertyChanged(nameof(ShowMoreLabel));
                }
            }
        }

        public List<Category> VisibleCategories =>
            ShowAllCategories ? Categories : Categories.Take(4).ToList();

        public bool HasMoreCategories => Categories.Count > 4;

        public string ShowMoreLabel => ShowAllCategories
            ? "Свернуть"
            : "Ещё ▾";

        public string AmountError => GetFirstError(nameof(Amount));

        public bool IsRepayment
        {
            get => _isRepayment;
            private set
            {
                if (SetProperty(ref _isRepayment, value))
                {
                    (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (SaveAndAddNextCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    OnPropertyChanged(nameof(RepaymentWarning));
                }
            }
        }

        public string RepaymentWarning => IsRepayment
            ? "Редактирование операции-погашения запрещено"
            : string.Empty;

        public bool IsIncome
        {
            get => Type == OperationType.Income;
            set { if (value) Type = OperationType.Income; }
        }

        public bool IsExpense
        {
            get => Type == OperationType.Expense;
            set { if (value) Type = OperationType.Expense; }
        }

        public PaymentType SelectedPaymentType
        {
            get => _selectedPaymentType;
            set
            {
                if (SetProperty(ref _selectedPaymentType, value))
                {
                    OnPropertyChanged(nameof(IsCash));
                    OnPropertyChanged(nameof(IsNonCash));
                    OnPropertyChanged(nameof(IsCreditCard));
                    OnPropertyChanged(nameof(IsDebt));

                    if (_selectedPaymentType == PaymentType.CreditCard)
                    {
                        SelectedCounterparty = null;
                        if (_selectedCreditCard == null && _availableCreditCards.Count == 1)
                            SelectedCreditCard = _availableCreditCards[0];
                    }
                    else if (_selectedPaymentType == PaymentType.Debt)
                    {
                        SelectedCreditCard = null;
                        if (_selectedCounterparty == null && _availableCounterparties.Count == 1)
                            SelectedCounterparty = _availableCounterparties[0];
                    }
                    else
                    {
                        SelectedCreditCard = null;
                        SelectedCounterparty = null;
                    }
                }
            }
        }

        public bool IsCash
        {
            get => SelectedPaymentType == PaymentType.Cash;
            set { if (value) SelectedPaymentType = PaymentType.Cash; }
        }

        public bool IsNonCash
        {
            get => SelectedPaymentType == PaymentType.NonCash;
            set { if (value) SelectedPaymentType = PaymentType.NonCash; }
        }

        public bool IsCreditCard
        {
            get => SelectedPaymentType == PaymentType.CreditCard;
            set { if (value) SelectedPaymentType = PaymentType.CreditCard; }
        }

        public bool IsDebt
        {
            get => SelectedPaymentType == PaymentType.Debt;
            set { if (value) SelectedPaymentType = PaymentType.Debt; }
        }

        public List<CreditCard> AvailableCreditCards
        {
            get => _availableCreditCards;
            set
            {
                if (SetProperty(ref _availableCreditCards, value))
                    OnPropertyChanged(nameof(HasAvailableCreditCards));
            }
        }

        public bool HasAvailableCreditCards => _availableCreditCards.Count > 0;

        public CreditCard? SelectedCreditCard
        {
            get => _selectedCreditCard;
            set
            {
                if (SetProperty(ref _selectedCreditCard, value))
                    ValidateCreditCard();
            }
        }

        public List<Counterparty> AvailableCounterparties
        {
            get => _availableCounterparties;
            set
            {
                if (SetProperty(ref _availableCounterparties, value))
                    OnPropertyChanged(nameof(HasAvailableCounterparties));
            }
        }

        public bool HasAvailableCounterparties => _availableCounterparties.Count > 0;

        public Counterparty? SelectedCounterparty
        {
            get => _selectedCounterparty;
            set
            {
                if (SetProperty(ref _selectedCounterparty, value))
                    ValidateCounterparty();
            }
        }

        public bool IsCategoryManagerOpen
        {
            get => _isCategoryManagerOpen;
            set => SetProperty(ref _isCategoryManagerOpen, value);
        }

        public CategoryManagerViewModel CategoryManagerViewModel
        {
            get => _categoryManagerViewModel;
            set => SetProperty(ref _categoryManagerViewModel, value);
        }

        #endregion

        #region Commands

        public ICommand SaveCommand { get; }
        public ICommand SaveAndAddNextCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SelectCashCommand { get; }
        public ICommand SelectNonCashCommand { get; }
        public ICommand SelectCreditCardPaymentCommand { get; }
        public ICommand SelectDebtCommand { get; }
        public ICommand SelectCreditCardCommand { get; }
        public ICommand SelectCounterpartyCommand { get; }
        public ICommand SelectExpenseTypeCommand { get; }
        public ICommand SelectIncomeTypeCommand { get; }
        public ICommand ToggleShowAllCategoriesCommand { get; }
        public ICommand SelectCategoryCommand { get; }
        public ICommand OpenCategoryManagerCommand { get; }

        #endregion

        #region Methods

        public void InitializeForAdd()
        {
            IsEditMode = false;
            IsRepayment = false;
            Date = DateTime.Now;
            _amount = 0;
            _amountText = "";
            OnPropertyChanged(nameof(AmountText));
            ClearErrors(nameof(Amount));
            OnPropertyChanged(nameof(AmountError));
            Type = OperationType.Expense;
            Description = string.Empty;
            SelectedPaymentType = PaymentType.Cash;
            SelectedCreditCard = null;
            SelectedCounterparty = null;
            ShowAllCategories = false;
            Calculator.Clear();
            Calculator.IsOpen = false;
            ValidateCategory();
            ValidateCreditCard();
            CaptureSnapshot();
        }

        public void InitializeForEdit(Transaction transaction)
        {
            IsEditMode = true;
            _transaction = transaction;

            Date = transaction.Date;
            AmountText = transaction.Amount.ToString(CultureInfo.InvariantCulture);
            Type = transaction.Type;
            Description = transaction.Description;
            SelectedPaymentType = transaction.PaymentType;
            SelectedCreditCard = null;
            SelectedCounterparty = null;
            ShowAllCategories = false;
            Calculator.Clear();
            Calculator.IsOpen = false;

            RunAsync(async () =>
            {
                await LoadCategoriesAsync();
                await LoadCreditCardsAsync();

                if (Categories != null && Categories.Count > 0)
                    SelectedCategory = Categories.FirstOrDefault(c => c.Id == transaction.CategoryId);

                if (_availableCreditCards.Count > 0 && transaction.CreditCardId.HasValue)
                    SelectedCreditCard = _availableCreditCards.FirstOrDefault(c => c.Id == transaction.CreditCardId.Value);

                if (_availableCounterparties.Count > 0 && transaction.CounterpartyId.HasValue)
                    SelectedCounterparty = _availableCounterparties.FirstOrDefault(c => c.Id == transaction.CounterpartyId.Value);

                IsRepayment = await _transactionService.IsRepaymentTransactionAsync(transaction.Id);

                CaptureSnapshot();
            });
        }

        private void CaptureSnapshot()
        {
            _initialAmountText = _amountText ?? "";
            _initialType = _type;
            _initialCategoryId = _selectedCategory?.Id;
            _initialDescription = _description ?? "";
            _initialPaymentType = _selectedPaymentType;
            _initialCreditCardId = _selectedCreditCard?.Id;
            _initialCounterpartyId = _selectedCounterparty?.Id;
            _initialDate = _date;
        }

        public bool HasUnsavedChanges =>
            (_amountText ?? "") != _initialAmountText
            || _type != _initialType
            || (_selectedCategory?.Id) != _initialCategoryId
            || (_description ?? "") != _initialDescription
            || _selectedPaymentType != _initialPaymentType
            || (_selectedCreditCard?.Id) != _initialCreditCardId
            || (_selectedCounterparty?.Id) != _initialCounterpartyId
            || _date != _initialDate;

        private async System.Threading.Tasks.Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _categoryService.GetByTypeAsync(Type, activeOnly: true);
                Categories = categories ?? new List<Category>();

                if (SelectedCategory == null || SelectedCategory.Type != Type)
                {
                    var defaultCategoryId = await _settingsService.GetDefaultCategoryIdAsync(Type);

                    if (defaultCategoryId.HasValue && Categories.Count > 0)
                        SelectedCategory = Categories.FirstOrDefault(c => c.Id == defaultCategoryId.Value);

                    if (SelectedCategory == null && Categories.Count > 0)
                        SelectedCategory = Categories.FirstOrDefault();
                }

                ValidateCategory();
            }
            catch (Exception ex)
            {
                Categories = new List<Category>();
                _dialogService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadCreditCardsAsync()
        {
            try
            {
                var cards = await _creditCardService.GetAllAsync();
                AvailableCreditCards = cards.Where(c => c.IsActive).ToList();

                if (_selectedPaymentType == PaymentType.CreditCard && _selectedCreditCard == null && _availableCreditCards.Count == 1)
                    SelectedCreditCard = _availableCreditCards[0];

                ValidateCreditCard();
            }
            catch (Exception ex)
            {
                AvailableCreditCards = new List<CreditCard>();
                _dialogService.ShowError($"Ошибка загрузки кредитных карт: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task LoadCounterpartiesAsync()
        {
            try
            {
                var counterparties = await _counterpartyService.GetActiveAsync().ConfigureAwait(false);
                AvailableCounterparties = counterparties.ToList();

                if (_selectedPaymentType == PaymentType.Debt && _selectedCounterparty == null && _availableCounterparties.Count == 1)
                    SelectedCounterparty = _availableCounterparties[0];

                ValidateCounterparty();
            }
            catch (Exception ex)
            {
                AvailableCounterparties = new List<Counterparty>();
                _dialogService.ShowError($"Ошибка загрузки контрагентов: {ex.Message}");
            }
        }

        private void OpenCategoryManager()
        {
            var vm = new CategoryManagerViewModel(_categoryService, _toastService);
            vm.OnClosed = async () =>
            {
                IsCategoryManagerOpen = false;
                CategoryManagerViewModel = null;
                await LoadCategoriesAsync();
            };
            CategoryManagerViewModel = vm;
            IsCategoryManagerOpen = true;
            RunAsync(vm.LoadAsync);
        }

        private void ValidateAmount()
        {
            if (_amount <= 0)
                SetErrors(nameof(Amount), new[] { "Сумма должна быть больше 0" });
            else if (_amount > 999_999_999)
                SetErrors(nameof(Amount), new[] { "Сумма слишком большая" });
            else
                ClearErrors(nameof(Amount));

            OnPropertyChanged(nameof(AmountError));
        }

        private void ValidateCategory()
        {
            if (SelectedCategory == null)
                SetErrors(nameof(SelectedCategory), new[] { "Выберите категорию" });
            else
                ClearErrors(nameof(SelectedCategory));
        }

        private void ValidateCreditCard()
        {
            if (_selectedPaymentType == PaymentType.CreditCard && _selectedCreditCard == null)
                SetErrors(nameof(SelectedCreditCard), new[] { "Выберите кредитную карту" });
            else
                ClearErrors(nameof(SelectedCreditCard));
        }

        private void ValidateCounterparty()
        {
            if (_selectedPaymentType == PaymentType.Debt && _selectedCounterparty == null)
                SetErrors(nameof(SelectedCounterparty), new[] { "Выберите контрагента" });
            else
                ClearErrors(nameof(SelectedCounterparty));
        }

        private bool _isSaving;

        private bool CanSave() => !_isSaving && !HasErrors && !string.IsNullOrEmpty(_amountText) && !IsRepayment;

        private async System.Threading.Tasks.Task SaveAsync()
        {
            if (_isSaving) return;

            ValidateAmount();
            ValidateCategory();
            ValidateCreditCard();
            ValidateCounterparty();

            if (HasErrors) return;

            _isSaving = true;
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveAndAddNextCommand as RelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                if (IsEditMode)
                {
                    _transaction.Date = Date;
                    _transaction.Amount = Amount;
                    _transaction.Type = Type;
                    _transaction.CategoryId = SelectedCategory.Id;
                    _transaction.Description = Description;
                    _transaction.PaymentType = SelectedPaymentType;
                    _transaction.CreditCardId = SelectedCreditCard?.Id;
                    _transaction.CounterpartyId = SelectedCounterparty?.Id;

                    await _transactionService.UpdateAsync(_transaction);
                }
                else
                {
                    var transaction = new Transaction
                    {
                        Date = Date,
                        Amount = Amount,
                        Type = Type,
                        CategoryId = SelectedCategory.Id,
                        Description = Description,
                        PaymentType = SelectedPaymentType,
                        CreditCardId = SelectedCreditCard?.Id,
                        CounterpartyId = SelectedCounterparty?.Id
                    };

                    await _transactionService.AddAsync(transaction);
                    SavedTransaction = transaction;
                }

                _toastService.ShowSuccess(IsEditMode ? "Операция сохранена" : "Операция добавлена");

                if (OnSaved != null)
                    await OnSaved();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                _isSaving = false;
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveAndAddNextCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private async System.Threading.Tasks.Task SaveAndAddNextAsync()
        {
            if (_isSaving) return;

            ValidateAmount();
            ValidateCategory();
            ValidateCreditCard();
            ValidateCounterparty();

            if (HasErrors) return;

            _isSaving = true;
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveAndAddNextCommand as RelayCommand)?.RaiseCanExecuteChanged();

            try
            {
                var transaction = new Transaction
                {
                    Date = Date,
                    Amount = Amount,
                    Type = Type,
                    CategoryId = SelectedCategory.Id,
                    Description = Description,
                    PaymentType = SelectedPaymentType,
                    CreditCardId = SelectedCreditCard?.Id,
                    CounterpartyId = SelectedCounterparty?.Id
                };

                await _transactionService.AddAsync(transaction);

                _toastService.ShowSuccess("Операция добавлена");

                if (OnSavedKeepOpen != null)
                    await OnSavedKeepOpen();

                // Сброс полей для следующей операции
                _amount = 0;
                AmountText = "";
                Description = string.Empty;
                ClearErrors(nameof(Amount));
                OnPropertyChanged(nameof(AmountError));
                Calculator.Clear();
                Calculator.IsOpen = false;
                CaptureSnapshot();

                RequestFocusAmount?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                _isSaving = false;
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveAndAddNextCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private System.Windows.Threading.DispatcherTimer _cancelToastTimer;
        private bool _isCancelToastVisible;
        public bool IsCancelToastVisible
        {
            get => _isCancelToastVisible;
            set => SetProperty(ref _isCancelToastVisible, value);
        }

        public ICommand ConfirmCancelCommand => new RelayCommand(_ =>
        {
            HideCancelToast();
            OnCancelled?.Invoke();
        });

        public ICommand DismissCancelToastCommand => new RelayCommand(_ => HideCancelToast());

        private void OnCancelToastTick(object sender, EventArgs e) => HideCancelToast();

        private void HideCancelToast()
        {
            IsCancelToastVisible = false;
            _cancelToastTimer?.Stop();
        }

        private void ShowCancelToast()
        {
            IsCancelToastVisible = true;

            if (_cancelToastTimer == null)
            {
                _cancelToastTimer = new System.Windows.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5)
                };
                _cancelToastTimer.Tick += OnCancelToastTick;
            }
            _cancelToastTimer.Stop();
            _cancelToastTimer.Start();
        }

        protected override void OnDispose()
        {
            if (_cancelToastTimer != null)
            {
                _cancelToastTimer.Stop();
                _cancelToastTimer.Tick -= OnCancelToastTick;
                _cancelToastTimer = null;
            }
            base.OnDispose();
        }

        private void Cancel()
        {
            if (!HasUnsavedChanges)
            {
                OnCancelled?.Invoke();
                return;
            }
            ShowCancelToast();
        }

        #endregion

        #region Events

        public Func<Task>? OnSaved { get; set; }
        public Func<Task>? OnSavedKeepOpen { get; set; }
        public Action? OnCancelled { get; set; }

        public event Action RequestFocusAmount;

        #endregion
    }
}
