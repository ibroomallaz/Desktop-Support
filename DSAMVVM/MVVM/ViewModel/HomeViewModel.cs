using DSAMVVM.MVVM.View.Dialogs;
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

    // ReSharper disable once ClassNeverInstantiated.Global
    public class HomeViewModel : ObservableObject
    {
        private readonly Action<string?>? _openUser;
        private readonly Action<string?>? _openComputer;
        private readonly Action? _goGroups;

        private readonly INetworkDetectionService? _networkService;
        private readonly IADDetectionService? _adDetectionService;
        private readonly IAuthenticationService? _authService;
        private readonly ISearchService? _searchService;
        private readonly IDeepLinkRoutingService? _linkRouter;
        private readonly IImageCacheService? _imageCacheService;
        private readonly ISettingsService? _settingsService;
        private readonly IServiceMeowService? _serviceMeowService;

        private NetworkStateInfo _networkState = NetworkStateInfo.Disconnected();
        private ADStateInfo _adState = ADStateInfo.Checking();

        private string _title = "Home";
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _subtitle = "Quick access dashboard and environment status";
        public string Subtitle
        {
            get => _subtitle;
            set => Set(ref _subtitle, value);
        }

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

        public string EntraAccountName =>
            !string.IsNullOrWhiteSpace(_authService?.CurrentAccountUpn)
                ? _authService.CurrentAccountUpn
                : $"{Environment.UserName.ToLowerInvariant()}@arizona.edu";

        public string EntraStatusDotColor => IsEntraSignedIn ? "#3CD070" : "#FFA000";

        public string EntraStatusToolTip => IsEntraSignedIn
            ? $"Signed in to Microsoft Entra ID\nAccount: {EntraAccountName}\nClick to view Entra tools"
            : $"Entra ID Session: Standby / Not signed in\nAccount: {EntraAccountName}\nClick to sign in or view Entra tools";

        // --- Active Directory Domain Controller Status ---
        public bool IsDcConnected => _adState.IsReachable;

        public string DcStatusText => IsTestingDc ? "Probing domain controllers..." : _adState.StatusText;

        public string DcStatusToolTip => _adState.ToolTipText;

        public string DcStatusDotColor => IsTestingDc ? "#FFA000" : _adState.IsReachable ? "#3CD070" : "#FF5252";

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
                if (!Set(ref _isEditMode, value)) return;

                OnPropertyChanged(nameof(CanAddShortcut));
                if (!value)
                {
                    // Exited edit mode: flush any reordered or updated items to disk
                    _settingsService?.RequestSave(App.Settings, Globals.g_SettingsPath);
                }
            }
        }

        public bool CanAddShortcut => Shortcuts.Count < HomeShortcutsSettings.MaxShortcuts;

        public ICommand ToggleEditModeCommand { get; }
        public ICommand GoSettingsShortcutsCommand { get; }
        public ICommand RemoveShortcutCommand { get; }
        public ICommand AddShortcutCommand { get; }

        // --- Pet of the Day (ServiceMeow) ---
        private readonly List<ServiceMeowPet> _pets = [];
        public IReadOnlyList<ServiceMeowPet> Pets => _pets;

        private int _currentPetIndex;
        private ServiceMeowPet? _currentPet;
        public ServiceMeowPet? CurrentPet
        {
            get => _currentPet;
            set
            {
                if (!Set(ref _currentPet, value)) return;

                var validImages = value?.ValidImages ?? [];
                CurrentImageIndex = validImages.Count > 1 ? Random.Shared.Next(validImages.Count) : 0;
                OnPropertyChanged(nameof(HasMultiplePhotos));
                OnPropertyChanged(nameof(PhotoCountDisplay));
                _ = LoadPetImageAsync(value, CurrentImageIndex, forceRefresh: false);
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

        private int _currentImageIndex;
        public int CurrentImageIndex
        {
            get => _currentImageIndex;
            set
            {
                if (!Set(ref _currentImageIndex, value)) return;

                OnPropertyChanged(nameof(PhotoCountDisplay));
                OnPropertyChanged(nameof(HasMultiplePhotos));
            }
        }

        public bool HasMultiplePhotos => (CurrentPet?.ValidImages.Count ?? 0) > 1;

        public string PhotoCountDisplay
        {
            get
            {
                var count = CurrentPet?.ValidImages.Count ?? 0;
                return count <= 1 ? string.Empty : $"{CurrentImageIndex + 1}/{count}";
            }
        }

        // --- ServiceMeow Availability & Layout State ---
        private bool _isServiceMeowEnabled = true;
        public bool IsServiceMeowEnabled
        {
            get => _isServiceMeowEnabled;
            set
            {
                if (Set(ref _isServiceMeowEnabled, value))
                {
                    OnPropertyChanged(nameof(ShortcutColumns));
                }
            }
        }

        public int ShortcutColumns => IsServiceMeowEnabled ? 2 : 3;

        private CancellationTokenSource? _petImageCts;

        public ICommand NextPetCommand { get; }
        public ICommand NextPetPhotoCommand { get; }
        public ICommand PrevPetPhotoCommand { get; }
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
            IServiceMeowService? serviceMeowService = null)
        {
            _openUser = openUser;
            _openComputer = openComputer;
            _goGroups = goGroups;
            _networkService = networkService;
            _adDetectionService = adDetectionService;
            _authService = authService;
            _searchService = searchService;
            _linkRouter = linkRouter;
            _imageCacheService = imageCacheService;
            _settingsService = settingsService;
            _serviceMeowService = serviceMeowService;

            _isServiceMeowEnabled = App.Settings.Ui.ServiceMeow.Enabled;

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
                else goEntra?.Invoke();
            });
            GoLinksCommand = new RelayCommand(_ =>
            {
                if (_linkRouter != null) _linkRouter.RequestNavigation("links", string.Empty);
                else goLinks?.Invoke();
            });
            GoAboutCommand = new RelayCommand(_ =>
            {
                if (_linkRouter != null) _linkRouter.RequestNavigation("about", string.Empty);
                else goAbout?.Invoke();
            });

            // Refresh network state safely
            RefreshNetworkStatusCommand = new RelayCommand(_ => ExecuteRefreshNetworkStatus());

            // Live AD DC test command safely
            TestDcCommand = new RelayCommand(_ => ExecuteTestDc(), _ => !IsTestingDc);

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
                if (param is not HomeShortcutItem item) return;

                Shortcuts.Remove(item);
                App.Settings.Ui.Shortcuts.Items.RemoveAll(x => x.Id == item.Id);
                App.Settings.Ui.Shortcuts.Normalize();
                _settingsService?.RequestSave(App.Settings, Globals.g_SettingsPath);
                OnPropertyChanged(nameof(CanAddShortcut));
                UiNotify.Info($"Removed shortcut: {item.Title}", showStatusBar: true);
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
                    UiNotify.RunOnUiAsync(() =>
                    {
                        LoadShortcutsFromSettings();
                        UpdateServiceMeowState();
                    });
                };
            }

            // Initial ServiceMeow mascot placeholder while remote data loads
            _currentPet = new ServiceMeowPet
            {
                Name = "ServiceMeow",
                Title = "Support Mascot",
                Species = "Cat",
                Blurb = "Loading mascot of the day..."
            };

            NextPetCommand = new RelayCommand(_ =>
            {
                if (_pets.Count == 0) return;
                _currentPetIndex = (_currentPetIndex + 1) % _pets.Count;
                CurrentPet = _pets[_currentPetIndex];
            }, _ => _pets.Count > 1);

            NextPetPhotoCommand = new RelayCommand(_ => NextPetPhoto(), _ => (CurrentPet?.ValidImages.Count ?? 0) > 1);

            PrevPetPhotoCommand = new RelayCommand(_ => PrevPetPhoto(), _ => (CurrentPet?.ValidImages.Count ?? 0) > 1);

            RefreshPetImageCommand = new RelayCommand(_ => ExecuteRefreshPetImage(), _ => !IsImageRefreshing && CurrentPet != null);

            SubmitPetCommand = new RelayCommand(_ =>
            {
                var window = new FeedbackWindow(defaultIndex: 4)
                {
                    Owner = Application.Current?.MainWindow
                };
                window.ShowDialog();
            });

            // Wire search service history for real recent searches
            if (_searchService != null)
            {
                _searchService.HistoryChanged += OnSearchHistoryChanged;
                SyncRecentActivities();
            }

            // Asynchronously load real ServiceMeow data from service if enabled
            if (IsServiceMeowEnabled)
            {
                _ = InitializeServiceMeowAsync();
            }
        }

        private void UpdateServiceMeowState()
        {
            bool wasEnabled = IsServiceMeowEnabled;
            IsServiceMeowEnabled = App.Settings.Ui.ServiceMeow.Enabled;
            if (!wasEnabled && IsServiceMeowEnabled && _pets.Count == 0)
            {
                _ = InitializeServiceMeowAsync();
            }
        }

        private void ExecuteRefreshNetworkStatus()
        {
            _ = ExecuteRefreshNetworkStatusAsync();
        }

        private async Task ExecuteRefreshNetworkStatusAsync()
        {
            try
            {
                if (_networkService != null)
                {
                    var state = await _networkService.RefreshAsync();
                    UiNotify.Info($"Network: {state.Label}", showStatusBar: true);
                }
            }
            catch (Exception ex)
            {
                Log.Warn("HomeVM", $"Failed to refresh network status: {ex.Message}");
            }
        }

        private void ExecuteTestDc()
        {
            _ = ExecuteTestDcAsync();
        }

        private async Task ExecuteTestDcAsync()
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
            catch (Exception ex)
            {
                Log.Warn("HomeVM", $"Domain controller probe failed: {ex.Message}");
            }
            finally
            {
                IsTestingDc = false;
            }
        }

        private void NextPetPhoto()
        {
            var images = CurrentPet?.ValidImages;
            if (images == null || images.Count <= 1) return;
            CurrentImageIndex = (CurrentImageIndex + 1) % images.Count;
            _ = LoadPetImageAsync(CurrentPet, CurrentImageIndex, forceRefresh: false);
        }

        private void PrevPetPhoto()
        {
            var images = CurrentPet?.ValidImages;
            if (images == null || images.Count <= 1) return;
            CurrentImageIndex = (CurrentImageIndex - 1 + images.Count) % images.Count;
            _ = LoadPetImageAsync(CurrentPet, CurrentImageIndex, forceRefresh: false);
        }

        private void ExecuteRefreshPetImage()
        {
            _ = ExecuteRefreshPetImageAsync();
        }

        private async Task ExecuteRefreshPetImageAsync()
        {
            if (IsImageRefreshing || CurrentPet == null) return;
            IsImageRefreshing = true;
            try
            {
                UiNotify.Info($"Refreshing photo for {CurrentPet.Name}...", showStatusBar: true);
                await LoadPetImageAsync(CurrentPet, CurrentImageIndex, forceRefresh: true);
            }
            catch (Exception ex)
            {
                Log.Warn("HomeVM", $"Pet image refresh failed: {ex.Message}");
            }
            finally
            {
                IsImageRefreshing = false;
            }
        }

        private async Task InitializeServiceMeowAsync()
        {
            if (_serviceMeowService == null || !IsServiceMeowEnabled) return;

            try
            {
                var data = await _serviceMeowService.LoadServiceMeowDataAsync().ConfigureAwait(false);
                var allPets = data?.AllPets ?? [];

                var initialPet = _serviceMeowService.GetRandomPet();

                if (Application.Current?.Dispatcher is { } dispatcher)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        _pets.Clear();
                        if (allPets.Count > 0)
                        {
                            _pets.AddRange(allPets);
                            _currentPetIndex = initialPet != null ? Math.Max(0, _pets.IndexOf(initialPet)) : 0;
                            CurrentPet = initialPet ?? _pets[_currentPetIndex];
                        }
                        else
                        {
                            CurrentPet = new ServiceMeowPet
                            {
                                Name = "ServiceMeow",
                                Title = "Support Mascot",
                                Species = "Cat",
                                Blurb = "Welcome to Desktop Support! Mascot data is standing by."
                            };
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Warn("HomeVM", $"Failed to initialize ServiceMeow: {ex.Message}");
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
                if (item.Target.StartsWith(Uri.UriSchemeHttps + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase) ||
                    item.Target.StartsWith(Uri.UriSchemeHttp + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase))
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
                    OpenUrl(Uri.UriSchemeHttps + Uri.SchemeDelimiter + item.Target);
                }
            }
            catch (Exception ex)
            {
                UiNotify.Warn($"Could not open shortcut: {ex.Message}");
            }
        }

        private async Task LoadPetImageAsync(ServiceMeowPet? pet, int photoIndex, bool forceRefresh)
        {
            if (!IsServiceMeowEnabled)
            {
                CurrentPetImage = null;
                return;
            }

            if (_petImageCts != null)
            {
                await _petImageCts.CancelAsync().ConfigureAwait(false);
                _petImageCts.Dispose();
            }
            var cts = new CancellationTokenSource();
            _petImageCts = cts;

            var images = pet?.ValidImages ?? [];
            if (pet == null || images.Count == 0)
            {
                CurrentPetImage = null;
                return;
            }

            var safeIndex = Math.Clamp(photoIndex, 0, images.Count - 1);
            var targetUrl = images[safeIndex];
            var targetPetId = pet.Id;

            try
            {
                ImageSource? image = null;
                if (_serviceMeowService != null)
                {
                    image = await _serviceMeowService.GetImageAsync(targetUrl, forceRefresh: forceRefresh, ct: cts.Token).ConfigureAwait(false);
                }
                else if (_imageCacheService != null)
                {
                    image = await _imageCacheService.GetImageAsync(targetUrl, forceRefresh: forceRefresh, ct: cts.Token).ConfigureAwait(false);
                }

                if (cts.Token.IsCancellationRequested) return;

                if (Application.Current?.Dispatcher is { } dispatcher)
                {
                    await dispatcher.InvokeAsync(() =>
                    {
                        if (!cts.Token.IsCancellationRequested && _currentPet?.Id == targetPetId)
                        {
                            CurrentPetImage = image;
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Canceled due to mascot rotation or forced refresh
            }
            catch (Exception ex)
            {
                if (!cts.Token.IsCancellationRequested)
                {
                    Log.Warn("HomeVM", $"Failed to load image for mascot '{pet.Name}': {ex.Message}");
                    if (Application.Current?.Dispatcher is { } dispatcher)
                    {
                        await dispatcher.InvokeAsync(() =>
                        {
                            if (_currentPet?.Id == targetPetId)
                            {
                                CurrentPetImage = null;
                            }
                        });
                    }
                }
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

                RecentActivities.Add(new RecentActivityItem
                {
                    Query = entry.Query,
                    Title = entry.Query,
                    Target = entry.Target,
                    TypeTag = entry.Target.ToString().ToUpperInvariant(),
                    Icon = glyph,
                    ActionCommand = new RelayCommand(_ =>
                    {
                        Action? navAction = entry.Target switch
                        {
                            SearchTarget.User => () => _openUser?.Invoke(entry.Query),
                            SearchTarget.Computer => () => _openComputer?.Invoke(entry.Query),
                            SearchTarget.Group => () => _goGroups?.Invoke(),
                            _ => null
                        };
                        navAction?.Invoke();
                    })
                });
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error("HomeVM", $"Failed to launch URL '{url}': {ex.Message}");
                UiNotify.Warn($"Could not open link: {ex.Message}");
            }
        }
    }
}
