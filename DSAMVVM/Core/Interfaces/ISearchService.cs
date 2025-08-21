using System.Threading.Tasks;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Models;

namespace DSAMVVM.Core.Interfaces
{
    public interface ISearchService
    {
        Task<object?> SearchAsync(SearchContextDTO context, SearchTarget target);
    }
}
