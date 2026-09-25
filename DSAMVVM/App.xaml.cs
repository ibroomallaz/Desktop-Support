using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Services;
using DSAMVVM.Core.Services.AD;
using DSAMVVM.Core.Services.Graph;
using DSAMVVM.Core.Services.Updates;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Services.Status;
using DSAMVVM.MVVM.Services.Updates;
using DSAMVVM.MVVM.View;
using DSAMVVM.MVVM.View.Overlays;
using DSAMVVM.MVVM.ViewModel;
using DSAMVVM.MVVM.ViewModel.Overlays;
using H.NotifyIcon;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Shell;

namespace DSAMVVM
{
    public partial class App
    {
        private IServiceProvider _serviceProvider = null!;
        private AppSettings? _settings;
        private int _persistOnceFlag;
        private SplashWindow? _splash;
        private TaskbarIcon? _appTrayIcon;
        private readonly CancellationTokenSource _appCts = new();

        // Single Instance Identifiers
        private const string UniqueMutexName = "DSAMVVM_Mutex_Global_v1";
        private const string PipeName = "DSAMVVM_Pipe_Channel_v1";
        private Mutex? _mutex;

        public static IServiceProvider Services { get; private set; } = null!;
        public static AppSettings Settings
        {
            get => ((App)Current)._settings ??= new AppSettings();
            set => ((App)Current)._settings = value;
        }

        // Stores command-line arguments passed during application startup
        public static string[] StartupArgs { get; private set; } = [];

        // Global flag to check if we just arrived from an update
        public static bool WasJustUpdated => HasArg("-updated");

