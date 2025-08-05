using System.Threading.Tasks;
using DSAMVVM.Core.Enums;

namespace DSAMVVM.Core.Interfaces
{
    public interface ISearchService
    {
        Task<object?> SearchAsync(SearchContextDTO context, SearchTarget target);
    }
}
