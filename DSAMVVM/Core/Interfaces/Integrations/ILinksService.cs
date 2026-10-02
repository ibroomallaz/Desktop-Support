using DSAMVVM.MVVM.Model.Data;
namespace DSAMVVM.Core.Interfaces.Integrations;

public interface ILinksService
{
    Task<LinksData?> LoadLinksDataAsync();
    Task ReloadLinksDataAsync();
    LinksData? GetCachedLinksData();
}
