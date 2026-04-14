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
        private PaymentType _selectedPaymentType = PaymentType.Cash;
        private bool _showAllCategories;

        public CalculatorViewModel Calculator { get; }

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
        private DateTime _initialDate;

        public TransactionEditViewModel(
            ITransactionService transactionService,
            ICategoryService categoryService,
            IDialogService dialogService,
            ISettingsService settingsService,
            IToastNotificationService toastService)
        {
            _transactionService = transactionService;
            _categoryService = categoryService;
            _dialogService = dialogService;
            _settingsService = settingsService;
            _toastService = toastService;

            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => Cancel());
            SelectCashCommand = new RelayCommand(_ => SelectedPaymentType = PaymentType.Cash);
            SelectNonCashCommand = new RelayCommand(_ => SelectedPaymentType = PaymentType.NonCash);
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
        public ICommand CancelCommand { get; }
        public ICommand SelectCashCommand { get; }
        public ICommand SelectNonCashCommand { get; }
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
            Date = DateTime.Now;
            _amount = 0;
            _amountText = "";
            OnPropertyChanged(nameof(AmountText));
            ClearErrors(nameof(Amount));
            OnPropertyChanged(nameof(AmountError));
            Type = OperationType.Expense;
            Description = string.Empty;
            SelectedPaymentType = PaymentType.Cash;
            ShowAllCategories = false;
            Calculator.Clear();
            Calculator.IsOpen = false;
            ValidateCategory();
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
            ShowAllCategories = false;
            Calculator.Clear();
            Calculator.IsOpen = false;

            RunAsync(async () =>
            {
                await LoadCategoriesAsync();

                if (Categories != null && Categories.Count > 0)
                    SelectedCategory = Categories.FirstOrDefault(c => c.Id == transaction.CategoryId);

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
            _initialDate = _date;
        }

        public bool HasUnsavedChanges =>
            (_amountText ?? "") != _initialAmountText
            || _type != _initialType
            || (_selectedCategory?.Id) != _initialCategoryId
            || (_description ?? "") != _initialDescription
            || _selectedPaymentType != _initialPaymentType
            || _date != _initialDate;

        private async System.Threading.Tasks.Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _categoryService.GetByTypeAsync(Type, activeOnly: true);
                Categories = categories ?? new List<Category>();

                if (SelectedCategory == null || SelectedCategory.Type != Type)
                {
                    var defaultCategoryId = _settingsService.GetDefaultCategoryId(Type);

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

        private bool CanSave() => !HasErrors && !string.IsNullOrEmpty(_amountText);

        private async System.Threading.Tasks.Task SaveAsync()
        {
            ValidateAmount();
            ValidateCategory();

            if (HasErrors) return;

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
                        PaymentType = SelectedPaymentType
                    };

                    await _transactionService.AddAsync(transaction);
                }

                _toastService.ShowSuccess(IsEditMode ? "Операция сохранена" : "Операция добавлена");
                OnSaved?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка сохранения: {ex.Message}");
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
                _cancelToastTimer.Tick += (s, e) => HideCancelToast();
            }
            _cancelToastTimer.Stop();
            _cancelToastTimer.Start();
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

        public Action OnSaved { get; set; }
        public Action OnCancelled { get; set; }

        #endregion
    }
}
