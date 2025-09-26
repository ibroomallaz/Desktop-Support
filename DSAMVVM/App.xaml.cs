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

        public static IServiceProvider Services { get; private set; } = default!;
        public static AppSettings Settings => ((App)Current)._settings ?? new AppSettings();

        public App()
        {
            this.DispatcherUnhandledException += (_, __) => TryPersistSettingsOnce();
            AppDomain.CurrentDomain.UnhandledException += (_, __) => TryPersistSettingsOnce();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ConfigureServices();
            Services = _serviceProvider!;

            if (!Globals.TryEnsureCoreDirs(out var ensureErr) && !string.IsNullOrWhiteSpace(ensureErr))
            {
                MessageBox.Show(
                    $"Some application folders could not be created.\n\nDetails: {ensureErr}",
                    "Startup Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            var bus = _serviceProvider!.GetRequiredService<StatusBus>();
            UiNotify.Initialize(bus.Report, bus.RemoveByKey, bus.Clear);

            Log.Initialize(_serviceProvider!.GetRequiredService<IAppLogger>(), min: AppLogLevel.Warn);

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

            this.SessionEnding += App_SessionEnding;

            MainViewModel mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVM };
            MainWindow = mainWindow;

            mainVM.BootstrapInitialView();
            mainWindow.ContentRendered += (_, __) => mainVM.StartWarmup();

            mainWindow.Closed += (_, __) => TryPersistSettingsOnce();
            mainWindow.Show();

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
            try { _serviceProvider?.GetService<ISettingsService>()?.FlushPendingSaves(); } catch { }
            TryPersistSettingsOnce();

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
                if (_serviceProvider is null || _settings is null) return;

                var settingsSvc = _serviceProvider.GetRequiredService<ISettingsService>();
                settingsSvc.SaveAsync(_settings, Globals.g_SettingsPath).GetAwaiter().GetResult();

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

            services.AddSingleton<IAppLogger>(_ => new FileLogger(Globals.g_LogsDir));
            services.AddSingleton<StatusBus>();
            services.AddSingleton<StatusBarViewModel>();

            services.AddSingleton<IHttpService, HttpService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            services.AddSingleton<IDepartmentService, DepartmentService>();
            services.AddSingleton<IADService, ADService>();
            services.AddSingleton<ILinksService, LinksService>();
            services.AddSingleton<ISearchService, SearchService>();

            services.AddSingleton<IOutputTextSettingsProvider>(sp =>
                new OutputTextSettingsProvider(
                    sp.GetRequiredService<ISettingsService>(),
                    () => _settings ?? new AppSettings()));

            services.AddSingleton<IFlowDocService, FlowDocService>();

            services.AddSingleton<MainViewModel>();
            services.AddTransient<UserViewModel>();
            services.AddTransient<GroupViewModel>();
            services.AddTransient<ComputerViewModel>();
            services.AddTransient<LinksViewModel>();
            services.AddSingleton<AboutViewModel>();
            services.AddSingleton<HomeViewModel>(sp =>
                new HomeViewModel(
                    openUser: q =>
                    {
                        var main = Services.GetRequiredService<MainViewModel>();
                        main.SelectedView = AppView.User;
                        if (!string.IsNullOrWhiteSpace(q)) { main.SearchQuery = q; main.ExecuteSearchCommand.Execute(null); }
                    },
                    openComputer: q =>
                    {
                        var main = Services.GetRequiredService<MainViewModel>();
                        main.SelectedView = AppView.Computer;
                        if (!string.IsNullOrWhiteSpace(q)) { main.SearchQuery = q; main.ExecuteSearchCommand.Execute(null); }
                    },
                    goGroups: () => Services.GetRequiredService<MainViewModel>().SelectedView = AppView.Group,
                    goEntra: () => Services.GetRequiredService<MainViewModel>().SelectedView = AppView.Entra,
                    goLinks: () => Services.GetRequiredService<MainViewModel>().SelectedView = AppView.Links,
                    goAbout: () => Services.GetRequiredService<MainViewModel>().SelectedView = AppView.About
                )
            );

            services.AddTransient<Func<UserViewModel>>(sp => () => sp.GetRequiredService<UserViewModel>());
            services.AddTransient<Func<GroupViewModel>>(sp => () => sp.GetRequiredService<GroupViewModel>());
            services.AddTransient<Func<ComputerViewModel>>(sp => () => sp.GetRequiredService<ComputerViewModel>());
            services.AddTransient<Func<LinksViewModel>>(sp => () => sp.GetRequiredService<LinksViewModel>());

            services.AddTransient<VersionCheckerUI>();

            _serviceProvider = services.BuildServiceProvider();
        }
    }
}
