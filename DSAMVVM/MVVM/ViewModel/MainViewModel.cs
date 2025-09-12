using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using System.Collections.ObjectModel;
using DSAMVVM.Core.Models;
using System.Windows;


namespace DSAMVVM.MVVM.ViewModel
{
    public class MainViewModel : ObeservableObject
    {
        // Services
        public IDepartmentService DeptService { get; } = null!;
        private readonly IADService _adService = null!;
        private readonly ISearchService _searchService = null!;
        private readonly ILinksService _linksService = null!;
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

        private object? _currentView;
        public object? CurrentView
        {
            get => _currentView;
            set
            {
                if (_currentView != value)
                {
                    _currentView = value;
                    OnPropertyChanged();
                }
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
                }
            }
        }

        public ObservableCollection<string> SearchHistory { get; } = [];

        public MainViewModel(
            IDepartmentService deptService,
            IADService adService,
            ISearchService searchService,
            StatusBarViewModel statusBar,
            ILinksService linksService,
            Func<UserViewModel> userVMFactory,
            Func<ComputerViewModel> computerVMFactory,
            Func<GroupViewModel> groupVMFactory,
            AboutViewModel aboutVM)
        {
            try
            {
                DeptService = deptService;
                _adService = adService;
                _searchService = searchService;
                _linksService = linksService;
                StatusBar = statusBar;

                _searchService.HistoryChanged += OnHistoryChanged;
                SyncHistoryFromService();

                _ = InitializeAsync();

                InitializeViewModels(userVMFactory, computerVMFactory, groupVMFactory, aboutVM);
                InitializeCommands();

                CurrentView = HomeVM;
            }
            catch (Exception ex)
            {
                UiNotify.Error("Initialization error", ex.Message, ex, alsoStatusBar: true);
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                await DeptService.PreCacheDataAsync();
            }
            catch (Exception ex)
            {
                UiNotify.Warn($"Failed to load department data: {ex.Message}", sticky: true);
            }
        }

        private void InitializeViewModels(
            Func<UserViewModel> userVMFactory,
            Func<ComputerViewModel> computerVMFactory,
            Func<GroupViewModel> groupVMFactory,
            AboutViewModel aboutVM)
        {
            HomeVM = new HomeViewModel();
            UserVM = userVMFactory();
            ComputerVM = computerVMFactory();
            GroupVM = groupVMFactory();
            EntraVM = new EntraViewModel();
            LinksVM = new LinksViewModel(_linksService);
            AboutVM = aboutVM;
            SettingsVM = new SettingsViewModel();
        }

        private void InitializeCommands()
        {
            HomeViewCommand = new RelayCommand(_ => CurrentView = HomeVM);
            UserCommand = new RelayCommand(_ => CurrentView = UserVM);
            ComputerCommand = new RelayCommand(_ => CurrentView = ComputerVM);
            GroupCommand = new RelayCommand(_ => CurrentView = GroupVM);
            EntraCommand = new RelayCommand(_ => CurrentView = EntraVM);
            LinksCommand = new RelayCommand(_ => CurrentView = LinksVM);
            AboutCommand = new RelayCommand(_ => CurrentView = AboutVM);
            SettingsCommand = new RelayCommand(_ => CurrentView = SettingsVM);
            ExecuteSearchCommand = new RelayCommand(_ => TriggerSearch());
        }

        private static SearchTarget? ResolveTargetFromView(object view)
        {
            return view switch
            {
                UserViewModel => SearchTarget.User,
                ComputerViewModel => SearchTarget.Computer,
                GroupViewModel => SearchTarget.Group,
                _ => null
            };
        }

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

            string displayName = target.Value.ToString();
            string key = $"{target}_Search";

            UiNotify.Info($"Searching {displayName}...", showStatusBar: true, key: key);

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
