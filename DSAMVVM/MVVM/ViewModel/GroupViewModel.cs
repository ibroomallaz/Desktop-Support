using System.IO;
using System.Windows.Input;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Renderers;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;

namespace DSAMVVM.MVVM.ViewModel
{
    public class GroupViewModel : ObservableObject, ISearchableViewModel, IDisposable
    {
        // Services
        private readonly IADService _ad;
        private readonly IDepartmentService _deptService;
        private readonly ISettingsService _settingsSvc;
        private readonly IOutputTextSettingsProvider _notifier;
        private readonly IFlowDocService _flowDoc;
        private readonly IDeepLinkRoutingService _linkRouter;

        private const string ViewKey = "GroupView";
        private string? _currentSearchQuery;

        // --- Mode State (Restored for Radio Buttons) ---
        public enum GroupSearchMode
        {
            UserMim,
            GroupMembers,
            Department,
            Division
        }

        private GroupSearchMode _searchMode = GroupSearchMode.UserMim;
        public string CurrentViewContext => $"GroupView.{_searchMode}";

        public string QueryPlaceholder => IsUserMim ? "Enter a NetID..." :
                                          IsDeptSearch ? "Enter Department Number..." :
                                          IsDivSearch ? "Enter 4-character Division Code..." :
                                          "Enter Group Name or 4-digit Dept#...";

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

        private void UpdateMode(GroupSearchMode newMode)
        {
            if (_searchMode == newMode) return;

            _searchMode = newMode;

            OnPropertyChanged(nameof(IsUserMim));
            OnPropertyChanged(nameof(IsGroupMembers));
            OnPropertyChanged(nameof(IsDeptSearch));
            OnPropertyChanged(nameof(IsDivSearch));
            OnPropertyChanged(nameof(QueryPlaceholder));
            OnPropertyChanged(nameof(CurrentViewContext));

            if (string.IsNullOrEmpty(SearchLog))
            {
                OnPropertyChanged(nameof(SearchLog));
            }
        }

