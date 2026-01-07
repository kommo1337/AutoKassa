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
    /// ViewModel для экрана управления категориями
    /// </summary>
    public class CategoriesViewModel : ViewModelBase
    {
        private readonly ICategoryService _categoryService;
        private readonly IDialogService _dialogService;

        private ObservableCollection<Category> _incomeCategories;
        private ObservableCollection<Category> _expenseCategories;
        private Category _selectedCategory;
        private OperationType? _filterType;
        private bool _isLoading;

        public CategoriesViewModel(
            ICategoryService categoryService,
            IDialogService dialogService)
        {
            _categoryService = categoryService;
            _dialogService = dialogService;

            IncomeCategories = new ObservableCollection<Category>();
            ExpenseCategories = new ObservableCollection<Category>();

            // Команды
            LoadCommand = new RelayCommand(async _ => await LoadCategoriesAsync());
            AddCommand = new RelayCommand(_ => AddCategory());
            EditCommand = new RelayCommand(_ => EditCategory(), _ => SelectedCategory != null);
            DeleteCommand = new RelayCommand(async _ => await DeleteCategoryAsync(), _ => SelectedCategory != null);
            FilterCommand = new RelayCommand<string>(type => ApplyFilter(type));

            // Загрузка данных
            _ = LoadCategoriesAsync();
        }

        #region Свойства

        /// <summary>
        /// Категории доходов
        /// </summary>
        public ObservableCollection<Category> IncomeCategories
        {
            get => _incomeCategories;
            set => SetProperty(ref _incomeCategories, value);
        }

        /// <summary>
        /// Категории расходов
        /// </summary>
        public ObservableCollection<Category> ExpenseCategories
        {
            get => _expenseCategories;
            set => SetProperty(ref _expenseCategories, value);
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
        /// Фильтр по типу
        /// </summary>
        public OperationType? FilterType
        {
            get => _filterType;
            set
            {
                if (SetProperty(ref _filterType, value))
                {
                    OnPropertyChanged(nameof(ShowIncomeCategories));
                    OnPropertyChanged(nameof(ShowExpenseCategories));
                }
            }
        }

        /// <summary>
        /// Показывать категории доходов
        /// </summary>
        public bool ShowIncomeCategories => FilterType == null || FilterType == OperationType.Income;

        /// <summary>
        /// Показывать категории расходов
        /// </summary>
        public bool ShowExpenseCategories => FilterType == null || FilterType == OperationType.Expense;

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

        public ICommand LoadCommand { get; }
        public ICommand AddCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand FilterCommand { get; }

        #endregion

        #region Методы

        /// <summary>
        /// Загрузка категорий
        /// </summary>
        private async Task LoadCategoriesAsync()
        {
            try
            {
                IsLoading = true;

                var categories = await _categoryService.GetAllAsync();

                // Группируем по типам
                var incomeCategories = categories.Where(c => c.Type == OperationType.Income).ToList();
                var expenseCategories = categories.Where(c => c.Type == OperationType.Expense).ToList();

                // Загружаем количество операций для каждой категории
                foreach (var category in categories)
                {
                    // Сохраняем количество операций в Tag (или создайте отдельное свойство)
                    var count = await _categoryService.GetOperationCountAsync(category.Id);
                    // Можно использовать расширенную модель или dynamic свойство
                }

                IncomeCategories.Clear();
                foreach (var cat in incomeCategories)
                {
                    IncomeCategories.Add(cat);
                }

                ExpenseCategories.Clear();
                foreach (var cat in expenseCategories)
                {
                    ExpenseCategories.Add(cat);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки категорий: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Применить фильтр
        /// </summary>
        private void ApplyFilter(string type)
        {
            FilterType = type switch
            {
                "Income" => OperationType.Income,
                "Expense" => OperationType.Expense,
                _ => null
            };
        }

        /// <summary>
        /// Добавить категорию
        /// </summary>
        private void AddCategory()
        {
            var settingsService = App.GetService<ISettingsService>();
            var viewModel = new CategoryEditViewModel(_categoryService, _dialogService);
            viewModel.InitializeForAdd();

            var window = new CategoryEditView(viewModel)
            {
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() == true)
            {
                _ = LoadCategoriesAsync();
            }
        }

        /// <summary>
        /// Редактировать категорию
        /// </summary>
        private void EditCategory()
        {
            // Получаем категорию из CommandParameter
            var category = SelectedCategory;
            if (category == null) return;

            var viewModel = new CategoryEditViewModel(_categoryService, _dialogService);
            viewModel.InitializeForEdit(category);

            var window = new CategoryEditView(viewModel)
            {
                Owner = Application.Current.MainWindow
            };

            if (window.ShowDialog() == true)
            {
                _ = LoadCategoriesAsync();
            }
        }

        /// <summary>
        /// Удалить или деактивировать категорию
        /// </summary>
        private async System.Threading.Tasks.Task DeleteCategoryAsync()
        {
            var category = SelectedCategory;
            if (category == null) return;

            // Проверяем количество операций
            var operationCount = await _categoryService.GetOperationCountAsync(category.Id);

            if (operationCount > 0)
            {
                // Есть операции - предлагаем деактивацию
                var result = _dialogService.ShowConfirmation(
                    $"Категория \"{category.Name}\" используется в {operationCount} операциях.\n\n" +
                    "Удалить категорию нельзя, но можно деактивировать её.\n" +
                    "Деактивированная категория не будет отображаться в списках выбора.\n\n" +
                    "Деактивировать категорию?",
                    "Деактивация категории"
                );

                if (result)
                {
                    try
                    {
                        await _categoryService.DeactivateAsync(category.Id);
                        _dialogService.ShowInfo("Категория деактивирована");
                        await LoadCategoriesAsync();
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowError($"Ошибка деактивации: {ex.Message}");
                    }
                }
            }
            else
            {
                // Нет операций - можно удалить
                var result = _dialogService.ShowConfirmation(
                    $"Вы уверены, что хотите удалить категорию \"{category.Name}\"?",
                    "Подтверждение удаления"
                );

                if (result)
                {
                    try
                    {
                        var deleted = await _categoryService.DeleteAsync(category.Id);
                        if (deleted)
                        {
                            _dialogService.ShowInfo("Категория удалена");
                            await LoadCategoriesAsync();
                        }
                        else
                        {
                            _dialogService.ShowError("Не удалось удалить категорию");
                        }
                    }
                    catch (Exception ex)
                    {
                        _dialogService.ShowError($"Ошибка удаления: {ex.Message}");
                    }
                }
            }
        }

        #endregion
    }
}