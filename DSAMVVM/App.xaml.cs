using System.Windows;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Services;
using DSAMVVM.Core.Services.AD;
using DSAMVVM.Core.Services.Updates;
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
        private IServiceProvider _serviceProvider = null!;
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

            var sw = System.Diagnostics.Stopwatch.StartNew();

            ConfigureServices();
            Services = _serviceProvider;

            if (!Globals.TryEnsureCoreDirs(out var ensureErr) && !string.IsNullOrWhiteSpace(ensureErr))
            {
                MessageBox.Show(
                    $"Some application folders could not be created.\n\nDetails: {ensureErr}",
                    "Startup Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            var bus = _serviceProvider.GetRequiredService<StatusBus>();
            UiNotify.Initialize(bus.Report, bus.RemoveByKey, bus.Clear);

            Log.Initialize(_serviceProvider.GetRequiredService<IAppLogger>(), min: AppLogLevel.Warn);

            // -> load settings before creating MainWindow / BootstrapInitialView
            var settingsSvc = _serviceProvider.GetRequiredService<ISettingsService>();
            try
            {
                _settings = settingsSvc.LoadAsync(Globals.g_SettingsPath).GetAwaiter().GetResult();
                _settings.ApplyDefaultsAndClamp();
                Log.ApplySettings(_settings);
            }
            catch (Exception ex)
            {
                Log.Warn("Settings", $"Early settings load failed, using defaults: {ex.Message}");
                _settings = new AppSettings();
                _settings.ApplyDefaultsAndClamp();
                Log.ApplySettings(_settings);
            }

            this.SessionEnding += App_SessionEnding;

            // -> enforce required gate before showing MainWindow
            try
            {
                var gate = _serviceProvider.GetRequiredService<VersionCheckerUI>();
                await gate.EnforceRequiredAsync().ConfigureAwait(true); // may shutdown if outdated                                                     
                if (this.Dispatcher.HasShutdownStarted || this.Dispatcher.HasShutdownFinished) return;  // bail if shutdown was initiated by the gate

            }
            catch (Exception ex)
            {
                Log.Warn("VersionCheck", $"Required gate failed to run: {ex.Message}");
            }

            // -> create VM/window and show
            var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVM };
            MainWindow = mainWindow;

            mainVM.BootstrapInitialView();
            mainWindow.ContentRendered += (_, __) =>
            {
                try { mainVM.StartWarmup(); } catch (Exception ex) { Log.Warn("Warmup", ex.Message); }
                Log.Info("Startup", $"first frame @ {sw.ElapsedMilliseconds} ms");
            };

            mainWindow.Closed += (_, __) => TryPersistSettingsOnce();
            mainWindow.Show();

            // -> start background scheduler (6h cadence, no idle duplicate)
            try
            {
                var scheduler = _serviceProvider.GetRequiredService<VersionUpdateScheduler>();
                scheduler.Start(TimeSpan.FromHours(6), runImmediately: false);
            }
            catch (Exception ex)
            {
                Log.Warn("VersionScheduler", $"Startup schedule failed: {ex.Message}");
            }

            Log.Info("Startup", $"window shown @ {sw.ElapsedMilliseconds} ms");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _serviceProvider.GetService<ISettingsService>()?.FlushPendingSaves(); } catch { }
            TryPersistSettingsOnce();

            // -> stop background scheduler
            (_serviceProvider.GetService<VersionUpdateScheduler>() as IDisposable)?.Dispose();

            if (_serviceProvider.GetService<IAppLogger>() is FileLogger fl)
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
            if (Interlocked.Exchange(ref _persistOnceFlag, 1) == 1) return;
            TryPersistSettings();
        }

        private void TryPersistSettings()
        {
            try
            {
                if (_settings is null) return;

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
                        if (!string.IsNullOrWhiteSpace(q))
                        {
                            main.SearchQuery = q;
                            main.ExecuteSearchCommand.Execute(null);
                        }
                    },
                    openComputer: q =>
                    {
                        var main = Services.GetRequiredService<MainViewModel>();
                        main.SelectedView = AppView.Computer;
                        if (!string.IsNullOrWhiteSpace(q))
                        {
                            main.SearchQuery = q;
                            main.ExecuteSearchCommand.Execute(null);
                        }
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

            // -> version update infrastructure
            services.AddSingleton<VersionCheckerUI>();                     // concrete
            services.AddSingleton<IVersionCheckHandler>(sp =>              // interface -> UI
                sp.GetRequiredService<VersionCheckerUI>());
            services.AddSingleton<VersionUpdateScheduler>();               // core scheduler

            _serviceProvider = services.BuildServiceProvider();
        }
    }
}
