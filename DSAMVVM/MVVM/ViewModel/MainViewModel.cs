using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;
using DSAMVVM.MVVM.View.Resources;

namespace DSAMVVM.MVVM.ViewModel
{
    public class MainViewModel : ObeservableObject
    {
        // Services
        public IDepartmentService DeptService { get; } = null!;
        private readonly IADService _adService = null!;
        private readonly ISearchService _searchService = null!;
        public StatusBarViewModel StatusBar { get; } = null!;

        // ViewModels
        public HomeViewModel HomeVM { get; private set; } = null!;
        public ComputerViewModel ComputerVM { get; private set; } = null!;
        public UserViewModel UserVM { get; private set; } = null!;
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

        // Navigation collection and selection
        public ObservableCollection<NavItem> NavItems { get; } = new();
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
                SelectedView = value.View; // keep enum in sync
                value.Command?.Execute(null);
            }
        }

        // Current content view and selected enum
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

        // Search binding and history
        private string? _searchQuery;
        public string? SearchQuery
        {
            get => _searchQuery;
            set { if (_searchQuery != value) { _searchQuery = value; OnPropertyChanged(); } }
        }
        public ObservableCollection<string> SearchHistory { get; } = [];

        // ctor
        public MainViewModel(
            IDepartmentService deptService,
            IADService adService,
            ISearchService searchService,
            StatusBarViewModel statusBar,
            Func<UserViewModel> userVMFactory,
            Func<ComputerViewModel> computerVMFactory,
            Func<GroupViewModel> groupVMFactory,
            Func<LinksViewModel> linksVMFactory,
            AboutViewModel aboutVM)
        {
            DeptService = deptService;
            _adService = adService;
            _searchService = searchService;
            StatusBar = statusBar;

            _searchService.HistoryChanged += OnHistoryChanged;
            SyncHistoryFromService();

            InitializeViewModels(userVMFactory, computerVMFactory, groupVMFactory, linksVMFactory, aboutVM);
            InitializeCommands();
            InitializeNavigation();
        }

        // Build nav items for sidebar
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

        // initial view bootstrap
        public void BootstrapInitialView()
        {
            CurrentView = HomeVM;
            var d = Application.Current?.Dispatcher;
            if (d is not null)
                d.BeginInvoke(() => SelectedView = AppView.Home, DispatcherPriority.Loaded);
            else
                SelectedView = AppView.Home;
            SelectedNav = NavItems.FirstOrDefault(n => n.View == AppView.Home);
        }

        // warmup tasks
        public void StartWarmup()
        {
            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try { await DeptService.PreCacheDataAsync(); }
            catch (Exception ex) { UiNotify.Warn($"Failed to load department data: {ex.Message}", sticky: true); }
        }

        // vm factories
        private void InitializeViewModels(
            Func<UserViewModel> userVMFactory,
            Func<ComputerViewModel> computerVMFactory,
            Func<GroupViewModel> groupVMFactory,
            Func<LinksViewModel> linksVMFactory,
            AboutViewModel aboutVM)
        {
            HomeVM = App.Services.GetRequiredService<HomeViewModel>();
            UserVM = userVMFactory();
            ComputerVM = computerVMFactory();
            GroupVM = groupVMFactory();
            EntraVM = new EntraViewModel();
            LinksVM = linksVMFactory();
            AboutVM = aboutVM;
            SettingsVM = new SettingsViewModel();
        }

        // command setup
        private void InitializeCommands()
        {
            HomeViewCommand = new RelayCommand(_ => SelectedView = AppView.Home);
            UserCommand = new RelayCommand(_ => SelectedView = AppView.User);
            ComputerCommand = new RelayCommand(_ => SelectedView = AppView.Computer);
            GroupCommand = new RelayCommand(_ => SelectedView = AppView.Group);
            EntraCommand = new RelayCommand(_ => SelectedView = AppView.Entra);
            LinksCommand = new RelayCommand(_ => SelectedView = AppView.Links);
            AboutCommand = new RelayCommand(_ => SelectedView = AppView.About);
            SettingsCommand = new RelayCommand(_ => SelectedView = AppView.Settings);
            ExecuteSearchCommand = new RelayCommand(_ => TriggerSearch());
        }

        // search helpers
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
            if (string.IsNullOrWhiteSpace(query) || CurrentView is not ISearchableViewModel searchable)
                return;

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
    }
}
