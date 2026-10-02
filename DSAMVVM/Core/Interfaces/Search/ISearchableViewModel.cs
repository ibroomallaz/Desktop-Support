using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Models;

namespace DSAMVVM.Core.Interfaces.Search
{
    public interface ISearchableViewModel
    {
        Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target);
    }
}