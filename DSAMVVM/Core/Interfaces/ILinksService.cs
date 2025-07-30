using DSAMVVM.MVVM.Model;
using System.Threading.Tasks;

namespace DSAMVVM.Core.Interfaces
{
    public interface ILinksService
    {
        Task<LinksData?> LoadLinksDataAsync();
        Task ReloadLinksDataAsync();
    }
}
