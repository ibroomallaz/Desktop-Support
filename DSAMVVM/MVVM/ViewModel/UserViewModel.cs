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
        private readonly IFlowDocService _flowDoc;

        // Per-view font context
        private const string ViewKey = "UserView";

        // State for linking
        private string? _currentRawLicense;

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

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set { _isRefreshing = value; OnPropertyChanged(nameof(IsRefreshing)); }
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
                    IOutputTextSettingsProvider notifier,
                    IFlowDocService flowDoc) // Inject
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _flowDoc = flowDoc ?? throw new ArgumentNullException(nameof(flowDoc));

            _notifier.Changed += OnFontSettingsChanged;

            // Subscribe to Link Clicks
            _flowDoc.LinkClicked += OnLinkClicked;

            RefreshEffectiveFontSize();
        }

        private async void OnLinkClicked(object? sender, string url)
        {
            if (url.StartsWith("dsa://team/", StringComparison.OrdinalIgnoreCase))
            {
                var encodedName = url["dsa://team/".Length..];
                var teamName = Uri.UnescapeDataString(encodedName);
                await ShowTeamInfoAsync(teamName);
            }
            else if (url.Equals("dsa://license/show", StringComparison.OrdinalIgnoreCase))
            {
                // Show raw license info
                if (!string.IsNullOrWhiteSpace(_currentRawLicense))
                {
                    AppendRaw(string.Empty);
                    AppendRaw("[yellow]Raw AD License Attribute:[/yellow]");
                    AppendRaw($"[lightgray]{_currentRawLicense}[/lightgray]");
                    AppendRaw(string.Empty);
                }
                else
                {
                    AppendRaw("[red]No raw license data available.[/red]");
                }
            }
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

        private void AppendLabeledLink(string labelPrefix, string labelText, string pathOrUrl)
        {
            if (string.IsNullOrWhiteSpace(pathOrUrl))
            {
                AppendRaw($"[cyan]{labelPrefix}[/cyan][red]None[/red]");
                return;
            }

            string linkTarget;

            if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var u))
            {
                linkTarget = u.AbsoluteUri;
            }
            else if (pathOrUrl.StartsWith(@"\\") || Path.IsPathRooted(pathOrUrl))
            {
                try
                {
                    var fu = new Uri(pathOrUrl, UriKind.Absolute);
                    linkTarget = fu.AbsoluteUri;
                }
                catch
                {
                    linkTarget = pathOrUrl;
                }
            }
            else
            {
                linkTarget = pathOrUrl;
            }

            AppendRaw($"[cyan]{labelPrefix}[/cyan][red][{labelText}]({linkTarget})[/red]");
        }

        private async Task ShowTeamInfoAsync(string teamName)
        {
            AppendRaw(string.Empty);
            AppendRaw($"[green]Fetching info for team: {teamName}...[/green]");

            var team = await _deptService.GetSupportTeamAsync(teamName);

            if (team == null)
            {
                AppendRaw($"[red]Team details not found.[/red]");
                return;
            }

            AppendTitle($"Team: {team.SupportTeamName}");

            if (!string.IsNullOrWhiteSpace(team.ManagerName))
            {
                var mgr = team.ManagerName;
                if (!string.IsNullOrWhiteSpace(team.ManagerNetID)) mgr += $" ({team.ManagerNetID})";
                AppendLabelValue("Manager: ", mgr);
            }

            if (!string.IsNullOrWhiteSpace(team.PhoneNumber))
            {
                AppendLabelValue("Support Phone: ", team.PhoneNumber);
            }

            if (team.SupportedDivisions != null && team.SupportedDivisions.Count > 0)
            {
                AppendRaw("[cyan]Supported Divisions:[/cyan]");

                // Sort: Abbrev first, then Name
                var sortedDivs = team.SupportedDivisions
                    .OrderBy(d => d.DivAbbrev)
                    .ThenBy(d => d.DivFullName);

                foreach (var div in sortedDivs)
                {
                    // Format: Abbrev - Name (Gray)
                    AppendRaw($"[lightgray]   • {div.DivAbbrev} - {div.DivFullName}[/lightgray]");
                }
            }
            else
            {
                AppendRaw("[gray](No specific divisions listed)[/gray]");
            }
            AppendRaw(string.Empty);
        }

        // Search entry point
        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;
            _currentRawLicense = null; // Reset previous raw data

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

                // Store raw license for linking
                _currentRawLicense = user.RawLicense;

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

                // --- License Output with Link ---
                if (!string.IsNullOrWhiteSpace(user.RawLicense))
                {
                    // Render as a clickable link to show raw data
                    AppendRaw($"[cyan]O365 Licensing: [/cyan][red][{user.License}](dsa://license/show)[/red]");
                }
                else
                {
                    AppendLabelValue("O365 Licensing: ", user.License, treatEmptyAsNone: false);
                }

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
                        {
                            var url = $"dsa://team/{Uri.EscapeDataString(teamName)}";
                            AppendRaw($"[cyan]Support Team: [/cyan][red][{teamName}]({url})[/red]");
                        }
                        else
                        {
                            AppendLabelValue("Teams: ", "None", treatEmptyAsNone: false);
                        }

                        var repoPath = await _deptService.GetFileRepoPathAsync(dept.Number);
                        if (!string.IsNullOrWhiteSpace(repoPath))
                            AppendLabeledLink("File Repository: ", "Open File Repository", repoPath);

                        if (!string.IsNullOrEmpty(dept.Notes))
                            AppendLabelValue("Notes: ", dept.Notes, treatEmptyAsNone: false);

                        Log.Info("UserView",
                            $"Dept info: Number='{dept.Number}', SupportKnown={dept.SupportKnown}, Team='{teamName ?? "(none)"}', Repo='{(string.IsNullOrWhiteSpace(repoPath) ? "(none)" : repoPath)}'");
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

        // Refresh department JSON: status bar + view log + app log
        public async Task RefreshDepartmentDataAsync()
        {
            const string StatusKey = "DeptRefresh";
            if (IsRefreshing)
            {
                AppendRaw("[cyan]Busy: department refresh already in progress.[/cyan]");
                return;
            }

            IsRefreshing = true;
            var sw = Stopwatch.StartNew();

            try
            {
                AppendRaw("[green]Refreshing department data…[/green]");
                UiNotify.Info("Refreshing department data…", showStatusBar: true, key: StatusKey);
                Log.Info("UserView", "Department refresh started.");

                await _deptService.ReloadDataAsync();

                sw.Stop();
                var msg = $"Department data refresh completed in {sw.ElapsedMilliseconds} ms.";
                AppendRaw($"[green]{msg}[/green]");
                UiNotify.Info(msg, showStatusBar: true, key: StatusKey);
                Log.Info("UserView", msg);
            }
            catch (Exception ex)
            {
                sw.Stop();
                var em = $"Department data refresh failed: {ex.Message}";
                AppendRaw($"[red]{em}[/red]");
                UiNotify.Error("Department data refresh failed", ex.Message, alsoStatusBar: true, key: StatusKey);
                Log.Error("UserView", "Department refresh failed", ex);
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        public Task<string?> LookupNameByID(string id) => _adService.LookupNameByEmployeeID(id);

        public void ClearLog() => SearchLog = string.Empty;

        // Font controls
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

        // Dispose pattern
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
                _flowDoc.LinkClicked -= OnLinkClicked;
            }
            _disposed = true;
        }
    }
}