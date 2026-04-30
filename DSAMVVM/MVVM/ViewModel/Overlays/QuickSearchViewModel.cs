using System.Windows.Documents;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;

namespace DSAMVVM.MVVM.ViewModel.Overlays
{
    public class QuickSearchViewModel : ObservableObject
    {
        private readonly ISearchService _searchService;
        private readonly IFlowDocService _flowDocService;

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

        public QuickSearchViewModel(ISearchService searchService, IFlowDocService flowDocService)
        {
            _searchService = searchService;
            _flowDocService = flowDocService;
        }

        public async Task ExecuteInlineSearchAsync(AppView category)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return;

            IsSearching = true;
            ResultDocument = null;

            try
            {
                var target = MapViewToTarget(category);
                var context = new SearchContextDTO(SearchText.Trim());

                var rawData = await _searchService.SearchAsync(context, target);

                if (rawData != null)
                {
                    // Formats the returned object into a string for the FlowDoc parser
                    string formattedText = rawData.ToString() ?? "Data retrieved but could not be parsed.";

                    ResultDocument = _flowDocService.BuildDocument(
                        fullText: formattedText,
                        viewName: category.ToString());
                }
                else
                {
                    ResultDocument = _flowDocService.BuildDocument("No results found.");
                }
            }
            catch (Exception ex)
            {
                ResultDocument = _flowDocService.BuildDocument($"Search failed: {ex.Message}");
            }
            finally
            {
                IsSearching = false;
            }
        }

        private static SearchTarget MapViewToTarget(AppView view)
        {
            return view switch
            {
                AppView.User => SearchTarget.User,
                AppView.Computer => SearchTarget.Computer,
                AppView.Group => SearchTarget.Group,
                _ => SearchTarget.User
            };
        }
    }
}