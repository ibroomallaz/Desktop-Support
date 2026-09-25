using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Services.Status;
using DSAMVVM.MVVM.View.Dialogs;
using DSAMVVM.MVVM.View.Resources;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DSAMVVM.MVVM.ViewModel
{
    public partial class MainViewModel : ObservableObject
    {
        // Services
        public IDepartmentService DeptService { get; }
        private readonly ISearchService _searchService;
        private readonly IVersionCheckHandler _versionHandler;
        private readonly IDeepLinkRoutingService _linkRouter;
        private readonly IAuthenticationService _authService;
        private readonly IApplicationStateService _appStateService;
        public StatusBarViewModel StatusBar { get; }
        public bool IsUserSignedIn => _authService.IsAuthenticated;

        // VM factories
        private readonly Func<UserViewModel> _userVMFactory;
        private readonly Func<ComputerViewModel> _computerVMFactory;
        private readonly Func<GroupViewModel> _groupVMFactory;
        private readonly Func<LinksViewModel> _linksVMFactory;
        private readonly Func<AdminViewModel> _adminVMFactory;

        // ViewModels
        public HomeViewModel HomeVM { get; private set; } = null!;
        public UserViewModel UserVM { get; private set; } = null!;
        public ComputerViewModel ComputerVM { get; private set; } = null!;
        public GroupViewModel GroupVM { get; private set; } = null!;
        public EntraViewModel EntraVM { get; private set; } = null!;
        public LinksViewModel LinksVM { get; private set; } = null!;
        public AboutViewModel AboutVM { get; private set; } = null!;
        public SettingsViewModel SettingsVM { get; private set; } = null!;
        public AdminViewModel AdminVM { get; private set; } = null!;

        // Commands
        public RelayCommand HomeViewCommand { get; private set; } = null!;
        public RelayCommand UserCommand { get; private set; } = null!;
        public RelayCommand ComputerCommand { get; private set; } = null!;
        public RelayCommand GroupCommand { get; private set; } = null!;
        public RelayCommand EntraCommand { get; private set; } = null!;
        public RelayCommand LinksCommand { get; private set; } = null!;
        public RelayCommand AboutCommand { get; private set; } = null!;
        public RelayCommand ExecuteSearchCommand { get; private set; } = null!;
        public RelayCommand SettingsCommand { get; private set; } = null!;
        public RelayCommand OpenFeedbackCommand { get; private set; } = null!;
        public RelayCommand AdminCommand { get; private set; } = null!;

        public ICommand ShowWindowCommand { get; private set; } = null!;
        public ICommand CheckUpdateCommand { get; private set; } = null!;
        public ICommand ExitApplicationCommand { get; private set; } = null!;

        public ObservableCollection<NavItem> NavItems { get; } = [];
        private NavItem? _selectedNav;
        public NavItem? SelectedNav
        {
            get => _selectedNav;
            set
            {
                if (_selectedNav == value) return;
                _selectedNav = value;
                OnPropertyChanged();
                if (value is null) return;
                SelectedView = value.View;
                value.Command?.Execute(null);
            }
        }

        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            set { if (_currentView != value) { _currentView = value; OnPropertyChanged(); } }
        }

        private AppView _selectedView = AppView.Home;
        public AppView SelectedView
        {
            get => _selectedView;
            set
            {
                if (_selectedView == value) return;

                _selectedView = value;
                OnPropertyChanged();

                _appStateService.CurrentView = value.ToString();

                CurrentView = value switch
                {
                    AppView.Home => HomeVM,
                    AppView.User => UserVM,
                    AppView.Computer => ComputerVM,
                    AppView.Group => GroupVM,
                    AppView.Entra => EntraVM,
                    AppView.Links => LinksVM,
                    AppView.Settings => SettingsVM,
                    AppView.About => AboutVM,
                    AppView.Admin => AdminVM,
                    _ => HomeVM
                };
            }
        }

        private string? _searchQuery;
        public string? SearchQuery
        {
            get => _searchQuery;
            set { if (_searchQuery != value) { _searchQuery = value; OnPropertyChanged(); } }
        }

        private bool _showAdminView;
        public bool ShowAdminView
        {
            get => _showAdminView;
            set
            {
                if (_showAdminView != value)
                {
                    _showAdminView = value;
                    OnPropertyChanged();
                    SyncAdminNavItem();
                }
            }
        }

        private bool _hasUnlockedAdmin;
        public bool HasUnlockedAdmin
        {
            get => _hasUnlockedAdmin;
            set
            {
                if (_hasUnlockedAdmin != value)
                {
                    _hasUnlockedAdmin = value;
                    OnPropertyChanged();
                    SyncAdminNavItem();
                }
            }
        }

        private void SyncAdminNavItem()
        {
            var adminItem = NavItems.FirstOrDefault(n => n.View == AppView.Admin);

            if (_hasUnlockedAdmin && _showAdminView)
            {
                if (adminItem == null)
                {
                    var settingsIndex = NavItems.IndexOf(NavItems.First(n => n.View == AppView.Settings));
                    NavItems.Insert(settingsIndex, new NavItem
                    {
                        Title = "Admin",
                        Glyph = Glyphs.Admin,
                        View = AppView.Admin,
                        Command = AdminCommand
                    });
                }
            }
            else
            {
                if (adminItem != null)
                {
                    NavItems.Remove(adminItem);
                    if (SelectedView == AppView.Admin)
                    {
                        SelectedView = AppView.Home;
                    }
                }
            }
        }

        public MainViewModel(
            IDepartmentService deptService,
            ISearchService searchService,
            IVersionCheckHandler versionHandler,
            IDeepLinkRoutingService linkRouter,
            IAuthenticationService authService,
            IApplicationStateService appStateService,
            StatusBarViewModel statusBar,
            AboutViewModel aboutVM,
            Func<UserViewModel> userVMFactory,
            Func<ComputerViewModel> computerVMFactory,
            Func<GroupViewModel> groupVMFactory,
            Func<LinksViewModel> linksVMFactory,
            Func<AdminViewModel> adminVMFactory)
        {
            DeptService = deptService;
            _searchService = searchService;
            _versionHandler = versionHandler;
            _linkRouter = linkRouter;
            _authService = authService;
            _appStateService = appStateService;
            StatusBar = statusBar;

            _userVMFactory = userVMFactory;
            _computerVMFactory = computerVMFactory;
            _groupVMFactory = groupVMFactory;
            _linksVMFactory = linksVMFactory;
            _adminVMFactory = adminVMFactory;

            _linkRouter.NavigationRequested += OnNavigationRequested;
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;

            // Subscribe to SettingsService changes
            var settingsService = App.Services.GetRequiredService<ISettingsService>();
            settingsService.SettingsChanged += OnSettingsChanged;

            InitializeViewModels(aboutVM);
            InitializeCommands();
            InitializeNavigation();

            // Set the initial view state in the application state service to avoid "Unknown" without changing view
            _appStateService.CurrentView = _selectedView.ToString();
        }

        // Event handler to sync properties when SettingsVM hits "Apply"
        private void OnSettingsChanged(object? sender, AppSettings newSettings)
        {
            UiNotify.RunOnUiAsync(() =>
            {
                // Sync Admin state safely
                HasUnlockedAdmin = newSettings.Ui.HasUnlockedAdmin;
                ShowAdminView = newSettings.Ui.ShowAdminView;
            });
        }

        //Static lock shared across all ghost VM instances
        private static DateTime _lastNavTime = DateTime.MinValue;

        private void OnNavigationRequested(string targetView, string targetQuery)
        {
            // If another instance of MainViewModel just processed a link in the last 500ms, block this one.
            if ((DateTime.UtcNow - _lastNavTime).TotalMilliseconds < 500) return;
            _lastNavTime = DateTime.UtcNow;

            Application.Current.Dispatcher.Invoke(() =>
            {
                RestoreWindow();

                string normView = (targetView ?? string.Empty).Trim();
                string normQuery = (targetQuery ?? string.Empty).Trim();

                // If path delimiters or query string was included in targetView
                int qIdx = normView.IndexOf('?');
                if (qIdx >= 0)
                {
                    if (string.IsNullOrEmpty(normQuery)) normQuery = normView[(qIdx + 1)..];
                    normView = normView[..qIdx];
                }
                else
                {
                    int sIdx = normView.IndexOf('/');
                    if (sIdx >= 0)
                    {
                        if (string.IsNullOrEmpty(normQuery)) normQuery = normView[(sIdx + 1)..];
                        normView = normView[..sIdx];
                    }
                }

                AppView targetAppView = normView.ToLowerInvariant() switch
                {
                    "user" => AppView.User,
                    "computer" => AppView.Computer,
                    "group" => AppView.Group,
                    "entra" => AppView.Entra,
                    "links" => AppView.Links,
                    "about" => AppView.About,
                    "settings" => AppView.Settings,
                    "admin" => AppView.Admin,
                    _ => AppView.Home
                };

                if (targetAppView == AppView.Home && !normView.Equals("home", StringComparison.OrdinalIgnoreCase)) return;

                SelectedView = targetAppView;

                var navItem = NavItems.FirstOrDefault(n => n.View == targetAppView);
                if (navItem != null)
                {
                    _selectedNav = navItem;
                    OnPropertyChanged(nameof(SelectedNav));
                }

                if (targetAppView == AppView.Settings)
                {
                    var (category, anchor) = ResolveSettingsTarget(normQuery);
                    SettingsVM?.SelectCategoryAndAnchor(category, anchor);
                }
                else
                {
                    SearchQuery = normQuery;
                    TriggerSearch();
                }
            });
        }

        public void ProcessArgs(string[]? args)
        {
            if (args == null || args.Length == 0) return;

            if (args.Any(a => a.Equals("-updated", StringComparison.OrdinalIgnoreCase)))
            {
                SelectedView = AppView.About;
                UiNotify.Info("✔ Update installed successfully!", showStatusBar: true);
                Log.Info("Update", "Application relaunched with '-updated' flag.");
                if (args.Length == 1) return;
            }

            string? mode = GetArgValue(args, "--mode");
            if (!string.IsNullOrEmpty(mode))
            {
                switch (mode.ToLowerInvariant())
                {
                    case "user": SelectedView = AppView.User; break;
                    case "computer": SelectedView = AppView.Computer; break;
                    case "group": SelectedView = AppView.Group; break;
                    case "settings":
                        SelectedView = AppView.Settings;
                        string? section = GetArgValue(args, "--section") ?? GetArgValue(args, "--tab") ?? GetArgValue(args, "--card");
                        if (!string.IsNullOrEmpty(section))
                        {
                            var (category, anchor) = ResolveSettingsTarget(section);
                            SettingsVM?.SelectCategoryAndAnchor(category, anchor);
                        }
                        break;
                    case "links": SelectedView = AppView.Links; break;
                    case "entra": SelectedView = AppView.Entra; break;
                    case "about": SelectedView = AppView.About; break;
                    case "update":
                        Application.Current.Dispatcher.InvokeAsync(async () =>
                        {
                            await _versionHandler.CheckAsync(showUpToDatePopup: true);
                        });
                        break;
                }
            }

            string? query = GetArgValue(args, "--query");
            if (!string.IsNullOrEmpty(query))
            {
                SearchQuery = query;
                if (ExecuteSearchCommand.CanExecute(null))
                {
                    ExecuteSearchCommand.Execute(null);
                }
            }
        }

        private static (SettingsCategory Category, string? Anchor) ResolveSettingsTarget(string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return (SettingsCategory.Appearance, null);
            }

            var tokens = query.ToLowerInvariant().Split(['/', '?', '&', '=', '#'], StringSplitOptions.RemoveEmptyEntries);

            // 1. Check for specific card anchors first
            foreach (var token in tokens)
            {
                switch (token)
                {
                    case "shortcuts" or "quickshortcuts" or "customshortcuts":
                        return (SettingsCategory.DataAndLinks, "CardHomeShortcuts");

                    case "links-mode" or "linksmode" or "startmode" or "override":
                        return (SettingsCategory.DataAndLinks, "CardLinksStartMode");

                    case "data" or "department" or "dept" or "links-data" or "datalocation":
                        return (SettingsCategory.DataAndLinks, "CardDataManagement");

                    case "tray" or "systemtray" or "minimizetotray":
                        return (SettingsCategory.Appearance, "CardSystemTray");

                    case "history" or "searchhistory":
                        return (SettingsCategory.Appearance, "CardSearchHistory");

                    case "searchtext" or "typography" or "fontsize" or "font" or "scaling" or "fontscaling":
                        return (SettingsCategory.Appearance, "CardSearchTypography");

                    case "quicksearch" or "overlay" or "hotkey" or "doubletap":
                        return (SettingsCategory.QuickSearch, "CardQuickSearch");

                    case "updates" or "update" or "prerelease" or "channels":
                        return (SettingsCategory.SystemAndMaintenance, "CardUpdates");

                    case "logs" or "log" or "logging" or "logretention":
                        return (SettingsCategory.SystemAndMaintenance, "CardLogs");

                    case "reset" or "factory":
                        return (SettingsCategory.SystemAndMaintenance, "CardReset");
                }
            }

            // 2. If no specific card anchor matched, check broad category tokens
            foreach (var token in tokens)
            {
                switch (token)
                {
                    case "dataandlinks" or "data-links" or "datalinks" or "links":
                        return (SettingsCategory.DataAndLinks, null);

                    case "appearance" or "interface" or "ui":
                        return (SettingsCategory.Appearance, null);

                    case "quicksearch" or "search":
                        return (SettingsCategory.QuickSearch, null);

                    case "system" or "maintenance":
                        return (SettingsCategory.SystemAndMaintenance, null);
                }
            }

            return (SettingsCategory.Appearance, null);
        }

        private static string? GetArgValue(string[] args, string key)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        private void InitializeNavigation()
        {
            NavItems.Clear();
            NavItems.Add(new NavItem { Title = "Home", Glyph = Glyphs.Home, View = AppView.Home, Command = HomeViewCommand });
            NavItems.Add(new NavItem { Title = "User", Glyph = Glyphs.User, View = AppView.User, Command = UserCommand });
            NavItems.Add(new NavItem { Title = "Computer", Glyph = Glyphs.Computer, View = AppView.Computer, Command = ComputerCommand });
            NavItems.Add(new NavItem { Title = "Group", Glyph = Glyphs.Group, View = AppView.Group, Command = GroupCommand });
            NavItems.Add(new NavItem { Title = "Entra", Glyph = Glyphs.Entra, View = AppView.Entra, Command = EntraCommand });
            NavItems.Add(new NavItem { Title = "Links", Glyph = Glyphs.Links, View = AppView.Links, Command = LinksCommand });
            NavItems.Add(new NavItem { Title = "About", Glyph = Glyphs.About, View = AppView.About, Command = AboutCommand });
            NavItems.Add(new NavItem { Title = "Settings", Glyph = Glyphs.Settings, View = AppView.Settings, Command = SettingsCommand });
        }

        public void BootstrapInitialView()
        {
            if (CurrentView == null || SelectedView == AppView.Home)
            {
                CurrentView = HomeVM;
                var d = Application.Current?.Dispatcher;
                if (d is not null)
                    d.BeginInvoke(() => SelectedView = AppView.Home, DispatcherPriority.Loaded);
                else
                    SelectedView = AppView.Home;

                SelectedNav = NavItems.FirstOrDefault(n => n.View == AppView.Home);
            }
        }

        public void StartWarmup()
        {
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try { await DeptService.PreCacheDataAsync(); }
            catch (Exception ex) { UiNotify.Warn($"Failed to load department data: {ex.Message}", sticky: true); }
        }

        private void InitializeViewModels(AboutViewModel aboutVM)
        {
            HomeVM = App.Services.GetRequiredService<HomeViewModel>();
            AboutVM = aboutVM;
            SettingsVM = new SettingsViewModel();
            EntraVM = App.Services.GetRequiredService<EntraViewModel>();

            UserVM = _userVMFactory();
            ComputerVM = _computerVMFactory();
            GroupVM = _groupVMFactory();
            LinksVM = _linksVMFactory();
            AdminVM = _adminVMFactory();
        }

        private void InitializeCommands()
        {
            HomeViewCommand = new RelayCommand(_ => { SelectedView = AppView.Home; RestoreWindow(); });
            UserCommand = new RelayCommand(_ => { SelectedView = AppView.User; RestoreWindow(); });
            ComputerCommand = new RelayCommand(_ => { SelectedView = AppView.Computer; RestoreWindow(); });
            GroupCommand = new RelayCommand(_ => { SelectedView = AppView.Group; RestoreWindow(); });
            EntraCommand = new RelayCommand(_ => { SelectedView = AppView.Entra; RestoreWindow(); });
            LinksCommand = new RelayCommand(_ => { SelectedView = AppView.Links; RestoreWindow(); });
            AboutCommand = new RelayCommand(_ => { SelectedView = AppView.About; RestoreWindow(); });
            SettingsCommand = new RelayCommand(_ => { SelectedView = AppView.Settings; RestoreWindow(); });
            AdminCommand = new RelayCommand(_ => { SelectedView = AppView.Admin; RestoreWindow(); });

            ExecuteSearchCommand = new RelayCommand(_ => TriggerSearch());
            ShowWindowCommand = new RelayCommand(_ => RestoreWindow());
            ExitApplicationCommand = new RelayCommand(_ => Application.Current.Shutdown());
            CheckUpdateCommand = new RelayCommand(_ =>
            {
                Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await _versionHandler.CheckAsync(showUpToDatePopup: true);
                });
            });

            // Initialize the OpenFeedbackCommand
            OpenFeedbackCommand = new RelayCommand(ExecuteOpenFeedback);
        }

        private static void ExecuteOpenFeedback(object? parameter)
        {
            int targetIndex = 0; // Default to 'Report a Bug'

            // Try to parse the CommandParameter passed from XAML
            if (parameter is string paramString && int.TryParse(paramString, out int parsed))
            {
                targetIndex = parsed;
            }

            var window = new FeedbackWindow(targetIndex)
            {
                Owner = Application.Current.MainWindow
            };

            window.ShowDialog();
        }

        private static SearchTarget? ResolveTargetFromView(object view) => view switch
        {
            UserViewModel => SearchTarget.User,
            ComputerViewModel => SearchTarget.Computer,
            GroupViewModel => SearchTarget.Group,
            AdminViewModel => SearchTarget.Admin,
            _ => null
        };

        private async void TriggerSearch()
        {
            var query = (SearchQuery ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            string lowerQuery = query.ToLowerInvariant();

            //Admin intercept
            if (lowerQuery == "-admin")
            {
                HasUnlockedAdmin = true;
                ShowAdminView = true;

                var settings = App.Settings;
                settings.Ui.HasUnlockedAdmin = true;
                settings.Ui.ShowAdminView = true;
                App.Services.GetRequiredService<ISettingsService>().RequestSave(settings, Globals.g_SettingsPath);

                SearchQuery = string.Empty;
                SelectedView = AppView.Admin;

                UiNotify.Info("Admin mode unlocked! You can now toggle this in Settings.", showStatusBar: true);
                Log.Info("Admin", "Admin panel unlocked via command prompt.");
                return;
            }

            // Internal testing toggle intercept
            if (lowerQuery is "-test-" or "-production-")
            {
                bool useTest = lowerQuery == "-test-";
                var appSettings = App.Settings;
                appSettings.Updates.UseInternalTestingSources = useTest;

                var settingsService = App.Services.GetRequiredService<ISettingsService>();
                settingsService.RequestSave(appSettings, Globals.g_SettingsPath);

                string mode = useTest ? "TEST" : "PRODUCTION";
                UiNotify.Info($"Update source toggled to: {mode}", showStatusBar: true);

                SearchQuery = string.Empty;
                return;
            }
            // Internal testing error intercept
            if (lowerQuery == "-debug-error-")
            {
                var bus = App.Services.GetRequiredService<StatusBus>();

                var testError = new StatusItemBuilder()
                    .Key("DEBUG_STICKY_ERROR")
                    .Level(StatusLevel.Error)
                    .Sticky(true)
                    .Priority(1)
                    .Text("DEBUG: This is a persistent high-priority error. Test the ")
                    .Bold("✕")
                    .Text(" button!")
                    .Build();

                bus.Report(testError);

                SearchQuery = string.Empty;
                return;
            }

            if (CurrentView is not ISearchableViewModel searchable) return;

            var target = ResolveTargetFromView(CurrentView);
            if (target is null)
            {
                UiNotify.Warn("Search not supported for this view.");
                return;
            }

            var key = $"{target}_Search";
            UiNotify.Info($"Searching {target}...", showStatusBar: true, key: key);

            _searchService.AddToHistory(query, target.Value);

            try
            {
                var context = new SearchContextDTO(query);
                await searchable.OnSearchUpdated(context, _searchService, target.Value);
                UiNotify.Success("Search complete.", key: key);
                SearchQuery = string.Empty;
            }
            catch (Exception ex)
            {
                UiNotify.Error("Search failed", ex.Message, ex, alsoStatusBar: true, key: key);
            }
        }

        private void OnAuthenticationStateChanged(bool isAuthenticated)
        {
            // Forces the UI to re-evaluate any elements bound to IsUserSignedIn
            OnPropertyChanged(nameof(IsUserSignedIn));
        }

        [LibraryImport("user32.dll", EntryPoint = "SwitchToThisWindow")]
        private static partial void SwitchToThisWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fAltTab);


        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetForegroundWindow(IntPtr hWnd);

        private static void RestoreWindow()
        {
            var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
            if (mainWindow == null) return;

            if (mainWindow.Visibility != Visibility.Visible)
            {
                mainWindow.Show();
            }

            if (mainWindow.WindowState == WindowState.Minimized)
            {
                mainWindow.WindowState = WindowState.Normal;
            }

            mainWindow.Activate();
            mainWindow.Topmost = true;
            mainWindow.Topmost = false;
            mainWindow.Focus();

            var interopHelper = new WindowInteropHelper(mainWindow);
            var handle = interopHelper.Handle;

            if (handle != IntPtr.Zero)
            {
                SwitchToThisWindow(handle, true);
            }
        }
    }
}
