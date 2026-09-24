using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.View.Resources;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel
{
    public class RecentActivityMockItem
    {
        public string Icon { get; set; } = "";
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string TypeTag { get; set; } = "";
        public string IconColor { get; set; } = "#5BC3FF";
        public string PillBg { get; set; } = "#121E2C";
        public string PillBorder { get; set; } = "#243E5C";
        public ICommand? ActionCommand { get; set; }
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

    public class ServiceMeowMockPet
    {
        public string Name { get; set; } = "";
        public string Species { get; set; } = "Cat";
        public string Breed { get; set; } = "";
        public string Title { get; set; } = "";
        public string Owner { get; set; } = "";
        public string FunFact { get; set; } = "";
        public string GlowColor { get; set; } = "#4A2538"; // Radial aura color
        public string AccentTagColor { get; set; } = "#FFAEC0";
        public string AccentTagBg { get; set; } = "#381824";
    }

    public class HomeViewModel : ObservableObject
    {
        // Callbacks provided by MainViewModel
        private readonly Action<string?>? _openUser;
        private readonly Action<string?>? _openComputer;
        private readonly Action? _goGroups;
        private readonly Action? _goEntra;
        private readonly Action? _goLinks;
        private readonly Action? _goAbout;

        // Header
        public string Title { get; } = $"Welcome, {IdentityUtility.GetFirstName()}";
        public string Subtitle { get; } = $"Desktop Support Assistant · v{Globals.g_AppVersion}";
        public static string AppVersion => $"Version: {Globals.g_AppVersion}";

        // Quick-panel inputs (preserved for compatibility)
        private string _userQuery = "";
        public string UserQuery
        {
            get => _userQuery;
            set { _userQuery = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        private string _computerQuery = "";
        public string ComputerQuery
        {
            get => _computerQuery;
            set { _computerQuery = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
        }

        // Standard Navigation Commands
        public ICommand OpenUserCommand { get; }
        public ICommand OpenComputerCommand { get; }
        public ICommand GoGroupsCommand { get; }
        public ICommand GoEntraCommand { get; }
        public ICommand GoLinksCommand { get; }
        public ICommand GoAboutCommand { get; }

        // --- Live Network Detection State ---
        private readonly INetworkDetectionService? _networkService;
        private NetworkStateInfo _networkState = new();

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

        // --- Visual Mockup: Entra / Azure Status ---
        private bool _isEntraSignedIn = true;
        public bool IsEntraSignedIn
        {
            get => _isEntraSignedIn;
            set { _isEntraSignedIn = value; OnPropertyChanged(); }
        }

        private string _entraAccountName = $"{Environment.UserName.ToLowerInvariant()}@arizona.edu";
        public string EntraAccountName
        {
            get => _entraAccountName;
            set { _entraAccountName = value; OnPropertyChanged(); }
        }

        // --- Visual Mockup: Domain Controller Status ---
        private bool _isDcConnected = true;
        public bool IsDcConnected
        {
            get => _isDcConnected;
            set { _isDcConnected = value; OnPropertyChanged(); }
        }

        private string _dcStatusText = "bluecat.arizona.edu · 14ms";
        public string DcStatusText
        {
            get => _dcStatusText;
            set { _dcStatusText = value; OnPropertyChanged(); }
        }

        private bool _isTestingDc;
        public bool IsTestingDc
        {
            get => _isTestingDc;
            set { _isTestingDc = value; OnPropertyChanged(); }
        }

        public ICommand TestDcCommand { get; }

        // --- Dynamic Content Collections ---
        public ObservableCollection<RecentActivityMockItem> RecentActivities { get; } = [];
        public ObservableCollection<HomeShortcutMockItem> Shortcuts { get; } = [];

        public ICommand CustomizeShortcutsCommand { get; }

        // --- Pet of the Day (ServiceMeow) ---
        private readonly List<ServiceMeowMockPet> _mockPets =
        [
            new()
            {
                Name = "Nimbus",
                Species = "Cat",
                Breed = "British Shorthair",
                Title = "Chief Packet Sniffer",
                Owner = "Dave T. · Network Operations",
                FunFact = "Discovered a loose patch cable by chewing on the boot.",
                GlowColor = "#2C3E50",
                AccentTagColor = "#68D391",
                AccentTagBg = "#1C4532"
            },
            new()
            {
                Name = "Pixel",
                Species = "Cat",
                Breed = "Calico",
                Title = "Senior Cable Untangler",
                Owner = "Sarah M. · Service Desk",
                FunFact = "Sleeps exclusively on warm Cisco switch exhausts.",
                GlowColor = "#4A2538",
                AccentTagColor = "#FFAEC0",
                AccentTagBg = "#381824"
            },
            new()
            {
                Name = "Barnaby",
                Species = "Dog",
                Breed = "Golden Retriever",
                Title = "Director of Morale",
                Owner = "Elena R. · Classroom Tech",
                FunFact = "Knows how to high-five whenever a ticket is marked resolved.",
                GlowColor = "#4A3B18",
                AccentTagColor = "#FBD38D",
                AccentTagBg = "#3D2B0F"
            },
            new()
            {
                Name = "Mochi",
                Species = "Cat",
                Breed = "Scottish Fold",
                Title = "Lead Code Reviewer",
                Owner = "Marcus K. · Systems",
                FunFact = "Reviews PRs by walking across the mechanical keyboard at 2am.",
                GlowColor = "#1D3A44",
                AccentTagColor = "#76E4F7",
                AccentTagBg = "#132D3B"
            }
        ];

        private int _currentPetIndex;
        private ServiceMeowMockPet _currentPet = null!;
        public ServiceMeowMockPet CurrentPet
        {
            get => _currentPet;
            set { _currentPet = value; OnPropertyChanged(); }
        }

        public ICommand NextPetCommand { get; }
        public ICommand SubmitPetCommand { get; }

        public HomeViewModel(
            Action<string?>? openUser = null,
            Action<string?>? openComputer = null,
            Action? goGroups = null,
            Action? goEntra = null,
            Action? goLinks = null,
            Action? goAbout = null,
            INetworkDetectionService? networkService = null)
        {
            _openUser = openUser;
            _openComputer = openComputer;
            _goGroups = goGroups;
            _goEntra = goEntra;
            _goLinks = goLinks;
            _goAbout = goAbout;
            _networkService = networkService;

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

            OpenUserCommand = new RelayCommand(
                _ => _openUser?.Invoke(UserQuery),
                _ => !string.IsNullOrWhiteSpace(UserQuery));

            OpenComputerCommand = new RelayCommand(
                _ => _openComputer?.Invoke(ComputerQuery),
                _ => !string.IsNullOrWhiteSpace(ComputerQuery));

            GoGroupsCommand = new RelayCommand(_ => _goGroups?.Invoke());
            GoEntraCommand = new RelayCommand(_ => _goEntra?.Invoke());
            GoLinksCommand = new RelayCommand(_ => _goLinks?.Invoke());
            GoAboutCommand = new RelayCommand(_ => _goAbout?.Invoke());

            // Refresh network state
            RefreshNetworkStatusCommand = new RelayCommand(async _ =>
            {
                if (_networkService != null)
                {
                    var state = await _networkService.RefreshAsync();
                    UiNotify.Info($"Network: {state.Label}", showStatusBar: true);
                }
            });

            // Mock DC test command
            TestDcCommand = new RelayCommand(async _ =>
            {
                if (IsTestingDc) return;
                IsTestingDc = true;
                DcStatusText = "Pinging domain controllers...";
                await Task.Delay(600);
                IsTestingDc = false;
                IsDcConnected = true;
                DcStatusText = "bluecat.arizona.edu · 12ms";
                UiNotify.Info("✔ AD Domain Controllers responding normally.", showStatusBar: true);
            });

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

            SubmitPetCommand = new RelayCommand(_ =>
            {
                UiNotify.Info("ServiceMeow: Pet submission portal will open in browser.", showStatusBar: true);
            });

            // Seed Mock Recent Activities with high-contrast color cues
            RecentActivities.Add(new RecentActivityMockItem
            {
                Icon = Glyphs.User,
                Title = "jsmith",
                Subtitle = "Faculty · Engineering",
                TypeTag = "User",
                IconColor = "#5BC3FF",
                PillBg = "#132338",
                PillBorder = "#224268",
                ActionCommand = new RelayCommand(_ => _openUser?.Invoke("jsmith"))
            });
            RecentActivities.Add(new RecentActivityMockItem
            {
                Icon = Glyphs.Computer,
                Title = "ENG-LAB-042",
                Subtitle = "Windows 11 · Online",
                TypeTag = "Device",
                IconColor = "#3CD070",
                PillBg = "#112A20",
                PillBorder = "#1F523B",
                ActionCommand = new RelayCommand(_ => _openComputer?.Invoke("ENG-LAB-042"))
            });
            RecentActivities.Add(new RecentActivityMockItem
            {
                Icon = Glyphs.Group,
                Title = "UA-MIM-Wrkst-AllDiv",
                Subtitle = "MIM Security Group",
                TypeTag = "Group",
                IconColor = "#C084FC",
                PillBg = "#281738",
                PillBorder = "#4F2A6E",
                ActionCommand = new RelayCommand(_ => _goGroups?.Invoke())
            });
            RecentActivities.Add(new RecentActivityMockItem
            {
                Icon = Glyphs.Computer,
                Title = "BIO-DESK-11",
                Subtitle = "Windows 10 · Offline",
                TypeTag = "Device",
                IconColor = "#FFB84D",
                PillBg = "#2B2113",
                PillBorder = "#563F1D",
                ActionCommand = new RelayCommand(_ => _openComputer?.Invoke("BIO-DESK-11"))
            });

            // Seed Mock Shortcuts with Segoe MDL2 glyphs and jewel-tone color accents
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

        private static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                UiNotify.Warn($"Could not open link: {ex.Message}");
            }
        }

        public static Task OnSearchUpdated(string _) => Task.CompletedTask; // no-op
    }
}
