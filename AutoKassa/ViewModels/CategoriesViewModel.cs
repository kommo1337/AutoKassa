using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    public class CategoriesViewModel : ViewModelBase
    {
        private readonly ICategoryService _categoryService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;

        private ObservableCollection<Category> _incomeCategories;
        private ObservableCollection<Category> _expenseCategories;
        private Category _selectedCategory;
        private OperationType? _filterType;
        private bool _isLoading;
        private bool _isModalOpen;
        private CategoryManagerViewModel _managerViewModel;

        public CategoriesViewModel(
            ICategoryService categoryService,
            IDialogService dialogService,
            IToastNotificationService toastService)
        {
            _categoryService = categoryService;
            _dialogService = dialogService;
            _toastService = toastService;

            IncomeCategories = new ObservableCollection<Category>();
            ExpenseCategories = new ObservableCollection<Category>();

            LoadCommand = new RelayCommand(async _ => await LoadCategoriesAsync());
            OpenManagerCommand = new RelayCommand(_ => OpenManager());
            DeleteCommand = new RelayCommand(async _ => await DeleteCategoryAsync(), _ => SelectedCategory != null);
            FilterCommand = new RelayCommand<string>(type => ApplyFilter(type));

            RunAsync(LoadCategoriesAsync);
        }

        #region Properties

        public ObservableCollection<Category> IncomeCategories
        {
            get => _incomeCategories;
            set => SetProperty(ref _incomeCategories, value);
        }

        public ObservableCollection<Category> ExpenseCategories
        {
            get => _expenseCategories;
            set => SetProperty(ref _expenseCategories, value);
        }

        public Category SelectedCategory
        {
            get => _selectedCategory;
            set => SetProperty(ref _selectedCategory, value);
        }

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

        public bool ShowIncomeCategories => FilterType == null || FilterType == OperationType.Income;
        public bool ShowExpenseCategories => FilterType == null || FilterType == OperationType.Expense;

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool IsModalOpen
        {
            get => _isModalOpen;
            set => SetProperty(ref _isModalOpen, value);
        }

        public CategoryManagerViewModel ManagerViewModel
        {
            get => _managerViewModel;
            set => SetProperty(ref _managerViewModel, value);
        }

        #endregion

        #region Commands

        public ICommand LoadCommand { get; }
        public ICommand OpenManagerCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand FilterCommand { get; }

        #endregion

        #region Methods

        private async Task LoadCategoriesAsync()
        {
            try
            {
                IsLoading = true;

                var categories = await _categoryService.GetAllAsync();

                var incomeCategories = categories.Where(c => c.Type == OperationType.Income).ToList();
                var expenseCategories = categories.Where(c => c.Type == OperationType.Expense).ToList();

                IncomeCategories.Clear();
                foreach (var cat in incomeCategories)
                    IncomeCategories.Add(cat);

                ExpenseCategories.Clear();
                foreach (var cat in expenseCategories)
                    ExpenseCategories.Add(cat);
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

        private void ApplyFilter(string type)
        {
            FilterType = type switch
            {
                "Income" => OperationType.Income,
                "Expense" => OperationType.Expense,
                _ => null
            };
        }

        private void OpenManager()
        {
            var vm = new CategoryManagerViewModel(_categoryService, _toastService);
            vm.OnClosed = () =>
            {
                IsModalOpen = false;
                ManagerViewModel = null;
                RunAsync(LoadCategoriesAsync);
            };
            ManagerViewModel = vm;
            IsModalOpen = true;
            RunAsync(vm.LoadAsync);
        }

        private async Task DeleteCategoryAsync()
        {
            var category = SelectedCategory;
            if (category == null) return;

            var operationCount = await _categoryService.GetOperationCountAsync(category.Id);

            if (operationCount > 0)
            {
                try
                {
                    await _categoryService.DeactivateAsync(category.Id);
                    await LoadCategoriesAsync();
                    _toastService.ShowInfo($"Категория \u00ab{category.Name}\u00bb скрыта из списков выбора");
                }
                catch (Exception ex)
                {
                    _dialogService.ShowError($"Ошибка деактивации: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    var deleted = await _categoryService.DeleteAsync(category.Id);
                    if (deleted)
                    {
                        await LoadCategoriesAsync();
                        _toastService.ShowDeleteWithUndo(
                            $"Категория \u00ab{category.Name}\u00bb удалена",
                            async () =>
                            {
                                var restored = new Category
                                {
                                    Name = category.Name,
                                    Type = category.Type,
                                    Color = category.Color,
                                    IsActive = true
                                };
                                await _categoryService.AddAsync(restored);
                                await LoadCategoriesAsync();
                            });
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

        #endregion
    }
}
