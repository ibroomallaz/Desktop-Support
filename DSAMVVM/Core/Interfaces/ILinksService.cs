using DSAMVVM.MVVM.Model;

public interface ILinksService
{
    Task<LinksData?> LoadLinksDataAsync();
    Task ReloadLinksDataAsync();
    LinksData? GetCachedLinksData();
}
