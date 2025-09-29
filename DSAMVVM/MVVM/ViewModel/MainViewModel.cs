using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using Microsoft.Extensions.DependencyInjection;

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
        }

        // Show Home immediately (content renders right away) and then sync the sidebar
        public void BootstrapInitialView()
        {
            CurrentView = HomeVM; // immediate render

            var d = Application.Current?.Dispatcher;
            if (d is not null)
                d.BeginInvoke(() => SelectedView = AppView.Home, DispatcherPriority.Loaded);
            else
                SelectedView = AppView.Home;
        }

        // Call this after first render to run warmups without delaying startup
        public void StartWarmup()
        {
            _ = InitializeAsync();
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
