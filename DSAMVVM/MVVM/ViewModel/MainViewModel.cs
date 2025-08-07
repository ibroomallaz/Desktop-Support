using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Services;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

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

        // Commands
        public RelayCommand HomeViewCommand { get; private set; } = null!;
        public RelayCommand UserCommand { get; private set; } = null!;
        public RelayCommand ComputerCommand { get; private set; } = null!;
        public RelayCommand GroupCommand { get; private set; } = null!;
        public RelayCommand EntraCommand { get; private set; } = null!;
        public RelayCommand LinksCommand { get; private set; } = null!;
        public RelayCommand AboutCommand { get; private set; } = null!;
        public RelayCommand ExecuteSearchCommand { get; private set; } = null!;

        // Current View
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

        // Search Query
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

        // Search History
        public ObservableCollection<string> SearchHistory { get; } = [];
        private const int MaxHistoryCount = 10;

        public MainViewModel(
            IDepartmentService deptService,
            IADService adService,
            ISearchService searchService,
            StatusBarViewModel statusBar,
            ILinksService linksService,
            Func<UserViewModel> userVMFactory,
            Func<ComputerViewModel> computerVMFactory,
            Func<GroupViewModel> groupVMFactory)
        {
            try
            {
                DeptService = deptService;
                _adService = adService;
                _searchService = searchService;
                _linksService = linksService;
                StatusBar = statusBar;

                _ = InitializeAsync();

                InitializeViewModels(userVMFactory, computerVMFactory, groupVMFactory);
                InitializeCommands();

                CurrentView = HomeVM;
            }
            catch (Exception ex)
            {
                StatusBar?.Report(StatusMessageFactory.Plain(
                    $"Initialization error: {ex.Message}",
                    priority: 2,
                    sticky: true));
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
                StatusBar?.Report(StatusMessageFactory.Plain(
                    $"Failed to load department data: {ex.Message}",
                    priority: 2,
                    sticky: true));
            }
        }

        private void InitializeViewModels(
            Func<UserViewModel> userVMFactory,
            Func<ComputerViewModel> computerVMFactory,
            Func<GroupViewModel> groupVMFactory)
        {
            HomeVM = new HomeViewModel();
            UserVM = userVMFactory();
            ComputerVM = computerVMFactory();
            GroupVM = groupVMFactory();
            EntraVM = new EntraViewModel();
            LinksVM = new LinksViewModel(_linksService);
            AboutVM = new AboutViewModel();
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
            if (string.IsNullOrWhiteSpace(SearchQuery) || CurrentView is not ISearchableViewModel searchable)
                return;

            // Add to history if not already present
            if (!SearchHistory.Contains(SearchQuery))
            {
                SearchHistory.Insert(0, SearchQuery);
                if (SearchHistory.Count > MaxHistoryCount)
                    SearchHistory.RemoveAt(SearchHistory.Count - 1);
            }

            var target = ResolveTargetFromView(CurrentView);
            if (target is null)
            {
                StatusBar.Report(StatusMessageFactory.Error("Search not supported for this view."));
                return;
            }

            string displayName = target.Value.ToString();
            string key = $"{target}_Search";

            StatusBar.Report(StatusMessageFactory.Plain($"Searching {displayName}...", priority: 1, key: key));

            try
            {
                var context = new SearchContextDTO(SearchQuery);
                await searchable.OnSearchUpdated(context, _searchService, target.Value);
                StatusBar.Report(StatusMessageFactory.Success("Search complete.", key: key));

                // Clear after search
                SearchQuery = string.Empty;
            }
            catch (Exception ex)
            {
                StatusBar.Report(StatusMessageFactory.Error($"Search failed: {ex.Message}", key: key));
            }
        }
    }
}
