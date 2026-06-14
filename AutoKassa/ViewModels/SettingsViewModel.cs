using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    public class SettingsViewModel : ViewModelBase, INavigationAware
    {
        private readonly ISettingsService _settingsService;
        private readonly ICreditCardService _creditCardService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;
        private readonly ICategoryService _categoryService;
        private readonly ITransactionService _transactionService;
        private readonly IDataChangeService _dataChangeService;
        private bool _isInitialized;
        private bool _needsRefresh;
        private CancellationTokenSource _navigateCts;

        #region Поля

        private AppSettings _currentSettings;
        private AppSettings _originalSettings;
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
            ICreditCardService creditCardService,
            IDialogService dialogService,
            IToastNotificationService toastService,
            ICategoryService categoryService,
            ITransactionService transactionService,
            IDataChangeService dataChangeService)
        {
            _settingsService = settingsService;
            _creditCardService = creditCardService;
            _dialogService = dialogService;
            _toastService = toastService;
            _categoryService = categoryService;
            _transactionService = transactionService;
            _dataChangeService = dataChangeService;

            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => HasUnsavedChanges);
            CancelCommand = new RelayCommand(_ => Cancel());
            ResetCommand = new RelayCommand(async _ => await ResetToDefaultsAsync());
            ExportCommand = new RelayCommand(async _ => await ExportSettingsAsync());
            ImportCommand = new RelayCommand(async _ => await ImportSettingsAsync());
            ChangePasswordCommand = new RelayCommand(_ => ChangePassword());
            SelectBackupPathCommand = new RelayCommand(_ => SelectBackupPath());
            CreateBackupCommand = new RelayCommand(async _ => await CreateBackupAsync());
            RestoreBackupCommand = new RelayCommand(async _ => await RestoreBackupAsync());

            _dataChangeService.DataChanged += OnDataChanged;
        }

        public void OnNavigatedTo()
        {
            if (!_isInitialized || _needsRefresh)
            {
                _needsRefresh = false;
                _isInitialized = true;

                var cts = new CancellationTokenSource();
                var old = Interlocked.Exchange(ref _navigateCts, cts);
                old?.Cancel();
                old?.Dispose();

                RunAsync(async () =>
                {
                    try { await Task.Delay(50, cts.Token); }
                    catch (OperationCanceledException) { return; }
                    await LoadSettingsAsync();
                });
            }
        }

        public void OnNavigatedFrom() { }

        private void OnDataChanged()
        {
            _needsRefresh = true;
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
                _originalSettings = CloneSettings(_currentSettings);

                LoadPropertiesFromSettings(_currentSettings);

                // Загрузить категории
                var income = await _categoryService.GetByTypeAsync(OperationType.Income);
                var expense = await _categoryService.GetByTypeAsync(OperationType.Expense);

                IncomeCategories.Clear();
                foreach (var c in income) IncomeCategories.Add(c);

                ExpenseCategories.Clear();
                foreach (var c in expense) ExpenseCategories.Add(c);

                // Загрузить данные «О программе»
                await LoadAboutDataAsync();

                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка загрузки настроек: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadAboutDataAsync()
        {
            AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

            var dbPath = await _settingsService.GetDatabasePathAsync();
            if (!string.IsNullOrEmpty(dbPath) && File.Exists(dbPath))
            {
                var size = new FileInfo(dbPath).Length;
                DbFileSize = size < 1024 * 1024
                    ? $"{size / 1024.0:F1} КБ"
                    : $"{size / (1024.0 * 1024.0):F1} МБ";
            }
            else
            {
                DbFileSize = "—";
            }

            TotalTransactions = await _transactionService.GetTotalCountAsync(new TransactionFilterParameters());
            var allCategories = await _categoryService.GetAllAsync();
            TotalCategories = allCategories.Count;

            var lastBackup = await _settingsService.GetLastBackupDateAsync();
            LastBackupDate = lastBackup?.ToString("dd.MM.yyyy HH:mm") ?? "Нет данных";
        }

        private void LoadPropertiesFromSettings(AppSettings settings)
        {
            _autoLockEnabled = settings.AutoLockEnabled;
            _autoLockMinutes = settings.AutoLockTimeout;
            _showNotifications = settings.ShowNotifications;
            _initialBalance = settings.InitialBalance;

            _showOperationsInSidebar = settings.ShowOperationsInSidebar;
            _defaultPageSize = settings.DefaultPageSize;
            _confirmDelete = settings.ConfirmDelete;
            _defaultOperationType = settings.DefaultOperationType;
            _defaultIncomeCategoryId = settings.DefaultIncomeCategoryId;
            _defaultExpenseCategoryId = settings.DefaultExpenseCategoryId;
            _defaultPaymentType = settings.DefaultPaymentType;

            _selectedDefaultPeriod = settings.DefaultPeriodFilter;
            _autoGenerateReports = settings.AutoGenerateReports;

            _backupEnabled = settings.BackupEnabled;
            _autoBackupDays = settings.AutoBackupDays;
            _backupPath = settings.BackupPath ?? string.Empty;
            _backupKeepCount = settings.BackupKeepCount;

            _requirePasswordOnStartup = settings.RequirePasswordOnStartup;
            _passwordExpireDays = settings.PasswordExpireDays;

            _creditCardLimit = settings.CreditCardLimit;
            _creditCardCurrentDebt = settings.CreditCardCurrentDebt;
            _creditCardInterestRate = settings.CreditCardInterestRate;
            _creditCardPaymentDay = settings.CreditCardPaymentDay;
            _creditCardLastPaymentDate = settings.CreditCardLastPaymentDate;
            _creditCardMinimumPaymentPercent = settings.CreditCardMinimumPaymentPercent;

            // Уведомляем UI о каждом изменённом свойстве вместо массового string.Empty.
            // Это убирает лишний пересчёт скрытых (Collapsed) элементов и ускоряет переключение.
            OnPropertyChanged(nameof(AutoLockEnabled));
            OnPropertyChanged(nameof(AutoLockMinutes));
            OnPropertyChanged(nameof(ShowNotifications));
            OnPropertyChanged(nameof(InitialBalance));
            OnPropertyChanged(nameof(ShowOperationsInSidebar));
            OnPropertyChanged(nameof(DefaultPageSize));
            OnPropertyChanged(nameof(ConfirmDelete));
            OnPropertyChanged(nameof(DefaultOperationType));
            OnPropertyChanged(nameof(DefaultIncomeCategoryId));
            OnPropertyChanged(nameof(DefaultExpenseCategoryId));
            OnPropertyChanged(nameof(DefaultPaymentType));
            OnPropertyChanged(nameof(SelectedDefaultPeriod));
            OnPropertyChanged(nameof(AutoGenerateReports));
            OnPropertyChanged(nameof(BackupEnabled));
            OnPropertyChanged(nameof(AutoBackupDays));
            OnPropertyChanged(nameof(BackupPath));
            OnPropertyChanged(nameof(BackupKeepCount));
            OnPropertyChanged(nameof(RequirePasswordOnStartup));
            OnPropertyChanged(nameof(PasswordExpireDays));
            OnPropertyChanged(nameof(CreditCardLimit));
            OnPropertyChanged(nameof(CreditCardCurrentDebt));
            OnPropertyChanged(nameof(CreditCardInterestRate));
            OnPropertyChanged(nameof(CreditCardPaymentDay));
            OnPropertyChanged(nameof(CreditCardLastPaymentDate));
            OnPropertyChanged(nameof(CreditCardMinimumPaymentPercent));
            OnPropertyChanged(nameof(HasUnsavedChanges));
        }

        private async Task SaveAsync()
        {
            try
            {
                if (!ValidateSettings()) return;

                IsLoading = true;

                ApplyPropertiesToSettings(_currentSettings);
                await _settingsService.SaveSettingsAsync(_currentSettings);
                await SyncCreditCardAsync(_currentSettings);

                _originalSettings = CloneSettings(_currentSettings);
                HasUnsavedChanges = false;
                _dataChangeService?.NotifyDataChanged();

                _toastService.ShowSuccess("Настройки сохранены");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка сохранения: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyPropertiesToSettings(AppSettings settings)
        {
            settings.AutoLockEnabled = AutoLockEnabled;
            settings.AutoLockTimeout = AutoLockMinutes;
            settings.ShowNotifications = ShowNotifications;
            settings.InitialBalance = InitialBalance;

            settings.ShowOperationsInSidebar = ShowOperationsInSidebar;
            settings.DefaultPageSize = DefaultPageSize;
            settings.ConfirmDelete = ConfirmDelete;
            settings.DefaultOperationType = DefaultOperationType;
            settings.DefaultIncomeCategoryId = DefaultIncomeCategoryId;
            settings.DefaultExpenseCategoryId = DefaultExpenseCategoryId;
            settings.DefaultPaymentType = DefaultPaymentType;

            settings.DefaultPeriodFilter = SelectedDefaultPeriod;
            settings.AutoGenerateReports = AutoGenerateReports;

            settings.BackupEnabled = BackupEnabled;
            settings.AutoBackupDays = AutoBackupDays;
            settings.BackupPath = string.IsNullOrWhiteSpace(BackupPath) ? null : BackupPath;
            settings.BackupKeepCount = BackupKeepCount;

            settings.RequirePasswordOnStartup = RequirePasswordOnStartup;
            settings.PasswordExpireDays = PasswordExpireDays;

            settings.CreditCardLimit = CreditCardLimit;
            settings.CreditCardCurrentDebt = CreditCardCurrentDebt;
            settings.CreditCardInterestRate = CreditCardInterestRate;
            settings.CreditCardPaymentDay = CreditCardPaymentDay;
            settings.CreditCardLastPaymentDate = CreditCardLastPaymentDate;
            settings.CreditCardMinimumPaymentPercent = CreditCardMinimumPaymentPercent;
        }

        private bool ValidateSettings()
        {
            if (AutoLockMinutes < 1 || AutoLockMinutes > 60)
            {
                _dialogService.ShowWarning("Время автоблокировки должно быть от 1 до 60 минут");
                return false;
            }

            if (DefaultPageSize < 10 || DefaultPageSize > 100)
            {
                _dialogService.ShowWarning("Количество записей на странице должно быть от 10 до 100");
                return false;
            }

            if (AutoBackupDays < 1 || AutoBackupDays > 365)
            {
                _dialogService.ShowWarning("Интервал резервного копирования должен быть от 1 до 365 дней");
                return false;
            }

            if (PasswordExpireDays < 0 || PasswordExpireDays > 365)
            {
                _dialogService.ShowWarning("Срок действия пароля должен быть от 0 до 365 дней");
                return false;
            }

            if (BackupKeepCount < 1 || BackupKeepCount > 100)
            {
                _dialogService.ShowWarning("Количество хранимых копий должно быть от 1 до 100");
                return false;
            }

            if (CreditCardLimit < 0)
            {
                _dialogService.ShowWarning("Кредитный лимит не может быть отрицательным");
                return false;
            }

            if (CreditCardCurrentDebt < 0)
            {
                _dialogService.ShowWarning("Текущий долг не может быть отрицательным");
                return false;
            }

            if (CreditCardInterestRate < 0)
            {
                _dialogService.ShowWarning("Процентная ставка не может быть отрицательной");
                return false;
            }

            if (CreditCardPaymentDay < 1 || CreditCardPaymentDay > 31)
            {
                _dialogService.ShowWarning("День платежа должен быть от 1 до 31");
                return false;
            }

            if (CreditCardMinimumPaymentPercent < 0 || CreditCardMinimumPaymentPercent > 100)
            {
                _dialogService.ShowWarning("Процент минимального платежа должен быть от 0 до 100");
                return false;
            }

            return true;
        }

        private void Cancel()
        {
            LoadPropertiesFromSettings(_originalSettings);
            HasUnsavedChanges = false;
        }

        private async Task ResetToDefaultsAsync()
        {
            try
            {
                IsLoading = true;

                await _settingsService.ResetToDefaultsAsync();
                _currentSettings = await _settingsService.GetSettingsAsync();
                _originalSettings = CloneSettings(_currentSettings);

                LoadPropertiesFromSettings(_currentSettings);
                HasUnsavedChanges = false;
                _dataChangeService?.NotifyDataChanged();

                _toastService.ShowSuccess("Настройки сброшены");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка сброса: {ex.Message}");
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
                _dialogService.ShowError($"Ошибка экспорта: {ex.Message}");
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
                    _originalSettings = CloneSettings(_currentSettings);
                    LoadPropertiesFromSettings(_currentSettings);
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
                _dialogService.ShowError($"Ошибка импорта: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ChangePassword()
        {
            var viewModel = new ChangePasswordViewModel(_settingsService, _dialogService, _toastService);
            var window = new ChangePasswordView(viewModel)
            {
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        private void SelectBackupPath()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Выберите папку для резервных копий",
                FileName = "Выберите эту папку",
                Filter = "Папка|*.folder",
                CheckPathExists = true,
                CheckFileExists = false
            };

            if (!string.IsNullOrEmpty(BackupPath))
                dialog.InitialDirectory = BackupPath;

            if (dialog.ShowDialog() == true)
            {
                BackupPath = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }
        }

        private async Task CreateBackupAsync()
        {
            string targetPath = BackupPath;

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                var dialog = new SaveFileDialog
                {
                    Title = "Выберите папку для сохранения резервной копии",
                    FileName = "Выберите эту папку",
                    Filter = "Папка|*.folder",
                    CheckPathExists = true,
                    CheckFileExists = false
                };

                if (dialog.ShowDialog() != true) return;
                targetPath = Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }

            try
            {
                IsLoading = true;
                var backupFile = await _settingsService.CreateBackupAsync(targetPath);

                if (!string.IsNullOrEmpty(backupFile))
                {
                    _toastService.ShowSuccess($"Резервная копия создана:\n{backupFile}");
                    // Обновить дату последнего бэкапа
                    var lastBackup = await _settingsService.GetLastBackupDateAsync();
                    LastBackupDate = lastBackup?.ToString("dd.MM.yyyy HH:mm") ?? "Нет данных";
                }
                else
                    _dialogService.ShowError("Не удалось создать резервную копию");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка создания резервной копии: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RestoreBackupAsync()
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Файлы базы данных (*.db)|*.db",
                Title = "Выберите файл резервной копии"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                IsLoading = true;
                var success = await _settingsService.RestoreBackupAsync(dialog.FileName);

                if (success)
                {
                    _dialogService.ShowInfo("База данных восстановлена. Приложение будет перезапущено.");
                    System.Diagnostics.Process.Start(Environment.ProcessPath!);
                    Application.Current.Shutdown();
                }
                else
                {
                    _dialogService.ShowError("Не удалось восстановить базу данных");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"Ошибка восстановления: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void MarkAsChanged()
        {
            if (!IsLoading)
                HasUnsavedChanges = true;
        }

        protected override void OnDispose()
        {
            var navCts = Interlocked.Exchange(ref _navigateCts, null);
            navCts?.Cancel();
            navCts?.Dispose();
            base.OnDispose();
        }

        private AppSettings CloneSettings(AppSettings source)
        {
            return new AppSettings
            {
                Id = source.Id,
                PasswordHash = source.PasswordHash,
                SecurityQuestionId = source.SecurityQuestionId,
                SecurityAnswerHash = source.SecurityAnswerHash,
                CustomSecurityQuestion = source.CustomSecurityQuestion,
                AutoLockTimeout = source.AutoLockTimeout,
                AutoLockEnabled = source.AutoLockEnabled,
                Theme = source.Theme,
                DefaultPeriodFilter = source.DefaultPeriodFilter,
                ShowNotifications = source.ShowNotifications,
                ShowOperationsInSidebar = source.ShowOperationsInSidebar,
                DefaultPageSize = source.DefaultPageSize,
                ConfirmDelete = source.ConfirmDelete,
                AutoGenerateReports = source.AutoGenerateReports,
                BackupEnabled = source.BackupEnabled,
                AutoBackupDays = source.AutoBackupDays,
                BackupFrequency = source.BackupFrequency,
                BackupKeepCount = source.BackupKeepCount,
                BackupPath = source.BackupPath,
                RequirePasswordOnStartup = source.RequirePasswordOnStartup,
                PasswordExpireDays = source.PasswordExpireDays,
                Language = source.Language,
                WindowWidth = source.WindowWidth,
                WindowHeight = source.WindowHeight,
                DefaultOperationType = source.DefaultOperationType,
                DefaultIncomeCategoryId = source.DefaultIncomeCategoryId,
                DefaultExpenseCategoryId = source.DefaultExpenseCategoryId,
                DefaultPaymentType = source.DefaultPaymentType,
                InitialBalance = source.InitialBalance,
                CreditCardLimit = source.CreditCardLimit,
                CreditCardCurrentDebt = source.CreditCardCurrentDebt,
                CreditCardInterestRate = source.CreditCardInterestRate,
                CreditCardPaymentDay = source.CreditCardPaymentDay,
                CreditCardLastPaymentDate = source.CreditCardLastPaymentDate,
                CreditCardMinimumPaymentPercent = source.CreditCardMinimumPaymentPercent
            };
        }

        /// <summary>
        /// Синхронизирует настройки основной кредитной карты с таблицей CreditCards.
        /// </summary>
        private async Task SyncCreditCardAsync(AppSettings settings)
        {
            try
            {
                var existing = await _creditCardService.GetByIdAsync(1);
                if (existing != null)
                {
                    existing.Name = "Основная кредитная карта";
                    existing.Limit = settings.CreditCardLimit;
                    existing.InitialDebt = settings.CreditCardCurrentDebt;
                    existing.InterestRate = settings.CreditCardInterestRate;
                    existing.PaymentDay = settings.CreditCardPaymentDay;
                    existing.LastPaymentDate = settings.CreditCardLastPaymentDate;
                    existing.MinimumPaymentPercent = settings.CreditCardMinimumPaymentPercent;
                    existing.IsActive = true;
                    await _creditCardService.UpdateAsync(existing);
                }
                else
                {
                    var card = new CreditCard
                    {
                        Name = "Основная кредитная карта",
                        Limit = settings.CreditCardLimit,
                        InitialDebt = settings.CreditCardCurrentDebt,
                        InterestRate = settings.CreditCardInterestRate,
                        PaymentDay = settings.CreditCardPaymentDay,
                        LastPaymentDate = settings.CreditCardLastPaymentDate,
                        MinimumPaymentPercent = settings.CreditCardMinimumPaymentPercent,
                        IsActive = true
                    };
                    await _creditCardService.CreateAsync(card);
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Не удалось синхронизировать кредитную карту: {ex.Message}");
            }
        }

        #endregion
    }
}
