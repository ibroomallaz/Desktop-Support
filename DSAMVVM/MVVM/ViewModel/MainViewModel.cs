using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
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
        private readonly IADService _adService;
        private readonly ISearchService _searchService;
        private readonly IVersionCheckHandler _versionHandler;
        private readonly IDeepLinkRoutingService _linkRouter;
        private readonly IAuthenticationService _authService;
        private readonly IApplicationStateService _appStateService = null!;
        public StatusBarViewModel StatusBar { get; } = null!;
        public bool IsUserSignedIn => _authService.IsAuthenticated;

        // VM factories
        private readonly Func<UserViewModel> _userVMFactory;
        private readonly Func<ComputerViewModel> _computerVMFactory;
        private readonly Func<GroupViewModel> _groupVMFactory;
        private readonly Func<LinksViewModel> _linksVMFactory;

        // ViewModels
        public HomeViewModel HomeVM { get; private set; } = null!;
        public UserViewModel UserVM { get; private set; } = null!;
        public ComputerViewModel ComputerVM { get; private set; } = null!;
        public GroupViewModel GroupVM { get; private set; } = null!;
        public EntraViewModel EntraVM { get; private set; } = null!;
        public LinksViewModel LinksVM { get; private set; } = null!;
        public AboutViewModel AboutVM { get; private set; } = null!;
        public SettingsViewModel SettingsVM { get; private set; } = null!;

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
        public ObservableCollection<string> SearchHistory { get; } = [];

        public MainViewModel(
            IDepartmentService deptService,
            IADService adService,
            ISearchService searchService,
            IVersionCheckHandler versionHandler,
            StatusBarViewModel statusBar,
            IDeepLinkRoutingService linkRouter,
            IAuthenticationService authService,
            Func<UserViewModel> userVMFactory,
            Func<ComputerViewModel> computerVMFactory,
            Func<GroupViewModel> groupVMFactory,
            Func<LinksViewModel> linksVMFactory,
            IApplicationStateService appStateService,
            AboutViewModel aboutVM)
        {
            DeptService = deptService;
            _adService = adService;
            _searchService = searchService;
            StatusBar = statusBar;
            _linkRouter = linkRouter;
            _versionHandler = versionHandler;

            _authService = authService;
            _appStateService = appStateService;
            _userVMFactory = userVMFactory;
            _computerVMFactory = computerVMFactory;
            _groupVMFactory = groupVMFactory;
            _linksVMFactory = linksVMFactory;

            _searchService.HistoryChanged += OnHistoryChanged;
            SyncHistoryFromService();

            // --- SUBSCRIBE TO AUTH STATE ---
            _authService.AuthenticationStateChanged += OnAuthenticationStateChanged;

            // Prevent instance-level duplicate subscriptions
            _linkRouter.NavigationRequested -= OnNavigationRequested;
            _linkRouter.NavigationRequested += OnNavigationRequested;

            InitializeViewModels(aboutVM);
            InitializeCommands();
            InitializeNavigation();

            // Set the initial view state in the application state service to avoid "Unknown" without changing view
            _appStateService.CurrentView = _selectedView.ToString();
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

                AppView targetAppView = targetView.ToLowerInvariant() switch
                {
                    "user" => AppView.User,
                    "computer" => AppView.Computer,
                    "group" => AppView.Group,
                    _ => AppView.Home
                };

                if (targetAppView == AppView.Home) return;

                SelectedView = targetAppView;

                var navItem = NavItems.FirstOrDefault(n => n.View == targetAppView);
                if (navItem != null)
                {
                    _selectedNav = navItem;
                    OnPropertyChanged(nameof(SelectedNav));
                }

                SearchQuery = targetQuery;
                TriggerSearch();
            });
        }

        public void ProcessArgs(string[] args)
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
                    case "settings": SelectedView = AppView.Settings; break;
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
            OpenFeedbackCommand = new RelayCommand(param => ExecuteOpenFeedback(param));
        }

        private static void ExecuteOpenFeedback(object? parameter)
        {
            int targetIndex = 0; // Default to 'Report a Bug'

            // Try to parse the CommandParameter passed from XAML
            if (parameter is string paramString && int.TryParse(paramString, out int parsed))
            {
                targetIndex = parsed;
            }

            var mainWindow = Application.Current.MainWindow;
            var window = new FeedbackWindow(targetIndex);

            if (mainWindow != null && mainWindow.IsVisible)
            {
                window.Owner = mainWindow;
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            window.ShowDialog();
        }

        private static SearchTarget? ResolveTargetFromView(object view) => view switch
        {
            UserViewModel => SearchTarget.User,
            ComputerViewModel => SearchTarget.Computer,
            GroupViewModel => SearchTarget.Group,
            _ => null
        };

        private async void TriggerSearch()
        {
            var query = (SearchQuery ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            string lowerQuery = query.ToLowerInvariant();

            if (lowerQuery == "-test-" || lowerQuery == "-production-")
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

        private void OnHistoryChanged(object? s, EventArgs e)
        {
            var d = Application.Current?.Dispatcher;
            if (d?.CheckAccess() == true) SyncHistoryFromService();
            else d?.BeginInvoke(new Action(SyncHistoryFromService));
        }

        private void SyncHistoryFromService()
        {
            var snap = _searchService.GetHistorySnapshot();
            SearchHistory.Clear();
            foreach (var q in snap) SearchHistory.Add(q);
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