        public App()
        {
            DispatcherUnhandledException += (_, _) => TryPersistSettingsOnce();
            AppDomain.CurrentDomain.UnhandledException += (_, _) => TryPersistSettingsOnce();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            // 1. Single Instance Check
            _mutex = new Mutex(true, UniqueMutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                // App is already running. Send args to the existing instance and shut down.
                await SendArgsToFirstInstanceAsync(e.Args);
                Shutdown();
                return;
            }

            // 2. Start listening for arguments from future instances (Jump List clicks)
            _ = Task.Run(ListenForArgumentsAsync);

            base.OnStartup(e);

            // Capture initial arguments
            if (e.Args.Length > 0)
            {
                StartupArgs = e.Args;
            }
            ConfigureJumpList();

            // 3. Normal Startup Sequence
            _splash = new SplashWindow();
            _splash.SourceInitialized += (_, _) =>
            {
                var area = SystemParameters.WorkArea;
                _splash.Left = area.Left + (area.Width - _splash.Width) / 2;
                _splash.Top = area.Top + (area.Height - _splash.Height) / 2;
            };
            _splash.Show();
            _splash.UpdateStatus("Starting…");

            Stopwatch sw = Stopwatch.StartNew();

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

            SessionEnding += App_SessionEnding;

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
                    if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
                    Mark("Update check done");
                }
                else
                {
                    Log.Warn("UpdateCheck", "Update check exceeded 3s; finishing while splash is visible.");
                    try
                    {
                        await updateTask.ConfigureAwait(true);
                        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished) return;
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

            InitializeTrayIcon(mainVM);

            // --- QUICKSEARCH INITIALIZATION ---
            var quickSearch = Services.GetRequiredService<IQuickSearchService>();

            quickSearch.QuickSearchTriggered += (_, capturedText) =>
            {
                Dispatcher.Invoke(() =>
                {
                    foreach (Window window in Windows)
                    {
                        if (window is QuickSearchOverlayView)
                        {
                            window.Close();
                        }
                    }

                    // Resolves the ViewModel from the DI container
                    var qsViewModel = _serviceProvider.GetRequiredService<QuickSearchOverlayViewModel>();

                    // Passes both required parameters to the constructor
                    var overlay = new QuickSearchOverlayView(capturedText, qsViewModel);
                    overlay.Show();
                    overlay.Activate();
                });
            };

            quickSearch.Start();

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

            mainWindow.ContentRendered += (_, _) =>
            {
                Mark("First window rendered");

                try { _splash?.Close(); _splash = null; }
                catch
                {
                    // ignored
                }

                Dispatcher.BeginInvoke(async () =>
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

                Dispatcher.BeginInvoke(async () =>
                {
                    try
                    {
                        if (mainVM.SelectedView == AppView.Links) return;
                        await mainVM.LinksVM.EnsureLoadedAsync().ConfigureAwait(true);
                    }
                    catch (Exception ex) { Log.Warn("LinksWarmup", ex.Message); }
                }, System.Windows.Threading.DispatcherPriority.Background);
            };

            mainWindow.Closed += (_, _) =>
            {
                TryPersistSettingsOnce();

                // Force-close any floating overlays so the app can terminate cleanly
                foreach (Window window in Windows)
                {
                    if (window is QuickSearchOverlayView)
                    {
                        window.Close();
                    }
                }
            };
            mainWindow.Show();
            Mark("Window shown");

            // Dispatches background Temp folder cleanup task
            try
            {
                var updaterService = _serviceProvider.GetRequiredService<IUpdaterService>();
                _ = updaterService.CleanupOldUpdatesAsync();
                Log.Debug("Startup", "Dispatched background Temp folder cleanup task");
            }
            catch (Exception ex)
            {
                Log.Warn("Startup", $"Failed to dispatch temp folder cleanup: {ex.Message}");
            }

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

            return;

            long Mark(string label)
            {
                var ms = sw.ElapsedMilliseconds;
                Log.Debug("Startup", $"{label} @ {ms} ms");
                _splash?.UpdateStatus(label + "…");
                return ms;
            }
        }

        // --- COMPONENT INITIALIZATION ---
        private void InitializeTrayIcon(MainViewModel mainVM)
        {
            // Respect the user's setting on startup
            if (!Settings.Ui.Tray.EnableTrayIcon) return;

            try
            {
                if (TryFindResource("GlobalAppTrayIcon") is not TaskbarIcon trayIcon) return;

                _appTrayIcon = trayIcon;
                _appTrayIcon.DataContext = mainVM;

                // EXPLICITLY pass the ViewModel to the ContextMenu so the bindings never fail
                if (_appTrayIcon.ContextMenu is { } menu)
                {
                    menu.DataContext = mainVM;
                }

                _appTrayIcon.ForceCreate();
            }
            catch (Exception ex)
            {
                Log.Warn("TrayIcon", $"Failed to initialize system tray icon: {ex.Message}");
            }
        }

        // Called dynamically by SettingsViewModel when the user clicks "Apply"
        public void ToggleTrayIcon(bool enable)
        {
            if (_appTrayIcon != null)
            {
                // If it already exists, just hide or show it instead of destroying the object entirely
                _appTrayIcon.Visibility = enable ? Visibility.Visible : Visibility.Collapsed;
                return;
            }

            // If it was never created (e.g., they started the app with it disabled), create it now
            if (!enable) return;

            var mainVM = Services.GetService<MainViewModel>();
            if (mainVM != null)
            {
                InitializeTrayIcon(mainVM);
            }
        }

        // --- SINGLE INSTANCE LOGIC: CLIENT ---
        private static async Task SendArgsToFirstInstanceAsync(string[] args)
        {
            try
            {
                await using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                await client.ConnectAsync(1000);

                await using var writer = new StreamWriter(client);
                writer.AutoFlush = true;

                // If there are no args, send a dummy "WAKE_UP" signal so the server still triggers
                var payload = args.Length > 0 ? string.Join(" ", args) : "WAKE_UP";
                await writer.WriteLineAsync(payload);
            }
            catch (Exception)
            {
                // Silently fail if connection drops; app will just exit
            }
        }

        // --- SINGLE INSTANCE LOGIC: SERVER ---
        private async Task ListenForArgumentsAsync()
        {
            while (!_appCts.IsCancellationRequested)
            {
                try
                {
                    await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                    await server.WaitForConnectionAsync(_appCts.Token).ConfigureAwait(false);

                    using var reader = new StreamReader(server);
                    var line = await reader.ReadLineAsync(_appCts.Token).ConfigureAwait(false);

                    // Parse the arguments, stripping out our dummy wake-up signal if it's there
                    var argsToPass = string.IsNullOrWhiteSpace(line) || line == "WAKE_UP"
                        ? []
                        : line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    // ALWAYS fire the handler so the window snaps to the front, even if args are empty
                    Dispatcher.Invoke(() => HandleExternalArgs(argsToPass));
                }
                catch (OperationCanceledException)
                {
                    break;
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
            if (MainWindow is { } w)
            {
                w.Show();

                if (w.WindowState == WindowState.Minimized)
                    w.WindowState = WindowState.Normal;

                w.Activate();
                w.Topmost = true;
                w.Topmost = false;
                w.Focus();
            }

            // Delegate routing to ViewModel
            var mainVM = Services.GetService<MainViewModel>();
            mainVM?.ProcessArgs(args);
        }

        private static void ConfigureJumpList()
        {
            try
            {
                var exePath = Process.GetCurrentProcess().MainModule?.FileName;

                var jumpList = new JumpList
                {
                    ShowFrequentCategory = false,
                    ShowRecentCategory = false
                };

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

                // -- "Application" --
                jumpList.JumpItems.Add(new JumpTask
                {
                    Title = "Check for Updates",
                    Description = "Force a check for application updates",
                    Arguments = "--mode update",
                    CustomCategory = "Application",
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

                JumpList.SetJumpList(Current, jumpList);
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
            _appCts.Cancel();
            _appCts.Dispose();

            try { _serviceProvider.GetService<ISettingsService>()?.FlushPendingSaves(); }
            catch
            {
                // ignored
            }

            TryPersistSettingsOnce();

            _serviceProvider.GetService<VersionUpdateScheduler>()?.Dispose();

            _serviceProvider.GetService<IQuickSearchService>()?.Stop();

            if (_serviceProvider.GetService<IAppLogger>() is FileLogger fl)
            {
                fl.Dispose();
            }

            _appTrayIcon?.Dispose();

            // Release Mutex on exit
            _mutex?.Dispose();

            base.OnExit(e);
        }

        private void App_SessionEnding(object? sender, SessionEndingCancelEventArgs e)
        {
            try { TryPersistSettingsOnce(); }
            catch
            {
                // ignored
            }
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

            services.AddSingleton<TeamsRoutingService>();

            services.AddSingleton<IAppLogger>(_ => new FileLogger(Globals.g_LogsDir));
            services.AddSingleton<StatusBus>();
            services.AddSingleton<StatusBarViewModel>();
            services.AddSingleton<IQuickSearchService, QuickSearchService>();

            services.AddSingleton<IHttpService, HttpService>();
            services.AddSingleton<IImageCacheService, ImageCacheService>();
            services.AddSingleton<ISettingsService, SettingsService>();

            services.AddSingleton<AppSettings>(_ => Settings);

            services.AddSingleton<IDepartmentService, DepartmentService>();
            services.AddSingleton<IADService, ADService>();
            services.AddSingleton<ILinksService, LinksService>();
            services.AddSingleton<IAdminService, AdminService>();
            services.AddSingleton<IFileDialogService, FileDialogService>();
            services.AddSingleton<IJsonExportService, JsonExportService>();
            services.AddSingleton<ISearchService, SearchService>();
            services.AddSingleton<IUpdaterService, UpdaterService>();
            services.AddSingleton<IDeepLinkRoutingService, DeepLinkRoutingService>();
            services.AddSingleton<IAuthenticationService, AuthenticationService>();
            services.AddSingleton<IApplicationStateService, ApplicationStateService>();
            services.AddSingleton<INetworkDetectionService, NetworkDetectionService>();
            services.AddSingleton<IADDetectionService, ADDetectionService>();

            services.AddSingleton<IOutputTextSettingsProvider>(sp =>
                new OutputTextSettingsProvider(
                    sp.GetRequiredService<ISettingsService>(),
                    () => Settings));

            services.AddSingleton<IFlowDocService, FlowDocService>();

            services.AddSingleton<MainViewModel>();
            services.AddTransient<UserViewModel>();
            services.AddTransient<GroupViewModel>();
            services.AddTransient<ComputerViewModel>();
            services.AddTransient<LinksViewModel>();
            services.AddSingleton<AboutViewModel>();
            services.AddSingleton<AdminViewModel>();

            services.AddSingleton<HomeViewModel>(sp =>
                new HomeViewModel(
                    openUser: q =>
                    {
                        var main = sp.GetRequiredService<MainViewModel>();
                        main.SelectedView = AppView.User;
                        if (string.IsNullOrWhiteSpace(q)) return;
                        main.SearchQuery = q;
                        main.ExecuteSearchCommand.Execute(null);
                    },
                    openComputer: q =>
                    {
                        var main = sp.GetRequiredService<MainViewModel>();
                        main.SelectedView = AppView.Computer;
                        if (string.IsNullOrWhiteSpace(q)) return;
                        main.SearchQuery = q;
                        main.ExecuteSearchCommand.Execute(null);
                    },
                    goGroups: () => sp.GetRequiredService<MainViewModel>().SelectedView = AppView.Group,
                    goEntra: () => sp.GetRequiredService<MainViewModel>().SelectedView = AppView.Entra,
                    goLinks: () => sp.GetRequiredService<MainViewModel>().SelectedView = AppView.Links,
                    goAbout: () => sp.GetRequiredService<MainViewModel>().SelectedView = AppView.About,
                    networkService: sp.GetRequiredService<INetworkDetectionService>(),
                    adDetectionService: sp.GetRequiredService<IADDetectionService>(),
                    authService: sp.GetRequiredService<IAuthenticationService>(),
                    searchService: sp.GetRequiredService<ISearchService>(),
                    linkRouter: sp.GetRequiredService<IDeepLinkRoutingService>(),
                    imageCacheService: sp.GetRequiredService<IImageCacheService>(),
                    settingsService: sp.GetRequiredService<ISettingsService>(),
                    linksService: sp.GetRequiredService<ILinksService>()
                )
            );

            services.AddTransient<QuickSearchOverlayViewModel>();
            services.AddTransient<EntraViewModel>();

            services.AddTransient<Func<UserViewModel>>(sp => sp.GetRequiredService<UserViewModel>);
            services.AddTransient<Func<GroupViewModel>>(sp => sp.GetRequiredService<GroupViewModel>);
            services.AddTransient<Func<ComputerViewModel>>(sp => sp.GetRequiredService<ComputerViewModel>);
            services.AddTransient<Func<LinksViewModel>>(sp => sp.GetRequiredService<LinksViewModel>);
            services.AddTransient<Func<AdminViewModel>>(sp => sp.GetRequiredService<AdminViewModel>);

            services.AddSingleton<VersionCheckerUI>();
            services.AddSingleton<IVersionCheckHandler>(sp => sp.GetRequiredService<VersionCheckerUI>());
            services.AddSingleton<VersionUpdateScheduler>();

            _serviceProvider = services.BuildServiceProvider();
        }
    }
}
