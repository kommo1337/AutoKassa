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
        private OperationType _type;
        private Category _selectedCategory;
        private string _description = string.Empty;
        private List<Category> _categories = new List<Category>();
        private string _amountError;
        private bool _isInitialized;
        private PaymentType _selectedPaymentType = PaymentType.Cash;
        private bool _showAllCategories;

        // Calculator state
        private bool _isCalcOpen;
        private string _calcDisplay = "";
        private string _calcCurrentInput = "";
        private decimal _calcLeft;
        private string _calcOp;
        private bool _calcWaiting;

        private readonly ISettingsService _settingsService;
        private readonly IToastNotificationService _toastService;

        // Category manager
        private bool _isCategoryManagerOpen;
        private CategoryManagerViewModel _categoryManagerViewModel;

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

            // Calculator commands
            ToggleCalcCommand = new RelayCommand(_ => { IsCalcOpen = !IsCalcOpen; if (IsCalcOpen) CalcClear(); });
            CalcDigitCommand = new RelayCommand(p => { if (p is string d) CalcDigit(d); });
            CalcOpCommand = new RelayCommand(p => { if (p is string op) CalcOp(op); });
            CalcEqualsCommand = new RelayCommand(_ => CalcEquals());
            CalcClearCommand = new RelayCommand(_ => CalcClear());
            CalcBackspaceCommand = new RelayCommand(_ => CalcBackspace());
            OpenCategoryManagerCommand = new RelayCommand(_ => OpenCategoryManager());

            Date = DateTime.Now;
            _type = _settingsService.GetDefaultOperationType();

            _ = InitializeAsync();
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

        public decimal Amount
        {
            get => _amount;
            set
            {
                if (SetProperty(ref _amount, value))
                    ValidateAmount();
            }
        }

        public OperationType Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    if (_isInitialized)
                        _ = LoadCategoriesAsync();
                    OnPropertyChanged(nameof(IsIncome));
                    OnPropertyChanged(nameof(IsExpense));
                    OnPropertyChanged(nameof(SaveButtonText));
                }
            }
        }

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
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

        public string AmountError
        {
            get => _amountError;
            set => SetProperty(ref _amountError, value);
        }

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

        // === Calculator properties ===

        public bool IsCalcOpen
        {
            get => _isCalcOpen;
            set => SetProperty(ref _isCalcOpen, value);
        }

        public string CalcDisplay
        {
            get => _calcDisplay;
            set => SetProperty(ref _calcDisplay, value);
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
        public ICommand ToggleCalcCommand { get; }
        public ICommand CalcDigitCommand { get; }
        public ICommand CalcOpCommand { get; }
        public ICommand CalcEqualsCommand { get; }
        public ICommand CalcClearCommand { get; }
        public ICommand CalcBackspaceCommand { get; }
        public ICommand OpenCategoryManagerCommand { get; }

        #endregion

        #region Methods

        public void InitializeForAdd()
        {
            IsEditMode = false;
            Date = DateTime.Now;
            Amount = 0;
            Type = OperationType.Expense;
            Description = string.Empty;
            SelectedPaymentType = PaymentType.Cash;
            ShowAllCategories = false;
            CalcClear();
            IsCalcOpen = false;
        }

        public async void InitializeForEdit(Transaction transaction)
        {
            IsEditMode = true;
            _transaction = transaction;

            Date = transaction.Date;
            Amount = transaction.Amount;
            Type = transaction.Type;
            Description = transaction.Description;
            SelectedPaymentType = transaction.PaymentType;
            ShowAllCategories = false;
            CalcClear();
            IsCalcOpen = false;

            await LoadCategoriesAsync();

            if (Categories != null && Categories.Count > 0)
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == transaction.CategoryId);
        }

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
            _ = vm.LoadAsync();
        }

        private void ValidateAmount()
        {
            if (Amount <= 0)
                AmountError = "Сумма должна быть больше 0";
            else if (Amount > 999999999)
                AmountError = "Сумма слишком большая";
            else
                AmountError = null;
        }

        private bool CanSave()
        {
            return Amount > 0 &&
                   SelectedCategory != null &&
                   string.IsNullOrEmpty(AmountError);
        }

        private async System.Threading.Tasks.Task SaveAsync()
        {
            ValidateAmount();

            if (!CanSave())
            {
                _dialogService.ShowError("Пожалуйста, исправьте ошибки в форме");
                return;
            }

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

                OnSaved?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка сохранения: {ex.Message}");
            }
        }

        private void Cancel()
        {
            OnCancelled?.Invoke();
        }

        #endregion

        #region Calculator logic

        private void CalcDigit(string digit)
        {
            if (_calcWaiting)
            {
                _calcCurrentInput = digit == "." ? "0." : digit;
                _calcWaiting = false;
            }
            else
            {
                if (digit == "." && _calcCurrentInput.Contains('.'))
                    return; // already has decimal point
                if (_calcCurrentInput == "0" && digit != ".")
                    _calcCurrentInput = digit;
                else
                    _calcCurrentInput += digit;
            }
            UpdateCalcDisplay();
        }

        private void CalcOp(string op)
        {
            if (decimal.TryParse(_calcCurrentInput.Replace(',', '.'),
                NumberStyles.Any, CultureInfo.InvariantCulture, out decimal val))
            {
                if (_calcOp != null && !_calcWaiting)
                    _calcLeft = ComputeCalc(_calcLeft, val, _calcOp);
                else
                    _calcLeft = val;
            }
            _calcOp = op;
            _calcWaiting = true;
            UpdateCalcDisplay();
        }

        private void CalcEquals()
        {
            if (_calcOp == null)
            {
                if (decimal.TryParse(_calcCurrentInput.Replace(',', '.'),
                    NumberStyles.Any, CultureInfo.InvariantCulture, out decimal v))
                {
                    Amount = v;
                    IsCalcOpen = false;
                }
                return;
            }

            if (!decimal.TryParse(_calcCurrentInput.Replace(',', '.'),
                NumberStyles.Any, CultureInfo.InvariantCulture, out decimal right))
                return;

            var result = ComputeCalc(_calcLeft, right, _calcOp);
            CalcDisplay = $"{_calcLeft} {_calcOp} {right} = {result}";
            Amount = result;
            _calcLeft = result;
            _calcOp = null;
            _calcCurrentInput = result.ToString(CultureInfo.InvariantCulture);
            _calcWaiting = false;
            IsCalcOpen = false;
        }

        private void UpdateCalcDisplay()
        {
            if (_calcOp == null)
                CalcDisplay = _calcCurrentInput;
            else if (_calcWaiting)
                CalcDisplay = $"{_calcLeft} {_calcOp}";
            else
                CalcDisplay = $"{_calcLeft} {_calcOp} {_calcCurrentInput}";
        }

        private decimal ComputeCalc(decimal left, decimal right, string op) => op switch
        {
            "+" => left + right,
            "-" => left - right,
            "×" => left * right,
            "÷" => right != 0 ? Math.Round(left / right, 2) : 0,
            _ => right
        };

        private void CalcClear()
        {
            _calcCurrentInput = "";
            _calcLeft = 0;
            _calcOp = null;
            _calcWaiting = false;
            CalcDisplay = "";
        }

        private void CalcBackspace()
        {
            if (_calcCurrentInput.Length > 0)
            {
                _calcCurrentInput = _calcCurrentInput[..^1];
                UpdateCalcDisplay();
            }
        }

        #endregion

        #region Events

        public Action OnSaved { get; set; }
        public Action OnCancelled { get; set; }

        #endregion
    }
}
