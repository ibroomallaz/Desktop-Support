using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Services;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System;
using System.Threading.Tasks;

namespace DSAMVVM.MVVM.ViewModel
{
    public class MainViewModel : ObeservableObject
    {
        // Services
        public IDepartmentService DeptService { get; }
        private readonly IADService _adService;
        private readonly ILinksService _linksService;
        public StatusBarViewModel StatusBar { get; }

        // ViewModels
        public HomeViewModel HomeVM { get; private set; }
        public ComputerViewModel ComputerVM { get; private set; }
        public UserViewModel UserVM { get; private set; }
        public GroupViewModel GroupVM { get; private set; }
        public EntraViewModel EntraVM { get; private set; }
        public LinksViewModel LinksVM { get; private set; }
        public AboutViewModel AboutVM { get; private set; }

        // Commands
        public RelayCommand HomeViewCommand { get; private set; }
        public RelayCommand UserCommand { get; private set; }
        public RelayCommand ComputerCommand { get; private set; }
        public RelayCommand GroupCommand { get; private set; }
        public RelayCommand EntraCommand { get; private set; }
        public RelayCommand LinksCommand { get; private set; }
        public RelayCommand AboutCommand { get; private set; }
        public RelayCommand ExecuteSearchCommand { get; private set; }

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

        public MainViewModel(
            IDepartmentService deptService,
            IADService adService,
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
                _linksService = linksService;
                StatusBar = statusBar;

                _ = InitializeAsync(); // Fire-and-forget

                InitializeViewModels(userVMFactory, computerVMFactory, groupVMFactory);
                InitializeCommands();

                CurrentView = HomeVM;
            }
            catch (Exception ex)
            {
                StatusBar?.Report(StatusMessageFactory.Plain($"Initialization error: {ex.Message}", priority: 2, sticky: true));
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
                StatusBar?.Report(StatusMessageFactory.Plain($"Failed to load department data: {ex.Message}", priority: 2, sticky: true));
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

        private async void TriggerSearch()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery) || CurrentView is not ISearchableViewModel searchable)
                return;

            string rawName = CurrentView.GetType().Name;
            string displayName = rawName switch
            {
                "UserViewModel" => "NetID",
                "ComputerViewModel" => "Computer",
                "GroupViewModel" => "Group",
                "EntraViewModel" => "Entra",
                _ => rawName.Replace("ViewModel", "") // fallback
            };

            string key = $"{rawName}_Search";

            StatusBar.Report(StatusMessageFactory.Plain($"Searching {displayName}...", priority: 1, key: key));

            try
            {
                await searchable.OnSearchUpdated(SearchQuery);
                StatusBar.Report(StatusMessageFactory.Success("Search complete.", key: key));
            }
            catch (Exception ex)
            {
                StatusBar.Report(StatusMessageFactory.Error($"Search failed: {ex.Message}", key: key));
            }
        }

    }
}
