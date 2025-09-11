using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Models;

namespace DSAMVVM.Core.Interfaces
{
    public interface ISearchableViewModel
    {
        Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target);
    }
}