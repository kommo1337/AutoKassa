using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Services.Backup;
using AutoKassa.Services.CreditCards;
using AutoKassa.Services.Settings;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    public class SettingsViewModel : ViewModelBase, INavigationAware, IPageViewModel
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly ICategoryService _categoryService;
        private readonly IDataChangeService _dataChangeService;
        private readonly ISettingsMapper _mapper;
        private readonly ISettingsValidator _validator;
        private readonly IBackupCoordinator _backupCoordinator;
        private readonly IAppInfoService _appInfoService;
        private readonly ICreditCardSettingsService _creditCardSettingsService;
        private readonly ILockService _lockService;

        private AppSettings _currentSettings;
        private AppSettings _originalSettings;
        private bool _isInitialized;
        private bool _needsRefresh;

        #region Поля

        private bool _isLoading;
        private bool _hasUnsavedChanges;
        private int _selectedTabIndex;

        // Общие
        private bool _autoLockEnabled;
        private int _autoLockMinutes;
        private bool _showNotifications;
        private decimal _initialBalance;

        // Операции
        private bool _showOperationsInSidebar;
        private int _defaultPageSize;
        private bool _confirmDelete;
        private int _defaultOperationType;
        private int? _defaultIncomeCategoryId;
        private int? _defaultExpenseCategoryId;
        private int _defaultPaymentType;

        // Отчёты
        private string _selectedDefaultPeriod;
        private bool _autoGenerateReports;

        // Резервное копирование
        private bool _backupEnabled;
        private int _autoBackupDays;
        private string _backupPath;
        private int _backupKeepCount;

        // Безопасность
        private bool _requirePasswordOnStartup;
        private int _passwordExpireDays;

        // Кредитная карта
        private decimal _creditCardLimit;
        private decimal _creditCardCurrentDebt;
        private decimal _creditCardInterestRate;
        private int _creditCardPaymentDay;
        private DateTime? _creditCardLastPaymentDate;
        private decimal _creditCardMinimumPaymentPercent;

        // О программе
        private string _appVersion;
        private string _dbFileSize;
        private int _totalTransactions;
        private int _totalCategories;
        private string _lastBackupDate;

        #endregion

        #region Коллекции

        public ObservableCollection<DisplayItem> AvailableOperationTypes { get; } = new()
        {
            new DisplayItem { Value = "1", Display = "Доход" },
            new DisplayItem { Value = "2", Display = "Расход" }
        };

        public ObservableCollection<DisplayItem> AvailablePaymentTypes { get; } = new()
        {
            new DisplayItem { Value = "1", Display = "Наличные" },
            new DisplayItem { Value = "2", Display = "Безналичные" }
        };

        public ObservableCollection<DisplayItem> AvailablePeriods { get; } = new()
        {
            new DisplayItem { Value = "Today", Display = "Сегодня" },
            new DisplayItem { Value = "Week", Display = "Неделя" },
            new DisplayItem { Value = "Month", Display = "Месяц" },
            new DisplayItem { Value = "Quarter", Display = "Квартал" },
            new DisplayItem { Value = "Year", Display = "Год" }
        };

        public ObservableCollection<Category> IncomeCategories { get; } = new();
        public ObservableCollection<Category> ExpenseCategories { get; } = new();

        #endregion

        public SettingsViewModel(
            ISettingsService settingsService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            ICategoryService categoryService,
            IDataChangeService dataChangeService,
            ISettingsMapper mapper,
            ISettingsValidator validator,
            IBackupCoordinator backupCoordinator,
            IAppInfoService appInfoService,
            ICreditCardSettingsService creditCardSettingsService,
            ILockService lockService)
        {
            _settingsService = settingsService;
            _dialogService = dialogService;
            _toastService = toastService;
            _categoryService = categoryService;
            _dataChangeService = dataChangeService;
            _mapper = mapper;
            _validator = validator;
            _backupCoordinator = backupCoordinator;
            _appInfoService = appInfoService;
            _creditCardSettingsService = creditCardSettingsService;
            _lockService = lockService;

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasUnsavedChanges);
            CancelCommand = new RelayCommand(_ => Cancel());
            ResetCommand = new AsyncRelayCommand(ResetToDefaultsAsync);
            ExportCommand = new AsyncRelayCommand(ExportSettingsAsync);
            ImportCommand = new AsyncRelayCommand(ImportSettingsAsync);
            ChangePasswordCommand = new RelayCommand(_ => ChangePassword());
            SelectBackupPathCommand = new RelayCommand(_ => SelectBackupPath());
            CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
            RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);

            _dataChangeService.DataChanged += OnDataChanged;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized || _needsRefresh)
            {
                _needsRefresh = false;
                _isInitialized = true;
                RunAsync(LoadSettingsAsync);
            }
        }

        public void OnNavigatedFrom() { }

        private void OnDataChanged()
        {
            _needsRefresh = true;
        }

        /// <summary>
        /// Публичный метод для вызова <see cref="OnPropertyChanged"/> из маппера настроек.
        /// </summary>
        internal void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        #region Свойства

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => SetProperty(ref _hasUnsavedChanges, value);
        }

        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        #region Общие

        public bool AutoLockEnabled
        {
            get => _autoLockEnabled;
            set { if (SetProperty(ref _autoLockEnabled, value)) MarkAsChanged(); }
        }

        public int AutoLockMinutes
        {
            get => _autoLockMinutes;
            set { if (SetProperty(ref _autoLockMinutes, value)) MarkAsChanged(); }
        }

        public bool ShowNotifications
        {
            get => _showNotifications;
            set { if (SetProperty(ref _showNotifications, value)) MarkAsChanged(); }
        }

        public decimal InitialBalance
        {
            get => _initialBalance;
            set { if (SetProperty(ref _initialBalance, value)) MarkAsChanged(); }
        }

        #endregion

        #region Операции

        public bool ShowOperationsInSidebar
        {
            get => _showOperationsInSidebar;
            set { if (SetProperty(ref _showOperationsInSidebar, value)) MarkAsChanged(); }
        }

        public int DefaultPageSize
        {
            get => _defaultPageSize;
            set { if (SetProperty(ref _defaultPageSize, value)) MarkAsChanged(); }
        }

        public bool ConfirmDelete
        {
            get => _confirmDelete;
            set { if (SetProperty(ref _confirmDelete, value)) MarkAsChanged(); }
        }

        public int DefaultOperationType
        {
            get => _defaultOperationType;
            set { if (SetProperty(ref _defaultOperationType, value)) MarkAsChanged(); }
        }

        public int? DefaultIncomeCategoryId
        {
            get => _defaultIncomeCategoryId;
            set { if (SetProperty(ref _defaultIncomeCategoryId, value)) MarkAsChanged(); }
        }

        public int? DefaultExpenseCategoryId
        {
            get => _defaultExpenseCategoryId;
            set { if (SetProperty(ref _defaultExpenseCategoryId, value)) MarkAsChanged(); }
        }

        public int DefaultPaymentType
        {
            get => _defaultPaymentType;
            set { if (SetProperty(ref _defaultPaymentType, value)) MarkAsChanged(); }
        }

        #endregion

        #region Отчёты

        public string SelectedDefaultPeriod
        {
            get => _selectedDefaultPeriod;
            set { if (SetProperty(ref _selectedDefaultPeriod, value)) MarkAsChanged(); }
        }

        public bool AutoGenerateReports
        {
            get => _autoGenerateReports;
            set { if (SetProperty(ref _autoGenerateReports, value)) MarkAsChanged(); }
        }

        #endregion

        #region Резервное копирование

        public bool BackupEnabled
        {
            get => _backupEnabled;
            set { if (SetProperty(ref _backupEnabled, value)) MarkAsChanged(); }
        }

        public int AutoBackupDays
        {
            get => _autoBackupDays;
            set { if (SetProperty(ref _autoBackupDays, value)) MarkAsChanged(); }
        }

        public string BackupPath
        {
            get => _backupPath;
            set { if (SetProperty(ref _backupPath, value)) MarkAsChanged(); }
        }

        public int BackupKeepCount
        {
            get => _backupKeepCount;
            set { if (SetProperty(ref _backupKeepCount, value)) MarkAsChanged(); }
        }

        #endregion

        #region Безопасность

        public bool RequirePasswordOnStartup
        {
            get => _requirePasswordOnStartup;
            set { if (SetProperty(ref _requirePasswordOnStartup, value)) MarkAsChanged(); }
        }

        public int PasswordExpireDays
        {
            get => _passwordExpireDays;
            set { if (SetProperty(ref _passwordExpireDays, value)) MarkAsChanged(); }
        }

        #endregion

        #region Кредитная карта

        public decimal CreditCardLimit
        {
            get => _creditCardLimit;
            set { if (SetProperty(ref _creditCardLimit, value)) MarkAsChanged(); }
        }

        public decimal CreditCardCurrentDebt
        {
            get => _creditCardCurrentDebt;
            set { if (SetProperty(ref _creditCardCurrentDebt, value)) MarkAsChanged(); }
        }

        public decimal CreditCardInterestRate
        {
            get => _creditCardInterestRate;
            set { if (SetProperty(ref _creditCardInterestRate, value)) MarkAsChanged(); }
        }

        public int CreditCardPaymentDay
        {
            get => _creditCardPaymentDay;
            set { if (SetProperty(ref _creditCardPaymentDay, value)) MarkAsChanged(); }
        }

        public DateTime? CreditCardLastPaymentDate
        {
            get => _creditCardLastPaymentDate;
            set { if (SetProperty(ref _creditCardLastPaymentDate, value)) MarkAsChanged(); }
        }

        public decimal CreditCardMinimumPaymentPercent
        {
            get => _creditCardMinimumPaymentPercent;
            set { if (SetProperty(ref _creditCardMinimumPaymentPercent, value)) MarkAsChanged(); }
        }

        #endregion

        #region О программе (read-only)

        public string AppVersion
        {
            get => _appVersion;
            private set => SetProperty(ref _appVersion, value);
        }

        public string DbFileSize
        {
            get => _dbFileSize;
            private set => SetProperty(ref _dbFileSize, value);
        }

        public int TotalTransactions
        {
            get => _totalTransactions;
            private set => SetProperty(ref _totalTransactions, value);
        }

        public int TotalCategories
        {
            get => _totalCategories;
            private set => SetProperty(ref _totalCategories, value);
        }

        public string LastBackupDate
        {
            get => _lastBackupDate;
            private set => SetProperty(ref _lastBackupDate, value);
        }

        #endregion

        #endregion

        #region Команды

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ChangePasswordCommand { get; }
        public ICommand SelectBackupPathCommand { get; }
        public ICommand CreateBackupCommand { get; }
        public ICommand RestoreBackupCommand { get; }

        #endregion

        #region Методы

        private async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;

                _currentSettings = await _settingsService.GetSettingsAsync();
                _originalSettings = _mapper.Clone(_currentSettings);
                _mapper.MapToViewModel(_currentSettings, this);

                var income = await _categoryService.GetByTypeAsync(OperationType.Income);
                var expense = await _categoryService.GetByTypeAsync(OperationType.Expense);
                IncomeCategories.Clear();
                foreach (var c in income) IncomeCategories.Add(c);
                ExpenseCategories.Clear();
                foreach (var c in expense) ExpenseCategories.Add(c);

                await LoadAboutDataAsync();
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка загрузки настроек");
                _dialogService.ShowError("Не удалось загрузить настройки. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAboutDataAsync()
        {
            var info = await _appInfoService.GetAppInfoAsync();
            AppVersion = info.AppVersion;
            DbFileSize = info.DbFileSize;
            TotalTransactions = info.TotalTransactions;
            TotalCategories = info.TotalCategories;
            LastBackupDate = info.LastBackupDate;
        }

        private async Task SaveAsync()
        {
            try
            {
                var errors = _validator.Validate(this);
                if (errors.Any())
                {
                    _dialogService.ShowWarning(errors.First());
                    return;
                }

                IsLoading = true;
                _mapper.MapToModel(this, _currentSettings);
                await _settingsService.SaveSettingsAsync(_currentSettings);

                try { await _creditCardSettingsService.SyncFromSettingsAsync(_currentSettings); }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Ошибка синхронизации кредитной карты с настройками");
                    _dialogService.ShowWarning("Не удалось синхронизировать кредитную карту с новыми настройками. Подробности — в файле журнала (папка logs).");
                }

                _originalSettings = _mapper.Clone(_currentSettings);
                HasUnsavedChanges = false;
                _dataChangeService?.NotifyDataChanged();

                // Применяем настройки автоблокировки к таймеру сразу,
                // не дожидаясь перезапуска приложения
                if (AutoLockEnabled && AutoLockMinutes > 0)
                    _lockService.StartAutoLockTimer(); // Перечитает настройки и пересоздаст интервал
                else
                    _lockService.StopAutoLockTimer();

                _toastService.ShowSuccess("Настройки сохранены");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка сохранения настроек");
                _dialogService.ShowError("Не удалось сохранить настройки. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void Cancel()
        {
            _mapper.MapToViewModel(_originalSettings, this);
            HasUnsavedChanges = false;
        }

        private async Task ResetToDefaultsAsync()
        {
            try
            {
                IsLoading = true;

                await _settingsService.ResetToDefaultsAsync();
                _currentSettings = await _settingsService.GetSettingsAsync();
                _originalSettings = _mapper.Clone(_currentSettings);

                _mapper.MapToViewModel(_currentSettings, this);
                HasUnsavedChanges = false;
                _dataChangeService?.NotifyDataChanged();

                _toastService.ShowSuccess("Настройки сброшены");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка сброса настроек");
                _dialogService.ShowError("Не удалось сбросить настройки. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ExportSettingsAsync()
        {
            var dialog = new SaveFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json",
                FileName = $"AutoKassa_Settings_{DateTime.Now:yyyyMMdd}.json",
                Title = "Экспорт настроек"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                IsLoading = true;
                var success = await _settingsService.ExportSettingsAsync(dialog.FileName);

                if (success)
                    _toastService.ShowSuccess("Настройки экспортированы");
                else
                    _dialogService.ShowError("Не удалось экспортировать настройки");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка экспорта настроек в {FileName}", dialog.FileName);
                _dialogService.ShowError("Не удалось экспортировать настройки. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ImportSettingsAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "JSON файлы (*.json)|*.json",
                Title = "Импорт настроек"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                IsLoading = true;
                var success = await _settingsService.ImportSettingsAsync(dialog.FileName);

                if (success)
                {
                    _currentSettings = await _settingsService.GetSettingsAsync();
                    _originalSettings = _mapper.Clone(_currentSettings);
                    _mapper.MapToViewModel(_currentSettings, this);
                    HasUnsavedChanges = false;
                    _dataChangeService?.NotifyDataChanged();

                    _toastService.ShowSuccess("Настройки импортированы");
                }
                else
                {
                    _dialogService.ShowError("Не удалось импортировать настройки. Проверьте формат файла.");
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка импорта настроек из {FileName}", dialog.FileName);
                _dialogService.ShowError("Не удалось импортировать настройки. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ChangePassword()
        {
            _dialogService.ShowDialog<ChangePasswordViewModel>();
        }

        private void SelectBackupPath()
        {
            var path = _backupCoordinator.SelectBackupFolder(BackupPath);
            if (!string.IsNullOrEmpty(path))
                BackupPath = path;
        }

        private async Task CreateBackupAsync()
        {
            var backupFile = await _backupCoordinator.CreateBackupAsync(BackupPath);
            if (!string.IsNullOrEmpty(backupFile))
            {
                var lastBackup = await _settingsService.GetLastBackupDateAsync();
                LastBackupDate = lastBackup?.ToString("dd.MM.yyyy HH:mm") ?? "Нет данных";
            }
        }

        private async Task RestoreBackupAsync() => await _backupCoordinator.RestoreBackupAsync();

        private void MarkAsChanged()
        {
            if (!IsLoading)
                HasUnsavedChanges = true;
        }

        protected override void OnDispose()
        {
            _dataChangeService.DataChanged -= OnDataChanged;

            base.OnDispose();
        }

        #endregion
    }
}
