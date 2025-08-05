using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;

public interface ISearchableViewModel
{
    Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target);
}
