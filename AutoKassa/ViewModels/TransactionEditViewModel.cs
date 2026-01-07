using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для формы добавления/редактирования операции
    /// </summary>
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
        private string _description;
        private List<Category> _categories;
        private string _amountError;

        private readonly ISettingsService _settingsService;

        public TransactionEditViewModel(
            ITransactionService transactionService,
            ICategoryService categoryService,
            IDialogService dialogService,
            ISettingsService settingsService)
        {
            _transactionService = transactionService;
            _categoryService = categoryService;
            _dialogService = dialogService;
            _settingsService = settingsService;

            // Значения по умолчанию из настроек
            Date = DateTime.Now;
            Type = _settingsService.GetDefaultOperationType();

            // Команды
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => Cancel());

            // Загрузка категорий
            _ = LoadCategoriesAsync();
        }

        #region Свойства

        /// <summary>
        /// Режим редактирования
        /// </summary>
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(Title));
                }
            }
        }

        /// <summary>
        /// Заголовок формы
        /// </summary>
        public string Title => IsEditMode ? "Редактирование операции" : "Новая операция";

        /// <summary>
        /// Дата операции
        /// </summary>
        public DateTime Date
        {
            get => _date;
            set => SetProperty(ref _date, value);
        }

        /// <summary>
        /// Сумма операции
        /// </summary>
        public decimal Amount
        {
            get => _amount;
            set
            {
                if (SetProperty(ref _amount, value))
                {
                    ValidateAmount();
                }
            }
        }

        /// <summary>
        /// Тип операции
        /// </summary>
        public OperationType Type
        {
            get => _type;
            set
            {
                if (SetProperty(ref _type, value))
                {
                    // При смене типа фильтруем категории
                    _ = LoadCategoriesAsync();
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
        /// Описание операции
        /// </summary>
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Список категорий (фильтруется по типу)
        /// </summary>
        public List<Category> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
        }

        /// <summary>
        /// Ошибка валидации суммы
        /// </summary>
        public string AmountError
        {
            get => _amountError;
            set => SetProperty(ref _amountError, value);
        }

        /// <summary>
        /// Доход выбран
        /// </summary>
        public bool IsIncome
        {
            get => Type == OperationType.Income;
            set
            {
                if (value)
                {
                    Type = OperationType.Income;
                }
            }
        }

        /// <summary>
        /// Расход выбран
        /// </summary>
        public bool IsExpense
        {
            get => Type == OperationType.Expense;
            set
            {
                if (value)
                {
                    Type = OperationType.Expense;
                }
            }
        }

        #endregion

        #region Команды

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Методы

        /// <summary>
        /// Инициализация для добавления
        /// </summary>
        public void InitializeForAdd()
        {
            IsEditMode = false;
            Date = DateTime.Now;
            Amount = 0;
            Type = OperationType.Expense;
            Description = string.Empty;
        }

        /// <summary>
        /// Инициализация для редактирования
        /// </summary>
        public void InitializeForEdit(Transaction transaction)
        {
            IsEditMode = true;
            _transaction = transaction;

            Date = transaction.Date;
            Amount = transaction.Amount;
            Type = transaction.Type;
            Description = transaction.Description;

            // Загрузим категории и выберем нужную
            _ = LoadCategoriesAsync().ContinueWith(t =>
            {
                SelectedCategory = Categories?.FirstOrDefault(c => c.Id == transaction.CategoryId);
            });
        }

        /// <summary>
        /// Загрузка категорий (фильтр по типу)
        /// </summary>
        private async System.Threading.Tasks.Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _categoryService.GetByTypeAsync(Type, activeOnly: true);
                Categories = categories;

                // Если категория не выбрана или не подходит по типу
                if (SelectedCategory == null || SelectedCategory.Type != Type)
                {
                    // Пытаемся установить категорию по умолчанию из настроек
                    var defaultCategoryId = _settingsService.GetDefaultCategoryId(Type);

                    if (defaultCategoryId.HasValue)
                    {
                        SelectedCategory = Categories.FirstOrDefault(c => c.Id == defaultCategoryId.Value);
                    }

                    // Если не нашли, выбираем первую
                    if (SelectedCategory == null)
                    {
                        SelectedCategory = Categories.FirstOrDefault();
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
        }

        /// <summary>
        /// Валидация суммы
        /// </summary>
        private void ValidateAmount()
        {
            if (Amount <= 0)
            {
                AmountError = "Сумма должна быть больше 0";
            }
            else if (Amount > 999999999)
            {
                AmountError = "Сумма слишком большая";
            }
            else
            {
                AmountError = null;
            }
        }

        /// <summary>
        /// Можно ли сохранить
        /// </summary>
        private bool CanSave()
        {
            return Amount > 0 &&
                   SelectedCategory != null &&
                   string.IsNullOrEmpty(AmountError);
        }

        /// <summary>
        /// Сохранить операцию
        /// </summary>
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
                    // Редактирование
                    _transaction.Date = Date;
                    _transaction.Amount = Amount;
                    _transaction.Type = Type;
                    _transaction.CategoryId = SelectedCategory.Id;
                    _transaction.Description = Description;

                    await _transactionService.UpdateAsync(_transaction);
                    _dialogService.ShowInfo("Операция успешно обновлена");
                }
                else
                {
                    // Добавление
                    var transaction = new Transaction
                    {
                        Date = Date,
                        Amount = Amount,
                        Type = Type,
                        CategoryId = SelectedCategory.Id,
                        Description = Description
                    };

                    await _transactionService.AddAsync(transaction);
                    _dialogService.ShowInfo("Операция успешно добавлена");
                }

                // Закрываем окно
                OnSaved?.Invoke();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка сохранения: {ex.Message}");
            }
        }

        /// <summary>
        /// Отмена
        /// </summary>
        private void Cancel()
        {
            OnCancelled?.Invoke();
        }

        #endregion

        #region События

        /// <summary>
        /// Событие успешного сохранения
        /// </summary>
        public Action OnSaved { get; set; }

        /// <summary>
        /// Событие отмены
        /// </summary>
        public Action OnCancelled { get; set; }

        #endregion
    }
}