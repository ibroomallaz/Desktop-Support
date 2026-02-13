using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Shell;
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
using DSAMVVM.MVVM.View;
using DSAMVVM.MVVM.ViewModel;
using Microsoft.Extensions.DependencyInjection;

namespace DSAMVVM
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider = null!;
        private AppSettings? _settings;
        private int _persistOnceFlag;
        private SplashWindow? _splash;

        // Single Instance Identifiers
        private const string UniqueMutexName = "DSAMVVM_Mutex_Global_v1";
        private const string PipeName = "DSAMVVM_Pipe_Channel_v1";
        private Mutex? _mutex;

        public static IServiceProvider Services { get; private set; } = default!;
        public static AppSettings Settings => ((App)Current)._settings ?? new AppSettings();

        // Stores command-line arguments passed during application startup
        public static string[] StartupArgs { get; private set; } = [];

        public App()
        {
            this.DispatcherUnhandledException += (_, __) => TryPersistSettingsOnce();
            AppDomain.CurrentDomain.UnhandledException += (_, __) => TryPersistSettingsOnce();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 1. Single Instance Check
            bool isNewInstance;
            _mutex = new Mutex(true, UniqueMutexName, out isNewInstance);

            if (!isNewInstance)
            {
                // App is already running. Send args to the existing instance and shut down.
                await SendArgsToFirstInstanceAsync(e.Args);
                Shutdown();
                return;
            }

            // 2. Start listening for arguments from future instances (Jump List clicks)
            _ = Task.Run(() => ListenForArgumentsAsync());

            base.OnStartup(e);

            // Capture initial arguments
            if (e.Args != null && e.Args.Length > 0)
            {
                StartupArgs = e.Args;
            }
            ConfigureJumpList();

            // 3. Normal Startup Sequence
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

            var bus = _serviceProvider.GetRequiredService<StatusBus>();
            UiNotify.Initialize(bus.Report, bus.RemoveByKey, bus.Clear);
            Log.Initialize(_serviceProvider.GetRequiredService<IAppLogger>(), min: AppLogLevel.Debug);
            Mark("Logger ready");

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

            try
            {
                var updateUi = _serviceProvider.GetRequiredService<VersionCheckerUI>();
                _splash.UpdateStatus("Checking for updates");
                _ = Mark("Update check start");

                var updateTask = updateUi.EnforceRequiredAsync();
                var firstChance = await Task.WhenAny(updateTask, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(true);

                if (firstChance == updateTask)
                {
                    await updateTask.ConfigureAwait(true);
                    if (this.Dispatcher.HasShutdownStarted || this.Dispatcher.HasShutdownFinished) return;
                    Mark("Update check done");
                }
                else
                {
                    Log.Warn("UpdateCheck", "Update check exceeded 3s; finishing while splash is visible.");
                    try
                    {
                        await updateTask.ConfigureAwait(true);
                        if (this.Dispatcher.HasShutdownStarted || this.Dispatcher.HasShutdownFinished) return;
                        Mark("Update check done");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("UpdateCheck", $"Required update check (post-timeout) failed: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn("UpdateCheck", $"Required update check failed to run: {ex.Message}");
            }

            var mainVM = _serviceProvider.GetRequiredService<MainViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVM };
            MainWindow = mainWindow;

            // Handle initial view routing
            if (StartupArgs.Length > 0)
            {
                mainVM.ProcessArgs(StartupArgs);
            }
            else
            {
                // Default view if no arguments provided
                mainVM.SelectedView = AppView.Home;
            }

            mainWindow.ContentRendered += (_, __) =>
            {
                Mark("First window rendered");

                try { _splash?.Close(); _splash = null; } catch { }

                this.Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        var updateUi = _serviceProvider.GetRequiredService<VersionCheckerUI>();
                        await updateUi.CheckAsync().ConfigureAwait(true);
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("UpdateCheck", $"Optional update check failed: {ex.Message}");
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);

                try { mainVM.BootstrapInitialView(); } catch (Exception ex) { Log.Warn("Bootstrap", ex.Message); }
                try { mainVM.StartWarmup(); } catch (Exception ex) { Log.Warn("Warmup", ex.Message); }

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

        // --- SINGLE INSTANCE LOGIC: CLIENT ---
        private async Task SendArgsToFirstInstanceAsync(string[] args)
        {
            if (args.Length == 0) return;

            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                await client.ConnectAsync(1000);

                using var writer = new StreamWriter(client) { AutoFlush = true };
                await writer.WriteLineAsync(string.Join(" ", args));
            }
            catch (Exception)
            {
                // Silently fail if connection drops; app will just exit
            }
        }

        // --- SINGLE INSTANCE LOGIC: SERVER ---
        private async Task ListenForArgumentsAsync()
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync();

                    using var reader = new StreamReader(server);
                    var line = await reader.ReadLineAsync();

                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var args = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        Application.Current.Dispatcher.Invoke(() => HandleExternalArgs(args));
                    }
                }
                catch
                {
                    // Ignore pipe errors to keep server alive
                }
            }
        }

        // --- SINGLE INSTANCE LOGIC: HANDLER ---
        private void HandleExternalArgs(string[] args)
        {
            // Update static args for context
            StartupArgs = args;

            // Force window to front
            if (MainWindow is Window w)
            {
                if (w.WindowState == WindowState.Minimized)
                    w.WindowState = WindowState.Normal;

                w.Activate();
                w.Topmost = true;
                w.Topmost = false;
                w.Focus();
            }

            // Delegate routing to ViewModel
            var mainVM = Services.GetService<MainViewModel>();
            if (mainVM != null)
            {
                mainVM.ProcessArgs(args);
            }
        }

        private void ConfigureJumpList()
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;

                var jumpList = new JumpList();
                jumpList.ShowFrequentCategory = false;
                jumpList.ShowRecentCategory = false;

                // --- Search Items ---
                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = "Search Users",
                    Description = "Lookup user attributes, licenses, and groups",
                    Arguments = "--mode user",
                    CustomCategory = "Search",
                    IconResourcePath = exePath
                });

                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = "Search Computers",
                    Description = "Lookup device details",
                    Arguments = "--mode computer",
                    CustomCategory = "Search",
                    IconResourcePath = exePath
                });

                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = "Search Groups",
                    Description = "Lookup MIM groups and Dept Support",
                    Arguments = "--mode group",
                    CustomCategory = "Search",
                    IconResourcePath = exePath
                });
                // --- "Resources" Item ---
                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = "Quick Links",
                    Description = "Helpful Links and locations",
                    Arguments = "--mode links",
                    CustomCategory = "Resources",
                    IconResourcePath = exePath
                });

                // --- Settings Item ---
                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = "Settings",
                    Description = "Configure application preferences",
                    Arguments = "--mode settings",
                    CustomCategory = "Application",
                    IconResourcePath = exePath
                });

                JumpList.SetJumpList(Application.Current, jumpList);
            }
            catch (Exception ex)
            {
                Log.Warn("JumpList", $"Failed to configure Jump List: {ex.Message}");
            }
        }

        public static bool HasArg(string arg)
        {
            return StartupArgs.Any(a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));
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

            // Release Mutex on exit
            _mutex?.Dispose();

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