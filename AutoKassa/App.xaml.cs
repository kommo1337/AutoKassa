using AutoKassa.Services;
using AutoKassa.Services.Backup;
using AutoKassa.Services.CreditCards;
using AutoKassa.Services.Dashboard;
using AutoKassa.Services.Settings;
using AutoKassa.Services.Transactions;
using AutoKassa.ViewModels;
using AutoKassa.ViewModels.Reports;
using AutoKassa.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Threading.Tasks;
using System.Windows;

namespace AutoKassa
{
    /// <summary>
    /// Главный класс приложения с настройкой Dependency Injection
    /// </summary>
    public partial class App : Application
    {
        private ServiceProvider _serviceProvider;

        /// <summary>
        /// Конфигурирование DI контейнера при запуске приложения
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            // Инициализируем Serilog ДО всего остального, чтобы ловить ошибки старта
            var logPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)!,
                "logs", "autokassa-.log");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            // Глобальные обработчики необработанных исключений
            DispatcherUnhandledException += (_, args) =>
            {
                Log.Fatal(args.Exception, "Необработанное исключение UI (Dispatcher)");
                ShowFatalError(args.Exception);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Log.Fatal(args.ExceptionObject as Exception, "Необработанное исключение домена приложения");
                Log.CloseAndFlush();
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error(args.Exception, "Необработанное исключение в Task");
                args.SetObserved();
            };

            Log.Information("=== AutoKassa запуск, версия {Version} ===",
                System.Reflection.Assembly.GetExecutingAssembly().GetName().Version);

            try
            {
                base.OnStartup(e);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Критическая ошибка при OnStartup(base)");
                ShowFatalError(ex);
                Shutdown(1);
                return;
            }

            // Создаем коллекцию сервисов
            var services = new ServiceCollection();

            // Регистрация сервисов
            ConfigureServices(services);

            // Создаем провайдер сервисов
            try
            {
                _serviceProvider = services.BuildServiceProvider();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Ошибка построения DI контейнера");
                ShowFatalError(ex);
                Shutdown(1);
                return;
            }

            // Асинхронная инициализация выполняется синхронно на фоновом потоке,
            // чтобы избежать deadlock на UI-потоке и позволить обработать ошибки.
            try
            {
                Task.Run(() => InitializeAsync(_serviceProvider)).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Ошибка инициализации приложения (миграция БД / настройки)");
                ShowFatalError(ex);
                Shutdown(1);
                return;
            }

            // Модальные диалоги до главного окна: при ShutdownMode.OnLastWindowClose
            // (по умолчанию) закрытие единственного на тот момент окна инициирует
            // Application.Shutdown(), и показанный следом MainWindow уже не спасает —
            // приложение завершается сразу после старта. Отключаем на время диалогов.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Проверяем, установлен ли пароль
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();

            if (!settingsService.IsPasswordSet())
            {
                // Пароль не установлен - показываем экран первоначальной настройки
                var setupViewModel = _serviceProvider.GetRequiredService<InitialSetupViewModel>();
                var setupWindow = new InitialSetupView(setupViewModel);

                var result = setupWindow.ShowDialog();

                if (result != true)
                {
                    // Пользователь закрыл окно настройки - закрываем приложение
                    Log.Information("Пользователь закрыл начальную настройку — завершение");
                    Shutdown();
                    return;
                }
            }

            // Показываем главное окно
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();

            // Возвращаем штатное завершение при закрытии последнего окна
            ShutdownMode = ShutdownMode.OnLastWindowClose;
            Log.Information("Главное окно открыто");

            // Авто-бэкап в фоне (не блокирует запуск);
            // ошибки логируем явно, иначе они потеряются в fire-and-forget задаче
            _ = settingsService.RunAutoBackupIfDueAsync().ContinueWith(
                t => Log.Error(t.Exception, "Ошибка авто-бэкапа при запуске"),
                TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Асинхронная инициализация: миграции БД и разогрев кеша настроек.
        /// </summary>
        private static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            var contextFactory = serviceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<AppDbContext>>();
            await SettingsService.MigrateAsync(contextFactory).ConfigureAwait(false);

            var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            // Разогреваем кеш настроек, чтобы все sync-чтения работали без I/O
            await settingsService.GetSettingsAsync().ConfigureAwait(false);
        }

        private static void ShowFatalError(Exception ex)
        {
            MessageBox.Show(
                $"Критическая ошибка:\n\n{ex.Message}\n\nПодробности записаны в журнал logs/autokassa-*.log",
                "AutoKassa — Ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        /// <summary>
        /// Конфигурация сервисов для DI
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // Фабрика DbContext — единственный способ доступа к БД для сервисов.
            // AppDbContext намеренно не регистрируется для прямого инжектирования:
            // контекст создаётся короткоживущим через IDbContextFactory на каждую операцию.
            // Пулинг переиспользует экземпляры контекста и снижает аллокации на слабом железе.
            // Строка подключения задаётся здесь, а не в OnConfiguring: при пулинге контекстов
            // модифицировать DbContextOptions в OnConfiguring запрещено (EF Core бросает
            // InvalidOperationException). Per-connection PRAGMA (synchronous/temp_store/cache_size)
            // выставляет SqlitePragmaInterceptor при каждом открытии соединения.
            services.AddPooledDbContextFactory<AppDbContext>(options =>
            {
                options.UseSqlite($"Data Source={AppDbContext.GetDatabasePath()}");
                options.AddInterceptors(new SqlitePragmaInterceptor());
            });

            // Регистрация базовых сервисов
            services.AddSingleton<INavigationService, AutoKassa.Services.NavigationService>();
            services.AddSingleton<IPasswordService, PasswordService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IToastNotificationService, ToastNotificationService>();
            services.AddSingleton<ILockService, LockService>();
            services.AddTransient<ITransactionService, TransactionService>();
            services.AddTransient<ITransactionListService, TransactionListService>();
            services.AddTransient<ITransactionGroupingService, TransactionGroupingService>();
            services.AddTransient<IInlineTransactionService, InlineTransactionService>();
            services.AddTransient<ITransactionSelectionService, TransactionSelectionService>();
            services.AddTransient<ICategoryService, CategoryService>();
            services.AddTransient<ICounterpartyService, CounterpartyService>();
            services.AddTransient<IDebtService, DebtService>();
            services.AddTransient<IReportService, ReportService>();
            services.AddTransient<ICreditCardService, CreditCardService>();
            services.AddSingleton<IExportService, ExportService>();
            services.AddSingleton<IDataChangeService, DataChangeService>();

            // Кэш справочников: singleton, используется transient-сервисами
            // категорий/контрагентов/карт; инвалидируется ими при мутациях
            // и полностью — по IDataChangeService.DataChanged.
            services.AddSingleton<ReferenceDataCache>();

            // Сервисы настроек (декомпозиция SettingsViewModel)
            services.AddSingleton<ISettingsMapper, SettingsMapper>();
            services.AddSingleton<ISettingsValidator, SettingsValidator>();
            services.AddSingleton<IAppInfoService, AppInfoService>();
            services.AddSingleton<IBackupCoordinator, BackupCoordinator>();
            services.AddTransient<ICreditCardSettingsService, CreditCardSettingsService>();

            // Регистрация фабрики отчётных ViewModel (убирает static Service Locator)
            services.AddSingleton<IReportViewModelFactory, ReportViewModelFactory>();

            // Регистрация фабрики редактирования операции (убирает дублирование создания VM)
            services.AddSingleton<ITransactionEditViewModelFactory, TransactionEditViewModelFactory>();

            // Регистрация фасадного сервиса дашборда (декомпозиция God-object VM)
            services.AddTransient<IDashboardService, DashboardService>();

            // Регистрация координаторов дашборда
            services.AddTransient<IDashboardPeriodCoordinator, DashboardPeriodCoordinator>();
            services.AddTransient<IDashboardTransactionCoordinator, DashboardTransactionCoordinator>();

            // Регистрация ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<InitialSetupViewModel>();
            // Страницы верхнего уровня (вкладки) — singleton-ы: живут всё время работы
            // приложения, не пересоздаются при навигации (см. IPageViewModel),
            // обновление данных — через _needsRefresh по IDataChangeService.DataChanged.
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<TransactionsViewModel>();
            services.AddTransient<TransactionEditViewModel>();
            services.AddSingleton<CategoriesViewModel>();
            services.AddTransient<CategoryManagerViewModel>();
            services.AddSingleton<ReportsViewModel>();
            services.AddTransient<BalanceReportViewModel>();
            services.AddTransient<CategoryReportViewModel>();
            services.AddTransient<TransactionDetailReportViewModel>();
            services.AddTransient<DebtReportViewModel>();
            services.AddSingleton<CounterpartiesViewModel>();
            services.AddTransient<CounterpartyEditViewModel>();
            services.AddSingleton<CreditCardsViewModel>();
            services.AddTransient<CreditCardEditViewModel>();
            services.AddTransient<CreditCardRepaymentViewModel>();
            services.AddTransient<ReconciliationViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddTransient<ChangePasswordViewModel>();
            services.AddTransient<LockScreenViewModel>();
            services.AddTransient<PasswordRecoveryViewModel>();

            // Регистрация Views
            services.AddTransient<ChangePasswordView>();
            services.AddTransient<LockScreenView>();

            services.AddSingleton<MainWindow>(provider =>
            {
                var viewModel = provider.GetRequiredService<MainWindowViewModel>();
                var toastService = provider.GetRequiredService<IToastNotificationService>();
                return new MainWindow(viewModel, toastService);
            });
        }

        /// <summary>
        /// Очистка ресурсов при закрытии приложения
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            Log.Information("=== AutoKassa завершение ===");
            Log.CloseAndFlush();
            _serviceProvider?.Dispose();
            base.OnExit(e);
        }

    }
}