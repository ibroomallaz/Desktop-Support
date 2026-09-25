using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
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

    public class HomeShortcutMockItem
    {
        public string Icon { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string AccentBg { get; set; } = "#152538";
        public string AccentBorder { get; set; } = "#264366";
        public string AccentFg { get; set; } = "#5BC3FF";
        public ICommand? OpenCommand { get; set; }
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
        public ObservableCollection<HomeShortcutMockItem> Shortcuts { get; } = [];

        public ICommand CustomizeShortcutsCommand { get; }

        // --- Pet of the Day (ServiceMeow) ---
        private readonly List<ServiceMeowPet> _mockPets =
        [
            new()
            {
                Name = "Nimbus",
                Species = "Cat",
                Breed = "British Shorthair",
                Title = "Chief Packet Sniffer",
                Owner = "Dave T.",
                OwnerTeam = "Network Operations",
                Blurb = "Discovered a loose patch cable by chewing on the boot."
            },
            new()
            {
                Name = "Pixel",
                Species = "Cat",
                Breed = "Calico",
                Title = "Senior Cable Untangler",
                Owner = "Sarah M.",
                OwnerTeam = "Service Desk",
                Blurb = "Always sleeps directly on top of the warmest switch rack."
            },
            new()
            {
                Name = "Rusty",
                Species = "Dog",
                Breed = "Golden Retriever",
                Title = "Lead Morale Specialist",
                Owner = "Marcus K.",
                OwnerTeam = "Systems Team",
                Blurb = "Has a 99.9% success rate resolving escalated user stress tickets."
            }
        ];

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
            IImageCacheService? imageCacheService = null)
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

            // Mock customize shortcuts command
            CustomizeShortcutsCommand = new RelayCommand(_ =>
            {
                UiNotify.Info("Shortcut customizer preview: Pins will sync with your Links favorites.", showStatusBar: true);
            });

            // ServiceMeow setup
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

            // Seed Mock Shortcuts with Segoe glyphs and jewel-tone color accents
            Shortcuts.Add(new HomeShortcutMockItem
            {
                Icon = "\uE8EC", // Ticket / Work Order
                Title = "ServiceNow",
                Description = "Incident & request queue",
                Url = "https://service-now.arizona.edu",
                AccentBg = "#12263F",
                AccentBorder = "#234975",
                AccentFg = "#5BC3FF",
                OpenCommand = new RelayCommand(_ => OpenUrl("https://service-now.arizona.edu"))
            });
            Shortcuts.Add(new HomeShortcutMockItem
            {
                Icon = "\uE8D7", // Key / Credentials
                Title = "NetID Portal",
                Description = "Password reset & 2FA tools",
                Url = "https://netid.arizona.edu",
                AccentBg = "#2C2013",
                AccentBorder = "#5C4123",
                AccentFg = "#FFB84D",
                OpenCommand = new RelayCommand(_ => OpenUrl("https://netid.arizona.edu"))
            });
            Shortcuts.Add(new HomeShortcutMockItem
            {
                Icon = "\uE774", // Globe / Network
                Title = "IT Status",
                Description = "Campus outage dashboard",
                Url = "https://it.arizona.edu/status",
                AccentBg = "#102C1F",
                AccentBorder = "#1F593D",
                AccentFg = "#48D588",
                OpenCommand = new RelayCommand(_ => OpenUrl("https://it.arizona.edu/status"))
            });
            Shortcuts.Add(new HomeShortcutMockItem
            {
                Icon = "\uE82D", // Library / Book / Knowledge
                Title = "Knowledge Base",
                Description = "Desktop Support SOPs",
                Url = "https://it.arizona.edu",
                AccentBg = "#301522",
                AccentBorder = "#61243E",
                AccentFg = "#FFAEC0",
                OpenCommand = new RelayCommand(_ => _goLinks?.Invoke())
            });
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
