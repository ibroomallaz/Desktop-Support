using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config.UI;
using DSAMVVM.MVVM.Model.Data;
using DSAMVVM.MVVM.View.Resources;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace DSAMVVM.MVVM.ViewModel
{
    public sealed class RecentActivityItem
    {
        public string Query { get; init; } = "";
        public string Title { get; init; } = "";
        public SearchTarget Target { get; init; } = SearchTarget.User;
        public string TypeTag { get; init; } = "";
        public string Icon { get; init; } = "";
        public ICommand? ActionCommand { get; init; }
    }

    public class HomeViewModel : ObservableObject
    {
        private readonly Action<string?>? _openUser;
        private readonly Action<string?>? _openComputer;
        private readonly Action? _goGroups;
        private readonly Action? _goEntra;
        private readonly Action? _goLinks;
        private readonly Action? _goAbout;

        private readonly INetworkDetectionService? _networkService;
        private readonly IADDetectionService? _adDetectionService;
        private readonly IAuthenticationService? _authService;
        private readonly ISearchService? _searchService;
        private readonly IDeepLinkRoutingService? _linkRouter;
        private readonly IImageCacheService? _imageCacheService;
        private readonly ISettingsService? _settingsService;
        private readonly ILinksService? _linksService;

        private NetworkStateInfo _networkState = NetworkStateInfo.Disconnected();
        private ADStateInfo _adState = ADStateInfo.Checking();

        public ICommand GoGroupsCommand { get; }
        public ICommand GoEntraCommand { get; }
        public ICommand GoLinksCommand { get; }
        public ICommand GoAboutCommand { get; }

        // --- Network Connection Status ---
        public string NetworkStatusText => _networkState.Label;
        public string NetworkStatusGlyph => _networkState.ConnectionType switch
        {
            NetworkConnectionType.CampusWired => Glyphs.NetworkWired,
            NetworkConnectionType.CampusWiFi => Glyphs.NetworkWiFi,
            NetworkConnectionType.Vpn => Glyphs.NetworkVpn,
            NetworkConnectionType.OffCampus => Glyphs.NetworkOffCampus,
            _ => Glyphs.NetworkDisconnected
        };
        public string NetworkStatusDotColor => _networkState.ConnectionType switch
        {
            NetworkConnectionType.CampusWired => "#3CD070",
            NetworkConnectionType.CampusWiFi => "#3CD070",
            NetworkConnectionType.Vpn => "#5BC3FF",
            NetworkConnectionType.OffCampus => "#FFB84D",
            _ => "#FF5252"
        };
        public string NetworkStatusToolTip => _networkState.ToolTipText;

        public ICommand RefreshNetworkStatusCommand { get; }

        // --- Entra / Microsoft 365 Status ---
        public bool IsEntraSignedIn => _authService?.IsAuthenticated ?? false;

        public string EntraAccountName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_authService?.CurrentAccountUpn))
                    return _authService.CurrentAccountUpn;

                return $"{Environment.UserName.ToLowerInvariant()}@arizona.edu";
            }
        }

        public string EntraStatusDotColor => IsEntraSignedIn ? "#3CD070" : "#FFA000";

        public string EntraStatusToolTip => IsEntraSignedIn
            ? $"Signed in to Microsoft Entra ID\nAccount: {EntraAccountName}\nClick to view Entra tools"
            : $"Entra ID Session: Standby / Not signed in\nAccount: {EntraAccountName}\nClick to sign in or view Entra tools";

        // --- Active Directory Domain Controller Status ---
        public bool IsDcConnected => _adState.IsReachable;

        public string DcStatusText => IsTestingDc ? "Probing domain controllers..." : _adState.StatusText;

        public string DcStatusToolTip => _adState.ToolTipText;

        public string DcStatusDotColor => IsTestingDc ? "#FFA000" : (_adState.IsReachable ? "#3CD070" : "#FF5252");

        private bool _isTestingDc;
        public bool IsTestingDc
        {
            get => _isTestingDc;
            set
            {
                _isTestingDc = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DcStatusText));
                OnPropertyChanged(nameof(DcStatusDotColor));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public ICommand TestDcCommand { get; }

        // --- Dynamic Content Collections ---
        public ObservableCollection<RecentActivityItem> RecentActivities { get; } = [];
        public ObservableCollection<HomeShortcutItem> Shortcuts { get; } = [];

        // --- Shortcuts Edit Mode & Customization ---
        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (Set(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(CanAddShortcut));
                    if (!value)
                    {
                        // Exited edit mode: flush any reordered or updated items to disk
                        _settingsService?.RequestSave(App.Settings, Globals.g_SettingsPath);
                    }
                }
            }
        }

        public bool CanAddShortcut => Shortcuts.Count < HomeShortcutsSettings.MaxShortcuts;

        public ICommand ToggleEditModeCommand { get; }
        public ICommand GoSettingsShortcutsCommand { get; }
        public ICommand RemoveShortcutCommand { get; }
        public ICommand AddShortcutCommand { get; }

        // --- Pet of the Day (ServiceMeow) ---
        private readonly List<ServiceMeowOwner> _mockOwners =
        [
            new()
            {
                NetId = "davet",
                Name = "Dave T.",
                Team = "Network Operations",
                Pets =
                [
                    new()
                    {
                        Name = "Nimbus",
                        Species = "Cat",
                        Breed = "British Shorthair",
                        Title = "Chief Packet Sniffer",
                        Blurb = "Discovered a loose patch cable by chewing on the boot."
                    },
                    new()
                    {
                        Name = "Barnaby",
                        Species = "Dog",
                        Breed = "Golden Retriever",
                        Title = "Lead Morale Specialist",
                        Blurb = "Has a 99.9% success rate resolving escalated user stress tickets."
                    }
                ]
            },
            new()
            {
                NetId = "sarahm",
                Name = "Sarah M.",
                Team = "Service Desk",
                Pets =
                [
                    new()
                    {
                        Name = "Pixel",
                        Species = "Cat",
                        Breed = "Calico",
                        Title = "Senior Cable Untangler",
                        Blurb = "Always sleeps directly on top of the warmest switch rack."
                    }
                ]
            }
        ];

        private readonly List<ServiceMeowPet> _mockPets;
        private int _currentPetIndex;
        private ServiceMeowPet _currentPet;
        public ServiceMeowPet CurrentPet
        {
            get => _currentPet;
            set
            {
                _currentPet = value;
                OnPropertyChanged();
                _ = LoadPetImageAsync(forceRefresh: false);
            }
        }

        private ImageSource? _currentPetImage;
        public ImageSource? CurrentPetImage
        {
            get => _currentPetImage;
            set
            {
                _currentPetImage = value;
                OnPropertyChanged();
            }
        }

        private bool _isImageRefreshing;
        public bool IsImageRefreshing
        {
            get => _isImageRefreshing;
            set
            {
                _isImageRefreshing = value;
                OnPropertyChanged();
            }
        }

        public ICommand NextPetCommand { get; }
        public ICommand RefreshPetImageCommand { get; }
        public ICommand SubmitPetCommand { get; }

        public HomeViewModel(
            Action<string?>? openUser = null,
            Action<string?>? openComputer = null,
            Action? goGroups = null,
            Action? goEntra = null,
            Action? goLinks = null,
            Action? goAbout = null,
            INetworkDetectionService? networkService = null,
            IADDetectionService? adDetectionService = null,
            IAuthenticationService? authService = null,
            ISearchService? searchService = null,
            IDeepLinkRoutingService? linkRouter = null,
            IImageCacheService? imageCacheService = null,
            ISettingsService? settingsService = null,
            ILinksService? linksService = null)
        {
            _openUser = openUser;
            _openComputer = openComputer;
            _goGroups = goGroups;
            _goEntra = goEntra;
            _goLinks = goLinks;
            _goAbout = goAbout;
            _networkService = networkService;
            _adDetectionService = adDetectionService;
            _authService = authService;
            _searchService = searchService;
            _linkRouter = linkRouter;
            _imageCacheService = imageCacheService;
            _settingsService = settingsService;
            _linksService = linksService;

            if (_networkService != null)
            {
                _networkState = _networkService.CurrentState;
                _networkService.NetworkStateChanged += (_, state) =>
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        _networkState = state;
                        OnPropertyChanged(nameof(NetworkStatusText));
                        OnPropertyChanged(nameof(NetworkStatusGlyph));
                        OnPropertyChanged(nameof(NetworkStatusDotColor));
                        OnPropertyChanged(nameof(NetworkStatusToolTip));
                    });
                };
            }

            if (_adDetectionService != null)
            {
                _adState = _adDetectionService.CurrentState;
                _adDetectionService.StateChanged += state =>
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        _adState = state;
                        OnPropertyChanged(nameof(IsDcConnected));
                        OnPropertyChanged(nameof(DcStatusText));
                        OnPropertyChanged(nameof(DcStatusToolTip));
                        OnPropertyChanged(nameof(DcStatusDotColor));
                    });
                };
            }

            if (_authService != null)
            {
                _authService.AuthenticationStateChanged += _ =>
                {
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(IsEntraSignedIn));
                        OnPropertyChanged(nameof(EntraAccountName));
                        OnPropertyChanged(nameof(EntraStatusDotColor));
                        OnPropertyChanged(nameof(EntraStatusToolTip));
                    });
                };

                // Asynchronously verify cached sign-in without blocking UI startup
                _ = Task.Run(async () =>
                {
                    await _authService.CheckCachedSignInAsync().ConfigureAwait(false);
                    Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        OnPropertyChanged(nameof(IsEntraSignedIn));
                        OnPropertyChanged(nameof(EntraAccountName));
                        OnPropertyChanged(nameof(EntraStatusDotColor));
                        OnPropertyChanged(nameof(EntraStatusToolTip));
                    });
                });
            }

            GoGroupsCommand = new RelayCommand(_ =>
            {
                if (_linkRouter != null) _linkRouter.RequestNavigation("group", string.Empty);
                else _goGroups?.Invoke();
            });
            GoEntraCommand = new RelayCommand(_ =>
            {
                if (_linkRouter != null) _linkRouter.RequestNavigation("entra", string.Empty);
                else _goEntra?.Invoke();
            });
            GoLinksCommand = new RelayCommand(_ =>
            {
                if (_linkRouter != null) _linkRouter.RequestNavigation("links", string.Empty);
                else _goLinks?.Invoke();
            });
            GoAboutCommand = new RelayCommand(_ =>
            {
                if (_linkRouter != null) _linkRouter.RequestNavigation("about", string.Empty);
                else _goAbout?.Invoke();
            });

            // Refresh network state
            RefreshNetworkStatusCommand = new RelayCommand(async _ =>
            {
                if (_networkService != null)
                {
                    var state = await _networkService.RefreshAsync();
                    UiNotify.Info($"Network: {state.Label}", showStatusBar: true);
                }
            });

            // Live AD DC test command
            TestDcCommand = new RelayCommand(async _ =>
            {
                if (IsTestingDc) return;
                IsTestingDc = true;
                try
                {
                    if (_adDetectionService != null)
                    {
                        var state = await _adDetectionService.ProbeDomainControllerAsync();
                        if (state.IsReachable)
                        {
                            UiNotify.Info($"AD: Connected to {state.DcHost} ({state.LatencyMs}ms)", showStatusBar: true);
                        }
                        else
                        {
                            UiNotify.Warn($"AD: {state.StatusText}");
                        }
                    }
                    else
                    {
                        await Task.Delay(500);
                    }
                }
                finally
                {
                    IsTestingDc = false;
                }
            }, _ => !IsTestingDc);

            // Toggle In-Place Edit Mode
            ToggleEditModeCommand = new RelayCommand(_ =>
            {
                IsEditMode = !IsEditMode;
            });

            // Navigate to Settings shortcuts page
            GoSettingsShortcutsCommand = new RelayCommand(_ =>
            {
                if (_linkRouter != null)
                {
                    _linkRouter.RequestNavigation("settings", "shortcuts");
                }
                else
                {
                    UiNotify.Info("Configure shortcuts in Settings -> Data & Links.", showStatusBar: true);
                }
            });

            // Remove Shortcut In-Place
            RemoveShortcutCommand = new RelayCommand(param =>
            {
                if (param is HomeShortcutItem item)
                {
                    Shortcuts.Remove(item);
                    App.Settings.Ui.Shortcuts.Items.RemoveAll(x => x.Id == item.Id);
                    App.Settings.Ui.Shortcuts.Normalize();
                    _settingsService?.RequestSave(App.Settings, Globals.g_SettingsPath);
                    OnPropertyChanged(nameof(CanAddShortcut));
                    UiNotify.Info($"Removed shortcut '{item.Title}'", showStatusBar: true);
                }
            });

            // Add Shortcut Slot Action (navigates to Settings)
            AddShortcutCommand = new RelayCommand(_ =>
            {
                if (_linkRouter != null)
                {
                    _linkRouter.RequestNavigation("settings", "shortcuts");
                }
                else
                {
                    UiNotify.Info("Add new shortcuts in Settings -> Data & Links.", showStatusBar: true);
                }
            });

            // Load initial shortcuts from AppSettings
            LoadShortcutsFromSettings();

            // Listen for external updates from SettingsView
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged += (_, _) =>
                {
                    UiNotify.RunOnUiAsync(LoadShortcutsFromSettings);
                };
            }

            // ServiceMeow setup
            foreach (var owner in _mockOwners)
            {
                foreach (var pet in owner.Pets)
                {
                    pet.Owner = owner;
                }
            }
            _mockPets = _mockOwners.SelectMany(o => o.Pets).ToList();
            _currentPet = _mockPets[0];

            NextPetCommand = new RelayCommand(_ =>
            {
                _currentPetIndex = (_currentPetIndex + 1) % _mockPets.Count;
                CurrentPet = _mockPets[_currentPetIndex];
            });

            RefreshPetImageCommand = new RelayCommand(async _ =>
            {
                if (IsImageRefreshing) return;
                IsImageRefreshing = true;
                try
                {
                    UiNotify.Info($"Refreshing photo for {CurrentPet.Name}...", showStatusBar: true);
                    await LoadPetImageAsync(forceRefresh: true);
                }
                finally
                {
                    IsImageRefreshing = false;
                }
            }, _ => !IsImageRefreshing);

            SubmitPetCommand = new RelayCommand(_ =>
            {
                UiNotify.Info("ServiceMeow: Pet submission portal will open in browser.", showStatusBar: true);
            });

            // Load initial mascot image from cache/disk
            _ = LoadPetImageAsync(forceRefresh: false);

            // Wire search service history for real recent searches
            if (_searchService != null)
            {
                _searchService.HistoryChanged += OnSearchHistoryChanged;
                SyncRecentActivities();
            }
        }

        private void LoadShortcutsFromSettings()
        {
            App.Settings.Ui.Shortcuts.Normalize();
            var items = App.Settings.Ui.Shortcuts.Items;

            Shortcuts.Clear();
            foreach (var item in items.OrderBy(x => x.Order))
            {
                var copy = item.Clone();
                copy.OpenCommand = new RelayCommand(_ => ExecuteShortcut(copy));
                Shortcuts.Add(copy);
            }
            OnPropertyChanged(nameof(CanAddShortcut));
        }

        private void ExecuteShortcut(HomeShortcutItem item)
        {
            if (IsEditMode || string.IsNullOrWhiteSpace(item.Target)) return;

            try
            {
                if (item.Target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    item.Target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    OpenUrl(item.Target);
                }
                else if (item.Target.StartsWith("app://", StringComparison.OrdinalIgnoreCase) ||
                         item.Target.StartsWith("dsa://", StringComparison.OrdinalIgnoreCase))
                {
                    _ = _linkRouter?.HandleLinkAsync(item.Target);
                }
                else
                {
                    // Fallback to https for standard domain inputs like "service-now.arizona.edu"
                    OpenUrl("https://" + item.Target);
                }
            }
            catch (Exception ex)
            {
                UiNotify.Warn($"Could not open shortcut: {ex.Message}");
            }
        }

        private async Task LoadPetImageAsync(bool forceRefresh)
        {
            if (_imageCacheService == null || string.IsNullOrWhiteSpace(CurrentPet?.ImageUrl))
            {
                CurrentPetImage = null;
                return;
            }

            try
            {
                var image = await _imageCacheService.GetImageAsync(CurrentPet.ImageUrl, forceRefresh: forceRefresh);
                CurrentPetImage = image;
            }
            catch (Exception ex)
            {
                Log.Warn("HomeVM", $"Failed to load image for mascot '{CurrentPet.Name}': {ex.Message}");
                CurrentPetImage = null;
            }
        }

        private void OnSearchHistoryChanged(object? sender, EventArgs e)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(SyncRecentActivities);
            }
            else
            {
                SyncRecentActivities();
            }
        }

        private void SyncRecentActivities()
        {
            if (_searchService == null) return;

            RecentActivities.Clear();
            var entries = _searchService.GetRecentSearchesSnapshot();

            foreach (var entry in entries)
            {
                var glyph = entry.Target switch
                {
                    SearchTarget.User => Glyphs.User,
                    SearchTarget.Computer => Glyphs.Computer,
                    SearchTarget.Group => Glyphs.Group,
                    SearchTarget.Admin => Glyphs.Admin,
                    _ => Glyphs.Search
                };

                var tag = entry.Target switch
                {
                    SearchTarget.User => "User",
                    SearchTarget.Computer => "Device",
                    SearchTarget.Group => "Group",
                    SearchTarget.Admin => "Admin",
                    _ => "Search"
                };

                RecentActivities.Add(new RecentActivityItem
                {
                    Query = entry.Query,
                    Title = entry.Query,
                    Target = entry.Target,
                    Icon = glyph,
                    TypeTag = tag,
                    ActionCommand = new RelayCommand(_ =>
                    {
                        string viewName = entry.Target switch
                        {
                            SearchTarget.User => "user",
                            SearchTarget.Computer => "computer",
                            SearchTarget.Group => "group",
                            SearchTarget.Admin => "admin",
                            _ => "home"
                        };
                        _linkRouter?.RequestNavigation(viewName, entry.Query);
                    })
                });
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // ignored
            }
        }
    }
}
