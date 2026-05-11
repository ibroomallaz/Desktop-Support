using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Renderers;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using System.IO;

namespace DSAMVVM.MVVM.ViewModel
{
    public class UserViewModel : ObservableObject, ISearchableViewModel, IDisposable
    {
        private readonly IADService _adService;
        private readonly IDepartmentService _deptService;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;
        private readonly IFlowDocService _flowDoc;
        private readonly IDeepLinkRoutingService _linkRouter;

        private const string ViewKey = "UserView";

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
            IFlowDocService flowDoc,
            IDeepLinkRoutingService linkRouter)
        {
            _adService = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _flowDoc = flowDoc ?? throw new ArgumentNullException(nameof(flowDoc));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));

            _notifier.Changed += OnFontSettingsChanged;
            _flowDoc.LinkClicked += OnLinkClicked;

            RefreshEffectiveFontSize();
        }

        private async void OnLinkClicked(object? sender, string url)
        {
            if (IsLoading) return;

            string result = await _linkRouter.HandleLinkAsync(url);
            if (!string.IsNullOrWhiteSpace(result))
            {
                SearchLog += result;
            }
        }

        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;

            var headerDoc = new FlowDocMarkupBuilder();
            if (!string.IsNullOrEmpty(SearchLog)) headerDoc.AddHeader("New Search");
            if (!string.IsNullOrWhiteSpace(context.Query)) headerDoc.AddLabelValue("Query: ", context.Query);

            SearchLog += headerDoc.ToString();

            if (target != SearchTarget.User || string.IsNullOrWhiteSpace(context.Query))
            {
                Error = "Invalid target or empty query.";
                Log.Warn(ViewKey, $"Search aborted: Invalid target ({target}) or empty query.");

                var errDoc = new FlowDocMarkupBuilder();
                errDoc.AddError("Aborted: Invalid search parameters.");
                SearchLog += errDoc.ToString();
                return;
            }

            try
            {
                IsLoading = true;
                Log.Info(ViewKey, $"Starting User search for '{context.Query}'");

                var user = await searchService.SearchAsync(context, target) as ADUserInfo;

                SearchLog += IdentityRenderer.RenderADUser(user);

                if (user != null && user.Exists)
                {
                    Log.Info(ViewKey, $"User '{user.Name}' found successfully.");

                    if (!string.IsNullOrEmpty(user.DepartmentNumber))
                    {
                        SearchLog += await OrganizationalRenderer.RenderDepartmentContextAsync(user.DepartmentNumber, _deptService);
                    }
                }
                else
                {
                    Error = user?.ErrorMessage ?? "User not found.";
                    Log.Warn(ViewKey, $"Search completed, but user '{context.Query}' was not found. Error: {Error}");
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Log.Error(ViewKey, $"Exception during user search for '{context.Query}'", ex);

                var failDoc = new FlowDocMarkupBuilder();
                failDoc.AddError($"Search failed: {ex.Message}");
                SearchLog += failDoc.ToString();
            }
            finally
            {
                IsLoading = false;
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

            GC.SuppressFinalize(this);
        }
    }
}