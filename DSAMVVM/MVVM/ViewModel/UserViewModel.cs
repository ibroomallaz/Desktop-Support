using System.IO;
using System.Text;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;

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

        private const string ViewKey = "UserView";

        // Context tracking
        private string? _currentSearchNetId;

        // Adobe Licensing State
        private bool _hasAcrobatPro;
        public bool HasAcrobatPro
        {
            get => _hasAcrobatPro;
            private set { _hasAcrobatPro = value; OnPropertyChanged(nameof(HasAcrobatPro)); }
        }

        private bool _hasCreativeCloud;
        public bool HasCreativeCloud
        {
            get => _hasCreativeCloud;
            private set { _hasCreativeCloud = value; OnPropertyChanged(nameof(HasCreativeCloud)); }
        }

        private bool _isAdobeCheckComplete;
        public bool IsAdobeCheckComplete
        {
            get => _isAdobeCheckComplete;
            private set { _isAdobeCheckComplete = value; OnPropertyChanged(nameof(IsAdobeCheckComplete)); }
        }

        // UI State
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
            IFlowDocService flowDoc)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _flowDoc = flowDoc ?? throw new ArgumentNullException(nameof(flowDoc));

            _notifier.Changed += OnFontSettingsChanged;
            _flowDoc.LinkClicked += OnLinkClicked;

            RefreshEffectiveFontSize();
        }

        // --- Link Routing ---
        private async void OnLinkClicked(object? sender, string url)
        {
            if (url.StartsWith("dsa://team/", StringComparison.OrdinalIgnoreCase))
            {
                var teamName = Uri.UnescapeDataString(url["dsa://team/".Length..]);
                await ShowTeamInfoAsync(teamName);
            }
            else if (url.StartsWith("dsa://license/adobe/", StringComparison.OrdinalIgnoreCase))
            {
                var targetNetId = url["dsa://license/adobe/".Length..];
                await PerformAdobeCheckAsync(targetNetId);
            }
            else if (url.StartsWith("dsa://license/o365/", StringComparison.OrdinalIgnoreCase))
            {
                var segments = url["dsa://license/o365/".Length..].Split('/');
                if (segments.Length >= 2)
                {
                    var netid = segments[0];
                    var base64Data = segments[1];
                    try
                    {
                        var rawLicense = Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
                        ShowRawLicenseInfo(netid, rawLicense);
                    }
                    catch { AppendRaw("[red]Error: Could not decode raw license data.[/red]"); }
                }
            }
        }

        private void ShowRawLicenseInfo(string netid, string rawLicense)
        {
            AppendRaw(string.Empty);
            AppendRaw($"[yellow]Raw AD License Attribute for {netid}:[/yellow]");
            AppendRaw($"[lightgray]{rawLicense}[/lightgray]");
            AppendRaw(string.Empty);
        }

        private async Task PerformAdobeCheckAsync(string netid)
        {
            if (string.IsNullOrWhiteSpace(netid)) return;

            AppendRaw(string.Empty);
            AppendRaw($"[yellow]Adobe Licenses ({netid}):[/yellow]");

            var status = await _adService.CheckAdobeLicensesAsync(netid);

            if (netid.Equals(_currentSearchNetId, StringComparison.OrdinalIgnoreCase))
            {
                HasAcrobatPro = status.HasAcrobatPro;
                HasCreativeCloud = status.HasCreativeCloud;
                IsAdobeCheckComplete = true;
            }

            string acroColor = status.HasAcrobatPro ? "green" : "red";
            string acroIcon = status.HasAcrobatPro ? "✓" : "✗";
            string acroText = status.HasAcrobatPro ? "Assigned" : "None";

            string ccColor = status.HasCreativeCloud ? "green" : "red";
            string ccIcon = status.HasCreativeCloud ? "✓" : "✗";
            string ccText = status.HasCreativeCloud ? "Assigned" : "None";

            AppendRaw($"[cyan]  Acrobat Pro: [/cyan][{acroColor}]{acroIcon} {acroText}[/{acroColor}]");
            AppendRaw($"[cyan]  Creative Cloud: [/cyan][{ccColor}]{ccIcon} {ccText}[/{ccColor}]");
            AppendRaw(string.Empty);
        }

        // --- Core Search Logic ---
        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;
            _currentSearchNetId = context.Query;

            IsAdobeCheckComplete = false;
            HasAcrobatPro = false;
            HasCreativeCloud = false;

            if (!string.IsNullOrEmpty(SearchLog))
            {
                AppendRaw("\n[cyan]────────── New Search ──────────[/cyan]\n");
            }
            AppendRaw($"[cyan]Query:[/cyan] [red]{context.Query}[/red]");

            try
            {
                IsLoading = true;
                var user = await searchService.SearchAsync(context, target) as ADUserInfo;

                if (user is null || !user.Exists)
                {
                    Error = user?.ErrorMessage ?? "User not found.";
                    AppendRaw($"[red]Search complete. User not found. Error: {Error}[/red]");
                    return;
                }

                AppendRaw(string.Empty);
                AppendTitle(user.DisplayName);

                if (!string.IsNullOrEmpty(user.EduAffiliation)) AppendLabelValue("Affiliation: ", user.EduAffiliation);
                if (!string.IsNullOrEmpty(user.Division)) AppendLabelValue("Division: ", user.Division);
                if (!string.IsNullOrEmpty(user.DepartmentName)) AppendLabelValue("Department: ", user.DepartmentName);
                if (user.Enabled == false) AppendLabelValue("Enabled: ", "False", false);
                if (user.Locked == true) AppendLabelValue("Locked: ", "True", false);

                AppendRaw("[cyan]Software Licenses:[/cyan]");

                if (!string.IsNullOrWhiteSpace(user.RawLicense))
                {
                    var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(user.RawLicense));
                    var o365Url = $"dsa://license/o365/{user.Name}/{b64}";
                    AppendRaw($"[lightgray]   • [/lightgray][cyan]O365: [/cyan][red][{user.License}]({o365Url})[/red]");
                }
                else
                {
                    AppendRaw($"[lightgray]   • [/lightgray][cyan]O365: [/cyan][red]{user.License ?? "None"}[/red]");
                }

                AppendRaw($"[lightgray]   • [/lightgray][cyan]Adobe: [/cyan][red][Check](dsa://license/adobe/{user.Name})[/red]");

                if (!string.IsNullOrEmpty(user.DepartmentNumber))
                {
                    var dept = await _deptService.GetDepartmentAsync(user.DepartmentNumber);
                    if (dept != null)
                    {
                        var team = await _deptService.GetTeamAsync(dept.Number);
                        if (!string.IsNullOrWhiteSpace(team))
                        {
                            var url = $"dsa://team/{Uri.EscapeDataString(team.Trim())}";
                            AppendRaw($"[cyan]Support Team: [/cyan][red][{team.Trim()}]({url})[/red]");
                        }
                        var repoPath = await _deptService.GetFileRepoPathAsync(dept.Number);
                        if (!string.IsNullOrWhiteSpace(repoPath)) AppendLabeledLink("File Repository: ", "Open Repository", repoPath);
                        if (!string.IsNullOrEmpty(dept.Notes)) AppendLabelValue("Notes: ", dept.Notes, false);
                    }
                }
                AppendRaw(string.Empty);
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                AppendRaw($"[red]Search failed: {ex.Message}[/red]");
            }
            finally
            {
                IsLoading = false;
                AppendRaw("[green]User search process completed.[/green]\n");
            }
        }

        // --- Code-Behind Support ---
        public async Task RefreshDepartmentDataAsync()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;
            try
            {
                AppendRaw("[green]Refreshing department data…[/green]");
                UiNotify.Info("Refreshing department data…", showStatusBar: true, key: "DeptRefresh");
                await _deptService.ReloadDataAsync();
                AppendRaw("[green]Department data refresh completed.[/green]");
            }
            catch (Exception ex)
            {
                AppendRaw($"[red]Refresh failed: {ex.Message}[/red]");
            }
            finally { IsRefreshing = false; }
        }

        public void AdjustFont(int delta)
        {
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;
            _settingsSvc.AdjustOutputFontSize(s, perView ? ViewKey : null, delta, perView);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        public void ResetFont()
        {
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;
            _settingsSvc.ResetOutputFontSize(s, perView ? ViewKey : null, perView, 14);
            _notifier.NotifyChanged();
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
        }

        public Task<string?> LookupNameByID(string id) => _adService.LookupNameByEmployeeID(id);
        public void ClearLog() => SearchLog = string.Empty;

        // --- UI Helpers ---
        private void AppendRaw(string msg) => SearchLog += msg + "\n";
        private void AppendTitle(string? t) => AppendRaw($"[yellow]{t}[/yellow]");
        private void AppendLabelValue(string l, string? v, bool tNone = true)
        {
            var val = (string.IsNullOrWhiteSpace(v) && tNone) ? "None" : v;
            if (val != null) AppendRaw($"[cyan]{l}[/cyan][red]{val}[/red]");
        }
        private void AppendLabeledLink(string lp, string lt, string target) =>
            AppendRaw($"[cyan]{lp}[/cyan][red][{lt}]({target})[/red]");

        private async Task ShowTeamInfoAsync(string teamName)
        {
            AppendRaw(string.Empty);
            var team = await _deptService.GetSupportTeamAsync(teamName);
            if (team == null) { AppendRaw("[red]Team not found.[/red]"); return; }
            AppendTitle($"Team: {team.SupportTeamName}");
            if (!string.IsNullOrWhiteSpace(team.ManagerName)) AppendLabelValue("Manager: ", team.ManagerName);
            AppendRaw(string.Empty);
        }

        private void OnFontSettingsChanged(object? s, EventArgs e) => RefreshEffectiveFontSize();
        private void RefreshEffectiveFontSize() => EffectiveFontSize = _notifier.GetFontSize(ViewKey);

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _notifier.Changed -= OnFontSettingsChanged;
            _flowDoc.LinkClicked -= OnLinkClicked;
            _disposed = true;
        }
    }
}