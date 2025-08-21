using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;

public interface ISearchableViewModel
{
    Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target);
}
