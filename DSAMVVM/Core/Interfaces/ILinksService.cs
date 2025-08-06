using DSAMVVM.MVVM.Model;
namespace DSAMVVM.Core.Interfaces;
public interface ILinksService
{
    Task<LinksData?> LoadLinksDataAsync();
    Task ReloadLinksDataAsync();
    LinksData? GetCachedLinksData();
}
