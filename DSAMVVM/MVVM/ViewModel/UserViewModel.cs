using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Renderers;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System.IO;
using System.Text;

namespace DSAMVVM.MVVM.ViewModel
{
    public class UserViewModel : ObservableObject, ISearchableViewModel, IDisposable
    {
        private readonly IADService _adService;
        private readonly IDepartmentService _deptService;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;
        private readonly IFlowDocService _flowDoc;

        private const string ViewKey = "UserView";
        private string? _currentSearchNetId;

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
                    catch
                    {
                        var errDoc = new FlowDocMarkupBuilder();
                        errDoc.AddError("Error: Could not decode raw license data.");
                        SearchLog += errDoc.ToString();
                    }
                }
            }
        }

        private void ShowRawLicenseInfo(string netid, string rawLicense)
        {
            var doc = new FlowDocMarkupBuilder();
            doc.AddRaw(string.Empty);
            doc.AddTitle($"Raw AD License Attribute for {netid}:");
            doc.AddDim(rawLicense);
            doc.AddRaw(string.Empty);
            SearchLog += doc.ToString();
        }

        private async Task PerformAdobeCheckAsync(string netid)
        {
            if (string.IsNullOrWhiteSpace(netid)) return;

            var doc = new FlowDocMarkupBuilder();
            doc.AddRaw(string.Empty);
            doc.AddTitle($"Adobe Licenses ({netid}):");

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

            doc.AddRaw($"[cyan]  Acrobat Pro: [/cyan][{acroColor}]{acroIcon} {acroText}[/{acroColor}]");
            doc.AddRaw($"[cyan]  Creative Cloud: [/cyan][{ccColor}]{ccIcon} {ccText}[/{ccColor}]");
            doc.AddRaw(string.Empty);

            SearchLog += doc.ToString();
        }

        private async Task ShowTeamInfoAsync(string teamName)
        {
            var doc = new FlowDocMarkupBuilder();
            doc.AddRaw(string.Empty);

            var team = await _deptService.GetSupportTeamAsync(teamName);
            if (team == null)
            {
                doc.AddError("Team not found.");
                SearchLog += doc.ToString();
                return;
            }

            doc.AddTitle($"Team: {team.SupportTeamName}");
            if (!string.IsNullOrWhiteSpace(team.ManagerName))
            {
                doc.AddRaw($"[red]Manager: {team.ManagerName} [/red][gray]([/gray][red]{team.ManagerNetID}[/red][gray])[/gray]");
            }
            if (!string.IsNullOrWhiteSpace(team.PhoneNumber)) doc.AddLabelValue("Phone: ", team.PhoneNumber);

            if (team.SupportedDivisions != null && team.SupportedDivisions.Count > 0)
            {
                doc.AddRaw("[cyan]Supported Divisions:[/cyan]");
                foreach (var div in team.SupportedDivisions)
                {
                    doc.AddRaw($"[gray]  • [/gray][red]{div.DivAbbrev}[/red] [gray]-[/gray] [red]{div.DivFullName}[/red]");
                }
            }
            doc.AddRaw(string.Empty);
            SearchLog += doc.ToString();
        }

        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;
            _currentSearchNetId = context.Query;

            IsAdobeCheckComplete = false;
            HasAcrobatPro = false;
            HasCreativeCloud = false;

            var headerDoc = new FlowDocMarkupBuilder();
            if (!string.IsNullOrEmpty(SearchLog)) headerDoc.AddHeader("New Search");
            if (!string.IsNullOrWhiteSpace(context.Query)) headerDoc.AddLabelValue("Query: ", context.Query);

            SearchLog += headerDoc.ToString();

            if (target != SearchTarget.User || string.IsNullOrWhiteSpace(context.Query))
            {
                Error = "Invalid target or empty query.";

                var errDoc = new FlowDocMarkupBuilder();
                errDoc.AddError("Aborted: Invalid search parameters.");
                SearchLog += errDoc.ToString();
                return;
            }

            try
            {
                IsLoading = true;

                var loadDoc = new FlowDocMarkupBuilder();
                loadDoc.AddSuccess("Starting user search...");
                SearchLog += loadDoc.ToString();

                var user = await searchService.SearchAsync(context, target) as ADUserInfo;

                // 1. Render the Identity Data (Requires ONLY the user)
                SearchLog += IdentityRenderer.RenderADUser(user);

                // 2. Render the Organizational Data (Requires BOTH the Dept Number and _deptService)
                if (user != null && user.Exists && !string.IsNullOrEmpty(user.DepartmentNumber))
                {
                    SearchLog += await OrganizationalRenderer.RenderDepartmentContextAsync(user.DepartmentNumber, _deptService);
                }

                if (user is null || !user.Exists)
                {
                    Error = user?.ErrorMessage ?? "User not found.";
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;

                var failDoc = new FlowDocMarkupBuilder();
                failDoc.AddError($"Search failed: {ex.Message}");
                SearchLog += failDoc.ToString();
            }
            finally
            {
                IsLoading = false;

                var compDoc = new FlowDocMarkupBuilder();
                compDoc.AddSuccess("User search process completed.");
                SearchLog += compDoc.ToString();
            }
        }

        public async Task RefreshDepartmentDataAsync()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;
            try
            {
                var startDoc = new FlowDocMarkupBuilder();
                startDoc.AddSuccess("Refreshing department data…");
                SearchLog += startDoc.ToString();

                UiNotify.Info("Refreshing department data…", showStatusBar: true, key: "DeptRefresh");
                await _deptService.ReloadDataAsync();

                var endDoc = new FlowDocMarkupBuilder();
                endDoc.AddSuccess("Department data refresh completed.");
                SearchLog += endDoc.ToString();
            }
            catch (Exception ex)
            {
                var errDoc = new FlowDocMarkupBuilder();
                errDoc.AddError($"Refresh failed: {ex.Message}");
                SearchLog += errDoc.ToString();
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