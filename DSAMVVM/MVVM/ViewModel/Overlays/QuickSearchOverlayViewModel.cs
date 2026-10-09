using DSAMVVM.MVVM.ViewModel.Cards;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
using DSAMVVM.MVVM.Model.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DSAMVVM.MVVM.ViewModel.Overlays
{
    public class QuickSearchOverlayViewModel : ObservableObject
    {
        private readonly ISearchService _searchService;
        private readonly IADService _adService;
        private readonly IDepartmentService _deptService;
        private readonly IDeepLinkRoutingService _linkRouter;

        public string ViewKey { get; } = "QuickSearchView";

        public Action? CloseAction { get; set; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (Set(ref _searchText, value))
                {
                    OnPropertyChanged(nameof(HasSearchText));
                }
            }
        }
        public bool HasSearchText => !string.IsNullOrWhiteSpace(SearchText);

        private bool _isSearching;
        public bool IsSearching
        {
            get => _isSearching;
            set
            {
                if (Set(ref _isSearching, value))
                {
                    OnPropertyChanged(nameof(ShowResultContainer));
                }
            }
        }

        private object? _resultCard;
        public object? ResultCard
        {
            get => _resultCard;
            private set
            {
                if (!Set(ref _resultCard, value)) return;
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ShowResultContainer));
            }
        }
        public bool HasResult => ResultCard != null;

        private string? _errorMessage;
        public string? ErrorMessage
        {
            get => _errorMessage;
            private set
            {
                if (Set(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(HasError));
                    OnPropertyChanged(nameof(ShowResultContainer));
                }
            }
        }
        public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

        public bool ShowResultContainer => IsSearching || HasResult || HasError;

        // Commands
        public ICommand OpenInMainAppCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public QuickSearchOverlayViewModel(
            ISearchService searchService,
            IADService adService,
            IDepartmentService deptService,
            IDeepLinkRoutingService linkRouter)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));

            ClearSearchCommand = new RelayCommand(_ =>
            {
                SearchText = string.Empty;
                ResultCard = null;
                ErrorMessage = null;
            });

            OpenInMainAppCommand = new RelayCommand(_ =>
            {
                switch (ResultCard)
                {
                    case UserHistoryItemViewModel userVm:
                        _linkRouter.RequestNavigation("user", userVm.NetId);
                        break;
                    case ComputerHistoryItemViewModel compVm:
                        _linkRouter.RequestNavigation("computer", compVm.ComputerName);
                        break;
                    case GroupHistoryItemViewModel groupVm:
                        _linkRouter.RequestNavigation("group", !string.IsNullOrWhiteSpace(groupVm.Query) ? groupVm.Query : groupVm.PrimaryHeaderTitle);
                        break;
                }

                CloseAction?.Invoke();
            });
        }

        public void LoadCapturedText(string? text)
        {
            SearchText = SanitizeCapturedText(text);
        }

        private static string SanitizeCapturedText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;

            var cleaned = text.Trim();
            if (cleaned.Length > 30) return string.Empty;

            cleaned = cleaned.Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
            return cleaned;
        }

        public async Task ExecuteInlineSearchAsync(AppView category, FrameworkElement? anchorElement = null)
        {
            var query = SearchText.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            if (category == AppView.Group)
            {
                PromptGroupSearchMenu(query, anchorElement);
            }
            else
            {
                await RunTargetedSearchAsync(query, category, null);
            }
        }

        private void PromptGroupSearchMenu(string query, FrameworkElement? anchorElement)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var activeWindow = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);

                var menu = new ContextMenu
                {
                    PlacementTarget = anchorElement ?? activeWindow,
                    Placement = anchorElement != null
                        ? System.Windows.Controls.Primitives.PlacementMode.Bottom
                        : System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                    IsOpen = true
                };

                void AddMenuItem(string header, string modeIdentifier, string inputGesture)
                {
                    var item = new MenuItem
                    {
                        Header = header,
                        InputGestureText = inputGesture
                    };
                    item.Click += async (_, _) => await RunTargetedSearchAsync(query, AppView.Group, modeIdentifier);
                    menu.Items.Add(item);
                }

                AddMenuItem($"1. Search '{query}' in User's MIM Groups", "MIM", "1");
                AddMenuItem($"2. Search '{query}' in AD Group Members", "AD", "2");
                AddMenuItem($"3. Search '{query}' in Department Support", "DEPT", "3");
                AddMenuItem($"4. Search '{query}' in Division Support", "DIV", "4");

                menu.KeyDown += async (_, e) =>
                {
                    string? mode = null;
                    switch (e.Key)
                    {
                        case Key.D1:
                        case Key.NumPad1:
                            mode = "MIM";
                            break;
                        case Key.D2:
                        case Key.NumPad2:
                            mode = "AD";
                            break;
                        case Key.D3:
                        case Key.NumPad3:
                            mode = "DEPT";
                            break;
                        case Key.D4:
                        case Key.NumPad4:
                            mode = "DIV";
                            break;
                    }

                    if (mode == null) return;
                    e.Handled = true;
                    menu.IsOpen = false;
                    await RunTargetedSearchAsync(query, AppView.Group, mode);
                };

                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    menu.Focus();
                }), System.Windows.Threading.DispatcherPriority.Input);
            });
        }

        private async Task RunTargetedSearchAsync(string query, AppView category, string? groupMode)
        {
            IsSearching = true;
            ErrorMessage = null;
            ResultCard = null;

            try
            {
                Log.Info("QuickSearch", $"Targeted inline search started for '{query}' (View: {category}, Mode: {groupMode ?? "N/A"})");

                if (category == AppView.User)
                {
                    var rawData = await _searchService.SearchAsync(new SearchContextDTO(query), SearchTarget.User);
                    var user = rawData as ADUserInfo;
                    if (user is { Exists: true })
                    {
                        var vm = new UserHistoryItemViewModel(query, user, _adService, _deptService, _linkRouter);
                        await vm.LoadDepartmentDetailsAsync();
                        ResultCard = vm;
                    }
                    else
                    {
                        ErrorMessage = user?.ErrorMessage ?? $"User '{query}' was not found in Active Directory.";
                    }
                }
                else if (category == AppView.Computer)
                {
                    var rawData = await _searchService.SearchAsync(new SearchContextDTO(query), SearchTarget.Computer);
                    var comp = rawData as ADComputerInfo;
                    if (comp is { Exists: true })
                    {
                        var vm = new ComputerHistoryItemViewModel(query, comp, _adService, _linkRouter);
                        ResultCard = vm;
                    }
                    else
                    {
                        ErrorMessage = comp?.ErrorMessage ?? $"Computer '{query}' was not found in Active Directory.";
                    }
                }
                else if (category == AppView.Group)
                {
                    _searchService.AddToHistory(query, SearchTarget.Group);
                    switch (groupMode)
                    {
                        case "MIM":
                            var mimRes = await _adService.GetUserMimGroupsAsync(query);
                            var mimVm = GroupHistoryItemViewModel.CreateUserMim(query, mimRes, _adService, _deptService, _linkRouter);
                            if (mimVm.IsFound) ResultCard = mimVm;
                            else ErrorMessage = mimVm.ErrorMessage ?? $"No MIM groups found for user '{query}'.";
                            break;

                        case "AD":
                            var groupName = query.Length == 4 && query.All(char.IsDigit) ? $"UA-MIM-0{query}" : query;
                            var adRes = await _adService.GetGroupAsync(groupName);
                            var adVm = GroupHistoryItemViewModel.CreateGroupMembers(query, groupName, adRes, _adService, _deptService, _linkRouter);
                            if (adVm.IsFound) ResultCard = adVm;
                            else ErrorMessage = adVm.ErrorMessage ?? $"Group '{groupName}' not found in Active Directory.";
                            break;

                        case "DEPT":
                            var dept = await _deptService.GetDepartmentAsync(query);
                            SupportTeam? team = null;
                            if (dept != null)
                            {
                                var teamName = await _deptService.GetTeamAsync(dept.Number);
                                if (!string.IsNullOrWhiteSpace(teamName))
                                    team = await _deptService.GetSupportTeamAsync(teamName.Trim());
                            }
                            var deptVm = GroupHistoryItemViewModel.CreateDepartment(query, dept, team, _adService, _deptService, _linkRouter);
                            if (deptVm.IsFound) ResultCard = deptVm;
                            else ErrorMessage = deptVm.ErrorMessage ?? $"Department '{query}' not found.";
                            break;

                        case "DIV":
                            var teams = await _deptService.GetTeamsByDivisionAsync(query);
                            var divVm = GroupHistoryItemViewModel.CreateDivision(query, teams, _adService, _deptService, _linkRouter);
                            if (divVm.IsFound) ResultCard = divVm;
                            else ErrorMessage = divVm.ErrorMessage ?? $"No support teams configured for division '{query}'.";
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("QuickSearch", $"Quick search failed for '{query}'", ex);
                ErrorMessage = $"Search failed: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
            }
        }

        public async Task RouteLinkClickAsync(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                await _linkRouter.HandleLinkAsync(url);
            }
        }
    }
}
