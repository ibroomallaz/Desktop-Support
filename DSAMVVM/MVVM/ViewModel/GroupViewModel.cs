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
    public class GroupViewModel : ObservableObject, ISearchableViewModel, IDisposable
    {
        // Services
        private readonly IADService _ad;
        private readonly IDepartmentService _deptService;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;

        // Mode state
        public enum GroupSearchMode
        {
            UserMim,
            GroupMembers,
            Department,
            Division
        }

        private GroupSearchMode _searchMode = GroupSearchMode.UserMim;
        public string CurrentViewContext => $"GroupView.{_searchMode}";

        public bool IsUserMim
        {
            get => _searchMode == GroupSearchMode.UserMim;
            set { if (value) UpdateMode(GroupSearchMode.UserMim); }
        }

        public bool IsGroupMembers
        {
            get => _searchMode == GroupSearchMode.GroupMembers;
            set { if (value) UpdateMode(GroupSearchMode.GroupMembers); }
        }

        public bool IsDeptSearch
        {
            get => _searchMode == GroupSearchMode.Department;
            set { if (value) UpdateMode(GroupSearchMode.Department); }
        }
        public bool IsDivSearch
        {
            get => _searchMode == GroupSearchMode.Division;
            set { if (value) UpdateMode(GroupSearchMode.Division); }
        }
        // Core logic to handle exclusive switching
        private void UpdateMode(GroupSearchMode newMode)
        {
            if (_searchMode == newMode) return;

            _searchMode = newMode;

            OnPropertyChanged(nameof(IsUserMim));
            OnPropertyChanged(nameof(IsGroupMembers));
            OnPropertyChanged(nameof(IsDeptSearch));
            OnPropertyChanged(nameof(IsDivSearch));
            OnPropertyChanged(nameof(QueryPlaceholder));
            //Notify the binder that the context has changed
            OnPropertyChanged(nameof(CurrentViewContext));
            // Force the FlowDocument to refresh instructions if the log is empty
            if (string.IsNullOrEmpty(SearchLog))
            {
                OnPropertyChanged(nameof(SearchLog));
            }
        }

        public string QueryPlaceholder => IsUserMim ? "Enter a NetID..." :
                                          IsDeptSearch ? "Enter Department Number..." :
                                          IsDivSearch ? "Enter 4-character Division Code..." :
                                          "Enter Group Name or 4-digit Dept#...";

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

        private string _searchLog = string.Empty;
        public string SearchLog
        {
            get => _searchLog;
            private set { _searchLog = value; OnPropertyChanged(nameof(SearchLog)); }
        }

        public GroupViewModel(
            IADService adService,
            IDepartmentService deptService,
            ISettingsService settingsSvc,
            IOutputTextSettingsProvider notifier)
        {
            _ad = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
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

        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService _search, SearchTarget target)
        {
            Error = null;

            if (!string.IsNullOrEmpty(SearchLog))
            {
                AppendRaw("\n[cyan]────────── New Search ──────────[/cyan]\n");
            }

            if (!string.IsNullOrWhiteSpace(context?.Query))
                AppendRaw($"[cyan]Query:[/cyan] [red]{context.Query}[/red]");

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
                AppendRaw($"[cyan]{QueryPlaceholder}[/cyan]");
                return;
            }

            Error = null;
            IsLoading = true;

            try
            {
                AppendRaw("[green]Starting search...[/green]");
                Log.Debug("GroupView", $"Mode={_searchMode}, Query='{Query}'");

                switch (_searchMode)
                {
                    case GroupSearchMode.UserMim:
                        await SearchUserMimGroups(Query);
                        break;
                    case GroupSearchMode.GroupMembers:
                        await SearchGroupMembers(Query);
                        break;
                    case GroupSearchMode.Department:
                        await SearchDepartmentSupport(Query);
                        break;
                    case GroupSearchMode.Division:
                        await SearchDivisionSupport(Query);
                        break;
                }
            }
            catch (Exception ex)
            {
                Error = $"Search failed: {ex.Message}";
                AppendRaw($"[red]Exception during search: {ex.Message}[/red]");
                Log.Error("GroupView", "Search failed", ex);
            }
            finally
            {
                IsLoading = false;
                AppendRaw("[green]Search completed.[/green]\n");
            }
        }

        // Logic: User MIM Groups
        private async Task SearchUserMimGroups(string query)
        {
            AppendRaw(string.Empty);

            AppendTitle($"MIM groups for user '{query}'");
            var r = await _ad.GetUserMimGroupsAsync(query);

            if (!r.Exists)
            {
                Error = string.IsNullOrWhiteSpace(r.Error) ? $"'{query}' is not a valid NetID." : r.Error;
                AppendRaw($"[red]{Error}[/red]");
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

            AppendRaw(string.Empty);
        }

        // Logic: Group Members
        private async Task SearchGroupMembers(string query)
        {
            AppendRaw(string.Empty);

            var groupName = NormalizeGroupName(query);
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
            }
            else
            {
                Error = info.ErrorMessage ?? "Group not found or lookup failed.";
                AppendRaw($"[red]{Error}[/red]");
            }

            AppendRaw(string.Empty);
        }

        // Logic: Department Support
        private async Task SearchDepartmentSupport(string deptNumber)
        {
            AppendRaw($"[gray]Looking up department '{deptNumber}'...[/gray]");

            var dept = await _deptService.GetDepartmentAsync(deptNumber);

            if (dept == null)
            {
                Error = $"Department '{deptNumber}' not found in configuration.";
                AppendRaw($"[red]{Error}[/red]");
                return;
            }

            AppendRaw(string.Empty);

            AppendTitle($"Department: {dept.Number}");

            if (!string.IsNullOrWhiteSpace(dept.Notes))
                AppendLabelValue("Notes: ", dept.Notes);

            var teamName = await _deptService.GetTeamAsync(dept.Number);
            if (!string.IsNullOrWhiteSpace(teamName))
            {
                AppendRaw($"[cyan]Assigned Team: [/cyan][red]{teamName}[/red]");

                var teamInfo = await _deptService.GetSupportTeamAsync(teamName);
                if (teamInfo != null)
                {
                    if (!string.IsNullOrWhiteSpace(teamInfo.ManagerName))
                    {
                        var mgr = teamInfo.ManagerName;
                        if (!string.IsNullOrWhiteSpace(teamInfo.ManagerNetID)) mgr += $" ({teamInfo.ManagerNetID})";
                        AppendLabelValue("Manager: ", mgr);
                    }

                    if (!string.IsNullOrWhiteSpace(teamInfo.PhoneNumber))
                        AppendLabelValue("Support Phone: ", teamInfo.PhoneNumber);
                }
            }
            else
            {
                AppendLabelValue("Assigned Team: ", "None", treatEmptyAsNone: false);
            }

            if (!string.IsNullOrWhiteSpace(dept.FileRepoPath))
            {
                AppendRaw($"[cyan]File Repository: [/cyan][red][Open Location]({dept.FileRepoPath})[/red]");
            }

            AppendRaw(string.Empty);
        }
        //Division search
        private async Task SearchDivisionSupport(string divCode)
        {
            AppendRaw($"[gray]Looking up support for division '{divCode}'...[/gray]");
            var teams = (await _deptService.GetTeamsByDivisionAsync(divCode)).ToList();

            if (teams.Count == 0)
            {
                Error = $"No division '{divCode}' found.";
                AppendRaw($"[red]{Error}[/red]");
                return;
            }

            foreach (var team in teams)
            {
                AppendRaw(string.Empty);
                AppendTitle($"Support Team: {team.SupportTeamName}");

                if (!string.IsNullOrWhiteSpace(team.ManagerName))
                {
                    var mgr = team.ManagerName;
                    if (!string.IsNullOrWhiteSpace(team.ManagerNetID)) mgr += $" ({team.ManagerNetID})";
                    AppendLabelValue("Manager: ", mgr);
                }

                if (!string.IsNullOrWhiteSpace(team.PhoneNumber))
                    AppendLabelValue("Support Phone: ", team.PhoneNumber);
            }
        }

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

        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _notifier.Changed -= OnFontSettingsChanged;
            }
            _disposed = true;
        }
    }
}