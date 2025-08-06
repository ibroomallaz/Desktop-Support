using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;

namespace DSAMVVM.MVVM.ViewModel;
public class LinksViewModel : ObeservableObject
{
    private readonly ILinksService _linksService;

    private List<Link> _commonLinks = [];
    public List<Link> CommonLinks
    {
        get => _commonLinks;
        set { _commonLinks = value; OnPropertyChanged(); }
    }

    private List<TeamLinkGroup> _teamLinks = [];
    public List<TeamLinkGroup> TeamLinks
    {
        get => _teamLinks;
        set { _teamLinks = value; OnPropertyChanged(); }
    }

    public LinksViewModel(ILinksService linksService)
    {
        _linksService = linksService;
        _ = LoadLinksAsync();
    }

    private async Task LoadLinksAsync()
    {
        // Check cache first
        var cached = _linksService.GetCachedLinksData();
        if (cached != null)
        {
            CommonLinks = cached.CommonLinks;
            TeamLinks = cached.TeamLinks;

            PrintCommonLinks(); // print immediately from cache
            return;
        }

        // Load from source if cache is empty
        var data = await _linksService.LoadLinksDataAsync();
        if (data != null)
        {
            CommonLinks = data.CommonLinks;
            TeamLinks = data.TeamLinks;

            PrintCommonLinks(); // print after load
        }
    }

    private void PrintCommonLinks()
    {
        System.Diagnostics.Debug.WriteLine("=== Common Links ===");
        foreach (var link in CommonLinks)
        {
            System.Diagnostics.Debug.WriteLine($"{link.Name} - {link.URL}");
        }
    }
}
