using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System.Diagnostics;
using System.IO;

namespace DSAMVVM.MVVM.ViewModel
{
    public class UserViewModel : ObeservableObject, ISearchableViewModel, IDisposable
    {
        // Services
        private readonly IADService _adService;
        private readonly IDepartmentService _deptService;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;

        // Per-view font context
        private const string ViewKey = "UserView";

        // UI state
        private double _effectiveFontSize = 14;
        public double EffectiveFontSize
        {
            get => _effectiveFontSize;
            private set { _effectiveFontSize = value; OnPropertyChanged(nameof(EffectiveFontSize)); }
        }

        private string? _error;
        public string? Error
        {
            get => _error;
            private set { _error = value; OnPropertyChanged(nameof(Error)); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); }
        }

        private string _searchLog = string.Empty;
        public string SearchLog
        {
            get => _searchLog;
            private set { _searchLog = value; OnPropertyChanged(nameof(SearchLog)); }
        }

        public UserViewModel(
            IADService adService,
            IDepartmentService deptService,
            ISettingsService settingsSvc,
            IOutputTextSettingsProvider notifier)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));

            _notifier.Changed += OnFontSettingsChanged;
            RefreshEffectiveFontSize();
        }

        private void OnFontSettingsChanged(object? sender, EventArgs e) => RefreshEffectiveFontSize();

        private void RefreshEffectiveFontSize()
        {
            EffectiveFontSize = _notifier.GetFontSize(ViewKey);
        }

        // Output helpers
        private void AppendRaw(string message)
        {
            SearchLog += message + "\n";
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

        // Search entry point for this view
        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;

            if (!string.IsNullOrEmpty(SearchLog))
            {
                AppendRaw("\n[cyan]────────── New Search ──────────[/cyan]\n");
                if (!string.IsNullOrWhiteSpace(context.Query))
                    AppendRaw($"[cyan]Query:[/cyan] [red]{context.Query}[/red]");
            }
            else if (!string.IsNullOrWhiteSpace(context.Query))
            {
                AppendRaw($"[cyan]Query:[/cyan] [red]{context.Query}[/red]");
            }

            Log.Info("UserView", $"Search started: target={target}, query='{context.Query}'");

            if (target != SearchTarget.User)
            {
                Error = "Invalid search target provided to UserViewModel.";
                AppendRaw("[red]Invalid search target for UserViewModel[/red]");
                Log.Warn("UserView", $"Invalid target: {target}");
                return;
            }

            if (string.IsNullOrWhiteSpace(context.Query))
            {
                AppendRaw("[cyan]Query was null or whitespace.[/cyan]");
                Log.Info("UserView", "Aborted: empty query");
                return;
            }

            try
            {
                IsLoading = true;
                AppendRaw("[green]Starting user search...[/green]");
                Log.Debug("UserView", "Dispatching directory search");

                var user = await searchService.SearchAsync(context, target) as ADUserInfo;

                if (user is null || !user.Exists)
                {
                    Error = user?.ErrorMessage ?? "User not found.";
                    AppendRaw($"[red]Search complete. User not found. Error: {Error}[/red]");
                    Log.Info("UserView", $"Not found. Error='{Error}'");
                    return;
                }

                AppendRaw(string.Empty);
                AppendTitle(user.DisplayName);

                if (!string.IsNullOrEmpty(user.EduAffiliation))
                    AppendLabelValue("Affiliation: ", user.EduAffiliation);

                if (!string.IsNullOrEmpty(user.Division))
                    AppendLabelValue("Division: ", user.Division);

                if (!string.IsNullOrEmpty(user.DepartmentName))
                    AppendLabelValue("Department: ", user.DepartmentName);

                if (user.Enabled == false)
                    AppendLabelValue("Enabled: ", "False", treatEmptyAsNone: false);

                if (user.Locked == true)
                    AppendLabelValue("Locked: ", "True", treatEmptyAsNone: false);

                AppendLabelValue("O365 Licensing: ", user.License, treatEmptyAsNone: false);

                Log.Info("UserView",
                    $"User found: DisplayName='{user.DisplayName}', Affiliation='{user.EduAffiliation}', Division='{user.Division}', DeptName='{user.DepartmentName}', Enabled={user.Enabled}, Locked={(user.Locked.HasValue ? user.Locked.ToString() : "null")}, License='{user.License}', DeptNum='{user.DepartmentNumber}'");

                if (!string.IsNullOrEmpty(user.DepartmentNumber))
                {
                    var dept = await _deptService.GetDepartmentAsync(user.DepartmentNumber);

                    if (dept != null)
                    {
                        var team = await _deptService.GetTeamAsync(dept.Number);
                        var teamName = string.IsNullOrWhiteSpace(team) ? null : team.Trim();

                        if (!string.IsNullOrWhiteSpace(teamName))
                            AppendLabelValue("Support Team: ", teamName);
                        else
                            AppendLabelValue("Teams: ", "None", treatEmptyAsNone: false);

                        var repoPath = await _deptService.GetFileRepoPathAsync(dept.Number);
                        if (!string.IsNullOrWhiteSpace(repoPath))
                            AppendLabelValue("File Repository: ", repoPath, treatEmptyAsNone: false);

                        if (!string.IsNullOrEmpty(dept.Notes))
                            AppendLabelValue("Notes: ", dept.Notes, treatEmptyAsNone: false);

                        Log.Info("UserView",
                            $"Dept info: Number='{dept.Number}', SupportKnown={dept.SupportKnown}, Team='{teamName ?? "(none)"}', Repo='{(string.IsNullOrWhiteSpace(repoPath) ? "(none)" : repoPath)}'");
                        Log.Debug("UserView", $"Dept notes length={(dept.Notes?.Length ?? 0)}");
                    }
                    else
                    {
                        AppendRaw("[cyan]Department information not found in cache.[/cyan]");
                        Log.Info("UserView", $"Dept cache miss: '{user.DepartmentNumber}'");
                    }
                }

                AppendRaw(string.Empty);
                Log.Info("UserView", "Search completed");
            }
            catch (Exception ex)
            {
                Error = $"Search failed: {ex.Message}";
                AppendRaw($"[red]Exception during user search: {ex}[/red]");
                Log.Error("UserView", "Search failed", ex);
            }
            finally
            {
                IsLoading = false;
                AppendRaw("[green]User search process completed.[/green]\n");
            }
        }

        public Task<string?> LookupNameByID(string id) => _adService.LookupNameByEmployeeID(id);

        public void ClearLog() => SearchLog = string.Empty;

        // Font control actions; respects per-view override setting
        public void AdjustFont(int delta)
        {
            var s = App.Settings;
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;

            _ = _settingsSvc.AdjustOutputFontSize(s, preferPerView ? ViewKey : null, delta, preferPerView);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        public void ResetFont()
        {
            var s = App.Settings;
            bool preferPerView = s.Ui.Font.ViewFontSizeOverride;

            _settingsSvc.ResetOutputFontSize(s, preferPerView ? ViewKey : null, preferPerView, defaultSize: 14);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        public void Dispose()
        {
            _notifier.Changed -= OnFontSettingsChanged;
        }
    }
}
