using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Models.Enums;
using AutoKassa.Services;
using AutoKassa.Services.Backup;
using AutoKassa.Services.Backup.YandexDisk;
using AutoKassa.Services.CreditCards;
using AutoKassa.Services.Settings;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
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
        private readonly IYandexDiskAuthService _yandexAuthService;
        private readonly IYandexDiskBackupService _yandexBackupService;
        private readonly IAutomaticBackupService _automaticBackupService;
        private readonly IClipboardService _clipboardService;

        // Контекст UI-потока для маршалинга событий фоновых сервисов (Singleton-VM создаётся на UI-потоке).
        private readonly SynchronizationContext? _uiContext = SynchronizationContext.Current;
        private bool _isDisposed;

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

        // Яндекс Диск
        private bool _yandexDiskBackupEnabled;
        private YandexDiskAuthState _yandexAuthState = YandexDiskAuthState.NotConnected;
        private string? _cloudStatusMessage;
        private DateTime? _cloudLastSuccessUtc;
        private bool _cloudHasPendingFile;
        private bool _cloudIsBusy;
        private bool _cloudAwaitingCode;
        private string _cloudAuthCode = string.Empty;
        private string? _cloudAuthUrl;

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
            ILockService lockService,
            IYandexDiskAuthService yandexAuthService,
            IYandexDiskBackupService yandexBackupService,
            IAutomaticBackupService automaticBackupService,
            IClipboardService clipboardService)
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
            _yandexAuthService = yandexAuthService;
            _yandexBackupService = yandexBackupService;
            _automaticBackupService = automaticBackupService;
            _clipboardService = clipboardService;

            SaveCommand = new AsyncRelayCommand(SaveAsync, () => HasUnsavedChanges);
            CancelCommand = new RelayCommand(_ => Cancel());
            ResetCommand = new AsyncRelayCommand(ResetToDefaultsAsync);
            ExportCommand = new AsyncRelayCommand(ExportSettingsAsync);
            ImportCommand = new AsyncRelayCommand(ImportSettingsAsync);
            ChangePasswordCommand = new RelayCommand(_ => ChangePassword());
            SelectBackupPathCommand = new RelayCommand(_ => SelectBackupPath());
            CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync);
            RestoreBackupCommand = new AsyncRelayCommand(RestoreBackupAsync);
            ConnectYandexCommand = new AsyncRelayCommand(ConnectYandexAsync, CanConnectYandex);
            ConfirmYandexCodeCommand = new AsyncRelayCommand(ConfirmYandexCodeAsync, CanConfirmYandexCode);
            CancelYandexInputCommand = new AsyncRelayCommand(CancelYandexInputAsync, CanCancelYandexInput);
            CopyYandexAuthLinkCommand = new RelayCommand(_ => CopyYandexAuthLink(), _ => CanCopyYandexAuthLink());
            DisconnectYandexCommand = new AsyncRelayCommand(DisconnectYandexAsync, CanDisconnectYandex);
            UploadNowCommand = new AsyncRelayCommand(UploadNowAsync, CanUploadNow);

            // События фоновых сервисов поднимаются на рабочем потоке — маршалим на UI-поток.
            _yandexAuthService.StateChanged += OnYandexAuthStateChanged;
            _yandexBackupService.StateChanged += OnCloudBackupStateChanged;
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

        #region Яндекс Диск

        public bool YandexDiskBackupEnabled
        {
            get => _yandexDiskBackupEnabled;
            set { if (SetProperty(ref _yandexDiskBackupEnabled, value)) MarkAsChanged(); }
        }

        /// <summary>Состояние подключения аккаунта (для доступности кнопок и подписей).</summary>
        public YandexDiskAuthState YandexAuthState
        {
            get => _yandexAuthState;
            private set { if (SetProperty(ref _yandexAuthState, value)) RefreshCloudCommandStates(); }
        }

        /// <summary>Понятное пользователю состояние облачной копии (без технических деталей и секретов).</summary>
        public string CloudStatusMessage
        {
            get => _cloudStatusMessage ?? "Яндекс Диск не подключён.";
            private set => SetProperty(ref _cloudStatusMessage, value);
        }

        /// <summary>Дата последней подтверждённой облачной выгрузки (UTC) или null.</summary>
        public DateTime? CloudLastSuccessUtc
        {
            get => _cloudLastSuccessUtc;
            private set => SetProperty(ref _cloudLastSuccessUtc, value);
        }

        /// <summary>В локальной очереди остался незагруженный файл.</summary>
        public bool CloudHasPendingFile
        {
            get => _cloudHasPendingFile;
            private set => SetProperty(ref _cloudHasPendingFile, value);
        }

        /// <summary>Идёт облачная операция (загрузка или подключение) — блокирует кнопки.</summary>
        public bool CloudIsBusy
        {
            get => _cloudIsBusy;
            private set { if (SetProperty(ref _cloudIsBusy, value)) RefreshCloudCommandStates(); }
        }

        /// <summary>Браузер открыт, ожидается ввод одноразового кода.</summary>
        public bool CloudAwaitingCode
        {
            get => _cloudAwaitingCode;
            private set { if (SetProperty(ref _cloudAwaitingCode, value)) RefreshCloudCommandStates(); }
        }

        /// <summary>Одноразовый код, введённый пользователем после разрешения доступа в браузере.</summary>
        public string CloudAuthCode
        {
            get => _cloudAuthCode;
            set
            {
                if (SetProperty(ref _cloudAuthCode, value))
                    (ConfirmYandexCodeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        /// <summary>
        /// URL страницы разрешения доступа текущей попытки входа. Действителен, даже если
        /// браузер не открылся автоматически, — пользователь может открыть его вручную (YD-10).
        /// </summary>
        public string? CloudAuthUrl
        {
            get => _cloudAuthUrl;
            private set { if (SetProperty(ref _cloudAuthUrl, value)) (CopyYandexAuthLinkCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
        }

        /// <summary>Форматированная дата последнего успеха для UI.</summary>
        public string CloudLastSuccessText
            => CloudLastSuccessUtc == null ? "—" : CloudLastSuccessUtc.Value.ToLocalTime().ToString("dd.MM.yyyy HH:mm");

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
        public ICommand ConnectYandexCommand { get; }
        public ICommand ConfirmYandexCodeCommand { get; }
        public ICommand CancelYandexInputCommand { get; }
        public ICommand CopyYandexAuthLinkCommand { get; }
        public ICommand DisconnectYandexCommand { get; }
        public ICommand UploadNowCommand { get; }

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
                await RefreshCloudStatusAsync();
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

                // Сроки бэкапа могли измениться — пробуждаем координатор для внеочередной проверки в фоне.
                _automaticBackupService.RequestCheck();

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

        #region Яндекс Диск

        private bool CanConnectYandex() => !CloudIsBusy && !CloudAwaitingCode && YandexAuthState != YandexDiskAuthState.Connected;

        private bool CanConfirmYandexCode() => CloudAwaitingCode && !string.IsNullOrWhiteSpace(CloudAuthCode);

        private bool CanCancelYandexInput() => CloudAwaitingCode && !CloudIsBusy;

        private bool CanCopyYandexAuthLink() => CloudAwaitingCode && !string.IsNullOrEmpty(CloudAuthUrl);

        private bool CanDisconnectYandex() => !CloudIsBusy && YandexAuthState != YandexDiskAuthState.NotConnected;

        private bool CanUploadNow() => !CloudIsBusy && YandexAuthState == YandexDiskAuthState.Connected;

        /// <summary>
        /// Единая точка применения состояния подключения: транзитное AwaitingCode
        /// отображается через CloudAwaitingCode, а не как самостоятельный экран.
        /// </summary>
        private void ApplyAuthState(YandexDiskAuthState state)
        {
            YandexAuthState = state;
            CloudAwaitingCode = state == YandexDiskAuthState.AwaitingCode;
        }

        private void RefreshCloudCommandStates()
        {
            (ConnectYandexCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (ConfirmYandexCodeCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (CancelYandexInputCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (CopyYandexAuthLinkCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DisconnectYandexCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            (UploadNowCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        }

        /// <summary>Начальная загрузка облачного состояния: состояние копии через общий ApplyCloudState
        /// (дата из настроек подставляется только если сервис её ещё не знает), затем авторитетное
        /// состояние подключения из сервиса авторизации (YD-11).</summary>
        private async Task RefreshCloudStatusAsync()
        {
            try
            {
                var state = _yandexBackupService.CurrentState;
                ApplyCloudState(state with
                {
                    LastSuccessUtc = state.LastSuccessUtc ?? _currentSettings.YandexDiskLastBackupUtc
                });
                ApplyAuthState(await _yandexAuthService.GetStateAsync());
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка загрузки состояния Яндекс Диска");
            }
        }

        private async Task ConnectYandexAsync()
        {
            try
            {
                CloudIsBusy = true;

                // ClientId не задан ни в appsettings.json, ни ранее пользователем —
                // предлагаем ввести его (значение сохраняется на этом компьютере).
                if (!_yandexAuthService.IsClientIdConfigured())
                {
                    var entered = _dialogService.ShowDialog<YandexClientIdDialogViewModel>();
                    if (entered != true)
                        return;

                    if (!_yandexAuthService.IsClientIdConfigured())
                    {
                        _dialogService.ShowWarning("ClientId не сохранён. Подключение отменено.");
                        return;
                    }
                }

                // Открывает системный браузер на странице разрешения доступа;
                // пользователь вводит показанный одноразовый код в поле ниже.
                var start = await _yandexAuthService.StartAuthorizationAsync();
                CloudAuthUrl = start.AuthorizationUrl;
                YandexAuthState = YandexDiskAuthState.AwaitingCode;
                CloudAwaitingCode = true;
                if (!start.BrowserOpened)
                {
                    // Автооткрытие не сработало, но URL действителен — пользователь открывает его вручную (YD-10).
                    _dialogService.ShowWarning("Не удалось автоматически открыть браузер. Скопируйте ссылку для подключения, откройте её в браузере и введите показанный код.");
                }
            }
            catch (InvalidOperationException ex)
            {
                // Бизнес-правило из сервиса — текст уже понятен пользователю
                _dialogService.ShowWarning(ex.Message);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка начала подключения Яндекс Диска");
                _dialogService.ShowError("Не удалось подключить Яндекс Диск. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                CloudIsBusy = false;
            }
        }

        private async Task ConfirmYandexCodeAsync()
        {
            try
            {
                CloudIsBusy = true;
                await _yandexAuthService.CompleteAuthorizationAsync(CloudAuthCode);

                // Новая учётная запись должна сразу получить актуальную копию:
                // сбрасываем дату последнего успеха и будим координатор.
                await _settingsService.SetYandexDiskLastBackupUtcAsync(null);
                ApplyAuthState(YandexDiskAuthState.Connected);
                CloudAuthCode = string.Empty;
                CloudAuthUrl = null;
                CloudLastSuccessUtc = null;
                _toastService.ShowSuccess("Яндекс Диск подключён");
                _automaticBackupService.RequestCheck();
            }
            catch (InvalidOperationException ex)
            {
                // Бизнес-правило из сервиса (нет попытки входа, истёк код, отказ Яндекса) — текст уже понятен пользователю.
                // Неуспешный обмен сбрасывает ожидание кода — «Подключить» снова доступно без перезапуска (YD-03).
                CloudAwaitingCode = false;
                CloudAuthCode = string.Empty;
                CloudAuthUrl = null;
                try
                {
                    YandexAuthState = await _yandexAuthService.GetStateAsync();
                }
                catch (Exception stateEx)
                {
                    Serilog.Log.Error(stateEx, "Ошибка чтения состояния подключения Яндекс Диска после неуспешного обмена кодом");
                }
                _dialogService.ShowWarning(ex.Message);
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка подключения Яндекс Диска по одноразовому коду");
                _dialogService.ShowError("Не удалось подключить Яндекс Диск. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                CloudIsBusy = false;
            }
        }

        private async Task CancelYandexInputAsync()
        {
            try
            {
                await _yandexAuthService.CancelAuthorizationAsync();
                CloudAuthCode = string.Empty;
                CloudAuthUrl = null;
                CloudAwaitingCode = false;
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка отмены подключения Яндекс Диска");
                _dialogService.ShowError("Не удалось отменить подключение. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
        }

        private void CopyYandexAuthLink()
        {
            if (string.IsNullOrEmpty(CloudAuthUrl))
                return;

            if (_clipboardService.SetText(CloudAuthUrl))
            {
                _toastService.ShowSuccess("Ссылка для подключения скопирована");
            }
            else
            {
                _dialogService.ShowError("Не удалось скопировать ссылку. Попробуйте ещё раз.");
            }
        }

        private async Task DisconnectYandexAsync()
        {
            try
            {
                CloudIsBusy = true;
                await _yandexAuthService.DisconnectAsync();
                await _settingsService.SetYandexDiskLastBackupUtcAsync(null);

                ApplyAuthState(YandexDiskAuthState.NotConnected);
                CloudAuthCode = string.Empty;
                CloudAuthUrl = null;
                CloudLastSuccessUtc = null;
                // Признак pending не сбрасываем вручную: файл очереди при отключении намеренно
                // не удаляется — индикатор обязан совпадать с фактическим состоянием (YD-11).
                CloudStatusMessage = "Яндекс Диск не подключён.";
                _toastService.ShowSuccess("Яндекс Диск отключён");
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка отключения Яндекс Диска");
                _dialogService.ShowError("Не удалось отключить Яндекс Диск. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                CloudIsBusy = false;
            }
        }

        private async Task UploadNowAsync()
        {
            try
            {
                CloudIsBusy = true;
                var result = await _yandexBackupService.UploadNowAsync(force: true);

                if (result.Success)
                {
                    ApplyCloudState(result.State);
                    _toastService.ShowSuccess("Копия загружена на Яндекс Диск");
                }
                else
                {
                    // Сервис возвращает только понятные пользователю сообщения без технических деталей и секретов.
                    ApplyCloudState(result.State);
                    _dialogService.ShowWarning(result.State.UserMessage ?? "Не удалось загрузить копию на Яндекс Диск.");
                }
            }
            catch (OperationCanceledException)
            {
                // Штатная отмена (закрытие приложения) — состояние уже обновлено сервисом.
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Ошибка загрузки копии на Яндекс Диск");
                _dialogService.ShowError("Не удалось загрузить копию на Яндекс Диск. Попробуйте ещё раз. Подробности — в файле журнала (папка logs).");
            }
            finally
            {
                CloudIsBusy = false;
            }
        }

        /// <summary>Обновление отображаемого состояния из состояния облачного сервиса.</summary>
        private void ApplyCloudState(CloudBackupState state)
        {
            if (state.UserMessage != null)
                CloudStatusMessage = state.UserMessage;
            CloudHasPendingFile = state.HasPendingFile;
            if (state.LastSuccessUtc != null)
                CloudLastSuccessUtc = state.LastSuccessUtc;
            OnPropertyChanged(nameof(CloudLastSuccessText));

            if (state.Status == CloudBackupStatus.AuthRequired)
                YandexAuthState = YandexDiskAuthState.AuthRequired;
        }

        private void OnYandexAuthStateChanged()
        {
            MarshalToUi(async () =>
            {
                try
                {
                    ApplyAuthState(await _yandexAuthService.GetStateAsync());
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "Ошибка чтения состояния подключения Яндекс Диска");
                }
            });
        }

        private void OnCloudBackupStateChanged(CloudBackupState state)
        {
            MarshalToUi(() => ApplyCloudState(state));
        }

        /// <summary>События сервисов поднимаются на рабочем потоке — возвращаемся на захваченный UI-поток.</summary>
        private void MarshalToUi(Func<Task> action)
        {
            if (_isDisposed)
                return;

            if (_uiContext != null)
                _uiContext.Post(_ => _ = action(), null);
            else
                _ = action();
        }

        private void MarshalToUi(Action action)
        {
            if (_isDisposed)
                return;

            if (_uiContext != null)
                _uiContext.Post(_ => action(), null);
            else
                action();
        }

        #endregion

        private void MarkAsChanged()
        {
            if (!IsLoading)
                HasUnsavedChanges = true;
        }

        protected override void OnDispose()
        {
            _isDisposed = true;
            _dataChangeService.DataChanged -= OnDataChanged;
            _yandexAuthService.StateChanged -= OnYandexAuthStateChanged;
            _yandexBackupService.StateChanged -= OnCloudBackupStateChanged;

            base.OnDispose();
        }

        #endregion
    }
}