        // --- Standard UI State ---
        private string? _error;
        public string? Error { get => _error; private set { _error = value; OnPropertyChanged(nameof(Error)); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; private set { _isLoading = value; OnPropertyChanged(nameof(IsLoading)); } }

        private string _searchLog = string.Empty;
        public string SearchLog { get => _searchLog; private set { _searchLog = value; OnPropertyChanged(nameof(SearchLog)); } }

        private double _effectiveFontSize = 14;
        public double EffectiveFontSize
        {
            get => _effectiveFontSize;
            private set { _effectiveFontSize = value; OnPropertyChanged(nameof(EffectiveFontSize)); }
        }

        // --- Commands ---
        public ICommand ClearLogCommand { get; }
        public ICommand IncreaseFontCommand { get; }
        public ICommand DecreaseFontCommand { get; }
        public ICommand ResetFontCommand { get; }

        public GroupViewModel(
            IADService adService,
            IDepartmentService deptService,
            ISettingsService settingsSvc,
            IOutputTextSettingsProvider notifier,
            IFlowDocService flowDoc,
            IDeepLinkRoutingService linkRouter)
        {
            _ad = adService ?? throw new ArgumentNullException(nameof(adService));
            _deptService = deptService ?? throw new ArgumentNullException(nameof(deptService));
            _settingsSvc = settingsSvc ?? throw new ArgumentNullException(nameof(settingsSvc));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _flowDoc = flowDoc ?? throw new ArgumentNullException(nameof(flowDoc));
            _linkRouter = linkRouter ?? throw new ArgumentNullException(nameof(linkRouter));

            RefreshEffectiveFont();
            _notifier.Changed += OnOutputFontSettingsChanged;
            _flowDoc.LinkClicked += OnLinkClicked;

            ClearLogCommand = new RelayCommand(_ => ClearLog());
            IncreaseFontCommand = new RelayCommand(_ => AdjustFont(+1));
            DecreaseFontCommand = new RelayCommand(_ => AdjustFont(-1));
            ResetFontCommand = new RelayCommand(_ => ResetFont());
        }

        // --- Deep Link Handler ---
        private async void OnLinkClicked(object? sender, string url)
        {
            string? sourceView = sender as string;
            if (sourceView == null || !sourceView.StartsWith("GroupView")) return;

            if (IsLoading) return;

            string result = await _linkRouter.HandleLinkAsync(url);
            if (!string.IsNullOrWhiteSpace(result))
            {
                SearchLog += result;
            }
        }

        private void OnOutputFontSettingsChanged(object? sender, EventArgs e) => RefreshEffectiveFont();
        private void RefreshEffectiveFont() => EffectiveFontSize = _notifier.GetFontSize(ViewKey);

        private void AdjustFont(int delta)
        {
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;
            _ = _settingsSvc.AdjustOutputFontSize(s, perView ? ViewKey : null, delta, perView);
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
            _notifier.NotifyChanged();
        }

        private void ResetFont()
        {
            var s = App.Settings;
            bool perView = s.Ui.Font.ViewFontSizeOverride;
            _settingsSvc.ResetOutputFontSize(s, perView ? ViewKey : null, perView, 14);
            _settingsSvc.RequestSave(s, Path.Combine(Globals.g_AppDir, "settings.json"));
            _notifier.NotifyChanged();
        }

        // --- Search Flow ---
        public async Task OnSearchUpdated(SearchContextDTO context, ISearchService searchService, SearchTarget target)
        {
            Error = null;
            _currentSearchQuery = context.Query;

            var headerDoc = new FlowDocMarkupBuilder();
            if (!string.IsNullOrEmpty(SearchLog)) headerDoc.AddHeader("New Search");

            if (string.IsNullOrWhiteSpace(context.Query))
            {
                Error = "Empty query.";
                headerDoc.AddError("Aborted: Empty query.");
                SearchLog += headerDoc.ToString();
                return;
            }

            headerDoc.AddLabelValue("Query: ", context.Query);
            SearchLog += headerDoc.ToString();

            try
            {
                IsLoading = true;
                Log.Info(ViewKey, $"Starting Group search for '{context.Query}'. Mode: {_searchMode}");

                // Route the search directly based on the Radio Button selected
                switch (_searchMode)
                {
                    case GroupSearchMode.UserMim:
                        var mimResult = await _ad.GetUserMimGroupsAsync(context.Query);
                        SearchLog += IdentityRenderer.RenderMimGroups(mimResult, context.Query);
                        break;

                    case GroupSearchMode.GroupMembers:
                        var groupName = NormalizeGroupName(context.Query);
                        var adGroup = await _ad.GetGroupAsync(groupName);
                        SearchLog += IdentityRenderer.RenderGroupMembers(adGroup, groupName);
                        break;

                    case GroupSearchMode.Department:
                        var deptLog = await OrganizationalRenderer.RenderDepartmentContextAsync(context.Query, _deptService);
                        if (string.IsNullOrWhiteSpace(deptLog))
                        {
                            var errDoc = new FlowDocMarkupBuilder();
                            errDoc.AddError($"Department '{context.Query}' not found in configuration.");
                            SearchLog += errDoc.ToString();
                        }
                        else
                        {
                            var titleDoc = new FlowDocMarkupBuilder();
                            titleDoc.AddRaw(string.Empty);
                            titleDoc.AddTitle($"Department: {context.Query}");
                            SearchLog += titleDoc.ToString() + deptLog;
                        }
                        break;

                    case GroupSearchMode.Division:
                        SearchLog += await OrganizationalRenderer.RenderDivisionSupportAsync(context.Query, _deptService);
                        break;
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Log.Error(ViewKey, $"Exception during group search for '{context.Query}'", ex);

                var failDoc = new FlowDocMarkupBuilder();
                failDoc.AddError($"Search failed: {ex.Message}");
                SearchLog += failDoc.ToString();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void ClearLog() => SearchLog = string.Empty;

        private static string NormalizeGroupName(string input)
        {
            var s = input.Trim();
            if (s.Length == 4 && int.TryParse(s, out _)) return $"UA-MIM-0{s}";
            return s;
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
                _notifier.Changed -= OnOutputFontSettingsChanged;
                _flowDoc?.LinkClicked -= OnLinkClicked;
            }
            _disposed = true;
        }
    }
}