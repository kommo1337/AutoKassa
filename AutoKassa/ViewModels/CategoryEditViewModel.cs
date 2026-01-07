using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для формы добавления/редактирования категории
    /// </summary>
    public class CategoryEditViewModel : ViewModelBase
    {
        private readonly ICategoryService _categoryService;
        private readonly IDialogService _dialogService;

        private Category _category;
        private bool _isEditMode;
        private string _name;
        private OperationType _type;
        private string _nameError;

        public CategoryEditViewModel(
            ICategoryService categoryService,
            IDialogService dialogService)
        {
            _categoryService = categoryService;
            _dialogService = dialogService;

            // Значения по умолчанию
            Type = OperationType.Expense;

            // Команды
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => Cancel());
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
                    OnPropertyChanged(nameof(CanChangeType));
                }
            }
        }

        /// <summary>
        /// Заголовок формы
        /// </summary>
        public string Title => IsEditMode ? "Редактирование категории" : "Новая категория";

        /// <summary>
        /// Можно ли изменить тип (только при создании)
        /// </summary>
        public bool CanChangeType => !IsEditMode;

        /// <summary>
        /// Название категории
        /// </summary>
        public string Name
        {
            get => _name;
            set
            {
                if (SetProperty(ref _name, value))
                {
                    ValidateName();
                }
            }
        }

        /// <summary>
        /// Тип категории
        /// </summary>
        public OperationType Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        /// <summary>
        /// Ошибка валидации названия
        /// </summary>
        public string NameError
        {
            get => _nameError;
            set => SetProperty(ref _nameError, value);
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
            Name = string.Empty;
            Type = OperationType.Expense;
        }

        /// <summary>
        /// Инициализация для редактирования
        /// </summary>
        public void InitializeForEdit(Category category)
        {
            IsEditMode = true;
            _category = category;

            Name = category.Name;
            Type = category.Type;
        }

        /// <summary>
        /// Валидация названия
        /// </summary>
        private async void ValidateName()
        {
            if (string.IsNullOrWhiteSpace(Name))
            {
                NameError = "Введите название категории";
                return;
            }

            if (Name.Length < 3)
            {
                NameError = "Название должно содержать минимум 3 символа";
                return;
            }

            if (Name.Length > 100)
            {
                NameError = "Название слишком длинное (максимум 100 символов)";
                return;
            }

            // Проверка уникальности
            try
            {
                var exists = await _categoryService.ExistsAsync(
                    Name.Trim(),
                    Type,
                    IsEditMode ? _category.Id : (int?)null
                );

                if (exists)
                {
                    NameError = "Категория с таким названием уже существует";
                    return;
                }

                NameError = null;
            }
            catch
            {
                // Игнорируем ошибки проверки уникальности
                NameError = null;
            }
        }

        /// <summary>
        /// Можно ли сохранить
        /// </summary>
        private bool CanSave()
        {
            return !string.IsNullOrWhiteSpace(Name) &&
                   Name.Length >= 3 &&
                   Name.Length <= 100 &&
                   string.IsNullOrEmpty(NameError);
        }

        /// <summary>
        /// Сохранить категорию
        /// </summary>
        private async System.Threading.Tasks.Task SaveAsync()
        {
            ValidateName();

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
                    _category.Name = Name.Trim();
                    // Тип нельзя изменить при редактировании

                    await _categoryService.UpdateAsync(_category);
                    _dialogService.ShowInfo("Категория успешно обновлена");
                }
                else
                {
                    // Добавление
                    var category = new Category
                    {
                        Name = Name.Trim(),
                        Type = Type,
                        IsSystem = false
                    };

                    await _categoryService.AddAsync(category);
                    _dialogService.ShowInfo("Категория успешно добавлена");
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