using System.Windows;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Services;
using DSAMVVM.Core.Services.AD;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Services.Status;
using DSAMVVM.MVVM.Services.Updates;
using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;

namespace DSAMVVM
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        private AppSettings? _settings;
        private int _persistOnceFlag;

        // DI for behaviors (e.g., FlowDocBinder)
        public static IServiceProvider Services { get; private set; } = default!;

        // Live in-memory settings
        public static AppSettings Settings => ((App)Current)._settings ?? new AppSettings();

        public App()
        {
            // Persist on unhandled exceptions
            this.DispatcherUnhandledException += (_, __) => TryPersistSettingsOnce();
            AppDomain.CurrentDomain.UnhandledException += (_, __) => TryPersistSettingsOnce();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureServices();
            Services = _serviceProvider!;

            // Ensure core dirs (warn but continue on failure)
            if (!Globals.TryEnsureCoreDirs(out var ensureErr) && !string.IsNullOrWhiteSpace(ensureErr))
            {
                MessageBox.Show(
                    $"Some application folders could not be created.\n\nDetails: {ensureErr}",
                    "Startup Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            // Initialize UiNotify and logging
            var bus = _serviceProvider!.GetRequiredService<StatusBus>();
            UiNotify.Initialize(bus.Report, bus.RemoveByKey, bus.Clear);

            Log.Initialize(_serviceProvider!.GetRequiredService<IAppLogger>(), min: AppLogLevel.Warn);

            // Load settings
            var settingsSvc = _serviceProvider!.GetRequiredService<ISettingsService>();
            try
            {
                _settings = await settingsSvc.LoadAsync(Globals.g_SettingsPath).ConfigureAwait(true);
                _settings.ApplyDefaultsAndClamp();
                Log.ApplySettings(_settings);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Settings could not be fully loaded. Using defaults.\n\nDetails: {ex.Message}",
                    "Settings Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _settings ??= new AppSettings();
                _settings.ApplyDefaultsAndClamp();
                Log.ApplySettings(_settings);
            }

            // Persist on OS logoff/shutdown
            this.SessionEnding += App_SessionEnding;

            // Main window
            var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVM };
            MainWindow = mainWindow;
            mainWindow.Closed += (_, __) => TryPersistSettingsOnce();
            mainWindow.Show();

            // Background version check
            try
            {
                var versionChecker = _serviceProvider.GetRequiredService<VersionCheckerUI>();
                await versionChecker.CheckAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                Log.Warn("VersionCheck", $"Version check failed: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Flush debounced saves then persist once
            try { _serviceProvider?.GetService<ISettingsService>()?.FlushPendingSaves(); } catch { }
            TryPersistSettingsOnce();

            // Close log file
            if (_serviceProvider?.GetService<IAppLogger>() is FileLogger fl)
            {
                fl.Dispose();
            }

            base.OnExit(e);
        }

        private void App_SessionEnding(object? sender, SessionEndingCancelEventArgs e)
        {
            try { TryPersistSettingsOnce(); } catch { }
        }

        // Persist-once guard
        private void TryPersistSettingsOnce()
        {
            if (Interlocked.Exchange(ref _persistOnceFlag, 1) == 1)
            {
                return;
            }

            TryPersistSettings();
        }

        // Final settings persist
        private void TryPersistSettings()
        {
            try
            {
                if (_serviceProvider is null || _settings is null)
                {
                    return;
                }

                var settingsSvc = _serviceProvider.GetRequiredService<ISettingsService>();
                settingsSvc.SaveAsync(_settings, Globals.g_SettingsPath).GetAwaiter().GetResult();

                // Notify listeners that settings may have changed
                _serviceProvider.GetService<IOutputTextSettingsProvider>()?.NotifyChanged();
            }
            catch (Exception ex)
            {
                Log.Warn("SettingsPersist", $"Unable to persist settings on shutdown: {ex.Message}");
            }
        }

        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // Logging sink
            services.AddSingleton<IAppLogger>(_ => new FileLogger(Globals.g_LogsDir));

            // Status bus for status bar + UiNotify
            services.AddSingleton<StatusBus>();

            // Core shared services
            services.AddSingleton<StatusBarViewModel>();

            // HTTP + Settings
            services.AddSingleton<IHttpService, HttpService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            // Domain services
            services.AddSingleton<IDepartmentService, DepartmentService>();
            services.AddSingleton<IADService, ADService>();
            services.AddSingleton<ILinksService, LinksService>();
            services.AddSingleton<ISearchService, SearchService>();

            // FlowDocument rendering + settings bridge
            services.AddSingleton<IOutputTextSettingsProvider>(sp =>
                new OutputTextSettingsProvider(
                    sp.GetRequiredService<ISettingsService>(),
                    () => _settings ?? new AppSettings()));

            services.AddSingleton<IFlowDocService, FlowDocService>();

            // ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddTransient<UserViewModel>();
            services.AddTransient<GroupViewModel>();
            services.AddTransient<ComputerViewModel>();
            services.AddTransient<LinksViewModel>();
            services.AddSingleton<AboutViewModel>();

            // ViewModel factories for MainViewModel
            services.AddTransient<Func<UserViewModel>>(sp => () => sp.GetRequiredService<UserViewModel>());
            services.AddTransient<Func<GroupViewModel>>(sp => () => sp.GetRequiredService<GroupViewModel>());
            services.AddTransient<Func<ComputerViewModel>>(sp => () => sp.GetRequiredService<ComputerViewModel>());
            services.AddTransient<Func<LinksViewModel>>(sp => () => sp.GetRequiredService<LinksViewModel>());

            // Utilities
            services.AddTransient<VersionCheckerUI>();

            _serviceProvider = services.BuildServiceProvider();
        }
    }
}
