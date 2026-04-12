using AutoKassa.Services;
using AutoKassa.ViewModels;
using AutoKassa.ViewModels.Reports;
using AutoKassa.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
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

            // Проверяем, установлен ли пароль
            ISettingsService settingsService;
            try
            {
                settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Ошибка инициализации SettingsService (миграция БД?)");
                ShowFatalError(ex);
                Shutdown(1);
                return;
            }

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
            Log.Information("Главное окно открыто");

            // Авто-бэкап в фоне (не блокирует запуск)
            _ = settingsService.RunAutoBackupIfDueAsync();
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
            // Регистрация DbContext + фабрика для Singleton-сервисов
            services.AddDbContext<AppDbContext>();
            services.AddDbContextFactory<AppDbContext>();

            // Регистрация базовых сервисов
            services.AddSingleton<INavigationService, AutoKassa.Services.NavigationService>();
            services.AddSingleton<IPasswordService, PasswordService>();
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IToastNotificationService, ToastNotificationService>();
            services.AddSingleton<ILockService, LockService>();
            services.AddScoped<ITransactionService, TransactionService>();
            services.AddScoped<ICategoryService, CategoryService>();
            services.AddScoped<IReportService, ReportService>();
            services.AddSingleton<IExportService, ExportService>();

            // Регистрация ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<InitialSetupViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<TransactionsViewModel>();
            services.AddTransient<TransactionEditViewModel>();
            services.AddTransient<CategoriesViewModel>();
            services.AddTransient<CategoryManagerViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<BalanceReportViewModel>();
            services.AddTransient<CategoryReportViewModel>();
            services.AddTransient<TransactionDetailReportViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ChangePasswordViewModel>();



            // Регистрация Views
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

        /// <summary>
        /// Публичный доступ к Service Provider для получения сервисов из UI
        /// </summary>
        public static T GetService<T>() where T : class
        {
            return ((App)Current)._serviceProvider.GetService<T>();
        }
    }
}