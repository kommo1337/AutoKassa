using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Views;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для экрана списка операций
    /// </summary>
    public class TransactionsViewModel : ViewModelBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ICategoryService _categoryService;
        private readonly IDialogService _dialogService;

        private ObservableCollection<Transaction> _transactions;
        private ObservableCollection<Category> _categories;
        private Transaction _selectedTransaction;
        private bool _isLoading;
        private int _totalCount;
        private int _currentPage;
        private int _pageSize = 100;

        // Фильтры
        private DateTime? _dateFrom;
        private DateTime? _dateTo;
        private OperationType? _selectedType;
        private Category _selectedCategory;
        private string _searchText;
        private readonly ISettingsService _settingsService;

        public TransactionsViewModel(
            ITransactionService transactionService,
            ICategoryService categoryService,
            IDialogService dialogService,
            ISettingsService settingsService)
        {
            _transactionService = transactionService;
            _categoryService = categoryService;
            _dialogService = dialogService;
            _settingsService = settingsService;

            Transactions = new ObservableCollection<Transaction>();
            Categories = new ObservableCollection<Category>();

            // Команды
            LoadCommand = new RelayCommand(async _ => await LoadDataAsync());
            AddCommand = new RelayCommand(_ => AddTransaction());
            EditCommand = new RelayCommand(_ => EditTransaction(), _ => SelectedTransaction != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteTransactionAsync(), _ => SelectedTransaction != null);
            ApplyFiltersCommand = new RelayCommand(async _ => await ApplyFiltersAsync());
            ResetFiltersCommand = new RelayCommand(async _ => await ResetFiltersAsync());
            LoadMoreCommand = new RelayCommand(async _ => await LoadMoreAsync());

            // Установка фильтров по умолчанию (текущий месяц)
            DateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            DateTo = DateTime.Now;

            // Загрузка данных
            _ = InitializeAsync();
        }

        #region Свойства

        /// <summary>
        /// Список операций
        /// </summary>
        public ObservableCollection<Transaction> Transactions
        {
            get => _transactions;
            set => SetProperty(ref _transactions, value);
        }

        /// <summary>
        /// Список категорий для фильтра
        /// </summary>
        public ObservableCollection<Category> Categories
        {
            get => _categories;
            set => SetProperty(ref _categories, value);
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
        /// Идет загрузка данных
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Общее количество записей
        /// </summary>
        public int TotalCount
        {
            get => _totalCount;
            set
            {
                if (SetProperty(ref _totalCount, value))
                {
                    OnPropertyChanged(nameof(DisplayInfo));
                }
            }
        }

        /// <summary>
        /// Информация о количестве отображаемых записей
        /// </summary>
        public string DisplayInfo => $"Показано {Transactions.Count} из {TotalCount} записей";

        /// <summary>
        /// Можно ли загрузить еще записи
        /// </summary>
        public bool CanLoadMore => Transactions.Count < TotalCount;

        #endregion

        #region Фильтры

        /// <summary>
        /// Дата начала периода
        /// </summary>
        public DateTime? DateFrom
        {
            get => _dateFrom;
            set => SetProperty(ref _dateFrom, value);
        }

        /// <summary>
        /// Дата окончания периода
        /// </summary>
        public DateTime? DateTo
        {
            get => _dateTo;
            set => SetProperty(ref _dateTo, value);
        }

        /// <summary>
        /// Выбранный тип операции (null = все)
        /// </summary>
        public OperationType? SelectedType
        {
            get => _selectedType;
            set => SetProperty(ref _selectedType, value);
        }

        /// <summary>
        /// Выбранная категория (null = все)
        /// </summary>
        public Category SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

        /// <summary>
        /// Текст поиска по описанию
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        /// <summary>
        /// Список типов операций для фильтра
        /// </summary>
        public OperationType?[] OperationTypes => new OperationType?[]
        {
            null, // "Все"
            OperationType.Income,
            OperationType.Expense
        };

        #endregion

        #region Команды

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ApplyFiltersCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand LoadMoreCommand { get; }

        #endregion

        #region Методы

        /// <summary>
        /// Инициализация (загрузка категорий и данных)
        /// </summary>
        private async Task InitializeAsync()
        {
            await LoadCategoriesAsync();
            await LoadDataAsync();
        }

        /// <summary>
        /// Загрузка категорий для фильтра
        /// </summary>
        private async Task LoadCategoriesAsync()
        {
            try
            {
                var categories = await _categoryService.GetActiveAsync();

                Categories.Clear();
                Categories.Add(new Category { Id = 0, Name = "Все категории" }); // Заглушка для "Все"

                foreach (var category in categories)
                {
                    Categories.Add(category);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
        }

        /// <summary>
        /// Загрузка данных с учетом фильтров
        /// </summary>
        private async Task LoadDataAsync()
        {
            try
            {
                IsLoading = true;
                _currentPage = 0;

                var filters = BuildFilterParameters();

                var transactions = await _transactionService.GetTransactionsAsync(filters);
                var totalCount = await _transactionService.GetTotalCountAsync(filters);

                Transactions.Clear();
                foreach (var transaction in transactions)
                {
                    Transactions.Add(transaction);
                }

                TotalCount = totalCount;
                OnPropertyChanged(nameof(CanLoadMore));
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
        /// Загрузить еще записи (Lazy Loading)
        /// </summary>
        private async Task LoadMoreAsync()
        {
            if (!CanLoadMore || IsLoading) return;

            try
            {
                IsLoading = true;
                _currentPage++;

                var filters = BuildFilterParameters();
                filters.Skip = _currentPage * _pageSize;

                var transactions = await _transactionService.GetTransactionsAsync(filters);

                foreach (var transaction in transactions)
                {
                    Transactions.Add(transaction);
                }

                OnPropertyChanged(nameof(DisplayInfo));
                OnPropertyChanged(nameof(CanLoadMore));
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
        /// Построить параметры фильтрации
        /// </summary>
        private TransactionFilterParameters BuildFilterParameters()
        {
            return new TransactionFilterParameters
            {
                DateFrom = DateFrom,
                DateTo = DateTo,
                Type = SelectedType,
                CategoryId = SelectedCategory?.Id > 0 ? SelectedCategory.Id : (int?)null,
                SearchText = SearchText,
                Skip = _currentPage * _pageSize,
                Take = _pageSize,
                SortBy = "Date",
                SortDescending = true
            };
        }

        /// <summary>
        /// Применить фильтры
        /// </summary>
        private async Task ApplyFiltersAsync()
        {
            await LoadDataAsync();
        }

        /// <summary>
        /// Сбросить фильтры
        /// </summary>
        private async Task ResetFiltersAsync()
        {
            DateFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            DateTo = DateTime.Now;
            SelectedType = null;
            SelectedCategory = Categories.FirstOrDefault();
            SearchText = string.Empty;

            await LoadDataAsync();
        }

        /// <summary>
        /// Добавить операцию
        /// </summary>
        /// <summary>
        /// Добавить операцию
        /// </summary>
        private void AddTransaction()
        {
            var viewModel = new TransactionEditViewModel(_transactionService, _categoryService, _dialogService, _settingsService);
            viewModel.InitializeForAdd();

            var window = new TransactionEditView(viewModel)
            {
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() == true)
            {
                // Перезагружаем данные
                _ = LoadDataAsync();
            }
        }

        /// <summary>
        /// Редактировать операцию
        /// </summary>
        private void EditTransaction()
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
                // Перезагружаем данные
                _ = LoadDataAsync();
            }
        }

        /// <summary>
        /// Удалить операцию
        /// </summary>
        private async Task DeleteTransactionAsync()
        {
            if (SelectedTransaction == null) return;

            var result = _dialogService.ShowConfirmation(
                $"Вы уверены, что хотите удалить операцию?\n\n" +
                $"Дата: {SelectedTransaction.Date:dd.MM.yyyy}\n" +
                $"Сумма: {SelectedTransaction.Amount:N2} ₽\n" +
                $"Категория: {SelectedTransaction.Category?.Name}",
                "Подтверждение удаления"
            );

            if (!result) return;

            try
            {
                await _transactionService.DeleteAsync(SelectedTransaction.Id);
                Transactions.Remove(SelectedTransaction);
                TotalCount--;

                _dialogService.ShowInfo("Операция успешно удалена");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка удаления: {ex.Message}");
            }
        }

        #endregion
    }
}