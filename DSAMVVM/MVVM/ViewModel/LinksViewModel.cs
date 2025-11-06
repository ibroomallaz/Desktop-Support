using System.Diagnostics;
using System.Windows.Input;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.Config;
using DSAMVVM.MVVM.Model.Data;

namespace DSAMVVM.MVVM.ViewModel;

public class LinksViewModel : ObeservableObject
{
    private const string Tag = "LinksVM";
    private const string StatusKey = "Links.Reload";

    private readonly ILinksService _linksService;
    private readonly ISettingsService _settingsService;

    private static AppSettings Settings => App.Settings;

    private int _loadedFlag; // 0 = not loaded, 1 = loaded (or in-flight first load)

    public LinksViewModel(ILinksService linksService, ISettingsService settingsService)
    {
        _linksService = linksService ?? throw new ArgumentNullException(nameof(linksService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        ReloadLinksCommand = new RelayCommand(async _ => await ReloadAsync());

        //no implicit loading here. Call EnsureLoadedAsync() after first paint.
        // this avoids slower startup times if loading is slow.
    }

    public async Task EnsureLoadedAsync()
    {
        if (Interlocked.Exchange(ref _loadedFlag, 1) == 1) return; // already loaded or loading

        var sw = Stopwatch.StartNew();
        Log.Debug(Tag, "initial_load: begin");

        try
        {
            var cached = _linksService.GetCachedLinksData();
            Log.Debug(Tag, $"initial_load: cached_present={(cached != null)}");
            if (cached is not null) Apply(cached);

            var data = await _linksService.LoadLinksDataAsync().ConfigureAwait(false);
            if (data is not null) Apply(data);

            sw.Stop();
            Log.Debug(Tag, $"initial_load: success duration_ms={sw.ElapsedMilliseconds} teams={TeamNames.Count} common={CommonLinks.Count}");
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error(Tag, $"initial_load: failed duration_ms={sw.ElapsedMilliseconds}", ex);
            UiNotify.Warn($"Failed to load links: {ex.Message}");
            // allow reattempt on next call
            Interlocked.Exchange(ref _loadedFlag, 0);
        }
    }

    private bool _isReloading;
    public bool IsReloading
    {
        get => _isReloading;
        private set { _isReloading = value; OnPropertyChanged(); }
    }

    private List<Link> _commonLinks = [];
    public List<Link> CommonLinks
    {
        get => _commonLinks;
        private set { _commonLinks = value ?? []; OnPropertyChanged(); }
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
        private set { _teamNames = value ?? []; OnPropertyChanged(); }
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
        private set { _selectedTeamLinks = value ?? []; OnPropertyChanged(); }
    }

    public ICommand ReloadLinksCommand { get; }

    public void ReevaluateSelection()
    {
        ChooseInitialTeam();
        UpdateSelectedTeamLinks();
    }

    private async Task ReloadAsync()
    {
        if (IsReloading) { Log.Debug(Tag, "reload: skipped (busy)"); return; }

        var previousTeam = SelectedTeam ?? "";
        var sw = Stopwatch.StartNew();
        IsReloading = true;

        Log.Debug(Tag, $"reload: begin prevTeam='{previousTeam}'");

        try
        {
            UiNotify.Info("Refreshing links…", showStatusBar: true, key: StatusKey);

            await _linksService.ReloadLinksDataAsync().ConfigureAwait(false);
            var data = _linksService.GetCachedLinksData();

            if (data is not null) Apply(data);

            if (!string.IsNullOrWhiteSpace(previousTeam) &&
                TeamNames.Any(n => string.Equals(n, previousTeam, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedTeam = previousTeam;
                Log.Debug(Tag, $"reload: selection_restored='{previousTeam}'");
            }
            else
            {
                Log.Debug(Tag, "reload: selection_restored=none");
            }

            sw.Stop();
            Log.Debug(Tag, $"reload: success duration_ms={sw.ElapsedMilliseconds} teams={TeamNames.Count} common={CommonLinks.Count} selected='{SelectedTeam ?? ""}' teamLinkCount={SelectedTeamLinks.Count}");

            UiNotify.Success("Links reloaded.", showStatusBar: true, key: StatusKey);
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log.Error(Tag, $"reload: failed duration_ms={sw.ElapsedMilliseconds}", ex);
            UiNotify.Warn($"Links reload failed: {ex.Message}");
        }
        finally
        {
            IsReloading = false;
        }
    }

    private void Apply(LinksData data)
    {
        CommonLinks = data?.CommonLinks ?? [];
        TeamLinks = data?.TeamLinks ?? [];
        Log.Debug(Tag, $"apply: common={CommonLinks.Count} teams={TeamLinks.Count}");
    }

    private void RebuildTeamNames()
    {
        TeamNames = [.. TeamLinks
            .Select(t => (t?.Team ?? string.Empty).Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)];
    }

    private void ChooseInitialTeam()
    {
        if (TeamNames.Count == 0) { SelectedTeam = null; return; }

        var prefs = Settings.Ui.Links;

        if (prefs.OverrideEnabled)
        {
            var ov = (prefs.OverrideTeam ?? string.Empty).Trim();
            var match = TeamNames.FirstOrDefault(n => string.Equals(n, ov, StringComparison.OrdinalIgnoreCase));
            SelectedTeam = match ?? TeamNames[0];
            return;
        }

        if (prefs.OpenLastViewedFirst)
        {
            var last = (prefs.LastTeam ?? string.Empty).Trim();
            var match = TeamNames.FirstOrDefault(n => string.Equals(n, last, StringComparison.OrdinalIgnoreCase));
            SelectedTeam = match ?? TeamNames[0];
            return;
        }

        SelectedTeam = TeamNames[0];
    }

    private void UpdateSelectedTeamLinks()
    {
        if (string.IsNullOrWhiteSpace(SelectedTeam)) { SelectedTeamLinks = []; return; }

        var group = TeamLinks.FirstOrDefault(g =>
            string.Equals(g?.Team, SelectedTeam, StringComparison.OrdinalIgnoreCase));

        SelectedTeamLinks = group?.Links ?? [];
    }

    private void PersistLastTeam()
    {
        if (!string.IsNullOrWhiteSpace(SelectedTeam) &&
            TeamNames.Any(n => string.Equals(n, SelectedTeam, StringComparison.OrdinalIgnoreCase)))
            Settings.Ui.Links.LastTeam = SelectedTeam;
        else
            Settings.Ui.Links.LastTeam = null;

        _settingsService.RequestSave(Settings, Globals.g_SettingsPath);
    }
}
