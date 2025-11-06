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
using DSAMVVM.MVVM.View;
using Microsoft.Extensions.DependencyInjection;

namespace DSAMVVM
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider = null!;
        private AppSettings? _settings;
        private int _persistOnceFlag;
        private SplashWindow? _splash;

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

            // splash appears immediately; hides on first render
            _splash = new SplashWindow();
            _splash.SourceInitialized += (_, __) =>
            {
                var area = SystemParameters.WorkArea;
                _splash.Left = area.Left + (area.Width - _splash.Width) / 2;
                _splash.Top = area.Top + (area.Height - _splash.Height) / 2;
            };
            _splash.Show();
            _splash.UpdateStatus("Starting…");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long Mark(string label)
            {
                var ms = sw.ElapsedMilliseconds;
                Log.Debug("Startup", $"{label} @ {ms} ms");
                _splash?.UpdateStatus(label + "…");
                return ms;
            }

            ConfigureServices();
            Services = _serviceProvider;
            Mark("Loading services");

            if (!Globals.TryEnsureCoreDirs(out var ensureErr) && !string.IsNullOrWhiteSpace(ensureErr))
            {
                MessageBox.Show(
                    $"Some application folders could not be created.\n\nDetails: {ensureErr}",
                    "Startup Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            // logging + UI notify bus ready before any early logs
            var bus = _serviceProvider.GetRequiredService<StatusBus>();
            UiNotify.Initialize(bus.Report, bus.RemoveByKey, bus.Clear);
            Log.Initialize(_serviceProvider.GetRequiredService<IAppLogger>(), min: AppLogLevel.Debug);
            Mark("Logger ready");

            // load settings on the UI context; clamps + apply to logging
            var settingsSvc = _serviceProvider.GetRequiredService<ISettingsService>();
            try
            {
                _ = Mark("Loading settings");
                _settings = await settingsSvc.LoadAsync(Globals.g_SettingsPath).ConfigureAwait(true);
                _settings.ApplyDefaultsAndClamp();
                Log.ApplySettings(_settings);
                Mark("Settings loaded");
            }
            catch (Exception ex)
            {
                Log.Warn("Settings", $"Early settings load failed, using defaults: {ex.Message}");
                _settings = new AppSettings();
                _settings.ApplyDefaultsAndClamp();
                Log.ApplySettings(_settings);
                Mark("Using default settings");
            }

            this.SessionEnding += App_SessionEnding;

            // short update check probe with deferral if slow
            bool updateCheckCompleted = false;
            try
            {
                var updateUi = _serviceProvider.GetRequiredService<VersionCheckerUI>();
                _splash.UpdateStatus("Checking for updates");
                _ = Mark("Update check start");

                var updateTask = updateUi.EnforceRequiredAsync();
                var firstChance = await Task.WhenAny(updateTask, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(true);

                if (firstChance == updateTask)
                {
                    updateCheckCompleted = true;
                    await updateTask.ConfigureAwait(true);
                    if (this.Dispatcher.HasShutdownStarted || this.Dispatcher.HasShutdownFinished) return;
                    Mark("Update check done");
                }
                else
                {
                    Log.Warn("UpdateCheck", "Update check exceeded 3s; completing after first render.");
                }
            }
            catch (Exception ex)
            {
                Log.Warn("UpdateCheck", $"Required update check failed to run: {ex.Message}");
            }

            // main window wiring; pre-select a home view to avoid heavy constructors
            var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVM };
            MainWindow = mainWindow;
            try { mainVM.SelectedView = AppView.Home; } catch { }

            // first paint must not await work; deferred tasks are dispatched
            mainWindow.ContentRendered += (_, __) =>
            {
                Mark("First window rendered");

                // close splash once the first frame is visible
                try { _splash?.Close(); _splash = null; } catch { }

                // finish update check without blocking UI
                if (!updateCheckCompleted)
                {
                    this.Dispatcher.BeginInvoke(async () =>
                    {
                        try
                        {
                            var updateUi = _serviceProvider.GetRequiredService<VersionCheckerUI>();
                            await updateUi.EnforceRequiredAsync().ConfigureAwait(true);
                            if (this.Dispatcher.HasShutdownStarted || this.Dispatcher.HasShutdownFinished) return;
                            Log.Debug("Startup", "Update check done (deferred)");
                        }
                        catch (Exception ex)
                        {
                            Log.Warn("UpdateCheck", $"Deferred update check failed: {ex.Message}");
                        }
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }

                // bootstrap + warmups; non-blocking
                try { mainVM.BootstrapInitialView(); } catch (Exception ex) { Log.Warn("Bootstrap", ex.Message); }
                try { mainVM.StartWarmup(); } catch (Exception ex) { Log.Warn("Warmup", ex.Message); }

                // post-paint Links warmup; skip if already on Links
                this.Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        if (mainVM.SelectedView == AppView.Links) return;
                        await mainVM.LinksVM.EnsureLoadedAsync().ConfigureAwait(true);
                    }
                    catch (Exception ex) { Log.Warn("LinksWarmup", ex.Message); }
                }, System.Windows.Threading.DispatcherPriority.Background);
            };

            // show window and start background scheduler off the critical path
            mainWindow.Closed += (_, __) => TryPersistSettingsOnce();
            mainWindow.Show();
            Mark("Window shown");

            try
            {
                var scheduler = _serviceProvider.GetRequiredService<VersionUpdateScheduler>();
                scheduler.Start(TimeSpan.FromHours(6), runImmediately: false);
                Mark("Background update scheduler started");
            }
            catch (Exception ex)
            {
                Log.Warn("UpdateScheduler", $"Startup schedule failed: {ex.Message}");
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try { _serviceProvider.GetService<ISettingsService>()?.FlushPendingSaves(); } catch { }
            TryPersistSettingsOnce();

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

            services.AddSingleton<VersionCheckerUI>();
            services.AddSingleton<IVersionCheckHandler>(sp => sp.GetRequiredService<VersionCheckerUI>());
            services.AddSingleton<VersionUpdateScheduler>();

            _serviceProvider = services.BuildServiceProvider();
        }
    }
}
