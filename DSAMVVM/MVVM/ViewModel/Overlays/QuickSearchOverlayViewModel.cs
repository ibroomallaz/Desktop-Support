using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Renderers;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.AD;
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
        private readonly IFlowDocService _flowDocService;

        public string ViewKey { get; } = "QuickSearchView";

        public Action? CloseAction { get; set; }

        public QuickSearchOverlayViewModel(
            ISearchService searchService,
            IADService adService,
            IDepartmentService deptService,
            IDeepLinkRoutingService linkRouter,
            IFlowDocService flowDocService)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));
            _flowDocService = flowDocService ?? throw new ArgumentNullException(nameof(flowDocService));

            _flowDocService.LinkClicked += OnLinkClicked;
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); }
        }

        private bool _isSearching;
        public bool IsSearching
        {
            get => _isSearching;
            set { _isSearching = value; OnPropertyChanged(); }
        }

        private string? _resultText;
        public string? ResultText
        {
            get => _resultText;
            set { _resultText = value; OnPropertyChanged(); }
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
            var query = SearchText?.Trim();
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
                    item.Click += async (s, e) => await RunTargetedSearchAsync(query, AppView.Group, modeIdentifier);
                    menu.Items.Add(item);
                }

                AddMenuItem($"1. Search '{query}' in User's MIM Groups", "MIM", "1");
                AddMenuItem($"2. Search '{query}' in AD Group Members", "AD", "2");
                AddMenuItem($"3. Search '{query}' in Department Support", "DEPT", "3");
                AddMenuItem($"4. Search '{query}' in Division Support", "DIV", "4");

                menu.KeyDown += async (s, e) =>
                {
                    string? mode = null;
                    if (e.Key == Key.D1 || e.Key == Key.NumPad1) mode = "MIM";
                    else if (e.Key == Key.D2 || e.Key == Key.NumPad2) mode = "AD";
                    else if (e.Key == Key.D3 || e.Key == Key.NumPad3) mode = "DEPT";
                    else if (e.Key == Key.D4 || e.Key == Key.NumPad4) mode = "DIV";

                    if (mode != null)
                    {
                        e.Handled = true;
                        menu.IsOpen = false;
                        await RunTargetedSearchAsync(query, AppView.Group, mode);
                    }
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
            ResultText = null;

            try
            {
                Log.Info("QuickSearch", $"Targeted inline search started for '{query}' (View: {category}, Mode: {groupMode ?? "N/A"})");

                string markupResult = string.Empty;

                if (category == AppView.User)
                {
                    var rawData = await _searchService.SearchAsync(new SearchContextDTO(query), SearchTarget.User);
                    var user = rawData as ADUserInfo;
                    markupResult = IdentityRenderer.RenderQuickADUser(user);

                    if (user != null && user.Exists && !string.IsNullOrEmpty(user.DepartmentNumber))
                    {
                        var deptDoc = new FlowDocMarkupBuilder();
                        var dept = await _deptService.GetDepartmentAsync(user.DepartmentNumber);
                        if (dept != null)
                        {
                            var teamName = await _deptService.GetTeamAsync(dept.Number);
                            if (!string.IsNullOrWhiteSpace(teamName)) deptDoc.AddLabelValue("Support Team: ", teamName);
                            if (!string.IsNullOrWhiteSpace(dept.Notes)) deptDoc.AddLabelValue("Notes: ", dept.Notes);
                        }
                        markupResult += deptDoc.ToString();
                        markupResult += $"\n[gray]   • [/gray][cyan]Action:[/cyan] [red][View Full Profile](dsa://nav/user/{user.Name})[/red]";
                    }
                }
                else if (category == AppView.Computer)
                {
                    var rawData = await _searchService.SearchAsync(new SearchContextDTO(query), SearchTarget.Computer);
                    markupResult = IdentityRenderer.RenderQuickADComputer(rawData as ADComputerInfo);
                }
                else if (category == AppView.Group)
                {
                    switch (groupMode)
                    {
                        case "MIM":
                            var mimRes = await _adService.GetUserMimGroupsAsync(query);
                            markupResult = IdentityRenderer.RenderMimGroups(mimRes, query);
                            break;
                        case "AD":
                            var groupName = query.Length == 4 && query.All(char.IsDigit) ? $"UA-MIM-0{query}" : query;
                            var adRes = await _adService.GetGroupAsync(groupName);
                            markupResult = IdentityRenderer.RenderGroupMembers(adRes, groupName);
                            break;
                        case "DEPT":
                            markupResult = await OrganizationalRenderer.RenderDepartmentContextAsync(query, _deptService);
                            if (string.IsNullOrWhiteSpace(markupResult))
                                markupResult = $"[cyan]Department '{query}' not found.[/cyan]";
                            break;
                        case "DIV":
                            markupResult = await OrganizationalRenderer.RenderDivisionSupportAsync(query, _deptService);
                            if (string.IsNullOrWhiteSpace(markupResult))
                                markupResult = $"[cyan]Division '{query}' not found.[/cyan]";
                            break;
                    }
                }

                if (string.IsNullOrWhiteSpace(markupResult))
                {
                    markupResult = "[cyan]No results found or unrecognized data format.[/cyan]";
                }

                ResultText = markupResult;

                if (category == AppView.Group)
                {
                    _searchService.AddToHistory(query);
                }
            }
            catch (Exception ex)
            {
                Log.Error("QuickSearch", $"Quick search failed for '{query}'", ex);

                var errDoc = new FlowDocMarkupBuilder();
                errDoc.AddError($"Search failed: {ex.Message}");
                ResultText = errDoc.ToString();
            }
            finally
            {
                IsSearching = false;
            }
        }

        private async void OnLinkClicked(object? sender, string url)
        {
            string? sourceView = sender as string;
            if (sourceView != ViewKey) return;

            if (IsSearching) return;

            string result = await _linkRouter.HandleLinkAsync(url);
            if (!string.IsNullOrWhiteSpace(result))
            {
                ResultText += result;
            }
        }

        public async Task RouteLinkClickAsync(string url)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                string result = await _linkRouter.HandleLinkAsync(url);
                if (!string.IsNullOrWhiteSpace(result))
                {
                    ResultText += result;
                }
            }
        }
    }
}