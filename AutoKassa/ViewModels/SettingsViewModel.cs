using AutoKassa.Helpers;
using AutoKassa.Models;
using AutoKassa.Services;
using AutoKassa.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace AutoKassa.ViewModels
{
    /// <summary>
    /// ViewModel для окна настроек
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ISettingsService _settingsService;
        private readonly IDialogService _dialogService;
        private readonly IToastNotificationService _toastService;

        #region Поля

        private AppSettings _currentSettings;
        private AppSettings _originalSettings;
        private bool _isLoading;
        private bool _hasUnsavedChanges;
        private int _selectedTabIndex;

        // Общие настройки
        private bool _autoLockEnabled;
        private int _autoLockMinutes;
        private bool _showNotifications;
        private string _selectedTheme;

        // Настройки операций
        private bool _showOperationsInSidebar;
        private int _defaultPageSize;
        private bool _confirmDelete;

        // Настройки отчетов
        private string _selectedDefaultPeriod;
        private bool _autoGenerateReports;

        // Резервное копирование
        private bool _backupEnabled;
        private int _autoBackupDays;
        private string _backupPath;

        // Безопасность
        private bool _requirePasswordOnStartup;
        private int _passwordExpireDays;

        // Финансы
        private decimal _initialBalance;

        // Интерфейс
        private string _selectedLanguage;
        private double _windowWidth;
        private double _windowHeight;

        #endregion

        #region Коллекции

        public ObservableCollection<string> AvailableThemes { get; } = new()
        {
            "Light",
            "Dark"
        };

        public ObservableCollection<string> AvailableLanguages { get; } = new()
        {
            "ru-RU",
            "en-US"
        };

        public ObservableCollection<string> AvailablePeriods { get; } = new()
        {
            "Today",
            "Week",
            "Month",
            "Quarter",
            "Year"
        };

        #endregion

        public SettingsViewModel(
            ISettingsService settingsService,
            IDialogService dialogService,
            IToastNotificationService toastService)
        {
            _settingsService = settingsService;
            _dialogService = dialogService;
            _toastService = toastService;

            // Команды
            SaveCommand = new RelayCommand(async _ => await SaveAsync(), _ => HasUnsavedChanges);
            CancelCommand = new RelayCommand(_ => Cancel());
            ResetCommand = new RelayCommand(async _ => await ResetToDefaultsAsync());
            ExportCommand = new RelayCommand(async _ => await ExportSettingsAsync());
            ImportCommand = new RelayCommand(async _ => await ImportSettingsAsync());
            ChangePasswordCommand = new RelayCommand(_ => ChangePassword());
            SelectBackupPathCommand = new RelayCommand(_ => SelectBackupPath());
            CreateBackupCommand = new RelayCommand(async _ => await CreateBackupAsync());
            RestoreBackupCommand = new RelayCommand(async _ => await RestoreBackupAsync());

            // Загрузка данных
            _ = LoadSettingsAsync();
        }

        #region Свойства

        /// <summary>
        /// Идет загрузка
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Есть несохраненные изменения
        /// </summary>
        public bool HasUnsavedChanges
        {
            get => _hasUnsavedChanges;
            set => SetProperty(ref _hasUnsavedChanges, value);
        }

        /// <summary>
        /// Индекс выбранной вкладки
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        #region Общие настройки

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

        public string SelectedTheme
        {
            get => _selectedTheme;
            set { if (SetProperty(ref _selectedTheme, value)) MarkAsChanged(); }
        }

        #endregion

        #region Настройки операций

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

        #endregion

        #region Настройки отчетов

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

        #region Финансовые настройки

        public decimal InitialBalance
        {
            get => _initialBalance;
            set { if (SetProperty(ref _initialBalance, value)) MarkAsChanged(); }
        }

        #endregion

        #region Интерфейс

        public string SelectedLanguage
        {
            get => _selectedLanguage;
            set { if (SetProperty(ref _selectedLanguage, value)) MarkAsChanged(); }
        }

        public double WindowWidth
        {
            get => _windowWidth;
            set { if (SetProperty(ref _windowWidth, value)) MarkAsChanged(); }
        }

        public double WindowHeight
        {
            get => _windowHeight;
            set { if (SetProperty(ref _windowHeight, value)) MarkAsChanged(); }
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

        /// <summary>
        /// Загрузить настройки
        /// </summary>
        private async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;

                _currentSettings = await _settingsService.GetSettingsAsync();
                _originalSettings = CloneSettings(_currentSettings);

                // Заполняем свойства
                LoadPropertiesFromSettings(_currentSettings);

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

        /// <summary>
        /// Загрузить свойства из настроек
        /// </summary>
        private void LoadPropertiesFromSettings(AppSettings settings)
        {
            _autoLockEnabled = settings.AutoLockEnabled;
            _autoLockMinutes = settings.AutoLockTimeout;
            _showNotifications = settings.ShowNotifications;
            _selectedTheme = settings.Theme;

            _showOperationsInSidebar = settings.ShowOperationsInSidebar;
            _defaultPageSize = settings.DefaultPageSize;
            _confirmDelete = settings.ConfirmDelete;

            _selectedDefaultPeriod = settings.DefaultPeriodFilter;
            _autoGenerateReports = settings.AutoGenerateReports;

            _backupEnabled = settings.BackupEnabled;
            _autoBackupDays = settings.AutoBackupDays;
            _backupPath = settings.BackupPath ?? string.Empty;

            _requirePasswordOnStartup = settings.RequirePasswordOnStartup;
            _passwordExpireDays = settings.PasswordExpireDays;

            _initialBalance = settings.InitialBalance;

            _selectedLanguage = settings.Language;
            _windowWidth = settings.WindowWidth;
            _windowHeight = settings.WindowHeight;

            // Уведомляем UI
            OnPropertyChanged(nameof(AutoLockEnabled));
            OnPropertyChanged(nameof(AutoLockMinutes));
            OnPropertyChanged(nameof(ShowNotifications));
            OnPropertyChanged(nameof(SelectedTheme));
            OnPropertyChanged(nameof(ShowOperationsInSidebar));
            OnPropertyChanged(nameof(DefaultPageSize));
            OnPropertyChanged(nameof(ConfirmDelete));
            OnPropertyChanged(nameof(SelectedDefaultPeriod));
            OnPropertyChanged(nameof(AutoGenerateReports));
            OnPropertyChanged(nameof(BackupEnabled));
            OnPropertyChanged(nameof(AutoBackupDays));
            OnPropertyChanged(nameof(BackupPath));
            OnPropertyChanged(nameof(RequirePasswordOnStartup));
            OnPropertyChanged(nameof(PasswordExpireDays));
            OnPropertyChanged(nameof(InitialBalance));
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(WindowWidth));
            OnPropertyChanged(nameof(WindowHeight));
        }

        /// <summary>
        /// Сохранить настройки
        /// </summary>
        private async Task SaveAsync()
        {
            try
            {
                // Валидация
                if (!ValidateSettings()) return;

                IsLoading = true;

                // Применяем изменения к настройкам
                ApplyPropertiesToSettings(_currentSettings);

                await _settingsService.SaveSettingsAsync(_currentSettings);

                _originalSettings = CloneSettings(_currentSettings);
                HasUnsavedChanges = false;

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

        /// <summary>
        /// Применить свойства к настройкам
        /// </summary>
        private void ApplyPropertiesToSettings(AppSettings settings)
        {
            settings.AutoLockEnabled = AutoLockEnabled;
            settings.AutoLockTimeout = AutoLockMinutes;
            settings.ShowNotifications = ShowNotifications;
            settings.Theme = SelectedTheme;

            settings.ShowOperationsInSidebar = ShowOperationsInSidebar;
            settings.DefaultPageSize = DefaultPageSize;
            settings.ConfirmDelete = ConfirmDelete;

            settings.DefaultPeriodFilter = SelectedDefaultPeriod;
            settings.AutoGenerateReports = AutoGenerateReports;

            settings.BackupEnabled = BackupEnabled;
            settings.AutoBackupDays = AutoBackupDays;
            settings.BackupPath = string.IsNullOrWhiteSpace(BackupPath) ? null : BackupPath;

            settings.RequirePasswordOnStartup = RequirePasswordOnStartup;
            settings.PasswordExpireDays = PasswordExpireDays;

            settings.InitialBalance = InitialBalance;

            settings.Language = SelectedLanguage;
            settings.WindowWidth = WindowWidth;
            settings.WindowHeight = WindowHeight;
        }

        /// <summary>
        /// Валидация настроек
        /// </summary>
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

            return true;
        }

        /// <summary>
        /// Отменить изменения
        /// </summary>
        private void Cancel()
        {
            LoadPropertiesFromSettings(_originalSettings);
            HasUnsavedChanges = false;
        }

        /// <summary>
        /// Сбросить к значениям по умолчанию
        /// </summary>
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

        /// <summary>
        /// Экспортировать настройки
        /// </summary>
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

        /// <summary>
        /// Импортировать настройки
        /// </summary>
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

        /// <summary>
        /// Изменить пароль
        /// </summary>
        private void ChangePassword()
        {
            var viewModel = new ChangePasswordViewModel(_settingsService, _dialogService, _toastService);
            var window = new ChangePasswordView(viewModel)
            {
                Owner = Application.Current.MainWindow
            };

            window.ShowDialog();
        }

        /// <summary>
        /// Выбрать папку для резервных копий
        /// </summary>
        private void SelectBackupPath()
        {
            // Используем SaveFileDialog для выбора папки (выбираем любой файл в нужной папке)
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
                BackupPath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }
        }

        /// <summary>
        /// Создать резервную копию
        /// </summary>
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

                if (dialog.ShowDialog() != true)
                    return;

                targetPath = System.IO.Path.GetDirectoryName(dialog.FileName) ?? string.Empty;
            }

            try
            {
                IsLoading = true;

                var backupFile = await _settingsService.CreateBackupAsync(targetPath);

                if (!string.IsNullOrEmpty(backupFile))
                    _toastService.ShowSuccess($"Резервная копия создана:\n{backupFile}");
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

        /// <summary>
        /// Восстановить из резервной копии
        /// </summary>
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

                    // Перезапуск приложения
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

        /// <summary>
        /// Пометить как измененное
        /// </summary>
        private void MarkAsChanged()
        {
            if (!IsLoading)
                HasUnsavedChanges = true;
        }

        /// <summary>
        /// Клонировать настройки
        /// </summary>
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
                InitialBalance = source.InitialBalance
            };
        }

        #endregion
    }
}
