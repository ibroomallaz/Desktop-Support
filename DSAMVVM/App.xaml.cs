using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Services;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Windows;

namespace DSAMVVM
{
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        private string _settingsPath = string.Empty;
        private AppSettings? _settings;

        // Expose DI for behaviors (e.g., FlowDocBinder)
        public static IServiceProvider Services { get; private set; } = default!;

        // NEW: expose the live in-memory settings object (used by UserView buttons, etc.)
        public static AppSettings Settings => ((App)Current)._settings ?? new AppSettings();

        public App()
        {
            // Last-ditch persistence on unexpected crashes
            this.DispatcherUnhandledException += (s, e) =>
            {
                TryPersistSettings();
                // Let default crash dialog show
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
            Services = _serviceProvider!; // make DI available app-wide

            // Ensure core dirs; if something is wrong, warn but continue
            if (!Globals.TryEnsureCoreDirs(out var ensureErr) && !string.IsNullOrWhiteSpace(ensureErr))
            {
                MessageBox.Show(
                    $"Some application folders could not be created.\n\nDetails: {ensureErr}",
                    "Startup Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            _settingsPath = Path.Combine(Globals.g_AppDir, "settings.json");

            // Initialize facades that need singletons from DI
            UiNotify.Initialize(_serviceProvider!.GetRequiredService<StatusBus>());
            Log.Initialize(_serviceProvider!.GetRequiredService<IAppLogger>(), min: AppLogLevel.Warn);

            // Load settings (service is self-healing; creates/repairs as needed)
            var settingsSvc = _serviceProvider!.GetRequiredService<ISettingsService>();
            try
            {
                _settings = await settingsSvc.LoadAsync(_settingsPath);
                _settings.ApplyDefaultsAndClamp();

                // Apply logging prefs (sets retention and enables cleanup)
                Log.ApplySettings(_settings);
            }
            catch (Exception ex)
            {
                // Non-fatal: continue with defaults
                MessageBox.Show(
                    $"Settings could not be fully loaded. Using defaults.\n\nDetails: {ex.Message}",
                    "Settings Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                // Make sure we still have a usable settings object
                _settings ??= new AppSettings();
                _settings.ApplyDefaultsAndClamp();
                Log.ApplySettings(_settings);
            }

            // Persist on OS logoff/shutdown as well
            this.SessionEnding += App_SessionEnding;

            // Create and show main window
            var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVM };
            MainWindow = mainWindow;

            // Also persist when the main window closes
            mainWindow.Closed += (_, __) => TryPersistSettings();

            mainWindow.Show();

            // Run version check in the background
            try
            {
                var versionChecker = _serviceProvider.GetRequiredService<VersionCheckerUI>();
                await versionChecker.CheckAsync();
            }
            catch (Exception ex)
            {
                Log.Warn("VersionCheck", $"Version check failed: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Flush any debounced saves first, then do a final persisted save
            try { _serviceProvider?.GetService<ISettingsService>()?.FlushPendingSaves(); } catch { }
            TryPersistSettings();

            // Flush/close log file
            if (_serviceProvider?.GetService<IAppLogger>() is FileLogger fl)
                fl.Dispose();

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

                // Notify any listeners (e.g., FlowDocBinder) that sizes may have changed
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

            // Logging sink (no cleanup until Log.ApplySettings runs)
            services.AddSingleton<IAppLogger>(_ => new FileLogger(Globals.g_LogsDir));

            // Status bus for single-line status bar + UiNotify
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
