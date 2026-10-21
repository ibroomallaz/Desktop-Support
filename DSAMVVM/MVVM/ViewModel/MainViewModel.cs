using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces.Infrastructure;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Services.Status;
using DSAMVVM.MVVM.View.Dialogs;
using DSAMVVM.MVVM.View.Helpers;
using DSAMVVM.MVVM.View.Resources;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
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
        private readonly INetworkDetectionService? _networkService;
        public StatusBarViewModel StatusBar { get; }
        public bool IsUserSignedIn => _authService.IsAuthenticated;

        // Taskbar State
        private ImageSource? _taskbarOverlayIcon;
        public ImageSource? TaskbarOverlayIcon
        {
            get => _taskbarOverlayIcon;
            private set => Set(ref _taskbarOverlayIcon, value);
        }

        private string _taskbarDescription = Strings.App_Title;
        public string TaskbarDescription
        {
            get => _taskbarDescription;
            private set => Set(ref _taskbarDescription, value);
        }

        private string _windowTitle = Strings.App_Title;
        public string WindowTitle
        {
            get => _windowTitle;
            set => Set(ref _windowTitle, value);
        }

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
        public RelayCommand ClearSearchCommand { get; private set; } = null!;
        public RelayCommand SettingsCommand { get; private set; } = null!;
        public RelayCommand OpenFeedbackCommand { get; private set; } = null!;
        public RelayCommand AdminCommand { get; private set; } = null!;
        public RelayCommand ToggleDevModeCommand { get; private set; } = null!;
        public RelayCommand ToggleAdminModeCommand { get; private set; } = null!;
        public RelayCommand OpenLogsFolderCommand { get; private set; } = null!;
        public RelayCommand OpenAppDataFolderCommand { get; private set; } = null!;

        public RelayCommand ToggleSidebarCommand { get; private set; } = null!;

        public ICommand ShowWindowCommand { get; private set; } = null!;
        public ICommand CheckUpdateCommand { get; private set; } = null!;
        public ICommand ExitApplicationCommand { get; private set; } = null!;

        public ObservableCollection<NavItem> NavItems { get; } = [];
        public ObservableCollection<NavItem> PrimaryNavItems { get; } = [];
        public ObservableCollection<NavItem> FooterNavItems { get; } = [];

        // Sidebar Collapse State
        private bool _userSidebarPreference;
        private bool _isSidebarCollapsed;
        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (Set(ref _isSidebarCollapsed, value))
                {
                    if (SelectedView != AppView.Admin)
                    {
                        _userSidebarPreference = value;
                    }
                    OnPropertyChanged(nameof(IsCompactSidebar));
                    OnPropertyChanged(nameof(SidebarWidth));
                    OnPropertyChanged(nameof(SidebarToggleGlyph));
                    OnPropertyChanged(nameof(SidebarToggleText));
                    OnPropertyChanged(nameof(SidebarToggleToolTip));
                }
            }
        }

        public bool IsCompactSidebar => _isSidebarCollapsed || SelectedView == AppView.Admin;
        public double SidebarWidth => IsCompactSidebar ? 56 : 200;
        public string SidebarToggleGlyph => Glyphs.GlobalNav;
        public string SidebarToggleText => string.Empty;
        public string SidebarToggleToolTip => IsCompactSidebar ? "Expand navigation" : "Collapse navigation";

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

                var prevView = _selectedView;
                _selectedView = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsSearchEnabled));
                OnPropertyChanged(nameof(SearchWatermark));

                _selectedNav = NavItems.FirstOrDefault(n => n.View == value);
                OnPropertyChanged(nameof(SelectedNav));

                if (value == AppView.Admin)
                {
                    IsSidebarCollapsed = true;
                }
                else if (prevView == AppView.Admin)
                {
                    IsSidebarCollapsed = _userSidebarPreference;
                }
                else
                {
                    OnPropertyChanged(nameof(IsCompactSidebar));
                    OnPropertyChanged(nameof(SidebarWidth));
                    OnPropertyChanged(nameof(SidebarToggleGlyph));
                    OnPropertyChanged(nameof(SidebarToggleText));
                    OnPropertyChanged(nameof(SidebarToggleToolTip));
                }

                _appStateService.CurrentView = value.ToString();

                if (value == AppView.Settings)
                {
                    SettingsVM.RefreshFromSettings();
                }

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
            set
            {
                if (_searchQuery != value)
                {
                    _searchQuery = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HasSearchQuery));
                }
            }
        }

        public bool HasSearchQuery => !string.IsNullOrWhiteSpace(SearchQuery);
        public bool IsSearchEnabled => SelectedView is AppView.User or AppView.Computer or AppView.Group or AppView.Admin;

        public string SearchWatermark => SelectedView switch
        {
            AppView.User => "Search Users...",
            AppView.Computer => "Search Computers...",
            AppView.Group => "Search Groups...",
            AppView.Admin => "Search Admin...",
            _ => "Select view to search..."
        };

        public ReadOnlyObservableCollection<string> SearchHistory => _searchService.SearchHistory;

        // Developer Mode State (Session-only)
        private bool _isDevModeEnabled;
        public bool IsDevModeEnabled
        {
            get => _isDevModeEnabled;
            set
            {
                if (Set(ref _isDevModeEnabled, value))
                {
                    SettingsVM.IsDevModeEnabled = value;
                    OnPropertyChanged(nameof(DevModeStatusText));
                }
            }
        }
        public string DevModeStatusText => IsDevModeEnabled ? "Disable Developer Settings" : "Enable Developer Settings";

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
                    OnPropertyChanged(nameof(AdminModeStatusText));
                    SyncAdminNavItem();
                }
            }
        }
        public string AdminModeStatusText => HasUnlockedAdmin ? "Disable JSON Admin" : "Enable JSON Admin";

        private void SyncAdminNavItem()
        {
            var adminItemInNav = NavItems.FirstOrDefault(n => n.View == AppView.Admin);
            var adminItemInFooter = FooterNavItems.FirstOrDefault(n => n.View == AppView.Admin);

            if (_hasUnlockedAdmin && _showAdminView)
            {
                if (adminItemInNav == null || adminItemInFooter == null)
                {
                    var newAdminItem = new NavItem
                    {
                        Title = Strings.Nav_Admin,
                        Glyph = Glyphs.Admin,
                        View = AppView.Admin,
                        Command = AdminCommand
                    };

                    if (adminItemInFooter == null)
                    {
                        FooterNavItems.Insert(0, newAdminItem);
                    }

                    if (adminItemInNav == null)
                    {
                        var settingsNav = NavItems.FirstOrDefault(n => n.View == AppView.Settings);
                        if (settingsNav != null)
                        {
                            var settingsIndex = NavItems.IndexOf(settingsNav);
                            NavItems.Insert(settingsIndex, newAdminItem);
                        }
                    }
                }
            }
            else
            {
                if (adminItemInFooter != null)
                {
                    FooterNavItems.Remove(adminItemInFooter);
                }

                if (adminItemInNav != null)
                {
                    NavItems.Remove(adminItemInNav);
                }

                if (SelectedView == AppView.Admin)
                {
                    SelectedView = AppView.Home;
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
            Func<AdminViewModel> adminVMFactory,
            INetworkDetectionService? networkService = null)
        {
            DeptService = deptService;
            _searchService = searchService;
            _versionHandler = versionHandler;
            _linkRouter = linkRouter;
            _authService = authService;
            _appStateService = appStateService;
            _networkService = networkService;
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

            if (_networkService != null)
            {
                _networkService.NetworkStateChanged += (_, state) => UpdateTaskbarState(state);
                UpdateTaskbarState(_networkService.CurrentState);
            }

            InitializeViewModels(aboutVM);

            InitializeCommands();
            InitializeNavigation();

            HasUnlockedAdmin = App.Settings.Ui.HasUnlockedAdmin;
            ShowAdminView = App.Settings.Ui.ShowAdminView;
            IsDevModeEnabled = App.Settings.Ui.EnableDevSettings;

            if (App.Settings.Ui.CollapseSidebarByDefault)
            {
                IsSidebarCollapsed = true;
            }

            // Set the initial view state in the application state service to avoid "Unknown" without changing view
            _appStateService.CurrentView = _selectedView.ToString();
        }

        // Event handler to sync properties when SettingsVM hits "Apply"
        private void OnSettingsChanged(object? sender, AppSettings newSettings)
        {
            UiNotify.RunOnUiAsync(() =>
            {
                // Sync Admin and Dev state safely
                HasUnlockedAdmin = newSettings.Ui.HasUnlockedAdmin;
                ShowAdminView = newSettings.Ui.ShowAdminView;
                IsDevModeEnabled = newSettings.Ui.EnableDevSettings;
                SettingsVM.SyncExternalAdminAndDevState(newSettings.Ui.HasUnlockedAdmin, newSettings.Ui.ShowAdminView, newSettings.Ui.EnableDevSettings);

                if (SelectedView != AppView.Admin)
                {
                    IsSidebarCollapsed = newSettings.Ui.CollapseSidebarByDefault;
                }
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

                string normView = (targetView).Trim();
                string normQuery = (targetQuery).Trim();
                string? viewSubTarget = null;

                // If path delimiters or query string was included in targetView
                int qIdx = normView.IndexOf('?');
                if (qIdx >= 0)
                {
                    viewSubTarget = normView[(qIdx + 1)..];
                    normView = normView[..qIdx];
                }
                else
                {
                    int sIdx = normView.IndexOf('/');
                    if (sIdx >= 0)
                    {
                        viewSubTarget = normView[(sIdx + 1)..];
                        normView = normView[..sIdx];
                    }
                }

                if (string.IsNullOrEmpty(normQuery) && !string.IsNullOrEmpty(viewSubTarget))
                {
                    normQuery = viewSubTarget;
                }

                AppView targetAppView = normView.ToLowerInvariant() switch
                {
                    "user" or "userview" => AppView.User,
                    "computer" or "computerview" => AppView.Computer,
                    "group" or "groupview" => AppView.Group,
                    "entra" or "entraview" => AppView.Entra,
                    "links" or "linksview" => AppView.Links,
                    "about" or "aboutview" => AppView.About,
                    "settings" or "settingsview" => AppView.Settings,
                    "admin" or "adminview" => AppView.Admin,
                    "home" or "homeview" => AppView.Home,
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
                    SettingsVM.SelectCategoryAndAnchor(category, anchor);
                }
                else if (targetAppView == AppView.Group)
                {
                    var (groupMode, cleanQuery) = ResolveGroupTarget(viewSubTarget, normQuery);
                    if (groupMode.HasValue)
                    {
                        GroupVM.SetSearchMode(groupMode.Value);
                    }
                    SearchQuery = cleanQuery;
                    TriggerSearch();
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
                    case "group":
                        SelectedView = AppView.Group;
                        string? groupModeArg = GetArgValue(args, "--group-mode") ?? GetArgValue(args, "--groupmode");
                        if (!string.IsNullOrEmpty(groupModeArg))
                        {
                            var parsedMode = GroupViewModel.ParseSearchMode(groupModeArg);
                            if (parsedMode.HasValue) GroupVM.SetSearchMode(parsedMode.Value);
                        }
                        break;
                    case "settings":
                        SelectedView = AppView.Settings;
                        string? section = GetArgValue(args, "--section") ?? GetArgValue(args, "--tab") ?? GetArgValue(args, "--card");
                        if (!string.IsNullOrEmpty(section))
                        {
                            (SettingsCategory category, string? anchor) = ResolveSettingsTarget(section);
                            SettingsVM.SelectCategoryAndAnchor(category, anchor);
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

        private static (GroupViewModel.GroupSearchMode? Mode, string CleanQuery) ResolveGroupTarget(string? subTarget, string? query)
        {
            GroupViewModel.GroupSearchMode? resolvedMode = null;
            string cleanQuery = (query ?? string.Empty).Trim();

            // 1. Try resolving mode from subTarget (e.g. from targetView like "group/Department" or "group?mode=dept")
            if (!string.IsNullOrWhiteSpace(subTarget))
            {
                var trimmedSub = subTarget.Trim();

                if (trimmedSub.Contains('=') || trimmedSub.Contains('&'))
                {
                    var pairs = trimmedSub.Split('&', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            var key = kv[0].Trim().ToLowerInvariant();
                            var val = kv[1].Trim();
                            if (key is "mode" or "type" or "target")
                            {
                                resolvedMode = GroupViewModel.ParseSearchMode(val);
                            }
                            else if (key is "q" or "query" or "search" && string.IsNullOrEmpty(cleanQuery))
                            {
                                cleanQuery = val;
                            }
                        }
                    }
                }
                else if (trimmedSub.Contains('/'))
                {
                    var parts = trimmedSub.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        resolvedMode = GroupViewModel.ParseSearchMode(parts[0]);
                    }
                    if (parts.Length > 1 && string.IsNullOrEmpty(cleanQuery))
                    {
                        cleanQuery = parts[1].Trim();
                    }
                }
                else
                {
                    resolvedMode = GroupViewModel.ParseSearchMode(trimmedSub);
                    if (!resolvedMode.HasValue && string.IsNullOrEmpty(cleanQuery))
                    {
                        cleanQuery = trimmedSub;
                    }
                }
            }

            // 2. If mode not yet resolved, inspect cleanQuery (e.g. "mode=dept&q=1234", "dept/1234", "dept:1234")
            if (!resolvedMode.HasValue && !string.IsNullOrWhiteSpace(cleanQuery))
            {
                if (cleanQuery.Contains('=') || cleanQuery.Contains('&'))
                {
                    var pairs = cleanQuery.Split('&', StringSplitOptions.RemoveEmptyEntries);
                    string? extractedQ = null;
                    foreach (var pair in pairs)
                    {
                        var kv = pair.Split('=', 2);
                        if (kv.Length == 2)
                        {
                            var key = kv[0].Trim().ToLowerInvariant();
                            var val = kv[1].Trim();
                            if (key is "mode" or "type" or "target")
                            {
                                resolvedMode = GroupViewModel.ParseSearchMode(val);
                            }
                            else if (key is "q" or "query" or "search")
                            {
                                extractedQ = val;
                            }
                        }
                    }
                    if (extractedQ != null)
                    {
                        cleanQuery = extractedQ;
                    }
                }
                else if (cleanQuery.Contains('/'))
                {
                    var parts = cleanQuery.Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        var modeFromPart = GroupViewModel.ParseSearchMode(parts[0]);
                        if (modeFromPart.HasValue)
                        {
                            resolvedMode = modeFromPart;
                            cleanQuery = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                        }
                    }
                }
                else if (cleanQuery.Contains(':'))
                {
                    var parts = cleanQuery.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0)
                    {
                        var modeFromPart = GroupViewModel.ParseSearchMode(parts[0]);
                        if (modeFromPart.HasValue)
                        {
                            resolvedMode = modeFromPart;
                            cleanQuery = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                        }
                    }
                }
            }

            // 3. Auto-inference fallback based on query pattern
            if (!resolvedMode.HasValue && !string.IsNullOrWhiteSpace(cleanQuery))
            {
                if (cleanQuery.StartsWith("UA-", StringComparison.OrdinalIgnoreCase) ||
                    cleanQuery.StartsWith("UA_", StringComparison.OrdinalIgnoreCase))
                {
                    resolvedMode = GroupViewModel.GroupSearchMode.GroupMembers;
                }
            }

            return (resolvedMode, cleanQuery);
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
            PrimaryNavItems.Clear();
            FooterNavItems.Clear();

            var home = new NavItem { Title = Strings.Nav_Home, Glyph = Glyphs.Home, View = AppView.Home, Command = HomeViewCommand };
            var user = new NavItem { Title = Strings.Nav_User, Glyph = Glyphs.User, View = AppView.User, Command = UserCommand };
            var computer = new NavItem { Title = Strings.Nav_Computer, Glyph = Glyphs.Computer, View = AppView.Computer, Command = ComputerCommand };
            var group = new NavItem { Title = Strings.Nav_Groups, Glyph = Glyphs.Group, View = AppView.Group, Command = GroupCommand };
            var entra = new NavItem { Title = Strings.Nav_Entra, Glyph = Glyphs.Entra, View = AppView.Entra, Command = EntraCommand };
            var links = new NavItem { Title = Strings.Nav_Links, Glyph = Glyphs.Links, View = AppView.Links, Command = LinksCommand };
            var about = new NavItem { Title = Strings.Nav_About, Glyph = Glyphs.About, View = AppView.About, Command = AboutCommand };
            var settings = new NavItem { Title = Strings.Nav_Settings, Glyph = Glyphs.Settings, View = AppView.Settings, Command = SettingsCommand };

            PrimaryNavItems.Add(home);
            PrimaryNavItems.Add(user);
            PrimaryNavItems.Add(computer);
            PrimaryNavItems.Add(group);
            PrimaryNavItems.Add(entra);
            PrimaryNavItems.Add(links);
            PrimaryNavItems.Add(about);

            FooterNavItems.Add(settings);

            NavItems.Add(home);
            NavItems.Add(user);
            NavItems.Add(computer);
            NavItems.Add(group);
            NavItems.Add(entra);
            NavItems.Add(links);
            NavItems.Add(about);
            NavItems.Add(settings);

            SyncAdminNavItem();
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
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);

            ToggleDevModeCommand = new RelayCommand(_ =>
            {
                bool newState = !IsDevModeEnabled;
                IsDevModeEnabled = newState;

                var settings = App.Settings;
                settings.Ui.EnableDevSettings = newState;
                SettingsVM.SyncExternalAdminAndDevState(HasUnlockedAdmin, ShowAdminView, newState);
                App.Services.GetRequiredService<ISettingsService>().RequestSave(settings, Globals.g_SettingsPath);

                if (newState)
                {
                    UiNotify.Info("Developer settings enabled and saved.", showStatusBar: true);
                }
                else
                {
                    UiNotify.Info("Developer settings disabled.", showStatusBar: true);
                }
            });

            ToggleAdminModeCommand = new RelayCommand(_ =>
            {
                bool newState = !HasUnlockedAdmin;
                HasUnlockedAdmin = newState;
                ShowAdminView = newState;

                var settings = App.Settings;
                settings.Ui.HasUnlockedAdmin = newState;
                settings.Ui.ShowAdminView = newState;
                SettingsVM.SyncExternalAdminAndDevState(newState, newState, IsDevModeEnabled);
                App.Services.GetRequiredService<ISettingsService>().RequestSave(settings, Globals.g_SettingsPath);

                if (newState)
                {
                    SelectedView = AppView.Admin;
                    UiNotify.Info("JSON Admin enabled and saved to settings.", showStatusBar: true);
                }
                else
                {
                    UiNotify.Info("JSON Admin disabled.", showStatusBar: true);
                }
            });

            OpenLogsFolderCommand = new RelayCommand(_ => OpenFolder(Globals.g_LogsDir));
            OpenAppDataFolderCommand = new RelayCommand(_ => OpenFolder(Globals.g_AppDir));

            ExecuteSearchCommand = new RelayCommand(_ => TriggerSearch());
            ClearSearchCommand = new RelayCommand(_ => SearchQuery = string.Empty, _ => !string.IsNullOrEmpty(SearchQuery));
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

        private static void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { UiNotify.Warn($"Failed to open folder: {ex.Message}"); }
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

        private void UpdateTaskbarState(NetworkStateInfo state)
        {
            UiNotify.RunOnUiAsync(() =>
            {
                switch (state.ConnectionType)
                {
                    case NetworkConnectionType.OffCampus:
                        TaskbarOverlayIcon = TaskbarOverlayHelper.OffCampusBadge;
                        TaskbarDescription = $"{Strings.App_Title} — Network: Off Campus (VPN recommended for campus tools)";
                        WindowTitle = $"{Strings.App_Title} — Network: Off Campus (VPN recommended)";
                        break;

                    case NetworkConnectionType.Disconnected:
                        TaskbarOverlayIcon = TaskbarOverlayHelper.DisconnectedBadge;
                        TaskbarDescription = $"{Strings.App_Title} — Network Disconnected";
                        WindowTitle = $"{Strings.App_Title} — Network Disconnected";
                        break;

                    default:
                        TaskbarOverlayIcon = null;
                        TaskbarDescription = !string.IsNullOrWhiteSpace(state.Label)
                            ? $"{Strings.App_Title} — {state.Label}"
                            : Strings.App_Title;
                        WindowTitle = Strings.App_Title;
                        break;
                }
            });
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
