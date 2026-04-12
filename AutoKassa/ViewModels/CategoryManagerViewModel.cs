using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    public class CategoryManagerViewModel : ViewModelBase
    {
        private readonly ICategoryService _categoryService;
        private readonly IToastNotificationService _toastService;
        private static readonly Random _random = new();

        public CategoryManagerViewModel(ICategoryService categoryService, IToastNotificationService toastService)
        {
            _categoryService = categoryService;
            _toastService = toastService;

            IncomeCategories = new ObservableCollection<CategoryItemViewModel>();
            ExpenseCategories = new ObservableCollection<CategoryItemViewModel>();

            AddIncomeCategoryCommand = new RelayCommand(async _ => await AddCategoryAsync(OperationType.Income));
            AddExpenseCategoryCommand = new RelayCommand(async _ => await AddCategoryAsync(OperationType.Expense));
            CloseCommand = new RelayCommand(_ => OnClosed?.Invoke());
        }

        public ObservableCollection<CategoryItemViewModel> IncomeCategories { get; }
        public ObservableCollection<CategoryItemViewModel> ExpenseCategories { get; }

        public ICommand AddIncomeCategoryCommand { get; }
        public ICommand AddExpenseCategoryCommand { get; }
        public ICommand CloseCommand { get; }

        public Action OnClosed { get; set; }

        public async Task LoadAsync()
        {
            IncomeCategories.Clear();
            ExpenseCategories.Clear();

            var all = await _categoryService.GetAllAsync();

            foreach (var cat in all.Where(c => c.Type == OperationType.Income))
            {
                IncomeCategories.Add(new CategoryItemViewModel(cat, _categoryService, RemoveCategory));
            }

            foreach (var cat in all.Where(c => c.Type == OperationType.Expense))
            {
                ExpenseCategories.Add(new CategoryItemViewModel(cat, _categoryService, RemoveCategory));
            }
        }

        private async Task AddCategoryAsync(OperationType type)
        {
            var collection = type == OperationType.Income ? IncomeCategories : ExpenseCategories;
            var maxSort = collection.Any() ? collection.Max(c => c.Model.SortOrder) : 0;
            var color = CategoryItemViewModel.PresetColors[_random.Next(CategoryItemViewModel.PresetColors.Count)];

            var category = new Category
            {
                Name = "Новая категория",
                Type = type,
                Color = color,
                SortOrder = maxSort + 1,
                IsSystem = false
            };

            await _categoryService.AddAsync(category);

            var item = new CategoryItemViewModel(category, _categoryService, RemoveCategory);
            collection.Add(item);
            item.IsRenaming = true;
        }

        public async Task MoveCategoryAsync(CategoryItemViewModel source, CategoryItemViewModel target, OperationType type)
        {
            var collection = type == OperationType.Income ? IncomeCategories : ExpenseCategories;

            var oldIndex = collection.IndexOf(source);
            var newIndex = collection.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;

            collection.Move(oldIndex, newIndex);

            var updates = new List<(int Id, int SortOrder)>();
            for (int i = 0; i < collection.Count; i++)
            {
                collection[i].Model.SortOrder = i + 1;
                updates.Add((collection[i].Model.Id, i + 1));
            }

            await _categoryService.ReorderAsync(updates);
        }

        private void RemoveCategory(CategoryItemViewModel item)
        {
            RunAsync(() => RemoveCategoryAsync(item));
        }

        private async Task RemoveCategoryAsync(CategoryItemViewModel item)
        {
            var collection = item.Model.Type == OperationType.Income ? IncomeCategories : ExpenseCategories;
            var operationCount = await _categoryService.GetOperationCountAsync(item.Model.Id);

            if (operationCount > 0)
            {
                await _categoryService.DeactivateAsync(item.Model.Id);
                collection.Remove(item);
                _toastService.ShowDeleteWithUndo(
                    $"Категория \u00ab{item.Model.Name}\u00bb скрыта (есть операции)",
                    async () =>
                    {
                        item.Model.IsActive = true;
                        await _categoryService.UpdateAsync(item.Model);
                        await LoadAsync();
                    });
            }
            else
            {
                var snapshot = new Category
                {
                    Name = item.Model.Name,
                    Type = item.Model.Type,
                    Color = item.Model.Color,
                    SortOrder = item.Model.SortOrder,
                    IsSystem = item.Model.IsSystem
                };

                var deleted = await _categoryService.DeleteAsync(item.Model.Id);
                if (deleted)
                {
                    collection.Remove(item);
                    _toastService.ShowDeleteWithUndo(
                        $"Категория \u00ab{snapshot.Name}\u00bb удалена",
                        async () =>
                        {
                            await _categoryService.AddAsync(snapshot);
                            await LoadAsync();
                        });
                }
            }
        }
    }
}
