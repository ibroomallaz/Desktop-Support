using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using System.Diagnostics;
using System.IO;



namespace DSAMVVM.MVVM.ViewModel
{
    public class GroupViewModel : ObeservableObject, ISearchableViewModel, IDisposable
    {
        // Services
        private readonly IADService _ad;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;

        // Mode state
        private bool _isUserMim = true;
        public bool IsUserMim
        {
            get => _isUserMim;
            set
            {
                if (_isUserMim == value) return;
                _isUserMim = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGroupMembers));
            }
        }

        public bool IsGroupMembers
        {
            get => !_isUserMim;
            set
            {
                if (value == IsGroupMembers) return;
                _isUserMim = !value;
                OnPropertyChanged(nameof(IsUserMim));
                OnPropertyChanged();
            }
        }

        // UI state
        private double _effectiveFontSize = 14;
        public double EffectiveFontSize
        {
            get => _effectiveFontSize;
            private set { _effectiveFontSize = value; OnPropertyChanged(nameof(EffectiveFontSize)); }
        }

        private string _query = string.Empty;
        public string Query
        {
            get => _query;
            set { _query = value?.Trim() ?? string.Empty; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(); }
        }

        private string? _error;
        public string? Error
        {
            get => _error;
            private set { _error = value; OnPropertyChanged(); }
        }

        // Output log
        private string _searchLog = string.Empty;
        public string SearchLog
        {
            get => _searchLog;
            private set { _searchLog = value; OnPropertyChanged(nameof(SearchLog)); }
        }

        public GroupViewModel(
            IADService adService,
            ISettingsService settingsSvc,
            IOutputTextSettingsProvider notifier)
        {
            _ad = adService ?? throw new ArgumentNullException(nameof(adService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));

            _notifier.Changed += OnFontSettingsChanged;
            RefreshEffectiveFontSize();
        }

        private void OnFontSettingsChanged(object? sender, EventArgs e) => RefreshEffectiveFontSize();

        private void RefreshEffectiveFontSize()
        {
            EffectiveFontSize = _notifier.GetFontSize("GroupView");
        }

        // Output helpers
        private void AppendRaw(string message)
        {
            SearchLog += (message ?? string.Empty) + "\n";
            Debug.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }

        private void AppendTitle(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            AppendRaw($"[yellow]{text}[/yellow]");
        }

        private void AppendLabelValue(string label, string? value, bool treatEmptyAsNone = true)
        {
            var finalValue = value;
            if (string.IsNullOrWhiteSpace(finalValue) && treatEmptyAsNone) finalValue = "None";
            if (finalValue == null) return;
            AppendRaw($"[cyan]{label}[/cyan][red]{finalValue}[/red]");
        }

        public void ClearLog() => SearchLog = string.Empty;

        // Search entry point for this view
        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService _search, SearchTarget target)
        {
            Error = null;

            if (!string.IsNullOrEmpty(SearchLog))
            {
                AppendRaw("\n[cyan]────────── New Search ──────────[/cyan]\n");
                if (!string.IsNullOrWhiteSpace(context?.Query))
                    AppendRaw($"[cyan]Query:[/cyan] [red]{context.Query}[/red]");
            }
            else if (!string.IsNullOrWhiteSpace(context?.Query))
            {
                AppendRaw($"[cyan]Query:[/cyan] [red]{context.Query}[/red]");
            }

            if (target != SearchTarget.Group)
            {
                Error = "Invalid search target provided to GroupViewModel.";
                AppendRaw("[red]Invalid search target for GroupViewModel[/red]");
                Log.Warn("GroupView", $"Invalid target: {target}");
                return;
            }

            if (!string.IsNullOrWhiteSpace(context?.Query))
                Query = context!.Query!.Trim();

            await ExecuteAsync();
        }

        public async Task ExecuteAsync()
        {
            if (string.IsNullOrWhiteSpace(Query))
            {
                AppendRaw($"[cyan]Enter {(IsUserMim ? "a NetID" : "a group name or 4-digit dept#")} in the main search.[/cyan]");
                Log.Info("GroupView", "Aborted: empty query");
                return;
            }

            Error = null;
            IsLoading = true;

            try
            {
                AppendRaw("[green]Starting group search...[/green]");
                Log.Debug("GroupView", $"Dispatching lookup. Mode={(IsUserMim ? "UserMIM" : "GroupMembers")}, Query='{Query}'");

                if (IsUserMim)
                {
                    AppendTitle($"MIM groups for user '{Query}'");
                    var r = await _ad.GetUserMimGroupsAsync(Query);

                    if (!r.Exists)
                    {
                        Error = string.IsNullOrWhiteSpace(r.Error) ? $"'{Query}' is not a valid NetID." : r.Error;
                        AppendRaw($"[red]{Error}[/red]");
                        Log.Info("GroupView", $"UserMIM not found. Error='{Error}'");
                        return;
                    }

                    if (r.Enabled == false)
                        AppendLabelValue("Enabled: ", "False", treatEmptyAsNone: false);

                    AppendLabelValue("Total MIM groups: ", r.Groups?.Count.ToString() ?? "0", treatEmptyAsNone: false);

                    if (r.Groups is { Count: > 0 })
                    {
                        foreach (var g in r.Groups)
                            AppendRaw($"[lightgray] • {g}[/lightgray]");
                    }
                    else
                    {
                        AppendRaw("[cyan]No valid MIM groups found.[/cyan]");
                    }

                    Log.Info("GroupView", $"UserMIM success: Count={r.Groups?.Count ?? 0}, Enabled={r.Enabled}");
                }
                else
                {
                    var groupName = NormalizeGroupName(Query);
                    AppendTitle($"Members of group '{groupName}'");

                    var info = await _ad.GetGroupAsync(groupName);

                    if (info.Exists && info.MemberCount is int c)
                    {
                        AppendLabelValue("Total members: ", c.ToString(), treatEmptyAsNone: false);

                        if (c == 0)
                        {
                            AppendRaw("[cyan]No group members exist.[/cyan]");
                        }
                        else
                        {
                            if (info.GroupMembers is not null)
                                foreach (var m in info.GroupMembers)
                                    AppendRaw($"[lightgray] • {m}[/lightgray]");
                        }

                        Log.Info("GroupView", $"Group found: '{groupName}', Members={c}");
                    }
                    else
                    {
                        Error = info.ErrorMessage ?? "Group not found or lookup failed.";
                        AppendRaw($"[red]{Error}[/red]");
                        Log.Info("GroupView", $"Group not found: '{groupName}', Error='{Error}'");
                    }
                }

                AppendRaw(string.Empty);
                Log.Info("GroupView", "Search completed");
            }
            catch (Exception ex)
            {
                Error = $"Search failed: {ex.Message}";
                AppendRaw($"[red]Exception during group search: {ex}[/red]");
                Log.Error("GroupView", "Search failed", ex);
            }
            finally
            {
                IsLoading = false;
                AppendRaw("[green]Group search process completed.[/green]\n");
            }
        }

        // Font control actions; respects per-view override setting
        public void AdjustFont(int delta)
        {
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;

            _ = _settingsSvc.AdjustOutputFontSize(s, perView ? "GroupView" : null, delta, perView);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        public void ResetFont()
        {
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;

            _settingsSvc.ResetOutputFontSize(s, perView ? "GroupView" : null, perView, 14);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        private static string NormalizeGroupName(string input)
        {
            var s = input.Trim();
            if (s.Length == 4 && int.TryParse(s, out _)) return $"UA-MIM-0{s}";
            return s;
        }

        public void Dispose()
        {
            _notifier.Changed -= OnFontSettingsChanged;
        }
    }
}
