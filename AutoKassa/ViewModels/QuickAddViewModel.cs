using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для быстрого добавления операции (панель на Dashboard)
    /// </summary>
    public class QuickAddViewModel : ViewModelBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;

        /// <summary>
        /// Вызывается после успешного добавления операции
        /// </summary>
        public Action OnTransactionAdded { get; set; }

        #region Поля

        private decimal _quickAmount;
        private bool _isIncome;
        private Category _selectedCategory;
        private ObservableCollection<Category> _filteredCategories;
        private DateTime _quickDate = DateTime.Today;
        private string _quickDescription;
        private bool _isDescriptionVisible;
        private bool _isDateVisible;
        private string _quickAmountText;

        #endregion

        public QuickAddViewModel(
            ITransactionService transactionService,
            ICategoryService categoryService,
            IDialogService dialogService,
            IToastNotificationService toastService)
        {
            _transactionService = transactionService;
            _categoryService = categoryService;
            _dialogService = dialogService;
            _toastService = toastService;

            FilteredCategories = new ObservableCollection<Category>();

            AddQuickTransactionCommand  = new RelayCommand(async _ => await AddAsync(), _ => CanAdd());
            ToggleTransactionTypeCommand = new RelayCommand(_ => IsIncome = !IsIncome);
            ToggleDescriptionCommand     = new RelayCommand(_ => IsDescriptionVisible = !IsDescriptionVisible);
            ToggleDateCommand            = new RelayCommand(_ => IsDateVisible = !IsDateVisible);
        }

        #region Свойства

        public string QuickAmountText
        {
            get => _quickAmountText;
            set
            {
                if (SetProperty(ref _quickAmountText, value))
                    _quickAmount = decimal.TryParse(value?.Replace(" ", "").Replace("₽", ""), out var v) ? v : 0;
            }
        }

        public decimal QuickAmount
        {
            get => _quickAmount;
            set => SetProperty(ref _quickAmount, value);
        }

        public bool IsIncome
        {
            get => _isIncome;
            set
            {
                if (SetProperty(ref _isIncome, value))
                    _ = LoadCategoriesAsync();
            }
        }

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        public ObservableCollection<Category> FilteredCategories
        {
            get => _filteredCategories;
            set => SetProperty(ref _filteredCategories, value);
        }

        public DateTime QuickDate
        {
            get => _quickDate;
            set => SetProperty(ref _quickDate, value);
        }

        public string QuickDescription
        {
            get => _quickDescription;
            set => SetProperty(ref _quickDescription, value);
        }

        public bool IsDescriptionVisible
        {
            get => _isDescriptionVisible;
            set => SetProperty(ref _isDescriptionVisible, value);
        }

        public bool IsDateVisible
        {
            get => _isDateVisible;
            set => SetProperty(ref _isDateVisible, value);
        }

        #endregion

        #region Команды

        public ICommand AddQuickTransactionCommand  { get; }
        public ICommand ToggleTransactionTypeCommand { get; }
        public ICommand ToggleDescriptionCommand     { get; }
        public ICommand ToggleDateCommand            { get; }

        #endregion

        /// <summary>
        /// Загрузка категорий при инициализации
        /// </summary>
        public async Task InitializeAsync() => await LoadCategoriesAsync();

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var type = IsIncome ? OperationType.Income : OperationType.Expense;
                var categories = await _categoryService.GetByTypeAsync(type);

                FilteredCategories.Clear();
                foreach (var c in categories)
                    FilteredCategories.Add(c);

                SelectedCategory = FilteredCategories.FirstOrDefault();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
        }

        private bool CanAdd() => _quickAmount > 0 && SelectedCategory != null;

        private async Task AddAsync()
        {
            try
            {
                var transaction = new Transaction
                {
                    Amount      = _quickAmount,
                    Type        = IsIncome ? OperationType.Income : OperationType.Expense,
                    CategoryId  = SelectedCategory.Id,
                    Date        = QuickDate,
                    Description = QuickDescription ?? string.Empty,
                    CreatedAt   = DateTime.Now,
                    IsDeleted   = false
                };

                await _transactionService.AddAsync(transaction);
                _toastService.ShowSuccess("Операция добавлена успешно");

                ClearForm();
                OnTransactionAdded?.Invoke();
            }
            catch (Exception ex)
            {
                _toastService.ShowError($"Ошибка: {ex.Message}");
            }
        }

        private void ClearForm()
        {
            QuickAmountText        = string.Empty;
            QuickAmount            = 0;
            QuickDescription       = string.Empty;
            QuickDate              = DateTime.Today;
            IsDescriptionVisible   = false;
            IsDateVisible          = false;
        }
    }
}
