using AutoKassa.Services;
using AutoKassa.ViewModels;
using AutoKassa.ViewModels.Reports;
using AutoKassa.Views;
using Microsoft.Extensions.DependencyInjection;
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
            base.OnStartup(e);

            // Создаем коллекцию сервисов
            var services = new ServiceCollection();

            // Регистрация сервисов
            ConfigureServices(services);

            // Создаем провайдер сервисов
            _serviceProvider = services.BuildServiceProvider();

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
                    Shutdown();
                    return;
                }
            }

            // Показываем главное окно
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        /// <summary>
        /// Конфигурация сервисов для DI
        /// </summary>
        private void ConfigureServices(IServiceCollection services)
        {
            // Регистрация DbContext
            services.AddDbContext<AppDbContext>();

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
            services.AddTransient<TransactionsViewModel>();
            services.AddTransient<TransactionEditViewModel>();
            services.AddTransient<CategoriesViewModel>();
            services.AddTransient<CategoryEditViewModel>();
            services.AddTransient<ReportsViewModel>();
            services.AddTransient<BalanceReportViewModel>();
            services.AddTransient<CategoryReportViewModel>();



            // Регистрация Views
            services.AddSingleton<MainWindow>(provider =>
            {
                var viewModel = provider.GetRequiredService<MainWindowViewModel>();
                return new MainWindow(viewModel);
            });
        }

        /// <summary>
        /// Очистка ресурсов при закрытии приложения
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
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