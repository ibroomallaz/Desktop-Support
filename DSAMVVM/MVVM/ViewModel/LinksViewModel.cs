using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model.Data;

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
        set
        {
            _teamLinks = value;
            OnPropertyChanged();
            RebuildTeamNames();
            EnsureDefaultTeam();
        }
    }

    private List<string> _teamNames = [];
    public List<string> TeamNames
    {
        get => _teamNames;
        private set { _teamNames = value; OnPropertyChanged(); }
    }

    private string? _selectedTeam;
    public string? SelectedTeam
    {
        get => _selectedTeam;
        set
        {
            if (_selectedTeam == value) return;
            _selectedTeam = value;
            OnPropertyChanged();
            UpdateSelectedTeamLinks();
        }
    }

    private List<Link> _selectedTeamLinks = [];
    public List<Link> SelectedTeamLinks
    {
        get => _selectedTeamLinks;
        private set { _selectedTeamLinks = value; OnPropertyChanged(); }
    }

    public LinksViewModel(ILinksService linksService)
    {
        _linksService = linksService;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var cached = _linksService.GetCachedLinksData();
        if (cached != null) { Apply(cached); return; }

        var data = await _linksService.LoadLinksDataAsync();
        if (data != null) Apply(data);
    }

    private void Apply(LinksData data)
    {
        CommonLinks = data.CommonLinks ?? [];
        TeamLinks = data.TeamLinks ?? [];
        UpdateSelectedTeamLinks();
    }

    private void RebuildTeamNames()
    {
        TeamNames = TeamLinks?
            .Select(t => (t?.Team ?? "").Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private void EnsureDefaultTeam()
    {
        if (SelectedTeam == null && TeamNames.Count > 0)
            SelectedTeam = TeamNames[0];
    }

    private void UpdateSelectedTeamLinks()
    {
        if (string.IsNullOrWhiteSpace(SelectedTeam))
        {
            SelectedTeamLinks = [];
            return;
        }

        var group = TeamLinks?.FirstOrDefault(g =>
            string.Equals(g?.Team, SelectedTeam, StringComparison.OrdinalIgnoreCase));

        SelectedTeamLinks = group?.Links ?? [];
    }
}
