using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Services;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace DSAMVVM
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        private string _settingsPath = string.Empty;
        private AppSettings? _settings;
        public App()
        {
            // Last-ditch persistence on unexpected crashes
            this.DispatcherUnhandledException += (s, e) =>
            {
                TryPersistSettings();
                // Let default crash dialog show;
            };
            AppDomain.CurrentDomain.UnhandledException += (_, __) =>
            {
                TryPersistSettings();
            };
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureServices();

            // Ensure global app dir exists
            Directory.CreateDirectory(Globals.g_AppDir);

            _settingsPath = Path.Combine(Globals.g_AppDir, "settings.json");

            // Load settings (service is self-healing; creates/repairs as needed)
            var settingsSvc = _serviceProvider!.GetRequiredService<ISettingsService>();
            try
            {
                await settingsSvc.LoadAsync(_settingsPath);
            }
            catch (Exception ex)
            {
                // Non-fatal: continue with defaults
                MessageBox.Show(
                    $"Settings could not be fully loaded. Using defaults.\n\nDetails: {ex.Message}",
                    "Settings Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            // Save on OS logoff/shutdown as well
            this.SessionEnding += App_SessionEnding;

            var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVM };
            MainWindow = mainWindow;

            // Also persist when the main window closes
            mainWindow.Closed += (_, __) => TryPersistSettings();

            mainWindow.Show();

            // run version check in the background
            _ = Task.Run(async () =>
            {
                try
                {
                    var versionChecker = _serviceProvider.GetRequiredService<VersionCheckerUI>();
                    await versionChecker.CheckAsync();
                }
                catch
                {
                    // TODO: background version check errors (log if desired)
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            TryPersistSettings();
            base.OnExit(e);
        }

        private void App_SessionEnding(object? sender, SessionEndingCancelEventArgs e)
        {
            // Persist on user logoff or shutdown; swallow errors to not block shutdown
            try { TryPersistSettings(); } catch { }
        }

        private void TryPersistSettings()
        {
            try
            {
                if (_serviceProvider is null || string.IsNullOrWhiteSpace(_settingsPath) || _settings is null) return;
                var settingsSvc = _serviceProvider.GetRequiredService<ISettingsService>();
                settingsSvc.SaveAsync(_settings, _settingsPath).GetAwaiter().GetResult();
            }
            catch
            {
                //TODO: Add logging
            }
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core shared services
            services.AddSingleton<StatusBarViewModel>();
            services.AddSingleton<IStatusReporter>(sp => sp.GetRequiredService<StatusBarViewModel>());

            // HTTP + Settings
            services.AddSingleton<IHttpService, HttpService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            // Domain services
            services.AddSingleton<IDepartmentService, DepartmentService>();
            services.AddSingleton<IADService, ADService>();
            services.AddSingleton<ILinksService, LinksService>();
            services.AddSingleton<ISearchService, SearchService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<UserViewModel>();
            services.AddTransient<GroupViewModel>();
            services.AddTransient<ComputerViewModel>();
            services.AddTransient<LinksViewModel>();

            // ViewModel factories for MainViewModel constructor
            services.AddTransient<Func<UserViewModel>>(sp => () => sp.GetRequiredService<UserViewModel>());
            services.AddTransient<Func<GroupViewModel>>(sp => () => sp.GetRequiredService<GroupViewModel>());
            services.AddTransient<Func<ComputerViewModel>>(sp => () => sp.GetRequiredService<ComputerViewModel>());

            // Utilities
            services.AddTransient<VersionCheckerUI>();

            _serviceProvider = services.BuildServiceProvider();
        }
    }
}
