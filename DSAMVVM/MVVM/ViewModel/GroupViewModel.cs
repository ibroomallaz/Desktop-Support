using DSAMVVM.MVVM.ViewModel.Cards;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using DSAMVVM.Core.Enums;
using DSAMVVM.Core.Interfaces;
using DSAMVVM.Core.Interfaces.AD;
using DSAMVVM.Core.Interfaces.Integrations;
using DSAMVVM.Core.Interfaces.UI;
using DSAMVVM.Core.Logging;
using DSAMVVM.Core.Models;
using DSAMVVM.Core.Renderers;
using DSAMVVM.Core.Utilities;
using DSAMVVM.MVVM.Model;
using DSAMVVM.MVVM.Model.AD;
using DSAMVVM.MVVM.Model.Config.UI;
using DSAMVVM.MVVM.Model.Data;

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

        // Modern Card Feed History
        public ObservableCollection<GroupHistoryItemViewModel> History { get; } = new();
        public bool HasHistory => History.Count > 0;

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

        private double _effectiveFontSize = UiLimits.DefaultFontSize;
        public double EffectiveFontSize
        {
            get => _effectiveFontSize;
            private set
            {
                if (Math.Abs(_effectiveFontSize - value) > 0.01)
                {
                    _effectiveFontSize = value;
                    OnPropertyChanged(nameof(EffectiveFontSize));
                    OnPropertyChanged(nameof(Typography));
                }
            }
        }

        public CardTypography Typography => CardTypography.FromBase(EffectiveFontSize);

        // --- Commands ---
        public ICommand ClearCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand RemoveHistoryItemCommand { get; }
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

            ClearCommand = new RelayCommand(_ => ClearLog());
            ClearLogCommand = new RelayCommand(_ => ClearLog());
            CollapseAllCommand = new RelayCommand(_ => CollapseAll());
            ExpandAllCommand = new RelayCommand(_ => ExpandAll());
            RemoveHistoryItemCommand = new RelayCommand(param =>
            {
                if (param is GroupHistoryItemViewModel item)
                {
                    History.Remove(item);
                }
            });
            IncreaseFontCommand = new RelayCommand(_ => AdjustFont(+1));
            DecreaseFontCommand = new RelayCommand(_ => AdjustFont(-1));
            ResetFontCommand = new RelayCommand(_ => ResetFont());

            History.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasHistory));

            RefreshEffectiveFont();
            _notifier.Changed += OnOutputFontSettingsChanged;
            _flowDoc.LinkClicked += OnLinkClicked;
        }

        // --- Deep Link Handler ---
        private async void OnLinkClicked(object? sender, string url)
        {
            if (sender is not string sourceView || !sourceView.StartsWith("GroupView")) return;

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
            _settingsSvc.ResetOutputFontSize(s, perView ? ViewKey : null, perView, (int)UiLimits.DefaultFontSize);
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

            // Auto-switch mode if user pasted/clicked a UA- group while in UserMim mode
            if (_searchMode == GroupSearchMode.UserMim &&
                (context.Query.StartsWith("UA-", StringComparison.OrdinalIgnoreCase) || context.Query.StartsWith("UA_", StringComparison.OrdinalIgnoreCase)))
            {
                UpdateMode(GroupSearchMode.GroupMembers);
            }

            headerDoc.AddLabelValue("Query: ", context.Query);
            SearchLog += headerDoc.ToString();

            searchService.AddToHistory(context.Query, target);

            try
            {
                IsLoading = true;
                Log.Info(ViewKey, $"Starting Group search for '{context.Query}'. Mode: {_searchMode}");

                // Collapse previous cards so newest expands
                foreach (var item in History)
                {
                    item.IsExpanded = false;
                }

                // Route the search directly based on the Radio Button selected
                switch (_searchMode)
                {
                    case GroupSearchMode.UserMim:
                    {
                        var mimResult = await _ad.GetUserMimGroupsAsync(context.Query);
                        SearchLog += IdentityRenderer.RenderMimGroups(mimResult, context.Query);

                        var entry = GroupHistoryItemViewModel.CreateUserMim(context.Query, mimResult, _ad, _deptService, _linkRouter);
                        entry.IsExpanded = true;
                        AddHistoryEntry(entry);
                        break;
                    }

                    case GroupSearchMode.GroupMembers:
                    {
                        var groupName = NormalizeGroupName(context.Query);
                        var adGroup = await _ad.GetGroupAsync(groupName);
                        SearchLog += IdentityRenderer.RenderGroupMembers(adGroup, groupName);

                        var entry = GroupHistoryItemViewModel.CreateGroupMembers(context.Query, groupName, adGroup, _ad, _deptService, _linkRouter);
                        entry.IsExpanded = true;
                        AddHistoryEntry(entry);
                        break;
                    }

                    case GroupSearchMode.Department:
                    {
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

                        var dept = await _deptService.GetDepartmentAsync(context.Query);
                        SupportTeam? team = null;
                        if (dept != null)
                        {
                            var teamName = await _deptService.GetTeamAsync(dept.Number);
                            if (!string.IsNullOrWhiteSpace(teamName))
                            {
                                team = await _deptService.GetSupportTeamAsync(teamName.Trim());
                            }
                        }

                        var entry = GroupHistoryItemViewModel.CreateDepartment(context.Query, dept, team, _ad, _deptService, _linkRouter);
                        entry.IsExpanded = true;
                        AddHistoryEntry(entry);
                        break;
                    }

                    case GroupSearchMode.Division:
                    {
                        SearchLog += await OrganizationalRenderer.RenderDivisionSupportAsync(context.Query, _deptService);

                        var teams = await _deptService.GetTeamsByDivisionAsync(context.Query);
                        var entry = GroupHistoryItemViewModel.CreateDivision(context.Query, teams, _ad, _deptService, _linkRouter);
                        entry.IsExpanded = true;
                        AddHistoryEntry(entry);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Error = ex.Message;
                Log.Error(ViewKey, $"Exception during group search for '{context.Query}'", ex);

                var failDoc = new FlowDocMarkupBuilder();
                failDoc.AddError($"Search failed: {ex.Message}");
                SearchLog += failDoc.ToString();

                foreach (var item in History)
                {
                    item.IsExpanded = false;
                }

                var failEntry = _searchMode switch
                {
                    GroupSearchMode.UserMim => GroupHistoryItemViewModel.CreateUserMim(context.Query, new MimLookupResult { Exists = false, Error = ex.Message }, _ad, _deptService, _linkRouter),
                    GroupSearchMode.GroupMembers => GroupHistoryItemViewModel.CreateGroupMembers(context.Query, NormalizeGroupName(context.Query), new ADGroupInfo { Exists = false, ErrorMessage = ex.Message }, _ad, _deptService, _linkRouter),
                    GroupSearchMode.Department => GroupHistoryItemViewModel.CreateDepartment(context.Query, null, null, _ad, _deptService, _linkRouter),
                    GroupSearchMode.Division => GroupHistoryItemViewModel.CreateDivision(context.Query, null, _ad, _deptService, _linkRouter),
                    _ => GroupHistoryItemViewModel.CreateUserMim(context.Query, new MimLookupResult { Exists = false, Error = ex.Message }, _ad, _deptService, _linkRouter)
                };
                failEntry.IsExpanded = true;
                AddHistoryEntry(failEntry);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void AddHistoryEntry(GroupHistoryItemViewModel entry)
        {
            entry.RemoveRequested += item => History.Remove(item);
            History.Add(entry);
        }

        public void CollapseAll()
        {
            foreach (var item in History)
            {
                item.IsExpanded = false;
            }
        }

        public void ExpandAll()
        {
            foreach (var item in History)
            {
                item.IsExpanded = true;
            }
        }

        public void ClearLog()
        {
            SearchLog = string.Empty;
            History.Clear();
        }

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
                _flowDoc.LinkClicked -= OnLinkClicked;
            }
            _disposed = true;
        }
    }
}
