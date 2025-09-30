using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Model.Data;

namespace DSAMVVM.MVVM.ViewModel;

public class LinksViewModel : ObeservableObject
{
    private readonly ILinksService _linksService;
    private readonly ISettingsService _settingsService;

    private static AppSettings Settings => App.Settings;

    public LinksViewModel(ILinksService linksService, ISettingsService settingsService)
    {
        _linksService = linksService ?? throw new ArgumentNullException(nameof(linksService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _ = LoadAsync();
    }

    //Data

    private List<Link> _commonLinks = [];
    public List<Link> CommonLinks
    {
        get => _commonLinks;
        private set
        {
            _commonLinks = value ?? [];
            OnPropertyChanged();
        }
    }

    private List<TeamLinkGroup> _teamLinks = [];
    public List<TeamLinkGroup> TeamLinks
    {
        get => _teamLinks;
        private set
        {
            _teamLinks = value ?? [];
            OnPropertyChanged();
            RebuildTeamNames();
            ChooseInitialTeam();
            UpdateSelectedTeamLinks();
        }
    }

    private List<string> _teamNames = [];
    public List<string> TeamNames
    {
        get => _teamNames;
        private set
        {
            _teamNames = value ?? [];
            OnPropertyChanged();
        }
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
            PersistLastTeam();
        }
    }

    private List<Link> _selectedTeamLinks = [];
    public List<Link> SelectedTeamLinks
    {
        get => _selectedTeamLinks;
        private set
        {
            _selectedTeamLinks = value ?? [];
            OnPropertyChanged();
        }
    }

    //Public hook for settings UI
    public void ReevaluateSelection()
    {
        ChooseInitialTeam();
        UpdateSelectedTeamLinks();
    }

    //Load & apply

    private async Task LoadAsync()
    {
        try
        {
            var cached = _linksService.GetCachedLinksData();
            if (cached is not null) Apply(cached);

            var data = await _linksService.LoadLinksDataAsync().ConfigureAwait(false);
            if (data is not null) Apply(data);
        }
        catch (Exception ex)
        {
            UiNotify.Warn($"Failed to load links: {ex.Message}");
        }
    }

    private void Apply(LinksData data)
    {
        CommonLinks = data?.CommonLinks ?? [];
        TeamLinks = data?.TeamLinks ?? [];
    }

    private void RebuildTeamNames()
    {
        TeamNames = [.. TeamLinks
            .Select(t => (t?.Team ?? string.Empty).Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)];
    }

    // Preference priority:
    // 1) OverrideEnabled + valid OverrideTeam
    // 2) OpenLastViewedFirst + valid LastTeam
    // 3) First team
    private void ChooseInitialTeam()
    {
        if (TeamNames.Count == 0)
        {
            SelectedTeam = null;
            return;
        }

        var prefs = Settings.Ui.Links;

        // 1) Override mode
        if (prefs.OverrideEnabled)
        {
            var ov = (prefs.OverrideTeam ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(ov))
            {
                var match = TeamNames.FirstOrDefault(n =>
                    string.Equals(n, ov, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match))
                {
                    SelectedTeam = match;
                    return;
                }
            }

            // If override is active but missing/invalid -> fall through to first
            SelectedTeam = TeamNames[0];
            return;
        }

        // 2) Last viewed mode
        if (prefs.OpenLastViewedFirst)
        {
            var last = (prefs.LastTeam ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(last))
            {
                var match = TeamNames.FirstOrDefault(n =>
                    string.Equals(n, last, StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(match))
                {
                    SelectedTeam = match;
                    return;
                }
            }

            // If last viewed is missing/invalid -> fall through to first
            SelectedTeam = TeamNames[0];
            return;
        }

        // 3) Neither mode is enabled -> first team
        SelectedTeam = TeamNames[0];
    }

    private void UpdateSelectedTeamLinks()
    {
        if (string.IsNullOrWhiteSpace(SelectedTeam))
        {
            SelectedTeamLinks = [];
            return;
        }

        var group = TeamLinks.FirstOrDefault(g =>
            string.Equals(g?.Team, SelectedTeam, StringComparison.OrdinalIgnoreCase));

        SelectedTeamLinks = group?.Links ?? [];
    }

    private void PersistLastTeam()
    {
        // Always keep the last viewed up to date (useful when user switches to "Last viewed" later)
        if (!string.IsNullOrWhiteSpace(SelectedTeam) &&
            TeamNames.Any(n => string.Equals(n, SelectedTeam, StringComparison.OrdinalIgnoreCase)))
        {
            Settings.Ui.Links.LastTeam = SelectedTeam;
        }
        else
        {
            Settings.Ui.Links.LastTeam = null;
        }

        _settingsService.RequestSave(Settings, Globals.g_SettingsPath);
    }
}
