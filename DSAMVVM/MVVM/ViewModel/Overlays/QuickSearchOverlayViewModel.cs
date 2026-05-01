using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.Core.Renderers;
using DSAMVVM.Core.Logging;
using DSAMVVM.MVVM.Model.AD;
using System.Windows.Documents;

namespace DSAMVVM.MVVM.ViewModel.Overlays
{

    public class QuickSearchOverlayViewModel(
        ISearchService searchService,
        IFlowDocService flowDocService,
        IDepartmentService deptService) : ObservableObject
    {
        private readonly ISearchService _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
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

            IsSearching = true;
            ResultDocument = null;

            try
            {
                Log.Info("QuickSearch", $"Inline search started for '{query}' in category: {category}");

                var target = MapViewToTarget(category);
                var context = new SearchContextDTO(query);

                var rawData = await _searchService.SearchAsync(context, target);
                string markupResult = string.Empty;

                switch (target)
                {
                    case SearchTarget.User:
                        var user = rawData as ADUserInfo;
                        markupResult = IdentityRenderer.RenderQuickADUser(user);

                        if (user != null && user.Exists)
                        {
                            if (!string.IsNullOrEmpty(user.DepartmentNumber))
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
                            }

                            // Drop the deep link at the absolute bottom
                            markupResult += $"\n[gray]   • [/gray][cyan]Action:[/cyan] [red][View Full Profile](dsa://nav/user/{user.Name})[/red]";
                        }
                        break;

                    case SearchTarget.Computer:
                        markupResult = IdentityRenderer.RenderQuickADComputer(rawData as ADComputerInfo);
                        break;

                    case SearchTarget.Group:
                        if (rawData is MimLookupResult mimResult)
                            markupResult = IdentityRenderer.RenderMimGroups(mimResult, query);
                        else if (rawData is ADGroupInfo adGroup)
                            markupResult = IdentityRenderer.RenderGroupMembers(adGroup, query);
                        break;
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

        private static SearchTarget MapViewToTarget(AppView view) => view switch
        {
            AppView.User => SearchTarget.User,
            AppView.Computer => SearchTarget.Computer,
            AppView.Group => SearchTarget.Group,
            _ => SearchTarget.User
        };
    }
}