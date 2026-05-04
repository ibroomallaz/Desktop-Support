using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.Core.Renderers;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model.AD;
using System.Windows.Documents;
using System.Windows;
using System.Windows.Controls;

namespace DSAMVVM.MVVM.ViewModel.Overlays
{
    public class QuickSearchOverlayViewModel(
        ISearchService searchService,
        IADService adService,
        IFlowDocService flowDocService,
        IDepartmentService deptService) : ObservableObject
    {
        private readonly ISearchService _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        private readonly IADService _adService = adService ?? throw new ArgumentNullException(nameof(adService));
        private readonly IFlowDocService _flowDocService = flowDocService ?? throw new ArgumentNullException(nameof(flowDocService));
        private readonly IDepartmentService _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));

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

        private FlowDocument? _resultDocument;
        public FlowDocument? ResultDocument
        {
            get => _resultDocument;
            set { _resultDocument = value; OnPropertyChanged(); }
        }

        public async Task ExecuteInlineSearchAsync(AppView category)
        {
            var query = SearchText?.Trim();
            if (string.IsNullOrWhiteSpace(query)) return;

            if (category == AppView.Group)
            {
                PromptGroupSearchMenu(query);
            }
            else
            {
                await RunTargetedSearchAsync(query, category, null);
            }
        }

        private void PromptGroupSearchMenu(string query)
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var menu = new ContextMenu
                {
                    Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint,
                    IsOpen = true
                };

                void AddMenuItem(string header, string modeIdentifier)
                {
                    var item = new MenuItem { Header = header };
                    item.Click += async (s, e) => await RunTargetedSearchAsync(query, AppView.Group, modeIdentifier);
                    menu.Items.Add(item);
                }

                AddMenuItem($"Search '{query}' in User's MIM Groups", "MIM");
                AddMenuItem($"Search '{query}' in AD Group Members", "AD");
                AddMenuItem($"Search '{query}' in Department Support", "DEPT");
                AddMenuItem($"Search '{query}' in Division Support", "DIV");
            });
        }

        private async Task RunTargetedSearchAsync(string query, AppView category, string? groupMode)
        {
            IsSearching = true;
            ResultDocument = null;

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
                    // Execute specifically based on what user clicked in the submenu
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

                ResultDocument = _flowDocService.BuildDocument(markupResult, viewName: category.ToString());
            }
            catch (Exception ex)
            {
                Log.Error("QuickSearch", $"Quick search failed for '{query}'", ex);

                var errDoc = new FlowDocMarkupBuilder();
                errDoc.AddError($"Search failed: {ex.Message}");
                ResultDocument = _flowDocService.BuildDocument(errDoc.ToString(), viewName: category.ToString());
            }
            finally
            {
                IsSearching = false;
            }
        }
    }
}